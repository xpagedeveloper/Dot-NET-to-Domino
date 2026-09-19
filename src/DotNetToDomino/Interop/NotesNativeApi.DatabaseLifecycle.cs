using System.Runtime.InteropServices;

namespace DotNetToDomino.Interop;

internal sealed partial class NotesNativeApi
{
    internal void CreateDatabase(string server, string filePath)
    {
        EnsureInitialized();
        using var thread = EnterThread();
        using var file = ToLmbcs(filePath);
        using var serverName = ToLmbcs(server);
        var networkPath = Marshal.AllocHGlobal(4096);
        try
        {
            Zero(networkPath, 4096);
            CheckStatus(Resolve<OSPathNetConstructDelegate>("OSPathNetConstruct")(0, serverName.Pointer, file.Pointer, networkPath), "OSPathNetConstruct(create database)");
            CheckStatus(Resolve<DbLife_NSFDbCreate>("NSFDbCreate")(networkPath, unchecked((ushort)0xFF01), 0), "NSFDbCreate");
        }
        finally { Marshal.FreeHGlobal(networkPath); }
    }

    internal void DeleteDatabase(string server, string filePath)
    {
        EnsureInitialized();
        using var thread = EnterThread();
        using var file = ToLmbcs(filePath);
        using var serverName = ToLmbcs(server);
        var networkPath = Marshal.AllocHGlobal(4096);
        try
        {
            Zero(networkPath, 4096);
            CheckStatus(Resolve<OSPathNetConstructDelegate>("OSPathNetConstruct")(0, serverName.Pointer, file.Pointer, networkPath), "OSPathNetConstruct(delete database)");
            CheckStatus(Resolve<DbLife_NSFDbDelete>("NSFDbDelete")(networkPath), "NSFDbDelete");
        }
        finally { Marshal.FreeHGlobal(networkPath); }
    }

    internal nint CreateNote(nint db)
    {
        EnsureInitialized();
        using var thread = EnterThread();
        CheckStatus(Resolve<DbLife_NSFNoteCreate>("NSFNoteCreate")(db, out var note), "NSFNoteCreate");
        return note;
    }

    internal nint OpenProfile(nint db, string profileName, string profileKey)
    {
        EnsureInitialized();
        using var thread = EnterThread();
        using var name = ToLmbcs(profileName);
        using var key = ToLmbcs(profileKey);
        var keyPointer = profileKey.Length == 0 ? 0 : key.Pointer;
        var keyLength = profileKey.Length == 0 ? (ushort)0 : checked((ushort)Math.Min(key.Length, ushort.MaxValue));
        CheckStatus(Resolve<DbLife_NSFProfileOpen>("NSFProfileOpen")(
            db,
            name.Pointer,
            checked((ushort)Math.Min(name.Length, ushort.MaxValue)),
            keyPointer,
            keyLength,
            1,
            out var note), "NSFProfileOpen");
        return note;
    }

    internal NotesTimeDate GetDatabaseCreated(nint db)
    {
        EnsureInitialized();
        using var thread = EnterThread();
        CheckStatus(Resolve<DbLife_NSFDbIDGet>("NSFDbIDGet")(db, out var created), "NSFDbIDGet");
        return created;
    }

    internal NotesTimeDate GetDatabaseLastModified(nint db)
    {
        EnsureInitialized();
        using var thread = EnterThread();
        CheckStatus(Resolve<DbLife_NSFDbModifiedTime>("NSFDbModifiedTime")(db, out var dataModified, out var nonDataModified), "NSFDbModifiedTime");
        return Resolve<DbLife_TimeDateCollate>("TimeDateCollate")(ref dataModified, ref nonDataModified) >= 0 ? dataModified : nonDataModified;
    }

    internal int GetDatabaseFileFormat(nint db)
    {
        EnsureInitialized();
        using var thread = EnterThread();
        CheckStatus(Resolve<DbLife_NSFDbMajorMinorVersionGet>("NSFDbMajorMinorVersionGet")(db, out var major, out _), "NSFDbMajorMinorVersionGet");
        return major;
    }

    internal NotesTimeDate? GetDatabaseLastFullTextIndexed(nint db)
    {
        EnsureInitialized();
        using var thread = EnterThread();
        var status = Resolve<DbLife_FTGetLastIndexTime>("FTGetLastIndexTime")(db, out var indexed);
        if ((status & 0x3FFF) == 0x0F02) return null;
        CheckStatus(status, "FTGetLastIndexTime");
        return indexed;
    }

    internal bool IsDatabaseFullTextIndexed(nint db) => GetDatabaseLastFullTextIndexed(db).HasValue;

    internal void DeleteFullTextIndex(nint db)
    {
        EnsureInitialized();
        using var thread = EnterThread();
        CheckStatus(Resolve<DbLife_FTDeleteIndex>("FTDeleteIndex")(db), "FTDeleteIndex");
    }

    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort DbLife_NSFDbCreate(nint pathName, ushort dbClass, int forceCreation);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort DbLife_NSFDbDelete(nint pathName);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort DbLife_NSFNoteCreate(nint db, out nint note);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort DbLife_NSFProfileOpen(nint db, nint profileName, ushort profileNameLength, nint userName, ushort userNameLength, int copyProfile, out nint note);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort DbLife_NSFDbIDGet(nint db, out NotesTimeDate databaseId);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort DbLife_NSFDbModifiedTime(nint db, out NotesTimeDate dataModified, out NotesTimeDate nonDataModified);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort DbLife_NSFDbMajorMinorVersionGet(nint db, out ushort majorVersion, out ushort minorVersion);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate int DbLife_TimeDateCollate(ref NotesTimeDate first, ref NotesTimeDate second);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort DbLife_FTGetLastIndexTime(nint db, out NotesTimeDate indexed);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort DbLife_FTDeleteIndex(nint db);
}
