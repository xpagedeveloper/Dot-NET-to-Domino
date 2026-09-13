using System.Runtime.InteropServices;

namespace DotNetToDomino.Interop;

internal sealed record NotesDatabaseAccessData(
    string UserName,
    int Level,
    string LevelName,
    int AccessFlags,
    string[] NamesList,
    string[] Groups,
    string[] Roles);

internal sealed partial class NotesNativeApi
{
    private const int PasswordDigestCapacity = 2048;

    internal string HashPassword(string password)
    {
        EnsureInitialized();
        using var thread = EnterThread();
        using var passwordText = ToLmbcs(password ?? string.Empty);
        if (passwordText.Length > ushort.MaxValue) throw new NotesException(5, "Password is too long for NotesSession.HashPassword.");
        var digest = Marshal.AllocHGlobal(PasswordDigestCapacity);
        try
        {
            Zero(digest, PasswordDigestCapacity);
            CheckStatus(Resolve<SECHashPasswordDelegate>("SECHashPassword")(
                checked((ushort)passwordText.Length), passwordText.Pointer,
                checked((ushort)PasswordDigestCapacity), out var length,
                digest, 0, 0), "SECHashPassword");
            if (length >= PasswordDigestCapacity) throw new NotesException(5, "SECHashPassword returned an invalid digest length.");
            return FromLmbcs(digest, length);
        }
        finally { Zero(passwordText.Pointer, passwordText.Length + 1); Zero(digest, PasswordDigestCapacity); Marshal.FreeHGlobal(digest); }
    }

    internal bool VerifyPassword(string password, string digest)
    {
        EnsureInitialized();
        using var thread = EnterThread();
        using var passwordText = ToLmbcs(password ?? string.Empty);
        using var digestText = ToLmbcs(digest ?? string.Empty);
        if (passwordText.Length > ushort.MaxValue || digestText.Length > ushort.MaxValue) return false;
        try { return Resolve<SECVerifyPasswordDelegate>("SECVerifyPassword")(checked((ushort)passwordText.Length), passwordText.Pointer, checked((ushort)digestText.Length), digestText.Pointer, 0, 0) == 0; }
        finally { Zero(passwordText.Pointer, passwordText.Length + 1); }
    }

    internal void SetEnvironmentVariable(string name, string value)
    {
        EnsureInitialized(); using var thread = EnterThread(); using var n = ToLmbcs(name); using var v = ToLmbcs(value);
        Resolve<OSSetEnvironmentVariableDelegate>("OSSetEnvironmentVariable")(n.Pointer, v.Pointer);
    }

    internal (string Server, string FilePath)[] GetAddressBooks()
    {
        EnsureInitialized(); using var thread = EnterThread();
        CheckStatus(Resolve<NAMEGetAddressBooksDelegate>("NAMEGetAddressBooks")(0, 0, out var count, out var length, out var handle), "NAMEGetAddressBooks");
        if (count == 0 || handle == 0 || length == 0) { if (handle != 0) Resolve<OSMemFreeAccessDelegate>("OSMemFree")(handle); return []; }
        try
        {
            var pointer = Resolve<OSLockObjectAccessDelegate>("OSLockObject")(handle);
            if (pointer == 0) throw new NotesException("Unable to lock Notes address book list.");
            try
            {
                var result = new List<(string, string)>(count); var offset = 0;
                for (var i = 0; i < count && offset < length; i++)
                {
                    var remaining = length - offset; var byteLength = 0;
                    while (byteLength < remaining && Marshal.ReadByte(pointer, offset + byteLength) != 0) byteLength++;
                    var path = FromLmbcs(pointer + offset, byteLength); offset += Math.Min(remaining, byteLength + 1);
                    if (path.Length == 0) continue;
                    using var pathText = ToLmbcs(path); const int capacity = 4096;
                    var server = Marshal.AllocHGlobal(capacity); var file = Marshal.AllocHGlobal(capacity);
                    try { Zero(server, capacity); Zero(file, capacity); CheckStatus(Resolve<OSPathNetParseAccessDelegate>("OSPathNetParse")(pathText.Pointer, 0, server, file), "OSPathNetParse(address book)"); result.Add((FromLmbcsZeroTerminated(server, capacity - 1), FromLmbcsZeroTerminated(file, capacity - 1))); }
                    finally { Marshal.FreeHGlobal(server); Marshal.FreeHGlobal(file); }
                }
                return result.ToArray();
            }
            finally { Resolve<OSUnlockObjectAccessDelegate>("OSUnlockObject")(handle); }
        }
        finally { Resolve<OSMemFreeAccessDelegate>("OSMemFree")(handle); }
    }

    internal NotesDatabaseAccessData QueryDatabaseAccess(nint db, string userName)
    {
        EnsureInitialized(); using var thread = EnterThread(); userName = userName.Trim();
        if (userName.Length == 0) throw new NotesException(5, "NotesDatabase.QueryAccess user name cannot be empty.");
        using var name = ToLmbcs(userName); nint namesHandle = 0, aclHandle = 0, rolesHandle = 0, namesPointer = 0;
        var privileges = Marshal.AllocHGlobal(10);
        try
        {
            Zero(privileges, 10);
            CheckStatus(Resolve<NSFBuildNamesListDelegate>("NSFBuildNamesList")(name.Pointer, 0, out namesHandle), "NSFBuildNamesList");
            if (namesHandle == 0) throw new NotesException(5, "NSFBuildNamesList returned an empty names list.");

            namesPointer = Resolve<OSLockObjectAccessDelegate>("OSLockObject")(namesHandle);
            if (namesPointer == 0) throw new NotesException(5, "Unable to lock the Domino names list.");
            var names = ReadNamesList(namesHandle, namesPointer);
            if (names.Length == 0) throw new NotesException(5, "The Domino names list did not contain a user name.");

            CheckStatus(Resolve<NSFDbReadACLDelegate>("NSFDbReadACL")(db, out aclHandle), "NSFDbReadACL");
            if (aclHandle == 0) throw new NotesException(5, "NSFDbReadACL returned an empty ACL handle.");

            CheckStatus(Resolve<ACLLookupAccessDelegate>("ACLLookupAccess")(aclHandle, namesPointer, out var level, privileges, out var flags, out rolesHandle), "ACLLookupAccess");
            var roles = ReadTextList(rolesHandle);
            return new(names[0], level, AccessLevelName(level), flags, names, names.Length <= 1 ? [] : names[1..], roles);
        }
        finally
        {
            Marshal.FreeHGlobal(privileges);
            if (rolesHandle != 0) Resolve<OSMemFreeAccessDelegate>("OSMemFree")(rolesHandle);
            if (aclHandle != 0) Resolve<OSMemFreeAccessDelegate>("OSMemFree")(aclHandle);
            if (namesPointer != 0 && namesHandle != 0) Resolve<OSUnlockObjectAccessDelegate>("OSUnlockObject")(namesHandle);
            if (namesHandle != 0) Resolve<OSMemFreeAccessDelegate>("OSMemFree")(namesHandle);
        }
    }

    private string[] ReadNamesList(nint handle, nint pointer)
    {
        // NAMES_LIST is packed: WORD NumNames + 8-byte obsolete LICENSEID + DWORD Authenticated.
        const int headerSize = 14;
        var size = Resolve<OSMemGetSizeDelegate>("OSMemGetSize")(handle);
        if (size < headerSize) throw new NotesException(5, "Domino returned a truncated NAMES_LIST.");
        var count = unchecked((ushort)Marshal.ReadInt16(pointer));
        var result = new List<string>(count);
        var offset = headerSize;
        for (var i = 0; i < count; i++)
        {
            if ((uint)offset >= size) throw new NotesException(5, "Domino returned a truncated NAMES_LIST entry.");
            var max = checked((int)Math.Min(size - (uint)offset, int.MaxValue));
            var len = 0;
            while (len < max && Marshal.ReadByte(pointer, offset + len) != 0) len++;
            if (len == max) throw new NotesException(5, "Domino returned an unterminated NAMES_LIST entry.");
            result.Add(FromLmbcs(pointer + offset, len));
            offset += len + 1;
        }
        return result.ToArray();
    }

    private string[] ReadTextList(nint handle)
    {
        if (handle == 0) return [];
        var pointer = Resolve<OSLockObjectAccessDelegate>("OSLockObject")(handle);
        if (pointer == 0) throw new NotesException(5, "Unable to lock the Domino ACL role list.");
        try
        {
            var count = unchecked((ushort)Marshal.ReadInt16(pointer));
            var result = new List<string>(count);
            for (ushort i = 0; i < count; i++)
            {
                CheckStatus(Resolve<ListGetTextDelegate>("ListGetText")(pointer, 0, i, out var text, out var length), "ListGetText(ACL roles)");
                result.Add(length == 0 ? string.Empty : FromLmbcs(text, length));
            }
            return result.Where(value => value.Length != 0).ToArray();
        }
        finally { Resolve<OSUnlockObjectAccessDelegate>("OSUnlockObject")(handle); }
    }

    private static string AccessLevelName(ushort level) => level switch { 0 => "NoAccess", 1 => "Depositor", 2 => "Reader", 3 => "Author", 4 => "Editor", 5 => "Designer", 6 => "Manager", _ => "Unknown" };

    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort SECHashPasswordDelegate(ushort passwordLength, nint password, ushort maximumDigestLength, out ushort digestLength, nint digest, uint flags, nint reserved);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort SECVerifyPasswordDelegate(ushort passwordLength, nint password, ushort digestLength, nint digest, uint flags, nint reserved);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate void OSSetEnvironmentVariableDelegate(nint name, nint value);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort NAMEGetAddressBooksDelegate(nint server, ushort options, out ushort count, out ushort length, out nint handle);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate nint OSLockObjectAccessDelegate(nint handle);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate void OSUnlockObjectAccessDelegate(nint handle);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort OSMemFreeAccessDelegate(nint handle);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort OSPathNetParseAccessDelegate(nint path, nint port, nint server, nint file);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort NSFBuildNamesListDelegate(nint userName, uint flags, out nint namesList);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort NSFDbReadACLDelegate(nint db, out nint acl);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort ACLLookupAccessDelegate(nint acl, nint namesList, out ushort level, nint privileges, out ushort flags, out nint roles);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate uint OSMemGetSizeDelegate(nint handle);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort ListGetTextDelegate(nint list, int prefixDataType, ushort entry, out nint text, out ushort length);
}