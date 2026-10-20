using System.Globalization;
using System.Diagnostics;
using DotNetToDomino.Interop;

namespace DotNetToDomino;

/// <summary>Represents an HCL Notes/Domino NSF database.</summary>
[DebuggerDisplay("{DebuggerDisplay,nq}")]
public sealed partial class NotesDatabase : NotesObject
{
    private readonly object _childrenGate = new(); private readonly List<NotesObject> _children = []; private nint _handle; private readonly string _knownReplicaId; private string _server; private string _filePath; private string _openError;
    internal NotesDatabase(NotesSession session, nint handle, string server, string filePath, string? knownReplicaId = null, string? openError = null) : base(session) { _handle = handle; _knownReplicaId = knownReplicaId ?? string.Empty; Parent = session; _server = NotesSession.NormalizeServerName(server); _filePath = filePath?.Trim() ?? string.Empty; _openError = openError ?? string.Empty; }
    private string DebuggerDisplay => $"{nameof(NotesDatabase)}: Title={DebuggerTitle}, FileName={FileName}, {Server}!!{FilePath}, IsOpen={IsOpen}, IsRecycled={IsRecycled}";
    private string DebuggerTitle
    {
        get
        {
            try { return IsOpen ? Title : "(closed)"; }
            catch (Exception ex) { return $"<error: {ex.GetType().Name}>"; }
        }
    }
    public NotesSession Parent { get; } public NotesSession SessionObject => Parent; public string Server => _server; public string FilePath => _filePath; public string FileName => Path.GetFileName(FilePath); public bool IsOpen => !IsRecycled && _handle != 0; public string OpenError { get { EnsureAlive(); return _openError; } } internal nint Handle { get { EnsureOpen(); return _handle; } }
    public string Title { get { if (!IsOpen) return string.Empty; return Session.WithNativeLock(() => Session.Api.GetDatabaseTitle(_handle)); } set { EnsureOpen(); ArgumentNullException.ThrowIfNull(value); Session.WithNativeLock(() => { Session.Api.SetDatabaseTitle(_handle, value); return true; }); } } public string Categories { get { if (!IsOpen) return string.Empty; return Session.WithNativeLock(() => Session.Api.GetDatabaseCategories(_handle)); } set { EnsureOpen(); ArgumentNullException.ThrowIfNull(value); Session.WithNativeLock(() => { Session.Api.SetDatabaseCategories(_handle, value); return true; }); } } public string TemplateName { get { if (!IsOpen) return string.Empty; return Session.WithNativeLock(() => Session.Api.GetDatabaseTemplateName(_handle)); } } public string DesignTemplateName { get { if (!IsOpen) return string.Empty; return Session.WithNativeLock(() => Session.Api.GetDatabaseDesignTemplateName(_handle)); } }
    public string ReplicaID { get { EnsureAlive(); return IsOpen ? Session.Api.GetDatabaseReplicaId(_handle) : _knownReplicaId; } } public long Size { get { EnsureOpen(); return Session.Api.GetDatabaseSpaceUsage(_handle).Size; } } public double PercentUsed { get { EnsureOpen(); return Session.Api.GetDatabaseSpaceUsage(_handle).PercentUsed; } } public int CurrentAccessLevel { get { EnsureOpen(); return Session.Api.GetDatabaseCurrentAccessLevel(_handle); } } public NotesDateTime Created { get { EnsureOpen(); return NotesDateTime.FromNative(Session, Session.Api.GetDatabaseCreated(_handle)); } } public NotesDateTime LastModified { get { EnsureOpen(); return NotesDateTime.FromNative(Session, Session.Api.GetDatabaseLastModified(_handle)); } } public int FileFormat { get { EnsureOpen(); return Session.Api.GetDatabaseFileFormat(_handle); } } public bool IsFTIndexed { get { EnsureAlive(); return IsOpen && Session.Api.IsDatabaseFullTextIndexed(_handle); } } public NotesDateTime? LastFTIndexed { get { EnsureAlive(); if (!IsOpen) return null; var value = Session.Api.GetDatabaseLastFullTextIndexed(_handle); return value.HasValue ? NotesDateTime.FromNative(Session, value.Value) : null; } }
    public NotesDocumentCollection? AllDocuments { get { if (!IsOpen) return null; return Own(new NotesDocumentCollection(Session, this, Session.Api.Search(_handle, "@All", 0))); } } public IReadOnlyList<NotesAgent> Agents { get { if (!IsOpen) return []; return GetAgents(); } } public IReadOnlyList<NotesForm> Forms { get { if (!IsOpen) return []; return GetForms(); } } public IReadOnlyList<NotesView> Views { get { if (!IsOpen) return []; return GetViews(); } }
    public NotesDatabaseAccess QueryAccess() => QueryAccess(Session.UserName);
    /// <summary>Creates a DQL query configuration owned by this database.</summary>
    public NotesDominoQuery CreateDominoQuery() { EnsureOpen(); return Own(new NotesDominoQuery(Session, this)); }
    /// <summary>Reads an editable copy of the ACL. Call ACL.Save() to persist changes.</summary>
    public NotesACL? ACL { get { if (!IsOpen) return null; return Own(new NotesACL(Session, this)); } }
    public NotesDatabaseAccess QueryAccess(string? userName) { EnsureOpen(); var name = string.IsNullOrWhiteSpace(userName) ? Session.UserName : userName.Trim(); return Own(new NotesDatabaseAccess(Session, this, Session.Api.QueryDatabaseAccess(_handle, name))); }
    public NotesDatabase OpenByReplicaID(string replicaId) => Session.OpenByReplicaID(Server, replicaId); public NotesDatabase OpenByReplicaID(string server, string replicaId) => Session.OpenByReplicaID(server, replicaId);
    public bool Open(string? server = null, string? filePath = null) { EnsureAlive(); var rs = NotesSession.NormalizeServerName(server); var rf = filePath?.Trim() ?? string.Empty; var assigned = _server.Length != 0 || _filePath.Length != 0; if (assigned) { if (rs.Length != 0 && !string.Equals(rs, _server, StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("NotesDatabase.Open cannot reassign the database server.", nameof(server)); if (rf.Length != 0 && !string.Equals(rf, _filePath, StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("NotesDatabase.Open cannot reassign the database file path.", nameof(filePath)); } else { if (rf.Length == 0) throw new ArgumentException("NotesDatabase.Open requires a database file path for an unassigned database.", nameof(filePath)); _server = rs; _filePath = rf; } if (IsOpen) return true; try { _handle = Session.Api.OpenDatabase(rs.Length == 0 ? _server : rs, rf.Length == 0 ? _filePath : rf); _openError = string.Empty; return _handle != 0; } catch (NotesException ex) { _openError = ex.Message; return false; } }
    public NotesView? GetView(string name) { EnsureAlive(); if (!IsOpen) return null; ArgumentException.ThrowIfNullOrWhiteSpace(name); try { return Own(new NotesView(Session, this, Session.Api.OpenView(_handle, name.Trim()), name.Trim())); } catch (NotesException) { return null; } } public NotesView? OpenView(string name) => GetView(name);
    public NotesDocument? GetDocumentByID(uint id) { EnsureAlive(); if (id == 0) throw new ArgumentOutOfRangeException(nameof(id), id, "Notes Note ID must be greater than zero."); if (!IsOpen) return null; var note = Session.Api.TryOpenNote(_handle, id, !Session.ConvertMIME); return note == 0 ? null : Own(new NotesDocument(Session, this, note, id, null)); } public NotesDocument? GetDocumentByID(string id) => GetDocumentByID(ParseNoteId(id)); public NotesDocument? GetDocumentByNoteId(uint id) => GetDocumentByID(id); public NotesDocument? GetDocumentByNoteId(string id) => GetDocumentByID(id); public NotesDocument? OpenDocumentByNoteId(uint id) => GetDocumentByID(id); public NotesDocument? OpenDocumentByNoteId(string id) => GetDocumentByID(id);
    public NotesDocument? GetDocumentByUNID(string unid) { EnsureAlive(); ArgumentException.ThrowIfNullOrWhiteSpace(unid); if (!IsOpen) return null; unid = NormalizeUnid(unid); var note = Session.Api.TryOpenNoteByUnid(_handle, unid, !Session.ConvertMIME); return note == 0 ? null : Own(new NotesDocument(Session, this, note, null, unid)); } public NotesDocument? OpenDocumentByUNID(string unid) => GetDocumentByUNID(unid);
    public void Sign()
    {
        EnsureOpen();
        using var design = CreateNoteCollection(false);
        design.SelectAllDesignElements(true);
        design.BuildCollection();
        foreach (var noteId in design)
            Session.Api.SignNoteById(_handle, noteId);
    }

    public NotesDocument CreateDocument() { EnsureOpen(); return Own(new NotesDocument(Session, this, Session.Api.CreateNote(_handle), 0, null)); } public NotesDocumentCollection CreateDocumentCollection() { EnsureOpen(); return Own(new NotesDocumentCollection(Session, this, [])); }
    public NotesDocumentCollection? GetModifiedDocuments() => GetModifiedDocuments(null, NotesConst.DBMOD_DOC_DATA); public NotesDocumentCollection? GetModifiedDocuments(NotesDateTime? since) => GetModifiedDocuments(since, NotesConst.DBMOD_DOC_DATA); public NotesDocumentCollection? GetModifiedDocuments(NotesDateTime? since, int noteClass) { EnsureAlive(); if (!IsOpen) return null; const int supported = 0x0F5D; if (noteClass != NotesConst.DBMOD_DOC_ALL && (noteClass <= 0 || (noteClass & ~supported) != 0)) throw new ArgumentOutOfRangeException(nameof(noteClass)); var modified = Session.Api.GetModifiedNoteIds(_handle, checked((ushort)noteClass), since?.NativeValue); var collection = Own(new NotesDocumentCollection(Session, this, modified.NoteIds)); collection.InitializeModifiedMetadata(modified.UntilTime, modified.DocumentNoteIds, modified.DesignNoteIds); return collection; }
    public NotesDocument GetProfileDocument(string profileName, string? profileKey = null) { EnsureOpen(); ArgumentException.ThrowIfNullOrWhiteSpace(profileName); var note = Session.Api.OpenProfile(_handle, profileName, profileKey ?? string.Empty); return Own(new NotesDocument(Session, this, note, Session.Api.GetNoteId(note), null)); }
    public NotesDocumentCollection Search(string formula, int maxResults = 0) { EnsureOpen(); ArgumentException.ThrowIfNullOrWhiteSpace(formula); if (maxResults < 0) throw new ArgumentOutOfRangeException(nameof(maxResults), maxResults, "'maxResults' cannot be negative."); return Own(new NotesDocumentCollection(Session, this, Session.Api.Search(_handle, formula.Trim(), maxResults))); } public NotesDocumentCollection FTSearch(string query, int maxResults = 0) { EnsureOpen(); ArgumentException.ThrowIfNullOrWhiteSpace(query); if (maxResults < 0) throw new ArgumentOutOfRangeException(nameof(maxResults), maxResults, "'maxResults' cannot be negative."); return Own(new NotesDocumentCollection(Session, this, Session.Api.FullTextSearch(_handle, 0, query.Trim(), maxResults))); } public NotesDocumentCollection FullTextSearch(string query, int maxResults = 0) => FTSearch(query, maxResults);
    /// <summary>Returns NoteIDs readable by the authenticated Notes session user.</summary>
    public IReadOnlyList<uint> GetAllowedNoteIds()
    {
        EnsureOpen();
        return Session.Api.Search(_handle, "@All", 0);
    }
    /// <summary>Runs full-text search against a supplied NoteID filter and returns one page of results.</summary>
    public NotesDocumentCollection FTSearch(string query, IReadOnlyCollection<uint> noteIds, int start = 0, int count = 0)
    {
        EnsureOpen(); ArgumentException.ThrowIfNullOrWhiteSpace(query); ArgumentNullException.ThrowIfNull(noteIds);
        if (start < 0) throw new ArgumentOutOfRangeException(nameof(start));
        if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
        var allowed = noteIds.Where(id => id != 0).ToHashSet();
        var matches = Session.Api.FullTextSearch(_handle, 0, query.Trim(), 0).Where(allowed.Contains);
        if (start > 0) matches = matches.Skip(start);
        if (count > 0) matches = matches.Take(count);
        return Own(new NotesDocumentCollection(Session, this, matches.ToArray()));
    }
    public NotesDocumentCollection FullTextSearch(string query, IReadOnlyCollection<uint> noteIds, int start = 0, int count = 0) => FTSearch(query, noteIds, start, count);
    public NotesAgent? GetAgent(string name) { EnsureOpen(); ArgumentException.ThrowIfNullOrWhiteSpace(name); var trimmed = name.Trim(); var id = Session.Api.FindAgentNoteIdWithPrivateLookup(_handle, trimmed); return id == 0 ? null : Own(new NotesAgent(Session, this, id, trimmed)); } public NotesAgentResult RunAgent(string name, NotesDocument? document = null) { EnsureOpen(); var agent = GetAgent(name) ?? throw new NotesException($"Notes agent '{name}' was not found."); return document is null ? agent.Run() : agent.RunWithDocumentContext(document); }
    public NotesNoteCollection CreateNoteCollection(bool selectAll = false) { EnsureOpen(); var c = Own(new NotesNoteCollection(Session, this)); c.SelectAllNotes(selectAll); return c; }
    public NotesDatabase CreateCopy(string server, string filePath, int maxSize = 0, bool includeDocuments = false) { EnsureOpen(); ValidateLegacyMaxSize(maxSize); ArgumentException.ThrowIfNullOrWhiteSpace(filePath); var s = NotesSession.NormalizeServerName(server); var f = filePath.Trim(); return new NotesDatabase(Session, Session.Api.CreateDatabaseCopy(Server, FilePath, s, f, includeDocuments), s, f); }
    /// <summary>Creates a replica copy that retains this database's replica ID.</summary>
    public NotesDatabase CreateReplica(string server, string filePath, int maxSize = 0, bool includeDocuments = true)
    {
        EnsureOpen(); ValidateLegacyMaxSize(maxSize); ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        var s = NotesSession.NormalizeServerName(server); var f = filePath.Trim();
        if (s.Length > 0 && Path.IsPathRooted(f)) throw new ArgumentException("An absolute database path is valid only when server is empty.", nameof(filePath));
        return new NotesDatabase(Session, Session.Api.CreateDatabaseCopy(Server, FilePath, s, f, includeDocuments, true), s, f);
    }
    public void SetReplicaId(string replicaId) { EnsureOpen(); ArgumentException.ThrowIfNullOrWhiteSpace(replicaId); Session.Api.SetDatabaseReplicaId(_handle, replicaId); } public void Create(string? server = null, string? filePath = null, bool open = true, int maxSize = 0, byte encryptionStrength = 0) { EnsureAlive(); ValidateLegacyMaxSize(maxSize); if (encryptionStrength > 3) throw new ArgumentOutOfRangeException(nameof(encryptionStrength), "Use 0 (none), 1 (simple), 2 (medium), or 3 (strong)."); var s = string.IsNullOrWhiteSpace(server) ? _server : NotesSession.NormalizeServerName(server); var f = string.IsNullOrWhiteSpace(filePath) ? _filePath : filePath.Trim(); if (f.Length == 0) throw new ArgumentException("NotesDatabase.Create requires a database file path."); if (s.Length > 0 && Path.IsPathRooted(f)) throw new ArgumentException("An absolute database path is valid only when server is empty.", nameof(filePath)); CloseForDatabaseOperation(); Session.Api.CreateDatabase(s, f, encryptionStrength); _server = s; _filePath = f; _openError = string.Empty; if (open) _handle = Session.Api.OpenDatabase(s, f); }
    public void Remove() { EnsureAlive(); if (_filePath.Length == 0) throw new InvalidOperationException("NotesDatabase has no database file path."); var s = _server; var f = _filePath; CloseForDatabaseOperation(); Session.Api.DeleteDatabase(s, f); } public void RemoveFTIndex() { EnsureOpen(); Session.Api.DeleteFullTextIndex(_handle); }
    /// <summary>Compacts a local database using the Domino C API.</summary>
    public uint Compact(ushort options = 0)
    {
        EnsureAlive();
        if (Server.Length != 0) throw new InvalidOperationException("Compact is supported only for local databases.");
        if (FilePath.Length == 0) throw new InvalidOperationException("NotesDatabase has no database file path.");
        CloseForDatabaseOperation();
        return Session.Api.CompactDatabase(FilePath, options);
    }
    internal T Own<T>(T child) where T : NotesObject { lock (_childrenGate) _children.Add(child); return child; } internal void UnregisterChild(NotesObject child) { lock (_childrenGate) _children.Remove(child); } protected override void ReleaseNative() => CloseForDatabaseOperation();
    private IReadOnlyList<NotesAgent> GetAgents() { var r = new List<NotesAgent>(); foreach (var id in Session.Api.BuildNoteCollection(_handle, "@All", 0x0200)) { var n = FirstDesignName(Session.Api.GetDesignNoteText(_handle, id, "$Title")); if (n.Length > 0) r.Add(Own(new NotesAgent(Session, this, id, n))); } return r; }
    private IReadOnlyList<NotesForm> GetForms() { var r = new List<NotesForm>(); foreach (var id in Session.Api.BuildNoteCollection(_handle, "@All", 0x0004)) { var n = FirstDesignName(Session.Api.GetDesignNoteText(_handle, id, "$Title")); if (n.Length > 0) r.Add(Own(new NotesForm(Session, this, id, n))); } return r; }
    private IReadOnlyList<NotesView> GetViews() { var r = new List<NotesView>(); foreach (var id in Session.Api.BuildNoteCollection(_handle, "@All", 0x0008)) { var n = FirstDesignName(Session.Api.GetDesignNoteText(_handle, id, "$Title")); if (n.Length == 0) continue; var v = GetView(n); if (v is not null) r.Add(v); } return r; }
    private static string FirstDesignName(string? title) { if (string.IsNullOrWhiteSpace(title)) return string.Empty; var i = title.IndexOf('|'); return (i >= 0 ? title[..i] : title).Trim(); }
    private void CloseForDatabaseOperation() { while (true) { NotesObject? child; lock (_childrenGate) { child = _children.Count == 0 ? null : _children[^1]; if (child is not null) _children.RemoveAt(_children.Count - 1); } if (child is null) break; try { child.Recycle(); } catch { UnregisterChild(child); } } var h = Interlocked.Exchange(ref _handle, 0); if (h != 0 && Session.Api.IsInitialized) Session.Api.CloseDatabase(h); }
    private void EnsureOpen() { EnsureAlive(); if (_handle == 0) throw new InvalidOperationException("NotesDatabase is not open."); } private static void ValidateLegacyMaxSize(int value) { if (value < 0 || value > 4) throw new ArgumentOutOfRangeException(nameof(value)); }
    internal static uint ParseNoteId(string value) { ArgumentException.ThrowIfNullOrWhiteSpace(value); var text = value.Trim(); if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) text = text[2..]; if (text.Length == 0 || text.Length > 8 || text.Any(c => !Uri.IsHexDigit(c))) throw new ArgumentException("Notes Note ID must contain one to eight hexadecimal characters.", nameof(value)); return uint.Parse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture); }
    private static string NormalizeUnid(string value) { value = value.Replace("-", string.Empty).Replace(":", string.Empty).Trim(); if (value.Length != 32 || value.Any(c => !Uri.IsHexDigit(c))) throw new ArgumentException("Notes UNID must contain 32 hexadecimal characters.", nameof(value)); return value.ToUpperInvariant(); }
}

