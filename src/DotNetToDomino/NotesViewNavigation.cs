using DotNetToDomino.Interop;

namespace DotNetToDomino;

/// <summary>Navigation and entry helpers for <see cref="NotesView"/>.</summary>
public static class NotesViewNavigationExtensions
{
    public static NotesViewEntryCollection CreateViewEntryCollection(this NotesView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        return new NotesViewEntryCollection(view.Parent.Parent, view, view.Parent, ReadRows(view).Where(r => r.IsDocument));
    }

    public static NotesViewEntryCollection GetAllEntries(this NotesView view) => view.CreateViewEntryCollection();

    public static NotesViewNavigator CreateViewNav(this NotesView view) => CreateViewNav(view, 64);

    public static NotesViewNavigator CreateViewNav(this NotesView view, int cacheSize)
    {
        ArgumentNullException.ThrowIfNull(view);
        return new NotesViewNavigator(view.Parent.Parent, view, view.Parent, ReadRows(view), cacheSize);
    }

    public static NotesViewNavigator CreateViewNavMaxLevel(this NotesView view, int maxLevel)
    {
        ArgumentNullException.ThrowIfNull(view);
        if (maxLevel < 0) throw new ArgumentOutOfRangeException(nameof(maxLevel), maxLevel, "'maxLevel' cannot be negative.");
        return new NotesViewNavigator(view.Parent.Parent, view, view.Parent, ReadRows(view).Where(r => r.Level <= maxLevel));
    }

    public static NotesViewNavigator CreateViewNavFromCategory(this NotesView view, string category)
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentException.ThrowIfNullOrWhiteSpace(category);
        category = category.Trim();
        var rows = ReadRows(view);
        var root = rows.FirstOrDefault(r => r.Kind == NotesViewEntryKind.Category && r.GetColumnValues().Any(v => string.Equals(Convert.ToString(v), category, StringComparison.OrdinalIgnoreCase)));
        return root is null
            ? new NotesViewNavigator(view.Parent.Parent, view, view.Parent, [])
            : new NotesViewNavigator(view.Parent.Parent, view, view.Parent, rows.Where(r => ReferenceEquals(r, root) || IsDescendant(root, r)));
    }

    public static NotesViewNavigator CreateViewNavFrom(this NotesView view, NotesViewEntry entry)
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(entry);
        var root = entry.Row;
        return new NotesViewNavigator(view.Parent.Parent, view, view.Parent, ReadRows(view).Where(r => SameRow(root, r) || IsDescendant(root, r)));
    }

    public static NotesViewNavigator CreateViewNavFromChildren(this NotesView view, NotesViewEntry entry)
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(entry);
        var root = entry.Row;
        return new NotesViewNavigator(view.Parent.Parent, view, view.Parent, ReadRows(view).Where(r => IsDirectChild(root, r)));
    }

    public static NotesViewNavigator CreateViewNavFromDescendants(this NotesView view, NotesViewEntry entry)
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(entry);
        var root = entry.Row;
        return new NotesViewNavigator(view.Parent.Parent, view, view.Parent, ReadRows(view).Where(r => IsDescendant(root, r)));
    }

    public static NotesViewEntry? GetEntryByKey(this NotesView view, string key)
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        var ids = view.Parent.Parent.Api.FindViewByTextKey(view.Handle, key.Trim(), 1, true);
        if (ids.Count == 0) return null;
        var row = ReadRows(view).FirstOrDefault(r => r.IsDocument && r.NoteId == ids[0]);
        return row is null ? null : new NotesViewEntry(view.Parent.Parent, view, view.Parent, row);
    }

    public static NotesViewEntryCollection GetAllEntriesByKey(this NotesView view, string key)
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        var ids = new HashSet<uint>(view.Parent.Parent.Api.FindViewByTextKey(view.Handle, key.Trim(), 0, true));
        return new NotesViewEntryCollection(view.Parent.Parent, view, view.Parent, ReadRows(view).Where(r => r.IsDocument && ids.Contains(r.NoteId)));
    }

    public static NotesViewEntryCollection GetAllReadEntries(this NotesView view, string? userName = null)
    {
        ArgumentNullException.ThrowIfNull(view);
        var session = view.Parent.Parent;
        var user = string.IsNullOrWhiteSpace(userName) ? session.UserName : userName;
        return new NotesViewEntryCollection(session, view, view.Parent, ReadRows(view).Where(r => r.IsDocument && !session.Api.IsDocumentUnread(view.Parent.Handle, r.NoteId, user)));
    }

    public static NotesViewEntryCollection GetAllUnreadEntries(this NotesView view, string? userName = null)
    {
        ArgumentNullException.ThrowIfNull(view);
        var session = view.Parent.Parent;
        var user = string.IsNullOrWhiteSpace(userName) ? session.UserName : userName;
        return new NotesViewEntryCollection(session, view, view.Parent, ReadRows(view).Where(r => r.IsDocument && session.Api.IsDocumentUnread(view.Parent.Handle, r.NoteId, user)));
    }

    public static NotesViewNavigator CreateViewNavFromAllUnread(this NotesView view, string? userName = null)
    {
        ArgumentNullException.ThrowIfNull(view);
        var session = view.Parent.Parent;
        var user = string.IsNullOrWhiteSpace(userName) ? session.UserName : userName;
        return new NotesViewNavigator(session, view, view.Parent, ReadRows(view).Where(r => r.IsDocument && session.Api.IsDocumentUnread(view.Parent.Handle, r.NoteId, user)));
    }

    public static void MarkAllRead(this NotesView view, string? userName = null) => MarkAll(view, userName, false);
    public static void MarkAllUnread(this NotesView view, string? userName = null) => MarkAll(view, userName, true);

    private static void MarkAll(NotesView view, string? userName, bool unread)
    {
        ArgumentNullException.ThrowIfNull(view);
        var session = view.Parent.Parent;
        var user = string.IsNullOrWhiteSpace(userName) ? session.UserName : userName;
        foreach (var row in ReadRows(view))
            if (row.IsDocument) session.Api.SetDocumentUnread(view.Parent.Handle, row.NoteId, user, unread);
    }

    internal static NotesViewRow[] ReadRows(NotesView view)
    {
        var session = view.Parent.Parent;
        return session.Api.ReadViewRows(view.Handle, view.Parent, view.Name, session);
    }

    internal static bool SameRow(NotesViewRow left, NotesViewRow right) =>
        ReferenceEquals(left, right) || (left.NoteId == right.NoteId && string.Equals(left.Position, right.Position, StringComparison.Ordinal));

    internal static bool IsDescendant(NotesViewRow parent, NotesViewRow candidate) =>
        candidate.Level > parent.Level && candidate.Position.StartsWith(parent.Position + ".", StringComparison.Ordinal);

    internal static bool IsDirectChild(NotesViewRow parent, NotesViewRow candidate) =>
        candidate.Level == parent.Level + 1 && candidate.Position.StartsWith(parent.Position + ".", StringComparison.Ordinal);

    internal static string ParentPosition(string position)
    {
        var index = position.LastIndexOf('.');
        return index < 0 ? string.Empty : position[..index];
    }
}

/// <summary>Represents one document, category, or total row in a Notes view.</summary>
public sealed class NotesViewEntry : NotesObject
{
    private NotesViewRow? _row;
    private readonly NotesView _view;
    private readonly NotesDatabase _database;

    internal NotesViewEntry(NotesSession session, NotesView view, NotesDatabase database, NotesViewRow row) : base(session)
    {
        _view = view;
        _database = database;
        _row = row;
    }

    internal NotesViewRow Row { get { EnsureAlive(); return _row ?? throw new ObjectDisposedException(nameof(NotesViewEntry)); } }
    public NotesView Parent { get { EnsureAlive(); return _view; } }
    public NotesDocument? Document { get { EnsureAlive(); return Row.IsDocument ? _database.GetDocumentByNoteId(Row.NoteId) : null; } }
    public string NoteID => Row.IsDocument ? Row.NoteId.ToString("X8", System.Globalization.CultureInfo.InvariantCulture) : string.Empty;
    public string UniversalID
    {
        get
        {
            using var document = Document;
            return document?.UniversalId ?? string.Empty;
        }
    }
    public object?[] ColumnValues => Row.GetColumnValues();
    public int IndentLevel => Row.IndentLevel;
    /// <summary>Gets the zero-based view hierarchy level.</summary>
    public int Level => Row.Level;
    /// <summary>Gets the canonical position in the view index.</summary>
    public string Position => Row.Position;
    public int ColumnIndentLevel => Row.IndentLevel;
    public int ChildCount => NotesViewNavigationExtensions.ReadRows(_view).Count(r => NotesViewNavigationExtensions.IsDirectChild(Row, r));
    public int DescendantCount => NotesViewNavigationExtensions.ReadRows(_view).Count(r => NotesViewNavigationExtensions.IsDescendant(Row, r));
    public int SiblingCount
    {
        get
        {
            var parent = NotesViewNavigationExtensions.ParentPosition(Row.Position);
            return NotesViewNavigationExtensions.ReadRows(_view).Count(r => r.Level == Row.Level && string.Equals(NotesViewNavigationExtensions.ParentPosition(r.Position), parent, StringComparison.Ordinal));
        }
    }
    public int FTSearchScore => Row.FTSearchScore;
    public bool IsConflict => false;
    public bool IsDocument => Row.Kind == NotesViewEntryKind.Document;
    public bool IsCategory => Row.Kind == NotesViewEntryKind.Category;
    public bool IsTotal => Row.Kind == NotesViewEntryKind.Total;
    /// <summary>Gets whether this document is unread for the current session user.</summary>
    public bool IsUnread => Row.IsDocument && Session.Api.IsDocumentUnread(_database.Handle, Row.NoteId, Session.UserName);
    /// <summary>Gets whether the current user can open this entry's document.</summary>
    public bool HasReaderAccess => !Row.IsDocument || Document is not null;
    public bool IsValid => !IsRecycled && _row is not null;
    public string GetPosition(string separator = ".")
    {
        ArgumentNullException.ThrowIfNull(separator);
        return separator == "." ? Row.Position : Row.Position.Replace(".", separator, StringComparison.Ordinal);
    }
    public bool GetRead(string? userName = null)
    {
        EnsureAlive();
        if (!Row.IsDocument) return true;
        var user = string.IsNullOrWhiteSpace(userName) ? Session.UserName : userName;
        return !Session.Api.IsDocumentUnread(_database.Handle, Row.NoteId, user);
    }
    protected override void ReleaseNative() => _row = null;
}

/// <summary>Mutable collection of document entries from a Notes view.</summary>
public sealed class NotesViewEntryCollection : NotesObject, IReadOnlyCollection<NotesViewEntry>
{
    private NotesViewRow[] _rows;
    private readonly NotesView _view;
    private readonly NotesDatabase _database;
    private readonly string _replicaId;
    private int _lastFetchedIndex = -1;
    private string _query = string.Empty;

    internal NotesViewEntryCollection(NotesSession session, NotesView view, NotesDatabase database, IEnumerable<NotesViewRow> rows) : base(session)
    {
        _view = view;
        _database = database;
        _rows = rows.Where(r => r.IsDocument).ToArray();
        _replicaId = session.Api.GetDatabaseReplicaId(database.Handle);
    }

    public NotesView Parent { get { EnsureAlive(); return _view; } }
    public int Count { get { EnsureAlive(); return _rows.Length; } }
    /// <summary>Gets the document NoteIDs represented by this view collection.</summary>
    public IReadOnlyList<uint> NoteIds { get { EnsureAlive(); return _rows.Where(row => row.IsDocument).Select(row => row.NoteId).ToArray(); } }
    /// <summary>Gets the document NoteIDs represented by this view collection as hexadecimal strings.</summary>
    public IReadOnlyList<string> NoteIdStrings { get { EnsureAlive(); return NoteIds.Select(id => id.ToString("X8", System.Globalization.CultureInfo.InvariantCulture)).ToArray(); } }
    /// <summary>Gets the number of pages for the supplied page size.</summary>
    public int GetPageCount(int pageSize)
    {
        EnsureAlive();
        if (pageSize < 1) throw new ArgumentOutOfRangeException(nameof(pageSize));
        return (_rows.Length + pageSize - 1) / pageSize;
    }
    public string Query { get { EnsureAlive(); return _query; } }
    public NotesViewEntry? GetFirstEntry() => OpenAt(_rows.Length == 0 ? -1 : 0);
    public NotesViewEntry? GetLastEntry() => OpenAt(_rows.Length == 0 ? -1 : _rows.Length - 1);
    public NotesViewEntry? GetNextEntry(NotesViewEntry entry) => MoveFromEntry(entry, 1);
    public NotesViewEntry? GetPrevEntry(NotesViewEntry entry) => MoveFromEntry(entry, -1);
    public NotesViewEntry? GetNthEntry(int oneBasedIndex) => OpenAt(oneBasedIndex <= 0 ? -1 : oneBasedIndex - 1);

    /// <summary>Returns a one-based page of entries without opening the documents.</summary>
    public IReadOnlyList<NotesViewEntry> GetPage(int oneBasedStart, int pageSize)
    {
        EnsureAlive();
        if (oneBasedStart < 1) throw new ArgumentOutOfRangeException(nameof(oneBasedStart));
        if (pageSize < 1) throw new ArgumentOutOfRangeException(nameof(pageSize));
        var start = oneBasedStart - 1;
        return _rows.Skip(start).Take(pageSize).Select(row => new NotesViewEntry(Session, _view, _database, row)).ToArray();
    }

    /// <summary>Refreshes the collection from the current view index and returns the new count.</summary>
    public int Refresh()
    {
        EnsureAlive();
        var selected = new HashSet<uint>(_rows.Select(row => row.NoteId));
        _rows = NotesViewNavigationExtensions.ReadRows(_view).Where(row => row.IsDocument && selected.Contains(row.NoteId)).ToArray();
        ResetCursor();
        return _rows.Length;
    }

    public NotesViewEntry? GetEntry(NotesDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        EnsureSameReplica(document.ParentDatabase);
        return OpenAt(Array.FindIndex(_rows, r => r.NoteId == document.NoteId));
    }

    public bool Contains(NotesDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        EnsureSameReplica(document.ParentDatabase);
        return Array.Exists(_rows, r => r.NoteId == document.NoteId);
    }

    public void AddEntry(NotesDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        EnsureSameReplica(document.ParentDatabase);
        if (Array.Exists(_rows, r => r.NoteId == document.NoteId)) return;
        var row = NotesViewNavigationExtensions.ReadRows(_view).FirstOrDefault(r => r.IsDocument && r.NoteId == document.NoteId)
            ?? throw new InvalidOperationException("Document is not present in the NotesView.");
        _rows = [.. _rows, row];
        ResetCursor();
    }

    public void DeleteEntry(NotesDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        EnsureSameReplica(document.ParentDatabase);
        _rows = _rows.Where(r => r.NoteId != document.NoteId).ToArray();
        ResetCursor();
    }

    public void RemoveAll() { EnsureAlive(); _rows = []; _query = string.Empty; ResetCursor(); }

    public NotesViewEntryCollection Clone()
    {
        EnsureAlive();
        var clone = new NotesViewEntryCollection(Session, _view, _database, _rows) { _query = _query };
        return clone;
    }

    public void Merge(NotesViewEntryCollection other) => Combine(other, 0);
    public void Intersect(NotesViewEntryCollection other) => Combine(other, 1);
    public void Subtract(NotesViewEntryCollection other) => Combine(other, 2);

    public void FTSearch(string query, int maxDocs = 0)
    {
        EnsureAlive();
        ArgumentException.ThrowIfNullOrWhiteSpace(query);
        if (maxDocs < 0) throw new ArgumentOutOfRangeException(nameof(maxDocs), maxDocs, "'maxDocs' cannot be negative.");
        _query = query.Trim();
        var ids = new HashSet<uint>(Session.Api.FullTextSearch(_database.Handle, _view.Handle, _query, maxDocs));
        _rows = _rows.Where(r => ids.Contains(r.NoteId)).ToArray();
        ResetCursor();
    }

    public void MarkAllRead(string? userName = null) => MarkAll(userName, false);
    public void MarkAllUnread(string? userName = null) => MarkAll(userName, true);

    public void PutAllInFolder(string folderName, bool createOnFail = false)
    {
        EnsureAlive();
        ArgumentException.ThrowIfNullOrWhiteSpace(folderName);
        foreach (var row in _rows) Session.Api.PutDocumentInFolder(_database.Handle, row.NoteId, folderName.Trim(), createOnFail);
        ResetCursor();
    }

    public void RemoveAllFromFolder(string folderName)
    {
        EnsureAlive();
        ArgumentException.ThrowIfNullOrWhiteSpace(folderName);
        foreach (var row in _rows) Session.Api.RemoveDocumentFromFolder(_database.Handle, row.NoteId, folderName.Trim());
        ResetCursor();
    }

    public void StampAll(string itemName, object? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(itemName);
        ForEachDocument(document => { document.SetValue(itemName, value); document.Save(); });
        ResetCursor();
    }

    public void StampAllMulti(IReadOnlyList<string> itemNames, IReadOnlyList<object?> values)
    {
        ArgumentNullException.ThrowIfNull(itemNames);
        ArgumentNullException.ThrowIfNull(values);
        if (itemNames.Count != values.Count) throw new ArgumentException("Item names and values must have matching counts.");
        ForEachDocument(document =>
        {
            for (var i = 0; i < itemNames.Count; i++)
            {
                ArgumentException.ThrowIfNullOrWhiteSpace(itemNames[i]);
                document.SetValue(itemNames[i], values[i]);
            }
            document.Save();
        });
        ResetCursor();
    }

    public void UpdateAll() { ForEachDocument(document => document.Save()); ResetCursor(); }

    public IEnumerator<NotesViewEntry> GetEnumerator()
    {
        EnsureAlive();
        foreach (var row in _rows) yield return new NotesViewEntry(Session, _view, _database, row);
    }
    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

    protected override void ReleaseNative() { _rows = []; ResetCursor(); }

    private void Combine(NotesViewEntryCollection other, int operation)
    {
        ArgumentNullException.ThrowIfNull(other);
        EnsureAlive();
        EnsureSameReplica(other._database);
        if (operation == 0)
        {
            var rows = new List<NotesViewRow>(_rows);
            foreach (var row in other._rows)
                if (!rows.Any(candidate => NotesViewNavigationExtensions.SameRow(candidate, row))) rows.Add(row);
            _rows = rows.ToArray();
        }
        else if (operation == 1)
            _rows = _rows.Where(row => other._rows.Any(candidate => NotesViewNavigationExtensions.SameRow(candidate, row))).ToArray();
        else
            _rows = _rows.Where(row => !other._rows.Any(candidate => NotesViewNavigationExtensions.SameRow(candidate, row))).ToArray();
        ResetCursor();
    }

    private NotesViewEntry? MoveFromEntry(NotesViewEntry entry, int delta)
    {
        ArgumentNullException.ThrowIfNull(entry);
        var row = entry.Row;
        var index = _lastFetchedIndex >= 0 && _lastFetchedIndex < _rows.Length && ReferenceEquals(_rows[_lastFetchedIndex], row)
            ? _lastFetchedIndex
            : Array.FindIndex(_rows, candidate => NotesViewNavigationExtensions.SameRow(candidate, row));
        return OpenAt(index < 0 ? -1 : index + delta);
    }

    private NotesViewEntry? OpenAt(int index)
    {
        EnsureAlive();
        if (index < 0 || index >= _rows.Length) { ResetCursor(); return null; }
        _lastFetchedIndex = index;
        return new NotesViewEntry(Session, _view, _database, _rows[index]);
    }

    private void EnsureSameReplica(NotesDatabase database)
    {
        var replicaId = Session.Api.GetDatabaseReplicaId(database.Handle);
        if (!string.Equals(_replicaId, replicaId, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Notes object belongs to a different database replica.", nameof(database));
    }

    private void ForEachDocument(Action<NotesDocument> action)
    {
        EnsureAlive();
        foreach (var row in _rows)
        {
            using var document = _database.GetDocumentByNoteId(row.NoteId);
            if (document is not null) action(document);
        }
    }

    private void MarkAll(string? userName, bool unread)
    {
        EnsureAlive();
        var user = string.IsNullOrWhiteSpace(userName) ? Session.UserName : userName;
        foreach (var row in _rows) Session.Api.SetDocumentUnread(_database.Handle, row.NoteId, user, unread);
        ResetCursor();
    }

    private void ResetCursor() => _lastFetchedIndex = -1;
}

/// <summary>Cursor-based navigator over document, category, and total rows in a Notes view.</summary>
public sealed class NotesViewNavigator : NotesObject
{
    private NotesViewRow[] _rows;
    private readonly NotesView _view;
    private readonly NotesDatabase _database;
    private readonly string _replicaId;
    private int _currentIndex = -1;
    private int _cacheSize = 64;
    private int _bufferMaxEntries = 64;
    private int _maxLevel = int.MaxValue;

    internal NotesViewNavigator(NotesSession session, NotesView view, NotesDatabase database, IEnumerable<NotesViewRow> rows, int cacheSize = 64) : base(session)
    {
        _view = view;
        _database = database;
        _rows = rows.ToArray();
        _replicaId = session.Api.GetDatabaseReplicaId(database.Handle);
        if (cacheSize < 0 || cacheSize > 512) throw new ArgumentOutOfRangeException(nameof(cacheSize), cacheSize, "'cacheSize' must be between 0 and 512.");
        _bufferMaxEntries = cacheSize;
        _cacheSize = Math.Clamp(cacheSize, 0, 512);
    }

    public NotesView ParentView => _view;
    public int Count { get { EnsureAlive(); return _rows.Count(IsVisible); } }
    public int BufferMaxEntries
    {
        get { EnsureAlive(); _bufferMaxEntries = Math.Clamp(_bufferMaxEntries, 0, 512); return _bufferMaxEntries; }
        set { EnsureAlive(); if (value < 0 || value > 512) throw new ArgumentOutOfRangeException(nameof(value), value, "'BufferMaxEntries' must be between 0 and 512."); _bufferMaxEntries = value; _cacheSize = value; }
    }
    public int CacheSize { get => BufferMaxEntries; set => BufferMaxEntries = value; }
    public int MaxLevel { get { EnsureAlive(); return _maxLevel == int.MaxValue ? 30 : _maxLevel; } set { EnsureAlive(); _maxLevel = Math.Max(0, value); NormalizeCurrent(); } }

    public NotesViewEntry? GetCurrent() => OpenAt(_currentIndex, false);
    public NotesViewEntry? GetFirst() => OpenVisible(0, 1);
    public NotesViewEntry? GetLast() => OpenVisible(_rows.Length - 1, -1);
    public NotesViewEntry? GetNext() => OpenVisible(_currentIndex < 0 ? 0 : _currentIndex + 1, 1);
    public NotesViewEntry? GetPrev() => OpenVisible(_currentIndex < 0 ? _rows.Length - 1 : _currentIndex - 1, -1);
    public NotesViewEntry? GetFirstDocument() => FindType(0, 1, NotesViewEntryKind.Document);
    public NotesViewEntry? GetLastDocument() => FindType(_rows.Length - 1, -1, NotesViewEntryKind.Document);
    public NotesViewEntry? GetNextDocument() => FindType(_currentIndex < 0 ? 0 : _currentIndex + 1, 1, NotesViewEntryKind.Document);
    public NotesViewEntry? GetPrevDocument() => FindType(_currentIndex < 0 ? _rows.Length - 1 : _currentIndex - 1, -1, NotesViewEntryKind.Document);
    public NotesViewEntry? GetNextCategory() => FindType(_currentIndex < 0 ? 0 : _currentIndex + 1, 1, NotesViewEntryKind.Category);
    public NotesViewEntry? GetPrevCategory() => FindType(_currentIndex < 0 ? _rows.Length - 1 : _currentIndex - 1, -1, NotesViewEntryKind.Category);
    public NotesViewEntry? GetNext(NotesViewEntry entry) => MoveRelative(entry, 1, null);
    public NotesViewEntry? GetPrev(NotesViewEntry entry) => MoveRelative(entry, -1, null);
    public NotesViewEntry? GetNextDocument(NotesViewEntry entry) => MoveRelative(entry, 1, NotesViewEntryKind.Document);
    public NotesViewEntry? GetPrevDocument(NotesViewEntry entry) => MoveRelative(entry, -1, NotesViewEntryKind.Document);
    public NotesViewEntry? GetNextCategory(NotesViewEntry entry) => MoveRelative(entry, 1, NotesViewEntryKind.Category);
    public NotesViewEntry? GetPrevCategory(NotesViewEntry entry) => MoveRelative(entry, -1, NotesViewEntryKind.Category);

    public NotesViewEntry? GetEntry(NotesDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        EnsureSameReplica(document.ParentDatabase);
        var index = Array.FindIndex(_rows, r => r.IsDocument && r.NoteId == document.NoteId && IsVisible(r));
        return OpenAt(index, index >= 0);
    }

    public NotesViewEntry? GetChild() => _currentIndex < 0 ? null : GetChild(new NotesViewEntry(Session, _view, _database, _rows[_currentIndex]));
    public NotesViewEntry? GetChild(NotesViewEntry entry)
    {
        var row = entry.Row;
        var index = Array.FindIndex(_rows, r => NotesViewNavigationExtensions.IsDirectChild(row, r) && IsVisible(r));
        return OpenAt(index, index >= 0);
    }

    public NotesViewEntry? GetParent() => _currentIndex < 0 ? null : GetParent(new NotesViewEntry(Session, _view, _database, _rows[_currentIndex]));
    public NotesViewEntry? GetParent(NotesViewEntry entry)
    {
        var parentPosition = NotesViewNavigationExtensions.ParentPosition(entry.Row.Position);
        if (parentPosition.Length == 0) return null;
        var index = Array.FindIndex(_rows, r => string.Equals(r.Position, parentPosition, StringComparison.Ordinal) && IsVisible(r));
        return OpenAt(index, index >= 0);
    }

    public NotesViewEntry? GetNextSibling() => _currentIndex < 0 ? null : FindSibling(new NotesViewEntry(Session, _view, _database, _rows[_currentIndex]), 1);
    public NotesViewEntry? GetNextSibling(NotesViewEntry entry) => FindSibling(entry, 1);
    public NotesViewEntry? GetPrevSibling() => _currentIndex < 0 ? null : FindSibling(new NotesViewEntry(Session, _view, _database, _rows[_currentIndex]), -1);
    public NotesViewEntry? GetPrevSibling(NotesViewEntry entry) => FindSibling(entry, -1);

    public NotesViewEntry? GetNth(int oneBasedIndex)
    {
        EnsureAlive();
        if (oneBasedIndex <= 0) return null;
        var visible = _rows.Where(IsVisible).ToArray();
        if (oneBasedIndex > visible.Length) return null;
        return OpenAt(Array.IndexOf(_rows, visible[oneBasedIndex - 1]), true);
    }

    public NotesViewEntry? GetPos(string position, string separator = ".")
    {
        EnsureAlive();
        ArgumentException.ThrowIfNullOrWhiteSpace(position);
        ArgumentNullException.ThrowIfNull(separator);
        if (!string.IsNullOrEmpty(separator) && separator != ".") position = position.Replace(separator, ".", StringComparison.Ordinal);
        var index = Array.FindIndex(_rows, r => IsVisible(r) && string.Equals(r.Position, position, StringComparison.Ordinal));
        return OpenAt(index, index >= 0);
    }

    public void GotoFirst() => MoveIfFound(GetFirst);
    public void GotoLast() => MoveIfFound(GetLast);
    public void GotoFirstDocument() => MoveIfFound(GetFirstDocument);
    public void GotoLastDocument() => MoveIfFound(GetLastDocument);
    public void GotoNext(NotesViewEntry entry) => MoveIfFound(() => GetNext(entry));
    public void GotoPrev(NotesViewEntry entry) => MoveIfFound(() => GetPrev(entry));
    public void GotoNextDocument(NotesViewEntry entry) => MoveIfFound(() => GetNextDocument(entry));
    public void GotoPrevDocument(NotesViewEntry entry) => MoveIfFound(() => GetPrevDocument(entry));
    public void GotoNextCategory(NotesViewEntry entry) => MoveIfFound(() => GetNextCategory(entry));
    public void GotoPrevCategory(NotesViewEntry entry) => MoveIfFound(() => GetPrevCategory(entry));
    public void GotoChild(NotesViewEntry entry) => MoveIfFound(() => GetChild(entry));
    public void GotoParent(NotesViewEntry entry) => MoveIfFound(() => GetParent(entry));
    public void GotoNextSibling(NotesViewEntry entry) => MoveIfFound(() => GetNextSibling(entry));
    public void GotoPrevSibling(NotesViewEntry entry) => MoveIfFound(() => GetPrevSibling(entry));
    public void GotoEntry(NotesViewEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        var index = Array.FindIndex(_rows, r => NotesViewNavigationExtensions.SameRow(entry.Row, r) && IsVisible(r));
        if (index >= 0) _currentIndex = index;
    }
    public void GotoPos(string position, string separator = ".") => MoveIfFound(() => GetPos(position, separator));

    public void MarkAllRead(string? userName = null) => MarkAll(userName, false);
    public void MarkAllUnread(string? userName = null) => MarkAll(userName, true);

    protected override void ReleaseNative() { _rows = []; _currentIndex = -1; }

    private NotesViewEntry? MoveRelative(NotesViewEntry entry, int step, NotesViewEntryKind? kind)
    {
        ArgumentNullException.ThrowIfNull(entry);
        var index = Array.FindIndex(_rows, r => NotesViewNavigationExtensions.SameRow(entry.Row, r));
        if (index < 0) return null;
        for (var i = index + step; i >= 0 && i < _rows.Length; i += step)
            if (IsVisible(_rows[i]) && (kind is null || _rows[i].Kind == kind.Value)) return OpenAt(i, true);
        return null;
    }

    private NotesViewEntry? FindType(int start, int step, NotesViewEntryKind kind)
    {
        EnsureAlive();
        for (var i = start; i >= 0 && i < _rows.Length; i += step)
            if (IsVisible(_rows[i]) && _rows[i].Kind == kind) return OpenAt(i, true);
        _currentIndex = -1;
        return null;
    }

    private NotesViewEntry? FindSibling(NotesViewEntry entry, int step)
    {
        var row = entry.Row;
        var parent = NotesViewNavigationExtensions.ParentPosition(row.Position);
        var index = Array.FindIndex(_rows, r => NotesViewNavigationExtensions.SameRow(row, r));
        if (index < 0) return null;
        for (var i = index + step; i >= 0 && i < _rows.Length; i += step)
        {
            var candidate = _rows[i];
            if (candidate.Level < row.Level) break;
            if (candidate.Level == row.Level && string.Equals(NotesViewNavigationExtensions.ParentPosition(candidate.Position), parent, StringComparison.Ordinal) && IsVisible(candidate))
                return OpenAt(i, true);
        }
        return null;
    }

    private NotesViewEntry? OpenVisible(int start, int step)
    {
        for (var i = start; i >= 0 && i < _rows.Length; i += step)
            if (IsVisible(_rows[i])) return OpenAt(i, true);
        _currentIndex = -1;
        return null;
    }

    private NotesViewEntry? OpenAt(int index, bool move)
    {
        EnsureAlive();
        if (index < 0 || index >= _rows.Length || !IsVisible(_rows[index])) { if (move) _currentIndex = -1; return null; }
        if (move) _currentIndex = index;
        return new NotesViewEntry(Session, _view, _database, _rows[index]);
    }

    private bool IsVisible(NotesViewRow row) => row.Level <= _maxLevel;
    private void NormalizeCurrent() { if (_currentIndex >= 0 && (_currentIndex >= _rows.Length || !IsVisible(_rows[_currentIndex]))) _currentIndex = -1; }
    private void MoveIfFound(Func<NotesViewEntry?> action)
    {
        var previous = _currentIndex;
        using var entry = action();
        if (entry is null) _currentIndex = previous;
    }
    private void EnsureSameReplica(NotesDatabase database)
    {
        var replicaId = Session.Api.GetDatabaseReplicaId(database.Handle);
        if (!string.Equals(_replicaId, replicaId, StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Notes object belongs to a different database replica.", nameof(database));
    }
    private void MarkAll(string? userName, bool unread)
    {
        EnsureAlive();
        var user = string.IsNullOrWhiteSpace(userName) ? Session.UserName : userName;
        foreach (var row in _rows) if (row.IsDocument) Session.Api.SetDocumentUnread(_database.Handle, row.NoteId, user, unread);
    }
}
