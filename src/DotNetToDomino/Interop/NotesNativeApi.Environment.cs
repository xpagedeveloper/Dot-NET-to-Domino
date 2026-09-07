using System.Runtime.InteropServices;

namespace DotNetToDomino.Interop;

internal sealed partial class NotesNativeApi
{
    internal string GetEnvironmentString(string name)
    {
        EnsureInitialized();
        using var thread = EnterThread();
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        using var variableName = ToLmbcs(name.Trim());
        const int capacity = 32768;
        var buffer = Marshal.AllocHGlobal(capacity);
        try
        {
            Zero(buffer, capacity);
            var found = Resolve<Environment_OSGetEnvironmentString>("OSGetEnvironmentString")(
                variableName.Pointer, buffer, checked((ushort)capacity));
            if (found == 0) return string.Empty;
            var length = 0;
            while (length < capacity && Marshal.ReadByte(buffer, length) != 0) length++;
            return length == 0 ? string.Empty : FromLmbcs(buffer, length);
        }
        finally { Marshal.FreeHGlobal(buffer); }
    }

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate int Environment_OSGetEnvironmentString(nint variableName, nint returnValueBuffer, ushort bufferLength);
}
