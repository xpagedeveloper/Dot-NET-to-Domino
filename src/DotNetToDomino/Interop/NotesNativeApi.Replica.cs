using System.Runtime.InteropServices;

namespace DotNetToDomino.Interop;

internal readonly record struct ReplicationHistoryData(NotesTimeDate ReplicationTime, string ServerName, string FileName, NotesReplicationDirection Direction, ushort AccessLevel, ushort AccessFlags);

internal sealed partial class NotesNativeApi
{
    private const ushort ReplicaNoteClassAll = 0x7fff;
    private const ushort ReplicaNoteClassAllNonData = 0x7ffe;

    internal void SetDatabaseReplicaId(nint db, string replicaId)
    {
        EnsureInitialized();
        using var thread = EnterThread();
        var id = ParseReplicaIdValue(replicaId);
        const int replicaInfoSize = 32;
        var buffer = Marshal.AllocHGlobal(replicaInfoSize);
        try
        {
            Zero(buffer, replicaInfoSize);
            CheckStatus(Resolve<Replica_NSFDbReplicaInfoGet>("NSFDbReplicaInfoGet")(db, buffer), "NSFDbReplicaInfoGet");
            Marshal.WriteInt32(buffer, 0, unchecked((int)id.Innards0));
            Marshal.WriteInt32(buffer, 4, unchecked((int)id.Innards1));
            CheckStatus(Resolve<Replica_NSFDbReplicaInfoSet>("NSFDbReplicaInfoSet")(db, buffer), "NSFDbReplicaInfoSet");
        }
        finally { Marshal.FreeHGlobal(buffer); }
    }

    internal void ClearDatabaseReplicationHistory(nint db)
    {
        EnsureInitialized();
        using var thread = EnterThread();
        CheckStatus(Resolve<Replica_NSFDbClearReplHistory>("NSFDbClearReplHistory")(db, 0), "NSFDbClearReplHistory");
    }

    internal IReadOnlyList<ReplicationHistoryData> GetDatabaseReplicationHistory(nint db)
    {
        EnsureInitialized(); using var thread = EnterThread(); uint handle = 0;
        try
        {
            CheckStatus(Resolve<Replica_NSFDbGetReplHistorySummary>("NSFDbGetReplHistorySummary")(db, 0, out handle, out var count), "NSFDbGetReplHistorySummary");
            if (handle == 0 || count == 0) return [];
            CheckStatus(Resolve<Replica_OSMemGetSize>("OSMemGetSize")(handle, out var size), "OSMemGetSize(replication history)");
            var pointer = Resolve<Replica_OSLockObject>("OSLockObject")(handle); if (pointer == 0) throw new NotesException("Unable to lock replication history.");
            try
            {
                const int entrySize = 32; var result = new List<ReplicationHistoryData>(checked((int)count));
                for (var i = 0; i < count && (i + 1) * entrySize <= size; i++)
                {
                    var entry = pointer + (nint)(i * entrySize);
                    var time = new NotesTimeDate { Innards0 = unchecked((uint)Marshal.ReadInt32(entry, 0)), Innards1 = unchecked((uint)Marshal.ReadInt32(entry, 4)) };
                    var packed = pointer + (nint)Marshal.ReadInt32(entry, 16); var serverLength = Marshal.ReadInt16(entry, 20); var fileLength = Marshal.ReadInt16(entry, 22);
                    result.Add(new ReplicationHistoryData(time, FromLmbcs(packed, serverLength), FromLmbcsZeroTerminated(packed + serverLength + 2, fileLength + 1), (NotesReplicationDirection)Marshal.ReadInt16(entry, 12), unchecked((ushort)Marshal.ReadInt16(entry, 8)), unchecked((ushort)Marshal.ReadInt16(entry, 10))));
                }
                return result;
            }
            finally { Resolve<Replica_OSUnlockObject>("OSUnlockObject")(handle); }
        }
        finally { if (handle != 0) CheckStatus(Resolve<Replica_OSMemFree>("OSMemFree")(handle), "OSMemFree(replication history)"); }
    }

    internal nint CreateDatabaseCopy(string sourceServer, string sourceFile, string destinationServer, string destinationFile, bool includeDocuments, bool replica = false)
    {
        EnsureInitialized();
        using var thread = EnterThread();
        using var sourceFileText = ToLmbcs(sourceFile);
        using var sourceServerText = ToLmbcs(sourceServer);
        using var destinationFileText = ToLmbcs(destinationFile);
        using var destinationServerText = ToLmbcs(destinationServer);
        nint sourceNetworkPath = 0;
        nint destinationNetworkPath = 0;
        try
        {
            var sourcePath = sourceFileText.Pointer;
            if (sourceServer.Length > 0)
            {
                sourceNetworkPath = Marshal.AllocHGlobal(4096);
                Zero(sourceNetworkPath, 4096);
                CheckStatus(Resolve<Replica_OSPathNetConstruct>("OSPathNetConstruct")(
                    0, sourceServerText.Pointer, sourceFileText.Pointer, sourceNetworkPath), "OSPathNetConstruct(source)");
                sourcePath = sourceNetworkPath;
            }

            var destinationPath = destinationFileText.Pointer;
            if (destinationServer.Length > 0)
            {
                destinationNetworkPath = Marshal.AllocHGlobal(4096);
                Zero(destinationNetworkPath, 4096);
                CheckStatus(Resolve<Replica_OSPathNetConstruct>("OSPathNetConstruct")(
                    0, destinationServerText.Pointer, destinationFileText.Pointer, destinationNetworkPath), "OSPathNetConstruct(destination)");
                destinationPath = destinationNetworkPath;
            }

            var noteClass = includeDocuments ? ReplicaNoteClassAll : ReplicaNoteClassAllNonData;
            const uint DbCopyReplica = 0x00000001;
            CheckStatus(Resolve<Replica_NSFDbCreateAndCopy>("NSFDbCreateAndCopy")(
                sourcePath, destinationPath, noteClass, 0, replica ? DbCopyReplica : 0, out var newDb), "NSFDbCreateAndCopy");
            return unchecked((nint)newDb);
        }
        finally
        {
            if (destinationNetworkPath != 0) Marshal.FreeHGlobal(destinationNetworkPath);
            if (sourceNetworkPath != 0) Marshal.FreeHGlobal(sourceNetworkPath);
        }
    }

    private static NotesTimeDate ParseReplicaIdValue(string text)
    {
        text = (text ?? string.Empty).Replace("-", string.Empty, StringComparison.Ordinal).Replace(":", string.Empty, StringComparison.Ordinal).Trim();
        if (text.Length != 16 || text.Any(c => !Uri.IsHexDigit(c))) throw new ArgumentException("Notes replica ID must contain exactly 16 hexadecimal characters.", nameof(text));
        return new NotesTimeDate
        {
            Innards1 = uint.Parse(text[..8], System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture),
            Innards0 = uint.Parse(text[8..], System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture)
        };
    }

    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort Replica_NSFDbReplicaInfoGet(nint db, nint replicaInfo);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort Replica_NSFDbReplicaInfoSet(nint db, nint replicaInfo);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort Replica_NSFDbClearReplHistory(nint db, ushort flags);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort Replica_NSFDbGetReplHistorySummary(nint db, uint flags, out uint history, out uint count);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate nint Replica_OSLockObject(uint handle);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate void Replica_OSUnlockObject(uint handle);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort Replica_OSMemGetSize(uint handle, out uint size);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort Replica_OSMemFree(uint handle);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort Replica_NSFDbCreateAndCopy(nint sourceDb, nint destinationDb, ushort noteClass, ushort limit, uint flags, out uint returnHandle);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort Replica_OSPathNetConstruct(nint portName, nint serverName, nint fileName, nint pathName);
}
