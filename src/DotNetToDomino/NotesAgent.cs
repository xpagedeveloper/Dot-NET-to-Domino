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
    public string ParameterDocID => string.Empty;
    public bool IsNotesAgent => true;
    public bool IsPublic => true;
    public bool IsWebAgent => false;
    public bool IsActivatable => true;
    public bool HasRunSinceModified => false;
    public bool ProhibitDesignUpdate { get { EnsureAlive(); return false; } set { EnsureAlive(); } }
    public bool IsEnabled { get { EnsureAlive(); return true; } set { EnsureAlive(); } }
    public int Trigger { get { EnsureAlive(); return 0; } }
    public int Target { get { EnsureAlive(); return 0; } }
    public string NotesURL => $"notes://{Parent.Server}/{Parent.FilePath}/0/{_noteId:X8}";
    public string HttpURL { get { EnsureAlive(); return string.Empty; } }
    public string OnBehalfOf { get { EnsureAlive(); return string.Empty; } }

    public NotesAgentResult Run(NotesDocument? document = null)
    {
        EnsureAlive();
        return Parent.Own(new NotesAgentResult(Session, Parent, Session.Api.RunAgent(Parent.Handle, Name, document?.Handle ?? 0)));
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

    public NotesAgentResult RunWithDocumentContext(NotesDocument document, uint noteId) => RunWithDocumentContext(document);
    public NotesAgentResult RunWithDocumentContext(NotesDocument document, string noteId) => RunWithDocumentContext(document);

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

    public void UnLock() => EnsureAlive();

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
    public bool Success => true;
    public int Status => 0;
    public string Output { get; }
    protected override void ReleaseNative() => Database.UnregisterChild(this);
}
