using DotNetToDomino.Interop;

namespace DotNetToDomino;

/// <summary>Represents an HCL Notes/Domino NSF database.</summary>
public sealed class NotesDatabase : NotesObject
{
    private readonly object _childrenGate = new();
    private readonly List<NotesObject> _children = [];
    private nint _handle;
    private readonly string _knownReplicaId;

    internal NotesDatabase(NotesSession session, nint handle, string server, string filePath, string? knownReplicaId = null) : base(session)
    {
        _handle = handle;
        _knownReplicaId = knownReplicaId ?? string.Empty;
        Parent = session;
        Server = server?.Trim() ?? string.Empty;
        FilePath = filePath?.Trim() ?? string.Empty;
    }

    public NotesSession Parent { get; }
    public NotesSession SessionObject => Parent;
    public string Server { get; }
    public string FilePath { get; private set; }
    public string FileName => Path.GetFileName(FilePath);
    public bool IsOpen => !IsRecycled && _handle != 0;
    internal nint Handle { get { EnsureOpen(); return _handle; } }

    public string Title { get { EnsureOpen(); return Session.Api.GetDatabaseTitle(_handle); } set { EnsureOpen(); Session.Api.SetDatabaseTitle(_handle, value ?? string.Empty); } }
    public string Categories { get { EnsureOpen(); return Session.Api.GetDatabaseCategories(_handle); } set { EnsureOpen(); Session.Api.SetDatabaseCategories(_handle, value ?? string.Empty); } }
    public string TemplateName { get { EnsureOpen(); return Session.Api.GetDatabaseTemplateName(_handle); } set { EnsureOpen(); Session.Api.SetDatabaseTemplateName(_handle, value ?? string.Empty); } }
    public string DesignTemplateName { get { EnsureOpen(); return Session.Api.GetDatabaseDesignTemplateName(_handle); } set { EnsureOpen(); Session.Api.SetDatabaseDesignTemplateName(_handle, value ?? string.Empty); } }
    public string ReplicaID { get { EnsureAlive(); return IsOpen ? Session.Api.GetDatabaseReplicaId(_handle) : _knownReplicaId; } }
    public long Size { get { EnsureOpen(); return Session.Api.GetDatabaseSpaceUsage(_handle).Size; } }
    public double PercentUsed { get { EnsureOpen(); return Session.Api.GetDatabaseSpaceUsage(_handle).PercentUsed; } }
    public int CurrentAccessLevel { get { EnsureOpen(); return Session.Api.GetDatabaseCurrentAccessLevel(_handle); } }
    public NotesDateTime Created { get { EnsureOpen(); return NotesDateTime.FromNative(Session, Session.Api.GetDatabaseCreated(_handle)); } }
    public NotesDateTime LastModified { get { EnsureOpen(); return NotesDateTime.FromNative(Session, Session.Api.GetDatabaseLastModified(_handle)); } }
    public int FileFormat { get { EnsureOpen(); return Session.Api.GetDatabaseFileFormat(_handle); } }
    public NotesDocumentCollection AllDocuments { get { EnsureOpen(); return Own(new NotesDocumentCollection(Session, this, Session.Api.Search(_handle, "@All", 0))); } }

    public NotesView? GetView(string name)
    {
        EnsureAlive();
        if (!IsOpen) return null;
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        try { return Own(new NotesView(Session, this, Session.Api.OpenView(_handle, name.Trim()), name.Trim())); }
        catch (NotesException) { return null; }
    }

    public NotesView? OpenView(string name) => GetView(name);

    public NotesDocument? GetDocumentByID(uint noteId)
    {
        EnsureAlive();
        if (!IsOpen || noteId == 0) return null;
        var note = Session.Api.TryOpenNote(_handle, noteId);
        return note == 0 ? null : Own(new NotesDocument(Session, this, note, noteId, null));
    }

    public NotesDocument? GetDocumentByNoteId(uint noteId) => GetDocumentByID(noteId);
    public NotesDocument? OpenDocumentByNoteId(uint noteId) => GetDocumentByID(noteId);

    public NotesDocument? GetDocumentByUNID(string unid)
    {
        EnsureAlive();
        if (!IsOpen || string.IsNullOrWhiteSpace(unid)) return null;
        unid = NormalizeUnid(unid);
        var note = Session.Api.TryOpenNoteByUnid(_handle, unid);
        return note == 0 ? null : Own(new NotesDocument(Session, this, note, null, unid));
    }

    public NotesDocument? OpenDocumentByUNID(string unid) => GetDocumentByUNID(unid);

    public NotesDocument CreateDocument()
    {
        EnsureOpen();
        return Own(new NotesDocument(Session, this, Session.Api.CreateNote(_handle), 0, null));
    }

    public NotesDocumentCollection CreateDocumentCollection()
    {
        EnsureOpen();
        return Own(new NotesDocumentCollection(Session, this, []));
    }

    public NotesDocument GetProfileDocument(string profileName, string? profileKey = null)
    {
        EnsureOpen();
        ArgumentException.ThrowIfNullOrWhiteSpace(profileName);
        var note = Session.Api.OpenProfile(_handle, profileName, profileKey ?? string.Empty);
        return Own(new NotesDocument(Session, this, note, Session.Api.GetNoteId(note), null));
    }

    public NotesDocumentCollection Search(string formula, int maxResults = 0)
    {
        EnsureOpen();
        if (maxResults < 0) throw new ArgumentOutOfRangeException(nameof(maxResults));
        return Own(new NotesDocumentCollection(Session, this, Session.Api.Search(_handle, formula, maxResults)));
    }

    public NotesDocumentCollection FTSearch(string query, int maxResults = 0)
    {
        EnsureOpen();
        if (maxResults < 0) throw new ArgumentOutOfRangeException(nameof(maxResults));
        return Own(new NotesDocumentCollection(Session, this, Session.Api.FullTextSearch(_handle, 0, query, maxResults)));
    }

    public NotesDocumentCollection FullTextSearch(string query, int maxResults = 0) => FTSearch(query, maxResults);

    public NotesAgent? GetAgent(string name)
    {
        EnsureOpen();
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var trimmed = name.Trim();
        var noteId = Session.Api.FindAgentNoteId(_handle, trimmed);
        return noteId == 0 ? null : Own(new NotesAgent(Session, this, noteId, trimmed));
    }

    public NotesAgentResult RunAgent(string name, NotesDocument? document = null)
    {
        EnsureOpen();
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return Own(new NotesAgentResult(Session, this, Session.Api.RunAgent(_handle, name.Trim(), document?.Handle ?? 0)));
    }

    public NotesNoteCollection CreateNoteCollection(bool selectAll = false)
    {
        EnsureOpen();
        var collection = Own(new NotesNoteCollection(Session, this));
        collection.SelectAllNotes(selectAll);
        return collection;
    }

    public NotesDatabase CreateCopy(string server, string filePath, int maxSize = 0, bool includeDocuments = false)
    {
        EnsureOpen();
        ValidateLegacyMaxSize(maxSize);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        var targetServer = server?.Trim() ?? string.Empty;
        var targetFile = filePath.Trim();
        var handle = Session.Api.CreateDatabaseCopy(Server, FilePath, targetServer, targetFile, includeDocuments);
        return new NotesDatabase(Session, handle, targetServer, targetFile);
    }

    public void SetReplicaId(string replicaId)
    {
        EnsureOpen();
        ArgumentException.ThrowIfNullOrWhiteSpace(replicaId);
        Session.Api.SetDatabaseReplicaId(_handle, replicaId);
    }

    public void Create(string? server = null, string? filePath = null, bool open = true, int maxSize = 0)
    {
        EnsureAlive();
        ValidateLegacyMaxSize(maxSize);
        var targetServer = string.IsNullOrWhiteSpace(server) ? Server : server.Trim();
        var targetFile = string.IsNullOrWhiteSpace(filePath) ? FilePath : filePath.Trim();
        if (targetFile.Length == 0) throw new ArgumentException("NotesDatabase.Create requires a database file path.", nameof(filePath));
        if (!string.Equals(targetServer, Server, StringComparison.OrdinalIgnoreCase) || !string.Equals(targetFile, FilePath, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("NotesDatabase.Create requires this NotesDatabase object to represent the target server and file path.");
        CloseForDatabaseOperation();
        Session.Api.CreateDatabase(targetServer, targetFile);
        if (open) _handle = Session.Api.OpenDatabase(targetServer, targetFile);
    }

    public void Remove()
    {
        EnsureAlive();
        if (FilePath.Length == 0) throw new InvalidOperationException("NotesDatabase.Remove requires a database file path.");
        CloseForDatabaseOperation();
        Session.Api.DeleteDatabase(Server, FilePath);
    }

    public void RemoveFTIndex()
    {
        EnsureOpen();
        Session.Api.DeleteFullTextIndex(_handle);
    }

    internal T Own<T>(T child) where T : NotesObject
    {
        lock (_childrenGate) _children.Add(child);
        return child;
    }

    internal void UnregisterChild(NotesObject child)
    {
        lock (_childrenGate) _children.Remove(child);
    }

    protected override void ReleaseNative()
    {
        CloseForDatabaseOperation();
    }

    private void CloseForDatabaseOperation()
    {
        while (true)
        {
            NotesObject? child;
            lock (_childrenGate) child = _children.Count == 0 ? null : _children[^1];
            if (child is null) break;
            try { child.Recycle(); }
            catch { UnregisterChild(child); }
        }
        var handle = Interlocked.Exchange(ref _handle, 0);
        if (handle != 0 && Session.Api.IsInitialized) Session.Api.CloseDatabase(handle);
    }

    private void EnsureOpen()
    {
        EnsureAlive();
        if (_handle == 0) throw new InvalidOperationException("NotesDatabase is not open.");
    }

    private static void ValidateLegacyMaxSize(int maxSize)
    {
        if (maxSize < 0 || maxSize > 4) throw new ArgumentOutOfRangeException(nameof(maxSize), "NotesDatabase maxSize must be between 0 and 4 gigabytes.");
    }

    private static string NormalizeUnid(string value)
    {
        value = value.Replace("-", string.Empty, StringComparison.Ordinal).Replace(":", string.Empty, StringComparison.Ordinal).Trim();
        if (value.Length != 32 || value.Any(c => !Uri.IsHexDigit(c))) throw new ArgumentException("Notes UNID must contain exactly 32 hexadecimal characters.", nameof(value));
        return value.ToUpperInvariant();
    }
}
