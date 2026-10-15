using DotNetToDomino.Interop;

namespace DotNetToDomino;

public abstract class NotesRichTextLinkedObject : NotesObject
{
    protected readonly NotesRichTextItem RichTextItem;
    private readonly int _segmentIndex;
    private readonly int _recordIndex;
    private readonly ushort _signature;
    private long _revision;

    internal NotesRichTextLinkedObject(NotesSession session, NotesRichTextItem item, NotesRichTextRecordData record) : base(session)
    {
        RichTextItem = item;
        _segmentIndex = record.SegmentIndex;
        _recordIndex = record.RecordIndex;
        _signature = record.Signature;
        _revision = item.RichTextRevision;
    }

    public NotesRichTextItem Parent { get { EnsureLinkedAlive(); return RichTextItem; } }
    internal NotesRichTextItem PositionOwner { get { EnsureLinkedAlive(); return RichTextItem; } }
    internal NotesRichTextRecordData PositionRecord() => CurrentRecord();

    private protected NotesRichTextRecordData CurrentRecord()
    {
        EnsureLinkedAlive();
        return RichTextItem.ReadRichTextRecords().FirstOrDefault(value =>
            value.SegmentIndex == _segmentIndex && value.RecordIndex == _recordIndex && value.Signature == _signature)
            ?? throw new InvalidOperationException("The rich text element no longer exists in its parent item.");
    }

    private protected IReadOnlyList<NotesRichTextRecordData> Records() { EnsureLinkedAlive(); return RichTextItem.ReadRichTextRecords(); }

    private protected int CurrentFlatIndex(IReadOnlyList<NotesRichTextRecordData> records)
    {
        EnsureLinkedAlive();
        for (var i = 0; i < records.Count; i++)
        {
            var value = records[i];
            if (value.SegmentIndex == _segmentIndex && value.RecordIndex == _recordIndex && value.Signature == _signature) return i;
        }
        throw new InvalidOperationException("The rich text element no longer exists in its parent item.");
    }

    protected void RemoveRecordSpan(int start, int end)
    {
        EnsureLinkedAlive();
        var records = Records();
        if (start < 0 || end < start || end >= records.Count) throw new InvalidOperationException("The rich text element span is invalid.");
        var removed = records.Skip(start).Take(end - start + 1)
            .Select(record => (record.SegmentIndex, record.RecordIndex)).ToHashSet();
        var rewritten = NotesRichTextCdTransform.Transform(records, record =>
            removed.Contains((record.SegmentIndex, record.RecordIndex))
                ? null
                : NotesRichTextRecordRewrite.Preserve(record));
        RichTextItem.RewriteRichTextRecords(rewritten);
        Recycle();
    }

    protected void AcceptCurrentRevision() => _revision = RichTextItem.RichTextRevision;
    protected void EnsureLinkedAlive()
    {
        EnsureAlive();
        RichTextItem.EnsureRichTextAlive();
        if (_revision != RichTextItem.RichTextRevision)
            throw new InvalidOperationException("The rich text item has changed. Obtain the element again from a new navigator.");
    }
    protected override void ReleaseNative() { }
}

/// <summary>Represents a Notes color value used by rich-text linked objects.</summary>
public sealed class NotesColorObject : NotesObject
{
    private int _notesColor;
    internal NotesColorObject(NotesSession session, int notesColor) : base(session) => _notesColor = Math.Clamp(notesColor, 0, 255);
    public NotesSession Parent { get { EnsureAlive(); return Session; } }
    public int NotesColor
    {
        get { EnsureAlive(); return _notesColor; }
        set { EnsureAlive(); if (value < 0 || value > 255) throw new ArgumentOutOfRangeException(nameof(value)); _notesColor = value; }
    }
    protected override void ReleaseNative() { }
}

/// <summary>Represents a collapsible section in Notes rich text.</summary>
public sealed class NotesRichTextSection : NotesRichTextLinkedObject
{
    private const uint BarExpanded = 0x00000002u;
    private const uint BarIsFormula = 0x00002000u;
    private const uint BarHasColor = 0x04000000u;
    private const ushort BarSignature = unchecked((ushort)-84);
    private const ushort BarEndSignature = unchecked((ushort)-82);

    internal NotesRichTextSection(NotesSession session, NotesRichTextItem item, NotesRichTextRecordData record) : base(session, item, record) { }

    public bool IsExpanded
    {
        get { var data = CurrentRecord().Data; return data.Length >= 8 && (ReadUInt32(data, 4) & BarExpanded) != 0; }
        set
        {
            EnsureLinkedAlive();
            RewriteHeader(data =>
            {
                if (data.Length < 8) throw new NotesException(91, "Invalid rich-text section header.");
                var flags = ReadUInt32(data, 4);
                flags = value ? flags | BarExpanded : flags & ~BarExpanded;
                WriteUInt32(data, 4, flags);
                return data;
            });
        }
    }
    public string Title
    {
        get
        {
            var data = CurrentRecord().Data;
            if (data.Length <= 12) return string.Empty;
            var flags = ReadUInt32(data, 4);
            var offset = 12;
            if ((flags & BarHasColor) != 0) { if (data.Length < 14) return string.Empty; offset += 2; }
            if (offset >= data.Length) return string.Empty;
            return (flags & BarIsFormula) != 0
                ? Session.Api.DecompileRichTextFormula(data, offset, data.Length - offset)
                : Session.Api.DecodeRichTextText(data, offset, data.Length - offset).TrimEnd('\0');
        }
        set
        {
            EnsureLinkedAlive();
            ArgumentNullException.ThrowIfNull(value);
            var current = CurrentRecord().Data;
            if (current.Length < 12) throw new NotesException(91, "Invalid rich-text section header.");
            if ((ReadUInt32(current, 4) & BarIsFormula) != 0)
                throw new NotSupportedException("Changing a formula-based rich-text section title is not supported.");
            var offset = 12;
            if ((ReadUInt32(current, 4) & BarHasColor) != 0) offset += 2;
            var encoded = Session.Api.EncodeRichTextText(value + "\0");
            var data = new byte[offset + encoded.Length];
            current.AsSpan(0, Math.Min(offset, current.Length)).CopyTo(data);
            encoded.CopyTo(data, offset);
            RewriteHeader(_ => data);
        }
    }
    public NotesColorObject BarColor
    {
        get
        {
            var data = CurrentRecord().Data;
            if (data.Length < 14) return new NotesColorObject(Session, 0);
            var flags = ReadUInt32(data, 4);
            return new NotesColorObject(Session, (flags & BarHasColor) != 0 ? ReadUInt16(data, 12) : 0);
        }
    }
    public NotesRichTextStyle TitleStyle
    {
        get
        {
            var data = CurrentRecord().Data;
            var result = new NotesRichTextStyle(Session);
            if (data.Length < 12) return result;
            var attrib = data[9];
            result.NotesFont = data[8];
            result.NotesColor = data[10];
            if (data[11] > 0) result.FontSize = data[11];
            result.Bold = (attrib & 0x01) != 0 ? -1 : 0;
            result.Italic = (attrib & 0x02) != 0 ? -1 : 0;
            result.Underline = (attrib & 0x04) != 0 ? -1 : 0;
            result.Strikethrough = (attrib & 0x08) != 0 ? -1 : 0;
            if ((attrib & 0x10) != 0) result.Effects = 1;
            else if ((attrib & 0x20) != 0) result.Effects = 2;
            else if ((attrib & 0xE0) == 0x80) result.Effects = 3;
            else if ((attrib & 0xF0) == 0x90) result.Effects = 4;
            else if ((attrib & 0xF0) == 0xA0) result.Effects = 5;
            else result.Effects = 0;
            return result;
        }
    }

    public void Remove()
    {
        EnsureLinkedAlive();
        var records = Records();
        var start = CurrentFlatIndex(records);
        var end = ResolveSectionEnd(records, start);
        RemoveRecordSpan(start, end);
    }

    public void SetBarColor(NotesColorObject color)
    {
        EnsureLinkedAlive();
        ArgumentNullException.ThrowIfNull(color);
        RewriteHeader(data =>
        {
            if (data.Length < 12) throw new NotesException(91, "Invalid rich-text section header.");
            var flags = ReadUInt32(data, 4) | BarHasColor;
            if (data.Length < 14) Array.Resize(ref data, 14);
            WriteUInt32(data, 4, flags);
            WriteUInt16(data, 12, checked((ushort)color.NotesColor));
            return data;
        });
    }

    public void SetTitleStyle(NotesRichTextStyle style)
    {
        EnsureLinkedAlive();
        ArgumentNullException.ThrowIfNull(style);
        var state = style.ExportState();
        RewriteHeader(data =>
        {
            if (data.Length < 12) throw new NotesException(91, "Invalid rich-text section header.");
            if (state.NotesFont != NotesRichTextStyle.StyleNoChange) data[8] = checked((byte)state.NotesFont);
            var attrib = data[9];
            attrib = ApplyFlag(attrib, 0x01, state.Bold);
            attrib = ApplyFlag(attrib, 0x02, state.Italic);
            attrib = ApplyFlag(attrib, 0x04, state.Underline);
            attrib = ApplyFlag(attrib, 0x08, state.Strikethrough);
            if (state.Effects != NotesRichTextStyle.StyleNoChange)
            {
                attrib = (byte)(attrib & 0x0f);
                attrib |= state.Effects switch { 1 => (byte)0x10, 2 => (byte)0x20, 3 => (byte)0x80, 4 => (byte)0x90, 5 => (byte)0xa0, _ => (byte)0 };
            }
            data[9] = attrib;
            if (state.NotesColor != NotesRichTextStyle.StyleNoChange) data[10] = checked((byte)state.NotesColor);
            if (state.FontSize != NotesRichTextStyle.StyleNoChange) data[11] = checked((byte)state.FontSize);
            return data;
        });
    }

    private void RewriteHeader(Func<byte[], byte[]> transform)
    {
        var current = CurrentRecord();
        var records = Records();
        var rewritten = NotesRichTextCdTransform.Transform(records, record =>
        {
            if (record.SegmentIndex != current.SegmentIndex || record.RecordIndex != current.RecordIndex)
                return NotesRichTextRecordRewrite.Preserve(record);
            var data = transform((byte[])record.Data.Clone());
            FixRecordLength(data);
            return new NotesRichTextRecordRewrite(record.SegmentIndex, record.RecordIndex, record.Signature, data);
        });
        RichTextItem.RewriteRichTextRecords(rewritten);
        AcceptCurrentRevision();
    }

    private static int ResolveSectionEnd(IReadOnlyList<NotesRichTextRecordData> records, int start)
    {
        var depth = 1;
        for (var i = start + 1; i < records.Count; i++)
        {
            if (records[i].Signature == BarSignature) depth++;
            else if (records[i].Signature == BarEndSignature && --depth == 0) return i;
        }
        throw new NotesException(91, "Rich text section has no matching end record.");
    }

    private static byte ApplyFlag(byte value, byte mask, int setting)
    {
        if (setting == NotesRichTextStyle.StyleNoChange) return value;
        return setting != 0 ? (byte)(value | mask) : (byte)(value & ~mask);
    }
    private static ushort ReadUInt16(byte[] data, int offset) => (ushort)(data[offset] | (data[offset + 1] << 8));
    private static uint ReadUInt32(byte[] data, int offset) => (uint)(data[offset] | (data[offset + 1] << 8) | (data[offset + 2] << 16) | (data[offset + 3] << 24));
    private static void WriteUInt16(byte[] data, int offset, ushort value) { data[offset] = (byte)value; data[offset + 1] = (byte)(value >> 8); }
    private static void WriteUInt32(byte[] data, int offset, uint value) { data[offset] = (byte)value; data[offset + 1] = (byte)(value >> 8); data[offset + 2] = (byte)(value >> 16); data[offset + 3] = (byte)(value >> 24); }
    private static void FixRecordLength(byte[] data)
    {
        if (data.Length < 2) return;
        if (data[1] == 0xff && data.Length >= 4) WriteUInt16(data, 2, checked((ushort)data.Length));
        else if (data[1] == 0 && data.Length >= 6)
        {
            var length = checked((uint)data.Length);
            data[2] = (byte)length; data[3] = (byte)(length >> 8); data[4] = (byte)(length >> 16); data[5] = (byte)(length >> 24);
        }
        else data[1] = checked((byte)data.Length);
    }
}

/// <summary>Represents a table in Notes rich text.</summary>
public sealed class NotesRichTextTable : NotesRichTextLinkedObject
{
    private const ushort PreTableBeginSignature = unchecked((ushort)-5);
    internal const ushort TableEndSignature = 165;
    internal const ushort NestedTableBeginSignature = 207;
    internal const ushort NestedTableEndSignature = 209;
    private const ushort TableRightToLeft = 0x0010;

    internal NotesRichTextTable(NotesSession session, NotesRichTextItem item, NotesRichTextRecordData record) : base(session, item, record) { }
    public int RowCount { get { var d = GetDimensions(); return d.Rows; } }
    public int ColumnCount { get { var d = GetDimensions(); return d.Columns; } }
    /// <summary>Returns the cell records contained directly by this table.</summary>
    public IReadOnlyList<NotesRichTextTableCell> Cells
    {
        get
        {
            EnsureLinkedAlive();
            var records = Records();
            var start = CurrentFlatIndex(records);
            var result = new List<NotesRichTextTableCell>();
            var nestedDepth = 0;
            for (var i = start + 1; i < records.Count; i++)
            {
                var record = records[i];
                if (record.Signature == NestedTableBeginSignature) { nestedDepth++; continue; }
                if (record.Signature == NestedTableEndSignature) { if (nestedDepth > 0) nestedDepth--; continue; }
                if (nestedDepth > 0) continue;
                if (record.Signature == TableEndSignature) break;
                if (record.ElementType == NotesRichTextNavigator.ElementTableCell)
                    result.Add(new NotesRichTextTableCell(Session, RichTextItem, record));
            }
            return result;
        }
    }
    public bool RightToLeft { get { var data = CurrentRecord().Data; return data.Length >= 14 && (ReadUInt16(data, 12) & TableRightToLeft) != 0; } }
    public int Style { get { var data = CurrentRecord().Data; return data.Length >= 14 ? ReadUInt16(data, 12) : 0; } }
    /// <summary>Number of rows recorded in the optional CDPRETABLEBEGIN header.</summary>
    public int PreTableRowCount => ReadPreTableByte(8);
    /// <summary>Number of columns recorded in the optional CDPRETABLEBEGIN header.</summary>
    public int PreTableColumnCount => ReadPreTableByte(9);
    /// <summary>Flags from the optional CDPRETABLEBEGIN header.</summary>
    public uint PreTableFlags => ReadPreTableUInt32(4);
    /// <summary>First RGB color from CDPRETABLEBEGIN, or zero when the optional header is absent.</summary>
    public uint StyleColor1 => ReadPreTableUInt32(24);
    /// <summary>Second RGB color from CDPRETABLEBEGIN, or zero when the optional header is absent.</summary>
    public uint StyleColor2 => ReadPreTableUInt32(28);
    /// <summary>Inner border color value from CDPRETABLEBEGIN, or zero when absent.</summary>
    public uint InnerBorderColor => ReadPreTableUInt32(32);
    /// <summary>Length of the optional HTML name/ID payload.</summary>
    public int PreTableNameLength => ReadPreTableUInt16(36);
    /// <summary>Length of the optional image packet payload.</summary>
    public int PreTableImagePacketLength => ReadPreTableUInt16(38);
    /// <summary>Length of the optional row-label payload.</summary>
    public int PreTableRowLabelDataLength => ReadPreTableUInt16(40);
    /// <summary>Optional HTML name/ID stored in the pre-table header.</summary>
    public string PreTableName
    {
        get
        {
            var data = FindPreTable()?.Data;
            var length = ReadPreTableUInt16(36);
            if (data is null || length == 0 || data.Length < 42 || data.Length < 42 + length) return string.Empty;
            return Session.Api.DecodeRichTextText(data, 42, length).TrimEnd('\0');
        }
    }
    /// <summary>Sets the table direction flag in the CDTABLEBEGIN style word.</summary>
    public void SetRightToLeft(bool rightToLeft)
    {
        EnsureLinkedAlive();
        var current = CurrentRecord();
        var records = Records();
        var rewritten = NotesRichTextCdTransform.Transform(records, record =>
        {
            if (record.SegmentIndex != current.SegmentIndex || record.RecordIndex != current.RecordIndex)
                return NotesRichTextRecordRewrite.Preserve(record);
            var data = (byte[])record.Data.Clone();
            if (data.Length < 14) throw new NotesException(91, "Invalid rich-text table header record.");
            var style = ReadUInt16(data, 12);
            style = rightToLeft ? (ushort)(style | TableRightToLeft) : (ushort)(style & ~TableRightToLeft);
            WriteUInt16(data, 12, style);
            return new NotesRichTextRecordRewrite(record.SegmentIndex, record.RecordIndex, record.Signature, data);
        });
        RichTextItem.RewriteRichTextRecords(rewritten);
        AcceptCurrentRevision();
    }
    /// <summary>Sets the table style word while preserving the current direction flag.</summary>
    public void SetStyle(int style)
    {
        EnsureLinkedAlive();
        if (style < 0 || style > ushort.MaxValue) throw new ArgumentOutOfRangeException(nameof(style));
        var current = CurrentRecord();
        var records = Records();
        var rewritten = NotesRichTextCdTransform.Transform(records, record =>
        {
            if (record.SegmentIndex != current.SegmentIndex || record.RecordIndex != current.RecordIndex)
                return NotesRichTextRecordRewrite.Preserve(record);
            var data = (byte[])record.Data.Clone();
            if (data.Length < 14) throw new NotesException(91, "Invalid rich-text table header record.");
            var currentStyle = ReadUInt16(data, 12);
            var direction = (ushort)(currentStyle & TableRightToLeft);
            WriteUInt16(data, 12, (ushort)(style | direction));
            return new NotesRichTextRecordRewrite(record.SegmentIndex, record.RecordIndex, record.Signature, data);
        });
        RichTextItem.RewriteRichTextRecords(rewritten);
        AcceptCurrentRevision();
    }
    /// <summary>Returns the cell at the zero-based row and column, if present.</summary>
    public NotesRichTextTableCell? GetCell(int row, int column)
    {
        EnsureLinkedAlive();
        if (row < 0) throw new ArgumentOutOfRangeException(nameof(row));
        if (column < 0) throw new ArgumentOutOfRangeException(nameof(column));
        var records = Records();
        var start = CurrentFlatIndex(records);
        var nestedDepth = 0;
        for (var i = start + 1; i < records.Count; i++)
        {
            var record = records[i];
            if (record.Signature == NestedTableBeginSignature) { nestedDepth++; continue; }
            if (record.Signature == NestedTableEndSignature) { if (nestedDepth > 0) nestedDepth--; continue; }
            if (nestedDepth > 0) continue;
            if (record.Signature == TableEndSignature) break;
            if (record.ElementType != NotesRichTextNavigator.ElementTableCell || record.Data.Length < 18) continue;
            if (record.Data[2] == row && record.Data[3] == column)
                return new NotesRichTextTableCell(Session, RichTextItem, record);
        }
        return null;
    }

    /// <summary>Appends a copy of the last direct table row.</summary>
    public void AddRow()
    {
        EnsureLinkedAlive();
        var records = Records();
        var tableStart = CurrentFlatIndex(records);
        var preTable = FindPreTable();
        var tableEnd = -1;
        var cells = new List<(int Index, int Row)>();
        var nestedDepth = 0;
        for (var i = tableStart + 1; i < records.Count; i++)
        {
            var record = records[i];
            if (record.Signature == NestedTableBeginSignature) { nestedDepth++; continue; }
            if (record.Signature == NestedTableEndSignature) { if (nestedDepth > 0) nestedDepth--; continue; }
            if (nestedDepth > 0) continue;
            if (record.Signature == TableEndSignature) { tableEnd = i; break; }
            if (record.ElementType == NotesRichTextNavigator.ElementTableCell && record.Data.Length >= 4)
                cells.Add((i, record.Data[2]));
        }
        if (tableEnd < 0 || cells.Count == 0) throw new NotesException(91, "Rich text table has no complete row structure.");
        var sourceRow = cells.Max(cell => cell.Row);
        if (sourceRow >= byte.MaxValue) throw new InvalidOperationException("A rich-text table cannot exceed 256 rows.");
        var sourceCells = cells.Where(cell => cell.Row == sourceRow).ToArray();
        var clones = new List<(ushort Signature, byte[] Data)>();
        foreach (var cell in sourceCells)
        {
            var end = tableEnd;
            for (var i = cell.Index + 1; i < tableEnd; i++)
                if (records[i].ElementType == NotesRichTextNavigator.ElementTableCell && records[i].Data.Length >= 4) { end = i; break; }
            for (var i = cell.Index; i < end; i++)
            {
                var data = (byte[])records[i].Data.Clone();
                if (i == cell.Index) data[2] = checked((byte)(sourceRow + 1));
                clones.Add((records[i].Signature, data));
            }
        }
        var rewritten = new List<NotesRichTextRecordRewrite>(records.Count + clones.Count);
        var nextIndex = 0;
        for (var i = 0; i < records.Count; i++)
        {
            if (i == tableEnd)
                foreach (var clone in clones) rewritten.Add(new NotesRichTextRecordRewrite(0, nextIndex++, clone.Signature, clone.Data));
            var record = records[i];
            var original = (byte[])record.Data.Clone();
            if (preTable is not null && record.SegmentIndex == preTable.SegmentIndex && record.RecordIndex == preTable.RecordIndex && original.Length > 8)
                original[8] = checked((byte)(original[8] + 1));
            rewritten.Add(new NotesRichTextRecordRewrite(0, nextIndex++, record.Signature, original));
        }
        RichTextItem.RewriteRichTextRecords(rewritten);
        AcceptCurrentRevision();
    }

    /// <summary>Appends multiple copies of the last direct table row.</summary>
    public void AddRow(int count)
    {
        EnsureLinkedAlive();
        if (count < 1) throw new ArgumentOutOfRangeException(nameof(count));
        for (var i = 0; i < count; i++) AddRow();
    }

    /// <summary>Inserts <paramref name="count"/> copies immediately before the specified zero-based target row.</summary>
    public void AddRow(int count, int targetRow)
    {
        EnsureLinkedAlive();
        if (count < 1) throw new ArgumentOutOfRangeException(nameof(count));
        if (targetRow < 0) throw new ArgumentOutOfRangeException(nameof(targetRow));
        for (var i = 0; i < count; i++) AddRowBefore(targetRow);
    }

    private void AddRowBefore(int targetRow)
    {
        var records = Records();
        var tableStart = CurrentFlatIndex(records);
        var preTable = FindPreTable();
        var tableEnd = -1;
        var cells = new List<(int Index, int Row)>();
        var nestedDepth = 0;
        for (var i = tableStart + 1; i < records.Count; i++)
        {
            var record = records[i];
            if (record.Signature == NestedTableBeginSignature) { nestedDepth++; continue; }
            if (record.Signature == NestedTableEndSignature) { if (nestedDepth > 0) nestedDepth--; continue; }
            if (nestedDepth > 0) continue;
            if (record.Signature == TableEndSignature) { tableEnd = i; break; }
            if (record.ElementType == NotesRichTextNavigator.ElementTableCell && record.Data.Length >= 4)
                cells.Add((i, record.Data[2]));
        }
        if (tableEnd < 0 || cells.Count == 0) throw new NotesException(91, "Rich text table has no complete row structure.");
        var sourceCells = cells.Where(cell => cell.Row == targetRow).ToArray();
        if (sourceCells.Length == 0) throw new ArgumentOutOfRangeException(nameof(targetRow), "The table row does not exist.");
        var maxRow = cells.Max(cell => cell.Row);
        if (maxRow >= byte.MaxValue) throw new InvalidOperationException("A rich-text table cannot exceed 256 rows.");
        var clones = new List<(ushort Signature, byte[] Data)>();
        foreach (var cell in sourceCells)
        {
            var end = tableEnd;
            for (var i = cell.Index + 1; i < tableEnd; i++)
                if (records[i].ElementType == NotesRichTextNavigator.ElementTableCell && records[i].Data.Length >= 4) { end = i; break; }
            for (var i = cell.Index; i < end; i++)
            {
                var data = (byte[])records[i].Data.Clone();
                if (i == cell.Index) data[2] = checked((byte)targetRow);
                clones.Add((records[i].Signature, data));
            }
        }
        var rewritten = new List<NotesRichTextRecordRewrite>(records.Count + clones.Count);
        var nextIndex = 0;
        for (var i = 0; i < records.Count; i++)
        {
            var record = records[i];
            var original = (byte[])record.Data.Clone();
            if (preTable is not null && record.SegmentIndex == preTable.SegmentIndex && record.RecordIndex == preTable.RecordIndex && original.Length > 8)
                original[8] = checked((byte)(original[8] + 1));
            if (i == tableEnd)
            {
                foreach (var clone in clones) rewritten.Add(new NotesRichTextRecordRewrite(0, nextIndex++, clone.Signature, clone.Data));
            }
            if (record.ElementType == NotesRichTextNavigator.ElementTableCell && original.Length >= 4 && original[2] >= targetRow)
                original[2] = checked((byte)(original[2] + 1));
            rewritten.Add(new NotesRichTextRecordRewrite(0, nextIndex++, record.Signature, original));
        }
        RichTextItem.RewriteRichTextRecords(rewritten);
        AcceptCurrentRevision();
    }

    /// <summary>Removes one zero-based table row, including all rich-text records inside its cells.</summary>
    public void RemoveRow(int row)
    {
        EnsureLinkedAlive();
        if (row < 0) throw new ArgumentOutOfRangeException(nameof(row));
        var records = Records();
        var start = CurrentFlatIndex(records);
        var preTable = FindPreTable();
        var cellIndexes = new List<int>();
        var nestedDepth = 0;
        var tableEnd = -1;
        for (var i = start + 1; i < records.Count; i++)
        {
            var record = records[i];
            if (record.Signature == NestedTableBeginSignature) { nestedDepth++; continue; }
            if (record.Signature == NestedTableEndSignature) { if (nestedDepth > 0) nestedDepth--; continue; }
            if (nestedDepth > 0) continue;
            if (record.Signature == TableEndSignature) { tableEnd = i; break; }
            if (record.ElementType == NotesRichTextNavigator.ElementTableCell && record.Data.Length >= 4 && record.Data[2] == row)
                cellIndexes.Add(i);
        }
        if (tableEnd < 0) throw new NotesException(91, "Rich text table has no matching end record.");
        if (cellIndexes.Count == 0) throw new ArgumentOutOfRangeException(nameof(row), "The table row does not exist.");
        var tableKeys = records.Skip(start).Take(tableEnd - start + 1)
            .Select(record => (record.SegmentIndex, record.RecordIndex)).ToHashSet();

        var remove = new HashSet<(int Segment, int Record)>();
        foreach (var cellIndex in cellIndexes)
        {
            var end = tableEnd - 1;
            for (var i = cellIndex + 1; i < tableEnd; i++)
            {
                var candidate = records[i];
                if (candidate.ElementType == NotesRichTextNavigator.ElementTableCell && candidate.Data.Length >= 4)
                {
                    end = i - 1;
                    break;
                }
            }
            for (var i = cellIndex; i <= end; i++) remove.Add((records[i].SegmentIndex, records[i].RecordIndex));
        }

        var rewritten = NotesRichTextCdTransform.Transform(records, record =>
        {
            if (remove.Contains((record.SegmentIndex, record.RecordIndex))) return null;
            if (preTable is not null && record.SegmentIndex == preTable.SegmentIndex && record.RecordIndex == preTable.RecordIndex && record.Data.Length > 8)
            {
                var data = (byte[])record.Data.Clone();
                if (data[8] > 0) data[8]--;
                return new NotesRichTextRecordRewrite(record.SegmentIndex, record.RecordIndex, record.Signature, data);
            }
            if (tableKeys.Contains((record.SegmentIndex, record.RecordIndex)) && record.ElementType == NotesRichTextNavigator.ElementTableCell && record.Data.Length >= 4 && record.Data[2] > row)
            {
                var data = (byte[])record.Data.Clone();
                data[2]--;
                return new NotesRichTextRecordRewrite(record.SegmentIndex, record.RecordIndex, record.Signature, data);
            }
            return NotesRichTextRecordRewrite.Preserve(record);
        });
        RichTextItem.RewriteRichTextRecords(rewritten);
        Recycle();
    }

    /// <summary>Removes <paramref name="count"/> rows starting at the specified zero-based target row.</summary>
    public void RemoveRow(int count, int targetRow)
    {
        EnsureLinkedAlive();
        if (count < 1) throw new ArgumentOutOfRangeException(nameof(count));
        if (targetRow < 0) throw new ArgumentOutOfRangeException(nameof(targetRow));
        for (var i = 0; i < count; i++)
        {
            if (IsRecycled) return;
            RemoveRow(targetRow);
        }
    }
    public void Remove()
    {
        EnsureLinkedAlive();
        var records = Records();
        var start = CurrentFlatIndex(records);
        var end = start;
        var nestedDepth = 0;
        for (var i = start + 1; i < records.Count; i++)
        {
            end = i;
            if (records[i].Signature == NestedTableBeginSignature) { nestedDepth++; continue; }
            if (records[i].Signature == NestedTableEndSignature) { if (nestedDepth > 0) nestedDepth--; continue; }
            if (nestedDepth > 0) continue;
            if (records[i].Signature == TableEndSignature) break;
        }
        if (end == start || records[end].Signature != TableEndSignature)
            throw new NotesException(91, "Rich text table has no matching end record.");
        RemoveRecordSpan(start, end);
    }

    private (int Rows, int Columns) GetDimensions()
    {
        var records = Records();
        var start = CurrentFlatIndex(records);
        var maxRow = -1; var maxColumn = -1;
        var nestedDepth = 0;
        for (var i = start + 1; i < records.Count; i++)
        {
            var record = records[i];
            if (record.Signature == NestedTableBeginSignature) { nestedDepth++; continue; }
            if (record.Signature == NestedTableEndSignature) { if (nestedDepth > 0) nestedDepth--; continue; }
            if (nestedDepth > 0) continue;
            if (record.Signature == TableEndSignature) break;
            if (record.ElementType != NotesRichTextNavigator.ElementTableCell || record.Data.Length < 4) continue;
            maxRow = Math.Max(maxRow, record.Data[2]);
            maxColumn = Math.Max(maxColumn, record.Data[3]);
        }
        return (maxRow + 1, maxColumn + 1);
    }
    private static ushort ReadUInt16(byte[] data, int offset) => (ushort)(data[offset] | (data[offset + 1] << 8));
    private byte ReadPreTableByte(int offset) { var data = FindPreTable()?.Data; return data is not null && data.Length > offset ? data[offset] : (byte)0; }
    private uint ReadPreTableUInt32(int offset)
    {
        var data = FindPreTable()?.Data;
        return data is not null && data.Length >= offset + 4 ? (uint)(data[offset] | (data[offset + 1] << 8) | (data[offset + 2] << 16) | (data[offset + 3] << 24)) : 0;
    }
    private ushort ReadPreTableUInt16(int offset)
    {
        var data = FindPreTable()?.Data;
        return data is not null && data.Length >= offset + 2 ? ReadUInt16(data, offset) : (ushort)0;
    }
    private NotesRichTextRecordData? FindPreTable()
    {
        var records = Records();
        var current = CurrentFlatIndex(records);
        for (var i = current - 1; i >= 0 && current - i <= 3; i--)
            if (records[i].Signature == PreTableBeginSignature) return records[i];
        return null;
    }
    private static void WriteUInt16(byte[] data, int offset, ushort value) { data[offset] = (byte)value; data[offset + 1] = (byte)(value >> 8); }
}

/// <summary>Read-only metadata for a rich-text table cell.</summary>
public sealed class NotesRichTextTableCell : NotesRichTextLinkedObject
{
    internal NotesRichTextTableCell(NotesSession session, NotesRichTextItem item, NotesRichTextRecordData record) : base(session, item, record) { }
    public int Row { get { var data = PositionRecord().Data; return data.Length > 2 ? data[2] : 0; } }
    public int Column { get { var data = PositionRecord().Data; return data.Length > 3 ? data[3] : 0; } }
    public int LeftMargin { get { var data = PositionRecord().Data; return data.Length >= 6 ? ReadUInt16(data, 4) : 0; } }
    public int RightMargin { get { var data = PositionRecord().Data; return data.Length >= 8 ? ReadUInt16(data, 6) : 0; } }
    public int FractionalWidth { get { var data = PositionRecord().Data; return data.Length >= 10 ? ReadUInt16(data, 8) : 0; } }
    public int Border { get { var data = PositionRecord().Data; return data.Length > 10 ? data[10] : 0; } }
    public int Flags { get { var data = PositionRecord().Data; return data.Length > 11 ? data[11] : 0; } }
    /// <summary>Indicates that the cell uses the wider Notes 4.5+ border encoding.</summary>
    public bool UsesV42Borders => (Flags & 0x02) != 0;
    public int V42Border { get { var data = PositionRecord().Data; return data.Length >= 14 ? ReadUInt16(data, 12) : 0; } }
    public int BackgroundColor { get { var data = PositionRecord().Data; return data.Length >= 18 ? ReadUInt16(data, 16) : 0; } }
    public int RowSpan { get { var data = PositionRecord().Data; return data.Length >= 16 ? Math.Max(1, (int)data[14]) : 1; } }
    public int ColumnSpan { get { var data = PositionRecord().Data; return data.Length >= 16 ? Math.Max(1, (int)data[15]) : 1; } }
    public void Remove()
    {
        EnsureLinkedAlive();
        var records = Records();
        var start = CurrentFlatIndex(records);
        var end = start;
        var nestedDepth = 0;
        for (var i = start + 1; i < records.Count; i++)
        {
            var record = records[i];
            if (record.Signature == NotesRichTextTable.NestedTableBeginSignature) { nestedDepth++; continue; }
            if (record.Signature == NotesRichTextTable.NestedTableEndSignature) { if (nestedDepth > 0) nestedDepth--; continue; }
            if (nestedDepth == 0 && (record.ElementType == NotesRichTextNavigator.ElementTableCell || record.Signature == NotesRichTextTable.TableEndSignature)) break;
            end = i;
        }
        // A CDTABLECELL record is structural and cannot be removed in isolation.
        // Remove only the records contained by the cell so the table remains valid.
        if (end > start) RemoveRecordSpan(start + 1, end);
    }

    /// <summary>Sets the legacy one-byte cell border value and persists the CDTABLECELL record.</summary>
    public void SetBorder(int border)
    {
        EnsureLinkedAlive();
        if (border < 0 || border > byte.MaxValue) throw new ArgumentOutOfRangeException(nameof(border));
        RewriteCell(data => { if (data.Length < 12) throw new NotesException(91, "Invalid rich-text table cell record."); data[10] = (byte)border; return data; });
    }

    /// <summary>Sets the Notes color index used for the cell background.</summary>
    public void SetBackgroundColor(int color)
    {
        EnsureLinkedAlive();
        if (color < 0 || color > ushort.MaxValue) throw new ArgumentOutOfRangeException(nameof(color));
        RewriteCell(data =>
        {
            if (data.Length < 18) throw new NotesException(91, "Invalid rich-text table cell record.");
            WriteUInt16(data, 16, (ushort)color);
            return data;
        });
    }

    /// <summary>Sets the left and right cell margins in twips.</summary>
    public void SetMargins(int left, int right)
    {
        EnsureLinkedAlive();
        if (left < 0 || left > ushort.MaxValue) throw new ArgumentOutOfRangeException(nameof(left));
        if (right < 0 || right > ushort.MaxValue) throw new ArgumentOutOfRangeException(nameof(right));
        RewriteCell(data =>
        {
            if (data.Length < 8) throw new NotesException(91, "Invalid rich-text table cell record.");
            WriteUInt16(data, 4, (ushort)left);
            WriteUInt16(data, 6, (ushort)right);
            return data;
        });
    }

    /// <summary>Sets the row and column span, where each value must be at least one.</summary>
    public void SetSpan(int rowSpan, int columnSpan)
    {
        EnsureLinkedAlive();
        if (rowSpan is < 1 or > byte.MaxValue) throw new ArgumentOutOfRangeException(nameof(rowSpan));
        if (columnSpan is < 1 or > byte.MaxValue) throw new ArgumentOutOfRangeException(nameof(columnSpan));
        RewriteCell(data =>
        {
            if (data.Length < 16) throw new NotesException(91, "Invalid rich-text table cell record.");
            data[14] = (byte)rowSpan;
            data[15] = (byte)columnSpan;
            return data;
        });
    }

    /// <summary>Sets the fractional width used by the table layout.</summary>
    public void SetFractionalWidth(int width)
    {
        EnsureLinkedAlive();
        if (width < 0 || width > ushort.MaxValue) throw new ArgumentOutOfRangeException(nameof(width));
        RewriteCell(data => { if (data.Length < 10) throw new NotesException(91, "Invalid rich-text table cell record."); WriteUInt16(data, 8, (ushort)width); return data; });
    }

    /// <summary>Sets the extended v4.2 border word when present.</summary>
    public void SetV42Border(int border)
    {
        EnsureLinkedAlive();
        if (border < 0 || border > ushort.MaxValue) throw new ArgumentOutOfRangeException(nameof(border));
        RewriteCell(data => { if (data.Length < 14) throw new NotesException(91, "Invalid rich-text table cell record."); WriteUInt16(data, 12, (ushort)border); return data; });
    }

    /// <summary>Sets the CDTABLECELL flags byte.</summary>
    public void SetFlags(int flags)
    {
        EnsureLinkedAlive();
        if (flags < 0 || flags > byte.MaxValue) throw new ArgumentOutOfRangeException(nameof(flags));
        RewriteCell(data => { if (data.Length < 12) throw new NotesException(91, "Invalid rich-text table cell record."); data[11] = (byte)flags; return data; });
    }

    private void RewriteCell(Func<byte[], byte[]> transform)
    {
        var current = CurrentRecord();
        var records = Records();
        var rewritten = NotesRichTextCdTransform.Transform(records, record =>
        {
            if (record.SegmentIndex != current.SegmentIndex || record.RecordIndex != current.RecordIndex)
                return NotesRichTextRecordRewrite.Preserve(record);
            var data = transform((byte[])record.Data.Clone());
            return new NotesRichTextRecordRewrite(record.SegmentIndex, record.RecordIndex, record.Signature, data);
        });
        RichTextItem.RewriteRichTextRecords(rewritten);
        AcceptCurrentRevision();
    }

    private static ushort ReadUInt16(byte[] data, int offset) => (ushort)(data[offset] | (data[offset + 1] << 8));
    private static void WriteUInt16(byte[] data, int offset, ushort value) { data[offset] = (byte)value; data[offset + 1] = (byte)(value >> 8); }
}

/// <summary>Represents a document link in Notes rich text.</summary>
public sealed class NotesRichTextDocLink : NotesRichTextLinkedObject
{
    private const ushort LinkExport2Signature = unchecked((ushort)-110);
    internal NotesRichTextDocLink(NotesSession session, NotesRichTextItem item, NotesRichTextRecordData record) : base(session, item, record) { }

    public string DbReplicaID => ParseTarget().DbReplicaId;
    public string ViewUNID => ParseTarget().ViewUnid;
    public string DocUNID => ParseTarget().DocUnid;
    public string DisplayComment => ParseTarget().DisplayComment;
    public string ServerHint => ParseTarget().ServerHint;
    public string HotSpotText => ParseTarget().HotspotText;
    public void Remove()
    {
        EnsureLinkedAlive();
        var records = Records();
        RemoveRecordSpan(CurrentFlatIndex(records), CurrentFlatIndex(records));
    }

    private (string DbReplicaId, string ViewUnid, string DocUnid, string DisplayComment, string ServerHint, string HotspotText) ParseTarget()
    {
        var record = CurrentRecord();
        var data = record.Data;
        if (record.Signature == LinkExport2Signature && data.Length >= 44)
        {
            var strings = ReadStrings(data, 44, 3);
            return (Session.Api.FormatRichTextTimeDate(data, 4), Session.Api.FormatRichTextUnid(data, 12), Session.Api.FormatRichTextUnid(data, 28), strings[0], strings[1], strings[2]);
        }
        if (data.Length >= 6)
        {
            var strings = ReadStrings(data, 6, 3);
            var target = Session.Api.GetRichTextLinkTarget(RichTextItem.Parent.Handle, (ushort)(data[4] | (data[5] << 8)));
            return (target.Replica, target.View, target.Document, strings[0], strings[1], strings[2]);
        }
        return (string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty);
    }

    private string[] ReadStrings(byte[] data, int offset, int count)
    {
        var result = new string[count];
        for (var i = 0; i < count; i++)
        {
            if (offset >= data.Length) { result[i] = string.Empty; continue; }
            var end = offset;
            while (end < data.Length && data[end] != 0) end++;
            result[i] = Session.Api.DecodeRichTextText(data, offset, end - offset);
            offset = end < data.Length ? end + 1 : end;
        }
        return result;
    }
}
