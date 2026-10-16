using System.Buffers.Binary;
using System.Runtime.InteropServices;

namespace DotNetToDomino.Interop;

internal sealed partial class NotesNativeApi
{
    private const byte RichTextAttachmentHotspotEndSignature = 170;
    private const ushort RichTextAttachmentCompressionHuffman = 1;

    internal NotesAttachmentMetadata AttachRichTextFile(nint note, string richTextItemName, string sourcePath, string? displayName = null)
    {
        EnsureInitialized();
        using var thread = EnterThread();
        richTextItemName = richTextItemName.Trim();
        sourcePath = sourcePath.Trim();
        if (richTextItemName.Length == 0) throw new ArgumentException("Rich text item name cannot be empty.", nameof(richTextItemName));
        if (sourcePath.Length == 0) throw new ArgumentException("Attachment source path cannot be empty.", nameof(sourcePath));

        sourcePath = Path.GetFullPath(sourcePath);
        if (!File.Exists(sourcePath)) throw new FileNotFoundException("Attachment source file was not found.", sourcePath);

        var sourceName = Path.GetFileName(sourcePath);
        var attachmentName = string.IsNullOrWhiteSpace(displayName) ? sourceName : displayName.Trim();
        if (sourceName.Length == 0) throw new ArgumentException("Attachment source must include a file name.", nameof(sourcePath));
        if (attachmentName.IndexOf('\0') >= 0) throw new ArgumentException("Attachment file name cannot contain NUL characters.", nameof(displayName));
        if (TryFindAttachment(note, attachmentName, out _)) throw new IOException($"An attachment named '{attachmentName}' already exists in the document.");

        using var itemName = ToLmbcs(AttachmentItemName);
        using var fileName = ToLmbcs(sourcePath);
        using var originalName = ToLmbcs(attachmentName);
        CheckStatus(Resolve<RichText_NSFNoteAttachFile>("NSFNoteAttachFile")(
            note,
            itemName.Pointer,
            checked((ushort)itemName.Length),
            fileName.Pointer,
            originalName.Pointer,
            RichTextAttachmentCompressionHuffman), "NSFNoteAttachFile");

        try
        {
            AppendRichTextAttachmentHotspot(note, richTextItemName, attachmentName, attachmentName);
            if (!TryGetAttachmentMetadata(note, attachmentName, out var metadata))
                throw new FileNotFoundException("Attached file metadata could not be resolved.", attachmentName);
            return metadata;
        }
        catch
        {
            if (TryFindAttachment(note, attachmentName, out var itemBlock))
            {
                try { _ = Resolve<RichText_NSFNoteDetachFile>("NSFNoteDetachFile")(note, itemBlock); }
                catch { }
            }
            throw;
        }
    }

    private void AppendRichTextAttachmentHotspot(nint note, string richTextItemName, string internalName, string sourceName)
    {
        using var itemName = ToLmbcs(richTextItemName);
        CheckStatus(Resolve<RichTextAttachment_CompoundTextCreate>("CompoundTextCreate")(
            checked((uint)note), itemName.Pointer, out var compound), "CompoundTextCreate");
        var closed = false;
        try
        {
            var records = BuildRichTextAttachmentHotspot(internalName, sourceName);
            var buffer = Marshal.AllocHGlobal(records.Length);
            try
            {
                Marshal.Copy(records, 0, buffer, records.Length);
                CheckStatus(Resolve<RichTextAttachment_CompoundTextAddCDRecords>("CompoundTextAddCDRecords")(
                    compound, buffer, checked((uint)records.Length)), "CompoundTextAddCDRecords");
            }
            finally { Marshal.FreeHGlobal(buffer); }

            CheckStatus(Resolve<RichTextAttachment_CompoundTextClose>("CompoundTextClose")(
                compound, 0, 0, 0, 0), "CompoundTextClose");
            closed = true;
        }
        finally
        {
            if (!closed)
            {
                try { Resolve<RichTextAttachment_CompoundTextDiscard>("CompoundTextDiscard")(compound); }
                catch { }
            }
        }
    }

    private byte[] BuildRichTextAttachmentHotspot(string internalName, string sourceName)
    {
        using var internalText = ToLmbcs(internalName);
        using var sourceText = ToLmbcs(sourceName);
        var dataLength = checked(internalText.Length + 1 + sourceText.Length + 1);
        var beginLength = checked(12 + dataLength);
        if (beginLength > ushort.MaxValue) throw new ArgumentOutOfRangeException(nameof(internalName), "Attachment hotspot name data exceeds the Notes CD record size limit.");

        var alignedBeginLength = (beginLength + 1) & ~1;
        var records = new byte[checked(alignedBeginLength + 2)];

        records[0] = unchecked((byte)SigCdHotspotBegin);
        records[1] = 0xff;
        BinaryPrimitives.WriteUInt16LittleEndian(records.AsSpan(2, 2), checked((ushort)beginLength));
        BinaryPrimitives.WriteUInt16LittleEndian(records.AsSpan(4, 2), HotspotTypeFile);
        BinaryPrimitives.WriteUInt32LittleEndian(records.AsSpan(6, 4), 0);
        BinaryPrimitives.WriteUInt16LittleEndian(records.AsSpan(10, 2), checked((ushort)dataLength));

        if (internalText.Length > 0) Marshal.Copy(internalText.Pointer, records, 12, internalText.Length);
        var sourceOffset = checked(12 + internalText.Length + 1);
        if (sourceText.Length > 0) Marshal.Copy(sourceText.Pointer, records, sourceOffset, sourceText.Length);

        records[alignedBeginLength] = RichTextAttachmentHotspotEndSignature;
        records[alignedBeginLength + 1] = 2;
        return records;
    }


    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate ushort RichText_NSFNoteAttachFile(
        nint note,
        nint itemName,
        ushort itemNameLength,
        nint fileName,
        nint originalPathName,
        ushort encodingType);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate ushort RichText_NSFNoteDetachFile(nint note, NotesBlockId itemBlock);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate ushort RichTextAttachment_CompoundTextCreate(uint note, nint itemName, out uint compound);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate ushort RichTextAttachment_CompoundTextAddCDRecords(uint compound, nint records, uint recordLength);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate ushort RichTextAttachment_CompoundTextClose(uint compound, nint returnBuffer, nint returnBufferSize, nint returnFile, ushort returnFileNameSize);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate void RichTextAttachment_CompoundTextDiscard(uint compound);
}
