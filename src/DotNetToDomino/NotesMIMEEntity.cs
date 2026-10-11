namespace DotNetToDomino;

/// <summary>Represents a MIME entity backed by Domino's native MIME directory.</summary>
public sealed class NotesMIMEEntity : NotesObject
{
    private readonly NotesDocument _document;
    private readonly string _itemName;
    private readonly NotesMimeDirectoryOwner _owner;
    private readonly nint _nativeEntity;

    internal NotesMIMEEntity(NotesSession session, NotesDocument document, string itemName) : base(session)
    {
        _document = document;
        _itemName = itemName;
        if (!string.Equals(itemName, "Body", StringComparison.OrdinalIgnoreCase))
            throw new NotSupportedException("Native NotesMIMEEntity directory access currently supports the Body item only; MIMEOpenDirectory is note-level.");
        _owner = document.GetMimeDirectoryOwner();
        _nativeEntity = _owner.RootEntity;
    }

    private NotesMIMEEntity(NotesSession session, NotesDocument document, string itemName, NotesMimeDirectoryOwner owner, nint entity) : base(session)
    { _document = document; _itemName = itemName; _owner = owner; _nativeEntity = entity; }

    public NotesDocument Parent { get { EnsureEntityAlive(); return _document; } }
    public string BoundaryStart { get { var b = TypeParam(NotesConst.MIME_SYMBOL_BOUNDARY); return b.Length == 0 ? string.Empty : "--" + b; } }
    public string BoundaryEnd { get { var b = TypeParam(NotesConst.MIME_SYMBOL_BOUNDARY); return b.Length == 0 ? string.Empty : "--" + b + "--"; } }
    public string Charset => TypeParam(NotesConst.MIME_SYMBOL_CHARSET);
    public string ContentType { get { EnsureEntityAlive(); return MimeSymbolText(_owner.ContentTypeSymbol(_nativeEntity)); } }
    public string ContentSubType { get { EnsureEntityAlive(); return MimeSymbolText(_owner.ContentSubtypeSymbol(_nativeEntity)); } }
    public string ContentID { get { EnsureEntityAlive(); return Session.Api.GetMimeContentId(_nativeEntity); } }
    public string ContentLocation { get { EnsureEntityAlive(); return Session.Api.GetMimeContentLocation(_nativeEntity); } }
    public bool IsMultipart { get { EnsureEntityAlive(); return Session.Api.IsMimeEntityMultipart(_nativeEntity); } }
    public bool IsDiscretePart { get { EnsureEntityAlive(); return Session.Api.IsMimeEntityDiscrete(_nativeEntity); } }
    public bool IsMessagePart { get { EnsureEntityAlive(); return Session.Api.IsMimeEntityMessage(_nativeEntity); } }

    public NotesMIMEEntity? GetFirstChildEntity() => Wrap(_owner.FirstSubpart(CheckedEntity()));
    public NotesMIMEEntity? GetParentEntity() => Wrap(_owner.Parent(CheckedEntity()));
    public NotesMIMEEntity? GetNextSibling() => Wrap(_owner.NextSibling(CheckedEntity()));
    public NotesMIMEEntity? GetPrevSibling() => Wrap(_owner.PrevSibling(CheckedEntity()));
    public NotesMIMEEntity? GetNextEntity() { EnsureEntityAlive(); return Wrap(_owner.IterateNext(_owner.RootEntity, _nativeEntity)); }
    public NotesMIMEEntity? GetNextEntity(int search)
    {
        if (search != NotesConst.SEARCH_DEPTH) throw new NotSupportedException("NotesMIMEEntity.GetNextEntity currently supports SEARCH_DEPTH only; Domino MIMEIterateNext is depth-first.");
        return GetNextEntity();
    }

    public string ContentAsText { get { EnsureEntityAlive(); return ReadRootText(nameof(ContentAsText)); } }
    public string Headers { get { EnsureEntityAlive(); return ReadRootHeaders(null); } }
    public string Preamble { get => throw Unsupported(nameof(Preamble)); set => throw Unsupported(nameof(Preamble)); }
    public int Encoding
    {
        get
        {
            EnsureEntityAlive();
            var value = GetRootHeader(ReadRootEntity(nameof(Encoding)), "Content-Transfer-Encoding").Trim();
            return value.Equals("base64", StringComparison.OrdinalIgnoreCase) ? 1727
                : value.Equals("quoted-printable", StringComparison.OrdinalIgnoreCase) ? 1726
                : value.Equals("7bit", StringComparison.OrdinalIgnoreCase) ? 1724
                : value.Equals("8bit", StringComparison.OrdinalIgnoreCase) ? 1725
                : value.Equals("binary", StringComparison.OrdinalIgnoreCase) ? 1728
                : 1725;
        }
    }
    public NotesMIMEHeader? GetNthHeader(string name, int occurrence = 1)
    {
        EnsureRootEntity(nameof(GetNthHeader));
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (occurrence < 1) throw new ArgumentOutOfRangeException(nameof(occurrence));
        var wanted = name.Trim();
        var found = 0;
        foreach (var header in ReadRootHeaderValues())
        {
            if (!header.Name.Equals(wanted, StringComparison.OrdinalIgnoreCase)) continue;
            if (++found == occurrence) return new NotesMIMEHeader(Session, this, wanted, occurrence);
        }
        return null;
    }
    public NotesMIMEHeader CreateHeader(string name)
    {
        EnsureRootEntity(nameof(CreateHeader));
        ValidateHeaderName(name);
        var headers = ReadRootHeaderValues();
        var normalizedName = name.Trim();
        headers.Add((normalizedName, string.Empty));
        RewriteRootHeaders(headers);
        var occurrence = headers.Count(header => header.Name.Equals(normalizedName, StringComparison.OrdinalIgnoreCase));
        return new NotesMIMEHeader(Session, this, normalizedName, occurrence);
    }
    public string GetSomeHeaders(string headerNames) { EnsureEntityAlive(); return ReadRootHeaders(headerNames); }
    public void RemoveHeaders(string name)
    {
        EnsureRootEntity(nameof(RemoveHeaders));
        ValidateHeaderName(name);
        var wanted = name.Trim();
        var headers = ReadRootHeaderValues();
        headers.RemoveAll(header => header.Name.Equals(wanted, StringComparison.OrdinalIgnoreCase));
        RewriteRootHeaders(headers);
    }
    public void GetContentAsBytes(NotesStream stream) { EnsureEntityAlive(); ArgumentNullException.ThrowIfNull(stream); stream.Write(ReadRootContent(nameof(GetContentAsBytes))); }
    public void GetContentAsText(NotesStream stream, bool entityCharset = false) { EnsureEntityAlive(); ArgumentNullException.ThrowIfNull(stream); stream.WriteText(ReadRootText(nameof(GetContentAsText))); }
    public void GetEntityAsText(NotesStream stream) { EnsureEntityAlive(); ArgumentNullException.ThrowIfNull(stream); stream.Write(ReadRootEntity(nameof(GetEntityAsText))); }
    public void SetContentFromText(NotesStream stream, string contentType, int encoding) => throw Unsupported(nameof(SetContentFromText));
    public void SetContentFromBytes(NotesStream stream, string contentType, int encoding) => throw Unsupported(nameof(SetContentFromBytes));
    public void EncodeContent(int encoding) => throw Unsupported(nameof(EncodeContent));
    public void DecodeContent() => throw Unsupported(nameof(DecodeContent));

    public void Remove()
    {
        EnsureEntityAlive();
        if (_nativeEntity != _owner.RootEntity) throw new NotSupportedException("Removing child MIME entities requires verified native per-entity mutation support.");
        _document.InvalidateMimeDirectory();
        _document.RemoveItem(_itemName);
        Recycle();
    }

    private void EnsureRootEntity(string member)
    {
        EnsureEntityAlive();
        if (_nativeEntity != _owner.RootEntity)
            throw new NotSupportedException("NotesMIMEEntity." + member + " currently supports the root entity only; child-entity data access is not yet verified.");
    }

    private byte[] ReadRootEntity(string member)
    {
        EnsureRootEntity(member);
        return Session.Api.ReadMimeStream(_document.Handle, _itemName);
    }

    private byte[] ReadRootContent(string member)
    {
        var raw = ReadRootEntity(member);
        var bodyOffset = FindRootBodyOffset(raw);
        var body = bodyOffset >= raw.Length ? [] : raw[bodyOffset..];
        var transferEncoding = GetRootHeader(raw, "Content-Transfer-Encoding").Trim();
        if (transferEncoding.Equals("base64", StringComparison.OrdinalIgnoreCase))
        {
            try { return Convert.FromBase64String(System.Text.Encoding.ASCII.GetString(body)); }
            catch (FormatException ex) { throw new NotesException(5, "Invalid base64 MIME root content: " + ex.Message); }
        }
        if (transferEncoding.Equals("quoted-printable", StringComparison.OrdinalIgnoreCase))
            return DecodeQuotedPrintable(body);
        return body;
    }

    private string ReadRootText(string member)
    {
        var content = ReadRootContent(member);
        var charset = Charset.Trim();
        if (charset.Length == 0) return System.Text.Encoding.UTF8.GetString(content);
        try { return System.Text.Encoding.GetEncoding(charset).GetString(content); }
        catch (ArgumentException ex) { throw new NotesException(5, "Unsupported MIME root charset '" + charset + "': " + ex.Message); }
    }

    private string ReadRootHeaders(string? headerNames)
    {
        var raw = ReadRootEntity(nameof(Headers));
        var bodyOffset = FindRootBodyOffset(raw);
        var headerBytes = raw[..Math.Min(bodyOffset, raw.Length)];
        var text = System.Text.Encoding.Latin1.GetString(headerBytes).TrimEnd('\r', '\n');
        if (string.IsNullOrWhiteSpace(headerNames)) return text;
        var wanted = new HashSet<string>(headerNames.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries), StringComparer.OrdinalIgnoreCase);
        if (wanted.Count == 0) return text;
        var result = new List<string>();
        string? currentName = null;
        var current = new System.Text.StringBuilder();
        void Flush()
        {
            if (currentName is not null && wanted.Contains(currentName)) result.Add(current.ToString());
            currentName = null; current.Clear();
        }
        foreach (var line in text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n'))
        {
            if ((line.StartsWith(' ') || line.StartsWith('\t')) && currentName is not null) { current.Append("\r\n").Append(line); continue; }
            Flush();
            var colon = line.IndexOf(':');
            if (colon <= 0) continue;
            currentName = line[..colon].Trim();
            current.Append(line);
        }
        Flush();
        return string.Join("\r\n", result);
    }

    private static string GetRootHeader(byte[] raw, string name)
    {
        var bodyOffset = FindRootBodyOffset(raw);
        var text = System.Text.Encoding.Latin1.GetString(raw, 0, Math.Max(0, Math.Min(raw.Length, bodyOffset)))
            .Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        string currentName = string.Empty;
        var currentValue = new System.Text.StringBuilder();
        foreach (var line in text.Split('\n'))
        {
            if ((line.StartsWith(' ') || line.StartsWith('\t')) && currentName.Length > 0) { currentValue.Append(' ').Append(line.Trim()); continue; }
            if (currentName.Equals(name, StringComparison.OrdinalIgnoreCase)) return currentValue.ToString();
            var colon = line.IndexOf(':');
            if (colon <= 0) { currentName = string.Empty; currentValue.Clear(); continue; }
            currentName = line[..colon].Trim(); currentValue.Clear(); currentValue.Append(line[(colon + 1)..].Trim());
        }
        return currentName.Equals(name, StringComparison.OrdinalIgnoreCase) ? currentValue.ToString() : string.Empty;
    }

    internal string ReadHeader(string name, int occurrence)
    {
        EnsureRootEntity(nameof(ReadHeader));
        var found = 0;
        foreach (var header in ReadRootHeaderValues())
        {
            if (!header.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) continue;
            if (++found == occurrence) return header.Value;
        }
        throw new NotesException(5, "MIME header is no longer present on the entity.");
    }

    internal void SetHeader(string name, int occurrence, string value, bool remove)
    {
        EnsureRootEntity(nameof(SetHeader));
        ValidateHeaderValue(value);
        var headers = ReadRootHeaderValues();
        var index = -1;
        var found = 0;
        for (var i = 0; i < headers.Count; i++)
        {
            if (!headers[i].Name.Equals(name, StringComparison.OrdinalIgnoreCase)) continue;
            if (++found == occurrence) { index = i; break; }
        }
        if (index < 0) throw new NotesException(5, "MIME header is no longer present on the entity.");
        if (remove) headers.RemoveAt(index); else headers[index] = (headers[index].Name, value);
        RewriteRootHeaders(headers);
    }

    private void RewriteRootHeaders(List<(string Name, string Value)> headers)
    {
        var raw = ReadRootEntity(nameof(RewriteRootHeaders));
        var bodyOffset = FindRootBodyOffset(raw);
        var body = bodyOffset >= raw.Length ? [] : raw[bodyOffset..];
        var headerText = headers.Count == 0 ? string.Empty : string.Join("\r\n", headers.Select(header => header.Name + ": " + header.Value)) + "\r\n\r\n";
        var headerBytes = System.Text.Encoding.Latin1.GetBytes(headerText);
        var rewritten = new byte[headerBytes.Length + body.Length];
        Buffer.BlockCopy(headerBytes, 0, rewritten, 0, headerBytes.Length);
        Buffer.BlockCopy(body, 0, rewritten, headerBytes.Length, body.Length);
        Session.Api.WriteMimeStream(_document.Handle, _itemName, rewritten);
        _document.InvalidateMimeDirectory();
    }

    private static void ValidateHeaderName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Contains(':') || name.Contains('\r') || name.Contains('\n'))
            throw new NotesException(5, "Invalid MIME header name.");
    }

    private static void ValidateHeaderValue(string? value)
    {
        if (value is not null && (value.Contains('\r') || value.Contains('\n')))
            throw new NotesException(5, "Invalid MIME header value.");
    }

    private List<(string Name, string Value)> ReadRootHeaderValues()
    {
        var raw = ReadRootEntity(nameof(ReadRootHeaderValues));
        var bodyOffset = FindRootBodyOffset(raw);
        var text = System.Text.Encoding.Latin1.GetString(raw, 0, Math.Max(0, Math.Min(raw.Length, bodyOffset)))
            .Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        var result = new List<(string Name, string Value)>();
        string? currentName = null;
        var currentValue = new System.Text.StringBuilder();
        void Flush()
        {
            if (currentName is not null) result.Add((currentName, currentValue.ToString()));
            currentName = null;
            currentValue.Clear();
        }
        foreach (var line in text.Split('\n'))
        {
            if ((line.StartsWith(' ') || line.StartsWith('\t')) && currentName is not null)
            { currentValue.Append(' ').Append(line.Trim()); continue; }
            Flush();
            var colon = line.IndexOf(':');
            if (colon <= 0) continue;
            currentName = line[..colon].Trim();
            currentValue.Append(line[(colon + 1)..].Trim());
        }
        Flush();
        return result;
    }

    private static int FindRootBodyOffset(byte[] raw)
    {
        for (var i = 0; i + 3 < raw.Length; i++)
            if (raw[i] == 13 && raw[i + 1] == 10 && raw[i + 2] == 13 && raw[i + 3] == 10) return i + 4;
        for (var i = 0; i + 1 < raw.Length; i++)
            if (raw[i] == 10 && raw[i + 1] == 10) return i + 2;
        return raw.Length;
    }

    private static byte[] DecodeQuotedPrintable(byte[] input)
    {
        using var output = new MemoryStream();
        for (var i = 0; i < input.Length; i++)
        {
            if (input[i] == '=' && i + 1 < input.Length && (input[i + 1] == '\r' || input[i + 1] == '\n'))
            {
                if (input[i + 1] == '\r' && i + 2 < input.Length && input[i + 2] == '\n') i += 2; else i++;
                continue;
            }
            if (input[i] == '=' && i + 2 < input.Length && TryHex(input[i + 1], out var hi) && TryHex(input[i + 2], out var lo))
            { output.WriteByte((byte)((hi << 4) | lo)); i += 2; continue; }
            output.WriteByte(input[i]);
        }
        return output.ToArray();
    }

    private static bool TryHex(byte value, out int result)
    {
        if (value >= '0' && value <= '9') { result = value - '0'; return true; }
        if (value >= 'A' && value <= 'F') { result = value - 'A' + 10; return true; }
        if (value >= 'a' && value <= 'f') { result = value - 'a' + 10; return true; }
        result = 0; return false;
    }

    private string TypeParam(int symbol) { EnsureEntityAlive(); return _owner.TypeParam(_nativeEntity, symbol); }
    private nint CheckedEntity() { EnsureEntityAlive(); return _nativeEntity; }
    private NotesMIMEEntity? Wrap(nint entity) => entity == 0 ? null : new NotesMIMEEntity(Session, _document, _itemName, _owner, entity);
    private void EnsureEntityAlive() { EnsureAlive(); _ = _document.Handle; if (!_owner.IsAlive) throw new ObjectDisposedException(nameof(NotesMIMEEntity)); }
    private static NotSupportedException Unsupported(string member) => new($"NotesMIMEEntity.{member} requires verified Domino native per-entity MIME data/header support; managed root-stream parsing is intentionally not used.");
    protected override void ReleaseNative() { }

    private static string MimeSymbolText(int symbol) => symbol switch
    {
        NotesConst.MIME_SYMBOL_TEXT => "text", NotesConst.MIME_SYMBOL_MULTIPART => "multipart", NotesConst.MIME_SYMBOL_MESSAGE => "message", NotesConst.MIME_SYMBOL_APPLICATION => "application", NotesConst.MIME_SYMBOL_IMAGE => "image", NotesConst.MIME_SYMBOL_AUDIO => "audio", NotesConst.MIME_SYMBOL_VIDEO => "video", NotesConst.MIME_SYMBOL_PLAIN => "plain", NotesConst.MIME_SYMBOL_OCTET_STREAM => "octet-stream", NotesConst.MIME_SYMBOL_HTML => "html", NotesConst.MIME_SYMBOL_ALTERNATIVE => "alternative", NotesConst.MIME_SYMBOL_MIXED => "mixed", _ => string.Empty
    };
}

/// <summary>Represents a root MIME header backed by the entity's serialized native MIME stream.</summary>
public sealed class NotesMIMEHeader : NotesObject
{
    private readonly NotesMIMEEntity _entity;
    private readonly string _name;
    private readonly int _occurrence;

    internal NotesMIMEHeader(NotesSession session, NotesMIMEEntity entity, string name, int occurrence) : base(session)
    { _entity = entity ?? throw new ArgumentNullException(nameof(entity)); _name = name; _occurrence = occurrence; }

    /// <summary>Gets the MIME header name.</summary>
    public string HeaderName { get { EnsureAlive(); _ = _entity.ReadHeader(_name, _occurrence); return _name; } }
    /// <summary>Gets the MIME header value.</summary>
    public string GetHeaderVal() { EnsureAlive(); return _entity.ReadHeader(_name, _occurrence); }
    /// <summary>Gets the MIME header value including parameters.</summary>
    public string GetHeaderValAndParams() => GetHeaderVal();
    /// <summary>Gets a semicolon-delimited MIME parameter value.</summary>
    public string GetParamVal(string parameterName)
    {
        EnsureAlive();
        ArgumentException.ThrowIfNullOrWhiteSpace(parameterName);
        var wanted = parameterName.Trim();
        foreach (var part in GetHeaderVal().Split(';').Skip(1))
        {
            var equals = part.IndexOf('=');
            if (equals <= 0 || !part[..equals].Trim().Equals(wanted, StringComparison.OrdinalIgnoreCase)) continue;
            return part[(equals + 1)..].Trim().Trim('"');
        }
        return string.Empty;
    }

    /// <summary>Replaces the MIME header value.</summary>
    public void SetHeaderVal(string? value) { EnsureAlive(); _entity.SetHeader(_name, _occurrence, value ?? string.Empty, false); }
    /// <summary>Replaces the MIME header value including parameters.</summary>
    public void SetHeaderValAndParams(string? value) => SetHeaderVal(value);
    /// <summary>Appends text to the MIME header value.</summary>
    public void AddValText(string? value) { EnsureAlive(); SetHeaderVal(GetHeaderVal() + (value ?? string.Empty)); }
    /// <summary>Removes this MIME header.</summary>
    public void Remove() { EnsureAlive(); _entity.SetHeader(_name, _occurrence, string.Empty, true); Recycle(); }

    protected override void ReleaseNative() { }
}
