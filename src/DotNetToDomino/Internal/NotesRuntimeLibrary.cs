using System.Runtime.InteropServices;

namespace DotNetToDomino.Internal;

internal sealed class NotesRuntimeLibrary : IDisposable
{
    private nint _handle;

    private NotesRuntimeLibrary(string runtimeDirectory, string libraryPath, nint handle)
    {
        RuntimeDirectory = runtimeDirectory;
        LibraryPath = libraryPath;
        _handle = handle;
    }

    internal string RuntimeDirectory { get; }
    internal string LibraryPath { get; }
    internal bool IsLoaded => _handle != 0;
    internal nint Handle => _handle;

    internal static NotesRuntimeLibrary Load(string runtimeDirectory)
    {
        if (string.IsNullOrWhiteSpace(runtimeDirectory))
            throw new ArgumentException("The Notes/Domino runtime directory is required.", nameof(runtimeDirectory));

        var resolvedDirectory = Path.GetFullPath(runtimeDirectory.Trim());
        if (!Directory.Exists(resolvedDirectory))
            throw new DirectoryNotFoundException($"Notes/Domino runtime directory does not exist: {resolvedDirectory}");

        var candidates = OperatingSystem.IsWindows()
            ? new[] { "nnotes.dll" }
            : OperatingSystem.IsMacOS()
                ? new[] { "libnotes.dylib", "libnotes64.dylib" }
                : new[] { "libnotes.so", "libnotes64.so" };

        foreach (var candidate in candidates)
        {
            var path = Path.Combine(resolvedDirectory, candidate);
            if (!File.Exists(path))
                continue;

            try
            {
                if (OperatingSystem.IsWindows())
                {
                    // nnotes.dll has native dependencies beside it. Ensure Windows
                    // resolves those dependencies from the selected Notes runtime,
                    // matching the XPScript host setup.
                    SetDllDirectory(resolvedDirectory);
                }
                return new NotesRuntimeLibrary(resolvedDirectory, path, NativeLibrary.Load(path));
            }
            catch (Exception ex) when (ex is DllNotFoundException or BadImageFormatException)
            {
                throw new NotesException($"Failed to load the HCL Notes/Domino native runtime from '{path}'.", ex);
            }
        }

        throw new DllNotFoundException(
            $"No HCL Notes/Domino native runtime library was found in '{resolvedDirectory}'. Expected: {string.Join(", ", candidates)}.");
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool SetDllDirectory(string? path);

    internal nint GetExport(string name)
    {
        if (_handle == 0)
            throw new ObjectDisposedException(nameof(NotesRuntimeLibrary));

        if (!NativeLibrary.TryGetExport(_handle, name, out var address))
            throw new EntryPointNotFoundException($"Notes/Domino C API export '{name}' was not found in '{LibraryPath}'.");

        return address;
    }

    public void Dispose()
    {
        if (_handle == 0)
            return;

        NativeLibrary.Free(_handle);
        _handle = 0;
        GC.SuppressFinalize(this);
    }
}
