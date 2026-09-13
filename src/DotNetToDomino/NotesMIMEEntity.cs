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

    public string ContentAsText => throw Unsupported(nameof(ContentAsText));
    public string Headers => throw Unsupported(nameof(Headers));
    public string Preamble { get => throw Unsupported(nameof(Preamble)); set => throw Unsupported(nameof(Preamble)); }
    public int Encoding => throw Unsupported(nameof(Encoding));
    public NotesMIMEHeader? GetNthHeader(string name, int occurrence = 1) => throw Unsupported(nameof(GetNthHeader));
    public NotesMIMEHeader CreateHeader(string name) => throw Unsupported(nameof(CreateHeader));
    public string GetSomeHeaders(string headerNames) => throw Unsupported(nameof(GetSomeHeaders));
    public void GetContentAsBytes(NotesStream stream) => throw Unsupported(nameof(GetContentAsBytes));
    public void GetContentAsText(NotesStream stream, bool entityCharset = false) => throw Unsupported(nameof(GetContentAsText));
    public void GetEntityAsText(NotesStream stream) => throw Unsupported(nameof(GetEntityAsText));
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

/// <summary>MIME header surface reserved until native Domino header access is verified.</summary>
public sealed class NotesMIMEHeader : NotesObject
{
    internal NotesMIMEHeader(NotesSession session) : base(session) { }
    protected override void ReleaseNative() { }
}
