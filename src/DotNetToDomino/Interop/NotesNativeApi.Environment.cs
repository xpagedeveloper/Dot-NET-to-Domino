namespace DotNetToDomino.Interop;

internal sealed partial class NotesNativeApi
{
    internal string ReadEnvironmentString(string name)
    {
        EnsureInitialized();
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return GetEnvironmentString(name.Trim());
    }
}
