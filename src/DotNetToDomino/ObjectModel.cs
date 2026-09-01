namespace DotNetToDomino;

public sealed class NotesDatabase : NotesObject
{
    internal NotesDatabase(NotesSession session, string server, string filePath) : base(session)
    {
        Parent = session;
        Server = server;
        FilePath = filePath;
    }

    public NotesSession Parent { get; }
    public string Server { get; }
    public string FilePath { get; internal set; }
    public string FileName => Path.GetFileName(FilePath);
    public bool IsOpen { get; internal set; }
    public string Title { get; set; } = string.Empty;
    public string Categories { get; set; } = string.Empty;
    public string TemplateName { get; internal set; } = string.Empty;
    public string DesignTemplateName { get; internal set; } = string.Empty;
    public string ReplicaID { get; internal set; } = string.Empty;
    public long Size { get; internal set; }
    public double PercentUsed { get; internal set; }
    public int CurrentAccessLevel { get; internal set; }

    public NotesView OpenView(string name)
    {
        EnsureAlive();
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return new NotesView(Session, this, name.Trim());
    }

    public NotesDocument? GetDocumentByNoteId(uint noteId)
    {
        EnsureAlive();
        return noteId == 0 ? null : new NotesDocument(Session, this, noteId);
    }

    public NotesDocument? GetDocumentByUNID(string unid)
    {
        EnsureAlive();
        if (string.IsNullOrWhiteSpace(unid))
            return null;
        return new NotesDocument(Session, this, 0) { UniversalId = unid.Trim() };
    }

    public NotesDocumentCollection Search(string formula, int maxResults = 0)
    {
        EnsureAlive();
        return new NotesDocumentCollection(Session, this, []);
    }

    public NotesDocumentCollection FTSearch(string query, int maxResults = 0)
    {
        EnsureAlive();
        return new NotesDocumentCollection(Session, this, []);
    }

    public NotesAgent? GetAgent(string name)
    {
        EnsureAlive();
        return string.IsNullOrWhiteSpace(name) ? null : new NotesAgent(Session, this, name.Trim());
    }

    protected override void ReleaseNative() => IsOpen = false;
}

public sealed class NotesView : NotesObject
{
    internal NotesView(NotesSession session, NotesDatabase parent, string name) : base(session)
    {
        Parent = parent;
        Name = name;
    }

    public NotesDatabase Parent { get; }
    public string Name { get; }
    public bool AutoUpdate { get; set; } = true;

    public NotesDocument? GetFirstDocument() { EnsureAlive(); return null; }
    public NotesDocument? GetNextDocument(NotesDocument document) { EnsureAlive(); ArgumentNullException.ThrowIfNull(document); return null; }
    public NotesDocument? GetFirstDocumentByKey(string key, bool exactMatch = true) { EnsureAlive(); return null; }
    public NotesDocumentCollection GetAllDocumentsByKey(string key, bool exactMatch = true) { EnsureAlive(); return new NotesDocumentCollection(Session, Parent, []); }
    public NotesDocumentCollection FTSearch(string query, int maxResults = 0) { EnsureAlive(); return new NotesDocumentCollection(Session, Parent, []); }
    public void Refresh() => EnsureAlive();

    protected override void ReleaseNative() { }
}

public sealed class NotesDocumentCollection : NotesObject, IReadOnlyList<uint>
{
    private IReadOnlyList<uint> _noteIds;

    internal NotesDocumentCollection(NotesSession session, NotesDatabase parent, IReadOnlyList<uint> noteIds) : base(session)
    {
        Parent = parent;
        _noteIds = noteIds;
    }

    public NotesDatabase Parent { get; }
    public int Count => _noteIds.Count;
    public uint this[int index] => _noteIds[index];
    public string? FirstNoteId => Count == 0 ? null : _noteIds[0].ToString("X8");
    public string GetNoteIdString(int index) => _noteIds[index].ToString("X8");
    public IEnumerator<uint> GetEnumerator() => _noteIds.GetEnumerator();
    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    protected override void ReleaseNative() => _noteIds = Array.Empty<uint>();
}

public sealed class NotesNoteCollection : NotesObject, IReadOnlyList<uint>
{
    private IReadOnlyList<uint> _noteIds;

    internal NotesNoteCollection(NotesSession session, NotesDatabase parent, IReadOnlyList<uint> noteIds) : base(session)
    {
        Parent = parent;
        _noteIds = noteIds;
    }

    public NotesDatabase Parent { get; }
    public int Count => _noteIds.Count;
    public uint this[int index] => _noteIds[index];
    public IEnumerator<uint> GetEnumerator() => _noteIds.GetEnumerator();
    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    protected override void ReleaseNative() => _noteIds = Array.Empty<uint>();
}

public sealed class NotesDocument : NotesObject
{
    internal NotesDocument(NotesSession session, NotesDatabase parent, uint noteId) : base(session)
    {
        ParentDatabase = parent;
        NoteId = noteId;
    }

    public NotesDatabase ParentDatabase { get; }
    public uint NoteId { get; internal set; }
    public string NoteIdHex => NoteId.ToString("X8");
    public string UniversalId { get; internal set; } = string.Empty;
    public IReadOnlyList<string> Items { get; internal set; } = Array.Empty<string>();

    public NotesItem? GetFirstItem(string itemName) { EnsureAlive(); return string.IsNullOrWhiteSpace(itemName) ? null : new NotesItem(Session, this, itemName); }
    public NotesItem CreateNotesItem(string itemName) { EnsureAlive(); ArgumentException.ThrowIfNullOrWhiteSpace(itemName); return new NotesItem(Session, this, itemName); }
    public NotesItem ReplaceItemValue(string itemName, object? value) { EnsureAlive(); ArgumentException.ThrowIfNullOrWhiteSpace(itemName); return new NotesItem(Session, this, itemName); }
    public bool HasItem(string itemName) { EnsureAlive(); return false; }
    public object? GetValue(string itemName) { EnsureAlive(); return null; }
    public object?[] GetItemValue(string itemName) { EnsureAlive(); return []; }
    public void RemoveItem(string itemName) => EnsureAlive();
    public void Save() => EnsureAlive();

    protected override void ReleaseNative() { }
}

public class NotesItem : NotesObject
{
    internal NotesItem(NotesSession session, NotesDocument parent, string name) : base(session)
    {
        Parent = parent;
        Name = name;
    }

    public NotesDocument Parent { get; }
    public string Name { get; }
    public bool IsAuthors { get; set; }
    public bool IsEncrypted { get; set; }
    public bool IsNames { get; set; }
    public bool IsProtected { get; set; }
    public bool IsReaders { get; set; }
    public bool IsSigned { get; set; }
    public bool IsSummary { get; set; }
    public string Text { get; internal set; } = string.Empty;
    public int Type { get; internal set; }
    public long ValueLength { get; internal set; }
    public object?[] Values { get; set; } = [];

    protected override void ReleaseNative() { }
}

public sealed class NotesRichTextItem : NotesItem
{
    internal NotesRichTextItem(NotesSession session, NotesDocument parent, string name) : base(session, parent, name) { }
}

public sealed class NotesName : NotesObject
{
    internal NotesName(NotesSession session, string value) : base(session) => Original = value;
    public string Original { get; }
    public string Canonical { get; internal set; } = string.Empty;
    public string Common { get; internal set; } = string.Empty;
    protected override void ReleaseNative() { }
}

public sealed class NotesDateTime : NotesObject
{
    internal NotesDateTime(NotesSession session, string value) : base(session) => Text = value;
    internal NotesDateTime(NotesSession session, DateTimeOffset value) : base(session) { Value = value; Text = value.ToString("O"); }
    public DateTimeOffset? Value { get; internal set; }
    public string Text { get; internal set; }
    protected override void ReleaseNative() { }
}

public sealed class NotesAgent : NotesObject
{
    internal NotesAgent(NotesSession session, NotesDatabase parent, string name) : base(session) { Parent = parent; Name = name; }
    public NotesDatabase Parent { get; }
    public string Name { get; }
    public NotesAgentResult Run(NotesDocument? document = null) { EnsureAlive(); return new NotesAgentResult(Session, this); }
    protected override void ReleaseNative() { }
}

public sealed class NotesAgentResult : NotesObject
{
    internal NotesAgentResult(NotesSession session, NotesAgent agent) : base(session) => Agent = agent;
    public NotesAgent Agent { get; }
    public int Status { get; internal set; }
    public string Output { get; internal set; } = string.Empty;
    protected override void ReleaseNative() { }
}

public sealed class NotesStream : NotesObject
{
    private MemoryStream _stream = new();
    internal NotesStream(NotesSession session) : base(session) { }
    public long Position { get => _stream.Position; set => _stream.Position = value; }
    public long Length => _stream.Length;
    public void Write(ReadOnlySpan<byte> data) { EnsureAlive(); _stream.Write(data); }
    public byte[] ToArray() { EnsureAlive(); return _stream.ToArray(); }
    protected override void ReleaseNative() { _stream.Dispose(); _stream = new MemoryStream(); }
}

public sealed class NotesDXLImporter : NotesObject
{
    internal NotesDXLImporter(NotesSession session) : base(session) { }
    public void Import(NotesStream stream, NotesDatabase database) { EnsureAlive(); ArgumentNullException.ThrowIfNull(stream); ArgumentNullException.ThrowIfNull(database); }
    protected override void ReleaseNative() { }
}

public sealed class NotesDXLExporter : NotesObject
{
    internal NotesDXLExporter(NotesSession session) : base(session) { }
    public NotesStream Export(NotesDocument document) { EnsureAlive(); ArgumentNullException.ThrowIfNull(document); return new NotesStream(Session); }
    protected override void ReleaseNative() { }
}
