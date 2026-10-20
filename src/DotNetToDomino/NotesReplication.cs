namespace DotNetToDomino;

/// <summary>An editable snapshot of database replication settings. Call Save to persist changes.</summary>
public sealed class NotesReplication : NotesObject
{
    private readonly NotesDatabase _database;
    private readonly byte[] _data;
    internal NotesReplication(NotesSession session, NotesDatabase database) : base(session)
    {
        _database = database;
        try { _data = session.Api.ReadDatabaseReplicaInfo(database.Handle); }
        catch { Session.Unregister(this); throw; }
    }
    public NotesDatabase Parent { get { Check(); return _database; } }
    public bool Disabled { get => Flag(0x0004); set => Flag(0x0004, value); }
    public bool Abstract { get => Flag(0x0200); set => Flag(0x0200, value); }
    public bool IgnoreDeletes { get => Flag(0x0010); set => Flag(0x0010, value); }
    public bool IgnoreDestDeletes { get => Flag(0x1000); set => Flag(0x1000, value); }
    public bool CutoffDelete { get => Flag(0x0080); set => Flag(0x0080, value); }
    public int CutoffInterval { get { Check(); return BitConverter.ToUInt16(_data, 10); } set { Check(); if (value is < 0 or > ushort.MaxValue) throw new ArgumentOutOfRangeException(nameof(value)); BitConverter.GetBytes((ushort)value).CopyTo(_data, 10); } }
    public NotesDateTime CutoffDate
    {
        get { Check(); return NotesDateTime.FromNative(Session, new Interop.NotesTimeDate { Innards0 = BitConverter.ToUInt32(_data, 12), Innards1 = BitConverter.ToUInt32(_data, 16) }); }
        set { Check(); ArgumentNullException.ThrowIfNull(value); BitConverter.GetBytes(value.NativeValue.Innards0).CopyTo(_data, 12); BitConverter.GetBytes(value.NativeValue.Innards1).CopyTo(_data, 16); }
    }
    public int Priority
    {
        get { Check(); return (Flags & 0xC000) switch { 0x4000 => 1, 0 => 2, _ => 3 }; }
        set { Check(); Flags = (ushort)((Flags & 0x3FFF) | (value switch { 1 => 0x4000, 2 => 0, 3 => 0xC000, _ => throw new ArgumentOutOfRangeException(nameof(value)) })); }
    }
    /// <summary>Clears the parent database's replication history.</summary>
    public void ClearHistory() { Check(); Session.Api.ClearDatabaseReplicationHistory(_database.Handle); }
    public void Save()
    {
        Check();
        // Preserve a replica-ID changed through NotesDatabase after this snapshot was read.
        var current = Session.Api.ReadDatabaseReplicaInfo(_database.Handle); Array.Copy(current, _data, 8);
        Session.Api.WriteDatabaseReplicaInfo(_database.Handle, _data);
    }
    private ushort Flags { get => BitConverter.ToUInt16(_data, 8); set => BitConverter.GetBytes(value).CopyTo(_data, 8); }
    private bool Flag(ushort flag) { Check(); return (Flags & flag) != 0; }
    private void Flag(ushort flag, bool value) { Check(); Flags = value ? (ushort)(Flags | flag) : (ushort)(Flags & ~flag); }
    private void Check() { EnsureAlive(); _ = _database.Handle; }
    protected override void ReleaseNative() => _database.UnregisterChild(this);
}
