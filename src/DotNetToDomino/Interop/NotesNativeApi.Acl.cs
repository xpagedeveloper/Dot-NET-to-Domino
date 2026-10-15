using System.Runtime.InteropServices;

namespace DotNetToDomino.Interop;

internal sealed record NotesAclEntryData(string Name, ushort Level, ushort Flags, byte[] Privileges);

internal sealed partial class NotesNativeApi
{
    internal void InitializeNewDatabaseAcl(nint database)
    {
        EnsureInitialized(); using var thread = EnterThread();
        CheckStatus(Resolve<Acl_Create>("ACLCreate")(out var acl), "ACLCreate");
        try
        {
            WriteAclEntry(acl, new(GetUserName(), 6, 0x0020, new byte[10]), true);
            CheckStatus(Resolve<Acl_Store>("NSFDbStoreACL")(database, acl, 0, 1), "NSFDbStoreACL(new)");
        }
        finally { FreeAcl(acl); }
    }
    internal uint ReadAcl(nint database)
    {
        EnsureInitialized(); using var thread = EnterThread();
        CheckStatus(Resolve<NSFDbReadACLDelegate>("NSFDbReadACL")(database, out var acl), "NSFDbReadACL");
        if (acl == 0) throw new NotesException("NSFDbReadACL returned no ACL.");
        return acl;
    }
    internal void SaveAcl(nint database, uint acl)
    {
        EnsureInitialized(); using var thread = EnterThread();
        CheckStatus(Resolve<Acl_Store>("NSFDbStoreACL")(database, acl, 0, 0), "NSFDbStoreACL");
    }
    internal void FreeAcl(uint acl)
    {
        if (acl == 0 || !IsInitialized) return;
        using var thread = EnterThread();
        CheckStatus(Resolve<OSMemFreeAccessDelegate>("OSMemFree")(acl), "OSMemFree(ACL)");
    }
    internal IReadOnlyList<NotesAclEntryData> EnumerateAcl(uint acl)
    {
        EnsureInitialized(); using var thread = EnterThread();
        var entries = new List<NotesAclEntryData>();
        Exception? callbackError = null;
        Acl_Callback callback = (_, name, level, privileges, flags) =>
        {
            try
            {
                var bytes = new byte[10];
                if (privileges != 0) Marshal.Copy(privileges, bytes, 0, bytes.Length);
                var text = name == 0 ? "-Default-" : FromLmbcsZeroTerminated(name, 2047);
                entries.Add(new(text.Length == 0 ? "-Default-" : text, level, flags, bytes));
            }
            catch (Exception error) { callbackError ??= error; }
        };
        CheckStatus(Resolve<Acl_Enum>("ACLEnumEntries")(acl, callback, 0), "ACLEnumEntries");
        GC.KeepAlive(callback);
        if (callbackError is not null) throw new NotesException("Unable to enumerate ACL entries.", callbackError);
        return entries;
    }
    internal void WriteAclEntry(uint acl, NotesAclEntryData entry, bool create, string? newName = null)
    {
        EnsureInitialized(); using var thread = EnterThread();
        using var name = ToLmbcs(entry.Name);
        using var replacement = ToLmbcs(newName ?? entry.Name);
        var defaultEntry = entry.Name.Equals("-Default-", StringComparison.OrdinalIgnoreCase);
        var privileges = Marshal.AllocHGlobal(10);
        try
        {
            Marshal.Copy(entry.Privileges, 0, privileges, 10);
            if (create) CheckStatus(Resolve<Acl_Add>("ACLAddEntry")(acl, name.Pointer, entry.Level, privileges, entry.Flags), "ACLAddEntry");
            else CheckStatus(Resolve<Acl_Update>("ACLUpdateEntry")(acl, defaultEntry ? 0 : name.Pointer,
                (ushort)(newName is null ? 0x0E : 0x0F), replacement.Pointer, entry.Level, privileges, entry.Flags), "ACLUpdateEntry");
        }
        finally { Marshal.FreeHGlobal(privileges); }
    }
    internal void DeleteAclEntry(uint acl, string entryName)
    {
        EnsureInitialized(); using var thread = EnterThread(); using var name = ToLmbcs(entryName);
        CheckStatus(Resolve<Acl_Delete>("ACLDeleteEntry")(acl, name.Pointer), "ACLDeleteEntry");
    }
    internal Dictionary<int, string> GetAclRoles(uint acl)
    {
        EnsureInitialized(); using var thread = EnterThread();
        var result = new Dictionary<int, string>();
        var buffer = Marshal.AllocHGlobal(2048);
        try
        {
            for (ushort i = 5; i < 80; i++)
            {
                Zero(buffer, 2048);
                var status = Resolve<Acl_PrivName>("ACLGetPrivName")(acl, i, buffer);
                if ((status & 0x3FFF) == 0x0424) continue; // Unassigned privilege slot.
                CheckStatus(status, "ACLGetPrivName");
                var role = FromLmbcsZeroTerminated(buffer, 2047);
                if (role.Length > 0) result.Add(i, role);
            }
        }
        finally { Marshal.FreeHGlobal(buffer); }
        return result;
    }
    internal void SetAclRole(uint acl, int index, string role)
    {
        EnsureInitialized(); using var thread = EnterThread(); using var name = ToLmbcs(role);
        if (name.Length >= 16) throw new ArgumentException("ACL role names must fit in 15 LMBCS bytes.", nameof(role));
        CheckStatus(Resolve<Acl_PrivName>("ACLSetPrivName")(acl, checked((ushort)index), name.Pointer), "ACLSetPrivName");
    }
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate void Acl_Callback(nint context, nint name, ushort level, nint privileges, ushort flags);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort Acl_Enum(uint acl, Acl_Callback callback, nint context);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort Acl_Store(nint database, uint acl, uint objectId, ushort method);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort Acl_Create(out uint acl);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort Acl_Add(uint acl, nint name, ushort level, nint privileges, ushort flags);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort Acl_Update(uint acl, nint name, ushort updates, nint newName, ushort level, nint privileges, ushort flags);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort Acl_Delete(uint acl, nint name);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort Acl_PrivName(uint acl, ushort index, nint name);
}
