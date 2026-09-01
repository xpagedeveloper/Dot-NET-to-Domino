using System.Buffers.Binary;
using System.Runtime.InteropServices;

namespace DotNetToDomino.Interop;

internal readonly record struct NotesAttachmentMetadata(
    string Name,
    string Source,
    int CompressionType,
    long FileSize,
    NotesTimeDate FileCreated,
    NotesTimeDate FileModified,
    NotesBlockId ItemBlock);

internal sealed partial class NotesNativeApi
{
    internal bool TryGetRichTextAttachmentMetadata(nint note, string richTextItemName, string requestedName, out NotesAttachmentMetadata metadata)
    {
        EnsureInitialized();
        using var thread = EnterThread();
        metadata = default;
        if (!TryResolveRichTextAttachment(note, richTextItemName, requestedName, out var internalName)) return false;
        return TryGetAttachmentMetadata(note, internalName, out metadata);
    }

    internal IReadOnlyList<NotesAttachmentMetadata> GetRichTextAttachmentMetadata(nint note, string richTextItemName)
    {
        EnsureInitialized();
        using var thread = EnterThread();
        var names = GetRichTextAttachmentNames(note, richTextItemName);
        var result = new List<NotesAttachmentMetadata>(names.Count);
        foreach (var name in names)
            if (TryGetAttachmentMetadata(note, name, out var metadata)) result.Add(metadata);
        return result;
    }

    internal byte[] ReadAttachmentBytes(nint note, string attachmentName)
    {
        EnsureInitialized();
        using var thread = EnterThread();
        attachmentName = attachmentName.Trim();
        if (attachmentName.Length == 0 || !TryFindAttachment(note, attachmentName, out var itemBlock))
            throw new FileNotFoundException("Attachment was not found.", attachmentName);

        using var stream = new MemoryStream();
        Exception? callbackError = null;
        Embedded_NoteExtractCallback callback = (data, length, _) =>
        {
            if (length == 0) return 0;
            try
            {
                var buffer = new byte[checked((int)length)];
                Marshal.Copy(data, buffer, 0, buffer.Length);
                stream.Write(buffer, 0, buffer.Length);
                return 0;
            }
            catch (Exception ex)
            {
                callbackError = ex;
                return 1;
            }
        };

        var status = Resolve<Embedded_NSFNoteCipherExtractWithCallback>("NSFNoteCipherExtractWithCallback")(
            note, itemBlock, 0, 0, callback, 0, 0, 0);
        GC.KeepAlive(callback);
        if (callbackError is not null) throw new IOException("Attachment extraction callback failed.", callbackError);
        CheckStatus(status, "NSFNoteCipherExtractWithCallback");
        return stream.ToArray();
    }

    private bool TryGetAttachmentMetadata(nint note, string attachmentName, out NotesAttachmentMetadata metadata)
    {
        metadata = default;
        if (!TryFindAttachment(note, attachmentName, out var itemBlock)) return false;

        using var fileItemName = ToLmbcs(AttachmentItemName);
        var status = Resolve<Attachment_NSFItemInfo>("NSFItemInfo")(
            note, fileItemName.Pointer, checked((ushort)fileItemName.Length),
            out var currentItem, out var dataType, out var valueBlock, out var valueLength);
        if (status != 0) return false;

        while (true)
        {
            if (dataType == NotesTypeObject && currentItem.Pool == itemBlock.Pool && currentItem.Block == itemBlock.Block &&
                TryParseAttachmentMetadata(valueBlock, valueLength, currentItem, out metadata))
                return true;

            status = Resolve<Attachment_NSFItemInfoNext>("NSFItemInfoNext")(
                note, currentItem, fileItemName.Pointer, checked((ushort)fileItemName.Length),
                out var nextItem, out dataType, out valueBlock, out valueLength);
            if (status != 0) return false;
            currentItem = nextItem;
        }
    }

    private bool TryParseAttachmentMetadata(NotesBlockId valueBlock, uint valueLength, NotesBlockId itemBlock, out NotesAttachmentMetadata metadata)
    {
        metadata = default;
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
            if (BinaryPrimitives.ReadUInt16LittleEndian(raw.AsSpan(0, 2)) != 0) return false;

            var fileNameLength = BinaryPrimitives.ReadUInt16LittleEndian(raw.AsSpan(6, 2));
            if (fileNameLength == 0 || FileObjectFixedSize + fileNameLength > raw.Length) return false;
            var compression = BinaryPrimitives.ReadUInt16LittleEndian(raw.AsSpan(10, 2));
            var fileSize = BinaryPrimitives.ReadUInt32LittleEndian(raw.AsSpan(16, 4));
            var created = new NotesTimeDate
            {
                Innards0 = BinaryPrimitives.ReadUInt32LittleEndian(raw.AsSpan(20, 4)),
                Innards1 = BinaryPrimitives.ReadUInt32LittleEndian(raw.AsSpan(24, 4))
            };
            var modified = new NotesTimeDate
            {
                Innards0 = BinaryPrimitives.ReadUInt32LittleEndian(raw.AsSpan(28, 4)),
                Innards1 = BinaryPrimitives.ReadUInt32LittleEndian(raw.AsSpan(32, 4))
            };

            var namePointer = Marshal.AllocHGlobal(fileNameLength);
            try
            {
                Marshal.Copy(raw, FileObjectFixedSize, namePointer, fileNameLength);
                var name = FromLmbcs(namePointer, fileNameLength);
                metadata = new NotesAttachmentMetadata(name, name, compression, fileSize, created, modified, itemBlock);
                return name.Length > 0;
            }
            finally { Marshal.FreeHGlobal(namePointer); }
        }
        finally { Resolve<Attachment_OSUnlockObject>("OSUnlockObject")(valueBlock.Pool); }
    }

    private IReadOnlyList<string> GetRichTextAttachmentNames(nint note, string itemName)
    {
        var result = new List<string>();
        itemName = itemName.Trim();
        if (itemName.Length == 0) return result;
        using var itemNameText = ToLmbcs(itemName);
        var status = Resolve<Attachment_NSFItemInfo>("NSFItemInfo")(
            note, itemNameText.Pointer, checked((ushort)itemNameText.Length),
            out var currentItem, out var dataType, out var valueBlock, out var valueLength);
        if (status != 0) return result;

        while (true)
        {
            if (dataType == NotesTypeComposite) AppendRichTextAttachmentNames(valueBlock, valueLength, result);
            status = Resolve<Attachment_NSFItemInfoNext>("NSFItemInfoNext")(
                note, currentItem, itemNameText.Pointer, checked((ushort)itemNameText.Length),
                out var nextItem, out dataType, out valueBlock, out valueLength);
            if (status != 0) break;
            currentItem = nextItem;
        }
        return result;
    }

    private void AppendRichTextAttachmentNames(NotesBlockId valueBlock, uint valueLength, ICollection<string> destination)
    {
        Attachment_EnumCompositeAction callback = (record, signature, recordLength, _) =>
        {
            if (record == 0 || recordLength < 12 || !IsHotspotBeginSignature(signature)) return 0;
            var hotspotType = unchecked((ushort)Marshal.ReadInt16(record, 4));
            if (hotspotType != HotspotTypeFile) return 0;
            var dataLength = unchecked((ushort)Marshal.ReadInt16(record, 10));
            if (dataLength == 0 || dataLength > recordLength - 12) return 0;
            var data = nint.Add(record, 12);
            var nameLength = ZeroTerminatedLength(data, dataLength);
            if (nameLength <= 0) return 0;
            var name = FromLmbcs(data, nameLength);
            if (name.Length > 0 && !destination.Contains(name, StringComparer.OrdinalIgnoreCase)) destination.Add(name);
            return 0;
        };
        _ = Resolve<Attachment_EnumCompositeBuffer>("EnumCompositeBuffer")(valueBlock, valueLength, callback, 0);
        GC.KeepAlive(callback);
    }

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate ushort Embedded_NoteExtractCallback(nint data, uint length, nint parameter);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate ushort Embedded_NSFNoteCipherExtractWithCallback(
        nint note,
        NotesBlockId itemBlock,
        uint extractFlags,
        nint decryptionCipher,
        Embedded_NoteExtractCallback callback,
        nint parameter,
        uint reserved,
        nint reservedPointer);
}
