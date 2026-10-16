using System.Text;

namespace DotNetToDomino;

public sealed class NotesStream : NotesObject
{
    private MemoryStream _memory = new();
    private FileStream? _file;
    private string _charset = "Unicode";
    private bool _readOnly;
    internal NotesStream(NotesSession session) : base(session) { }
    private Stream Stream => (Stream?)_file ?? _memory;
    private Encoding Encoding => ResolveEncoding(_charset);
    public NotesSession Parent => Session;
    public int Bytes { get { EnsureAlive(); return checked((int)Math.Min(Stream.Length, int.MaxValue)); } }
    public long Length { get { EnsureAlive(); return Stream.Length; } }
    public string Charset { get { EnsureAlive(); return _charset; } set { EnsureAlive(); _charset = string.IsNullOrWhiteSpace(value) ? "Unicode" : value; } }
    public bool IsEOS { get { EnsureAlive(); return Stream.Position >= Stream.Length; } }
    public bool IsReadOnly { get { EnsureAlive(); return _readOnly; } }
    public int Position
    {
        get { EnsureAlive(); return checked((int)Math.Min(Stream.Position, int.MaxValue)); }
        set { EnsureAlive(); if (value < 0) throw new ArgumentOutOfRangeException(nameof(value), "NotesStream.Position cannot be negative."); Stream.Position = Math.Min((long)value, Stream.Length); }
    }

    public bool Open(string filePath, string mode = "binary")
    {
        EnsureAlive();
        Close();
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(mode);
        filePath = filePath.Trim();
        mode = mode.Trim();
        _readOnly = mode.Equals("readonly", StringComparison.OrdinalIgnoreCase) || mode.Equals("read", StringComparison.OrdinalIgnoreCase);
        try
        {
            var fullPath = Path.GetFullPath(filePath);
            if (!_readOnly)
            {
                var directory = Path.GetDirectoryName(fullPath);
                if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            }
            _file = new FileStream(fullPath, _readOnly ? FileMode.Open : FileMode.OpenOrCreate, _readOnly ? FileAccess.Read : FileAccess.ReadWrite, FileShare.Read);
            _file.Position = 0;
            return true;
        }
        catch { _file = null; _readOnly = false; return false; }
    }

    public void Close() { EnsureAlive(); _file?.Dispose(); _file = null; _readOnly = false; }
    public byte[] Read() { EnsureAlive(); return Read(checked((int)Math.Min(Stream.Length - Stream.Position, int.MaxValue))); }
    public byte[] Read(int length)
    {
        EnsureAlive();
        if (length < 0) throw new ArgumentOutOfRangeException(nameof(length));
        var bytes = new byte[Math.Min(length, checked((int)Math.Min(Stream.Length - Stream.Position, int.MaxValue)))];
        var read = Stream.Read(bytes, 0, bytes.Length);
        if (read != bytes.Length) Array.Resize(ref bytes, read);
        return bytes;
    }
    public string ReadText() => Encoding.GetString(Read());
    public string ReadText(int length) => Encoding.GetString(Read(length));
    public void Write(ReadOnlySpan<byte> data) { EnsureAlive(); if (_readOnly) throw new InvalidOperationException("NotesStream is read-only."); Stream.Write(data); Stream.Flush(); }
    public void Write(byte[] data) { ArgumentNullException.ThrowIfNull(data); Write(data.AsSpan()); }
    public void Write(string text) { ArgumentNullException.ThrowIfNull(text); Write(Encoding.GetBytes(text)); }
    public void WriteText(string text) { ArgumentNullException.ThrowIfNull(text); Write(Encoding.GetBytes(text)); }
    public byte[] ToArray()
    {
        EnsureAlive();
        if (_file is null) return _memory.ToArray();
        var position = _file.Position;
        _file.Position = 0;
        using var copy = new MemoryStream();
        _file.CopyTo(copy);
        _file.Position = position;
        return copy.ToArray();
    }
    public void Truncate() { EnsureAlive(); if (_readOnly) throw new InvalidOperationException("NotesStream is read-only."); Stream.SetLength(Stream.Position); Stream.Flush(); }
    internal Stream AsReadableStream() { EnsureAlive(); return new MemoryStream(ToArray(), writable: false); }
    internal void Replace(byte[] data)
    {
        EnsureAlive();
        if (_readOnly) throw new InvalidOperationException("NotesStream is read-only.");
        if (_file is not null)
        {
            _file.SetLength(0); _file.Position = 0; _file.Write(data); _file.Position = 0;
        }
        else
        {
            _memory.Dispose(); _memory = new MemoryStream(data.Length); _memory.Write(data); _memory.Position = 0;
        }
    }
    protected override void ReleaseNative() { _file?.Dispose(); _file = null; _memory.Dispose(); _memory = new MemoryStream(); }
    private static Encoding ResolveEncoding(string charset)
    {
        var name = charset.Trim();
        if (name.Length == 0 || name.Equals("Unicode", StringComparison.OrdinalIgnoreCase)) return Encoding.Unicode;
        if (name.Equals("UTF-8", StringComparison.OrdinalIgnoreCase) || name.Equals("UTF8", StringComparison.OrdinalIgnoreCase)) return new UTF8Encoding(false);
        try { return Encoding.GetEncoding(name); }
        catch { throw new ArgumentException("Unsupported NotesStream charset: " + charset, nameof(charset)); }
    }
}
