namespace DotNetToDomino;

/// <summary>One inline image extracted from a rich-text compound-text item.</summary>
public sealed class NotesInlineImage
{
    private readonly byte[] _data;
    internal NotesInlineImage(int index, ushort type, byte[] data) { Index = index; Type = type; _data = data; }
    public int Index { get; }
    public ushort Type { get; }
    public string Extension => Type switch { 1 => ".gif", 2 => ".jpg", 4 => ".png", 5 => ".svg", 6 => ".tif", _ => ".bin" };
    public long Length => _data.LongLength;
    public byte[] ToByteArray() => (byte[])_data.Clone();
    public void SaveAs(string path) { ArgumentException.ThrowIfNullOrWhiteSpace(path); File.WriteAllBytes(Path.GetFullPath(path), _data); }
}
