namespace DotNetToDomino;

/// <summary>Describes the effective Domino ACL access for a user.</summary>
public sealed class NotesDatabaseAccess : NotesObject
{
    private readonly NotesDatabase _database;
    private readonly Interop.NotesDatabaseAccessData _data;
    internal NotesDatabaseAccess(NotesSession session, NotesDatabase database, Interop.NotesDatabaseAccessData data) : base(session) { _database = database; _data = data; }
    public NotesDatabase Parent { get { EnsureAlive(); return _database; } }
    public string UserName { get { EnsureAlive(); return _data.UserName; } }
    public int Level { get { EnsureAlive(); return _data.Level; } }
    public string LevelName { get { EnsureAlive(); return _data.LevelName; } }
    public int AccessFlags { get { EnsureAlive(); return _data.AccessFlags; } }
    public string[] NamesList { get { EnsureAlive(); return _data.NamesList.ToArray(); } }
    public string[] Groups { get { EnsureAlive(); return _data.Groups.ToArray(); } }
    public string[] Roles { get { EnsureAlive(); return _data.Roles.ToArray(); } }
    public bool CannotCreateDocuments => HasFlag(0x0001);
    public bool IsServerEntry => HasFlag(0x0002);
    public bool CannotDeleteDocuments => HasFlag(0x0004);
    public bool CanCreatePersonalAgents => HasFlag(0x0008);
    public bool CanCreatePersonalFolders => HasFlag(0x0010);
    public bool IsPersonEntry => HasFlag(0x0020);
    public bool IsGroupEntry => HasFlag(0x0040);
    public bool CanCreateSharedFolders => HasFlag(0x0080);
    public bool CanCreateLotusScriptAgents => HasFlag(0x0100);
    public bool IsPublicReader => HasFlag(0x0200);
    public bool IsPublicWriter => HasFlag(0x0400);
    public bool CannotReplicateOrCopy => HasFlag(0x1000);
    private bool HasFlag(int flag) { EnsureAlive(); return (_data.AccessFlags & flag) != 0; }
    protected override void ReleaseNative() { }
}
