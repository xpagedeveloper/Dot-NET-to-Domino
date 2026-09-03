using DotNetToDomino.Interop;

namespace DotNetToDomino;

public abstract class NotesRichTextLinkedObject : NotesObject
{
    protected readonly NotesRichTextItem RichTextItem;
    private readonly int _segmentIndex;
    private readonly int _recordIndex;
    private readonly ushort _signature;

    internal NotesRichTextLinkedObject(NotesSession session, NotesRichTextItem item, NotesRichTextRecordData record) : base(session)
    {
        RichTextItem = item;
        _segmentIndex = record.SegmentIndex;
        _recordIndex = record.RecordIndex;
        _signature = record.Signature;
    }

    public NotesRichTextItem Parent { get { EnsureLinkedAlive(); return RichTextItem; } }

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
        for (var i = 0; i < records.Count; i++)
        {
            var value = records[i];
            if (value.SegmentIndex == _segmentIndex && value.RecordIndex == _recordIndex && value.Signature == _signature) return i;
        }
        throw new InvalidOperationException("The rich text element no longer exists in its parent item.");
    }

    protected void EnsureLinkedAlive() { EnsureAlive(); RichTextItem.EnsureRichTextAlive(); }
    protected static NotesException UnsupportedWrite(string member) => new(445, member + " requires atomic composite-data rewrite support and is not supported by this runtime yet.");
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

    internal NotesRichTextSection(NotesSession session, NotesRichTextItem item, NotesRichTextRecordData record) : base(session, item, record) { }

    public bool IsExpanded { get { var data = CurrentRecord().Data; return data.Length >= 8 && (ReadUInt32(data, 4) & BarExpanded) != 0; } }
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

    public void Remove() => throw UnsupportedWrite("NotesRichTextSection.Remove");
    public void SetBarColor(NotesColorObject color) => throw UnsupportedWrite("NotesRichTextSection.SetBarColor");
    public void SetTitleStyle(NotesRichTextStyle style) => throw UnsupportedWrite("NotesRichTextSection.SetTitleStyle");

    private static ushort ReadUInt16(byte[] data, int offset) => (ushort)(data[offset] | (data[offset + 1] << 8));
    private static uint ReadUInt32(byte[] data, int offset) => (uint)(data[offset] | (data[offset + 1] << 8) | (data[offset + 2] << 16) | (data[offset + 3] << 24));
}

/// <summary>Represents a table in Notes rich text.</summary>
public sealed class NotesRichTextTable : NotesRichTextLinkedObject
{
    private const ushort TableEndSignature = 165;
    private const ushort TableRightToLeft = 0x0010;

    internal NotesRichTextTable(NotesSession session, NotesRichTextItem item, NotesRichTextRecordData record) : base(session, item, record) { }
    public int RowCount { get { var d = GetDimensions(); return d.Rows; } }
    public int ColumnCount { get { var d = GetDimensions(); return d.Columns; } }
    public bool RightToLeft { get { var data = CurrentRecord().Data; return data.Length >= 14 && (ReadUInt16(data, 12) & TableRightToLeft) != 0; } }
    public int Style { get { var data = CurrentRecord().Data; return data.Length >= 14 ? ReadUInt16(data, 12) : 0; } }
    public IReadOnlyList<string> RowLabels { get { EnsureLinkedAlive(); return Array.Empty<string>(); } }
    public NotesColorObject Color { get { EnsureLinkedAlive(); return new NotesColorObject(Session, 0); } }
    public NotesColorObject AlternateColor { get { EnsureLinkedAlive(); return new NotesColorObject(Session, 0); } }

    public void AddRow() => throw UnsupportedWrite("NotesRichTextTable.AddRow");
    public void AddRow(int count) => throw UnsupportedWrite("NotesRichTextTable.AddRow");
    public void AddRow(int count, int targetRow) => throw UnsupportedWrite("NotesRichTextTable.AddRow");
    public void Remove() => throw UnsupportedWrite("NotesRichTextTable.Remove");
    public void RemoveRow() => throw UnsupportedWrite("NotesRichTextTable.RemoveRow");
    public void RemoveRow(int count) => throw UnsupportedWrite("NotesRichTextTable.RemoveRow");
    public void RemoveRow(int count, int targetRow) => throw UnsupportedWrite("NotesRichTextTable.RemoveRow");
    public void SetAlternateColor(NotesColorObject color) => throw UnsupportedWrite("NotesRichTextTable.SetAlternateColor");
    public void SetColor(NotesColorObject color) => throw UnsupportedWrite("NotesRichTextTable.SetColor");

    private (int Rows, int Columns) GetDimensions()
    {
        var records = Records();
        var start = CurrentFlatIndex(records);
        var maxRow = -1; var maxColumn = -1;
        for (var i = start + 1; i < records.Count; i++)
        {
            var record = records[i];
            if (record.Signature == TableEndSignature) break;
            if (record.ElementType != NotesRichTextNavigator.ElementTableCell || record.Data.Length < 4) continue;
            maxRow = Math.Max(maxRow, record.Data[2]);
            maxColumn = Math.Max(maxColumn, record.Data[3]);
        }
        return (maxRow + 1, maxColumn + 1);
    }
    private static ushort ReadUInt16(byte[] data, int offset) => (ushort)(data[offset] | (data[offset + 1] << 8));
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
    public NotesRichTextStyle HotSpotTextStyle { get { EnsureLinkedAlive(); return new NotesRichTextStyle(Session); } }

    public void Remove() => throw UnsupportedWrite("NotesRichTextDocLink.Remove");
    public void RemoveLinkage() => throw UnsupportedWrite("NotesRichTextDocLink.RemoveLinkage");
    public void SetHotSpotTextStyle(NotesRichTextStyle style) => throw UnsupportedWrite("NotesRichTextDocLink.SetHotSpotTextStyle");

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
            return (string.Empty, string.Empty, string.Empty, strings[0], strings[1], strings[2]);
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