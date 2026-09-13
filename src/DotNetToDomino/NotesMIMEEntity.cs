using System.Text;

namespace DotNetToDomino;

/// <summary>Represents a MIME entity stored in a Notes document MIME item.</summary>
public sealed class NotesMIMEEntity : NotesObject
{
    private readonly NotesDocument _document;
    private readonly string _itemName;
    private MimeMessage _message;

    internal NotesMIMEEntity(NotesSession session, NotesDocument document, string itemName) : base(session)
    {
        _document = document;
        _itemName = itemName;
        _message = MimeMessage.Parse(session.Api.ReadMimeStream(document.Handle, itemName));
    }

    public NotesDocument Parent { get { EnsureEntityAlive(); return _document; } }
    public string BoundaryStart { get { EnsureEntityAlive(); return _message.Boundary.Length == 0 ? string.Empty : "--" + _message.Boundary; } }
    public string BoundaryEnd { get { EnsureEntityAlive(); return _message.Boundary.Length == 0 ? string.Empty : "--" + _message.Boundary + "--"; } }
    public string Charset { get { EnsureEntityAlive(); return _message.Charset; } }
    public string ContentAsText { get { EnsureEntityAlive(); return _message.GetDecodedText(); } }
    public string ContentType { get { EnsureEntityAlive(); return _message.ContentType; } }
    public string ContentSubType { get { EnsureEntityAlive(); return _message.ContentSubType; } }
    public int Encoding { get { EnsureEntityAlive(); return MimeMessage.EncodingConstant(_message.TransferEncoding); } }
    public string Headers { get { EnsureEntityAlive(); return _message.HeadersText; } }
    public string Preamble { get { EnsureEntityAlive(); return _message.Preamble; } set { EnsureEntityAlive(); _message.Preamble = value ?? string.Empty; Commit(); } }

    public NotesMIMEHeader? GetNthHeader(string name, int occurrence = 1)
    {
        EnsureEntityAlive();
        if (string.IsNullOrWhiteSpace(name)) return null;
        occurrence = Math.Max(1, occurrence);
        var found = 0;
        for (var i = 0; i < _message.Headers.Count; i++)
            if (_message.Headers[i].Name.Equals(name.Trim(), StringComparison.OrdinalIgnoreCase) && ++found == occurrence)
                return new NotesMIMEHeader(Session, this, i);
        return null;
    }

    public NotesMIMEHeader CreateHeader(string name)
    {
        EnsureEntityAlive();
        name = name?.Trim() ?? string.Empty;
        if (name.Length == 0 || name.Contains(':') || name.Contains('\r') || name.Contains('\n')) throw new ArgumentException("Invalid MIME header name.", nameof(name));
        _message.Headers.Add(new MimeHeaderValue(name, string.Empty));
        Commit();
        return new NotesMIMEHeader(Session, this, _message.Headers.Count - 1);
    }

    public string GetSomeHeaders(string headerNames)
    {
        EnsureEntityAlive();
        var names = (headerNames ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (names.Length == 0) return Headers;
        var wanted = new HashSet<string>(names, StringComparer.OrdinalIgnoreCase);
        return string.Join("\r\n", _message.Headers.Where(h => wanted.Contains(h.Name)).Select(h => h.Name + ": " + h.Value));
    }

    public void GetContentAsBytes(NotesStream stream) { EnsureEntityAlive(); ArgumentNullException.ThrowIfNull(stream); stream.Write(_message.GetDecodedBytes()); }
    public void GetContentAsText(NotesStream stream, bool entityCharset = false) { EnsureEntityAlive(); ArgumentNullException.ThrowIfNull(stream); _ = entityCharset; stream.WriteText(_message.GetDecodedText()); }
    public void GetEntityAsText(NotesStream stream) { EnsureEntityAlive(); ArgumentNullException.ThrowIfNull(stream); stream.Write(_message.Serialize()); }

    public void SetContentFromText(NotesStream stream, string contentType, int encoding) { EnsureEntityAlive(); ArgumentNullException.ThrowIfNull(stream); SetContent(stream.Read(), contentType, encoding); }
    public void SetContentFromBytes(NotesStream stream, string contentType, int encoding) { EnsureEntityAlive(); ArgumentNullException.ThrowIfNull(stream); SetContent(stream.Read(), contentType, encoding); }
    public void EncodeContent(int encoding) { EnsureEntityAlive(); _message.SetDecodedContent(_message.GetDecodedBytes(), encoding); Commit(); }
    public void DecodeContent() { EnsureEntityAlive(); _message.SetDecodedContent(_message.GetDecodedBytes(), 1725); Commit(); }
    public void Remove() { EnsureEntityAlive(); _document.RemoveItem(_itemName); Recycle(); }

    internal MimeHeaderValue HeaderAt(int index) { EnsureEntityAlive(); return _message.Headers[index]; }
    internal void SetHeader(int index, string value) { EnsureEntityAlive(); _message.Headers[index].Value = value; Commit(); }
    internal void RemoveHeader(int index) { EnsureEntityAlive(); _message.Headers.RemoveAt(index); Commit(); }

    private void SetContent(byte[] data, string contentType, int encoding)
    {
        if (string.IsNullOrWhiteSpace(contentType)) contentType = "application/octet-stream";
        if (contentType.Contains('\r') || contentType.Contains('\n')) throw new ArgumentException("MIME content type cannot contain CR or LF.", nameof(contentType));
        _message.SetHeader("Content-Type", contentType);
        _message.SetDecodedContent(data, encoding);
        Commit();
    }
    private void Commit() { var raw = _message.Serialize(); Session.Api.WriteMimeStream(_document.Handle, _itemName, raw); _message = MimeMessage.Parse(Session.Api.ReadMimeStream(_document.Handle, _itemName)); }
    private void EnsureEntityAlive() { EnsureAlive(); _ = _document.Handle; }
    protected override void ReleaseNative() { }
}

public sealed class NotesMIMEHeader : NotesObject
{
    private readonly NotesMIMEEntity _entity; private int _index;
    internal NotesMIMEHeader(NotesSession session, NotesMIMEEntity entity, int index) : base(session) { _entity = entity; _index = index; }
    public NotesMIMEEntity Parent { get { EnsureAlive(); return _entity; } }
    public string HeaderName { get { EnsureAlive(); return _entity.HeaderAt(_index).Name; } }
    public string GetHeaderVal() { EnsureAlive(); return MimeMessage.ValueWithoutParams(_entity.HeaderAt(_index).Value); }
    public string GetHeaderValAndParams() { EnsureAlive(); return _entity.HeaderAt(_index).Value; }
    public string GetParamVal(string name) { EnsureAlive(); return MimeMessage.GetParameter(_entity.HeaderAt(_index).Value, name); }
    public void SetHeaderVal(string value) { EnsureAlive(); _entity.SetHeader(_index, value ?? string.Empty); }
    public void SetHeaderValAndParams(string value) => SetHeaderVal(value);
    public void AddValText(string value) { EnsureAlive(); _entity.SetHeader(_index, _entity.HeaderAt(_index).Value + (value ?? string.Empty)); }
    public void SetParamVal(string name, string value) { EnsureAlive(); _entity.SetHeader(_index, MimeMessage.SetParameter(_entity.HeaderAt(_index).Value, name, value)); }
    public void Remove() { EnsureAlive(); _entity.RemoveHeader(_index); Recycle(); }
    protected override void ReleaseNative() { _index = -1; }
}

internal sealed class MimeHeaderValue(string name, string value) { internal string Name { get; } = name; internal string Value { get; set; } = value; }

internal sealed class MimeMessage
{
    internal List<MimeHeaderValue> Headers { get; } = [];
    internal byte[] Body { get; set; } = [];
    internal string Preamble { get; set; } = string.Empty;
    internal string HeadersText => string.Join("\r\n", Headers.Select(h => h.Name + ": " + h.Value));
    internal string ContentType => SplitContentType(GetHeader("Content-Type")).type;
    internal string ContentSubType => SplitContentType(GetHeader("Content-Type")).subtype;
    internal string Charset => GetParameter(GetHeader("Content-Type"), "charset");
    internal string TransferEncoding => GetHeader("Content-Transfer-Encoding");
    internal string Boundary => GetParameter(GetHeader("Content-Type"), "boundary");

    internal static MimeMessage Parse(byte[] raw)
    {
        var result = new MimeMessage(); var separator = FindHeaderEnd(raw, out var separatorLength); var headerBytes = separator < 0 ? raw : raw[..separator]; result.Body = separator < 0 ? [] : raw[(separator + separatorLength)..];
        var text = Encoding.Latin1.GetString(headerBytes).Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n'); MimeHeaderValue? current = null;
        foreach (var line in text.Split('\n')) { if ((line.StartsWith(' ') || line.StartsWith('\t')) && current is not null) { current.Value += " " + line.Trim(); continue; } var colon = line.IndexOf(':'); if (colon <= 0) continue; current = new MimeHeaderValue(line[..colon].Trim(), line[(colon + 1)..].Trim()); result.Headers.Add(current); }
        if (result.ContentType.Equals("multipart", StringComparison.OrdinalIgnoreCase) && result.Boundary.Length > 0) { var marker = Encoding.Latin1.GetBytes("--" + result.Boundary); var p = IndexOf(result.Body, marker, 0); if (p > 0) result.Preamble = Encoding.Latin1.GetString(result.Body[..p]).TrimEnd('\r', '\n'); }
        return result;
    }
    internal byte[] Serialize() { var head = Encoding.Latin1.GetBytes(HeadersText + "\r\n\r\n"); var result = new byte[head.Length + Body.Length]; Buffer.BlockCopy(head, 0, result, 0, head.Length); Buffer.BlockCopy(Body, 0, result, head.Length, Body.Length); return result; }
    internal string GetHeader(string name) => Headers.FirstOrDefault(h => h.Name.Equals(name, StringComparison.OrdinalIgnoreCase))?.Value ?? string.Empty;
    internal void SetHeader(string name, string value) { var h = Headers.FirstOrDefault(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase)); if (h is null) Headers.Add(new MimeHeaderValue(name, value)); else h.Value = value; }
    internal byte[] GetDecodedBytes() { var encoding = TransferEncoding.Trim(); if (encoding.Equals("base64", StringComparison.OrdinalIgnoreCase)) { try { return Convert.FromBase64String(Encoding.ASCII.GetString(Body)); } catch { return Body; } } if (encoding.Equals("quoted-printable", StringComparison.OrdinalIgnoreCase)) return DecodeQuotedPrintable(Body); return Body; }
    internal string GetDecodedText() { var bytes = GetDecodedBytes(); if (Charset.Length == 0) return Encoding.UTF8.GetString(bytes); try { return Encoding.GetEncoding(Charset).GetString(bytes); } catch { return Encoding.UTF8.GetString(bytes); } }
    internal void SetDecodedContent(byte[] data, int encoding) { switch (encoding) { case 1727: SetHeader("Content-Transfer-Encoding", "base64"); Body = Encoding.ASCII.GetBytes(Convert.ToBase64String(data, Base64FormattingOptions.InsertLineBreaks)); break; case 1726: SetHeader("Content-Transfer-Encoding", "quoted-printable"); Body = EncodeQuotedPrintable(data); break; case 1730: SetHeader("Content-Transfer-Encoding", "binary"); Body = data; break; default: SetHeader("Content-Transfer-Encoding", "8bit"); Body = data; break; } }
    internal static int EncodingConstant(string value) => value.Trim().ToLowerInvariant() switch { "7bit" => 1725, "8bit" => 1725, "quoted-printable" => 1726, "base64" => 1727, "binary" => 1730, _ => 0 };
    internal static string ValueWithoutParams(string value) { var i = value.IndexOf(';'); return (i < 0 ? value : value[..i]).Trim(); }
    internal static string GetParameter(string value, string name) { foreach (var part in value.Split(';').Skip(1)) { var eq = part.IndexOf('='); if (eq <= 0 || !part[..eq].Trim().Equals(name?.Trim(), StringComparison.OrdinalIgnoreCase)) continue; return part[(eq + 1)..].Trim().Trim('"'); } return string.Empty; }
    internal static string SetParameter(string value, string name, string parameterValue) { name = name?.Trim() ?? string.Empty; var baseValue = ValueWithoutParams(value); var parts = value.Split(';').Skip(1).Select(p => p.Trim()).Where(p => p.Length > 0).ToList(); var replacement = name + "=\"" + (parameterValue ?? string.Empty).Replace("\"", "\\\"") + "\""; var replaced = false; for (var i = 0; i < parts.Count; i++) { var eq = parts[i].IndexOf('='); if (eq > 0 && parts[i][..eq].Trim().Equals(name, StringComparison.OrdinalIgnoreCase)) { parts[i] = replacement; replaced = true; } } if (!replaced) parts.Add(replacement); return baseValue + (parts.Count == 0 ? string.Empty : "; " + string.Join("; ", parts)); }
    private static (string type, string subtype) SplitContentType(string value) { var main = ValueWithoutParams(value); var slash = main.IndexOf('/'); return slash < 0 ? (main, string.Empty) : (main[..slash].Trim(), main[(slash + 1)..].Trim()); }
    private static int FindHeaderEnd(byte[] data, out int length) { for (var i = 0; i + 3 < data.Length; i++) if (data[i] == 13 && data[i+1] == 10 && data[i+2] == 13 && data[i+3] == 10) { length = 4; return i; } for (var i = 0; i + 1 < data.Length; i++) if (data[i] == 10 && data[i+1] == 10) { length = 2; return i; } length = 0; return -1; }
    private static int IndexOf(byte[] data, byte[] pattern, int start) { for (var i = start; i <= data.Length - pattern.Length; i++) { var ok = true; for (var j = 0; j < pattern.Length; j++) if (data[i+j] != pattern[j]) { ok = false; break; } if (ok) return i; } return -1; }
    private static byte[] DecodeQuotedPrintable(byte[] input) { using var output = new MemoryStream(); for (var i = 0; i < input.Length; i++) { if (input[i] == '=' && i + 1 < input.Length && (input[i+1] == '\r' || input[i+1] == '\n')) { if (input[i+1] == '\r' && i + 2 < input.Length && input[i+2] == '\n') i += 2; else i++; continue; } if (input[i] == '=' && i + 2 < input.Length && TryHex(input[i+1], out var hi) && TryHex(input[i+2], out var lo)) { output.WriteByte((byte)((hi << 4) | lo)); i += 2; } else output.WriteByte(input[i]); } return output.ToArray(); }
    private static byte[] EncodeQuotedPrintable(byte[] input) { var sb = new StringBuilder(); foreach (var b in input) { if ((b >= 33 && b <= 60) || (b >= 62 && b <= 126) || b is 9 or 32 or 13 or 10) sb.Append((char)b); else sb.Append('=').Append(b.ToString("X2", System.Globalization.CultureInfo.InvariantCulture)); } return Encoding.ASCII.GetBytes(sb.ToString()); }
    private static bool TryHex(byte b, out int value) { if (b >= '0' && b <= '9') { value = b - '0'; return true; } if (b >= 'A' && b <= 'F') { value = b - 'A' + 10; return true; } if (b >= 'a' && b <= 'f') { value = b - 'a' + 10; return true; } value = 0; return false; }
}
