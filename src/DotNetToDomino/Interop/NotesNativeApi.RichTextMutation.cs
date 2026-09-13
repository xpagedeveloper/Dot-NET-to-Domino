using System.Runtime.InteropServices;

namespace DotNetToDomino.Interop;

internal readonly record struct NotesRichTextRecordRewrite(int SegmentIndex, int RecordIndex, ushort Signature, byte[] Data)
{
    internal static NotesRichTextRecordRewrite Preserve(NotesRichTextRecordData record) =>
        new(record.SegmentIndex, record.RecordIndex, record.Signature, (byte[])record.Data.Clone());
}

internal static class NotesRichTextCdTransform
{
    internal static IReadOnlyList<NotesRichTextRecordRewrite> Preserve(IReadOnlyList<NotesRichTextRecordData> records) =>
        Transform(records, static record => NotesRichTextRecordRewrite.Preserve(record));

    internal static IReadOnlyList<NotesRichTextRecordRewrite> Transform(
        IReadOnlyList<NotesRichTextRecordData> records,
        Func<NotesRichTextRecordData, NotesRichTextRecordRewrite?> transform)
    {
        ArgumentNullException.ThrowIfNull(records);
        ArgumentNullException.ThrowIfNull(transform);
        var result = new List<NotesRichTextRecordRewrite>(records.Count);
        var previousSegment = -1;
        var previousRecord = -1;
        foreach (var record in records)
        {
            if (record.SegmentIndex < previousSegment || (record.SegmentIndex == previousSegment && record.RecordIndex <= previousRecord))
                throw new NotesException(91, "Rich text CD records are not in physical item order.");
            var rewritten = transform(record);
            if (rewritten is { } value)
            {
                if (value.SegmentIndex != record.SegmentIndex || value.RecordIndex != record.RecordIndex)
                    throw new NotesException(5, "A CD transform cannot change physical record identity.");
                ValidateCanonicalRecord(value.Signature, value.Data);
                result.Add(value with { Data = (byte[])value.Data.Clone() });
            }
            previousSegment = record.SegmentIndex;
            previousRecord = record.RecordIndex;
        }
        return result;
    }

    internal static IReadOnlyList<byte[]> GroupCanonicalSegments(IReadOnlyList<NotesRichTextRecordRewrite> records)
    {
        if (records.Count == 0) return [];
        var segments = new List<byte[]>();
        var bytes = new List<byte>();
        var segmentIndex = records[0].SegmentIndex;
        var previousRecord = -1;
        foreach (var record in records)
        {
            ValidateCanonicalRecord(record.Signature, record.Data);
            if (record.SegmentIndex < segmentIndex || (record.SegmentIndex == segmentIndex && record.RecordIndex <= previousRecord))
                throw new NotesException(91, "Transformed CD records are not in physical item order.");
            if (record.SegmentIndex != segmentIndex)
            {
                segments.Add(bytes.ToArray());
                bytes.Clear();
                segmentIndex = record.SegmentIndex;
                previousRecord = -1;
            }
            bytes.AddRange(record.Data);
            if ((record.Data.Length & 1) != 0) bytes.Add(0);
            previousRecord = record.RecordIndex;
        }
        segments.Add(bytes.ToArray());
        return segments;
    }

    internal static void ValidateForPersistence(IReadOnlyList<NotesRichTextRecordRewrite> records)
    {
        var previousSegment = -1;
        var previousRecord = -1;
        foreach (var record in records)
        {
            if (record.SegmentIndex < 0 || record.RecordIndex < 0)
                throw new NotesException(5, "Rich text CD record identity cannot be negative.");
            if (record.SegmentIndex < previousSegment || (record.SegmentIndex == previousSegment && record.RecordIndex <= previousRecord))
                throw new NotesException(91, "Transformed CD records are not in physical item order.");
            ValidateCanonicalRecord(record.Signature, record.Data);
            previousSegment = record.SegmentIndex;
            previousRecord = record.RecordIndex;
        }
    }

    private static void ValidateCanonicalRecord(ushort signature, byte[]? data)
    {
        if (data is null || data.Length < 2) throw new NotesException(5, "A CD rewrite record must contain canonical bytes.");
        if (data[0] != (byte)(signature & 0xff)) throw new NotesException(5, "A CD rewrite signature must match the canonical record header.");
        if ((signature & 0xff00) == 0xff00 && data[1] != 0xff) throw new NotesException(5, "A WSIG CD rewrite record must contain the canonical WSIG header marker.");
    }
}

internal sealed partial class NotesNativeApi
{
    internal void ReplaceRichTextCdRecords(nint note, string itemName,
        IReadOnlyList<NotesRichTextRecordData> original,
        IReadOnlyList<NotesRichTextRecordRewrite> replacement)
    {
        EnsureInitialized();
        using var thread = EnterThread();
        NotesRichTextCdTransform.ValidateForPersistence(replacement);
        var backup = NotesRichTextCdTransform.Preserve(original);
        var segmentCount = original.Count == 0 ? 0 : original.Max(r => r.SegmentIndex) + 1;
        DeleteCompositeItemsForRewrite(note, itemName, segmentCount);
        try { WriteCompositeRecordsForRewrite(note, itemName, replacement); }
        catch
        {
            try
            {
                var replacementSegments = replacement.Count == 0 ? 0 : replacement.Max(r => r.SegmentIndex) + 1;
                DeleteCompositeItemsForRewrite(note, itemName, replacementSegments);
                WriteCompositeRecordsForRewrite(note, itemName, backup);
            }
            catch { }
            throw;
        }
    }

    private void DeleteCompositeItemsForRewrite(nint note, string itemName, int count)
    {
        if (count <= 0) return;
        using var name = ToLmbcs(itemName);
        for (var i = 0; i < count; i++)
            CheckStatus(Resolve<Mutation_NSFItemDelete>("NSFItemDelete")(note, name.Pointer, checked((ushort)name.Length)), "NSFItemDelete(rich text rewrite)");
    }

    private void WriteCompositeRecordsForRewrite(nint note, string itemName, IReadOnlyList<NotesRichTextRecordRewrite> records)
    {
        using var name = ToLmbcs(itemName);
        CheckStatus(Resolve<Mutation_CompoundTextCreate>("CompoundTextCreate")(unchecked((uint)note), name.Pointer, out var compound), "CompoundTextCreate(rewrite)");
        var closed = false;
        try
        {
            foreach (var segment in NotesRichTextCdTransform.GroupCanonicalSegments(records))
            {
                if (segment.Length == 0) continue;
                var buffer = Marshal.AllocHGlobal(segment.Length);
                try
                {
                    Marshal.Copy(segment, 0, buffer, segment.Length);
                    CheckStatus(Resolve<Mutation_CompoundTextAddCDRecords>("CompoundTextAddCDRecords")(compound, buffer, checked((uint)segment.Length)), "CompoundTextAddCDRecords");
                }
                finally { Marshal.FreeHGlobal(buffer); }
            }
            CheckStatus(Resolve<Mutation_CompoundTextClose>("CompoundTextClose")(compound, 0, 0, 0, 0), "CompoundTextClose(rewrite)");
            closed = true;
        }
        finally
        {
            if (!closed) try { Resolve<Mutation_CompoundTextDiscard>("CompoundTextDiscard")(compound); } catch { }
        }
    }

    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort Mutation_NSFItemDelete(nint note, nint itemName, ushort nameLength);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort Mutation_CompoundTextCreate(uint note, nint itemName, out uint compound);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort Mutation_CompoundTextAddCDRecords(uint compound, nint records, uint recordLength);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort Mutation_CompoundTextClose(uint compound, nint returnBuffer, nint returnBufferSize, nint returnFile, ushort returnFileNameSize);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate void Mutation_CompoundTextDiscard(uint compound);
}
