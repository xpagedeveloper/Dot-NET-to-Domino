using System.Runtime.InteropServices;

namespace DotNetToDomino.Interop;

internal sealed partial class NotesNativeApi
{
    internal IReadOnlyList<uint> SearchByNoteClass(nint db, ushort noteClassMask)
    {
        EnsureInitialized();
        using var thread = EnterThread();
        if (noteClassMask == 0) return Array.Empty<uint>();
        var ids = new List<uint>();
        NoteCollection_SearchProc callback = (_, matchPointer, _) =>
        {
            if (matchPointer == 0) return 0;
            var match = Marshal.PtrToStructure<NotesSearchMatch>(matchPointer);
            if ((match.SERetFlags & 0x01) != 0 && (match.NoteClass & noteClassMask) != 0 && match.Id.NoteId != 0)
                ids.Add(match.Id.NoteId);
            return 0;
        };
        CheckStatus(Resolve<NoteCollection_NSFSearch>("NSFSearch")(
            db, 0, 0, 0, noteClassMask, 0, callback, 0, 0), "NSFSearch(note class)");
        GC.KeepAlive(callback);
        return ids.Distinct().ToArray();
    }

    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort NoteCollection_SearchProc(nint parameter, nint match, nint summary);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort NoteCollection_NSFSearch(nint db, nint formula, nint viewTitle, ushort searchFlags, ushort noteClassMask, nint since, NoteCollection_SearchProc callback, nint callbackParameter, nint until);
}
