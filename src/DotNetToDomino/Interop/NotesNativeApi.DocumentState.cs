using System.Runtime.InteropServices;

namespace DotNetToDomino.Interop;

internal sealed partial class NotesNativeApi
{
    private const ushort DocumentStateErrNoteDeleted = 0x0225;
    private const ushort DocumentStateErrInvalidNote = 0x0227;

    internal bool IsDocumentDeleted(nint database, uint noteId)
    {
        EnsureInitialized();
        using var thread = EnterThread();
        var status = GetDocumentInfoStatus(database, noteId);
        if ((status & 0x3fff) == DocumentStateErrNoteDeleted) return true;
        if (status == 0 || (status & 0x3fff) == DocumentStateErrInvalidNote) return false;
        CheckStatus(status, "NSFDbGetNoteInfoExt");
        return false;
    }

    internal bool IsDocumentValid(nint database, uint noteId)
    {
        EnsureInitialized();
        using var thread = EnterThread();
        var status = GetDocumentInfoStatus(database, noteId);
        if (status == 0) return true;
        var code = (ushort)(status & 0x3fff);
        if (code is DocumentStateErrNoteDeleted or DocumentStateErrInvalidNote) return false;
        CheckStatus(status, "NSFDbGetNoteInfoExt");
        return false;
    }

    internal NotesTimeDate GetDocumentLastAccessed(nint note)
    {
        EnsureInitialized();
        using var thread = EnterThread();
        const ushort noteMemberAccessed = 8;
        var pointer = Marshal.AllocHGlobal(Marshal.SizeOf<NotesTimeDate>());
        try
        {
            Zero(pointer, Marshal.SizeOf<NotesTimeDate>());
            Resolve<DocumentState_NSFNoteGetInfo>("NSFNoteGetInfo")(note, noteMemberAccessed, pointer);
            return Marshal.PtrToStructure<NotesTimeDate>(pointer);
        }
        finally { Marshal.FreeHGlobal(pointer); }
    }

    private ushort GetDocumentInfoStatus(nint database, uint noteId) =>
        Resolve<DocumentState_NSFDbGetNoteInfoExt>("NSFDbGetNoteInfoExt")(
            database, noteId, 0, 0, 0, 0, 0, 0);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate ushort DocumentState_NSFDbGetNoteInfoExt(nint database, uint noteId, nint retNoteOid, nint retModified, nint retNoteClass, nint retAddedToFile, nint retResponseCount, nint retParentNoteId);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate void DocumentState_NSFNoteGetInfo(nint note, ushort noteMember, nint value);
}
