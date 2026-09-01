using System.Runtime.InteropServices;
using NotesToDotNet.Internal;

namespace NotesToDotNet.Interop;

internal sealed class NotesNativeApi
{
    private readonly NotesRuntimeLibrary _runtime;
    private readonly Dictionary<string, Delegate> _delegates = new(StringComparer.Ordinal);

    internal NotesNativeApi(NotesRuntimeLibrary runtime)
    {
        _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
    }

    internal TDelegate Resolve<TDelegate>(string exportName) where TDelegate : Delegate
    {
        if (_delegates.TryGetValue(exportName, out var existing))
            return (TDelegate)existing;

        var address = _runtime.GetExport(exportName);
        var resolved = Marshal.GetDelegateForFunctionPointer<TDelegate>(address);
        _delegates.Add(exportName, resolved);
        return resolved;
    }

    internal static void CheckStatus(ushort status, string operation)
    {
        if (status == 0)
            return;

        throw new NotesException(status, $"HCL Notes/Domino C API call failed: {operation}. Status: 0x{status:X4}.");
    }
}
