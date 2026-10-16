using System.Diagnostics;

namespace DotNetToDomino;

/// <summary>Inspects the native Notes runtime before a session is initialized.</summary>
public sealed record NotesRuntimeInfo(
    string RuntimeDirectory,
    string NativeLibraryPath,
    bool Exists,
    long FileSize,
    string FileVersion,
    string FileArchitecture,
    string ProcessArchitecture)
{
    public bool ArchitectureMatches =>
        string.Equals(FileArchitecture, ProcessArchitecture, StringComparison.OrdinalIgnoreCase)
        || string.Equals(FileArchitecture, "unknown", StringComparison.OrdinalIgnoreCase)
        || string.Equals(ProcessArchitecture, "unknown", StringComparison.OrdinalIgnoreCase);
}

public static class NotesRuntimeDiagnostics
{
    public static NotesRuntimeInfo Inspect(string runtimeDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runtimeDirectory);
        var directory = Path.GetFullPath(runtimeDirectory.Trim());
        var path = Path.Combine(directory, OperatingSystem.IsWindows() ? "nnotes.dll" : OperatingSystem.IsMacOS() ? "libnotes.dylib" : "libnotes.so");
        var exists = File.Exists(path);
        var size = exists ? new FileInfo(path).Length : 0;
        var version = exists ? (FileVersionInfo.GetVersionInfo(path).FileVersion ?? string.Empty).Trim() : string.Empty;
        return new NotesRuntimeInfo(directory, path, exists, size, version, exists ? ReadArchitecture(path) : "missing", GetProcessArchitecture());
    }

    private static string GetProcessArchitecture() => Environment.Is64BitProcess ? "x64" : "x86";

    private static string ReadArchitecture(string path)
    {
        if (!OperatingSystem.IsWindows()) return "unknown";
        try
        {
            using var stream = File.OpenRead(path);
            using var reader = new BinaryReader(stream);
            if (reader.ReadUInt16() != 0x5A4D) return "unknown";
            stream.Position = 0x3C;
            stream.Position = reader.ReadUInt32();
            if (reader.ReadUInt32() != 0x00004550) return "unknown";
            return reader.ReadUInt16() switch { 0x014C => "x86", 0x8664 => "x64", _ => "unknown" };
        }
        catch { return "unknown"; }
    }
}
