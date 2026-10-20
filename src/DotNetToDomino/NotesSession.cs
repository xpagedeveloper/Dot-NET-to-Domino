using System.Diagnostics;
using Microsoft.Win32;
using DotNetToDomino.Internal;
using DotNetToDomino.Interop;

namespace DotNetToDomino;

/// <summary>Represents one initialized HCL Notes/Domino runtime session.</summary>
[DebuggerDisplay("{DebuggerDisplay,nq}")]
public sealed class NotesSession : IDisposable
{
    private static readonly object SessionGate = new();
    private static NotesSession? _activeSession;
    private readonly object _childrenGate = new();
    private readonly object _nativeGate = new();
    private readonly List<NotesObject> _children = [];
    private readonly NotesRuntimeLibrary _runtime;
    private readonly NotesNativeApi _api;
    private bool _recycled;
    private bool _recycling;

    public NotesSession() : this(ResolveDefaultRuntimeDirectoryWindows(), null, null) { }
    public NotesSession(string runtimeDirectory, string? notesIni = null, string? idPassword = null)
    {
        if (string.IsNullOrWhiteSpace(runtimeDirectory))
            throw new ArgumentException("The Notes program directory must be specified.", nameof(runtimeDirectory));
        runtimeDirectory = Path.GetFullPath(runtimeDirectory.Trim());
        if (!Directory.Exists(runtimeDirectory))
            throw new DirectoryNotFoundException($"The Notes program directory was not found: '{runtimeDirectory}'. Verify the HCL Notes installation path.");
        lock (SessionGate)
        {
            if (_activeSession is { IsRecycled: false }) throw new InvalidOperationException("Only one NotesSession may be active in a process at a time.");
            NotesIni = string.IsNullOrWhiteSpace(notesIni) ? string.Empty : Path.GetFullPath(notesIni.Trim());
            if (NotesIni.Length != 0)
            {
                var iniDirectory = Path.GetDirectoryName(NotesIni);
                if (string.IsNullOrWhiteSpace(iniDirectory) || !Directory.Exists(iniDirectory))
                    throw new DirectoryNotFoundException($"The directory containing notes.ini was not found: '{iniDirectory ?? NotesIni}'.");
                if (!File.Exists(NotesIni))
                    throw new FileNotFoundException($"The specified notes.ini file was not found: '{NotesIni}'.", NotesIni);
            }
            _runtime = NotesRuntimeLibrary.Load(runtimeDirectory); _api = new NotesNativeApi(_runtime); RuntimeDirectory = _runtime.RuntimeDirectory;
            var ini = ResolveNotesIniPath(RuntimeDirectory, NotesIni);
            IniDir = ini.Length == 0 ? string.Empty : Path.GetDirectoryName(ini) ?? string.Empty; DataDir = ini.Length == 0 ? string.Empty : ReadNotesIniDataDirectory(ini, IniDir);
            Platform = ResolvePlatform(); NotesVersion = ResolveNotesVersion(_runtime.LibraryPath); NotesBuildVersion = ResolveBuildVersion(_runtime.LibraryPath);
            try { _api.Initialize(NotesIni.Length == 0 ? null : NotesIni, idPassword); UserName = _api.GetUserName(); CommonUserName = ExtractCommonName(_api.CanonicalizeName(UserName)); International = new NotesInternational(this); _activeSession = this; }
            catch { try { _api.Terminate(); } catch { } _runtime.Dispose(); throw; }
        }
    }

    public string RuntimeDirectory { get; }
    public string ProgramDir => RuntimeDirectory;
    public string NotesIni { get; }
    public string IniDir { get; }
    public string DataDir { get; }
    public string UserName { get; }
    /// <summary>
    /// The effective identity for this standalone native session. A fristående C API-session har ingen agent- eller
    /// signerad körkontext, så den effektiva identiteten är den identitet som laddades från ID-filen.
    /// </summary>
    public string EffectiveUserName => UserName;
    public string CommonUserName { get; }
    public string NotesVersion { get; }
    public long NotesBuildVersion { get; }
    public string Platform { get; }
    /// <summary>Gets the regional settings associated with this Notes session.</summary>
    public NotesInternational International { get; }
    public bool ConvertMIME { get; set; } = true;
    public bool IsRecycled => _recycled;
    private string DebuggerDisplay => $"{nameof(NotesSession)}: {UserName}, Runtime={RuntimeDirectory}, IsRecycled={IsRecycled}";
    internal NotesNativeApi Api => _api;
    internal object NativeGate => _nativeGate;

    internal T WithNativeLock<T>(Func<T> action)
    {
        lock (_nativeGate)
        {
            EnsureAlive();
            return action();
        }
    }

    public IReadOnlyList<NotesDatabase> AddressBooks { get { EnsureAlive(); return _api.GetAddressBooks().Select(book => new NotesDatabase(this, 0, book.Server, book.FilePath)).ToArray(); } }
    public string HashPassword(string password) { EnsureAlive(); ArgumentException.ThrowIfNullOrWhiteSpace(password); return _api.HashPassword(password); }
    public bool VerifyPassword(string password, string hashedPassword) { EnsureAlive(); ArgumentException.ThrowIfNullOrWhiteSpace(password); ArgumentException.ThrowIfNullOrWhiteSpace(hashedPassword); return _api.VerifyPassword(password, hashedPassword); }
    public string GetEnvironmentString(string name, bool system = false) { EnsureAlive(); return _api.GetEnvironmentString(ResolveEnvironmentVariableName(name, system)); }
    public int? GetEnvironmentValue(string name, bool system = false) { EnsureAlive(); var text = _api.GetEnvironmentString(ResolveEnvironmentVariableName(name, system)); if (text.Length == 0) return null; return int.TryParse(text, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var value) ? value : null; }
    public void SetEnvironmentVar(string name, object? value, bool system = false) { EnsureAlive(); var resolved = ResolveEnvironmentVariableName(name, system); _api.SetEnvironmentVariable(resolved, ResolveEnvironmentVariableValue(value)); }
    /// <summary>Sends a command to a Domino server console and returns its LMBCS response.</summary>
    /// <param name="server">Server name, or empty string (also <c>local</c>) for the local server.</param>
    /// <param name="command">The Domino console command.</param>
    public string SendConsoleCommand(string server, string command) { EnsureAlive(); return _api.SendConsoleCommand(NormalizeServerName(server), command); }
    /// <summary>Checks whether a Domino server is reachable using the native NSPingServer API.</summary>
    public NotesServerPingResult PingServer(string server) { EnsureAlive(); ArgumentException.ThrowIfNullOrWhiteSpace(server); return _api.PingServer(server.Trim()); }

    public NotesDatabase? GetDatabase(string server, string filePath, bool createOnFail = true) { EnsureAlive(); var database = OpenDatabase(server, filePath); if (database.IsOpen || createOnFail) return database; database.Recycle(); return null; }
    public NotesDatabase OpenDatabase(string server, string filePath) { EnsureAlive(); ArgumentException.ThrowIfNullOrWhiteSpace(filePath); server = NotesSession.NormalizeServerName(server); filePath = filePath.Trim(); var directPath = IsDirectWindowsPath(filePath); if (server.Length > 0 && directPath) throw new ArgumentException("An absolute database path is valid only when server is empty.", nameof(filePath)); var nativeFilePath = server.Length == 0 && !directPath && DataDir.Length > 0 ? Path.Combine(DataDir, filePath.TrimStart('/', '\\')) : filePath; try { return new NotesDatabase(this, _api.OpenDatabase(server, nativeFilePath), server, filePath); } catch (NotesException ex) { return new NotesDatabase(this, 0, server, filePath, openError: ex.Message); } }
    public NotesDatabase OpenByReplicaID(string server, string replicaId) { EnsureAlive(); ArgumentException.ThrowIfNullOrWhiteSpace(replicaId); server = NotesSession.NormalizeServerName(server); replicaId = replicaId.Trim(); try { var filePath = _api.LocateDatabaseByReplicaId(server, replicaId); if (filePath.Length == 0) return new NotesDatabase(this, 0, server, filePath, replicaId); return new NotesDatabase(this, _api.OpenDatabase(server, filePath), server, filePath, replicaId); } catch (NotesException ex) { return new NotesDatabase(this, 0, server, string.Empty, replicaId, ex.Message); } }
    public NotesDBDirectory GetDbDirectory(string? server = null) { EnsureAlive(); return new NotesDBDirectory(this, NotesSession.NormalizeServerName(server)); }
    public NotesName CreateName(string value) { EnsureAlive(); ArgumentException.ThrowIfNullOrWhiteSpace(value); return new NotesName(this, value.Trim()); }
    public NotesDateTime CreateDateTime(string? value) { EnsureAlive(); return string.IsNullOrWhiteSpace(value) ? CreateDateTimeNow() : new NotesDateTime(this, value.Trim()); }
    public NotesDateTime CreateDateTimeNow() { EnsureAlive(); return NotesDateTime.FromNative(this, _api.CurrentTimeDate()); }
    public NotesStream CreateStream() { EnsureAlive(); return new NotesStream(this); }
    public NotesMail CreateMail() { EnsureAlive(); return new NotesMail(this); }
    public NotesDXLImporter CreateDXLImporter() { EnsureAlive(); return new NotesDXLImporter(this); }
    public NotesDXLExporter CreateDXLExporter() { EnsureAlive(); return new NotesDXLExporter(this); }
    public NotesRichTextStyle CreateRichTextStyle() { EnsureAlive(); return new NotesRichTextStyle(this); }
    public NotesRichTextParagraphStyle CreateRichTextParagraphStyle() { EnsureAlive(); return new NotesRichTextParagraphStyle(this, _api.GetDefaultRichTextParagraphStyle()); }

    public void Recycle() { lock (SessionGate) { if (_recycled || _recycling) return; _recycling = true; try { while (true) { NotesObject? child; lock (_childrenGate) { child = _children.Count == 0 ? null : _children[^1]; if (child is not null) _children.RemoveAt(_children.Count - 1); } if (child is null) break; try { child.Recycle(); } catch { Unregister(child); } } _api.Terminate(); _recycled = true; } finally { _runtime.Dispose(); _recycling = false; if (ReferenceEquals(_activeSession, this)) _activeSession = null; } GC.SuppressFinalize(this); } }
    public void Dispose() => Recycle();
    internal void Register(NotesObject value) { EnsureAlive(); lock (_childrenGate) _children.Add(value); }
    internal void Unregister(NotesObject value) { lock (_childrenGate) _children.Remove(value); }
    internal void EnsureAlive() { if (_recycled) throw new ObjectDisposedException(nameof(NotesSession), "NotesSession has been recycled."); }

    internal static string NormalizeServerName(string? server)
    {
        var name = server?.Trim() ?? string.Empty;
        return name.Equals("local", StringComparison.OrdinalIgnoreCase) ? string.Empty : name;
    }

    private static string ResolveEnvironmentVariableName(string name, bool system) { if (string.IsNullOrWhiteSpace(name)) throw new NotesException(5, "NotesSession environment variable name cannot be empty."); name = name.Trim(); if (system || name.StartsWith('$')) return name; return "$" + name; }
    private static string ResolveEnvironmentVariableValue(object? value) => value switch
    {
        string text => text,
        sbyte or byte or short or ushort or int or uint or long or ulong => Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty,
        DateTime dateTime => Convert.ToString(dateTime, System.Globalization.CultureInfo.CurrentCulture) ?? string.Empty,
        _ => throw new NotesException(13, "Environment variables must be strings, dates, or integers.")
    };
    private static string ResolveDefaultRuntimeDirectoryWindows() { if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("The parameterless NotesSession constructor is supported on Windows only. Supply runtimeDirectory explicitly on other platforms."); foreach (var registryPath in new[] { @"Software\HCL\Notes\Installer", @"Software\HCL\Domino\Installer" }) foreach (var hive in new[] { Registry.CurrentUser, Registry.LocalMachine }) try { using var key = hive.OpenSubKey(registryPath); var raw = key?.GetValue("PROGDIR") as string; if (string.IsNullOrWhiteSpace(raw)) continue; var path = Path.GetFullPath(Environment.ExpandEnvironmentVariables(raw.Trim().Trim('"'))); if (Directory.Exists(path) && File.Exists(Path.Combine(path, "nnotes.dll"))) return path; } catch { } try { using var key = Registry.LocalMachine.OpenSubKey(@"Software\HCL\Domino\1"); var raw = key?.GetValue("Path") as string; if (!string.IsNullOrWhiteSpace(raw)) { var path = Path.GetFullPath(Environment.ExpandEnvironmentVariables(raw.Trim().Trim('"'))); if (Directory.Exists(path) && File.Exists(Path.Combine(path, "nnotes.dll"))) return path; } } catch { } throw new InvalidOperationException("Unable to locate an installed HCL Notes or Domino runtime. Supply runtimeDirectory explicitly."); }
    private static bool IsDirectWindowsPath(string path) => OperatingSystem.IsWindows() && (path.Length >= 2 && char.IsLetter(path[0]) && path[1] == ':' || path.StartsWith("\\\\", StringComparison.Ordinal));
    private static string ResolveNotesIniPath(string runtimeDirectory, string explicitNotesIni) { if (explicitNotesIni.Length > 0 && File.Exists(explicitNotesIni)) return explicitNotesIni; var environmentIni = Environment.GetEnvironmentVariable("NOTESINI"); if (!string.IsNullOrWhiteSpace(environmentIni)) try { var path = Path.GetFullPath(Environment.ExpandEnvironmentVariables(environmentIni.Trim().Trim('"'))); if (File.Exists(path)) return path; } catch { } foreach (var candidate in EnumerateNotesIniCandidates(runtimeDirectory)) try { if (File.Exists(candidate)) return Path.GetFullPath(candidate); } catch { } return string.Empty; }
    private static IEnumerable<string> EnumerateNotesIniCandidates(string runtimeDirectory) { yield return Path.Combine(runtimeDirectory, "notes.ini"); yield return Path.Combine(Environment.CurrentDirectory, "notes.ini"); if (!OperatingSystem.IsWindows()) yield break; foreach (var registryPath in new[] { @"Software\HCL\Notes\Installer", @"Software\HCL\Domino\Installer" }) foreach (var hive in new[] { Registry.CurrentUser, Registry.LocalMachine }) { string? dataDirectory = null; try { using var key = hive.OpenSubKey(registryPath); dataDirectory = key?.GetValue("DATADIR") as string; } catch { } if (string.IsNullOrWhiteSpace(dataDirectory)) continue; string? expanded = null; try { expanded = Environment.ExpandEnvironmentVariables(dataDirectory.Trim().Trim('"')); } catch { } if (!string.IsNullOrWhiteSpace(expanded)) yield return Path.Combine(expanded, "notes.ini"); } string? dominoDataDirectory = null; try { using var key = Registry.LocalMachine.OpenSubKey(@"Software\HCL\Domino\1"); dominoDataDirectory = key?.GetValue("DataPath") as string; } catch { } if (!string.IsNullOrWhiteSpace(dominoDataDirectory)) { string? expanded = null; try { expanded = Environment.ExpandEnvironmentVariables(dominoDataDirectory.Trim().Trim('"')); } catch { } if (!string.IsNullOrWhiteSpace(expanded)) yield return Path.Combine(expanded, "notes.ini"); } }
    private static string ReadNotesIniDataDirectory(string iniPath, string iniDirectory) { try { foreach (var raw in File.ReadLines(iniPath)) { var line = raw.Trim(); if (line.Length == 0 || line.StartsWith(';')) continue; const string key = "Directory="; if (!line.StartsWith(key, StringComparison.OrdinalIgnoreCase)) continue; var value = Environment.ExpandEnvironmentVariables(line[key.Length..].Trim().Trim('"')); if (value.Length == 0) return string.Empty; if (!Path.IsPathRooted(value) && iniDirectory.Length > 0) value = Path.Combine(iniDirectory, value); return Path.GetFullPath(value); } } catch { } return string.Empty; }
    private static string ExtractCommonName(string canonical) { foreach (var part in canonical.Split('/')) { var value = part.Trim(); if (value.StartsWith("CN=", StringComparison.OrdinalIgnoreCase)) return value[3..]; } return canonical; }
    private static string ResolvePlatform() { if (OperatingSystem.IsWindows()) return Environment.Is64BitProcess ? "Windows/64" : "Windows/32"; if (OperatingSystem.IsLinux()) return Environment.Is64BitProcess ? "Linux/64" : "UNIX"; if (OperatingSystem.IsMacOS()) return Environment.Is64BitProcess ? "Macintosh/64" : "Macintosh"; return "UNIX"; }
    private static string ResolveNotesVersion(string libraryPath) { try { var info = FileVersionInfo.GetVersionInfo(libraryPath); return (info.ProductVersion ?? info.FileVersion ?? string.Empty).Trim(); } catch { return string.Empty; } }
    private static long ResolveBuildVersion(string libraryPath) { try { return FileVersionInfo.GetVersionInfo(libraryPath).FileBuildPart; } catch { return 0; } }
}
