namespace DotNetToDomino;

public sealed class NotesAgent : NotesObject
{
    private readonly uint _noteId;
    internal NotesAgent(NotesSession session, NotesDatabase parent, uint noteId, string name) : base(session)
    {
        Parent = parent;
        _noteId = noteId;
        Name = name;
    }
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
    public NotesAgentResult Run(NotesDocument? document = null)
    {
        EnsureAlive();
        return Parent.Own(new NotesAgentResult(Session, Parent, Session.Api.RunAgent(Parent.Handle, Name, document?.Handle ?? 0)));
    }
    public NotesAgentResult RunOnServer(NotesDocument? document = null) => Run(document);
    private string ReadText(string itemName)
    {
        using var doc = Parent.GetDocumentByID(_noteId);
        if (doc is null) return string.Empty;
        try { return doc.GetString(itemName); } catch { return string.Empty; }
    }
    protected override void ReleaseNative() => Parent.UnregisterChild(this);
}

public sealed class NotesAgentResult : NotesObject
{
    internal NotesAgentResult(NotesSession session, NotesDatabase database, string output) : base(session)
    {
        Database = database;
        Output = output;
    }
    public NotesDatabase Database { get; }
    public bool Success => true;
    public int Status => 0;
    public string Output { get; }
    protected override void ReleaseNative() => Database.UnregisterChild(this);
}
