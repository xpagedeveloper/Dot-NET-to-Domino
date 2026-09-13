namespace DotNetToDomino;

public sealed class NotesAgent : NotesObject
{
    private readonly uint _noteId;
    private string _returnMessage = string.Empty;
    private string _parameterDocId = string.Empty;
    private bool? _pendingEnabled;
    private string? _pendingServerName;

    internal NotesAgent(NotesSession session, NotesDatabase parent, uint noteId, string name) : base(session)
    {
        Parent = parent;
        _noteId = noteId;
        Name = name;
    }

    public NotesDatabase Parent { get; }
    public string Name { get; }
    public string Owner => ReadText("$UpdatedBy");
    public string CommonOwner
    {
        get
        {
            EnsureAlive();
            var owner = Owner;
            if (owner.Length == 0) return string.Empty;
            using var parsed = Session.CreateName(owner);
            return parsed.Common.Length == 0 ? owner : parsed.Common;
        }
    }
    public string Comment => ReadText("$Comment");
    public string Query => ReadText("$AssistQuery");
    public string ServerName
    {
        get { EnsureAlive(); return _pendingServerName ?? ReadText("$MachineName"); }
        set { EnsureAlive(); _pendingServerName = value?.Trim() ?? string.Empty; }
    }
    public string ParameterDocID { get { EnsureAlive(); return _parameterDocId; } }
    public bool IsNotesAgent { get { EnsureAlive(); return true; } }
    public bool IsPublic { get { EnsureAlive(); return !ReadText("$AssistFlags").Contains('P'); } }
    public bool HasRunSinceModified { get { EnsureAlive(); return Session.Api.GetAgentHasRunSinceModified(Parent.Handle, _noteId); } }
    public bool IsEnabled
    {
        get { EnsureAlive(); return _pendingEnabled ?? Session.Api.GetAgentEnabled(Parent.Handle, _noteId); }
        set { EnsureAlive(); _pendingEnabled = value; }
    }
    public int Trigger
    {
        get
        {
            EnsureAlive();
            var text = ReadText("$AssistTrigger").Trim();
            return text.Length == 0 ? 0 : int.TryParse(text, out var value) ? value : 0;
        }
    }
    public string NotesURL => $"notes://{Parent.Server}/{Parent.FilePath}/0/{_noteId:X8}";
    public string OnBehalfOf => ReadText("$OnBehalfOf");
    public string ReturnMessage { get { EnsureAlive(); return _returnMessage; } }

    public NotesAgentResult Run()
    {
        EnsureAlive();
        _parameterDocId = string.Empty;
        return Execute(0, 0);
    }

    /// <summary>Runs the agent with a Notes parameter document NoteID.</summary>
    public NotesAgentResult Run(uint noteId)
    {
        EnsureAlive();
        ValidateParameterNoteId(noteId);
        _parameterDocId = noteId.ToString("X", System.Globalization.CultureInfo.InvariantCulture);
        return Execute(0, noteId);
    }

    public NotesAgentResult Run(string noteId) => Run(ParseParameterNoteId(noteId));

    public NotesAgentResult RunWithDocumentContext(NotesDocument document)
    {
        EnsureAlive();
        ArgumentNullException.ThrowIfNull(document);
        _parameterDocId = string.Empty;
        return Execute(document.Handle, 0);
    }

    public NotesAgentResult RunWithDocumentContext(NotesDocument document, uint noteId)
    {
        EnsureAlive();
        ArgumentNullException.ThrowIfNull(document);
        ValidateParameterNoteId(noteId);
        _parameterDocId = noteId.ToString("X", System.Globalization.CultureInfo.InvariantCulture);
        return Execute(document.Handle, noteId);
    }

    public NotesAgentResult RunWithDocumentContext(NotesDocument document, string noteId) =>
        RunWithDocumentContext(document, ParseParameterNoteId(noteId));

    public NotesAgentResult RunOnServer() => Run();
    public NotesAgentResult RunOnServer(uint noteId) => Run(noteId);
    public NotesAgentResult RunOnServer(string noteId) => Run(noteId);

    public void Save()
    {
        EnsureAlive();
        if (_pendingEnabled is not null || _pendingServerName is not null)
        {
            Session.Api.SaveAgentState(Parent.Handle, _noteId, _pendingEnabled, _pendingServerName);
            _pendingEnabled = null;
            _pendingServerName = null;
            return;
        }
        Session.Api.SaveAgent(Parent.Handle, _noteId);
    }

    public void Remove()
    {
        EnsureAlive();
        if (!Session.Api.DeleteNote(Parent.Handle, _noteId, false))
            throw new NotesException(445, "Unable to remove NotesAgent.");
    }

    private NotesAgentResult Execute(nint documentContext, uint parameterNoteId)
    {
        _returnMessage = Session.Api.RunAgentByNoteId(Parent.Handle, _noteId, documentContext, parameterNoteId);
        return Parent.Own(new NotesAgentResult(Session, Parent, _returnMessage));
    }

    private static void ValidateParameterNoteId(uint noteId)
    {
        if (noteId == 0) throw new ArgumentOutOfRangeException(nameof(noteId), "NotesAgent parameter NoteID must be non-zero.");
    }

    private static uint ParseParameterNoteId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var text = value.Trim();
        if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) text = text[2..];
        if (!uint.TryParse(text, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out var noteId) || noteId == 0)
            throw new ArgumentException("NotesAgent parameter NoteID must be a non-zero hexadecimal Note ID.", nameof(value));
        return noteId;
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
        Output = NotesAgentOutput.Normalize(output);
    }
    public NotesDatabase Database { get; }
    public string Output { get; }
    protected override void ReleaseNative() => Database.UnregisterChild(this);
}

internal static class NotesAgentOutput
{
    private static readonly System.Text.RegularExpressions.Regex Prefix = new(
        @"\[[^\]\r\n]+\]\s+[^\r\n]*?\bAgent printing:\s*",
        System.Text.RegularExpressions.RegexOptions.Compiled);

    internal static string Normalize(string? output) => Prefix.Replace(output ?? string.Empty, string.Empty);
}
