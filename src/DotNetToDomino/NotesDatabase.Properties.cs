using System.Globalization;
using System.Xml.Linq;

namespace DotNetToDomino;

public sealed partial class NotesDatabase
{
    /// <summary>Explicit .NET extension for configuring the template name; the property remains read-only.</summary>
    public void SetTemplateName(string name) { EnsureOpen(); ArgumentNullException.ThrowIfNull(name); Session.Api.SetDatabaseTemplateName(_handle, name); }
    /// <summary>Explicit .NET extension for configuring design inheritance; the property remains read-only.</summary>
    public void SetDesignTemplateName(string name) { EnsureOpen(); ArgumentNullException.ThrowIfNull(name); Session.Api.SetDatabaseDesignTemplateName(_handle, name); }
    public string[] ACLActivityLog { get { EnsureOpen(); return Session.Api.GetAclHistory(_handle); } }
    public string[] Managers { get { using var acl = ACL; return acl.Entries.Where(x => x.Level == NotesConst.ACLLEVEL_MANAGER).Select(x => x.Name).ToArray(); } }
    public bool IsCurrentAccessPublicReader { get { using var access = QueryAccess(); return access.IsPublicReader; } }
    public bool IsCurrentAccessPublicWriter { get { using var access = QueryAccess(); return access.IsPublicWriter; } }
    public bool FolderReferencesEnabled { get => Option(20); set => Option(20, value); }
    public bool IsDesignLockingEnabled { get => Option(31); set => Option(31, value); }
    public bool IsDocumentLockingEnabled { get => Option(30); set => Option(30, value); }
    public bool IsConfigurationDirectory => Option(57);
    public bool IsDirectoryCatalog => Option(24);
    public bool IsMultiDbSearch => Option(16);
    public bool IsPendingDelete => Option(12);
    public bool IsInService { get => !Option(10); set => Option(10, !value); }
    public bool IsInMultiDbIndexing { get => ReplicaFlag(0x2000); set => ReplicaFlag(0x2000, value); }
    public bool ListInDbCatalog { get => !ReplicaFlag(0x0040); set => ReplicaFlag(0x0040, !value); }
    public int SizeQuota { get { EnsureOpen(); return checked((int)Session.Api.GetDatabaseQuota(_handle).Quota); } set { EnsureOpen(); ArgumentOutOfRangeException.ThrowIfNegative(value); Session.Api.SetDatabaseQuota(_handle, (uint)value, false); } }
    public int SizeWarning { get { EnsureOpen(); return checked((int)Session.Api.GetDatabaseQuota(_handle).Warning); } set { EnsureOpen(); ArgumentOutOfRangeException.ThrowIfNegative(value); Session.Api.SetDatabaseQuota(_handle, (uint)value, true); } }
    public double MaxSize { get { EnsureOpen(); return Session.Api.GetDatabaseQuota(_handle).Maximum * 1024.0; } }
    public bool IsLocallyEncrypted { get { EnsureOpen(); return Session.Api.IsDatabaseLocallyEncrypted(_handle); } }
    public int EncryptionStrength { get { EnsureOpen(); return Session.Api.GetDatabaseEncryption(_handle).Strength; } }
    public NotesReplication ReplicationInfo { get { EnsureOpen(); return Own(new NotesReplication(Session, this)); } }
    public string NotesURL { get { EnsureOpen(); return $"notes://{Uri.EscapeDataString(Server)}/{ReplicaID}"; } }
    /// <summary>
    /// Indicates a private address book using the only stable information available to a standalone C API client:
    /// an address-book database opened without a Domino server.
    /// </summary>
    public bool IsPrivateAddressBook => Type == 10 && Server.Length == 0;
    /// <summary>
    /// Indicates a public address book when it is an address-book database opened through a Domino server.
    /// </summary>
    public bool IsPublicAddressBook => Type == 10 && Server.Length != 0;
    public int LimitRevisions { get => NumericDxlProperty("maxrevisionentries", 0); set => SetNumericDxlProperty("maxrevisionentries", value); }
    public int LimitUpdatedBy { get => NumericDxlProperty("maxupdatedbyentries", 0); set => SetNumericDxlProperty("maxupdatedbyentries", value); }
    public int UndeleteExpireTime { get => NumericDxlProperty("softdeletionsexpirein", 48); set => SetNumericDxlProperty("softdeletionsexpirein", value); }
    public int Type
    {
        get
        {
            EnsureOpen();
            if (Option(62)) return 1;
            var type = (string?)Session.Api.ReadDatabaseDxlProperties(_handle).Attribute("type") ?? "standard";
            return type switch { "mailfile" => 2, "mailbox" => 3, "subscriptions" => 4, "newsproxy" => 5, "imapproxy" => 6, "portfolio" => 7, "multidbsearch" => 8, "lightaddressbook" => 9, "addressbook" => 10, "personaljournal" => 11, "library" => 12, "standard" => 13, _ => throw new NotesException("Unknown native database type: " + type) };
        }
    }
    public int FTIndexFrequency
    {
        get
        {
            EnsureServerFullText();
            var settings = Session.Api.ReadDatabaseDxlProperties(_handle).Elements().FirstOrDefault(x => x.Name.LocalName == "fulltextsettings");
            return (string?)settings?.Attribute("update") switch { "daily" => 1, "scheduled" => 2, "hourly" => 3, "immediate" => 4, _ => throw new NotesException("Full-text index frequency is not available.") };
        }
        set
        {
            EnsureServerFullText();
            var frequency = value switch { 1 => "daily", 2 => "scheduled", 3 => "hourly", 4 => "immediate", _ => throw new ArgumentOutOfRangeException(nameof(value)) };
            var root = Session.Api.ReadDatabaseDxlProperties(_handle);
            var settings = root.Elements().FirstOrDefault(x => x.Name.LocalName == "fulltextsettings") ?? throw new NotesException("Full-text settings missing.");
            settings.SetAttributeValue("update", frequency); Session.Api.WriteDatabaseDxlProperties(_handle, root);
        }
    }
    private bool Option(int bit) { EnsureOpen(); return Session.Api.GetDatabaseOption(_handle, bit); }
    private void Option(int bit, bool value) { EnsureOpen(); Session.Api.SetDatabaseOption(_handle, bit, value); }
    private bool ReplicaFlag(ushort flag) { EnsureOpen(); return (BitConverter.ToUInt16(Session.Api.ReadDatabaseReplicaInfo(_handle), 8) & flag) != 0; }
    private void ReplicaFlag(ushort flag, bool value)
    {
        EnsureOpen(); var data = Session.Api.ReadDatabaseReplicaInfo(_handle); var flags = BitConverter.ToUInt16(data, 8);
        BitConverter.GetBytes(value ? (ushort)(flags | flag) : (ushort)(flags & ~flag)).CopyTo(data, 8); Session.Api.WriteDatabaseReplicaInfo(_handle, data);
    }
    private int NumericDxlProperty(string name, int defaultValue)
    {
        EnsureOpen(); var value = (string?)Session.Api.ReadDatabaseDxlProperties(_handle).Attribute(name);
        return value is null ? defaultValue : int.Parse(value, CultureInfo.InvariantCulture);
    }
    private void SetNumericDxlProperty(string name, int value)
    {
        EnsureOpen(); ArgumentOutOfRangeException.ThrowIfNegative(value);
        var root = Session.Api.ReadDatabaseDxlProperties(_handle); root.SetAttributeValue(name, value.ToString(CultureInfo.InvariantCulture)); Session.Api.WriteDatabaseDxlProperties(_handle, root);
    }
    private void EnsureServerFullText()
    {
        EnsureOpen(); if (Server.Length == 0) throw new InvalidOperationException("FTIndexFrequency applies to server databases only.");
        if (!IsFTIndexed) throw new InvalidOperationException("FTIndexFrequency requires a full-text index.");
    }
}
