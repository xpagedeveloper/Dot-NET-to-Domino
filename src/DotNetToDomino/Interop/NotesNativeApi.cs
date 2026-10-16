using System.Runtime.InteropServices;
using System.Text;
using DotNetToDomino.Internal;

namespace DotNetToDomino.Interop;

internal sealed partial class NotesNativeApi
{
    private const ushort TranslateLmbcsToUtf8 = 22;
    private const ushort TranslateUtf8ToLmbcs = 24;
    private const uint KfmSwitchIdDontSetEnvVar = 0x00000008;

    [ThreadStatic]
    private static int _threadScopeDepth;

    private readonly NotesRuntimeLibrary _runtime;
    private readonly Dictionary<(string ExportName, Type DelegateType), Delegate> _delegates = new();
    private bool _initialized;
    private int _initializationThreadId;
    private string _notesIni = string.Empty;

    internal NotesNativeApi(NotesRuntimeLibrary runtime)
    {
        _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
    }

    internal bool IsInitialized => _initialized;

    internal void Initialize(string? notesIni, string? idPassword)
    {
        if (_initialized)
            return;

        _notesIni = notesIni is null ? string.Empty : Path.GetFullPath(notesIni);

        var args = new List<string> { Path.Combine(_runtime.RuntimeDirectory, "dotnet-to-domino") };
        if (!string.IsNullOrWhiteSpace(notesIni))
            args.Add("=" + Path.GetFullPath(notesIni));

        using var argv = BuildArgv(args);
        CheckStatus(Resolve<NotesInitExtendedDelegate>("NotesInitExtended")(args.Count, argv.Pointer), "NotesInitExtended");
        _initialized = true;
        _initializationThreadId = Environment.CurrentManagedThreadId;

        try
        {
            if (!string.IsNullOrEmpty(idPassword))
                AuthenticateCurrentId(idPassword);
        }
        catch
        {
            Terminate();
            throw;
        }
    }

    internal void Terminate()
    {
        if (!_initialized)
            return;

        Resolve<NotesTermDelegate>("NotesTerm")();
        _initialized = false;
        _initializationThreadId = 0;
    }

    internal NotesThreadScope EnterThread()
    {
        EnsureInitialized();

        if (Environment.CurrentManagedThreadId == _initializationThreadId)
            return default;

        if (_threadScopeDepth == 0)
            CheckStatus(Resolve<NotesInitThreadDelegate>("NotesInitThread")(), "NotesInitThread");

        _threadScopeDepth++;
        return new NotesThreadScope(this, true);
    }

    private void ExitThread()
    {
        if (_threadScopeDepth <= 0)
            return;

        _threadScopeDepth--;
        if (_threadScopeDepth == 0)
            Resolve<NotesTermThreadDelegate>("NotesTermThread")();
    }

    internal string GetUserName()
    {
        EnsureInitialized();
        using var thread = EnterThread();
        const int capacity = 2048;
        var buffer = Marshal.AllocHGlobal(capacity);
        try
        {
            Zero(buffer, capacity);
            CheckStatus(Resolve<SECKFMGetUserNameDelegate>("SECKFMGetUserName")(buffer), "SECKFMGetUserName");
            return FromLmbcsZeroTerminated(buffer, capacity - 1);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    internal string CanonicalizeName(string value)
    {
        EnsureInitialized();
        using var thread = EnterThread();
        using var input = ToLmbcs(value);
        const int capacity = 4096;
        var output = Marshal.AllocHGlobal(capacity);
        try
        {
            Zero(output, capacity);
            CheckStatus(Resolve<DNCanonicalizeDelegate>("DNCanonicalize")(0, 0, input.Pointer, output, checked((ushort)(capacity - 1)), out var length), "DNCanonicalize");
            return FromLmbcs(output, length);
        }
        finally
        {
            Marshal.FreeHGlobal(output);
        }
    }

    internal nint OpenDatabase(string server, string filePath)
    {
        EnsureInitialized();
        using var thread = EnterThread();
        using var file = ToLmbcs(filePath);
        using var serverName = ToLmbcs(server);

        nint networkPath = 0;
        try
        {
            var path = filePath.Length == 0 && server.Length == 0 ? 0 : file.Pointer;
            if (server.Length > 0)
            {
                networkPath = Marshal.AllocHGlobal(4096);
                Zero(networkPath, 4096);
                CheckStatus(
                    Resolve<OSPathNetConstructDelegate>("OSPathNetConstruct")(0, serverName.Pointer, file.Pointer, networkPath),
                    "OSPathNetConstruct");
                path = networkPath;
            }

            CheckStatus(Resolve<NSFDbOpenDelegate>("NSFDbOpen")(path, out var handle), "NSFDbOpen");
            return handle;
        }
        finally
        {
            if (networkPath != 0)
                Marshal.FreeHGlobal(networkPath);
        }
    }

    internal void CloseDatabase(nint handle)
    {
        if (handle == 0 || !_initialized)
            return;

        using var thread = EnterThread();
        CheckStatus(Resolve<NSFDbCloseDelegate>("NSFDbClose")(handle), "NSFDbClose");
    }

    internal TDelegate Resolve<TDelegate>(string exportName) where TDelegate : Delegate
    {
        var key = (exportName, typeof(TDelegate));
        if (_delegates.TryGetValue(key, out var existing))
            return (TDelegate)existing;

        var address = _runtime.GetExport(exportName);
        var resolved = Marshal.GetDelegateForFunctionPointer<TDelegate>(address);
        _delegates.Add(key, resolved);
        return resolved;
    }

    internal void CheckStatus(ushort status, string operation)
    {
        if (status == 0)
            return;

        var text = _initialized ? TryLoadStatusText(status) : string.Empty;
        var suffix = text.Length == 0 ? string.Empty : " " + text;
        throw new NotesException(status, $"HCL Notes/Domino C API call failed: {operation}. Status: 0x{status:X4}.{suffix}");
    }

    private void AuthenticateCurrentId(string password)
    {
        var idFile = GetEnvironmentString("KeyFileName");
        if (idFile.Length == 0)
            idFile = GetEnvironmentString("ServerKeyFileName");
        if (idFile.Length == 0 && _notesIni.Length > 0)
            idFile = ReadIniValue("KeyFileName");
        if (idFile.Length == 0 && _notesIni.Length > 0)
            idFile = ReadIniValue("ServerKeyFileName");
        if (idFile.Length == 0)
            throw new NotesException("Unable to determine the Notes/Domino ID file from KeyFileName or ServerKeyFileName.");

        if (!Path.IsPathRooted(idFile))
        {
            var dataDirectory = GetEnvironmentString("Directory");
            if (dataDirectory.Length == 0) dataDirectory = ReadIniValue("Directory");
            if (!Path.IsPathRooted(dataDirectory)) dataDirectory = Path.Combine(Path.GetDirectoryName(_notesIni) ?? _runtime.RuntimeDirectory, dataDirectory);
            idFile = Path.GetFullPath(Path.Combine(dataDirectory, idFile));
        }

        using var idFileText = ToLmbcs(idFile);
        using var passwordText = ToLmbcs(password);
        const int capacity = 2048;
        var userName = Marshal.AllocHGlobal(capacity);
        try
        {
            Zero(userName, capacity);
            CheckStatus(
                Resolve<SECKFMSwitchToIDFileDelegate>("SECKFMSwitchToIDFile")(
                    idFileText.Pointer,
                    passwordText.Pointer,
                    userName,
                    checked((ushort)(capacity - 1)),
                    KfmSwitchIdDontSetEnvVar,
                    0),
                "SECKFMSwitchToIDFile");
        }
        finally
        {
            Zero(passwordText.Pointer, passwordText.Length + 1);
            Marshal.FreeHGlobal(userName);
        }
    }

    private string ReadIniValue(string key)
    {
        try
        {
            foreach (var raw in File.ReadLines(_notesIni))
            {
                var line = raw.Trim();
                if (line.StartsWith(key + "=", StringComparison.OrdinalIgnoreCase)) return line[(key.Length + 1)..].Trim().Trim('"');
            }
        }
        catch { }
        return string.Empty;
    }

    private string GetEnvironmentString(string name)
    {
        using var variableName = ToLmbcs(name);
        const int capacity = 4096;
        var buffer = Marshal.AllocHGlobal(capacity);
        try
        {
            Zero(buffer, capacity);
            var found = Resolve<OSGetEnvironmentStringDelegate>("OSGetEnvironmentString")(
                variableName.Pointer,
                buffer,
                checked((ushort)(capacity - 1)));
            return found == 0 ? string.Empty : FromLmbcsZeroTerminated(buffer, capacity - 1);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private LmbcsBuffer ToLmbcs(string value)
    {
        EnsureInitialized();
        var bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
        var input = Marshal.AllocHGlobal(Math.Max(1, bytes.Length));
        var capacity = Math.Max(16, checked(bytes.Length * 4 + 8));
        var output = Marshal.AllocHGlobal(capacity);
        try
        {
            if (bytes.Length > 0)
                Marshal.Copy(bytes, 0, input, bytes.Length);
            Zero(output, capacity);
            var length = Resolve<OSTranslate32Delegate>("OSTranslate32")(
                TranslateUtf8ToLmbcs,
                input,
                checked((uint)bytes.Length),
                output,
                checked((uint)(capacity - 1)));
            if (length >= capacity)
                throw new NotesException("LMBCS conversion exceeded the output buffer.");
            Marshal.WriteByte(output, checked((int)length), 0);
            return new LmbcsBuffer(output, checked((int)length));
        }
        catch
        {
            Marshal.FreeHGlobal(output);
            throw;
        }
        finally
        {
            Marshal.FreeHGlobal(input);
        }
    }

    private string FromLmbcs(nint input, int length)
    {
        if (input == 0 || length <= 0)
            return string.Empty;

        var capacity = Math.Max(16, checked(length * 4 + 8));
        var output = Marshal.AllocHGlobal(capacity);
        try
        {
            Zero(output, capacity);
            var count = Resolve<OSTranslate32Delegate>("OSTranslate32")(
                TranslateLmbcsToUtf8,
                input,
                checked((uint)length),
                output,
                checked((uint)(capacity - 1)));
            var bytes = new byte[checked((int)count)];
            if (count > 0)
                Marshal.Copy(output, bytes, 0, bytes.Length);
            return Encoding.UTF8.GetString(bytes);
        }
        finally
        {
            Marshal.FreeHGlobal(output);
        }
    }

    private string FromLmbcsZeroTerminated(nint input, int maximum)
    {
        var length = 0;
        while (length < maximum && Marshal.ReadByte(input, length) != 0)
            length++;
        return FromLmbcs(input, length);
    }

    private string TryLoadStatusText(ushort status)
    {
        try
        {
            const int capacity = 2048;
            var buffer = Marshal.AllocHGlobal(capacity);
            try
            {
                Zero(buffer, capacity);
                var length = Resolve<OSLoadStringDelegate>("OSLoadString")(0, (ushort)(status & 0x3fff), buffer, checked((ushort)(capacity - 1)));
                return length == 0 ? string.Empty : FromLmbcs(buffer, length);
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
        catch
        {
            return string.Empty;
        }
    }

    private ArgvBuffer BuildArgv(IReadOnlyList<string> args)
    {
        var values = new List<nint>(args.Count);
        var argv = Marshal.AllocHGlobal(IntPtr.Size * args.Count);
        try
        {
            for (var i = 0; i < args.Count; i++)
            {
                var value = Marshal.StringToHGlobalAnsi(args[i]);
                values.Add(value);
                Marshal.WriteIntPtr(argv, i * IntPtr.Size, value);
            }
            return new ArgvBuffer(argv, values);
        }
        catch
        {
            foreach (var value in values)
                Marshal.FreeHGlobal(value);
            Marshal.FreeHGlobal(argv);
            throw;
        }
    }

    private void EnsureInitialized()
    {
        if (!_initialized)
            throw new ObjectDisposedException(nameof(NotesNativeApi), "Notes C API runtime is not initialized.");
    }

    private static void Zero(nint pointer, int length)
    {
        for (var i = 0; i < length; i++)
            Marshal.WriteByte(pointer, i, 0);
    }

    internal readonly struct NotesThreadScope : IDisposable
    {
        private readonly NotesNativeApi? _owner;
        private readonly bool _ownsInitialization;

        internal NotesThreadScope(NotesNativeApi owner, bool ownsInitialization)
        {
            _owner = owner;
            _ownsInitialization = ownsInitialization;
        }

        public void Dispose()
        {
            if (_ownsInitialization)
                _owner?.ExitThread();
        }
    }

    private sealed class LmbcsBuffer : IDisposable
    {
        internal LmbcsBuffer(nint pointer, int length)
        {
            Pointer = pointer;
            Length = length;
        }

        internal nint Pointer { get; private set; }
        internal int Length { get; }

        public void Dispose()
        {
            var pointer = Pointer;
            Pointer = 0;
            if (pointer != 0)
                Marshal.FreeHGlobal(pointer);
        }
    }

    private sealed class ArgvBuffer : IDisposable
    {
        private readonly List<nint> _values;

        internal ArgvBuffer(nint pointer, List<nint> values)
        {
            Pointer = pointer;
            _values = values;
        }

        internal nint Pointer { get; private set; }

        public void Dispose()
        {
            foreach (var value in _values)
                if (value != 0)
                    Marshal.FreeHGlobal(value);
            if (Pointer != 0)
                Marshal.FreeHGlobal(Pointer);
            Pointer = 0;
        }
    }

    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort NotesInitExtendedDelegate(int argc, nint argv);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate void NotesTermDelegate();
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort NotesInitThreadDelegate();
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate void NotesTermThreadDelegate();
    // Domino C API signature: (mode, input, inputLength, output, outputLength).
    // Reversing the final two arguments makes nnotes.dll write through the size
    // value and causes an 0xC0000005 access violation during FromLmbcs.
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate uint OSTranslate32Delegate(ushort mode, nint input, uint inputLength, nint output, uint outputSize);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort OSLoadStringDelegate(nint module, ushort code, nint output, ushort outputLength);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort SECKFMGetUserNameDelegate(nint output);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort OSPathNetConstructDelegate(nint portName, nint serverName, nint fileName, nint pathName);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort NSFDbOpenDelegate(nint pathName, out nint db);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort NSFDbCloseDelegate(nint db);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort DNCanonicalizeDelegate(uint flags, nint templateName, nint input, nint output, ushort outputSize, out ushort outputLength);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate int OSGetEnvironmentStringDelegate(nint variableName, nint returnValueBuffer, ushort bufferLength);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort SECKFMSwitchToIDFileDelegate(nint idFileName, nint password, nint userName, ushort maxUserNameLength, uint flags, nint reserved);
}
