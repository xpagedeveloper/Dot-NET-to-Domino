namespace DotNetToDomino.Interop;

internal sealed partial class NotesNativeApi
{
    internal IReadOnlyList<uint> ExecuteDominoQuery(nint database, string query, int maxDocs, int maxEntries, int timeoutSeconds, bool noViews, bool refreshViews, out string explain, out string errorText)
    {
        EnsureInitialized();
        using var thread = EnterThread();
        using var text = ToLmbcs(query);
        nint results = 0, error = 0, explainHandle = 0;
        var flags = noViews ? 0x0001 : 0;
        if (refreshViews) flags |= 0x0002;
        try
        {
            var status = Resolve<Query_NSFQueryDBExt2>("NSFQueryDBExt2")(
                database, text.Pointer, checked((int)text.Length), flags,
                maxDocs, maxEntries, checked(timeoutSeconds * 1000), out results, out error, out explainHandle, 0);
            CheckStatus(status, "NSFQueryDBExt2");
            explain = explainHandle == 0 ? string.Empty : ReadHandleText(explainHandle);
            errorText = error == 0 ? string.Empty : ReadHandleText(error);
            if (results == 0) return Array.Empty<uint>();
            var ids = new List<uint>();
            uint id = 0;
            var first = 1;
            var scan = Resolve<Query_IDScan>("IDScan");
            while (scan(results, first, ref id) != 0) { first = 0; if (id != 0) ids.Add(id); }
            return ids.Distinct().ToArray();
        }
        finally
        {
            if (results != 0) Resolve<Query_OSMemFree>("OSMemFree")(results);
            if (error != 0) Resolve<Query_OSMemFree>("OSMemFree")(error);
            if (explainHandle != 0) Resolve<Query_OSMemFree>("OSMemFree")(explainHandle);
        }
    }

    private string ReadHandleText(nint handle)
    {
        var pointer = Resolve<Query_OSLockObject>("OSLockObject")(handle);
        if (pointer == 0) return string.Empty;
        try { return FromLmbcsZeroTerminated(pointer, 64 * 1024); }
        finally { Resolve<Query_OSUnlockObject>("OSUnlockObject")(handle); }
    }

    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)]
    private delegate ushort Query_NSFQueryDBExt2(nint db, nint query, int queryLength, int flags, int maxDocs, int maxEntries, int maxMsecs, out nint results, out nint error, out nint explain, nint argList);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] private delegate int Query_IDScan(nint table, int first, ref uint noteId);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] private delegate nint Query_OSLockObject(nint handle);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] private delegate void Query_OSUnlockObject(nint handle);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] private delegate ushort Query_OSMemFree(nint handle);
}
