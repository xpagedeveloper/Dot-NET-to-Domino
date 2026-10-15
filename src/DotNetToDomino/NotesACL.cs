using DotNetToDomino.Interop;

namespace DotNetToDomino;

/// <summary>An editable in-memory database ACL. Call Save to persist changes.</summary>
public sealed class NotesACL : NotesObject
{
    private readonly NotesDatabase _database;
    private readonly List<NotesACLEntry> _entries = [];
    private uint _handle;
    internal NotesACL(NotesSession session, NotesDatabase database) : base(session)
    {
        _database = database;
        try { _handle = session.Api.ReadAcl(database.Handle); }
        catch { Session.Unregister(this); throw; }
    }
    public NotesDatabase Parent { get { EnsureAcl(); return _database; } }
    public IReadOnlyList<NotesACLEntry> Entries { get { EnsureAcl(); return Session.Api.EnumerateAcl(_handle).Select(x => Wrap(x.Name)).ToArray(); } }
    public string[] Roles { get { EnsureAcl(); return Session.Api.GetAclRoles(_handle).Values.ToArray(); } }
    public NotesACLEntry? GetEntry(string name)
    {
        EnsureAcl(); ValidateName(name);
        var data = Session.Api.EnumerateAcl(_handle).FirstOrDefault(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        return data is null ? null : Wrap(data.Name);
    }
    public NotesACLEntry? GetFirstEntry() => Entries.FirstOrDefault();
    public NotesACLEntry? GetNextEntry(NotesACLEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (!ReferenceEquals(entry.Parent, this)) throw new ArgumentException("Entry belongs to another ACL.", nameof(entry));
        var entries = Entries;
        for (var i = 0; i < entries.Count - 1; i++) if (entries[i].Name.Equals(entry.Name, StringComparison.OrdinalIgnoreCase)) return entries[i + 1];
        return null;
    }
    public NotesACLEntry CreateACLEntry(string name, int level)
    {
        EnsureAcl(); ValidateName(name); ValidateLevel(level);
        if (GetEntry(name) is not null) throw new ArgumentException("ACL entry already exists.", nameof(name));
        Session.Api.WriteAclEntry(_handle, new(name, (ushort)level, 0, new byte[10]), true);
        return GetEntry(name) ?? throw new NotesException("Created ACL entry could not be reopened.");
    }
    public void AddRole(string role)
    {
        EnsureAcl(); role = NormalizeRole(role);
        var roles = Session.Api.GetAclRoles(_handle);
        if (roles.Values.Any(x => x.Equals("[" + role + "]", StringComparison.OrdinalIgnoreCase))) throw new ArgumentException("Role already exists.", nameof(role));
        var index = Enumerable.Range(5, 75).FirstOrDefault(x => !roles.ContainsKey(x), -1);
        if (index < 0) throw new InvalidOperationException("ACL has no free role slots.");
        Session.Api.SetAclRole(_handle, index, role);
    }
    public void RenameRole(string oldName, string newName)
    {
        EnsureAcl(); var index = RoleIndex(oldName); newName = NormalizeRole(newName);
        if (Roles.Any(x => x.Equals("[" + newName + "]", StringComparison.OrdinalIgnoreCase))) throw new ArgumentException("Role already exists.", nameof(newName));
        Session.Api.SetAclRole(_handle, index, newName);
    }
    public void DeleteRole(string role)
    {
        EnsureAcl(); var index = RoleIndex(role);
        foreach (var entry in Session.Api.EnumerateAcl(_handle))
        {
            entry.Privileges[index / 8] &= (byte)~(1 << (index % 8));
            Session.Api.WriteAclEntry(_handle, entry, false);
        }
        Session.Api.SetAclRole(_handle, index, string.Empty);
    }
    public void Save() { EnsureAcl(); Session.Api.SaveAcl(_database.Handle, _handle); }
    /// <summary>Removes an ACL entry from this editable ACL. Call <see cref="Save"/> to persist it.</summary>
    public void RemoveEntry(string name)
    {
        EnsureAcl(); ValidateName(name);
        if (name.Equals("-Default-", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The default entry cannot be removed.");
        Session.Api.DeleteAclEntry(_handle, name);
        foreach (var entry in _entries.Where(x => x.EntryName.Equals(name, StringComparison.OrdinalIgnoreCase)).ToArray()) entry.Recycle();
    }
    internal NotesAclEntryData ReadEntry(string name)
    {
        EnsureAcl(); return Session.Api.EnumerateAcl(_handle).FirstOrDefault(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException("ACL entry no longer exists.");
    }
    internal void Update(NotesAclEntryData data, string? newName = null)
    {
        EnsureAcl();
        if (newName is not null)
        {
            ValidateName(newName);
            if (data.Name == "-Default-") throw new InvalidOperationException("The default ACL entry cannot be renamed.");
            if (GetEntry(newName) is not null) throw new ArgumentException("ACL entry already exists.", nameof(newName));
        }
        Session.Api.WriteAclEntry(_handle, data, false, newName);
    }
    internal void RemoveEntryInternal(string name)
    {
        EnsureAcl(); if (name.Equals("-Default-", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("The default entry cannot be removed.");
        Session.Api.DeleteAclEntry(_handle, name);
    }
    internal int RoleIndex(string role)
    {
        EnsureAcl(); var name = "[" + NormalizeRole(role) + "]";
        return Session.Api.GetAclRoles(_handle).FirstOrDefault(x => x.Value.Equals(name, StringComparison.OrdinalIgnoreCase), new(-1, "")).Key is var index && index >= 0
            ? index : throw new ArgumentException("ACL role does not exist.", nameof(role));
    }
    internal string[] EntryRoles(byte[] privileges)
    {
        EnsureAcl(); return Session.Api.GetAclRoles(_handle).Where(x => (privileges[x.Key / 8] & (1 << (x.Key % 8))) != 0).Select(x => x.Value).ToArray();
    }
    private NotesACLEntry Wrap(string name)
    {
        var existing = _entries.FirstOrDefault(x => !x.IsRecycled && x.EntryName.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (existing is not null) return existing;
        var entry = new NotesACLEntry(Session, this, name); _entries.Add(entry); return entry;
    }
    private void EnsureAcl() { EnsureAlive(); _ = _database.Handle; }
    internal static void ValidateLevel(int level) { if (level is < 0 or > 6) throw new ArgumentOutOfRangeException(nameof(level)); }
    private static void ValidateName(string name) { ArgumentException.ThrowIfNullOrWhiteSpace(name); if (name.Contains('\0')) throw new ArgumentException("ACL name contains a null character.", nameof(name)); }
    private static string NormalizeRole(string role)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(role); role = role.Trim();
        if (role.StartsWith('[') && role.EndsWith(']')) role = role[1..^1];
        if (role.Length == 0 || role.Contains('\0') || role.Contains('[') || role.Contains(']')) throw new ArgumentException("Invalid ACL role name.", nameof(role));
        return role;
    }
    protected override void ReleaseNative()
    {
        foreach (var entry in _entries) entry.Recycle();
        _entries.Clear(); var handle = _handle; _handle = 0;
        try { Session.Api.FreeAcl(handle); } finally { _database.UnregisterChild(this); }
    }
}

/// <summary>An entry in an editable NotesACL. Changes are persisted by Parent.Save().</summary>
public sealed class NotesACLEntry : NotesObject
{
    private readonly NotesACL _acl;
    private string _name;
    internal NotesACLEntry(NotesSession session, NotesACL acl, string name) : base(session) { _acl = acl; _name = name; }
    internal string EntryName => _name;
    private NotesAclEntryData Data { get { EnsureAlive(); return _acl.ReadEntry(_name); } }
    public NotesACL Parent { get { _ = Data; return _acl; } }
    public string Name { get { _ = Data; return _name; } set { var data = Data; _acl.Update(data, value); _name = value; } }
    public int Level { get => Data.Level; set { NotesACL.ValidateLevel(value); var data = Data; _acl.Update(data with { Level = (ushort)value }); } }
    public int AccessFlags => Data.Flags;
    public string[] Roles => _acl.EntryRoles(Data.Privileges);
    public bool CanCreateDocuments { get => !Flag(0x0001); set => SetFlag(0x0001, !value); }
    public bool CanDeleteDocuments { get => !Flag(0x0004); set => SetFlag(0x0004, !value); }
    public bool CanCreatePersonalAgent { get => Flag(0x0008); set => SetFlag(0x0008, value); }
    public bool CanCreatePersonalFolder { get => Flag(0x0010); set => SetFlag(0x0010, value); }
    public bool CanCreateSharedFolder { get => Flag(0x0080); set => SetFlag(0x0080, value); }
    public bool CanCreateLSOrJavaAgent { get => Flag(0x0100); set => SetFlag(0x0100, value); }
    public bool IsPublicReader { get => Flag(0x0200); set => SetFlag(0x0200, value); }
    public bool IsPublicWriter { get => Flag(0x0400); set => SetFlag(0x0400, value); }
    public bool CanReplicateOrCopyDocuments { get => !Flag(0x1000); set => SetFlag(0x1000, !value); }
    public bool IsPerson { get => Flag(0x0020); set => SetFlag(0x0020, value); }
    public bool IsGroup { get => Flag(0x0040); set => SetFlag(0x0040, value); }
    public bool IsServer { get => Flag(0x0002); set => SetFlag(0x0002, value); }
    public bool IsRoleEnabled(string role) { var index = _acl.RoleIndex(role); return (Data.Privileges[index / 8] & (1 << (index % 8))) != 0; }
    public void EnableRole(string role) => SetRole(role, true);
    public void DisableRole(string role) => SetRole(role, false);
    public void Remove() { _ = Data; _acl.RemoveEntryInternal(_name); Recycle(); }
    private bool Flag(ushort flag) => (Data.Flags & flag) != 0;
    private void SetFlag(ushort flag, bool enabled) { var data = Data; _acl.Update(data with { Flags = enabled ? (ushort)(data.Flags | flag) : (ushort)(data.Flags & ~flag) }); }
    private void SetRole(string role, bool enabled)
    {
        var data = Data; var index = _acl.RoleIndex(role);
        if (enabled) data.Privileges[index / 8] |= (byte)(1 << (index % 8)); else data.Privileges[index / 8] &= (byte)~(1 << (index % 8));
        _acl.Update(data);
    }
    protected override void ReleaseNative() { }
}
