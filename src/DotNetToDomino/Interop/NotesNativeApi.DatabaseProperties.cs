using System.Runtime.InteropServices;
using System.Xml;
using System.Xml.Linq;

namespace DotNetToDomino.Interop;

internal sealed partial class NotesNativeApi
{
    internal bool GetDatabaseOption(nint database, int bit)
    {
        EnsureInitialized(); using var thread = EnterThread();
        var buffer = Marshal.AllocHGlobal(16);
        try { Zero(buffer, 16); CheckStatus(Resolve<DbProps_OptionsGet>("NSFDbGetOptionsExt")(database, buffer), "NSFDbGetOptionsExt"); return (Marshal.ReadByte(buffer, bit / 8) & (1 << (bit % 8))) != 0; }
        finally { Marshal.FreeHGlobal(buffer); }
    }
    internal void SetDatabaseOption(nint database, int bit, bool enabled)
    {
        EnsureInitialized(); using var thread = EnterThread();
        var values = Marshal.AllocHGlobal(16); var mask = Marshal.AllocHGlobal(16);
        try
        {
            Zero(values, 16); Zero(mask, 16);
            Marshal.WriteByte(mask, bit / 8, (byte)(1 << (bit % 8)));
            if (enabled) Marshal.WriteByte(values, bit / 8, (byte)(1 << (bit % 8)));
            CheckStatus(Resolve<DbProps_OptionsSet>("NSFDbSetOptionsExt")(database, values, mask), "NSFDbSetOptionsExt");
        }
        finally { Marshal.FreeHGlobal(mask); Marshal.FreeHGlobal(values); }
    }
    internal (uint Warning, uint Quota, uint Maximum) GetDatabaseQuota(nint database)
    {
        EnsureInitialized(); using var thread = EnterThread(); var buffer = Marshal.AllocHGlobal(64);
        try
        {
            Zero(buffer, 64); CheckStatus(Resolve<DbProps_QuotaGet>("NSFDbQuotaGethDB")(database, buffer), "NSFDbQuotaGethDB");
            return (unchecked((uint)Marshal.ReadInt32(buffer)), unchecked((uint)Marshal.ReadInt32(buffer, 4)), unchecked((uint)Marshal.ReadInt32(buffer, 12)));
        }
        finally { Marshal.FreeHGlobal(buffer); }
    }
    internal void SetDatabaseQuota(nint database, uint value, bool warning)
    {
        EnsureInitialized(); using var thread = EnterThread();
        var path = Marshal.AllocHGlobal(4096); var quota = Marshal.AllocHGlobal(16);
        try
        {
            Zero(path, 4096); CheckStatus(Resolve<DbProps_PathGet>("NSFDbPathGet")(database, path, 0), "NSFDbPathGet");
            for (var i = 0; i < 4; i++) Marshal.WriteInt32(quota, i * 4, -1);
            Marshal.WriteInt32(quota, warning ? 0 : 4, unchecked((int)value));
            CheckStatus(Resolve<DbProps_QuotaSet>("NSFDbQuotaSetExt")(path, 0, quota), "NSFDbQuotaSetExt");
        }
        finally { Marshal.FreeHGlobal(quota); Marshal.FreeHGlobal(path); }
    }
    internal (int State, int Strength) GetDatabaseEncryption(nint database)
    {
        EnsureInitialized(); using var thread = EnterThread();
        CheckStatus(Resolve<DbProps_Encryption>("NSFDbLocalSecInfoGetLocal")(unchecked((uint)database), out var state, out var strength), "NSFDbLocalSecInfoGetLocal");
        return (state, strength);
    }
    internal bool IsDatabaseLocallyEncrypted(nint database)
    {
        EnsureInitialized(); using var thread = EnterThread();
        CheckStatus(Resolve<DbProps_IsEncrypted>("NSFDbIsLocallyEncrypted")(database, out var result), "NSFDbIsLocallyEncrypted"); return result != 0;
    }
    internal string[] GetAclHistory(nint database)
    {
        EnsureInitialized(); using var thread = EnterThread(); var acl = ReadAcl(database); uint history = 0;
        try
        {
            CheckStatus(Resolve<DbProps_AclHistory>("ACLGetHistory")(acl, out history, out var count), "ACLGetHistory");
            if (count == 0 || history == 0) return [];
            CheckStatus(Resolve<OSMemGetSizeDelegate>("OSMemGetSize")(history, out var size), "OSMemGetSize(ACL history)");
            var buffer = Resolve<OSLockObjectAccessDelegate>("OSLockObject")(history);
            try
            {
                var result = new List<string>(); var offset = 0;
                for (var i = 0; i < count; i++)
                {
                    var start = offset;
                    while (offset + 1 < size && !(Marshal.ReadByte(buffer, offset) == 0 && Marshal.ReadByte(buffer, offset + 1) == 0)) offset++;
                    if (offset + 1 >= size) throw new NotesException("Invalid ACL history buffer.");
                    result.Add(FromLmbcs(nint.Add(buffer, start), offset - start).Replace('\0', ' ')); offset += 2;
                }
                return result.ToArray();
            }
            finally { Resolve<OSUnlockObjectAccessDelegate>("OSUnlockObject")(history); }
        }
        finally { if (history != 0) FreeAcl(history); FreeAcl(acl); }
    }
    internal byte[] ReadDatabaseReplicaInfo(nint database)
    {
        EnsureInitialized(); using var thread = EnterThread(); var buffer = Marshal.AllocHGlobal(32);
        try { Zero(buffer, 32); CheckStatus(Resolve<Replica_NSFDbReplicaInfoGet>("NSFDbReplicaInfoGet")(database, buffer), "NSFDbReplicaInfoGet"); var bytes = new byte[20]; Marshal.Copy(buffer, bytes, 0, 20); return bytes; }
        finally { Marshal.FreeHGlobal(buffer); }
    }
    internal void WriteDatabaseReplicaInfo(nint database, byte[] data)
    {
        EnsureInitialized(); using var thread = EnterThread(); var buffer = Marshal.AllocHGlobal(32);
        try { Zero(buffer, 32); Marshal.Copy(data, 0, buffer, 20); CheckStatus(Resolve<Replica_NSFDbReplicaInfoSet>("NSFDbReplicaInfoSet")(database, buffer), "NSFDbReplicaInfoSet"); }
        finally { Marshal.FreeHGlobal(buffer); }
    }
    internal XElement ReadDatabaseDxlProperties(nint database)
    {
        var exporter = CreateDxlExporter();
        try
        {
            var bytes = ExportDxlCollection(exporter, database, []);
            using var stream = new MemoryStream(bytes);
            using var reader = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore, XmlResolver = null });
            return XDocument.Load(reader).Root ?? throw new NotesException("Database DXL properties missing.");
        }
        finally { DeleteDxlExporter(exporter); }
    }
    internal void WriteDatabaseDxlProperties(nint database, XElement root)
    {
        // Preserve all exported metadata but do not import design, documents or ACL.
        var copy = new XElement(root.Name, root.Attributes(), root.Elements().Where(x => x.Name.LocalName is "fulltextsettings" or "launchsettings").Select(x => new XElement(x)));
        var importer = CreateDxlImporter();
        try
        {
            SetDxlImporterBool(importer, 5, true); // ReplaceDbProperties
            SetDxlImporterInt(importer, 1, 1); // Ignore ACL
            SetDxlImporterInt(importer, 2, 1); // Ignore design
            SetDxlImporterInt(importer, 3, 1); // Ignore documents
            using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(copy.ToString(SaveOptions.DisableFormatting)));
            ImportDxl(importer, stream, database);
        }
        finally { DeleteDxlImporter(importer); }
    }
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort DbProps_OptionsGet(nint database, nint options);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort DbProps_OptionsSet(nint database, nint values, nint mask);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort DbProps_QuotaGet(nint database, nint quotas);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort DbProps_PathGet(nint database, nint canonical, nint expanded);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort DbProps_QuotaSet(nint path, uint flags, nint quotas);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort DbProps_Encryption(uint database, out int state, out int strength);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort DbProps_IsEncrypted(nint database, out int result);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort DbProps_AclHistory(uint acl, out uint history, out ushort count);
}
