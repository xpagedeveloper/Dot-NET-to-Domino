using System.Diagnostics;
using NotesToDotNet.Internal;

namespace NotesToDotNet;

/// <summary>Represents one initialized HCL Notes/Domino runtime session.</summary>
public sealed class NotesSession : IDisposable
{
    private static readonly object SessionGate = new();
    private static NotesSession? _activeSession;

    private readonly object _childrenGate = new();
    private readonly List<NotesObject> _children = [];
    private readonly NotesRuntimeLibrary _runtime;
    private bool _recycled;
    private bool _recycling;

    public NotesSession(string runtimeDirectory, string? notesIni = null, string? idPassword = null)
    {
        lock (SessionGate)
        {
            if (_activeSession is { IsRecycled: false })
                throw new InvalidOperationException("Only one NotesSession may be active in a process at a time.");

            _runtime = NotesRuntimeLibrary.Load(runtimeDirectory);
            RuntimeDirectory = _runtime.RuntimeDirectory;
            NotesIni = string.IsNullOrWhiteSpace(notesIni) ? string.Empty : Path.GetFullPath(notesIni);
            IdPasswordWasProvided = !string.IsNullOrEmpty(idPassword);
            Platform = ResolvePlatform();
            NotesVersion = ResolveNotesVersion(_runtime.LibraryPath);
            NotesBuildVersion = ResolveBuildVersion(_runtime.LibraryPath);

            // Native NotesInitExtended/NotesTerm binding is implemented in the interop layer next.
            _activeSession = this;
        }
    }

    public string RuntimeDirectory { get; }
    public string NotesIni { get; }
    public string Username { get; internal set; } = string.Empty;
    public string UserName => Username;
    public string CommonUsername { get; internal set; } = string.Empty;
    public string CommonUserName => CommonUsername;
    public string NotesVersion { get; }
    public long NotesBuildVersion { get; }
    public string Platform { get; }
    public bool IsRecycled => _recycled;
    internal bool IdPasswordWasProvided { get; }
    internal NotesRuntimeLibrary Runtime => _runtime;

    public NotesDatabase OpenDatabase(string server, string filePath)
    {
        EnsureAlive();
        return new NotesDatabase(this, server?.Trim() ?? string.Empty, filePath?.Trim() ?? string.Empty);
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
        return new NotesName(this, value ?? string.Empty);
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

                    child.Recycle();
                }

                // NotesTerm will be invoked here once native runtime initialization is wired.
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
        lock (_childrenGate)
            _children.Remove(value);
    }

    internal void EnsureAlive()
    {
        if (_recycled)
            throw new ObjectDisposedException(nameof(NotesSession), "NotesSession has been recycled.");
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
