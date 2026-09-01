using System.Runtime.InteropServices;

namespace DotNetToDomino.Interop;

internal sealed partial class NotesNativeApi
{
    private const ushort DocMemberModified = 4;
    private const ushort DocMemberParentNoteId = 10;
    private const ushort DocMemberResponses = 12;
    private const uint DeletedNoteIdFlag = 0x80000000u;
    private const ushort DocNoteClassView = 0x0008;

    internal string[] GetAllItemNames(nint note)
    {
        EnsureInitialized();
        using var thread = EnterThread();
        var names = new List<string>();
        Doc_NSFItemScanProc callback = (_, _, name, nameLength, _, _, _) =>
        {
            if (name != 0 && nameLength > 0) names.Add(FromLmbcs(name, nameLength));
            return 0;
        };
        CheckStatus(Resolve<Doc_NSFItemScan>("NSFItemScan")(note, callback, 0), "NSFItemScan");
        GC.KeepAlive(callback);
        return names.ToArray();
    }

    internal long GetDocumentSize(nint note)
    {
        EnsureInitialized();
        using var thread = EnterThread();
        long size = 0;
        Doc_NSFItemScanProc callback = (_, _, _, nameLength, _, valueLength, _) =>
        {
            size += nameLength + valueLength;
            return 0;
        };
        CheckStatus(Resolve<Doc_NSFItemScan>("NSFItemScan")(note, callback, 0), "NSFItemScan");
        GC.KeepAlive(callback);
        return size;
    }

    internal NotesTimeDate GetDocumentCreated(nint note)
    {
        EnsureInitialized();
        using var thread = EnterThread();
        var pointer = Marshal.AllocHGlobal(28);
        try
        {
            Zero(pointer, 28);
            Resolve<Doc_NSFNoteGetInfo>("NSFNoteGetInfo")(note, 2, pointer);
            return Marshal.PtrToStructure<NotesTimeDate>(nint.Add(pointer, 8));
        }
        finally { Marshal.FreeHGlobal(pointer); }
    }

    internal NotesTimeDate GetDocumentLastModified(nint note)
    {
        EnsureInitialized();
        using var thread = EnterThread();
        var pointer = Marshal.AllocHGlobal(8);
        try
        {
            Zero(pointer, 8);
            Resolve<Doc_NSFNoteGetInfo>("NSFNoteGetInfo")(note, DocMemberModified, pointer);
            return Marshal.PtrToStructure<NotesTimeDate>(pointer);
        }
        finally { Marshal.FreeHGlobal(pointer); }
    }

    internal uint GetParentNoteId(nint note)
    {
        EnsureInitialized();
        using var thread = EnterThread();
        var pointer = Marshal.AllocHGlobal(4);
        try
        {
            Zero(pointer, 4);
            Resolve<Doc_NSFNoteGetInfo>("NSFNoteGetInfo")(note, DocMemberParentNoteId, pointer);
            return unchecked((uint)Marshal.ReadInt32(pointer));
        }
        finally { Marshal.FreeHGlobal(pointer); }
    }

    internal uint[] GetResponseIds(nint note)
    {
        EnsureInitialized();
        using var thread = EnterThread();
        var pointer = Marshal.AllocHGlobal(4);
        try
        {
            Zero(pointer, 4);
            Resolve<Doc_NSFNoteGetInfo>("NSFNoteGetInfo")(note, DocMemberResponses, pointer);
            var table = unchecked((uint)Marshal.ReadInt32(pointer));
            if (table == 0) return [];
            var ids = new List<uint>();
            uint id = 0;
            var first = 1;
            while (Resolve<Doc_IDScan>("IDScan")(table, first, ref id) != 0)
            {
                first = 0;
                if (id != 0) ids.Add(id);
            }
            return ids.ToArray();
        }
        finally { Marshal.FreeHGlobal(pointer); }
    }

    internal void SetDocumentUnread(nint db, uint noteId, string userName, bool unread)
    {
        EnsureInitialized();
        using var thread = EnterThread();
        userName = userName.Trim();
        if (userName.Length == 0) throw new ArgumentException("User name is required.", nameof(userName));
        using var user = ToLmbcs(userName);
        CheckStatus(Resolve<Doc_NSFDbGetUnreadNoteTable>("NSFDbGetUnreadNoteTable")(
            db, user.Pointer, checked((ushort)user.Length), 1, out var table), "NSFDbGetUnreadNoteTable");
        if (table == 0) return;
        uint original = 0;
        try
        {
            CheckStatus(Resolve<Doc_IDTableCopy>("IDTableCopy")(table, out original), "IDTableCopy");
            CheckStatus(Resolve<Doc_NSFDbUpdateUnread>("NSFDbUpdateUnread")(db, table), "NSFDbUpdateUnread");
            if (unread)
                CheckStatus(Resolve<Doc_IDInsert>("IDInsert")(table, noteId & ~DeletedNoteIdFlag, 0), "IDInsert(unread)");
            else
                CheckStatus(Resolve<Doc_IDDelete>("IDDelete")(table, noteId & ~DeletedNoteIdFlag, 0), "IDDelete(read)");
            CheckStatus(Resolve<Doc_NSFDbSetUnreadNoteTable>("NSFDbSetUnreadNoteTable")(
                db, user.Pointer, checked((ushort)user.Length), 0, original, table), "NSFDbSetUnreadNoteTable");
        }
        finally
        {
            if (original != 0) _ = Resolve<Doc_IDDestroyTable>("IDDestroyTable")(original);
            _ = Resolve<Doc_IDDestroyTable>("IDDestroyTable")(table);
        }
    }

    internal void PutDocumentInFolder(nint db, uint noteId, string folderName)
    {
        EnsureInitialized();
        using var thread = EnterThread();
        using var name = ToLmbcs(folderName.Trim());
        CheckStatus(Resolve<Doc_NIFFindDesignNote>("NIFFindDesignNote")(db, name.Pointer, DocNoteClassView, out var folderNoteId), "NIFFindDesignNote(folder)");
        WithSingleIdTable(noteId, table => CheckStatus(Resolve<Doc_FolderDocAdd>("FolderDocAdd")(
            db, 0, folderNoteId, table, 0), "FolderDocAdd"));
    }

    internal void RemoveDocumentFromFolder(nint db, uint noteId, string folderName)
    {
        EnsureInitialized();
        using var thread = EnterThread();
        using var name = ToLmbcs(folderName.Trim());
        CheckStatus(Resolve<Doc_NIFFindDesignNote>("NIFFindDesignNote")(db, name.Pointer, DocNoteClassView, out var folderNoteId), "NIFFindDesignNote(folder)");
        WithSingleIdTable(noteId, table => CheckStatus(Resolve<Doc_FolderDocRemove>("FolderDocRemove")(
            db, 0, folderNoteId, table, 0), "FolderDocRemove"));
    }

    private void WithSingleIdTable(uint noteId, Action<uint> action)
    {
        CheckStatus(Resolve<Doc_IDCreateTable>("IDCreateTable")(4, out var table), "IDCreateTable");
        try
        {
            CheckStatus(Resolve<Doc_IDInsert>("IDInsert")(table, noteId & ~DeletedNoteIdFlag, 0), "IDInsert");
            action(table);
        }
        finally { _ = Resolve<Doc_IDDestroyTable>("IDDestroyTable")(table); }
    }

    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort Doc_NSFItemScan(nint note, Doc_NSFItemScanProc callback, nint context);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort Doc_NSFItemScanProc(ushort spare, ushort flags, nint name, ushort nameLength, nint value, uint valueLength, nint context);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate void Doc_NSFNoteGetInfo(nint note, ushort member, nint value);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate int Doc_IDScan(uint table, int first, ref uint id);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort Doc_NSFDbGetUnreadNoteTable(nint db, nint userName, ushort userNameLength, int create, out uint table);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort Doc_NSFDbUpdateUnread(nint db, uint table);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort Doc_NSFDbSetUnreadNoteTable(nint db, nint userName, ushort userNameLength, int force, uint original, uint updated);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort Doc_IDTableCopy(uint source, out uint destination);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort Doc_IDCreateTable(ushort alignment, out uint table);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort Doc_IDInsert(uint table, uint id, nint inserted);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort Doc_IDDelete(uint table, uint id, nint deleted);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort Doc_IDDestroyTable(uint table);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort Doc_NIFFindDesignNote(nint db, nint name, ushort noteClass, out uint noteId);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort Doc_FolderDocAdd(nint dataDb, nint folderDb, uint folderNoteId, uint idTable, uint flags);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort Doc_FolderDocRemove(nint dataDb, nint folderDb, uint folderNoteId, uint idTable, uint flags);
}
