using System.Runtime.InteropServices;

namespace DotNetToDomino.Interop;

internal sealed partial class NotesNativeApi
{
    internal uint CompactDatabase(string path, ushort options)
    {
        EnsureInitialized();
        using var thread = EnterThread();
        using var pathname = ToLmbcs(path);
        CheckStatus(Resolve<DbCompact>("NSFDbCompact")(pathname.Pointer, options, out var stats), "NSFDbCompact");
        return stats;
    }

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate ushort DbCompact(nint pathname, ushort options, out uint retStats);
}
