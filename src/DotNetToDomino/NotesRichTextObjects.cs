using System.Text;
using DotNetToDomino.Interop;

namespace DotNetToDomino;

/// <summary>Represents character formatting for Notes rich text.</summary>
public sealed class NotesRichTextStyle : NotesObject
{
    public const int StyleNoChange = 255;

    private int _bold = StyleNoChange;
    private int _effects = StyleNoChange;
    private int _fontSize = StyleNoChange;
    private int _italic = StyleNoChange;
    private int _notesColor = StyleNoChange;
    private int _notesFont = StyleNoChange;
    private int _passThruHtml = StyleNoChange;
    private int _strikethrough = StyleNoChange;
    private int _underline = StyleNoChange;

    internal NotesRichTextStyle(NotesSession session) : base(session) { }

    public NotesSession Parent { get { EnsureAlive(); return Session; } }
    public int Bold { get { EnsureAlive(); return _bold; } set { EnsureAlive(); _bold = ValidateBooleanStyle(value, nameof(Bold)); } }
    public int Effects
    {
        get { EnsureAlive(); return _effects; }
        set
        {
            EnsureAlive();
            if ((value < 0 || value > 5) && value != StyleNoChange)
                throw new ArgumentOutOfRangeException(nameof(value), "Effects must be 0 through 5 or STYLE_NO_CHANGE (255).");
            _effects = value;
        }
    }
    public int FontSize
    {
        get { EnsureAlive(); return _fontSize; }
        set
        {
            EnsureAlive();
            if ((value < 1 || value > 250) && value != StyleNoChange)
                throw new ArgumentOutOfRangeException(nameof(value), "FontSize must be 1 through 250 or STYLE_NO_CHANGE (255).");
            _fontSize = value;
        }
    }
    public bool IsDefault
    {
        get
        {
            EnsureAlive();
            return _bold == StyleNoChange && _effects == StyleNoChange && _fontSize == StyleNoChange &&
                   _italic == StyleNoChange && _notesColor == StyleNoChange && _notesFont == StyleNoChange &&
                   _passThruHtml == StyleNoChange && _strikethrough == StyleNoChange && _underline == StyleNoChange;
        }
    }
    public int Italic { get { EnsureAlive(); return _italic; } set { EnsureAlive(); _italic = ValidateBooleanStyle(value, nameof(Italic)); } }
    public int NotesColor
    {
        get { EnsureAlive(); return _notesColor; }
        set
        {
            EnsureAlive();
            if (value < 0 || value > 255) throw new ArgumentOutOfRangeException(nameof(value), "NotesColor must be between 0 and 255.");
            _notesColor = value;
        }
    }
    public int NotesFont
    {
        get { EnsureAlive(); return _notesFont; }
        set
        {
            EnsureAlive();
            if (value < 0 || value > 255) throw new ArgumentOutOfRangeException(nameof(value), "NotesFont must be between 0 and 255.");
            _notesFont = value;
        }
    }
    public int PassThruHTML { get { EnsureAlive(); return _passThruHtml; } set { EnsureAlive(); _passThruHtml = ValidateBooleanStyle(value, nameof(PassThruHTML)); } }
    public int Strikethrough { get { EnsureAlive(); return _strikethrough; } set { EnsureAlive(); _strikethrough = ValidateBooleanStyle(value, nameof(Strikethrough)); } }
    public int Underline { get { EnsureAlive(); return _underline; } set { EnsureAlive(); _underline = ValidateBooleanStyle(value, nameof(Underline)); } }

    internal NotesRichTextStyleState ExportState()
    {
        EnsureAlive();
        return new NotesRichTextStyleState(_bold, _effects, _fontSize, _italic, _notesColor, _notesFont, _passThruHtml, _strikethrough, _underline);
    }

    private static int ValidateBooleanStyle(int value, string name)
    {
        if (value is -1 or 0 or StyleNoChange) return value;
        throw new ArgumentOutOfRangeException(name, "Value must be -1, 0, or STYLE_NO_CHANGE (255).");
    }

    protected override void ReleaseNative() { }
}

/// <summary>Represents paragraph formatting for Notes rich text.</summary>
public sealed class NotesRichTextParagraphStyle : NotesObject
{
    public const int TabLeft = 0;
    public const int TabRight = 1;
    public const int TabDecimal = 2;
    public const int TabCenter = 3;

    private readonly List<NotesRichTextTab> _tabs = [];
    private int _alignment;
    private int _interLineSpacing;
    private int _pagination;
    private int _firstLineLeftMargin;
    private int _leftMargin;
    private int _rightMargin;
    private int _spacingAbove;
    private int _spacingBelow;
    private short _baseFlags;

    internal NotesRichTextParagraphStyle(NotesSession session, NotesCompoundStyleState state) : base(session)
    {
        _alignment = unchecked((ushort)state.JustifyMode);
        _interLineSpacing = unchecked((ushort)state.LineSpacing);
        _firstLineLeftMargin = unchecked((ushort)state.FirstLineLeftMargin);
        _leftMargin = unchecked((ushort)state.LeftMargin);
        _rightMargin = unchecked((ushort)state.RightMargin);
        _spacingAbove = unchecked((ushort)state.ParagraphSpacingBefore);
        _spacingBelow = unchecked((ushort)state.ParagraphSpacingAfter);
        _baseFlags = state.Flags;
        _pagination = DecodePagination(state.Flags);
        foreach (var tabValue in state.Tabs)
        {
            var raw = unchecked((ushort)tabValue);
            if (raw == 0) continue;
            var position = raw & 0x3fff;
            var type = (raw >> 14) & 0x3;
            _tabs.Add(new NotesRichTextTab(session, this, position, type));
        }
    }

    public int Alignment
    {
        get { EnsureStyleAlive(); return _alignment; }
        set
        {
            EnsureStyleAlive();
            if (value < 0 || value > 4) throw new ArgumentOutOfRangeException(nameof(value), "Alignment must be 0 through 4.");
            _alignment = value;
        }
    }
    public int FirstLineLeftMargin { get { EnsureStyleAlive(); return _firstLineLeftMargin; } set { EnsureStyleAlive(); _firstLineLeftMargin = ValidateTwips(value, nameof(value)); } }
    public int InterLineSpacing
    {
        get { EnsureStyleAlive(); return _interLineSpacing; }
        set
        {
            EnsureStyleAlive();
            if (value < 0 || value > 4) throw new ArgumentOutOfRangeException(nameof(value), "InterLineSpacing must be 0 through 4.");
            _interLineSpacing = value;
        }
    }
    public int LeftMargin { get { EnsureStyleAlive(); return _leftMargin; } set { EnsureStyleAlive(); _leftMargin = ValidateTwips(value, nameof(value)); } }
    public int Pagination
    {
        get { EnsureStyleAlive(); return _pagination; }
        set
        {
            EnsureStyleAlive();
            if (value is not (0 or 1 or 2 or 4)) throw new ArgumentOutOfRangeException(nameof(value), "Pagination must be 0, 1, 2, or 4.");
            _pagination = value;
        }
    }
    public int RightMargin { get { EnsureStyleAlive(); return _rightMargin; } set { EnsureStyleAlive(); _rightMargin = ValidateTwips(value, nameof(value)); } }
    public int SpacingAbove { get { EnsureStyleAlive(); return _spacingAbove; } set { EnsureStyleAlive(); _spacingAbove = ValidateTwips(value, nameof(value)); } }
    public int SpacingBelow { get { EnsureStyleAlive(); return _spacingBelow; } set { EnsureStyleAlive(); _spacingBelow = ValidateTwips(value, nameof(value)); } }
    public IReadOnlyList<NotesRichTextTab> Tabs { get { EnsureStyleAlive(); return _tabs.Where(tab => !tab.IsRecycled).ToArray(); } }

    public void ClearAllTabs()
    {
        EnsureStyleAlive();
        foreach (var tab in _tabs.ToArray()) tab.Recycle();
        _tabs.Clear();
    }

    public NotesRichTextTab SetTab(int position, int type)
    {
        EnsureStyleAlive();
        position = ValidateTwips(position, nameof(position));
        type = ValidateTabType(type);
        var existing = _tabs.FirstOrDefault(tab => !tab.IsRecycled && tab.Position == position);
        if (existing is not null)
        {
            existing.SetType(type);
            return existing;
        }
        if (_tabs.Count(tab => !tab.IsRecycled) >= 20) throw new InvalidOperationException("A rich text paragraph style supports at most 20 tabs.");
        var created = new NotesRichTextTab(Session, this, position, type);
        _tabs.Add(created);
        _tabs.Sort((left, right) => left.Position.CompareTo(right.Position));
        return created;
    }

    public void SetTabs(int count, int start, int interval) => SetTabs(count, start, interval, TabLeft);

    public void SetTabs(int count, int start, int interval, int type)
    {
        EnsureStyleAlive();
        start = ValidateTwips(start, nameof(start));
        type = ValidateTabType(type);
        if (count < 0 || count > 20) throw new ArgumentOutOfRangeException(nameof(count), "Tab count must be between 0 and 20.");
        if (interval < 0) throw new ArgumentOutOfRangeException(nameof(interval), "Tab interval cannot be negative.");
        ClearAllTabs();
        for (var i = 0; i < count; i++) SetTab(start + (i * interval), type);
    }

    internal void RemoveTab(NotesRichTextTab tab) => _tabs.Remove(tab);
    internal void EnsureStyleAlive() => EnsureAlive();

    internal NotesCompoundStyleState ExportState()
    {
        EnsureStyleAlive();
        var rawTabs = _tabs.Where(tab => !tab.IsRecycled).OrderBy(tab => tab.Position).Take(20)
            .Select(tab => unchecked((short)((tab.Position & 0x3fff) | ((tab.Type & 0x3) << 14)))).ToArray();
        var flags = unchecked((ushort)_baseFlags);
        flags = (ushort)(flags & ~(0x0001 | 0x0002 | 0x0004));
        flags = (ushort)(flags | _pagination);
        return new NotesCompoundStyleState(
            unchecked((short)_alignment), unchecked((short)_interLineSpacing), unchecked((short)_spacingAbove), unchecked((short)_spacingBelow),
            unchecked((short)_leftMargin), unchecked((short)_rightMargin), unchecked((short)_firstLineLeftMargin), unchecked((short)flags), rawTabs);
    }

    private static int DecodePagination(short flags) => unchecked((ushort)flags) & 0x0007;
    private static int ValidateTwips(int value, string name)
    {
        if (value < 0 || value > 0x3fff) throw new ArgumentOutOfRangeException(name, "Value must be between 0 and 16383 twips.");
        return value;
    }
    private static int ValidateTabType(int value)
    {
        if (value < TabLeft || value > TabCenter) throw new ArgumentOutOfRangeException(nameof(value), "Tab type must be 0 through 3.");
        return value;
    }

    protected override void ReleaseNative()
    {
        foreach (var tab in _tabs.ToArray()) tab.Recycle();
        _tabs.Clear();
    }
}

/// <summary>Represents one paragraph tab stop.</summary>
public sealed class NotesRichTextTab : NotesObject
{
    private readonly NotesRichTextParagraphStyle _parent;
    private readonly int _position;
    private bool _cleared;
    private int _type;

    internal NotesRichTextTab(NotesSession session, NotesRichTextParagraphStyle parent, int position, int type) : base(session)
    {
        _parent = parent;
        _position = position;
        _type = type;
    }

    public int Position { get { EnsureTabAlive(); return _position; } }
    public int Type { get { EnsureTabAlive(); return _type; } }

    public void Clear()
    {
        EnsureTabAlive();
        _parent.RemoveTab(this);
        _cleared = true;
        Recycle();
    }

    internal void SetType(int type) { EnsureTabAlive(); _type = type; }

    private void EnsureTabAlive()
    {
        EnsureAlive();
        _parent.EnsureStyleAlive();
        if (_cleared) throw new ObjectDisposedException(nameof(NotesRichTextTab), "NotesRichTextTab has been cleared.");
    }

    protected override void ReleaseNative() => _cleared = true;
}

/// <summary>Traverses logical records in a Notes rich-text item.</summary>
public sealed class NotesRichTextNavigator : NotesObject
{
    public const int ElementTable = 1;
    public const int ElementTextRun = 3;
    public const int ElementTextParagraph = 4;
    public const int ElementDocLink = 5;
    public const int ElementSection = 6;
    public const int ElementTableCell = 7;
    public const int ElementEmbeddedObject = 8;
    /// <summary>Logical table end record.</summary>
    public const int ElementTableEnd = 9;

    private readonly NotesRichTextItem _item;
    private List<NotesRichTextRecordData> _records = [];
    private int _currentIndex = -1;
    private int _lastElementType;
    private int _charOffset;
    private long _loadedRevision = -1;
    private int _rangeStart;
    private int _rangeEnd = int.MaxValue;

    internal NotesRichTextNavigator(NotesSession session, NotesRichTextItem item) : base(session)
    {
        _item = item;
        Reload();
    }

    private NotesRichTextNavigator(NotesSession session, NotesRichTextItem item, int currentIndex, int lastElementType, int charOffset) : base(session)
    {
        _item = item;
        Reload();
        _currentIndex = currentIndex >= 0 && currentIndex < _records.Count ? currentIndex : -1;
        _lastElementType = lastElementType;
        _charOffset = charOffset;
    }

    public NotesRichTextNavigator Clone()
    {
        EnsureNavigatorAlive();
        RefreshIfChanged();
        return new NotesRichTextNavigator(Session, _item, _currentIndex, _lastElementType, _charOffset);
    }

    public bool FindFirstElement(int type)
    {
        EnsureNavigatorAlive(); RefreshIfChanged(); type = ValidateElementType(type);
        var index = FindElement(0, type, 1);
        if (index < 0) return false;
        SetCurrent(index, type, 0); return true;
    }

    public bool FindLastElement(int type)
    {
        EnsureNavigatorAlive(); RefreshIfChanged(); type = ValidateElementType(type);
        var last = Math.Min(_records.Count - 1, _rangeEnd);
        for (var i = last; i >= _rangeStart; i--)
        {
            if (_records[i].ElementType != type) continue;
            SetCurrent(i, type, 0); return true;
        }
        return false;
    }

    public bool FindNthElement(int type, int occurrence)
    {
        EnsureNavigatorAlive(); RefreshIfChanged(); type = ValidateElementType(type); occurrence = ValidateOccurrence(occurrence);
        var index = FindElement(0, type, occurrence);
        if (index < 0) return false;
        SetCurrent(index, type, 0); return true;
    }

    public bool FindNextElement()
    {
        EnsureNavigatorAlive();
        if (_lastElementType == 0) return false;
        return FindNextElement(_lastElementType, 1);
    }

    public bool FindNextElement(int type) => FindNextElement(type, 1);

    public bool FindNextElement(int type, int occurrence)
    {
        EnsureNavigatorAlive(); RefreshIfChanged();
        if (_currentIndex < 0) return false;
        type = ValidateElementType(type); occurrence = ValidateOccurrence(occurrence);
        var index = FindElement(_currentIndex + 1, type, occurrence);
        if (index < 0) return false;
        SetCurrent(index, type, 0); return true;
    }

    public bool FindFirstString(string target, int options = 0)
    {
        EnsureNavigatorAlive(); RefreshIfChanged();
        return FindString(0, target ?? throw new ArgumentNullException(nameof(target)), options);
    }

    public bool FindNextString(string target, int options = 0)
    {
        EnsureNavigatorAlive(); RefreshIfChanged();
        if (_currentIndex < 0) return false;
        return FindString(_currentIndex, target ?? throw new ArgumentNullException(nameof(target)), options, _charOffset + 1);
    }

    public object? GetElement()
    {
        EnsureNavigatorAlive(); RefreshIfChanged();
        return MaterializeElement(CurrentRecord);
    }

    public void SetPosition(object element)
    {
        EnsureNavigatorAlive();
        ArgumentNullException.ThrowIfNull(element);
        var record = element switch
        {
            NotesRichTextLinkedObject linked when ReferenceEquals(linked.PositionOwner, _item) => linked.PositionRecord(),
            NotesEmbeddedObject embedded when ReferenceEquals(embedded.Parent, _item) => _records.FirstOrDefault(value => value.ElementType == ElementEmbeddedObject && string.Equals(value.LinkedObjectName, embedded.Name, StringComparison.OrdinalIgnoreCase)),
            _ => throw new ArgumentException("'element' must be a rich-text element belonging to this item.", nameof(element))
        } ?? throw new InvalidOperationException("The rich-text element no longer exists.");
        var index = _records.FindIndex(value => value.SegmentIndex == record.SegmentIndex && value.RecordIndex == record.RecordIndex && value.Signature == record.Signature);
        if (index < _rangeStart || index > _rangeEnd || index < 0) throw new InvalidOperationException("The rich-text element is outside the navigator range.");
        SetCurrent(index, record.ElementType, 0);
    }

    public void SetPositionAtEnd(object element)
    {
        SetPosition(element);
        var endIndex = FindElementEnd(_currentIndex, _lastElementType);
        _charOffset = _records[endIndex].Text.Length;
        _currentIndex = endIndex;
    }

    private int FindElementEnd(int startIndex, int elementType)
    {
        if (elementType is ElementTable or ElementEmbeddedObject)
        {
            var table = elementType == ElementTable;
            var depth = 0;
            for (var i = startIndex; i <= Math.Min(_rangeEnd, _records.Count - 1); i++)
            {
                var signature = _records[i].Signature;
                var begin = table ? signature is 163 or 207 : signature is unchecked((ushort)-87) or unchecked((ushort)-83) or unchecked((ushort)-130);
                var end = table ? signature is 165 or 209 : signature is 170 or 174 or 127;
                if (begin) depth++;
                else if (end && --depth == 0) return i;
            }
            throw new InvalidOperationException($"Rich-text {(table ? "table" : "embedded hotspot")} has no matching end record in the navigator range.");
        }
        if (elementType == ElementTextParagraph)
        {
            for (var i = startIndex + 1; i <= Math.Min(_rangeEnd, _records.Count - 1); i++)
                if (_records[i].ElementType == ElementTextParagraph) return i - 1;
            return Math.Min(_rangeEnd, _records.Count - 1);
        }
        return startIndex;
    }

    public object? GetFirstElement(int type)
    {
        EnsureNavigatorAlive(); RefreshIfChanged(); type = ValidateGetElementType(type);
        var index = FindElement(0, type, 1);
        if (index < 0) return null;
        SetCurrent(index, type, 0); return MaterializeElement(_records[index]);
    }

    public object? GetLastElement(int type)
    {
        EnsureNavigatorAlive(); RefreshIfChanged(); type = ValidateGetElementType(type);
        var last = Math.Min(_records.Count - 1, _rangeEnd);
        for (var i = last; i >= _rangeStart; i--)
        {
            if (_records[i].ElementType != type) continue;
            SetCurrent(i, type, 0); return MaterializeElement(_records[i]);
        }
        return null;
    }

    public object? GetNextElement()
    {
        EnsureNavigatorAlive(); RefreshIfChanged();
        if (_currentIndex < 0 || _lastElementType == 0) throw new InvalidOperationException("NotesRichTextNavigator has no current element position.");
        return GetNextElement(_lastElementType, 1);
    }

    public object? GetNextElement(int type) => GetNextElement(type, 1);

    public object? GetNextElement(int type, int occurrence)
    {
        EnsureNavigatorAlive(); RefreshIfChanged();
        if (_currentIndex < 0) throw new InvalidOperationException("NotesRichTextNavigator has no current element position.");
        type = ValidateGetElementType(type); occurrence = ValidateOccurrence(occurrence);
        var index = FindElement(_currentIndex + 1, type, occurrence);
        if (index < 0) return null;
        SetCurrent(index, type, 0); return MaterializeElement(_records[index]);
    }

    public object? GetNthElement(int type, int occurrence)
    {
        EnsureNavigatorAlive(); RefreshIfChanged(); type = ValidateGetElementType(type); occurrence = ValidateOccurrence(occurrence);
        var index = FindElement(0, type, occurrence);
        if (index < 0) return null;
        SetCurrent(index, type, 0); return MaterializeElement(_records[index]);
    }

    public void SetCharOffset(int offset)
    {
        EnsureNavigatorAlive(); RefreshIfChanged();
        if (_currentIndex < 0) throw new InvalidOperationException("NotesRichTextNavigator has no current position.");
        if (offset < 0) throw new ArgumentOutOfRangeException(nameof(offset));
        _charOffset += offset;
    }

    internal NotesRichTextRecordData CurrentRecord
    {
        get
        {
            EnsureNavigatorAlive(); RefreshIfChanged();
            if (_currentIndex < 0 || _currentIndex >= _records.Count) throw new InvalidOperationException("NotesRichTextNavigator has no current position.");
            return _records[_currentIndex];
        }
    }
    internal int CurrentIndex { get { _ = CurrentRecord; return _currentIndex; } }
    internal int CurrentCharOffset { get { _ = CurrentRecord; return _charOffset; } }
    internal int CurrentElementType { get { _ = CurrentRecord; return _lastElementType; } }
    internal NotesRichTextItem RichTextItem => _item;

    internal void SetBounds(int startRecord, int endRecord)
    {
        EnsureNavigatorAlive(); RefreshIfChanged();
        if (_records.Count == 0) { _rangeStart = 0; _rangeEnd = -1; _currentIndex = -1; return; }
        if (startRecord < 0 || endRecord < startRecord || endRecord >= _records.Count) throw new ArgumentOutOfRangeException(nameof(startRecord), "Invalid rich text navigator range.");
        _rangeStart = startRecord; _rangeEnd = endRecord;
        if (_currentIndex < _rangeStart || _currentIndex > _rangeEnd) _currentIndex = -1;
    }

    internal void SetInternalPosition(int recordIndex, int charOffset)
    {
        EnsureNavigatorAlive(); RefreshIfChanged();
        if (recordIndex < 0 || recordIndex >= _records.Count || recordIndex < _rangeStart || recordIndex > _rangeEnd) throw new ArgumentOutOfRangeException(nameof(recordIndex), "Invalid rich text position.");
        _currentIndex = recordIndex; _charOffset = Math.Max(0, charOffset); _lastElementType = _records[recordIndex].ElementType;
    }

    private bool FindString(int startRecord, string target, int options, int initialOffset = 0)
    {
        if (target.Length == 0) throw new ArgumentException("Rich text search string cannot be empty.", nameof(target));
        var comparison = (options & 1) != 0 ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        var firstRecord = Math.Max(_rangeStart, Math.Max(0, startRecord));
        var lastRecord = Math.Min(_rangeEnd, _records.Count - 1);
        for (var i = firstRecord; i <= lastRecord; i++)
        {
            var text = _records[i].Text;
            if (text.Length == 0) continue;
            var start = i == startRecord ? Math.Min(initialOffset, text.Length) : 0;
            var found = text.IndexOf(target, start, comparison);
            if (found < 0) continue;
            SetCurrent(i, 11, found); return true;
        }
        return false;
    }

    private int FindElement(int start, int type, int occurrence)
    {
        var found = 0;
        var firstRecord = Math.Max(_rangeStart, Math.Max(0, start));
        var lastRecord = Math.Min(_rangeEnd, _records.Count - 1);
        for (var i = firstRecord; i <= lastRecord; i++)
        {
            if (_records[i].ElementType != type) continue;
            found++;
            if (found == occurrence) return i;
        }
        return -1;
    }

    private object MaterializeElement(NotesRichTextRecordData record)
    {
        if (record.ElementType == ElementTable) return new NotesRichTextTable(Session, _item, record);
        if (record.ElementType == ElementDocLink) return new NotesRichTextDocLink(Session, _item, record);
        if (record.ElementType == ElementSection) return new NotesRichTextSection(Session, _item, record);
        if (record.ElementType == ElementEmbeddedObject)
        {
            if (record.LinkedObjectName.Length == 0) throw new InvalidDataException("Rich text attachment hotspot has no attachment name.");
            if (!Session.Api.TryGetRichTextAttachmentMetadata(_item.Parent.Handle, _item.Name, record.LinkedObjectName, out var metadata))
                throw new FileNotFoundException("Rich text attachment object was not found.", record.LinkedObjectName);
            return new NotesEmbeddedObject(Session, _item, metadata);
        }
        if (record.ElementType is ElementTextRun or ElementTextParagraph or ElementTableCell)
            throw new InvalidOperationException("Text runs, text paragraphs, and table cells must be accessed through NotesRichTextRange.");
        throw new NotSupportedException($"Unsupported rich text element type {record.ElementType}.");
    }

    private void SetCurrent(int index, int type, int charOffset) { _currentIndex = index; _lastElementType = type; _charOffset = charOffset; }
    private void RefreshIfChanged() { if (_item.RichTextRevision != _loadedRevision) Reload(); }
    private void Reload()
    {
        _item.EnsureRichTextAlive();
        _records = _item.ReadRichTextRecords().ToList();
        _loadedRevision = _item.RichTextRevision;
        _currentIndex = -1; _lastElementType = 0; _charOffset = 0;
    }
    private static int ValidateElementType(int type)
    {
        if (type is ElementTable or ElementTextRun or ElementTextParagraph or ElementDocLink or ElementSection or ElementTableCell or ElementEmbeddedObject or ElementTableEnd) return type;
        throw new ArgumentOutOfRangeException(nameof(type), $"Unsupported rich text element type {type}.");
    }
    private static int ValidateGetElementType(int type)
    {
        if (type is ElementTable or ElementDocLink or ElementSection or ElementEmbeddedObject) return type;
        if (type is ElementTextRun or ElementTextParagraph or ElementTableCell) throw new InvalidOperationException("Text runs, text paragraphs, and table cells must be accessed through NotesRichTextRange.");
        throw new ArgumentOutOfRangeException(nameof(type), $"Unsupported rich text element type {type}.");
    }
    private static int ValidateOccurrence(int occurrence)
    {
        if (occurrence <= 0) throw new ArgumentOutOfRangeException(nameof(occurrence), "Rich text occurrence must be positive.");
        return occurrence;
    }
    private void EnsureNavigatorAlive() { EnsureAlive(); _item.EnsureRichTextAlive(); }
    protected override void ReleaseNative() { _records.Clear(); _currentIndex = -1; }
}

/// <summary>Represents a bounded range in a Notes rich-text item.</summary>
public sealed class NotesRichTextRange : NotesObject
{
    private readonly NotesRichTextItem _item;
    private int _beginRecord;
    private int _beginOffset;
    private int _endRecord;
    private int _endOffset;
    private int _type;

    internal NotesRichTextRange(NotesSession session, NotesRichTextItem item) : base(session) { _item = item; ResetCore(); }
    private NotesRichTextRange(NotesSession session, NotesRichTextItem item, int beginRecord, int beginOffset, int endRecord, int endOffset, int type) : base(session)
    {
        _item = item; _beginRecord = beginRecord; _beginOffset = beginOffset; _endRecord = endRecord; _endOffset = endOffset; _type = type;
    }

    public NotesRichTextNavigator Navigator
    {
        get
        {
            EnsureRangeAlive();
            var navigator = new NotesRichTextNavigator(Session, _item);
            if (_endRecord >= _beginRecord && _endRecord >= 0) { navigator.SetBounds(_beginRecord, _endRecord); navigator.SetInternalPosition(_beginRecord, _beginOffset); }
            return navigator;
        }
    }
    public NotesRichTextStyle Style
    {
        get
        {
            EnsureRangeAlive();
            var result = new NotesRichTextStyle(Session);
            var records = _item.ReadRichTextRecords();
            var html = false;
            for (var i = 0; i <= _endRecord && i < records.Count; i++)
            {
                var record = records[i];
                if (record.Signature == 0xFFCC) html = true;
                else if (record.Signature == 0xFFCD) html = false;
                if (i < _beginRecord || record.Signature != 0xFF85 || record.Data.Length < 8 || record.Text.Length == 0) continue;
                var data = record.Data;
                var attributes = data[5];
                result.NotesFont = data[4];
                result.NotesColor = data[6];
                if (data[7] is >= 1 and <= 250) result.FontSize = data[7];
                result.Bold = (attributes & 1) != 0 ? -1 : 0;
                result.Italic = (attributes & 2) != 0 ? -1 : 0;
                result.Underline = (attributes & 4) != 0 ? -1 : 0;
                result.Strikethrough = (attributes & 8) != 0 ? -1 : 0;
                result.Effects = (attributes & 0xB0) switch { 0x10 => 1, 0x20 => 2, 0x80 => 3, 0x90 => 4, 0xA0 => 5, _ => 0 };
                result.PassThruHTML = html ? -1 : 0;
                break;
            }
            return result;
        }
    }
    public string TextParagraph
    {
        get
        {
            EnsureRangeAlive();
            var records = _item.ReadRichTextRecords();
            if (records.Count == 0 || _endRecord < _beginRecord) return string.Empty;
            var builder = new StringBuilder();
            var started = false;
            for (var i = _beginRecord; i <= _endRecord && i < records.Count; i++)
            {
                var record = records[i];
                if (record.ElementType == NotesRichTextNavigator.ElementTextParagraph && started) break;
                if (record.Text.Length == 0) continue;
                started = true; builder.Append(SliceText(record.Text, i));
            }
            return builder.ToString();
        }
    }
    public string TextRun
    {
        get
        {
            EnsureRangeAlive();
            var records = _item.ReadRichTextRecords();
            if (records.Count == 0 || _endRecord < _beginRecord) return string.Empty;
            for (var i = _beginRecord; i <= _endRecord && i < records.Count; i++) if (records[i].Text.Length > 0) return SliceText(records[i].Text, i);
            return string.Empty;
        }
    }
    public int Type { get { EnsureRangeAlive(); return _type; } }

    public NotesRichTextRange Clone() { EnsureRangeAlive(); return new NotesRichTextRange(Session, _item, _beginRecord, _beginOffset, _endRecord, _endOffset, _type); }
    public void Reset() { EnsureRangeAlive(); ResetCore(); }
    public void SetBegin(NotesRichTextNavigator navigator) { ArgumentNullException.ThrowIfNull(navigator); SetBeginCore(navigator.CurrentIndex, navigator.CurrentCharOffset, navigator.CurrentElementType, navigator.RichTextItem); }
    public void SetBegin(NotesRichTextRange range) { ArgumentNullException.ThrowIfNull(range); SetBeginCore(range.BeginRecord, range.BeginOffset, range.Type, range.RichTextItem); }
    public void SetEnd(NotesRichTextNavigator navigator) { ArgumentNullException.ThrowIfNull(navigator); SetEndCore(navigator.CurrentIndex, navigator.CurrentCharOffset, navigator.CurrentElementType, navigator.RichTextItem); }
    public void SetEnd(NotesRichTextRange range) { ArgumentNullException.ThrowIfNull(range); SetEndCore(range.EndRecord, range.EndOffset, range.Type, range.RichTextItem); }
    public void SetStyle(NotesRichTextStyle style)
    {
        EnsureRangeAlive();
        ArgumentNullException.ThrowIfNull(style);
        var state = style.ExportState();
        var records = _item.ReadRichTextRecords();
        var selected = records
            .Select((record, index) => (record, index))
            .Where(value => value.index >= _beginRecord && value.index <= _endRecord)
            .Select(value => (value.record.SegmentIndex, value.record.RecordIndex))
            .ToHashSet();
        var rewritten = NotesRichTextCdTransform.Transform(records, record =>
        {
            if (!selected.Contains((record.SegmentIndex, record.RecordIndex)) || record.Signature != 133 || record.Data.Length < 8)
                return NotesRichTextRecordRewrite.Preserve(record);
            var data = (byte[])record.Data.Clone();
            if (state.NotesFont != NotesRichTextStyle.StyleNoChange) data[4] = checked((byte)state.NotesFont);
            var attributes = data[5];
            attributes = ApplyStyleFlag(attributes, 0x01, state.Bold);
            attributes = ApplyStyleFlag(attributes, 0x02, state.Italic);
            attributes = ApplyStyleFlag(attributes, 0x04, state.Underline);
            attributes = ApplyStyleFlag(attributes, 0x08, state.Strikethrough);
            data[5] = attributes;
            if (state.NotesColor != NotesRichTextStyle.StyleNoChange) data[6] = checked((byte)state.NotesColor);
            if (state.FontSize != NotesRichTextStyle.StyleNoChange) data[7] = checked((byte)state.FontSize);
            return new NotesRichTextRecordRewrite(record.SegmentIndex, record.RecordIndex, record.Signature, data);
        });
        _item.RewriteRichTextRecords(rewritten);
    }
    public void Remove()
    {
        EnsureRangeAlive();
        var records = _item.ReadRichTextRecords();
        if (records.Count == 0 || _endRecord < _beginRecord) return;
        var selected = records
            .Select((record, index) => (record, index))
            .Where(value => value.index >= _beginRecord && value.index <= _endRecord)
            .Select(value => (value.record.SegmentIndex, value.record.RecordIndex))
            .ToHashSet();
        var rewritten = NotesRichTextCdTransform.Transform(records, record =>
        {
            if (record.Signature != 133 || record.Data.Length < 8 || record.Text.Length == 0)
                return NotesRichTextRecordRewrite.Preserve(record);
            if (!selected.Contains((record.SegmentIndex, record.RecordIndex))) return NotesRichTextRecordRewrite.Preserve(record);
            var start = record.SegmentIndex == records[_beginRecord].SegmentIndex && record.RecordIndex == records[_beginRecord].RecordIndex ? Math.Clamp(_beginOffset, 0, record.Text.Length) : 0;
            var end = record.SegmentIndex == records[_endRecord].SegmentIndex && record.RecordIndex == records[_endRecord].RecordIndex ? Math.Clamp(_endOffset, start, record.Text.Length) : record.Text.Length;
            if (start == 0 && end == record.Text.Length) return null;
            if (end <= start) return NotesRichTextRecordRewrite.Preserve(record);
            var replacementText = record.Text[..start] + record.Text[end..];
            var encoded = Session.Api.EncodeRichTextText(replacementText.Replace('\n', '\0'));
            var data = new byte[8 + encoded.Length];
            record.Data.AsSpan(0, 8).CopyTo(data);
            encoded.CopyTo(data, 8);
            data[2] = (byte)data.Length; data[3] = (byte)(data.Length >> 8);
            return new NotesRichTextRecordRewrite(record.SegmentIndex, record.RecordIndex, record.Signature, data);
        });
        _item.RewriteRichTextRecords(rewritten);
        ResetCore();
    }

    internal NotesRichTextItem RichTextItem => _item;
    internal int BeginRecord { get { EnsureRangeAlive(); return _beginRecord; } }
    internal int BeginOffset { get { EnsureRangeAlive(); return _beginOffset; } }
    internal int EndRecord { get { EnsureRangeAlive(); return _endRecord; } }
    internal int EndOffset { get { EnsureRangeAlive(); return _endOffset; } }

    private void SetBeginCore(int record, int offset, int type, NotesRichTextItem item)
    {
        EnsureRangeAlive(); EnsureSameItem(item);
        if (_endRecord >= 0 && Compare(record, offset, _endRecord, _endOffset) > 0) throw new ArgumentException("The rich text range beginning cannot be after its end.");
        _beginRecord = record; _beginOffset = offset; _type = type;
    }
    private void SetEndCore(int record, int offset, int type, NotesRichTextItem item)
    {
        EnsureRangeAlive(); EnsureSameItem(item);
        if (_beginRecord >= 0 && Compare(record, offset, _beginRecord, _beginOffset) < 0) throw new ArgumentException("The rich text range end cannot be before its beginning.");
        _endRecord = record; _endOffset = offset; if (_type == 0) _type = type;
    }
    private string SliceText(string text, int recordIndex)
    {
        var start = recordIndex == _beginRecord ? Math.Clamp(_beginOffset, 0, text.Length) : 0;
        var end = recordIndex == _endRecord ? Math.Clamp(_endOffset, start, text.Length) : text.Length;
        return text[start..end];
    }
    private void ResetCore()
    {
        _item.EnsureRichTextAlive();
        var records = _item.ReadRichTextRecords();
        _type = 0; _beginRecord = 0; _beginOffset = 0;
        if (records.Count == 0) { _endRecord = -1; _endOffset = 0; return; }
        _endRecord = records.Count - 1; _endOffset = records[_endRecord].Text.Length;
    }
    private void EnsureSameItem(NotesRichTextItem item) { if (!ReferenceEquals(_item, item)) throw new ArgumentException("Rich text positions must belong to the same NotesRichTextItem."); }
    private static int Compare(int leftRecord, int leftOffset, int rightRecord, int rightOffset) { var byRecord = leftRecord.CompareTo(rightRecord); return byRecord != 0 ? byRecord : leftOffset.CompareTo(rightOffset); }
    private static byte ApplyStyleFlag(byte value, byte mask, int setting) => setting == NotesRichTextStyle.StyleNoChange ? value : setting != 0 ? (byte)(value | mask) : (byte)(value & ~mask);
    private void EnsureRangeAlive() { EnsureAlive(); _item.EnsureRichTextAlive(); }
    protected override void ReleaseNative() { }
}

/// <summary>Represents an attachment embedded in a Notes rich-text item.</summary>
public sealed class NotesEmbeddedObject : NotesObject
{
    public const int EmbedAttachment = 1454;

    private readonly NotesRichTextItem _parent;
    private readonly NotesAttachmentMetadata _metadata;

    internal NotesEmbeddedObject(NotesSession session, NotesRichTextItem parent, NotesAttachmentMetadata metadata) : base(session)
    {
        _parent = parent;
        _metadata = metadata;
    }

    public NotesDateTime FileCreated { get { EnsureEmbeddedAlive(); return NotesDateTime.FromNative(Session, _metadata.FileCreated); } }
    public int FileEncoding { get { EnsureEmbeddedAlive(); return _metadata.CompressionType; } }
    public NotesDateTime FileModified { get { EnsureEmbeddedAlive(); return NotesDateTime.FromNative(Session, _metadata.FileModified); } }
    public long FileSize { get { EnsureEmbeddedAlive(); return _metadata.FileSize; } }
    public string Name { get { EnsureEmbeddedAlive(); return _metadata.Name; } }
    public NotesRichTextItem Parent { get { EnsureEmbeddedAlive(); return _parent; } }
    public string Source { get { EnsureEmbeddedAlive(); return _metadata.Source; } }
    public int Type { get { EnsureEmbeddedAlive(); return EmbedAttachment; } }

    public void ExtractFile(string path)
    {
        EnsureEmbeddedAlive(); ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!Session.Api.SaveRichTextAttachment(_parent.Parent.Handle, _parent.Name, _metadata.Name, path)) throw new FileNotFoundException("Attachment could not be extracted.", _metadata.Name);
    }

    public byte[] ToByteArray() { EnsureEmbeddedAlive(); return Session.Api.ReadAttachmentBytes(_parent.Parent.Handle, _metadata.Name); }
    public void Remove()
    {
        EnsureEmbeddedAlive();
        _parent.RemoveEmbeddedObject(_metadata);
        Recycle();
    }
    private void EnsureEmbeddedAlive() { EnsureAlive(); _parent.EnsureRichTextAlive(); }
    protected override void ReleaseNative() { }
}
