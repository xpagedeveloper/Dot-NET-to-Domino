using System.Diagnostics;
using DotNetToDomino.Internal;
using DotNetToDomino.Interop;

namespace DotNetToDomino;

/// <summary>Represents one initialized HCL Notes/Domino runtime session.</summary>
public sealed class NotesSession : IDisposable
{
    private static readonly object SessionGate = new();
    private static NotesSession? _activeSession;

    private readonly object _childrenGate = new();
    private readonly List<NotesObject> _children = [];
    private readonly Dictionary<NotesDatabase, nint> _databaseHandles = [];
    private readonly NotesRuntimeLibrary _runtime;
    private readonly NotesNativeApi _api;
    private bool _recycled;
    private bool _recycling;

    public NotesSession(string runtimeDirectory, string? notesIni = null, string? idPassword = null)
    {
        lock (SessionGate)
        {
            if (_activeSession is { IsRecycled: false })
                throw new InvalidOperationException("Only one NotesSession may be active in a process at a time.");

            _runtime = NotesRuntimeLibrary.Load(runtimeDirectory);
            _api = new NotesNativeApi(_runtime);
            RuntimeDirectory = _runtime.RuntimeDirectory;
            NotesIni = string.IsNullOrWhiteSpace(notesIni) ? string.Empty : Path.GetFullPath(notesIni);
            Platform = ResolvePlatform();
            NotesVersion = ResolveNotesVersion(_runtime.LibraryPath);
            NotesBuildVersion = ResolveBuildVersion(_runtime.LibraryPath);

            try
            {
                _api.Initialize(NotesIni.Length == 0 ? null : NotesIni, idPassword);
                Username = _api.GetUserName();
                CommonUsername = ExtractCommonName(_api.CanonicalizeName(Username));
                _activeSession = this;
            }
            catch
            {
                try { _api.Terminate(); }
                catch { }
                _runtime.Dispose();
                throw;
            }
        }
    }

    public string RuntimeDirectory { get; }
    public string NotesIni { get; }
    public string Username { get; }
    public string UserName => Username;
    public string CommonUsername { get; }
    public string CommonUserName => CommonUsername;
    public string NotesVersion { get; }
    public long NotesBuildVersion { get; }
    public string Platform { get; }
    public bool IsRecycled => _recycled;

    internal NotesNativeApi Api => _api;

    public NotesDatabase OpenDatabase(string server, string filePath)
    {
        EnsureAlive();
        server = server?.Trim() ?? string.Empty;
        filePath = filePath?.Trim() ?? string.Empty;

        var database = new NotesDatabase(this, server, filePath);
        if (filePath.Length == 0)
            return database;

        try
        {
            var handle = _api.OpenDatabase(server, filePath);
            lock (_childrenGate)
                _databaseHandles[database] = handle;
            database.IsOpen = handle != 0;
        }
        catch (NotesException)
        {
            database.IsOpen = false;
        }

        return database;
    }

    public NotesDatabase OpenByReplicaID(string server, string replicaId)
    {
        EnsureAlive();
        ArgumentException.ThrowIfNullOrWhiteSpace(replicaId);

        return new NotesDatabase(this, server?.Trim() ?? string.Empty, string.Empty)
        {
            ReplicaID = replicaId.Trim()
        };
    }

    public NotesName CreateName(string value)
    {
        EnsureAlive();
        var original = value ?? string.Empty;
        var result = new NotesName(this, original)
        {
            Canonical = _api.CanonicalizeName(original)
        };
        result.Common = ExtractCommonName(result.Canonical);
        return result;
    }

    public NotesDateTime CreateDateTime(string value)
    {
        EnsureAlive();
        return new NotesDateTime(this, value ?? string.Empty);
    }

    public NotesDateTime CreateDateTimeNow()
    {
        EnsureAlive();
        return new NotesDateTime(this, DateTimeOffset.Now);
    }

    public NotesStream CreateStream()
    {
        EnsureAlive();
        return new NotesStream(this);
    }

    public NotesDXLImporter CreateDXLImporter()
    {
        EnsureAlive();
        return new NotesDXLImporter(this);
    }

    public NotesDXLExporter CreateDXLExporter()
    {
        EnsureAlive();
        return new NotesDXLExporter(this);
    }

    public void Recycle()
    {
        lock (SessionGate)
        {
            if (_recycled || _recycling)
                return;

            _recycling = true;
            try
            {
                while (true)
                {
                    NotesObject? child;
                    lock (_childrenGate)
                        child = _children.Count == 0 ? null : _children[^1];

                    if (child is null)
                        break;

                    try { child.Recycle(); }
                    catch { Unregister(child); }
                }

                _api.Terminate();
                _recycled = true;
            }
            finally
            {
                _runtime.Dispose();
                _recycling = false;
                if (ReferenceEquals(_activeSession, this))
                    _activeSession = null;
            }

            GC.SuppressFinalize(this);
        }
    }

    public void Dispose() => Recycle();

    internal void Register(NotesObject value)
    {
        EnsureAlive();
        lock (_childrenGate)
            _children.Add(value);
    }

    internal void Unregister(NotesObject value)
    {
        nint databaseHandle = 0;
        lock (_childrenGate)
        {
            _children.Remove(value);
            if (value is NotesDatabase database && _databaseHandles.Remove(database, out var handle))
                databaseHandle = handle;
        }

        if (databaseHandle != 0 && _api.IsInitialized)
            _api.CloseDatabase(databaseHandle);
    }

    internal void EnsureAlive()
    {
        if (_recycled)
            throw new ObjectDisposedException(nameof(NotesSession), "NotesSession has been recycled.");
    }

    private static string ExtractCommonName(string canonical)
    {
        foreach (var part in canonical.Split('/'))
        {
            var value = part.Trim();
            if (value.StartsWith("CN=", StringComparison.OrdinalIgnoreCase))
                return value[3..];
        }
        return canonical;
    }

    private static string ResolvePlatform()
    {
        if (OperatingSystem.IsWindows())
            return Environment.Is64BitProcess ? "Windows/64" : "Windows/32";
        if (OperatingSystem.IsLinux())
            return Environment.Is64BitProcess ? "Linux/64" : "UNIX";
        if (OperatingSystem.IsMacOS())
            return Environment.Is64BitProcess ? "Macintosh/64" : "Macintosh";
        return "UNIX";
    }

    private static string ResolveNotesVersion(string libraryPath)
    {
        try
        {
            var info = FileVersionInfo.GetVersionInfo(libraryPath);
            return (info.ProductVersion ?? info.FileVersion ?? string.Empty).Trim();
        }
        catch
        {
            return string.Empty;
        }
    }

    private static long ResolveBuildVersion(string libraryPath)
    {
        try
        {
            return FileVersionInfo.GetVersionInfo(libraryPath).FileBuildPart;
        }
        catch
        {
            return 0;
        }
    }
}
