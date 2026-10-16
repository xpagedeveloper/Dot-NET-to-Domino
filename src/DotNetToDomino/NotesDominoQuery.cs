namespace DotNetToDomino;

/// <summary>Configures and executes Domino Query Language (DQL) queries for a database.</summary>
/// <remarks>
/// The Notes C API exposes the low-level NSF query sample, but does not publish the
/// server-side DominoQuery handle used by LotusScript/JNX. This object therefore
/// preserves the complete configuration surface and reports a clear
/// <see cref="NotSupportedException"/> for operations that require that unavailable
/// server-side handle, rather than silently treating DQL as a formula search.
/// </remarks>
public sealed class NotesDominoQuery : NotesObject
{
    private readonly NotesDatabase _database;
    internal NotesDominoQuery(NotesSession session, NotesDatabase database) : base(session) => _database = database;

    public NotesDatabase Parent => _database;
    public int MaxScanDocs { get; set; } = 500_000;
    public int MaxScanEntries { get; set; } = 200_000;
    public bool NoViews { get; set; }
    public bool RefreshViews { get; set; }
    public bool RefreshFullText { get; set; }
    public bool RefreshDesignCatalog { get; set; }
    public bool RebuildDesignCatalog { get; set; }
    public int TimeoutSec { get; set; } = 300;
    public string LastExplain { get; private set; } = string.Empty;
    public string LastError { get; private set; } = string.Empty;

    public void SetNamedVariable(string name, object value) { EnsureAlive(); RequireText(name, nameof(name)); ArgumentNullException.ThrowIfNull(value); throw new NotSupportedException("Named DQL variables require NSFQueryDBAddArgs value-list marshalling and are not enabled yet."); }
    public void ResetNamedVariables() { EnsureAlive(); throw new NotSupportedException("Named DQL variables are not enabled yet."); }
    public NotesDocumentCollection Execute(string query) { EnsureAlive(); var ids = ExecuteNative(query, out _); return _database.Own(new NotesDocumentCollection(Session, _database, ids)); }
    public string Explain(string query) { EnsureAlive(); ExecuteNative(query, out var explain); return explain; }
    public bool Parse(string query) { EnsureAlive(); ExecuteNative(query, out _); return true; }
    protected override void ReleaseNative() { }
    private IReadOnlyList<uint> ExecuteNative(string query, out string explain)
    {
        RequireText(query, nameof(query));
        if (MaxScanDocs < 1 || MaxScanEntries < 1 || TimeoutSec < 1) throw new ArgumentOutOfRangeException("DQL scan limits and timeout must be positive.");
        var result = Session.Api.ExecuteDominoQuery(_database.Handle, query.Trim(), MaxScanDocs, MaxScanEntries, TimeoutSec, NoViews, RefreshViews, out explain, out var error);
        LastExplain = explain;
        LastError = error;
        return result;
    }
}
