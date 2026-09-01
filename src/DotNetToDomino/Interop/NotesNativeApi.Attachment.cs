using System.Buffers.Binary;
using System.Runtime.InteropServices;

namespace DotNetToDomino.Interop;

internal sealed partial class NotesNativeApi
{
    private const string AttachmentItemName = "$FILE";
    private const int FileObjectFixedSize = 36;
    private const ushort NotesTypeObject = 0x0003;
    private const ushort HotspotTypeFile = 4;
    private const ushort SigCdHotspotBegin = unchecked((ushort)-87);
    private const ushort SigCdV4HotspotBegin = unchecked((ushort)-83);
    private const ushort SigCdV5HotspotBegin = unchecked((ushort)-130);

    internal bool SaveAttachment(nint note, string attachmentName, string outputPath) => SaveAttachmentCore(note, attachmentName, outputPath);

    internal bool SaveRichTextAttachment(nint note, string richTextItemName, string attachmentName, string outputPath)
    {
        EnsureInitialized();
        using var thread = EnterThread();
        richTextItemName = richTextItemName.Trim();
        attachmentName = attachmentName.Trim();
        outputPath = outputPath.Trim();
        if (richTextItemName.Length == 0 || attachmentName.Length == 0 || outputPath.Length == 0) return false;
        if (!TryResolveRichTextAttachment(note, richTextItemName, attachmentName, out var internalName)) return false;
        return SaveAttachmentCore(note, internalName, outputPath);
    }

    private bool SaveAttachmentCore(nint note, string attachmentName, string outputPath)
    {
        EnsureInitialized();
        attachmentName = attachmentName.Trim();
        outputPath = outputPath.Trim();
        if (attachmentName.Length == 0 || outputPath.Length == 0) return false;
        if (!TryFindAttachment(note, attachmentName, out var itemBlock)) return false;

        if (Directory.Exists(outputPath) || outputPath.EndsWith(Path.DirectorySeparatorChar) || outputPath.EndsWith(Path.AltDirectorySeparatorChar))
            outputPath = Path.Combine(outputPath, Path.GetFileName(attachmentName));
        else
            outputPath = Path.GetFullPath(outputPath);

        var directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
        using var path = ToLmbcs(outputPath);
        CheckStatus(Resolve<Attachment_NSFNoteExtractFile>("NSFNoteExtractFile")(note, itemBlock, path.Pointer, 0), "NSFNoteExtractFile");
        return true;
    }

    private bool TryFindAttachment(nint note, string attachmentName, out NotesBlockId itemBlock)
    {
        itemBlock = default;
        using var fileItemName = ToLmbcs(AttachmentItemName);
        var status = Resolve<Attachment_NSFItemInfo>("NSFItemInfo")(
            note, fileItemName.Pointer, checked((ushort)fileItemName.Length),
            out var currentItem, out var dataType, out var valueBlock, out var valueLength);
        if (status != 0) return false;

        while (true)
        {
            if (dataType == NotesTypeObject && TryReadAttachmentName(valueBlock, valueLength, out var storedName) &&
                string.Equals(storedName, attachmentName, StringComparison.OrdinalIgnoreCase))
            {
                itemBlock = currentItem;
                return true;
            }

            status = Resolve<Attachment_NSFItemInfoNext>("NSFItemInfoNext")(
                note, currentItem, fileItemName.Pointer, checked((ushort)fileItemName.Length),
                out var nextItem, out dataType, out valueBlock, out valueLength);
            if (status != 0) return false;
            currentItem = nextItem;
        }
    }

    private bool TryReadAttachmentName(NotesBlockId valueBlock, uint valueLength, out string name)
    {
        name = string.Empty;
        if (valueBlock.Pool == 0 || valueLength < 2 + FileObjectFixedSize) return false;
        var basePointer = Resolve<Attachment_OSLockObject>("OSLockObject")(valueBlock.Pool);
        if (basePointer == 0) return false;
        try
        {
            var valuePointer = nint.Add(basePointer, valueBlock.Block);
            var rawLength = checked((int)valueLength - 2);
            var raw = new byte[rawLength];
            Marshal.Copy(nint.Add(valuePointer, 2), raw, 0, rawLength);
            if (raw.Length < FileObjectFixedSize) return false;
            var objectType = BinaryPrimitives.ReadUInt16LittleEndian(raw.AsSpan(0, 2));
            if (objectType != 0) return false;
            var fileNameLength = BinaryPrimitives.ReadUInt16LittleEndian(raw.AsSpan(6, 2));
            if (fileNameLength == 0 || FileObjectFixedSize + fileNameLength > raw.Length) return false;
            var pointer = Marshal.AllocHGlobal(fileNameLength);
            try
            {
                Marshal.Copy(raw, FileObjectFixedSize, pointer, fileNameLength);
                name = FromLmbcs(pointer, fileNameLength);
                return name.Length > 0;
            }
            finally { Marshal.FreeHGlobal(pointer); }
        }
        finally { Resolve<Attachment_OSUnlockObject>("OSUnlockObject")(valueBlock.Pool); }
    }

    private bool TryResolveRichTextAttachment(nint note, string itemName, string requestedName, out string internalName)
    {
        internalName = string.Empty;
        using var itemNameText = ToLmbcs(itemName);
        var status = Resolve<Attachment_NSFItemInfo>("NSFItemInfo")(
            note, itemNameText.Pointer, checked((ushort)itemNameText.Length),
            out var currentItem, out var dataType, out var valueBlock, out var valueLength);
        if (status != 0) return false;

        while (true)
        {
            if (dataType == NotesTypeComposite && TryResolveAttachmentInComposite(valueBlock, valueLength, requestedName, out internalName)) return true;
            status = Resolve<Attachment_NSFItemInfoNext>("NSFItemInfoNext")(
                note, currentItem, itemNameText.Pointer, checked((ushort)itemNameText.Length),
                out var nextItem, out dataType, out valueBlock, out valueLength);
            if (status != 0) return false;
            currentItem = nextItem;
        }
    }

    private bool TryResolveAttachmentInComposite(NotesBlockId valueBlock, uint valueLength, string requestedName, out string internalName)
    {
        var resolved = string.Empty;
        Attachment_EnumCompositeAction callback = (record, signature, recordLength, _) =>
        {
            if (resolved.Length > 0 || record == 0 || recordLength < 12 || !IsHotspotBeginSignature(signature)) return 0;
            var hotspotType = unchecked((ushort)Marshal.ReadInt16(record, 4));
            if (hotspotType != HotspotTypeFile) return 0;
            var dataLength = unchecked((ushort)Marshal.ReadInt16(record, 10));
            if (dataLength == 0 || dataLength > recordLength - 12) return 0;
            var data = nint.Add(record, 12);
            var firstLength = ZeroTerminatedLength(data, dataLength);
            if (firstLength >= dataLength) return 0;
            var second = nint.Add(data, firstLength + 1);
            var remaining = checked((int)dataLength - firstLength - 1);
            var secondLength = ZeroTerminatedLength(second, remaining);
            var firstName = firstLength == 0 ? string.Empty : FromLmbcs(data, firstLength);
            var secondName = secondLength == 0 ? string.Empty : FromLmbcs(second, secondLength);
            if (string.Equals(firstName, requestedName, StringComparison.OrdinalIgnoreCase) || string.Equals(secondName, requestedName, StringComparison.OrdinalIgnoreCase))
                resolved = firstName.Length > 0 ? firstName : requestedName;
            return 0;
        };
        _ = Resolve<Attachment_EnumCompositeBuffer>("EnumCompositeBuffer")(valueBlock, valueLength, callback, 0);
        GC.KeepAlive(callback);
        internalName = resolved;
        return resolved.Length > 0;
    }

    private static bool IsHotspotBeginSignature(ushort signature) => signature is SigCdHotspotBegin or SigCdV4HotspotBegin or SigCdV5HotspotBegin;
    private static int ZeroTerminatedLength(nint pointer, int maximum) { var length = 0; while (length < maximum && Marshal.ReadByte(pointer, length) != 0) length++; return length; }

    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort Attachment_NSFItemInfo(nint note, nint itemName, ushort nameLength, out NotesBlockId itemBlock, out ushort dataType, out NotesBlockId valueBlock, out uint valueLength);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort Attachment_NSFItemInfoNext(nint note, NotesBlockId itemBlock, nint itemName, ushort nameLength, out NotesBlockId nextItem, out ushort dataType, out NotesBlockId valueBlock, out uint valueLength);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort Attachment_NSFNoteExtractFile(nint note, NotesBlockId itemBlock, nint fileName, nint decryptionKey);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate nint Attachment_OSLockObject(nint handle);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate void Attachment_OSUnlockObject(nint handle);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort Attachment_EnumCompositeBuffer(NotesBlockId itemValue, uint itemValueLength, Attachment_EnumCompositeAction action, nint context);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort Attachment_EnumCompositeAction(nint record, ushort signature, uint recordLength, nint context);
}
