namespace DotNetToDomino;

public sealed class NotesDatabase : NotesObject
{
    private nint _handle;
    private readonly string _knownReplicaId;

    internal NotesDatabase(NotesSession session, nint handle, string server, string filePath, string? knownReplicaId = null) : base(session)
    {
        _handle = handle;
        _knownReplicaId = knownReplicaId ?? string.Empty;
        Parent = session;
        Server = server;
        FilePath = filePath;
    }

    public NotesSession Parent { get; }
    public string Server { get; }
    public string FilePath { get; internal set; }
    public string FileName => Path.GetFileName(FilePath);
    public bool IsOpen => _handle != 0;

    public string Title
    {
        get
        {
            EnsureOpen();
            return Session.Api.GetDatabaseTitle(_handle);
        }
        set
        {
            EnsureOpen();
            Session.Api.SetDatabaseTitle(_handle, value ?? string.Empty);
        }
    }

    public string Categories
    {
        get
        {
            EnsureOpen();
            return Session.Api.GetDatabaseCategories(_handle);
        }
        set
        {
            EnsureOpen();
            Session.Api.SetDatabaseCategories(_handle, value ?? string.Empty);
        }
    }

    public string TemplateName
    {
        get
        {
            EnsureOpen();
            return Session.Api.GetDatabaseTemplateName(_handle);
        }
    }

    public string DesignTemplateName
    {
        get
        {
            EnsureOpen();
            return Session.Api.GetDatabaseDesignTemplateName(_handle);
        }
    }

    public string ReplicaID
    {
        get
        {
            if (!IsOpen)
                return _knownReplicaId;
            EnsureAlive();
            return Session.Api.GetDatabaseReplicaId(_handle);
        }
    }

    public long Size
    {
        get
        {
            EnsureOpen();
            return Session.Api.GetDatabaseSpaceUsage(_handle).Size;
        }
    }

    public double PercentUsed
    {
        get
        {
            EnsureOpen();
            return Session.Api.GetDatabaseSpaceUsage(_handle).PercentUsed;
        }
    }

    public int CurrentAccessLevel
    {
        get
        {
            EnsureOpen();
            return Session.Api.GetDatabaseCurrentAccessLevel(_handle);
        }
    }

    internal nint Handle
    {
        get
        {
            EnsureOpen();
            return _handle;
        }
    }

    public NotesView OpenView(string name)
    {
        EnsureOpen();
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return new NotesView(Session, this, name.Trim());
    }

    public NotesDocument? GetDocumentByNoteId(uint noteId)
    {
        EnsureOpen();
        if (noteId == 0)
            return null;

        var note = Session.Api.TryOpenNote(_handle, noteId);
        return note == 0 ? null : new NotesDocument(Session, this, note, noteId, null);
    }

    public NotesDocument? GetDocumentByUNID(string unid)
    {
        EnsureOpen();
        if (string.IsNullOrWhiteSpace(unid))
            return null;

        unid = NormalizeUnid(unid);
        var note = Session.Api.TryOpenNoteByUnid(_handle, unid);
        return note == 0 ? null : new NotesDocument(Session, this, note, null, unid);
    }

    public NotesDocumentCollection Search(string formula, int maxResults = 0)
    {
        EnsureOpen();
        return new NotesDocumentCollection(Session, this, []);
    }

    public NotesDocumentCollection FTSearch(string query, int maxResults = 0)
    {
        EnsureOpen();
        return new NotesDocumentCollection(Session, this, []);
    }

    public NotesAgent? GetAgent(string name)
    {
        EnsureOpen();
        return string.IsNullOrWhiteSpace(name) ? null : new NotesAgent(Session, this, name.Trim());
    }

    protected override void ReleaseNative()
    {
        var handle = _handle;
        _handle = 0;
        if (handle != 0 && Session.Api.IsInitialized)
            Session.Api.CloseDatabase(handle);
    }

    private void EnsureOpen()
    {
        EnsureAlive();
        if (_handle == 0)
            throw new InvalidOperationException("NotesDatabase is not open.");
    }

    private static string NormalizeUnid(string value)
    {
        value = value.Replace("-", string.Empty, StringComparison.Ordinal).Replace(":", string.Empty, StringComparison.Ordinal).Trim();
        if (value.Length != 32 || value.Any(c => !Uri.IsHexDigit(c)))
            throw new ArgumentException("Notes UNID must contain exactly 32 hexadecimal characters.", nameof(value));
        return value.ToUpperInvariant();
    }
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
    private nint _handle;

    internal NotesDocument(
        NotesSession session,
        NotesDatabase parent,
        nint handle,
        uint? knownNoteId,
        string? knownUnid) : base(session)
    {
        ParentDatabase = parent;
        _handle = handle;

        try
        {
            NoteId = knownNoteId ?? Session.Api.GetNoteId(handle);
            UniversalId = knownUnid ?? Session.Api.GetUnid(handle);
        }
        catch
        {
            _handle = 0;
            try { Session.Api.CloseNote(handle); }
            finally { Session.Unregister(this); }
            throw;
        }
    }

    public NotesDatabase ParentDatabase { get; }
    public uint NoteId { get; }
    public string NoteIdHex => NoteId.ToString("X8");
    public string UniversalId { get; }
    public IReadOnlyList<string> Items { get; internal set; } = Array.Empty<string>();

    internal nint Handle
    {
        get
        {
            EnsureAlive();
            if (_handle == 0)
                throw new InvalidOperationException("NotesDocument is not open.");
            return _handle;
        }
    }

    public NotesItem? GetFirstItem(string itemName)
    {
        EnsureAlive();
        if (string.IsNullOrWhiteSpace(itemName) || !Session.Api.HasItem(Handle, itemName))
            return null;
        return new NotesItem(Session, this, itemName);
    }

    public NotesItem CreateNotesItem(string itemName)
    {
        EnsureAlive();
        ArgumentException.ThrowIfNullOrWhiteSpace(itemName);
        Session.Api.SetItemText(Handle, itemName, string.Empty);
        return new NotesItem(Session, this, itemName);
    }

    public NotesItem ReplaceItemValue(string itemName, object? value)
    {
        EnsureAlive();
        ArgumentException.ThrowIfNullOrWhiteSpace(itemName);
        Session.Api.SetItemValue(Handle, itemName, value);
        return new NotesItem(Session, this, itemName);
    }

    public bool HasItem(string itemName)
    {
        EnsureAlive();
        ArgumentException.ThrowIfNullOrWhiteSpace(itemName);
        return Session.Api.HasItem(Handle, itemName);
    }

    public object? GetValue(string itemName)
    {
        EnsureAlive();
        ArgumentException.ThrowIfNullOrWhiteSpace(itemName);
        return Session.Api.GetItemValue(Handle, itemName);
    }

    public object?[] GetItemValue(string itemName)
    {
        var value = GetValue(itemName);
        return value is null ? [] : [value];
    }

    public string GetString(string itemName)
    {
        EnsureAlive();
        ArgumentException.ThrowIfNullOrWhiteSpace(itemName);
        return Session.Api.GetItemText(Handle, itemName);
    }

    public double GetNumber(string itemName)
    {
        EnsureAlive();
        ArgumentException.ThrowIfNullOrWhiteSpace(itemName);
        return Session.Api.GetItemNumber(Handle, itemName);
    }

    public void SetValue(string itemName, object? value)
    {
        EnsureAlive();
        ArgumentException.ThrowIfNullOrWhiteSpace(itemName);
        Session.Api.SetItemValue(Handle, itemName, value);
    }

    public void SetString(string itemName, string value)
    {
        EnsureAlive();
        ArgumentException.ThrowIfNullOrWhiteSpace(itemName);
        Session.Api.SetItemText(Handle, itemName, value ?? string.Empty);
    }

    public void SetNumber(string itemName, double value)
    {
        EnsureAlive();
        ArgumentException.ThrowIfNullOrWhiteSpace(itemName);
        Session.Api.SetItemNumber(Handle, itemName, value);
    }

    public void RemoveItem(string itemName)
    {
        EnsureAlive();
        ArgumentException.ThrowIfNullOrWhiteSpace(itemName);
        Session.Api.DeleteItem(Handle, itemName);
    }

    public void Save()
    {
        EnsureAlive();
        Session.Api.SaveNote(Handle);
    }

    protected override void ReleaseNative()
    {
        var handle = _handle;
        _handle = 0;
        if (handle != 0 && Session.Api.IsInitialized)
            Session.Api.CloseNote(handle);
    }
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
    public string Text { get { EnsureAlive(); return Session.Api.GetItemText(Parent.Handle, Name); } }
    public int Type { get; internal set; }
    public long ValueLength { get; internal set; }
    public object?[] Values { get { EnsureAlive(); return Parent.GetItemValue(Name); } }

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
