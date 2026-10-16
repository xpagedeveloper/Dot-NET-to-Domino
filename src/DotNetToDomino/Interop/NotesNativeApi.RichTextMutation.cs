using System.Runtime.InteropServices;

namespace DotNetToDomino.Interop;

internal readonly record struct NotesRichTextRecordRewrite(int SegmentIndex, int RecordIndex, ushort Signature, byte[] Data)
{
    internal static NotesRichTextRecordRewrite Preserve(NotesRichTextRecordData record) =>
        new(record.SegmentIndex, record.RecordIndex, record.Signature, (byte[])record.Data.Clone());
}

internal static class NotesRichTextCdTransform
{
    internal static IReadOnlyList<NotesRichTextRecordRewrite> Compact(IReadOnlyList<NotesRichTextRecordData> records)
    {
        var result = new List<NotesRichTextRecordRewrite>();
        foreach (var record in records)
        {
            var next = NotesRichTextRecordRewrite.Preserve(record);
            // Join only immediately adjacent, identical-font CDTEXT records.
            // Keep paragraph, HTML, hotspot and unknown-record boundaries.
            // Physical item boundaries are storage details and may be coalesced.
            if (result.Count > 0 && next.Signature == 0xFF85 && next.Data.Length >= 8)
            {
                var previous = result[^1];
                var length = previous.Data.Length + next.Data.Length - 8;
                if (previous.Signature == next.Signature &&
                    previous.Data.Length >= 8 && length <= 16000 &&
                    previous.Data.AsSpan(4, 4).SequenceEqual(next.Data.AsSpan(4, 4)))
                {
                    var merged = new byte[length];
                    previous.Data.CopyTo(merged, 0);
                    next.Data.AsSpan(8).CopyTo(merged.AsSpan(previous.Data.Length));
                    merged[2] = (byte)length;
                    merged[3] = (byte)(length >> 8);
                    result[^1] = previous with { Data = merged };
                    continue;
                }
            }
            result.Add(next);
        }
        ValidateForPersistence(result);
        return result;
    }

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
        var flags = HasItem(note, itemName) ? GetItemMetadata(note, itemName).Flags : (ushort)0;
        var segmentCount = original.Count == 0 ? 0 : original.Max(r => r.SegmentIndex) + 1;
        DeleteCompositeItemsForRewrite(note, itemName, segmentCount);
        try
        {
            WriteCompositeRecordsForRewrite(note, itemName, replacement);
            RestoreCompositeFlags(note, itemName, flags);
        }
        catch
        {
            try
            {
                var replacementSegments = replacement.Count == 0 ? 0 : replacement.Max(r => r.SegmentIndex) + 1;
                DeleteCompositeItemsForRewrite(note, itemName, replacementSegments);
                WriteCompositeRecordsForRewrite(note, itemName, backup);
                RestoreCompositeFlags(note, itemName, flags);
            }
            catch { }
            throw;
        }
    }

    private void RestoreCompositeFlags(nint note, string itemName, ushort flags)
    {
        EnsureInitialized();
        using var thread = EnterThread();
        using var name = ToLmbcs(itemName);
        var status = Resolve<Attachment_NSFItemInfo>("NSFItemInfo")(note, name.Pointer, checked((ushort)name.Length),
            out var block, out var type, out var valueBlock, out var valueLength);
        var segments = new List<(NotesBlockId Block, ushort Flags, byte[] Data)>();
        while (status == 0)
        {
            if (type == NotesTypeComposite)
            {
                Resolve<ItemEx_NSFItemQuery>("NSFItemQuery")(note, block, 0, 0, out _, out var segmentFlags,
                    out _, out _, out _);
                var info = new NotesItemMetadata(itemName, block, valueBlock, segmentFlags, type, valueLength);
                segments.Add((block, segmentFlags, CopyItemValueWithoutType(info)));
            }
            status = Resolve<Attachment_NSFItemInfoNext>("NSFItemInfoNext")(note, block, name.Pointer, checked((ushort)name.Length),
                out var next, out type, out valueBlock, out valueLength);
            block = next;
        }
        if (segments.All(segment => segment.Flags == flags)) return;
        // Append/remove every segment in order: changing just the first one would
        // move it behind the continuation items and reorder the rich text.
        foreach (var segment in segments)
        {
            var data = Marshal.AllocHGlobal(Math.Max(1, segment.Data.Length));
            try
            {
                Marshal.Copy(segment.Data, 0, data, segment.Data.Length);
                CheckStatus(Resolve<Ops_NSFItemAppend>("NSFItemAppend")(note, flags, name.Pointer, checked((ushort)name.Length),
                    NotesTypeComposite, data, checked((uint)segment.Data.Length)), "NSFItemAppend(rich text flags)");
                RemoveItemByBlock(note, segment.Block);
            }
            finally { Marshal.FreeHGlobal(data); }
        }
    }

    private void DeleteCompositeItemsForRewrite(nint note, string itemName, int count)
    {
        if (count <= 0) return;
        using var name = ToLmbcs(itemName);
        for (var i = 0; i < count; i++)
        {
            var status = Resolve<Mutation_NSFItemDelete>("NSFItemDelete")(note, name.Pointer, checked((ushort)name.Length));
            // Notes may remove all same-name segments in the first deletion.
            if ((status & ErrMask) == ErrItemNotFound) break;
            CheckStatus(status, "NSFItemDelete(rich text rewrite)");
        }
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
