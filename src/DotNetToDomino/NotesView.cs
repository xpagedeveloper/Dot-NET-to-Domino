namespace DotNetToDomino;

/// <summary>Represents a Notes view collection.</summary>
public sealed class NotesView : NotesObject
{
    private nint _handle;

    internal NotesView(NotesSession session, NotesDatabase parent, nint handle, string name) : base(session)
    {
        Parent = parent;
        _handle = handle;
        Name = name;
    }

    public NotesDatabase Parent { get; }
    public string Name { get; }
    public string[] ColumnNames { get { EnsureOpen(); return Session.Api.GetViewColumnNames(Parent.Handle, Name); } }
    public bool AutoUpdate { get; set; } = true;
    public NotesViewEntryCollection AllEntries => CreateViewEntryCollection();
    internal nint Handle { get { EnsureOpen(); return _handle; } }

    public NotesDocument? GetFirstDocument()
    {
        var ids = SnapshotIds(1);
        return ids.Count == 0 ? null : Parent.GetDocumentByID(ids[0]);
    }

    public NotesDocument? GetNextDocument(NotesDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var ids = SnapshotIds();
        var index = Array.IndexOf(ids.ToArray(), document.NoteId);
        return index < 0 || index + 1 >= ids.Count ? null : Parent.GetDocumentByID(ids[index + 1]);
    }

    public NotesDocument? GetDocumentByKey(string key, bool exactMatch = true) => GetFirstDocumentByKey(key, exactMatch);

    public NotesDocument? GetFirstDocumentByKey(string key, bool exactMatch = true)
    {
        EnsureOpen();
        var ids = Session.Api.FindViewByTextKey(_handle, key ?? string.Empty, 1, exactMatch);
        return ids.Count == 0 ? null : Parent.GetDocumentByID(ids[0]);
    }

    public NotesDocumentCollection GetAllDocumentsByKey(string key, bool exactMatch = true)
    {
        EnsureOpen();
        return Parent.Own(new NotesDocumentCollection(Session, Parent, Session.Api.FindViewByTextKey(_handle, key ?? string.Empty, 0, exactMatch)));
    }

    public NotesDocumentCollection FTSearch(string query, int maxResults = 0)
    {
        EnsureOpen();
        if (maxResults < 0) throw new ArgumentOutOfRangeException(nameof(maxResults));
        return Parent.Own(new NotesDocumentCollection(Session, Parent, Session.Api.FullTextSearch(Parent.Handle, _handle, query, maxResults)));
    }

    public NotesDocumentCollection FullTextSearch(string query, int maxResults = 0) => FTSearch(query, maxResults);

    public void Refresh()
    {
        EnsureOpen();
        Session.Api.UpdateCollection(_handle);
    }

    public NotesViewEntryCollection CreateViewEntryCollection() => NotesViewNavigationExtensions.CreateViewEntryCollection(this);
    public NotesViewNavigator CreateViewNav() => NotesViewNavigationExtensions.CreateViewNav(this);
    public NotesViewNavigator CreateViewNav(int cacheSize) => NotesViewNavigationExtensions.CreateViewNav(this, cacheSize);
    public NotesViewNavigator CreateViewNavMaxLevel(int maxLevel) => NotesViewNavigationExtensions.CreateViewNavMaxLevel(this, maxLevel);
    public NotesViewNavigator CreateViewNavFromCategory(string category) => NotesViewNavigationExtensions.CreateViewNavFromCategory(this, category);
    public NotesViewNavigator CreateViewNavFrom(NotesViewEntry entry) => NotesViewNavigationExtensions.CreateViewNavFrom(this, entry);
    public NotesViewNavigator CreateViewNavFromChildren(NotesViewEntry entry) => NotesViewNavigationExtensions.CreateViewNavFromChildren(this, entry);
    public NotesViewNavigator CreateViewNavFromDescendants(NotesViewEntry entry) => NotesViewNavigationExtensions.CreateViewNavFromDescendants(this, entry);
    public NotesViewNavigator CreateViewNavFromAllUnread(string? userName = null) => NotesViewNavigationExtensions.CreateViewNavFromAllUnread(this, userName);
    public NotesViewEntry? GetEntryByKey(string key) => NotesViewNavigationExtensions.GetEntryByKey(this, key);
    public NotesViewEntryCollection GetAllEntriesByKey(string key) => NotesViewNavigationExtensions.GetAllEntriesByKey(this, key);
    public NotesViewEntryCollection GetAllReadEntries(string? userName = null) => NotesViewNavigationExtensions.GetAllReadEntries(this, userName);
    public NotesViewEntryCollection GetAllUnreadEntries(string? userName = null) => NotesViewNavigationExtensions.GetAllUnreadEntries(this, userName);
    public void MarkAllRead(string? userName = null) => NotesViewNavigationExtensions.MarkAllRead(this, userName);
    public void MarkAllUnread(string? userName = null) => NotesViewNavigationExtensions.MarkAllUnread(this, userName);

    protected override void ReleaseNative()
    {
        var handle = _handle;
        _handle = 0;
        try { if (handle != 0 && Session.Api.IsInitialized) Session.Api.CloseView(handle); }
        finally { Parent.UnregisterChild(this); }
    }

    private IReadOnlyList<uint> SnapshotIds(int maximum = 0)
    {
        EnsureOpen();
        if (AutoUpdate) Session.Api.UpdateCollection(_handle);
        return Session.Api.ReadViewNoteIds(_handle, maximum);
    }

    private void EnsureOpen()
    {
        EnsureAlive();
        if (_handle == 0) throw new InvalidOperationException("NotesView is not open.");
    }
}
