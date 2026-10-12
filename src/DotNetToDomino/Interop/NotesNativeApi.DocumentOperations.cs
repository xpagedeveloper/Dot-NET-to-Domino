using System.Runtime.InteropServices;

namespace DotNetToDomino.Interop;

internal sealed partial class NotesNativeApi
{
    private const ushort OpsNoteMemberDb = 0;
    private const ushort OpsNoteMemberId = 1;
    private const ushort OpsNoteMemberOid = 2;
    private const ushort OpsNoteTypeNoteRefList = 0x0004;
    private const ushort OpsItemSummary = 0x0004;
    private const ushort OpsNoteClassView = 0x0008;
    private const uint OpsDesignTypeShared = 0;
    private const ushort OpsErrorMask = 0x3fff;
    private const ushort OpsErrorNotFound = 0x0404;

    [StructLayout(LayoutKind.Sequential)]
    private struct OpsNotesOid
    {
        public NotesUnid Unid;
        public uint Sequence;
        public NotesTimeDate SequenceTime;
    }

    internal bool DeleteNote(nint database, uint noteId, bool force)
    {
        EnsureInitialized();
        using var thread = EnterThread();
        const ushort updateForce = 0x0001;
        var status = Resolve<Ops_NSFNoteDelete>("NSFNoteDelete")(database, noteId, force ? updateForce : (ushort)0);
        if (status == 0) return true;
        if (!force)
        {
            var text = TryLoadStatusText(status);
            if (text.Contains("conflict", StringComparison.OrdinalIgnoreCase) || text.Contains("modified", StringComparison.OrdinalIgnoreCase)) return false;
        }
        CheckStatus(status, "NSFNoteDelete");
        return false;
    }

    internal void SetUnid(nint note, string text)
    {
        EnsureInitialized();
        using var thread = EnterThread();
        var unid = ParseUnid(text);
        const int oidSize = 28;
        var oidPointer = Marshal.AllocHGlobal(oidSize);
        try
        {
            Zero(oidPointer, oidSize);
            Resolve<Ops_NSFNoteGetInfo>("NSFNoteGetInfo")(note, OpsNoteMemberOid, oidPointer);
            Marshal.StructureToPtr(unid.File, oidPointer, false);
            Marshal.StructureToPtr(unid.Note, nint.Add(oidPointer, 8), false);
            Resolve<Ops_NSFNoteSetInfo>("NSFNoteSetInfo")(note, OpsNoteMemberOid, oidPointer);
        }
        finally { Marshal.FreeHGlobal(oidPointer); }
    }

    internal void MakeResponse(nint note, nint parentNote)
    {
        EnsureInitialized();
        using var thread = EnterThread();
        var parent = ParseUnid(GetUnid(parentNote));
        if (HasItem(note, "$REF")) DeleteItem(note, "$REF");
        const int valueLength = 18;
        var value = Marshal.AllocHGlobal(valueLength);
        try
        {
            Zero(value, valueLength);
            Marshal.WriteInt16(value, 0, 1);
            Marshal.StructureToPtr(parent.File, nint.Add(value, 2), false);
            Marshal.StructureToPtr(parent.Note, nint.Add(value, 10), false);
            using var name = ToLmbcs("$REF");
            CheckStatus(Resolve<Ops_NSFItemAppend>("NSFItemAppend")(
                note, OpsItemSummary, name.Pointer, checked((ushort)name.Length), OpsNoteTypeNoteRefList, value, valueLength), "NSFItemAppend($REF)");
        }
        finally { Marshal.FreeHGlobal(value); }
    }

    internal nint CopyDocumentToDatabase(nint sourceNote, nint destinationDb)
    {
        EnsureInitialized();
        using var thread = EnterThread();
        CheckStatus(Resolve<Ops_NSFNoteCopy>("NSFNoteCopy")(sourceNote, out var copy), "NSFNoteCopy");
        try
        {
            CheckStatus(Resolve<Ops_NSFDbGenerateOid>("NSFDbGenerateOID")(destinationDb, out var oid), "NSFDbGenerateOID");
            var oidPointer = Marshal.AllocHGlobal(28);
            var dbPointer = Marshal.AllocHGlobal(IntPtr.Size);
            try
            {
                Marshal.StructureToPtr(oid, oidPointer, false);
                if (IntPtr.Size == 8) Marshal.WriteInt64(dbPointer, destinationDb); else Marshal.WriteInt32(dbPointer, unchecked((int)destinationDb));
                Resolve<Ops_NSFNoteSetInfo>("NSFNoteSetInfo")(copy, OpsNoteMemberOid, oidPointer);
                Resolve<Ops_NSFNoteSetInfo>("NSFNoteSetInfo")(copy, OpsNoteMemberId, 0);
                Resolve<Ops_NSFNoteSetInfo>("NSFNoteSetInfo")(copy, OpsNoteMemberDb, dbPointer);
                CheckStatus(Resolve<Ops_NSFNoteUpdate>("NSFNoteUpdate")(copy, 1), "NSFNoteUpdate(copy)");
                return copy;
            }
            finally
            {
                Marshal.FreeHGlobal(dbPointer);
                Marshal.FreeHGlobal(oidPointer);
            }
        }
        catch
        {
            CloseNote(copy);
            throw;
        }
    }

    internal void CopyAllItems(nint sourceNote, nint destinationNote, bool replace)
    {
        EnsureInitialized();
        using var thread = EnterThread();
        foreach (var name in GetAllItemNames(sourceNote))
        {
            if (replace && HasItem(destinationNote, name)) DeleteItem(destinationNote, name);
            if (!replace && HasItem(destinationNote, name)) continue;
            CopyItemToDocument(sourceNote, name, destinationNote, name);
        }
    }

    internal void CopyItemToDocument(nint sourceNote, string sourceName, nint destinationNote, string destinationName)
    {
        EnsureInitialized();
        using var thread = EnterThread();
        var info = GetItemMetadata(sourceNote, sourceName);
        // Deleting a self-copy would invalidate the BLOCKID before copying it.
        if (sourceNote == destinationNote && string.Equals(sourceName, destinationName, StringComparison.OrdinalIgnoreCase)) return;
        if (HasItem(destinationNote, destinationName)) DeleteItem(destinationNote, destinationName);
        using var newName = ToLmbcs(destinationName);
        CheckStatus(Resolve<Ops_NSFItemCopyAndRename>("NSFItemCopyAndRename")(
            destinationNote, info.ItemBlock, newName.Pointer), "NSFItemCopyAndRename");
    }

    internal void PutDocumentInFolder(nint db, uint noteId, string folderName, bool createOnFail)
    {
        EnsureInitialized();
        using var thread = EnterThread();
        folderName = folderName.Trim();
        if (folderName.Length == 0) throw new ArgumentException("Folder name cannot be empty.", nameof(folderName));
        using var name = ToLmbcs(folderName);
        var status = Resolve<Ops_NIFFindDesignNote>("NIFFindDesignNote")(db, name.Pointer, OpsNoteClassView, out var folderNoteId);
        if (status != 0)
        {
            if (!createOnFail || (status & OpsErrorMask) != OpsErrorNotFound)
            {
                CheckStatus(status, "NIFFindDesignNote(folder)");
                return;
            }
            CheckStatus(Resolve<Ops_FolderCreate>("FolderCreate")(
                unchecked((uint)db), 0, 0, 0, name.Pointer, checked((ushort)name.Length), OpsDesignTypeShared, 0, out folderNoteId), "FolderCreate");
        }
        WithOpsSingleIdTable(noteId, table => CheckStatus(Resolve<Ops_FolderDocAdd>("FolderDocAdd")(
            unchecked((uint)db), 0, folderNoteId, table, 0), "FolderDocAdd"));
    }

    private void WithOpsSingleIdTable(uint noteId, Action<uint> action)
    {
        CheckStatus(Resolve<Ops_IDCreateTable>("IDCreateTable")(4, out var table), "IDCreateTable");
        try
        {
            CheckStatus(Resolve<Ops_IDInsert>("IDInsert")(table, noteId & 0x7fffffffu, 0), "IDInsert");
            action(table);
        }
        finally { _ = Resolve<Ops_IDDestroyTable>("IDDestroyTable")(table); }
    }

    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort Ops_NSFNoteDelete(nint database, uint noteId, ushort flags);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate void Ops_NSFNoteGetInfo(nint note, ushort member, nint value);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate void Ops_NSFNoteSetInfo(nint note, ushort member, nint value);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort Ops_NSFItemAppend(nint note, ushort flags, nint name, ushort nameLength, ushort dataType, nint value, uint valueLength);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort Ops_NSFNoteCopy(nint source, out nint destination);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort Ops_NSFDbGenerateOid(nint db, out OpsNotesOid oid);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort Ops_NSFNoteUpdate(nint note, ushort flags);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort Ops_NSFItemCopyAndRename(nint destinationNote, NotesBlockId sourceItemBlock, nint newName);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort Ops_NIFFindDesignNote(nint db, nint name, ushort noteClass, out uint noteId);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort Ops_FolderCreate(uint dataDb, uint folderDb, uint formatNoteId, uint formatDb, nint name, ushort nameLength, uint folderType, uint flags, out uint folderNoteId);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort Ops_FolderDocAdd(uint dataDb, uint folderDb, uint folderNoteId, uint idTable, uint flags);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort Ops_IDCreateTable(ushort alignment, out uint table);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort Ops_IDInsert(uint table, uint id, nint inserted);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort Ops_IDDestroyTable(uint table);
}
