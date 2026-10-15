using System.Runtime.InteropServices;

namespace DotNetToDomino.Interop;

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
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort Replica_NSFDbCreateAndCopy(nint sourceDb, nint destinationDb, ushort noteClass, ushort limit, uint flags, out uint returnHandle);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort Replica_OSPathNetConstruct(nint portName, nint serverName, nint fileName, nint pathName);
}
