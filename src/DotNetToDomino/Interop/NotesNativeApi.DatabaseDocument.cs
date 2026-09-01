using System.Globalization;
using System.Runtime.InteropServices;

namespace DotNetToDomino.Interop;

internal sealed partial class NotesNativeApi
{
    private const int NsfInfoSize = 128;
    private const ushort InfoParseTitle = 0;
    private const ushort InfoParseCategories = 1;
    private const ushort InfoParseClass = 2;
    private const ushort InfoParseDesignClass = 3;
    private const ushort NoteMemberId = 1;
    private const ushort NoteMemberOid = 2;

    [StructLayout(LayoutKind.Sequential)]
    private struct NotesTimeDate
    {
        public uint Innards0;
        public uint Innards1;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NotesUnid
    {
        public NotesTimeDate File;
        public NotesTimeDate Note;
    }

    internal string GetDatabaseTitle(nint db) => GetDatabaseInfoPart(db, InfoParseTitle);
    internal string GetDatabaseCategories(nint db) => GetDatabaseInfoPart(db, InfoParseCategories);
    internal string GetDatabaseTemplateName(nint db) => GetDatabaseInfoPart(db, InfoParseClass);
    internal string GetDatabaseDesignTemplateName(nint db) => GetDatabaseInfoPart(db, InfoParseDesignClass);

    internal void SetDatabaseTitle(nint db, string value) => SetDatabaseInfoPart(db, InfoParseTitle, value);
    internal void SetDatabaseCategories(nint db, string value) => SetDatabaseInfoPart(db, InfoParseCategories, value);

    internal string GetDatabaseReplicaId(nint db)
    {
        EnsureInitialized();
        using var thread = EnterThread();
        var buffer = Marshal.AllocHGlobal(32);
        try
        {
            Zero(buffer, 32);
            CheckStatus(Resolve<NSFDbReplicaInfoGetDelegate>("NSFDbReplicaInfoGet")(db, buffer), "NSFDbReplicaInfoGet");
            var innards0 = unchecked((uint)Marshal.ReadInt32(buffer, 0));
            var innards1 = unchecked((uint)Marshal.ReadInt32(buffer, 4));
            return innards1.ToString("X8", CultureInfo.InvariantCulture) + innards0.ToString("X8", CultureInfo.InvariantCulture);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    internal string LocateDatabaseByReplicaId(string server, string replicaId)
    {
        EnsureInitialized();
        using var thread = EnterThread();
        var id = ParseReplicaId(replicaId);
        nint db = 0;
        try
        {
            db = OpenDatabase(server, string.Empty);
            const int capacity = 4096;
            var output = Marshal.AllocHGlobal(capacity);
            try
            {
                Zero(output, capacity);
                CheckStatus(
                    Resolve<NSFDbLocateByReplicaIDDelegate>("NSFDbLocateByReplicaID")(
                        db,
                        ref id,
                        output,
                        checked((ushort)(capacity - 1))),
                    "NSFDbLocateByReplicaID");
                return FromLmbcsZeroTerminated(output, capacity - 1);
            }
            finally
            {
                Marshal.FreeHGlobal(output);
            }
        }
        finally
        {
            if (db != 0)
                CloseDatabase(db);
        }
    }

    internal (long Size, double PercentUsed) GetDatabaseSpaceUsage(nint db)
    {
        EnsureInitialized();
        using var thread = EnterThread();
        CheckStatus(Resolve<NSFDbSpaceUsageDelegate>("NSFDbSpaceUsage")(db, out var allocated, out var free), "NSFDbSpaceUsage");
        var total = (long)allocated + free;
        var percent = total == 0 ? 0d : allocated * 100d / total;
        return (total, percent);
    }

    internal int GetDatabaseCurrentAccessLevel(nint db)
    {
        EnsureInitialized();
        using var thread = EnterThread();
        Resolve<NSFDbAccessGetDelegate>("NSFDbAccessGet")(db, out var level, out _);
        return level;
    }

    internal nint TryOpenNote(nint db, uint noteId)
    {
        EnsureInitialized();
        using var thread = EnterThread();
        var status = Resolve<NSFNoteOpenDelegate>("NSFNoteOpen")(db, noteId, 0x1000, out var note);
        if (status == 0)
            return note;
        if (IsMissingNoteStatus(status))
            return 0;
        CheckStatus(status, "NSFNoteOpen");
        return 0;
    }

    internal nint TryOpenNoteByUnid(nint db, string unidText)
    {
        EnsureInitialized();
        using var thread = EnterThread();
        var unid = ParseUnid(unidText);
        var status = Resolve<NSFNoteOpenByUnidDelegate>("NSFNoteOpenByUNID")(db, ref unid, 0x1000, out var note);
        if (status == 0)
            return note;
        if (IsMissingNoteStatus(status))
            return 0;
        CheckStatus(status, "NSFNoteOpenByUNID");
        return 0;
    }

    internal void CloseNote(nint note)
    {
        if (note == 0 || !IsInitialized)
            return;
        using var thread = EnterThread();
        CheckStatus(Resolve<NSFNoteCloseDelegate>("NSFNoteClose")(note), "NSFNoteClose");
    }

    internal uint GetNoteId(nint note)
    {
        EnsureInitialized();
        using var thread = EnterThread();
        var pointer = Marshal.AllocHGlobal(sizeof(uint));
        try
        {
            Resolve<NSFNoteGetInfoDelegate>("NSFNoteGetInfo")(note, NoteMemberId, pointer);
            return unchecked((uint)Marshal.ReadInt32(pointer));
        }
        finally
        {
            Marshal.FreeHGlobal(pointer);
        }
    }

    internal string GetUnid(nint note)
    {
        EnsureInitialized();
        using var thread = EnterThread();
        const int oidSize = 28;
        var pointer = Marshal.AllocHGlobal(oidSize);
        try
        {
            Zero(pointer, oidSize);
            Resolve<NSFNoteGetInfoDelegate>("NSFNoteGetInfo")(note, NoteMemberOid, pointer);
            var file = Marshal.PtrToStructure<NotesTimeDate>(pointer);
            var noteTime = Marshal.PtrToStructure<NotesTimeDate>(nint.Add(pointer, 8));
            return TimeDateHex(file) + TimeDateHex(noteTime);
        }
        finally
        {
            Marshal.FreeHGlobal(pointer);
        }
    }

    private string GetDatabaseInfoPart(nint db, ushort what)
    {
        EnsureInitialized();
        using var thread = EnterThread();
        var info = Marshal.AllocHGlobal(NsfInfoSize);
        var output = Marshal.AllocHGlobal(NsfInfoSize);
        try
        {
            Zero(info, NsfInfoSize);
            Zero(output, NsfInfoSize);
            CheckStatus(Resolve<NSFDbInfoGetDelegate>("NSFDbInfoGet")(db, info), "NSFDbInfoGet");
            Resolve<NSFDbInfoParseDelegate>("NSFDbInfoParse")(info, what, output, checked((ushort)(NsfInfoSize - 1)));
            return FromLmbcsZeroTerminated(output, NsfInfoSize - 1);
        }
        finally
        {
            Marshal.FreeHGlobal(output);
            Marshal.FreeHGlobal(info);
        }
    }

    private void SetDatabaseInfoPart(nint db, ushort what, string value)
    {
        EnsureInitialized();
        using var thread = EnterThread();
        var info = Marshal.AllocHGlobal(NsfInfoSize);
        try
        {
            Zero(info, NsfInfoSize);
            CheckStatus(Resolve<NSFDbInfoGetDelegate>("NSFDbInfoGet")(db, info), "NSFDbInfoGet");
            using var text = ToLmbcs(value ?? string.Empty);
            Resolve<NSFDbInfoModifyDelegate>("NSFDbInfoModify")(info, what, text.Pointer);
            CheckStatus(Resolve<NSFDbInfoSetDelegate>("NSFDbInfoSet")(db, info), "NSFDbInfoSet");
        }
        finally
        {
            Marshal.FreeHGlobal(info);
        }
    }

    private bool IsMissingNoteStatus(ushort status)
    {
        var message = TryLoadStatusText(status);
        return message.Contains("not found", StringComparison.OrdinalIgnoreCase) ||
               message.Contains("does not exist", StringComparison.OrdinalIgnoreCase) ||
               message.Contains("deleted", StringComparison.OrdinalIgnoreCase) ||
               message.Contains("invalid note", StringComparison.OrdinalIgnoreCase);
    }

    private static NotesTimeDate ParseReplicaId(string text)
    {
        text = (text ?? string.Empty).Replace("-", string.Empty, StringComparison.Ordinal).Replace(":", string.Empty, StringComparison.Ordinal).Trim();
        if (text.Length != 16 || text.Any(c => !Uri.IsHexDigit(c)))
            throw new ArgumentException("Notes replica ID must contain exactly 16 hexadecimal characters.", nameof(text));

        return new NotesTimeDate
        {
            Innards1 = uint.Parse(text[..8], NumberStyles.HexNumber, CultureInfo.InvariantCulture),
            Innards0 = uint.Parse(text[8..], NumberStyles.HexNumber, CultureInfo.InvariantCulture)
        };
    }

    private static NotesUnid ParseUnid(string text)
    {
        text = (text ?? string.Empty).Replace("-", string.Empty, StringComparison.Ordinal).Replace(":", string.Empty, StringComparison.Ordinal).Trim();
        if (text.Length != 32 || text.Any(c => !Uri.IsHexDigit(c)))
            throw new ArgumentException("Notes UNID must contain exactly 32 hexadecimal characters.", nameof(text));

        return new NotesUnid
        {
            File = ParseTimeDateHex(text[..16]),
            Note = ParseTimeDateHex(text[16..])
        };
    }

    private static NotesTimeDate ParseTimeDateHex(string text) => new()
    {
        Innards1 = uint.Parse(text[..8], NumberStyles.HexNumber, CultureInfo.InvariantCulture),
        Innards0 = uint.Parse(text[8..], NumberStyles.HexNumber, CultureInfo.InvariantCulture)
    };

    private static string TimeDateHex(NotesTimeDate value) =>
        value.Innards1.ToString("X8", CultureInfo.InvariantCulture) + value.Innards0.ToString("X8", CultureInfo.InvariantCulture);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort NSFDbInfoGetDelegate(nint db, nint buffer);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate void NSFDbInfoParseDelegate(nint info, ushort what, nint buffer, ushort length);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate void NSFDbInfoModifyDelegate(nint info, ushort what, nint buffer);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort NSFDbInfoSetDelegate(nint db, nint buffer);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort NSFDbReplicaInfoGetDelegate(nint db, nint replicaInfo);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort NSFDbLocateByReplicaIDDelegate(nint db, ref NotesTimeDate replicaId, nint pathName, ushort pathNameSize);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort NSFDbSpaceUsageDelegate(nint db, out uint allocatedBytes, out uint freeBytes);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate void NSFDbAccessGetDelegate(nint db, out ushort accessLevel, out ushort accessFlags);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort NSFNoteOpenDelegate(nint db, uint noteId, ushort flags, out nint note);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort NSFNoteOpenByUnidDelegate(nint db, ref NotesUnid unid, ushort flags, out nint note);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort NSFNoteCloseDelegate(nint note);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate void NSFNoteGetInfoDelegate(nint note, ushort member, nint value);
}
