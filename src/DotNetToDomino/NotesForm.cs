using System.Globalization;

namespace DotNetToDomino;

/// <summary>Represents a Notes form design element.</summary>
public sealed class NotesForm : NotesObject
{
    private readonly uint _noteId;
    private readonly NotesDatabase _parent;

    internal NotesForm(NotesSession session, NotesDatabase parent, uint noteId, string name) : base(session)
    {
        _parent = parent;
        _noteId = noteId;
        Name = name;
    }

    public NotesDatabase Parent { get { EnsureAlive(); return _parent; } }
    public string Name { get; }
    public string NoteID { get { EnsureAlive(); return _noteId.ToString("X", CultureInfo.InvariantCulture); } }
    public string NotesURL { get { EnsureAlive(); return $"notes://{Parent.Server}/{Parent.FilePath}/0/{_noteId:X8}"; } }

    protected override void ReleaseNative() => _parent.UnregisterChild(this);
}
