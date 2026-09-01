using DotNetToDomino.Interop;
using System.Globalization;
using System.Text;

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
    public NotesSession SessionObject => Parent;
    public string Server { get; }
    public string FilePath { get; internal set; }
    public string FileName => Path.GetFileName(FilePath);
    public bool IsOpen => !IsRecycled && _handle != 0;

    public string Title { get { EnsureOpen(); return Session.Api.GetDatabaseTitle(_handle); } set { EnsureOpen(); Session.Api.SetDatabaseTitle(_handle, value ?? string.Empty); } }
    public string Categories { get { EnsureOpen(); return Session.Api.GetDatabaseCategories(_handle); } set { EnsureOpen(); Session.Api.SetDatabaseCategories(_handle, value ?? string.Empty); } }
    public string TemplateName { get { EnsureOpen(); return Session.Api.GetDatabaseTemplateName(_handle); } set { EnsureOpen(); Session.Api.SetDatabaseTemplateName(_handle, value ?? string.Empty); } }
    public string DesignTemplateName { get { EnsureOpen(); return Session.Api.GetDatabaseDesignTemplateName(_handle); } set { EnsureOpen(); Session.Api.SetDatabaseDesignTemplateName(_handle, value ?? string.Empty); } }
    public string ReplicaID { get { if (!IsOpen) return _knownReplicaId; EnsureAlive(); return Session.Api.GetDatabaseReplicaId(_handle); } }
    public long Size { get { EnsureOpen(); return Session.Api.GetDatabaseSpaceUsage(_handle).Size; } }
    public double PercentUsed { get { EnsureOpen(); return Session.Api.GetDatabaseSpaceUsage(_handle).PercentUsed; } }
    public int CurrentAccessLevel { get { EnsureOpen(); return Session.Api.GetDatabaseCurrentAccessLevel(_handle); } }

    internal nint Handle { get { EnsureOpen(); return _handle; } }

    public NotesView OpenView(string name)
    {
        EnsureOpen();
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var trimmed = name.Trim();
        return new NotesView(Session, this, Session.Api.OpenView(_handle, trimmed), trimmed);
    }

    public NotesDocument? GetDocumentByNoteId(uint noteId)
    {
        EnsureOpen();
        if (noteId == 0) return null;
        var note = Session.Api.TryOpenNote(_handle, noteId);
        return note == 0 ? null : new NotesDocument(Session, this, note, noteId, null);
    }

    public NotesDocument? GetDocumentByID(uint noteId) => GetDocumentByNoteId(noteId);
    public NotesDocument? OpenDocumentByNoteId(uint noteId) => GetDocumentByNoteId(noteId);

    public NotesDocument? GetDocumentByUNID(string unid)
    {
        EnsureOpen();
        if (string.IsNullOrWhiteSpace(unid)) return null;
        unid = NormalizeUnid(unid);
        var note = Session.Api.TryOpenNoteByUnid(_handle, unid);
        return note == 0 ? null : new NotesDocument(Session, this, note, null, unid);
    }

    public NotesDocument? OpenDocumentByUNID(string unid) => GetDocumentByUNID(unid);

    public NotesDocumentCollection Search(string formula, int maxResults = 0)
    {
        EnsureOpen();
        if (maxResults < 0) throw new ArgumentOutOfRangeException(nameof(maxResults));
        return new NotesDocumentCollection(Session, this, Session.Api.Search(_handle, formula, maxResults));
    }

    public NotesDocumentCollection FTSearch(string query, int maxResults = 0)
    {
        EnsureOpen();
        if (maxResults < 0) throw new ArgumentOutOfRangeException(nameof(maxResults));
        return new NotesDocumentCollection(Session, this, Session.Api.FullTextSearch(_handle, 0, query, maxResults));
    }

    public NotesDocumentCollection FullTextSearch(string query, int maxResults = 0) => FTSearch(query, maxResults);

    public NotesAgent? GetAgent(string name)
    {
        EnsureOpen();
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var trimmed = name.Trim();
        var noteId = Session.Api.FindAgentNoteId(_handle, trimmed);
        return noteId == 0 ? null : new NotesAgent(Session, this, noteId, trimmed);
    }

    public NotesAgentResult RunAgent(string name, NotesDocument? document = null)
    {
        EnsureOpen();
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var output = Session.Api.RunAgent(_handle, name.Trim(), document?.Handle ?? 0);
        return new NotesAgentResult(Session, this, output);
    }

    public NotesNoteCollection CreateNoteCollection(bool selectDocuments = false)
    {
        EnsureOpen();
        return new NotesNoteCollection(Session, this) { SelectDocuments = selectDocuments };
    }

    protected override void ReleaseNative()
    {
        var handle = _handle;
        _handle = 0;
        if (handle != 0 && Session.Api.IsInitialized) Session.Api.CloseDatabase(handle);
    }

    private void EnsureOpen()
    {
        EnsureAlive();
        if (_handle == 0) throw new InvalidOperationException("NotesDatabase is not open.");
    }

    private static string NormalizeUnid(string value)
    {
        value = value.Replace("-", string.Empty, StringComparison.Ordinal).Replace(":", string.Empty, StringComparison.Ordinal).Trim();
        if (value.Length != 32 || value.Any(c => !Uri.IsHexDigit(c))) throw new ArgumentException("Notes UNID must contain exactly 32 hexadecimal characters.", nameof(value));
        return value.ToUpperInvariant();
    }
}

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
    public bool AutoUpdate { get; set; } = true;
    internal nint Handle { get { EnsureOpen(); return _handle; } }

    public NotesDocument? GetFirstDocument()
    {
        var ids = SnapshotIds(1);
        return ids.Count == 0 ? null : Parent.GetDocumentByNoteId(ids[0]);
    }

    public NotesDocument? GetNextDocument(NotesDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var ids = SnapshotIds();
        var index = Array.IndexOf(ids.ToArray(), document.NoteId);
        return index < 0 || index + 1 >= ids.Count ? null : Parent.GetDocumentByNoteId(ids[index + 1]);
    }

    public NotesDocument? GetFirstDocumentByKey(string key, bool exactMatch = true)
    {
        EnsureOpen();
        var ids = Session.Api.FindViewByTextKey(_handle, key ?? string.Empty, 1, exactMatch);
        return ids.Count == 0 ? null : Parent.GetDocumentByNoteId(ids[0]);
    }

    public NotesDocumentCollection GetAllDocumentsByKey(string key, bool exactMatch = true)
    {
        EnsureOpen();
        return new NotesDocumentCollection(Session, Parent, Session.Api.FindViewByTextKey(_handle, key ?? string.Empty, 0, exactMatch));
    }

    public NotesDocumentCollection FTSearch(string query, int maxResults = 0)
    {
        EnsureOpen();
        return new NotesDocumentCollection(Session, Parent, Session.Api.FullTextSearch(Parent.Handle, _handle, query, maxResults));
    }

    public NotesDocumentCollection FullTextSearch(string query, int maxResults = 0) => FTSearch(query, maxResults);

    public void Refresh()
    {
        EnsureOpen();
        Session.Api.UpdateCollection(_handle);
    }

    private IReadOnlyList<uint> SnapshotIds(int maximum = 0)
    {
        EnsureOpen();
        if (AutoUpdate) Session.Api.UpdateCollection(_handle);
        return Session.Api.ReadViewNoteIds(_handle, maximum);
    }

    protected override void ReleaseNative()
    {
        var handle = _handle;
        _handle = 0;
        if (handle != 0 && Session.Api.IsInitialized) Session.Api.CloseView(handle);
    }

    private void EnsureOpen()
    {
        EnsureAlive();
        if (_handle == 0) throw new InvalidOperationException("NotesView is not open.");
    }
}

public sealed class NotesDocumentCollection : NotesObject, IReadOnlyList<uint>
{
    private uint[] _noteIds;
    internal NotesDocumentCollection(NotesSession session, NotesDatabase parent, IEnumerable<uint> noteIds) : base(session)
    {
        Parent = parent;
        _noteIds = noteIds.Distinct().ToArray();
    }

    public NotesDatabase Parent { get; }
    public int Count { get { EnsureAlive(); return _noteIds.Length; } }
    public uint this[int index] { get { EnsureAlive(); return _noteIds[index]; } }
    public string? FirstNoteId { get { EnsureAlive(); return _noteIds.Length == 0 ? null : _noteIds[0].ToString("X8", CultureInfo.InvariantCulture); } }
    public string GetNoteIdString(int index) { EnsureAlive(); return _noteIds[index].ToString("X8", CultureInfo.InvariantCulture); }
    public string Get(int index) => GetNoteIdString(index);
    public NotesDocument? GetDocument(int index) { EnsureAlive(); return Parent.GetDocumentByNoteId(_noteIds[index]); }
    public NotesDocument? GetFirstDocument() => Count == 0 ? null : Parent.GetDocumentByNoteId(_noteIds[0]);
    internal IReadOnlyList<uint> NoteIds { get { EnsureAlive(); return _noteIds; } }
    public IEnumerator<uint> GetEnumerator() { EnsureAlive(); return ((IEnumerable<uint>)_noteIds).GetEnumerator(); }
    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    protected override void ReleaseNative() => _noteIds = [];
}

public sealed class NotesNoteCollection : NotesObject, IReadOnlyList<uint>
{
    private uint[] _noteIds = [];
    internal NotesNoteCollection(NotesSession session, NotesDatabase parent) : base(session) => Parent = parent;

    public NotesDatabase Parent { get; }
    public bool SelectDocuments { get; set; }
    public bool SelectForms { get; set; }
    public bool SelectViews { get; set; }
    public bool SelectAgents { get; set; }
    public int Count { get { EnsureAlive(); return _noteIds.Length; } }
    public uint this[int index] { get { EnsureAlive(); return _noteIds[index]; } }
    internal IReadOnlyList<uint> NoteIds { get { EnsureAlive(); return _noteIds; } }

    public void BuildCollection()
    {
        EnsureAlive();
        var ids = new HashSet<uint>();
        if (SelectDocuments) foreach (var id in Session.Api.Search(Parent.Handle, "@All", 0)) ids.Add(id);
        if (SelectForms || SelectViews || SelectAgents)
        {
            var mask = (ushort)0;
            if (SelectForms) mask |= 0x0004;
            if (SelectViews) mask |= 0x0008;
            if (SelectAgents) mask |= 0x0200;
            foreach (var id in Session.Api.SearchByNoteClass(Parent.Handle, mask)) ids.Add(id);
        }
        _noteIds = ids.ToArray();
    }

    public uint GetFirstNoteID() { EnsureAlive(); return _noteIds.Length == 0 ? 0 : _noteIds[0]; }
    public uint GetNextNoteID(uint noteId) { EnsureAlive(); var i = Array.IndexOf(_noteIds, noteId); return i < 0 || i + 1 >= _noteIds.Length ? 0 : _noteIds[i + 1]; }
    public IEnumerator<uint> GetEnumerator() { EnsureAlive(); return ((IEnumerable<uint>)_noteIds).GetEnumerator(); }
    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    protected override void ReleaseNative() => _noteIds = [];
}

public sealed class NotesDocument : NotesObject
{
    private nint _handle;

    internal NotesDocument(NotesSession session, NotesDatabase parent, nint handle, uint? knownNoteId, string? knownUnid) : base(session)
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
            try { Session.Api.CloseNote(handle); } finally { Session.Unregister(this); }
            throw;
        }
    }

    public NotesDatabase ParentDatabase { get; }
    public NotesDatabase Parent => ParentDatabase;
    public uint NoteId { get; private set; }
    public string NoteIdHex => NoteId.ToString("X8", CultureInfo.InvariantCulture);
    public string UniversalId { get; }
    public IReadOnlyList<string> Items { get { EnsureAlive(); return Session.Api.GetAllItemNames(Handle); } }
    public long Size { get { EnsureAlive(); return Session.Api.GetDocumentSize(Handle); } }
    public NotesDateTime Created { get { EnsureAlive(); return NotesDateTime.FromNative(Session, Session.Api.GetDocumentCreated(Handle)); } }
    public NotesDateTime LastModified { get { EnsureAlive(); return NotesDateTime.FromNative(Session, Session.Api.GetDocumentLastModified(Handle)); } }
    public uint ParentDocumentUNIDNoteId { get { EnsureAlive(); return Session.Api.GetParentNoteId(Handle); } }
    public NotesDocumentCollection Responses { get { EnsureAlive(); return new NotesDocumentCollection(Session, ParentDatabase, Session.Api.GetResponseIds(Handle)); } }
    internal nint Handle { get { EnsureAlive(); if (_handle == 0) throw new InvalidOperationException("NotesDocument is not open."); return _handle; } }

    public NotesItem? GetFirstItem(string itemName)
    {
        EnsureAlive();
        if (string.IsNullOrWhiteSpace(itemName) || !Session.Api.HasItem(Handle, itemName)) return null;
        var metadata = Session.Api.GetItemMetadata(Handle, itemName);
        return metadata.DataType == NotesNativeApi.NotesTypeComposite
            ? new NotesRichTextItem(Session, this, metadata.Name)
            : new NotesItem(Session, this, metadata.Name);
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
        SetValue(itemName, value);
        return GetFirstItem(itemName) ?? new NotesItem(Session, this, itemName);
    }

    public bool HasItem(string itemName) { EnsureAlive(); ArgumentException.ThrowIfNullOrWhiteSpace(itemName); return Session.Api.HasItem(Handle, itemName); }
    public object? GetValue(string itemName) { EnsureAlive(); ArgumentException.ThrowIfNullOrWhiteSpace(itemName); return Session.Api.GetItemValue(Handle, itemName); }
    public object?[] GetItemValue(string itemName) { EnsureAlive(); ArgumentException.ThrowIfNullOrWhiteSpace(itemName); return Session.Api.GetItemValues(Handle, itemName, td => NotesDateTime.FromNative(Session, td)); }
    public string GetString(string itemName) { EnsureAlive(); ArgumentException.ThrowIfNullOrWhiteSpace(itemName); return Session.Api.GetItemText(Handle, itemName); }
    public double GetNumber(string itemName) { EnsureAlive(); ArgumentException.ThrowIfNullOrWhiteSpace(itemName); return Session.Api.GetItemNumber(Handle, itemName); }
    public NotesDateTime GetDateTime(string itemName) { EnsureAlive(); ArgumentException.ThrowIfNullOrWhiteSpace(itemName); return NotesDateTime.FromNative(Session, Session.Api.GetItemTime(Handle, itemName)); }

    public void SetValue(string itemName, object? value) { EnsureAlive(); ArgumentException.ThrowIfNullOrWhiteSpace(itemName); Session.Api.SetItemValue(Handle, itemName, value); }
    public void SetString(string itemName, string value) { EnsureAlive(); ArgumentException.ThrowIfNullOrWhiteSpace(itemName); Session.Api.SetItemText(Handle, itemName, value ?? string.Empty); }
    public void SetNumber(string itemName, double value) { EnsureAlive(); ArgumentException.ThrowIfNullOrWhiteSpace(itemName); Session.Api.SetItemNumber(Handle, itemName, value); }
    public void SetDateTime(string itemName, NotesDateTime value) { EnsureAlive(); ArgumentException.ThrowIfNullOrWhiteSpace(itemName); ArgumentNullException.ThrowIfNull(value); Session.Api.SetItemTime(Handle, itemName, value.NativeValue); }
    public void RemoveItem(string itemName) { EnsureAlive(); ArgumentException.ThrowIfNullOrWhiteSpace(itemName); Session.Api.DeleteItem(Handle, itemName); }
    public void MarkRead(string? userName = null) => Session.Api.SetDocumentUnread(ParentDatabase.Handle, NoteId, userName ?? Session.Username, false);
    public void MarkUnread(string? userName = null) => Session.Api.SetDocumentUnread(ParentDatabase.Handle, NoteId, userName ?? Session.Username, true);
    public void PutInFolder(string folderName) { EnsureAlive(); ArgumentException.ThrowIfNullOrWhiteSpace(folderName); Session.Api.PutDocumentInFolder(ParentDatabase.Handle, NoteId, folderName); }
    public void RemoveFromFolder(string folderName) { EnsureAlive(); ArgumentException.ThrowIfNullOrWhiteSpace(folderName); Session.Api.RemoveDocumentFromFolder(ParentDatabase.Handle, NoteId, folderName); }

    public void Save()
    {
        EnsureAlive();
        Session.Api.SaveNote(Handle);
        NoteId = Session.Api.GetNoteId(Handle);
    }

    protected override void ReleaseNative()
    {
        var handle = _handle;
        _handle = 0;
        if (handle != 0 && Session.Api.IsInitialized) Session.Api.CloseNote(handle);
    }
}

public class NotesItem : NotesObject
{
    internal NotesItem(NotesSession session, NotesDocument parent, string name) : base(session) { Parent = parent; Name = name; }
    public NotesDocument Parent { get; }
    public string Name { get; }
    private NotesItemMetadata Metadata { get { EnsureAlive(); return Session.Api.GetItemMetadata(Parent.Handle, Name); } }
    public bool IsAuthors => (Metadata.Flags & NotesNativeApi.NotesItemReadWriters) != 0;
    public bool IsEncrypted => (Metadata.Flags & NotesNativeApi.NotesItemSeal) != 0;
    public bool IsNames => (Metadata.Flags & NotesNativeApi.NotesItemNames) != 0;
    public bool IsProtected => (Metadata.Flags & NotesNativeApi.NotesItemProtected) != 0;
    public bool IsReaders => (Metadata.Flags & NotesNativeApi.NotesItemReaders) != 0;
    public bool IsSigned => (Metadata.Flags & NotesNativeApi.NotesItemSign) != 0;
    public bool IsSummary => (Metadata.Flags & NotesNativeApi.NotesItemSummary) != 0;
    public string Text { get { EnsureAlive(); return Session.Api.GetItemText(Parent.Handle, Name); } }
    public int Type => Metadata.DataType;
    public long ValueLength => Metadata.ValueLength;
    public NotesDateTime LastModified => NotesDateTime.FromNative(Session, Session.Api.GetItemModifiedTime(Parent.Handle, Name));
    public object?[] Values { get { EnsureAlive(); return Parent.GetItemValue(Name); } set { EnsureAlive(); Parent.SetValue(Name, value); } }
    protected override void ReleaseNative() { }
}

public sealed class NotesRichTextItem : NotesItem
{
    internal NotesRichTextItem(NotesSession session, NotesDocument parent, string name) : base(session, parent, name) { }
}

public sealed class NotesName : NotesObject
{
    private readonly Dictionary<string, string> _parts = new(StringComparer.OrdinalIgnoreCase);
    internal NotesName(NotesSession session, string value) : base(session)
    {
        Source = value.Trim();
        Canonical = session.Api.CanonicalizeName(Source);
        Abbreviated = session.Api.AbbreviateName(Canonical);
        ParseCanonical(Canonical);
        ParseInternet(Source);
    }
    public NotesSession Parent => Session;
    public string Original => Source;
    public string Source { get; }
    public string Canonical { get; }
    public string Abbreviated { get; }
    public bool IsHierarchical => Canonical.Contains('=');
    public string Common => Part("CN");
    public string Country => Part("C");
    public string Organization => Part("O");
    public string OrgUnit1 => Part("OU1");
    public string OrgUnit2 => Part("OU2");
    public string OrgUnit3 => Part("OU3");
    public string OrgUnit4 => Part("OU4");
    public string ADMD => Part("A");
    public string PRMD => Part("P");
    public string Addr821 => Part("ADDR821");
    public string Addr822LocalPart => Part("LOCALPART");
    public string Addr822Phrase => Part("PHRASE");
    private string Part(string key) { EnsureAlive(); return _parts.TryGetValue(key, out var value) ? value : string.Empty; }
    private void ParseCanonical(string value)
    {
        var ou = 0;
        foreach (var raw in value.Split('/'))
        {
            var part = raw.Trim(); var separator = part.IndexOf('='); if (separator <= 0) continue;
            var key = part[..separator].Trim().ToUpperInvariant(); var text = part[(separator + 1)..].Trim();
            if (key == "OU") { ou++; if (ou <= 4) _parts["OU" + ou.ToString(CultureInfo.InvariantCulture)] = text; } else _parts[key] = text;
        }
    }
    private void ParseInternet(string source)
    {
        var at = source.LastIndexOf('@'); if (at <= 0 || at >= source.Length - 1) return;
        _parts["ADDR821"] = source; var before = source[..at].Trim(); var lt = before.LastIndexOf('<'); var gt = before.LastIndexOf('>');
        if (lt >= 0 && gt > lt) { _parts["PHRASE"] = before[..lt].Trim().Trim('"'); _parts["LOCALPART"] = before[(lt + 1)..gt].Split('@')[0]; } else _parts["LOCALPART"] = before;
    }
    protected override void ReleaseNative() => _parts.Clear();
}

public sealed class NotesDateTime : NotesObject
{
    private NotesTimeDate _value;
    internal NotesDateTime(NotesSession session, string value) : base(session)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("NotesDateTime requires a date/time value.", nameof(value));
        _value = session.Api.ParseTimeDate(value);
    }
    internal NotesDateTime(NotesSession session, DateTimeOffset value) : base(session) => _value = session.Api.ParseTimeDate(value.LocalDateTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
    private NotesDateTime(NotesSession session, NotesTimeDate value) : base(session) => _value = value;
    internal static NotesDateTime FromNative(NotesSession session, NotesTimeDate value) => new(session, value);
    internal NotesTimeDate NativeValue { get { EnsureAlive(); return _value; } }
    public NotesSession Parent => Session;
    public bool IsValidDate { get { EnsureAlive(); return true; } }
    public bool IsDST { get { EnsureAlive(); return Session.Api.ExpandTimeDate(_value).Dst != 0; } }
    public int TimeZone { get { EnsureAlive(); return Session.Api.ExpandTimeDate(_value).Zone; } }
    public string Text => LocalTime;
    public string LocalTime { get { EnsureAlive(); return Session.Api.FormatTimeDate(_value); } }
    public string GMTTime { get { var v = Session.Api.ExpandTimeDateGmt(_value); return FormatExpanded(v); } }
    public string ZoneTime { get { var v = Session.Api.ExpandTimeDate(_value); return FormatExpanded(v); } }
    public string DateOnly { get { var v = Session.Api.ExpandTimeDate(_value); return $"{v.Year:D4}-{v.Month:D2}-{v.Day:D2}"; } }
    public string TimeOnly { get { var v = Session.Api.ExpandTimeDate(_value); return $"{v.Hour:D2}:{v.Minute:D2}:{v.Second:D2}"; } }
    public DateTimeOffset? Value { get { var v = Session.Api.ExpandTimeDate(_value); try { return new DateTimeOffset(v.Year, v.Month, v.Day, v.Hour, v.Minute, v.Second, TimeSpan.Zero); } catch { return null; } } }
    public void AdjustSecond(int amount) => Adjust(amount, 0, 0, 0, 0, 0);
    public void AdjustMinute(int amount) => Adjust(0, amount, 0, 0, 0, 0);
    public void AdjustHour(int amount) => Adjust(0, 0, amount, 0, 0, 0);
    public void AdjustDay(int amount) => Adjust(0, 0, 0, amount, 0, 0);
    public void AdjustMonth(int amount) => Adjust(0, 0, 0, 0, amount, 0);
    public void AdjustYear(int amount) => Adjust(0, 0, 0, 0, 0, amount);
    private void Adjust(int seconds, int minutes, int hours, int days, int months, int years) { EnsureAlive(); Session.Api.AdjustTimeDate(ref _value, seconds, minutes, hours, days, months, years); }
    private static string FormatExpanded(NotesExpandedTime v) => $"{v.Year:D4}-{v.Month:D2}-{v.Day:D2} {v.Hour:D2}:{v.Minute:D2}:{v.Second:D2}";
    protected override void ReleaseNative() => _value = default;
}

public sealed class NotesAgent : NotesObject
{
    private readonly uint _noteId;
    internal NotesAgent(NotesSession session, NotesDatabase parent, uint noteId, string name) : base(session) { Parent = parent; _noteId = noteId; Name = name; }
    public NotesDatabase Parent { get; }
    public string Name { get; }
    public string Owner => ReadText("$UpdatedBy");
    public string CommonOwner => Owner;
    public string Comment => ReadText("$Comment");
    public string Query => ReadText("$AssistQuery");
    public string ServerName => Parent.Server;
    public bool IsNotesAgent => true;
    public bool IsPublic => true;
    public bool IsWebAgent => false;
    public bool IsActivatable => true;
    public string NotesURL => $"notes://{Parent.Server}/{Parent.FilePath}/0/{_noteId:X8}";
    public NotesAgentResult Run(NotesDocument? document = null) { EnsureAlive(); return new NotesAgentResult(Session, Parent, Session.Api.RunAgent(Parent.Handle, Name, document?.Handle ?? 0)); }
    public NotesAgentResult RunOnServer(NotesDocument? document = null) => Run(document);
    private string ReadText(string itemName) { using var doc = Parent.GetDocumentByNoteId(_noteId); if (doc is null) return string.Empty; try { return doc.GetString(itemName); } catch { return string.Empty; } }
    protected override void ReleaseNative() { }
}

public sealed class NotesAgentResult : NotesObject
{
    internal NotesAgentResult(NotesSession session, NotesDatabase database, string output) : base(session) { Database = database; Output = output; }
    public NotesDatabase Database { get; }
    public bool Success => true;
    public int Status => 0;
    public string Output { get; }
    protected override void ReleaseNative() { }
}

public sealed class NotesStream : NotesObject
{
    private MemoryStream _memory = new();
    private FileStream? _file;
    private string _charset = "Unicode";
    private bool _readOnly;
    internal NotesStream(NotesSession session) : base(session) { }
    private Stream Stream => (Stream?)_file ?? _memory;
    private Encoding Encoding => ResolveEncoding(_charset);
    public NotesSession Parent => Session;
    public int Bytes { get { EnsureAlive(); return checked((int)Math.Min(Stream.Length, int.MaxValue)); } }
    public long Length { get { EnsureAlive(); return Stream.Length; } }
    public string Charset { get { EnsureAlive(); return _charset; } set { EnsureAlive(); _charset = string.IsNullOrWhiteSpace(value) ? "Unicode" : value; } }
    public bool IsEOS { get { EnsureAlive(); return Stream.Position >= Stream.Length; } }
    public bool IsReadOnly { get { EnsureAlive(); return _readOnly; } }
    public long Position { get { EnsureAlive(); return Stream.Position; } set { EnsureAlive(); if (value < 0) throw new ArgumentOutOfRangeException(nameof(value)); Stream.Position = Math.Min(value, Stream.Length); } }
    public bool Open(string filePath, string mode = "binary")
    {
        EnsureAlive(); Close(); if (string.IsNullOrWhiteSpace(filePath)) return false;
        _readOnly = mode.Equals("readonly", StringComparison.OrdinalIgnoreCase) || mode.Equals("read", StringComparison.OrdinalIgnoreCase);
        try
        {
            var fullPath = Path.GetFullPath(filePath); if (!_readOnly) { var directory = Path.GetDirectoryName(fullPath); if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory); }
            _file = new FileStream(fullPath, _readOnly ? FileMode.Open : FileMode.OpenOrCreate, _readOnly ? FileAccess.Read : FileAccess.ReadWrite, FileShare.Read); _file.Position = 0; return true;
        }
        catch { _file = null; _readOnly = false; return false; }
    }
    public void Close() { EnsureAlive(); _file?.Dispose(); _file = null; _readOnly = false; }
    public byte[] Read() { EnsureAlive(); var remaining = checked((int)Math.Min(Stream.Length - Stream.Position, int.MaxValue)); return Read(remaining); }
    public byte[] Read(int length) { EnsureAlive(); if (length < 0) throw new ArgumentOutOfRangeException(nameof(length)); var bytes = new byte[Math.Min(length, checked((int)Math.Min(Stream.Length - Stream.Position, int.MaxValue)))]; var read = Stream.Read(bytes, 0, bytes.Length); if (read != bytes.Length) Array.Resize(ref bytes, read); return bytes; }
    public string ReadText() => Encoding.GetString(Read());
    public string ReadText(int length) => Encoding.GetString(Read(length));
    public void Write(ReadOnlySpan<byte> data) { EnsureAlive(); if (_readOnly) throw new InvalidOperationException("NotesStream is read-only."); Stream.Write(data); Stream.Flush(); }
    public void Write(byte[] data) => Write(data.AsSpan());
    public void WriteText(string text) => Write(Encoding.GetBytes(text ?? string.Empty));
    public byte[] ToArray() { EnsureAlive(); if (_file is null) return _memory.ToArray(); var pos = _file.Position; _file.Position = 0; using var copy = new MemoryStream(); _file.CopyTo(copy); _file.Position = pos; return copy.ToArray(); }
    public void Truncate() { EnsureAlive(); if (_readOnly) throw new InvalidOperationException("NotesStream is read-only."); Stream.SetLength(Stream.Position); Stream.Flush(); }
    internal Stream AsReadableStream() { EnsureAlive(); var bytes = ToArray(); return new MemoryStream(bytes, writable: false); }
    internal void Replace(byte[] data) { EnsureAlive(); if (_readOnly) throw new InvalidOperationException("NotesStream is read-only."); if (_file is not null) { _file.SetLength(0); _file.Position = 0; _file.Write(data); _file.Position = 0; } else { _memory.Dispose(); _memory = new MemoryStream(data.Length); _memory.Write(data); _memory.Position = 0; } }
    protected override void ReleaseNative() { _file?.Dispose(); _file = null; _memory.Dispose(); _memory = new MemoryStream(); }
    private static Encoding ResolveEncoding(string charset) { var name = charset.Trim(); if (name.Length == 0 || name.Equals("Unicode", StringComparison.OrdinalIgnoreCase)) return Encoding.Unicode; if (name.Equals("UTF-8", StringComparison.OrdinalIgnoreCase) || name.Equals("UTF8", StringComparison.OrdinalIgnoreCase)) return new UTF8Encoding(false); try { return Encoding.GetEncoding(name); } catch { throw new ArgumentException("Unsupported NotesStream charset: " + charset, nameof(charset)); } }
}

public sealed class NotesDXLImporter : NotesObject
{
    private uint _handle;
    internal NotesDXLImporter(NotesSession session) : base(session) => _handle = Session.Api.CreateDxlImporter();
    public int ACLImportOption { get => Session.Api.GetDxlImporterWord(_handle, 1); set => Session.Api.SetDxlImporterWord(_handle, 1, value); }
    public int DesignImportOption { get => Session.Api.GetDxlImporterWord(_handle, 2); set => Session.Api.SetDxlImporterWord(_handle, 2, value); }
    public int DocumentImportOption { get => Session.Api.GetDxlImporterWord(_handle, 3); set => Session.Api.SetDxlImporterWord(_handle, 3, value); }
    public bool CreateFullTextIndex { get => Session.Api.GetDxlImporterBool(_handle, 4); set => Session.Api.SetDxlImporterBool(_handle, 4, value); }
    public bool ReplaceDbProperties { get => Session.Api.GetDxlImporterBool(_handle, 5); set => Session.Api.SetDxlImporterBool(_handle, 5, value); }
    public int InputValidationOption { get => Session.Api.GetDxlImporterWord(_handle, 6); set => Session.Api.SetDxlImporterWord(_handle, 6, value); }
    public bool ReplicaRequired { get => Session.Api.GetDxlImporterBool(_handle, 7); set => Session.Api.SetDxlImporterBool(_handle, 7, value); }
    public bool ExitOnFirstFatalError { get => Session.Api.GetDxlImporterBool(_handle, 8); set => Session.Api.SetDxlImporterBool(_handle, 8, value); }
    public int UnknownTokenLogOption { get => Session.Api.GetDxlImporterWord(_handle, 9); set => Session.Api.SetDxlImporterWord(_handle, 9, value); }
    public string Log => Session.Api.GetDxlImporterLog(_handle);
    public int ImportedNoteCount => Session.Api.GetDxlImporterNoteCount(_handle);
    public void Import(NotesStream stream, NotesDatabase database) { EnsureAlive(); ArgumentNullException.ThrowIfNull(stream); ArgumentNullException.ThrowIfNull(database); Session.Api.ImportDxl(_handle, stream.AsReadableStream(), database.Handle); }
    protected override void ReleaseNative() { var h = _handle; _handle = 0; if (h != 0 && Session.Api.IsInitialized) Session.Api.DeleteDxlImporter(h); }
}

public sealed class NotesDXLExporter : NotesObject
{
    private uint _handle;
    internal NotesDXLExporter(NotesSession session) : base(session) => _handle = Session.Api.CreateDxlExporter();
    public int RichTextOption { get => Session.Api.GetDxlExporterInt(_handle, 6); set => Session.Api.SetDxlExporterInt(_handle, 6, value); }
    public int ValidationStyle { get => Session.Api.GetDxlExporterInt(_handle, 8); set => Session.Api.SetDxlExporterInt(_handle, 8, value); }
    public int MIMEOption { get => Session.Api.GetDxlExporterInt(_handle, 11); set => Session.Api.SetDxlExporterInt(_handle, 11, value); }
    public bool ForceNoteFormat { get => Session.Api.GetDxlExporterBool(_handle, 30); set => Session.Api.SetDxlExporterBool(_handle, 30, value); }
    public bool ExitOnFirstFatalError { get => Session.Api.GetDxlExporterBool(_handle, 31); set => Session.Api.SetDxlExporterBool(_handle, 31, value); }
    public bool OutputDOCTYPE { get => Session.Api.GetDxlExporterBool(_handle, 34); set => Session.Api.SetDxlExporterBool(_handle, 34, value); }
    public bool ConvertNotesBitmapsToGIF { get => Session.Api.GetDxlExporterBool(_handle, 35); set => Session.Api.SetDxlExporterBool(_handle, 35, value); }
    public bool OmitRichTextAttachments { get => Session.Api.GetDxlExporterBool(_handle, 36); set => Session.Api.SetDxlExporterBool(_handle, 36, value); }
    public bool OmitOLEObjects { get => Session.Api.GetDxlExporterBool(_handle, 37); set => Session.Api.SetDxlExporterBool(_handle, 37, value); }
    public bool OmitMiscFileObjects { get => Session.Api.GetDxlExporterBool(_handle, 38); set => Session.Api.SetDxlExporterBool(_handle, 38, value); }
    public bool OmitPictures { get => Session.Api.GetDxlExporterBool(_handle, 39); set => Session.Api.SetDxlExporterBool(_handle, 39, value); }
    public NotesStream Export(NotesDocument document) { EnsureAlive(); ArgumentNullException.ThrowIfNull(document); var stream = new NotesStream(Session); stream.Replace(Session.Api.ExportDxlDocument(_handle, document.Handle)); return stream; }
    public NotesStream Export(NotesDocumentCollection documents) { EnsureAlive(); ArgumentNullException.ThrowIfNull(documents); var stream = new NotesStream(Session); stream.Replace(Session.Api.ExportDxlCollection(_handle, documents.Parent.Handle, documents.NoteIds)); return stream; }
    public NotesStream Export(NotesNoteCollection notes) { EnsureAlive(); ArgumentNullException.ThrowIfNull(notes); var stream = new NotesStream(Session); stream.Replace(Session.Api.ExportDxlCollection(_handle, notes.Parent.Handle, notes.NoteIds)); return stream; }
    protected override void ReleaseNative() { var h = _handle; _handle = 0; if (h != 0 && Session.Api.IsInitialized) Session.Api.DeleteDxlExporter(h); }
}
