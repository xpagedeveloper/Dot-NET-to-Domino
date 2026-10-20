using System.Globalization;
using System.Diagnostics;
using DotNetToDomino.Interop;

namespace DotNetToDomino;

/// <summary>In-memory collection of Notes document note IDs.</summary>
[DebuggerDisplay("{DebuggerDisplay,nq}")]
public sealed class NotesDocumentCollection : NotesObject, IReadOnlyCollection<uint>
{
    private uint[] _noteIds;
    private readonly string _replicaId;
    private int _lastFetchedIndex = -1;
    private uint _lastFetchedNoteId;
    private bool _hasModifiedMetadata;
    private NotesTimeDate _untilTime;
    private string DebuggerDisplay => $"{nameof(NotesDocumentCollection)}: Count={_noteIds.Length}, FirstNoteId={( _noteIds.Length == 0 ? "(none)" : _noteIds[0].ToString("X8", CultureInfo.InvariantCulture) )}, IsRecycled={IsRecycled}";
    private uint[]? _documentNoteIds;
    private uint[]? _designNoteIds;
    private readonly NotesDatabase _parent;

    internal NotesDocumentCollection(NotesSession session, NotesDatabase parent, IEnumerable<uint> noteIds) : base(session)
    {
        _parent = parent;
        _noteIds = noteIds.Select(NormalizeNoteId).Distinct().ToArray();
        _replicaId = parent.IsOpen ? session.Api.GetDatabaseReplicaId(parent.Handle) : string.Empty;
    }

    public NotesDatabase Parent { get { EnsureAlive(); return _parent; } }
    public int Count { get { EnsureAlive(); return _noteIds.Length; } }
    /// <summary>Gets all NoteIDs in this collection without opening the documents.</summary>
    public IReadOnlyList<uint> NoteIds { get { EnsureAlive(); return _noteIds.ToArray(); } }
    /// <summary>Gets all NoteIDs in this collection as eight-character hexadecimal strings.</summary>
    public IReadOnlyList<string> NoteIdStrings { get { EnsureAlive(); return _noteIds.Select(id => id.ToString("X8", CultureInfo.InvariantCulture)).ToArray(); } }
    public NotesDocumentCollection Clone() { EnsureAlive(); return Parent.Own(new NotesDocumentCollection(Session, Parent, _noteIds)); }
    public string? FirstNoteId => Count == 0 ? null : _noteIds[0].ToString("X8", CultureInfo.InvariantCulture);
    public NotesDateTime? UntilTime { get { EnsureAlive(); return _hasModifiedMetadata ? NotesDateTime.FromNative(Session, _untilTime) : null; } }
    public NotesDocumentCollection Documents { get { EnsureAlive(); return _hasModifiedMetadata && _documentNoteIds is not null ? CreateModifiedSubset(_documentNoteIds, true) : Parent.Own(new NotesDocumentCollection(Session, Parent, [])); } }
    public NotesDocumentCollection DesignElements { get { EnsureAlive(); return _hasModifiedMetadata && _designNoteIds is not null ? CreateModifiedSubset(_designNoteIds, false) : Parent.Own(new NotesDocumentCollection(Session, Parent, [])); } }

    public bool Contains(NotesDocument document)
    {
        ArgumentNullException.ThrowIfNull(document); EnsureAlive(); EnsureSameReplica(document.ParentDatabase);
        return Array.IndexOf(_noteIds, document.NoteId) >= 0;
    }

    public bool Contains(NotesDocumentCollection collection)
    {
        ArgumentNullException.ThrowIfNull(collection); EnsureAlive(); EnsureSameReplica(collection.Parent);
        var current = _noteIds.ToHashSet(); return collection._noteIds.All(current.Contains);
    }

    internal void InitializeModifiedMetadata(NotesTimeDate untilTime, IEnumerable<uint> documentNoteIds, IEnumerable<uint> designNoteIds)
    {
        _untilTime = untilTime;
        _documentNoteIds = documentNoteIds.Select(NormalizeNoteId).Distinct().ToArray();
        _designNoteIds = designNoteIds.Select(NormalizeNoteId).Distinct().ToArray();
        _hasModifiedMetadata = true;
    }

    public NotesDocument? GetFirstDocument()
    {
        EnsureAlive();
        return _noteIds.Length == 0 ? ResetAndNull() : OpenAt(0);
    }

    public NotesDocument? GetLastDocument() => EnsureAndOpen(_noteIds.Length - 1);
    public NotesDocument? GetNthDocument(int index) => EnsureAndOpen(index - 1);
    public NotesDocument? GetPrevDocument(NotesDocument document)
    {
        ArgumentNullException.ThrowIfNull(document); EnsureAlive(); EnsureSameReplica(document.ParentDatabase);
        var index = Array.IndexOf(_noteIds, document.NoteId);
        return EnsureAndOpen(index - 1);
    }

    public NotesDocument? GetNextDocument(NotesDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        EnsureAlive();
        EnsureSameReplica(document.ParentDatabase);
        var index = document.NoteId == _lastFetchedNoteId && _lastFetchedIndex >= 0 && _lastFetchedIndex < _noteIds.Length && _noteIds[_lastFetchedIndex] == document.NoteId
            ? _lastFetchedIndex
            : Array.IndexOf(_noteIds, document.NoteId);
        return index < 0 || index + 1 >= _noteIds.Length ? ResetAndNull() : OpenAt(index + 1);
    }

    public NotesDocument? GetDocument(uint noteId)
    {
        EnsureAlive();
        var index = Array.IndexOf(_noteIds, noteId);
        return index < 0 ? null : OpenAt(index);
    }

    public NotesDocument? GetDocument(string noteId) => GetDocument(NotesDatabase.ParseNoteId(noteId));

    public NotesDocument? GetDocument(NotesDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        EnsureSameReplica(document.ParentDatabase);
        return GetDocument(document.NoteId);
    }

    public string GetNoteIdString(int index) { EnsureAlive(); return _noteIds[index].ToString("X8", CultureInfo.InvariantCulture); }
    public string Get(int index) => GetNoteIdString(index);

    public void AddDocument(NotesDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        EnsureAlive();
        EnsureSameReplica(document.ParentDatabase);
        if (Array.IndexOf(_noteIds, document.NoteId) < 0) _noteIds = [.. _noteIds, document.NoteId];
    }

    public void AddDocument(NotesDocumentCollection collection)
    {
        ArgumentNullException.ThrowIfNull(collection);
        EnsureAlive();
        EnsureSameReplica(collection.Parent);
        var ids = new List<uint>(_noteIds);
        var seen = new HashSet<uint>(_noteIds);
        foreach (var id in collection._noteIds) if (seen.Add(id)) ids.Add(id);
        _noteIds = ids.ToArray();
    }

    public void Merge(NotesDocument document) { AddDocument(document); }
    public void Merge(NotesDocumentCollection collection) { AddDocument(collection); }
    public void Intersect(NotesDocument document)
    {
        ArgumentNullException.ThrowIfNull(document); EnsureAlive(); EnsureSameReplica(document.ParentDatabase);
        if (Array.IndexOf(_noteIds, document.NoteId) < 0) _noteIds = [];
        ResetCursor();
    }
    public void Intersect(NotesDocumentCollection collection)
    {
        ArgumentNullException.ThrowIfNull(collection); EnsureAlive(); EnsureSameReplica(collection.Parent);
        var keep = collection._noteIds.ToHashSet(); _noteIds = _noteIds.Where(keep.Contains).ToArray(); ResetCursor();
    }
    public void Subtract(NotesDocument document) { RemoveDocument(document); }
    public void Subtract(NotesDocumentCollection collection) { RemoveDocument(collection); }

    public void RemoveDocument(NotesDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        EnsureSameReplica(document.ParentDatabase);
        RemoveIds([document.NoteId]);
    }

    public void RemoveDocument(NotesDocumentCollection collection)
    {
        ArgumentNullException.ThrowIfNull(collection);
        EnsureSameReplica(collection.Parent);
        RemoveIds(collection._noteIds);
    }

    public void StampAll(string itemName, object? value)
    {
        EnsureAlive(); ArgumentException.ThrowIfNullOrWhiteSpace(itemName);
        foreach (var noteId in _noteIds.ToArray()) using (var document = Parent.GetDocumentByID(noteId) ?? throw new NotesException(4091, $"NotesDocument {noteId:X8} could not be opened while stamping.")) { document.SetValue(itemName.Trim(), value); document.Save(); }
    }

    public void MarkAllRead(string? userName = null) { EnsureAlive(); foreach (var id in _noteIds) Session.Api.SetDocumentUnread(Parent.Handle, id, string.IsNullOrWhiteSpace(userName) ? Session.UserName : userName, false); }
    public void MarkAllUnread(string? userName = null) { EnsureAlive(); foreach (var id in _noteIds) Session.Api.SetDocumentUnread(Parent.Handle, id, string.IsNullOrWhiteSpace(userName) ? Session.UserName : userName, true); }
    public void PutAllInFolder(string folderName, bool createOnFail = false) { EnsureAlive(); ArgumentException.ThrowIfNullOrWhiteSpace(folderName); foreach (var id in _noteIds) Session.Api.PutDocumentInFolder(Parent.Handle, id, folderName.Trim(), createOnFail); ResetCursor(); }
    public void RemoveAllFromFolder(string folderName) { EnsureAlive(); ArgumentException.ThrowIfNullOrWhiteSpace(folderName); foreach (var id in _noteIds) Session.Api.RemoveDocumentFromFolder(Parent.Handle, id, folderName.Trim()); ResetCursor(); }
    public void RemoveAll(bool force = false) { EnsureAlive(); _noteIds = _noteIds.Where(id => !Session.Api.DeleteNote(Parent.Handle, id, force)).ToArray(); ResetCursor(); }

    /// <summary>Copies all items from a source document to every document in the collection and saves them.</summary>
    public void StampAllMulti(NotesDocument sourceDocument)
    {
        EnsureAlive();
        ArgumentNullException.ThrowIfNull(sourceDocument);
        EnsureSameReplica(sourceDocument.ParentDatabase);
        foreach (var noteId in _noteIds.ToArray())
        {
            if (sourceDocument.NoteId != 0 && (sourceDocument.NoteId & 0x7fffffffu) == noteId) continue;
            using var document = Parent.GetDocumentByID(noteId) ?? throw new NotesException(4091, $"NotesDocument {noteId:X8} could not be opened while stamping.");
            sourceDocument.CopyAllItems(document, true);
            document.Save();
        }
    }

    public IEnumerator<uint> GetEnumerator() { EnsureAlive(); return ((IEnumerable<uint>)_noteIds).GetEnumerator(); }
    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

    protected override void ReleaseNative()
    {
        _noteIds = [];
        _documentNoteIds = null;
        _designNoteIds = null;
        _hasModifiedMetadata = false;
        ResetCursor();
        _parent.UnregisterChild(this);
    }

    private NotesDocumentCollection CreateModifiedSubset(uint[] noteIds, bool documents)
    {
        var subset = Parent.Own(new NotesDocumentCollection(Session, Parent, noteIds));
        subset.InitializeModifiedMetadata(_untilTime, documents ? noteIds : [], documents ? [] : noteIds);
        return subset;
    }

    private void EnsureModifiedMetadata()
    {
        if (_hasModifiedMetadata && _documentNoteIds is not null && _designNoteIds is not null) return;
        throw new InvalidOperationException("Documents and DesignElements are available only on collections returned by NotesDatabase.GetModifiedDocuments.");
    }

    private NotesDocument? OpenAt(int index)
    {
        var document = Parent.GetDocumentByID(_noteIds[index]);
        if (document is null) return ResetAndNull();
        _lastFetchedIndex = index;
        _lastFetchedNoteId = _noteIds[index];
        return document;
    }

    private NotesDocument? EnsureAndOpen(int index)
    {
        EnsureAlive();
        if (index < 0 || index >= _noteIds.Length) return ResetAndNull();
        return OpenAt(index);
    }

    private NotesDocument? ResetAndNull() { ResetCursor(); return null; }
    private void ResetCursor() { _lastFetchedIndex = -1; _lastFetchedNoteId = 0; }
    private void RemoveIds(IEnumerable<uint> ids)
    {
        EnsureAlive();
        var remove = ids.ToHashSet();
        _noteIds = _noteIds.Where(id => !remove.Contains(id)).ToArray();
        ResetCursor();
    }
    private void EnsureSameReplica(NotesDatabase database)
    {
        var replicaId = database.IsOpen ? Session.Api.GetDatabaseReplicaId(database.Handle) : database.ReplicaID;
        if (!string.Equals(_replicaId, replicaId, StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Notes object belongs to a different database replica.", nameof(database));
    }
    private static uint NormalizeNoteId(uint noteId) => noteId & 0x7fffffffu;
}

/// <summary>Selection-driven collection of Notes data and design notes.</summary>
public sealed class NotesNoteCollection : NotesObject, IReadOnlyCollection<uint>
{
    private uint[] _noteIds = [];
    private bool _built;
    private DateTimeOffset _lastBuildUtc;
    private readonly NotesDatabase _parent;

    internal NotesNoteCollection(NotesSession session, NotesDatabase parent) : base(session) { _parent = parent; }

    public NotesDatabase Parent { get { EnsureAlive(); return _parent; } }
    public int Count { get { EnsureAlive(); RequireBuilt(); return _noteIds.Length; } }
    public string SelectionFormula { get; set; } = "@All";
    public NotesDateTime? SinceTime { get; set; }
    public NotesDateTime LastBuildTime { get { EnsureAlive(); RequireBuilt(); return new NotesDateTime(Session, _lastBuildUtc); } }

    public bool SelectACL { get; set; }
    public bool SelectActions { get; set; }
    public bool SelectAgents { get; set; }
    public bool SelectDatabaseScript { get; set; }
    public bool SelectDataConnections { get; set; }
    public bool SelectDocuments { get; set; }
    public bool SelectFolders { get; set; }
    public bool SelectForms { get; set; }
    public bool SelectFrameSets { get; set; }
    public bool SelectHelpAbout { get; set; }
    public bool SelectHelpIndex { get; set; }
    public bool SelectHelpUsing { get; set; }
    public bool SelectIcon { get; set; }
    public bool SelectImageResources { get; set; }
    public bool SelectJavaResources { get; set; }
    public bool SelectMiscCodeElements { get; set; }
    public bool SelectMiscFormatElements { get; set; }
    public bool SelectMiscIndexElements { get; set; }
    public bool SelectNavigators { get; set; }
    public bool SelectOutlines { get; set; }
    public bool SelectPages { get; set; }
    public bool SelectProfiles { get; set; }
    public bool SelectReplicationFormulas { get; set; }
    public bool SelectScriptLibraries { get; set; }
    public bool SelectSharedFields { get; set; }
    public bool SelectStyleSheetResources { get; set; }
    public bool SelectSubforms { get; set; }
    public bool SelectViews { get; set; }
    internal IReadOnlyList<uint> NoteIds { get { EnsureAlive(); RequireBuilt(); return _noteIds; } }

    public void BuildCollection()
    {
        EnsureAlive();
        _noteIds = Session.Api.BuildNoteCollection(Parent.Handle, SelectionFormula, BuildSelectionMask(), SinceTime?.NativeValue).Distinct().OrderBy(id => id).ToArray();
        _built = true;
        _lastBuildUtc = DateTimeOffset.UtcNow;
    }

    public void ClearCollection() { EnsureAlive(); _noteIds = []; _built = false; }
    public string GetFirstNoteID() { EnsureAlive(); RequireBuilt(); return _noteIds.Length == 0 ? string.Empty : _noteIds[0].ToString("X", CultureInfo.InvariantCulture); }
    public string GetNextNoteID(uint noteId) { EnsureAlive(); RequireBuilt(); var index = Array.IndexOf(_noteIds, noteId); return index < 0 || index + 1 >= _noteIds.Length ? string.Empty : _noteIds[index + 1].ToString("X", CultureInfo.InvariantCulture); }
    public string GetNextNoteID(string noteId) => GetNextNoteID(NotesDatabase.ParseNoteId(noteId));

    public void Add(uint noteId) => Mutate([noteId], 0);
    public void Add(string noteId) => Add(NotesDatabase.ParseNoteId(noteId));
    public void Add(NotesDocument document) { ArgumentNullException.ThrowIfNull(document); EnsureSameReplica(document.ParentDatabase); Mutate([document.NoteId], 0); }
    public void Add(NotesDocumentCollection collection) { ArgumentNullException.ThrowIfNull(collection); EnsureSameReplica(collection.Parent); Mutate(collection.NoteIds, 0); }
    public void Add(NotesNoteCollection collection) { ArgumentNullException.ThrowIfNull(collection); EnsureSameReplica(collection.Parent); Mutate(collection.NoteIds, 0); }
    public void Remove(uint noteId) => Mutate([noteId], 1);
    public void Remove(string noteId) => Remove(NotesDatabase.ParseNoteId(noteId));
    public void Remove(NotesDocument document) { ArgumentNullException.ThrowIfNull(document); EnsureSameReplica(document.ParentDatabase); Mutate([document.NoteId], 1); }
    public void Remove(NotesDocumentCollection collection) { ArgumentNullException.ThrowIfNull(collection); EnsureSameReplica(collection.Parent); Mutate(collection.NoteIds, 1); }
    public void Remove(NotesNoteCollection collection) { ArgumentNullException.ThrowIfNull(collection); EnsureSameReplica(collection.Parent); Mutate(collection.NoteIds, 1); }
    public void Intersect(NotesDocumentCollection collection) { ArgumentNullException.ThrowIfNull(collection); EnsureSameReplica(collection.Parent); Mutate(collection.NoteIds, 2); }
    public void Intersect(NotesNoteCollection collection) { ArgumentNullException.ThrowIfNull(collection); EnsureSameReplica(collection.Parent); Mutate(collection.NoteIds, 2); }

    /// <summary>Copies all items from a source document to every selected note and saves them.</summary>
    public void StampAllMulti(NotesDocument sourceDocument)
    {
        EnsureAlive();
        RequireBuilt();
        ArgumentNullException.ThrowIfNull(sourceDocument);
        EnsureSameReplica(sourceDocument.ParentDatabase);
        foreach (var noteId in _noteIds.ToArray())
        {
            if (sourceDocument.NoteId != 0 && (sourceDocument.NoteId & 0x7fffffffu) == noteId) continue;
            using var document = Parent.GetDocumentByID(noteId) ?? throw new NotesException(4091, $"NotesDocument {noteId:X8} could not be opened while stamping.");
            sourceDocument.CopyAllItems(document, true);
            document.Save();
        }
    }

    public void SelectAllAdminNotes(bool value) { SelectACL = value; SelectReplicationFormulas = value; }
    public void SelectAllCodeElements(bool value) { SelectAgents = value; SelectDatabaseScript = value; SelectDataConnections = value; SelectMiscCodeElements = value; SelectOutlines = value; SelectScriptLibraries = value; }
    public void SelectAllDataNotes(bool value) { SelectDocuments = value; SelectProfiles = value; }
    public void SelectAllFormatElements(bool value) { SelectActions = value; SelectForms = value; SelectFrameSets = value; SelectImageResources = value; SelectJavaResources = value; SelectMiscFormatElements = value; SelectPages = value; SelectStyleSheetResources = value; SelectSubforms = value; }
    public void SelectAllIndexElements(bool value) { SelectFolders = value; SelectMiscIndexElements = value; SelectNavigators = value; SelectViews = value; }
    public void SelectAllDesignElements(bool value) { SelectAllCodeElements(value); SelectAllFormatElements(value); SelectAllIndexElements(value); SelectHelpAbout = value; SelectHelpIndex = value; SelectHelpUsing = value; SelectIcon = value; SelectSharedFields = value; }
    public void SelectAllNotes(bool value) { SelectAllAdminNotes(value); SelectAllDesignElements(value); SelectDocuments = value; SelectProfiles = value; }

    public IEnumerator<uint> GetEnumerator() { EnsureAlive(); RequireBuilt(); return ((IEnumerable<uint>)_noteIds).GetEnumerator(); }
    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

    protected override void ReleaseNative()
    {
        _noteIds = [];
        _built = false;
        _parent.UnregisterChild(this);
    }

    private uint BuildSelectionMask()
    {
        uint mask = 0;
        if (SelectDocuments || SelectProfiles) mask |= 0x0001;
        if (SelectACL) mask |= 0x0040;
        if (SelectReplicationFormulas) mask |= 0x0800;
        if (SelectForms || SelectSubforms || SelectActions || SelectPages || SelectImageResources || SelectJavaResources || SelectMiscFormatElements || SelectFrameSets || SelectSharedFields || SelectIcon || SelectHelpAbout || SelectHelpIndex || SelectHelpUsing) mask |= 0x0004;
        if (SelectViews || SelectFolders || SelectNavigators || SelectMiscIndexElements) mask |= 0x0008;
        if (SelectAgents || SelectDatabaseScript || SelectDataConnections || SelectMiscCodeElements || SelectOutlines || SelectScriptLibraries) mask |= 0x0200;
        return mask;
    }

    private void Mutate(IEnumerable<uint> values, int operation)
    {
        EnsureAlive(); RequireBuilt();
        var set = new HashSet<uint>(_noteIds);
        if (operation == 0) set.UnionWith(values);
        else if (operation == 1) set.ExceptWith(values);
        else set.IntersectWith(values);
        _noteIds = set.OrderBy(id => id).ToArray();
    }
    private void RequireBuilt() { if (!_built) throw new InvalidOperationException("NotesNoteCollection.BuildCollection must be called before using the collection."); }
    private void EnsureSameReplica(NotesDatabase database)
    {
        if (!string.Equals(Parent.ReplicaID, database.ReplicaID, StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Notes object belongs to a different database replica.", nameof(database));
    }
}
