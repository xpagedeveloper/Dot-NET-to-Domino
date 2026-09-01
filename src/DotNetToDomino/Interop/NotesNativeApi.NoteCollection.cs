using System.Runtime.InteropServices;

namespace DotNetToDomino.Interop;

internal sealed partial class NotesNativeApi
{
    internal IReadOnlyList<uint> SearchByNoteClass(nint db, ushort noteClassMask) => BuildNoteCollection(db, "@All", noteClassMask);

    internal IReadOnlyList<uint> BuildNoteCollection(nint db, string formula, uint noteClassMask)
    {
        EnsureInitialized();
        using var thread = EnterThread();
        if (noteClassMask == 0) return Array.Empty<uint>();
        if (string.IsNullOrWhiteSpace(formula)) formula = "@All";
        using var formulaText = ToLmbcs(formula);
        if (formulaText.Length > ushort.MaxValue) throw new ArgumentOutOfRangeException(nameof(formula), "Notes note-collection formula exceeds the C API formula length limit.");

        var status = Resolve<NoteCollection_NSFFormulaCompile>("NSFFormulaCompile")(
            0, 0, formulaText.Pointer, checked((ushort)formulaText.Length), out var formulaHandle, out _,
            out var compileError, out var errorLine, out var errorColumn, out _, out _);
        if (status != 0 || compileError != 0)
        {
            if (formulaHandle != 0) Resolve<NoteCollection_OSMemFree>("OSMemFree")(formulaHandle);
            var code = status != 0 ? status : compileError;
            throw new NotesException(code, $"Notes formula compilation failed at line {errorLine}, column {errorColumn} (0x{code:X4}).");
        }

        var ids = new List<uint>();
        NoteCollection_SearchProc callback = (_, matchPointer, _) =>
        {
            if (matchPointer == 0) return 0;
            var match = Marshal.PtrToStructure<NotesSearchMatch>(matchPointer);
            if ((match.SERetFlags & 0x01) != 0 && match.Id.NoteId != 0) ids.Add(match.Id.NoteId);
            return 0;
        };
        try
        {
            CheckStatus(Resolve<NoteCollection_NSFSearch>("NSFSearch")(
                db, formulaHandle, 0, 0, checked((ushort)noteClassMask), 0, callback, 0, 0), "NSFSearch(note collection)");
            return ids.Distinct().ToArray();
        }
        finally
        {
            Resolve<NoteCollection_OSMemFree>("OSMemFree")(formulaHandle);
            GC.KeepAlive(callback);
        }
    }

    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort NoteCollection_NSFFormulaCompile(nint formulaName, ushort formulaNameLength, nint formulaText, ushort formulaTextLength, out nint formula, out ushort formulaLength, out ushort compileError, out ushort errorLine, out ushort errorColumn, out ushort errorOffset, out ushort errorLength);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort NoteCollection_SearchProc(nint parameter, nint match, nint summary);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort NoteCollection_NSFSearch(nint db, nint formula, nint viewTitle, ushort searchFlags, ushort noteClassMask, nint since, NoteCollection_SearchProc callback, nint callbackParameter, nint until);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort NoteCollection_OSMemFree(nint handle);
}
