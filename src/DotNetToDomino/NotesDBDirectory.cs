namespace DotNetToDomino;

/// <summary>Enumerates Notes databases in a local data directory or on a Domino server.</summary>
public sealed class NotesDBDirectory : NotesObject
{
    private readonly string _server;
    private string[] _paths = [];
    private int _position = -1;

    internal NotesDBDirectory(NotesSession session, string server) : base(session) => _server = server;

    public string Name { get { EnsureAlive(); return _server; } }
    public NotesSession Parent { get { EnsureAlive(); return Session; } }

    public NotesDatabase? GetFirstDatabase(int type)
    {
        EnsureAlive();
        _paths = Session.Api.ListDatabases(_server, NormalizeType(type));
        _position = _paths.Length == 0 ? -1 : 0;
        return CurrentDatabase();
    }

    public NotesDatabase? GetNextDatabase()
    {
        EnsureAlive();
        if (_position < 0) return null;
        _position++;
        if (_position >= _paths.Length) { _position = -1; return null; }
        return CurrentDatabase();
    }

    public NotesDatabase OpenDatabase(string filePath)
    {
        EnsureAlive();
        return Session.OpenDatabase(_server, RequireText(filePath, nameof(filePath)));
    }

    protected override void ReleaseNative() { _paths = []; _position = -1; }

    private NotesDatabase? CurrentDatabase() => _position < 0 || _position >= _paths.Length ? null : new NotesDatabase(Session, 0, _server, _paths[_position]);
    private static int NormalizeType(int type) => type is NotesConst.REPLICA_CANDIDATE or NotesConst.TEMPLATE_CANDIDATE or NotesConst.DATABASE or NotesConst.TEMPLATE
        ? type
        : throw new ArgumentOutOfRangeException(nameof(type), type, "'type' must be REPLICA_CANDIDATE (1245), TEMPLATE_CANDIDATE (1246), DATABASE (1247), or TEMPLATE (1248).");
}
