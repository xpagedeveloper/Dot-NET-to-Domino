using System.Runtime.InteropServices;

namespace DotNetToDomino.Interop;

internal sealed partial class NotesNativeApi
{
    internal string SendConsoleCommand(string server, string command)
    {
        EnsureInitialized();
        ArgumentNullException.ThrowIfNull(command);
        using var thread = EnterThread();
        using var serverText = ToLmbcs(server ?? string.Empty);
        using var commandText = ToLmbcs(command);

        // NSFRemoteConsole returns LMBCS text in the caller supplied buffer.
        const int capacity = 32 * 1024;
        var result = Marshal.AllocHGlobal(capacity);
        try
        {
            Zero(result, capacity);
            var status = Resolve<NSFRemoteConsoleDelegate>("NSFRemoteConsole")(
                serverText.Pointer, commandText.Pointer, 0, result,
                checked((ushort)(capacity - 1)), out var length);
            CheckStatus(status, "NSFRemoteConsole");
            var actualLength = Math.Min((int)length, capacity - 1);
            return FromLmbcs(result, actualLength);
        }
        finally
        {
            Marshal.FreeHGlobal(result);
        }
    }

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate ushort NSFRemoteConsoleDelegate(
        nint server,
        nint command,
        ushort flags,
        nint result,
        ushort resultMax,
        out ushort resultLength);
}
