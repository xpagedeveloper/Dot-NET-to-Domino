using System.Globalization;

namespace DotNetToDomino;

/// <summary>Represents a MIME entity backed by Domino's native MIME directory.</summary>
public sealed class NotesMIMEEntity : NotesObject
{
    private readonly NotesDocument _document;
    private readonly string _itemName;
    private NotesMimeDirectoryOwner _owner;
    private nint _nativeEntity;
    private readonly bool _isRoot;

    internal NotesMIMEEntity(NotesSession session, NotesDocument document, string itemName) : base(session)
    {
        _document = document;
        _itemName = itemName;
        if (!string.Equals(itemName, "Body", StringComparison.OrdinalIgnoreCase))
            throw new NotSupportedException("Native NotesMIMEEntity directory access currently supports the Body item only; MIMEOpenDirectory is note-level.");
        _owner = document.GetMimeDirectoryOwner();
        _nativeEntity = _owner.RootEntity;
        _isRoot = true;
    }

    private NotesMIMEEntity(NotesSession session, NotesDocument document, string itemName, NotesMimeDirectoryOwner owner, nint entity) : base(session)
    { _document = document; _itemName = itemName; _owner = owner; _nativeEntity = entity; _isRoot = entity == owner.RootEntity; }

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

    public NotesMIMEEntity? GetFirstChildEntity() { var entity = CheckedEntity(); return Wrap(_owner.FirstSubpart(entity)); }
    public NotesMIMEEntity? GetParentEntity() { var entity = CheckedEntity(); return Wrap(_owner.Parent(entity)); }
    public NotesMIMEEntity? GetNextSibling() { var entity = CheckedEntity(); return Wrap(_owner.NextSibling(entity)); }
    public NotesMIMEEntity? GetPrevSibling() { var entity = CheckedEntity(); return Wrap(_owner.PrevSibling(entity)); }
    public IReadOnlyList<NotesMIMEEntity> GetChildren()
    {
        EnsureEntityAlive();
        var result = new List<NotesMIMEEntity>();
        var child = Wrap(_owner.FirstSubpart(_nativeEntity));
        while (child is not null)
        {
            result.Add(child);
            child = child.GetNextSibling();
        }
        return result;
    }
    /// <summary>Creates a direct text child on the root multipart entity.</summary>
    public NotesMIMEEntity CreateChildEntity()
    {
        EnsureEntityAlive();
        if (_nativeEntity != _owner.RootEntity) return CreateNestedChildEntity();
        EnsureRootEntity(nameof(CreateChildEntity));
        var raw = ReadRootEntity(nameof(CreateChildEntity));
        var boundary = _owner.TypeParam(_owner.RootEntity, NotesConst.MIME_SYMBOL_BOUNDARY).Trim();
        if (boundary.Length == 0)
        {
            boundary = "----=_DotNetToDomino_" + Guid.NewGuid().ToString("N");
            var headerEnd = FindHeaderEnd(raw);
            var headers = System.Text.Encoding.ASCII.GetString(raw, 0, headerEnd);
            headers = System.Text.RegularExpressions.Regex.Replace(headers, "(?im)^Content-Type:.*$", "Content-Type: multipart/mixed; boundary=\"" + boundary + "\"");
            raw = System.Text.Encoding.ASCII.GetBytes(headers + "\r\n" + "\r\n" + "--" + boundary + "--\r\n");
        }
        var headerEndOffset = FindHeaderEnd(raw);
        var prefix = raw[..headerEndOffset];
        var existingBody = raw[headerEndOffset..];
        var marker = System.Text.Encoding.ASCII.GetBytes("--" + boundary + "--");
        var close = FindBytes(existingBody, marker, 0);
        if (close < 0) throw new NotesException(5, "MIME multipart boundary is missing.");
        var inserted = System.Text.Encoding.ASCII.GetBytes("--" + boundary + "\r\nContent-Type: text/plain; charset=UTF-8\r\nContent-Transfer-Encoding: 8bit\r\n\r\n\r\n");
        var output = new byte[prefix.Length + close + inserted.Length + existingBody.Length - close];
        Buffer.BlockCopy(prefix, 0, output, 0, prefix.Length);
        Buffer.BlockCopy(existingBody, 0, output, prefix.Length, close);
        Buffer.BlockCopy(inserted, 0, output, prefix.Length + close, inserted.Length);
        Buffer.BlockCopy(existingBody, close, output, prefix.Length + close + inserted.Length, existingBody.Length - close);
        Session.Api.WriteMimeStream(_document.ParentDatabase.Handle, _document.Handle, _itemName, output);
        _document.InvalidateMimeDirectory();
        var reopened = _document.GetMIMEEntity(_itemName) ?? throw new NotesException(5, "MIME entity could not be reopened.");
        return reopened.GetChildren().LastOrDefault() ?? throw new NotesException(5, "MIME child could not be reopened.");
    }

    /// <summary>Removes a direct or nested child belonging to this MIME entity.</summary>
    public void RemoveChildEntity(NotesMIMEEntity child)
    {
        ArgumentNullException.ThrowIfNull(child);
        EnsureEntityAlive();
        child.EnsureEntityAlive();
        if (!ReferenceEquals(child._document, _document) || !ReferenceEquals(child._owner, _owner))
            throw new NotesException(5, "MIME child belongs to another MIME directory.");
        var parent = child.GetParentEntity();
        if (parent is null || !ReferenceEquals(parent._document, _document))
            throw new NotesException(5, "MIME child does not belong to this entity.");
        if (_nativeEntity != _owner.RootEntity && child.GetEntityPath().Length != GetEntityPath().Length + 1)
            throw new NotesException(5, "MIME child must be a direct child of this entity.");
        child.Remove();
    }

    /// <summary>Moves an existing direct child to the end of this multipart entity.</summary>
    public void AppendChildEntity(NotesMIMEEntity child)
    {
        ArgumentNullException.ThrowIfNull(child);
        EnsureEntityAlive();
        child.EnsureEntityAlive();
        if (!ReferenceEquals(child._document, _document) || !ReferenceEquals(child._owner, _owner))
            throw new NotesException(5, "MIME child belongs to another MIME directory.");
        var parentPath = GetEntityPath();
        var childPath = child.GetEntityPath();
        if (childPath.Length != parentPath.Length + 1 || !childPath[..^1].SequenceEqual(parentPath))
            throw new NotesException(5, "AppendChildEntity child must be a direct child of this entity.");
        var raw = Session.Api.ReadMimeStream(_document.Handle, _itemName);
        var parent = GetSerializedEntity(raw, parentPath, nameof(AppendChildEntity));
        var offset = FindRootBodyOffset(parent);
        var headers = ParseHeaderValues(parent, offset);
        var contentType = headers.FirstOrDefault(h => h.Name.Equals("Content-Type", StringComparison.OrdinalIgnoreCase)).Value ?? string.Empty;
        var boundary = contentType.Split(';').Skip(1).Select(p => p.Trim()).FirstOrDefault(p => p.StartsWith("boundary=", StringComparison.OrdinalIgnoreCase))?.Split('=', 2).ElementAtOrDefault(1)?.Trim().Trim('"') ?? string.Empty;
        if (boundary.Length == 0) throw new NotesException(5, "MIME entity is not multipart.");
        var children = SplitDirectChildren(parent, boundary);
        var moved = children[childPath[^1]];
        children.RemoveAt(childPath[^1]);
        children.Add(moved);
        var rebuilt = System.Text.Encoding.Latin1.GetBytes(string.Join("\r\n", headers.Select(h => h.Name + ": " + h.Value)) + "\r\n\r\n").Concat(BuildMultipartBody(children, boundary)).ToArray();
        Session.Api.WriteMimeStream(_document.ParentDatabase.Handle, _document.Handle, _itemName, ReplaceSerializedEntity(raw, parentPath, rebuilt));
        _document.InvalidateMimeDirectory();
    }

    private NotesMIMEEntity CreateNestedChildEntity()
    {
        var raw = Session.Api.ReadMimeStream(_document.Handle, _itemName);
        var path = GetEntityPath();
        var target = GetSerializedEntity(raw, path, nameof(CreateChildEntity));
        var bodyOffset = FindRootBodyOffset(target);
        var headers = ParseHeaderValues(target, bodyOffset);
        var contentType = headers.FirstOrDefault(h => h.Name.Equals("Content-Type", StringComparison.OrdinalIgnoreCase)).Value ?? string.Empty;
        var boundary = contentType.Split(';').Skip(1).Select(p => p.Trim()).FirstOrDefault(p => p.StartsWith("boundary=", StringComparison.OrdinalIgnoreCase))?.Split('=', 2).ElementAtOrDefault(1)?.Trim().Trim('"') ?? string.Empty;
        if (boundary.Length == 0) throw new NotesException(5, "CreateChildEntity requires a multipart target entity.");
        var children = SplitDirectChildren(target, boundary);
        children.Add(System.Text.Encoding.ASCII.GetBytes("Content-Type: text/plain; charset=UTF-8\r\nContent-Transfer-Encoding: 8bit\r\n\r\n"));
        var replacement = headers.Count == 0 ? BuildMultipartBody(children, boundary) : System.Text.Encoding.Latin1.GetBytes(string.Join("\r\n", headers.Select(h => h.Name + ": " + h.Value)) + "\r\n\r\n").Concat(BuildMultipartBody(children, boundary)).ToArray();
        var updated = ReplaceSerializedEntity(raw, path, replacement);
        Session.Api.WriteMimeStream(_document.ParentDatabase.Handle, _document.Handle, _itemName, updated);
        _document.InvalidateMimeDirectory();
        return _document.GetMIMEEntity(_itemName) ?? throw new NotesException(5, "MIME entity could not be reopened.");
    }

    private static byte[] ReplaceSerializedEntity(byte[] raw, int[] path, byte[] replacement)
    {
        if (path.Length == 0) return replacement;
        var bodyOffset = FindRootBodyOffset(raw);
        var headers = raw[..bodyOffset];
        var contentType = ParseHeaderValues(raw, bodyOffset).FirstOrDefault(h => h.Name.Equals("Content-Type", StringComparison.OrdinalIgnoreCase)).Value ?? string.Empty;
        var boundary = contentType.Split(';').Skip(1).Select(p => p.Trim()).FirstOrDefault(p => p.StartsWith("boundary=", StringComparison.OrdinalIgnoreCase))?.Split('=', 2).ElementAtOrDefault(1)?.Trim().Trim('"') ?? string.Empty;
        if (boundary.Length == 0) throw new NotesException(5, "MIME parent is not multipart.");
        var children = SplitDirectChildren(raw, boundary);
        var index = path[0];
        if (index < 0 || index >= children.Count) throw new NotesException(5, "MIME entity path is no longer valid.");
        children[index] = ReplaceSerializedEntity(children[index], path[1..], replacement);
        return headers.Concat(BuildMultipartBody(children, boundary)).ToArray();
    }

    private NotesMIMEEntity ReopenPath(int[] path)
    {
        var root = _document.GetMIMEEntity(_itemName) ?? throw new NotesException(5, "MIME entity could not be reopened.");
        NotesMIMEEntity current = root;
        foreach (var index in path)
            current = current.GetChildren().ElementAtOrDefault(index) ?? throw new NotesException(5, "MIME entity path could not be reopened.");
        return current;
    }
    public NotesMIMEEntity? GetNextEntity() { EnsureEntityAlive(); var root = _owner.RootEntity; return Wrap(_owner.IterateNext(root, _nativeEntity)); }
    public NotesMIMEEntity? GetNextEntity(int search)
    {
        if (search != NotesConst.SEARCH_DEPTH) throw new NotSupportedException("NotesMIMEEntity.GetNextEntity currently supports SEARCH_DEPTH only; Domino MIMEIterateNext is depth-first.");
        return GetNextEntity();
    }

    public string ContentAsText { get { EnsureEntityAlive(); return ReadEntityText(nameof(ContentAsText)); } }
    public string Headers
    {
        get
        {
            EnsureEntityAlive();
            if (_nativeEntity == _owner.RootEntity) return ReadRootHeaders(null);
            var raw = Session.Api.ReadMimeStream(_document.Handle, _itemName);
            var entity = GetSerializedEntity(raw, GetEntityPath(), nameof(Headers));
            var offset = FindRootBodyOffset(entity);
            return System.Text.Encoding.Latin1.GetString(entity, 0, Math.Min(offset, entity.Length)).TrimEnd('\r', '\n');
        }
    }
    public string Preamble
    {
        get => ReadPreamble();
        set => WritePreamble(value);
    }
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
        if (_nativeEntity != _owner.RootEntity)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);
            RequireRange(occurrence, 1, int.MaxValue, nameof(occurrence));
            var raw = Session.Api.ReadMimeStream(_document.Handle, _itemName);
            var boundary = _owner.TypeParam(_owner.RootEntity, NotesConst.MIME_SYMBOL_BOUNDARY).Trim();
            var child = SplitDirectChildren(raw, boundary)[GetDirectChildIndex(nameof(GetNthHeader))];
            var childFound = 0;
            foreach (var header in ParseHeaderValues(child, FindRootBodyOffset(child)))
                if (header.Name.Equals(name.Trim(), StringComparison.OrdinalIgnoreCase) && ++childFound == occurrence)
                    return new NotesMIMEHeader(Session, this, name.Trim(), occurrence);
            return null;
        }
        EnsureRootEntity(nameof(GetNthHeader));
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        RequireRange(occurrence, 1, int.MaxValue, nameof(occurrence));
        var wanted = name.Trim();
        var found = 0;
        foreach (var header in ReadRootHeaderValues())
        {
            if (!header.Name.Equals(wanted, StringComparison.OrdinalIgnoreCase)) continue;
            if (++found == occurrence) return new NotesMIMEHeader(Session, this, wanted, occurrence);
        }
        return null;
    }
    public IReadOnlyList<NotesMIMEHeader> GetHeaders(string? name = null)
    {
        if (_nativeEntity != _owner.RootEntity)
        {
            if (name is not null && string.IsNullOrWhiteSpace(name)) throw new ArgumentException("'name' cannot be empty or whitespace when supplied.", nameof(name));
            var raw = Session.Api.ReadMimeStream(_document.Handle, _itemName);
            var child = GetSerializedEntity(raw, GetEntityPath(), nameof(GetHeaders));
            var childWanted = name?.Trim();
            var childOccurrences = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var childResult = new List<NotesMIMEHeader>();
            foreach (var header in ParseHeaderValues(child, FindRootBodyOffset(child)))
            {
                if (childWanted is not null && !header.Name.Equals(childWanted, StringComparison.OrdinalIgnoreCase)) continue;
                childOccurrences.TryGetValue(header.Name, out var occurrence);
                occurrence++;
                childOccurrences[header.Name] = occurrence;
                childResult.Add(new NotesMIMEHeader(Session, this, header.Name, occurrence));
            }
            return childResult;
        }
        EnsureRootEntity(nameof(GetHeaders));
        if (name is not null && string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("'name' cannot be empty or whitespace when supplied.", nameof(name));
        var wanted = name?.Trim();
        var occurrences = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var result = new List<NotesMIMEHeader>();
        foreach (var header in ReadRootHeaderValues())
        {
            if (wanted is not null && !header.Name.Equals(wanted, StringComparison.OrdinalIgnoreCase)) continue;
            occurrences.TryGetValue(header.Name, out var occurrence);
            occurrence++;
            occurrences[header.Name] = occurrence;
            result.Add(new NotesMIMEHeader(Session, this, header.Name, occurrence));
        }
        return result;
    }
    public NotesMIMEHeader CreateHeader(string name)
    {
        if (_nativeEntity != _owner.RootEntity)
        {
            ValidateHeaderName(name);
            var raw = Session.Api.ReadMimeStream(_document.Handle, _itemName);
            var path = GetEntityPath();
            var entity = GetSerializedEntity(raw, path, nameof(CreateHeader));
            var bodyOffset = FindRootBodyOffset(entity);
            var childHeaders = ParseHeaderValues(entity, bodyOffset);
            var childName = name.Trim();
            childHeaders.Add((childName, string.Empty));
            var body = bodyOffset >= entity.Length ? [] : entity[bodyOffset..];
            var headerText = string.Join("\r\n", childHeaders.Select(h => h.Name + ": " + h.Value)) + "\r\n\r\n";
            var replacement = System.Text.Encoding.Latin1.GetBytes(headerText).Concat(body).ToArray();
            Session.Api.WriteMimeStream(_document.ParentDatabase.Handle, _document.Handle, _itemName, ReplaceSerializedEntity(raw, path, replacement));
            _document.InvalidateMimeDirectory();
            var childOccurrence = childHeaders.Count(h => h.Name.Equals(childName, StringComparison.OrdinalIgnoreCase));
            return new NotesMIMEHeader(Session, ReopenPath(path), childName, childOccurrence);
        }
        EnsureRootEntity(nameof(CreateHeader));
        ValidateHeaderName(name);
        var headers = ReadRootHeaderValues();
        var normalizedName = name.Trim();
        headers.Add((normalizedName, string.Empty));
        RewriteRootHeaders(headers);
        var occurrence = headers.Count(header => header.Name.Equals(normalizedName, StringComparison.OrdinalIgnoreCase));
        return new NotesMIMEHeader(Session, this, normalizedName, occurrence);
    }
    public string GetSomeHeaders(string headerNames)
    {
        EnsureEntityAlive();
        if (_nativeEntity == _owner.RootEntity) return ReadRootHeaders(headerNames);
        ArgumentException.ThrowIfNullOrWhiteSpace(headerNames);
        var wanted = headerNames.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return string.Join("\r\n", wanted.SelectMany(name => GetHeaders(name)).Select(header => header.GetHeaderVal()));
    }
    public void RemoveHeaders(string name)
    {
        EnsureEntityAlive();
        if (_nativeEntity != _owner.RootEntity)
        {
            ValidateHeaderName(name);
            var childHeaders = GetHeaders(name).ToArray();
            for (var index = childHeaders.Length - 1; index >= 0; index--)
                SetHeader(childHeaders[index].HeaderName, index + 1, string.Empty, true);
            return;
        }
        EnsureRootEntity(nameof(RemoveHeaders));
        ValidateHeaderName(name);
        var wanted = name.Trim();
        var headers = ReadRootHeaderValues();
        headers.RemoveAll(header => header.Name.Equals(wanted, StringComparison.OrdinalIgnoreCase));
        RewriteRootHeaders(headers);
    }
    public void GetContentAsBytes(NotesStream stream) { EnsureEntityAlive(); ArgumentNullException.ThrowIfNull(stream); stream.Write(ReadEntityContent(nameof(GetContentAsBytes))); }
    public void GetContentAsText(NotesStream stream, bool entityCharset = false) { EnsureEntityAlive(); ArgumentNullException.ThrowIfNull(stream); stream.WriteText(ReadEntityText(nameof(GetContentAsText))); }
    public void GetEntityAsText(NotesStream stream)
    {
        EnsureEntityAlive();
        ArgumentNullException.ThrowIfNull(stream);
        var raw = Session.Api.ReadMimeStream(_document.Handle, _itemName);
        stream.Write(_nativeEntity == _owner.RootEntity ? raw : GetSerializedEntity(raw, GetEntityPath(), nameof(GetEntityAsText)));
    }
    public void SetContentFromText(NotesStream stream, string contentType, int encoding)
    {
        ArgumentNullException.ThrowIfNull(stream);
        var charset = Charset.Trim();
        var text = stream.ReadText();
        var bytes = charset.Length == 0 ? System.Text.Encoding.UTF8.GetBytes(text) : System.Text.Encoding.GetEncoding(charset).GetBytes(text);
        SetContent(bytes, contentType, encoding, nameof(SetContentFromText));
    }
    public void SetContentFromBytes(NotesStream stream, string contentType, int encoding)
    {
        ArgumentNullException.ThrowIfNull(stream);
        SetContent(stream.Read(), contentType, encoding, nameof(SetContentFromBytes));
    }
    public void EncodeContent(int encoding)
    {
        EnsureEntityAlive();
        if (_nativeEntity != _owner.RootEntity)
        {
            var nestedType = ContentType + (string.IsNullOrWhiteSpace(ContentSubType) ? string.Empty : "/" + ContentSubType);
            SetContent(ReadEntityContent(nameof(EncodeContent)), nestedType, encoding, nameof(EncodeContent));
            return;
        }
        EnsureRootEntity(nameof(EncodeContent));
        var headers = ReadRootHeaderValues();
        var contentType = headers.FirstOrDefault(h => h.Name.Equals("Content-Type", StringComparison.OrdinalIgnoreCase)).Value;
        if (string.IsNullOrWhiteSpace(contentType)) contentType = "application/octet-stream";
        SetContent(ReadRootContent(nameof(EncodeContent)), contentType, encoding, nameof(EncodeContent));
    }
    public void DecodeContent()
    {
        EnsureEntityAlive();
        if (_nativeEntity != _owner.RootEntity)
        {
            var nestedType = ContentType + (string.IsNullOrWhiteSpace(ContentSubType) ? string.Empty : "/" + ContentSubType);
            SetContent(ReadEntityContent(nameof(DecodeContent)), nestedType, 1725, nameof(DecodeContent));
            return;
        }
        EnsureRootEntity(nameof(DecodeContent));
        var headers = ReadRootHeaderValues();
        var contentType = headers.FirstOrDefault(h => h.Name.Equals("Content-Type", StringComparison.OrdinalIgnoreCase)).Value;
        if (string.IsNullOrWhiteSpace(contentType)) contentType = "application/octet-stream";
        SetContent(ReadRootContent(nameof(DecodeContent)), contentType, 1725, nameof(DecodeContent));
    }

    private void SetContent(byte[] content, string contentType, int encoding, string member)
    {
        var normalizedType = RequireText(contentType, nameof(contentType));
        var encoded = EncodeContentBytes(content, encoding);
        if (_nativeEntity != _owner.RootEntity)
        {
            WriteChildContent(encoded, normalizedType, encoding, member);
            return;
        }
        EnsureRootEntity(member);
        var headers = ReadRootHeaderValues();
        headers.RemoveAll(h => h.Name.Equals("Content-Type", StringComparison.OrdinalIgnoreCase) || h.Name.Equals("Content-Transfer-Encoding", StringComparison.OrdinalIgnoreCase));
        headers.Add(("Content-Type", normalizedType));
        headers.Add(("Content-Transfer-Encoding", EncodingName(encoding)));
        var headerText = string.Join("\r\n", headers.Select(h => h.Name + ": " + h.Value)) + "\r\n\r\n";
        var headerBytes = System.Text.Encoding.Latin1.GetBytes(headerText);
        var rewritten = new byte[headerBytes.Length + encoded.Length];
        Buffer.BlockCopy(headerBytes, 0, rewritten, 0, headerBytes.Length);
        Buffer.BlockCopy(encoded, 0, rewritten, headerBytes.Length, encoded.Length);
        Session.Api.WriteMimeStream(_document.ParentDatabase.Handle, _document.Handle, _itemName, rewritten);
        _document.InvalidateMimeDirectory();
    }

    private void WriteChildContent(byte[] encoded, string contentType, int encoding, string member)
    {
        var raw = Session.Api.ReadMimeStream(_document.Handle, _itemName);
        var path = GetEntityPath();
        if (path.Length > 1)
        {
            var entity = GetSerializedEntity(raw, path, member);
            var entityOffset = FindRootBodyOffset(entity);
            var entityHeaders = ParseHeaderValues(entity, entityOffset);
            entityHeaders.RemoveAll(h => h.Name.Equals("Content-Type", StringComparison.OrdinalIgnoreCase) || h.Name.Equals("Content-Transfer-Encoding", StringComparison.OrdinalIgnoreCase));
            entityHeaders.Add(("Content-Type", contentType));
            entityHeaders.Add(("Content-Transfer-Encoding", EncodingName(encoding)));
            var text = string.Join("\r\n", entityHeaders.Select(h => h.Name + ": " + h.Value)) + "\r\n\r\n";
            var head = System.Text.Encoding.Latin1.GetBytes(text);
            var nestedReplacement = head.Concat(encoded).ToArray();
            Session.Api.WriteMimeStream(_document.ParentDatabase.Handle, _document.Handle, _itemName, ReplaceSerializedEntity(raw, path, nestedReplacement));
            _document.InvalidateMimeDirectory();
            return;
        }
        var boundary = _owner.TypeParam(_owner.RootEntity, NotesConst.MIME_SYMBOL_BOUNDARY).Trim();
        if (boundary.Length == 0) throw new NotesException(5, "MIME child content requires a multipart root.");
        var children = SplitDirectChildren(raw, boundary);
        var index = GetDirectChildIndex(member);
        if (index < 0 || index >= children.Count) throw new NotesException(5, "MIME child is no longer present.");
        var child = children[index];
        var bodyOffset = FindRootBodyOffset(child);
        var headers = ParseHeaderValues(child, bodyOffset);
        headers.RemoveAll(h => h.Name.Equals("Content-Type", StringComparison.OrdinalIgnoreCase) || h.Name.Equals("Content-Transfer-Encoding", StringComparison.OrdinalIgnoreCase));
        headers.Add(("Content-Type", contentType));
        headers.Add(("Content-Transfer-Encoding", EncodingName(encoding)));
        var headerText = string.Join("\r\n", headers.Select(h => h.Name + ": " + h.Value)) + "\r\n\r\n";
        var headerBytes = System.Text.Encoding.Latin1.GetBytes(headerText);
        var replacement = new byte[headerBytes.Length + encoded.Length];
        Buffer.BlockCopy(headerBytes, 0, replacement, 0, headerBytes.Length);
        Buffer.BlockCopy(encoded, 0, replacement, headerBytes.Length, encoded.Length);
        children[index] = replacement;
        var rootBodyOffset = FindRootBodyOffset(raw);
        var prefix = raw[..rootBodyOffset];
        var rebuilt = prefix.Concat(BuildMultipartBody(children, boundary)).ToArray();
        Session.Api.WriteMimeStream(_document.ParentDatabase.Handle, _document.Handle, _itemName, rebuilt);
        _document.InvalidateMimeDirectory();
    }

    private static List<(string Name, string Value)> ParseHeaderValues(byte[] raw, int bodyOffset)
    {
        var text = System.Text.Encoding.Latin1.GetString(raw, 0, Math.Min(bodyOffset, raw.Length)).Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        var result = new List<(string Name, string Value)>();
        foreach (var line in text.Split('\n'))
        {
            var colon = line.IndexOf(':');
            if (colon > 0) result.Add((line[..colon].Trim(), line[(colon + 1)..].Trim()));
        }
        return result;
    }

    private static byte[] BuildMultipartBody(List<byte[]> children, string boundary)
    {
        using var output = new MemoryStream();
        foreach (var child in children)
        {
            var marker = System.Text.Encoding.ASCII.GetBytes("--" + boundary + "\r\n");
            output.Write(marker);
            output.Write(child);
            output.Write(System.Text.Encoding.ASCII.GetBytes("\r\n"));
        }
        output.Write(System.Text.Encoding.ASCII.GetBytes("--" + boundary + "--\r\n"));
        return output.ToArray();
    }

    private string ReadPreamble()
    {
        if (_nativeEntity != _owner.RootEntity)
        {
            EnsureEntityAlive();
            var rawEntity = Session.Api.ReadMimeStream(_document.Handle, _itemName);
            var entity = GetSerializedEntity(rawEntity, GetEntityPath(), nameof(Preamble));
            var nestedBodyOffset = FindRootBodyOffset(entity);
            var headers = ParseHeaderValues(entity, nestedBodyOffset);
            var contentType = headers.FirstOrDefault(h => h.Name.Equals("Content-Type", StringComparison.OrdinalIgnoreCase)).Value ?? string.Empty;
            var nestedBoundary = contentType.Split(';').Skip(1).Select(p => p.Trim()).FirstOrDefault(p => p.StartsWith("boundary=", StringComparison.OrdinalIgnoreCase))?.Split('=', 2).ElementAtOrDefault(1)?.Trim().Trim('"') ?? string.Empty;
            if (nestedBoundary.Length == 0) throw new NotesException(5, "MIME multipart boundary is missing.");
            var nestedMarker = FindBytes(entity, System.Text.Encoding.ASCII.GetBytes("--" + nestedBoundary), nestedBodyOffset);
            if (nestedMarker < 0) throw new NotesException(5, "MIME multipart boundary is missing.");
            return System.Text.Encoding.UTF8.GetString(entity, nestedBodyOffset, nestedMarker - nestedBodyOffset).TrimEnd('\r', '\n');
        }
        EnsureRootEntity(nameof(Preamble));
        var raw = ReadRootEntity(nameof(Preamble));
        var boundary = RequireMultipartBoundary(raw);
        var bodyOffset = FindRootBodyOffset(raw);
        var marker = FindBytes(raw, System.Text.Encoding.ASCII.GetBytes("--" + boundary), bodyOffset);
        if (marker < 0) throw new NotesException(5, "MIME multipart boundary is missing.");
        return System.Text.Encoding.UTF8.GetString(raw, bodyOffset, marker - bodyOffset).TrimEnd('\r', '\n');
    }

    private void WritePreamble(string? value)
    {
        if (_nativeEntity != _owner.RootEntity)
        {
            EnsureEntityAlive();
            var nestedRaw = Session.Api.ReadMimeStream(_document.Handle, _itemName);
            var path = GetEntityPath();
            var entity = GetSerializedEntity(nestedRaw, path, nameof(Preamble));
            var nestedOffset = FindRootBodyOffset(entity);
            var headers = ParseHeaderValues(entity, nestedOffset);
            var contentType = headers.FirstOrDefault(h => h.Name.Equals("Content-Type", StringComparison.OrdinalIgnoreCase)).Value ?? string.Empty;
            var nestedBoundary = contentType.Split(';').Skip(1).Select(p => p.Trim()).FirstOrDefault(p => p.StartsWith("boundary=", StringComparison.OrdinalIgnoreCase))?.Split('=', 2).ElementAtOrDefault(1)?.Trim().Trim('"') ?? string.Empty;
            if (nestedBoundary.Length == 0) throw new NotesException(5, "MIME multipart boundary is missing.");
            var nestedMarker = FindBytes(entity, System.Text.Encoding.ASCII.GetBytes("--" + nestedBoundary), nestedOffset);
            if (nestedMarker < 0) throw new NotesException(5, "MIME multipart boundary is missing.");
            var nestedPreamble = System.Text.Encoding.UTF8.GetBytes(value ?? string.Empty);
            var replacement = entity[..nestedOffset].Concat(nestedPreamble).Concat(entity[nestedMarker..]).ToArray();
            Session.Api.WriteMimeStream(_document.ParentDatabase.Handle, _document.Handle, _itemName, ReplaceSerializedEntity(nestedRaw, path, replacement));
            _document.InvalidateMimeDirectory();
            return;
        }
        EnsureRootEntity(nameof(Preamble));
        var raw = ReadRootEntity(nameof(Preamble));
        var boundary = RequireMultipartBoundary(raw);
        var bodyOffset = FindRootBodyOffset(raw);
        var marker = FindBytes(raw, System.Text.Encoding.ASCII.GetBytes("--" + boundary), bodyOffset);
        if (marker < 0) throw new NotesException(5, "MIME multipart boundary is missing.");
        var prefix = raw[..bodyOffset];
        var suffix = raw[marker..];
        var preamble = System.Text.Encoding.UTF8.GetBytes(value ?? string.Empty);
        var rewritten = new byte[prefix.Length + preamble.Length + suffix.Length];
        Buffer.BlockCopy(prefix, 0, rewritten, 0, prefix.Length);
        Buffer.BlockCopy(preamble, 0, rewritten, prefix.Length, preamble.Length);
        Buffer.BlockCopy(suffix, 0, rewritten, prefix.Length + preamble.Length, suffix.Length);
        Session.Api.WriteMimeStream(_document.ParentDatabase.Handle, _document.Handle, _itemName, rewritten);
        _document.InvalidateMimeDirectory();
    }

    private string RequireMultipartBoundary(byte[] raw)
    {
        if (!IsMultipart) throw new NotesException(5, "NotesMIMEEntity.Preamble requires a multipart entity.");
        var boundary = TypeParam(NotesConst.MIME_SYMBOL_BOUNDARY).Trim();
        if (boundary.Length == 0) throw new NotesException(5, "MIME multipart boundary is missing.");
        return boundary;
    }

    private static int FindBytes(byte[] source, byte[] value, int start)
    {
        for (var i = Math.Max(0, start); i <= source.Length - value.Length; i++)
        {
            var match = true;
            for (var j = 0; j < value.Length; j++) if (source[i + j] != value[j]) { match = false; break; }
            if (match) return i;
        }
        return -1;
    }

    private static int FindHeaderEnd(byte[] raw)
    {
        var crlf = FindBytes(raw, [13, 10, 13, 10], 0);
        if (crlf >= 0) return crlf + 4;
        var lf = FindBytes(raw, [10, 10], 0);
        return lf >= 0 ? lf + 2 : raw.Length;
    }

    private static string EncodingName(int encoding) => encoding switch
    {
        1724 => "7bit", 1725 => "8bit", 1726 => "quoted-printable", 1727 => "base64", 1728 => "binary",
        _ => throw new ArgumentOutOfRangeException(nameof(encoding), encoding, "'encoding' must be 1724 (7bit), 1725 (8bit), 1726 (quoted-printable), 1727 (base64), or 1728 (binary).")
    };

    private static byte[] EncodeContentBytes(byte[] content, int encoding) => encoding switch
    {
        1724 or 1725 or 1728 => content,
        1727 => System.Text.Encoding.ASCII.GetBytes(Convert.ToBase64String(content)),
        1726 => EncodeQuotedPrintable(content),
        _ => throw new ArgumentOutOfRangeException(nameof(encoding), encoding, "'encoding' must be 1724 (7bit), 1725 (8bit), 1726 (quoted-printable), 1727 (base64), or 1728 (binary).")
    };

    private static byte[] EncodeQuotedPrintable(byte[] content)
    {
        var builder = new System.Text.StringBuilder();
        foreach (var value in content)
            if ((value >= 33 && value <= 60) || (value >= 62 && value <= 126) || value is 9 or 32 or 10 or 13) builder.Append((char)value);
            else builder.Append('=').Append(value.ToString("X2", CultureInfo.InvariantCulture));
        return System.Text.Encoding.ASCII.GetBytes(builder.ToString());
    }

    public void Remove()
    {
        EnsureEntityAlive();
        if (_nativeEntity != _owner.RootEntity)
        {
            var raw = Session.Api.ReadMimeStream(_document.Handle, _itemName);
            var path = GetEntityPath();
            if (path.Length == 0) throw new NotesException(5, "MIME entity path is invalid.");
            var parentPath = path[..^1];
            var parent = GetSerializedEntity(raw, parentPath, nameof(Remove));
            var parentOffset = FindRootBodyOffset(parent);
            var parentHeaders = ParseHeaderValues(parent, parentOffset);
            var contentType = parentHeaders.FirstOrDefault(h => h.Name.Equals("Content-Type", StringComparison.OrdinalIgnoreCase)).Value ?? string.Empty;
            var boundary = contentType.Split(';').Skip(1).Select(p => p.Trim()).FirstOrDefault(p => p.StartsWith("boundary=", StringComparison.OrdinalIgnoreCase))?.Split('=', 2).ElementAtOrDefault(1)?.Trim().Trim('"') ?? string.Empty;
            if (boundary.Length == 0) throw new NotesException(5, "MIME parent is not multipart.");
            var children = SplitDirectChildren(parent, boundary);
            if (path[^1] < 0 || path[^1] >= children.Count) throw new NotesException(5, "MIME child is no longer present.");
            children.RemoveAt(path[^1]);
            var replacement = System.Text.Encoding.Latin1.GetBytes(string.Join("\r\n", parentHeaders.Select(h => h.Name + ": " + h.Value)) + "\r\n\r\n").Concat(BuildMultipartBody(children, boundary)).ToArray();
            Session.Api.WriteMimeStream(_document.ParentDatabase.Handle, _document.Handle, _itemName, ReplaceSerializedEntity(raw, parentPath, replacement));
            _document.InvalidateMimeDirectory();
            Recycle();
            return;
        }
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
        var raw = Session.Api.ReadMimeStream(_document.Handle, _itemName);
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

    private string ReadEntityText(string member)
    {
        var content = ReadEntityContent(member);
        var charset = Charset.Trim();
        if (charset.Length == 0) return System.Text.Encoding.UTF8.GetString(content);
        try { return System.Text.Encoding.GetEncoding(charset).GetString(content); }
        catch (ArgumentException ex) { throw new NotesException(5, "Unsupported MIME root charset '" + charset + "': " + ex.Message); }
    }

    private byte[] ReadEntityContent(string member)
    {
        if (_nativeEntity == _owner.RootEntity) return ReadRootContent(member);
        var raw = Session.Api.ReadMimeStream(_document.Handle, _itemName);
        var child = GetSerializedEntity(raw, GetEntityPath(), member);
        var bodyOffset = FindRootBodyOffset(child);
        var body = bodyOffset >= child.Length ? [] : child[bodyOffset..];
        var transfer = GetRootHeader(child, "Content-Transfer-Encoding").Trim();
        if (transfer.Equals("base64", StringComparison.OrdinalIgnoreCase)) return Convert.FromBase64String(System.Text.Encoding.ASCII.GetString(body));
        if (transfer.Equals("quoted-printable", StringComparison.OrdinalIgnoreCase)) return DecodeQuotedPrintable(body);
        return body;
    }

    private int GetDirectChildIndex(string member)
    {
        var index = 0;
        for (var entity = _owner.FirstSubpart(_owner.RootEntity); entity != 0; entity = _owner.NextSibling(entity), index++)
            if (entity == _nativeEntity) return index;
        throw new NotesException(5, "MIME child is not a direct child of the root entity.");
    }

    private int[] GetEntityPath()
    {
        var path = new List<int>();
        var current = _nativeEntity;
        while (current != _owner.RootEntity)
        {
            var parent = _owner.Parent(current);
            if (parent == 0) throw new NotesException(5, "MIME entity parent is unavailable.");
            var index = 0;
            for (var sibling = _owner.FirstSubpart(parent); sibling != 0; sibling = _owner.NextSibling(sibling), index++)
                if (sibling == current) { path.Add(index); break; }
            if (path.Count == 0) throw new NotesException(5, "MIME entity path is unavailable.");
            current = parent;
        }
        path.Reverse();
        return path.ToArray();
    }

    private static byte[] GetSerializedEntity(byte[] raw, int[] path, string member)
    {
        var current = raw;
        foreach (var index in path)
        {
            var bodyOffset = FindRootBodyOffset(current);
            var headers = ParseHeaderValues(current, bodyOffset);
            var contentType = headers.FirstOrDefault(h => h.Name.Equals("Content-Type", StringComparison.OrdinalIgnoreCase)).Value;
            var boundary = contentType?.Split(';').Skip(1).Select(p => p.Trim()).FirstOrDefault(p => p.StartsWith("boundary=", StringComparison.OrdinalIgnoreCase))?.Split('=', 2).ElementAtOrDefault(1)?.Trim().Trim('"') ?? string.Empty;
            if (boundary.Length == 0) throw new NotesException(5, "MIME entity is not multipart.");
            var children = SplitDirectChildren(current, boundary);
            if (index < 0 || index >= children.Count) throw new NotesException(5, "MIME entity path is no longer valid.");
            current = children[index];
        }
        return current;
    }

    private static List<byte[]> SplitDirectChildren(byte[] raw, string boundary)
    {
        var result = new List<byte[]>();
        var bodyOffset = FindRootBodyOffset(raw);
        var body = bodyOffset >= raw.Length ? [] : raw[bodyOffset..];
        var marker = System.Text.Encoding.Latin1.GetBytes("--" + boundary);
        var start = FindBytes(body, marker, 0);
        while (start >= 0)
        {
            var contentStart = start + marker.Length;
            if (contentStart + 2 <= body.Length && body[contentStart] == '-' && body[contentStart + 1] == '-') break;
            while (contentStart < body.Length && (body[contentStart] == 13 || body[contentStart] == 10)) contentStart++;
            var next = FindBytes(body, marker, contentStart);
            if (next < 0) break;
            var end = next;
            while (end > contentStart && (body[end - 1] == 13 || body[end - 1] == 10)) end--;
            result.Add(body[contentStart..end]);
            start = next;
        }
        return result;
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
        if (_nativeEntity != _owner.RootEntity)
        {
            var raw = Session.Api.ReadMimeStream(_document.Handle, _itemName);
            var boundary = _owner.TypeParam(_owner.RootEntity, NotesConst.MIME_SYMBOL_BOUNDARY).Trim();
            var children = SplitDirectChildren(raw, boundary);
            var child = children[GetDirectChildIndex(nameof(ReadHeader))];
            var headers = ParseHeaderValues(child, FindRootBodyOffset(child));
            var value = headers.Where(h => h.Name.Equals(name, StringComparison.OrdinalIgnoreCase)).Skip(occurrence - 1).FirstOrDefault();
            if (value.Name is null) throw new NotesException(5, "MIME header is no longer present on the entity.");
            return value.Value;
        }
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
        if (_nativeEntity != _owner.RootEntity)
        {
            var raw = Session.Api.ReadMimeStream(_document.Handle, _itemName);
            var boundary = _owner.TypeParam(_owner.RootEntity, NotesConst.MIME_SYMBOL_BOUNDARY).Trim();
            var children = SplitDirectChildren(raw, boundary);
            var childIndex = GetDirectChildIndex(nameof(SetHeader));
            var child = children[childIndex];
            var childHeaders = ParseHeaderValues(child, FindRootBodyOffset(child));
            var match = childHeaders.Select((h, i) => (h, i)).Where(x => x.h.Name.Equals(name, StringComparison.OrdinalIgnoreCase)).Skip(occurrence - 1).FirstOrDefault();
            if (match.h.Name is null) throw new NotesException(5, "MIME header is no longer present on the entity.");
            if (remove) childHeaders.RemoveAt(match.i); else childHeaders[match.i] = (childHeaders[match.i].Name, value);
            var bodyOffset = FindRootBodyOffset(child);
            var body = bodyOffset >= child.Length ? [] : child[bodyOffset..];
            var text = childHeaders.Count == 0 ? string.Empty : string.Join("\r\n", childHeaders.Select(h => h.Name + ": " + h.Value)) + "\r\n\r\n";
            var head = System.Text.Encoding.Latin1.GetBytes(text);
            children[childIndex] = head.Concat(body).ToArray();
            var rootOffset = FindRootBodyOffset(raw);
            Session.Api.WriteMimeStream(_document.ParentDatabase.Handle, _document.Handle, _itemName, raw[..rootOffset].Concat(BuildMultipartBody(children, boundary)).ToArray());
            _document.InvalidateMimeDirectory();
            return;
        }
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
        Session.Api.WriteMimeStream(_document.ParentDatabase.Handle, _document.Handle, _itemName, rewritten);
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
    private void EnsureEntityAlive()
    {
        EnsureAlive(); _ = _document.Handle;
        if (_owner.IsAlive) return;
        if (!_isRoot) throw new ObjectDisposedException(nameof(NotesMIMEEntity));
        _owner = _document.GetMimeDirectoryOwner();
        _nativeEntity = _owner.RootEntity;
    }
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

    /// <summary>Replaces or appends one MIME header parameter.</summary>
    public void SetParamVal(string parameterName, string? parameterValue)
    {
        EnsureAlive();
        ArgumentException.ThrowIfNullOrWhiteSpace(parameterName);
        var wanted = parameterName.Trim();
        var value = parameterValue ?? string.Empty;
        var header = GetHeaderVal();
        var parts = header.Split(';').Select(part => part.Trim()).ToList();
        var replacement = wanted + "=\"" + value.Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";
        var replaced = false;
        for (var index = 1; index < parts.Count; index++)
        {
            var equals = parts[index].IndexOf('=');
            if (equals <= 0 || !parts[index][..equals].Trim().Equals(wanted, StringComparison.OrdinalIgnoreCase)) continue;
            parts[index] = replacement;
            replaced = true;
            break;
        }
        if (!replaced) parts.Add(replacement);
        _entity.SetHeader(_name, _occurrence, string.Join("; ", parts), false);
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
