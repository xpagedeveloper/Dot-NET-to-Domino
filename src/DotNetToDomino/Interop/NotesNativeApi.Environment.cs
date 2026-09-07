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

internal static class NotesNativeApiEnvironmentExtensions
{
    internal static string GetEnvironmentString(this NotesNativeApi api, string name) => api.ReadEnvironmentString(name);
}
