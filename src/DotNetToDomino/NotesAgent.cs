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
    public string NotesURL => $"notes://{Parent.Server}/{Parent.FilePath}/0/{_noteId:X8}";

    public NotesAgentResult Run(NotesDocument? document = null)
    {
        EnsureAlive();
        return Parent.Own(new NotesAgentResult(Session, Parent, Session.Api.RunAgentByNoteId(Parent.Handle, _noteId, document?.Handle ?? 0)));
    }

    public NotesAgentResult Run(uint noteId)
    {
        EnsureAlive();
        using var document = Parent.GetDocumentByID(noteId) ?? throw new NotesException($"NotesAgent.Run document {noteId:X8} was not found.");
        return Run(document);
    }

    public NotesAgentResult Run(string noteId) => Run(NotesDatabase.ParseNoteId(noteId));

    public NotesAgentResult RunWithDocumentContext(NotesDocument document)
    {
        EnsureAlive();
        ArgumentNullException.ThrowIfNull(document);
        return Run(document);
    }

    public NotesAgentResult RunOnServer(NotesDocument? document = null) => Run(document);
    public NotesAgentResult RunOnServer(uint noteId) => Run(noteId);
    public NotesAgentResult RunOnServer(string noteId) => Run(noteId);

    public void Save()
    {
        EnsureAlive();
        Session.Api.SaveAgent(Parent.Handle, _noteId);
    }

    public void Remove()
    {
        EnsureAlive();
        if (!Session.Api.DeleteNote(Parent.Handle, _noteId, false))
            throw new NotesException(445, "Unable to remove NotesAgent.");
    }

    private string ReadText(string itemName)
    {
        EnsureAlive();
        return Session.Api.GetDesignNoteText(Parent.Handle, _noteId, itemName) ?? string.Empty;
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
    public string Output { get; }
    protected override void ReleaseNative() => Database.UnregisterChild(this);
}
