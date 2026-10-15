using DotNetToDomino.Interop;

namespace DotNetToDomino;

/// <summary>Represents an item in a Notes document.</summary>
public class NotesItem : NotesObject
{
    private bool _removed;
    internal NotesItem(NotesSession session, NotesDocument parent, string name) : base(session) { Parent = parent; Name = name; }
    public NotesDocument Parent { get; }
    public string Name { get; }
    /// <summary>Gets whether this item is a native Domino MIME part.</summary>
    public bool IsMIME { get { EnsureItemAlive(); return Session.Api.IsMimeItem(Parent.Handle, Name); } }
    private NotesItemMetadata Metadata { get { EnsureItemAlive(); return Session.Api.GetItemMetadata(Parent.Handle, Name); } }
    public NotesDateTime? DateTimeValue
    {
        get { var info = Metadata; return info.DataType == NotesNativeApi.NotesTypeTime ? NotesDateTime.FromNative(Session, Session.Api.GetItemTime(Parent.Handle, Name)) : null; }
        set
        {
            EnsureItemAlive();
            ArgumentNullException.ThrowIfNull(value);
            var flags = Metadata.Flags;
            Session.Api.SetItemTime(Parent.Handle, Name, value.NativeValue);
            Session.Api.SetItemFlags(Parent.Handle, Name, flags);
        }
    }
    public bool IsAuthors
    {
        get { var f = Metadata.Flags; return (f & NotesNativeApi.NotesItemReadWriters) != 0 && (f & NotesNativeApi.NotesItemNames) != 0; }
        set => UpdateFlags(f => value ? (ushort)((f | NotesNativeApi.NotesItemReadWriters | NotesNativeApi.NotesItemNames | NotesNativeApi.NotesItemSummary) & ~NotesNativeApi.NotesItemReaders) : (ushort)(f & ~NotesNativeApi.NotesItemReadWriters));
    }
    public bool IsEncrypted { get => HasFlag(NotesNativeApi.NotesItemSeal); set => SetFlag(NotesNativeApi.NotesItemSeal, value); }
    public bool IsNames
    {
        get => HasFlag(NotesNativeApi.NotesItemNames);
        set => UpdateFlags(f => value ? (ushort)(f | NotesNativeApi.NotesItemNames | NotesNativeApi.NotesItemSummary) : (ushort)(f & ~(NotesNativeApi.NotesItemNames | NotesNativeApi.NotesItemReaders | NotesNativeApi.NotesItemReadWriters)));
    }
    public bool IsProtected { get => HasFlag(NotesNativeApi.NotesItemProtected); set => SetFlag(NotesNativeApi.NotesItemProtected, value); }
    public bool IsReaders
    {
        get { var f = Metadata.Flags; return (f & NotesNativeApi.NotesItemReaders) != 0 && (f & NotesNativeApi.NotesItemNames) != 0; }
        set => UpdateFlags(f => value ? (ushort)((f | NotesNativeApi.NotesItemReaders | NotesNativeApi.NotesItemNames | NotesNativeApi.NotesItemSummary) & ~NotesNativeApi.NotesItemReadWriters) : (ushort)(f & ~NotesNativeApi.NotesItemReaders));
    }
    public bool IsSigned { get => HasFlag(NotesNativeApi.NotesItemSign); set => SetFlag(NotesNativeApi.NotesItemSign, value); }
    public bool IsSummary { get => HasFlag(NotesNativeApi.NotesItemSummary); set => SetFlag(NotesNativeApi.NotesItemSummary, value); }
    public NotesDateTime LastModified => NotesDateTime.FromNative(Session, Session.Api.GetItemModifiedTime(Parent.Handle, Name));
    public string Text { get { EnsureItemAlive(); return Session.Api.ConvertItemToText(Parent.Handle, Name); } }
    /// <summary>Opens this item as a MIME entity when the item is a native MIME part.</summary>
    public NotesMIMEEntity? GetMIMEEntity()
    {
        EnsureItemAlive();
        return Session.Api.IsMimeItem(Parent.Handle, Name) ? Parent.GetMIMEEntity(Name) : null;
    }
    public int Type => MapType(Metadata);
    public long ValueLength => Metadata.ValueLength;
    public object?[] Values { get { EnsureItemAlive(); return Parent.GetItemValue(Name); } set { EnsureItemAlive(); ArgumentNullException.ThrowIfNull(value); Session.Api.SetItemValues(Parent.Handle, Name, value); } }
    public NotesRichTextItem? GetRichTextItem()
    {
        EnsureItemAlive();
        return Metadata.DataType == NotesNativeApi.NotesTypeComposite ? this as NotesRichTextItem ?? Parent.ParentDatabase.Own(new NotesRichTextItem(Session, Parent, Name)) : null;
    }
    public void Remove() { EnsureItemAlive(); Session.Api.RemoveItemByBlock(Parent.Handle, Name); _removed = true; }
    public NotesItem CopyToDocument(NotesDocument destination, string? newName = null)
    {
        EnsureItemAlive(); ArgumentNullException.ThrowIfNull(destination);
        var targetName = string.IsNullOrWhiteSpace(newName) ? Name : newName.Trim();
        Session.Api.CopyItemToDocument(Parent.Handle, Name, destination.Handle, targetName);
        return destination.GetFirstItem(targetName) ?? throw new InvalidOperationException("Copied NotesItem could not be reopened.");
    }
    protected void EnsureItemAlive() { EnsureAlive(); if (_removed) throw new ObjectDisposedException(nameof(NotesItem), "NotesItem has been removed from its document."); _ = Parent.Handle; }
    protected override void ReleaseNative() { _removed = true; Parent.ParentDatabase.UnregisterChild(this); }
    private bool HasFlag(ushort flag) => (Metadata.Flags & flag) != 0;
    private void SetFlag(ushort flag, bool value) => UpdateFlags(f => value ? (ushort)(f | flag) : (ushort)(f & ~flag));
    private void UpdateFlags(Func<ushort, ushort> update)
    {
        var info = Metadata; var flags = update(info.Flags);
        if (flags != info.Flags) Session.Api.SetItemFlags(Parent.Handle, Name, flags);
    }
    private static int MapType(NotesItemMetadata info)
    {
        if ((info.Flags & NotesNativeApi.NotesItemReadWriters) != 0 && (info.Flags & NotesNativeApi.NotesItemNames) != 0) return 1076;
        if ((info.Flags & NotesNativeApi.NotesItemReaders) != 0 && (info.Flags & NotesNativeApi.NotesItemNames) != 0) return 1075;
        if ((info.Flags & NotesNativeApi.NotesItemNames) != 0) return 1074;
        return info.DataType switch
        {
            0x0001 => 1, 0x0002 => 2, 0x0004 => 4, 0x0006 => 6, 0x0007 => 7, 0x0008 => 8,
            0x000E => 14, 0x000F => 15, 0x0010 => 16, 0x0011 => 17, 0x0012 => 18, 0x0013 => 19,
            0x0014 => 20, 0x0015 => 21, 0x0019 => 25, 0x0100 => 256, 0x0200 => 512,
            0x0300 or 0x0301 => 768, 0x0400 or 0x0401 => 1024, 0x0500 or 0x0501 => 1280,
            0x0502 => 1282, 0x0003 => 1085, 0x0600 => 1536, 0x0700 => 1792, _ => 0
        };
    }
}

/// <summary>Represents a Notes composite rich-text item.</summary>
public sealed class NotesRichTextItem : NotesItem
{
    private long _richTextRevision;
    private NotesRichTextStyleState? _appendStyleState;

    internal NotesRichTextItem(NotesSession session, NotesDocument parent, string name) : base(session, parent, name) { }

    public string ConvertToHTML(bool inlineImages = false)
    {
        EnsureItemAlive();
        return Session.Api.ConvertRichTextToHtml(Parent.ParentDatabase.Handle, Parent.Handle, Name, inlineImages);
    }

    public bool SaveAttachment(string attachmentName, string outputPath)
    {
        EnsureItemAlive();
        ArgumentException.ThrowIfNullOrWhiteSpace(attachmentName);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        return Session.Api.SaveRichTextAttachment(Parent.Handle, Name, attachmentName.Trim(), outputPath.Trim());
    }

    /// <summary>Embeds a file attachment in this rich-text item.</summary>
    public NotesEmbeddedObject AttachFile(string sourcePath)
    {
        EnsureItemAlive();
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        sourcePath = sourcePath.Trim();
        if (!File.Exists(sourcePath)) throw new FileNotFoundException("Rich-text attachment source file was not found.", sourcePath);
        return new NotesEmbeddedObject(Session, this, Session.Api.AttachRichTextFile(Parent.Handle, Name, sourcePath));
    }

    /// <summary>Embeds a file object in this rich-text item.</summary>
    public NotesEmbeddedObject AppendObject(string sourcePath) => AttachFile(sourcePath);

    public void AppendText(string value)
    {
        EnsureItemAlive();
        ArgumentNullException.ThrowIfNull(value);
        var text = value;
        if (_appendStyleState is NotesRichTextStyleState style) Session.Api.AppendRichTextStyled(Parent.Handle, Name, text, style);
        else Session.Api.AppendRichText(Parent.Handle, Name, text);
        _richTextRevision++;
    }

    public void AppendStyle(NotesRichTextStyle style)
    {
        EnsureItemAlive();
        ArgumentNullException.ThrowIfNull(style);
        _appendStyleState = style.ExportState();
    }

    /// <summary>
    /// Persists the current document, including all changes made to this rich-text item.
    /// Rich-text edits are stored in the owning note; there is no separate native item
    /// handle to update independently.
    /// </summary>
    public void Update()
    {
        EnsureItemAlive();
        Parent.Save();
        _richTextRevision++;
    }

    /// <summary>Adds a document link to another document in this rich-text item.</summary>
    public void AppendDocLink(NotesDocument document, string? comment = null)
    {
        EnsureItemAlive();
        ArgumentNullException.ThrowIfNull(document);
        if (!string.Equals(Parent.ParentDatabase.ReplicaID, document.ParentDatabase.ReplicaID, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("The linked document must be in the same database replica.", nameof(document));
        Session.Api.AppendRichTextDocLink(Parent.Handle, Name, document.ParentDatabase.Handle, document.UniversalId, comment ?? string.Empty);
        _richTextRevision++;
    }

    /// <summary>Adds a document link represented by a local Notes URL.</summary>
    public void AppendNotesURL(string notesUrl, string? comment = null)
    {
        EnsureItemAlive();
        ArgumentException.ThrowIfNullOrWhiteSpace(notesUrl);
        if (!Uri.TryCreate(notesUrl.Trim(), UriKind.Absolute, out var uri) ||
            !uri.Scheme.Equals("notes", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("The value must be a valid notes:// URL.", nameof(notesUrl));
        var parts = uri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 3 || !uint.TryParse(parts[^1], System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out var noteId))
            throw new ArgumentException("The Notes URL must contain a hexadecimal NoteID.", nameof(notesUrl));
        var filePath = Uri.UnescapeDataString(string.Join('/', parts[..^2]));
        using var database = Session.OpenDatabase(uri.Host, filePath);
        if (!database.IsOpen) throw new NotesException(4091, $"Unable to open Notes URL database '{filePath}'.");
        using var document = database.GetDocumentByID(noteId) ?? throw new NotesException(4091, $"Unable to open Notes URL document '{noteId:X8}'.");
        AppendDocLink(document, comment);
    }

    /// <summary>Appends an empty Domino RichText table with the requested dimensions.</summary>
    public void AppendTable(int rows, int columns)
    {
        EnsureItemAlive();
        Session.Api.AppendRichTextTable(Parent.Handle, Name, rows, columns);
        _richTextRevision++;
    }

    public void AppendParagraphStyle(NotesRichTextParagraphStyle style)
    {
        EnsureItemAlive();
        ArgumentNullException.ThrowIfNull(style);
        throw new NotSupportedException("NotesRichTextItem.AppendParagraphStyle requires atomic composite-data rewrite support and is not supported by this runtime yet.");
    }

    public NotesRichTextNavigator CreateNavigator()
    {
        EnsureItemAlive();
        return new NotesRichTextNavigator(Session, this);
    }

    public NotesRichTextRange CreateRange()
    {
        EnsureItemAlive();
        return new NotesRichTextRange(Session, this);
    }

    public NotesEmbeddedObject? GetEmbeddedObject(string name)
    {
        EnsureItemAlive();
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return Session.Api.TryGetRichTextAttachmentMetadata(Parent.Handle, Name, name.Trim(), out var metadata)
            ? new NotesEmbeddedObject(Session, this, metadata)
            : null;
    }

    public IReadOnlyList<NotesEmbeddedObject> EmbeddedObjects
    {
        get
        {
            EnsureItemAlive();
            return Session.Api.GetRichTextAttachmentMetadata(Parent.Handle, Name)
                .Select(metadata => new NotesEmbeddedObject(Session, this, metadata))
                .ToArray();
        }
    }

    internal long RichTextRevision { get { EnsureItemAlive(); return _richTextRevision; } }
    internal void EnsureRichTextAlive() => EnsureItemAlive();
    internal IReadOnlyList<NotesRichTextRecordData> ReadRichTextRecords()
    {
        EnsureItemAlive();
        return Session.Api.GetRichTextRecords(Parent.Handle, Name);
    }

    internal void RewriteRichTextRecords(IReadOnlyList<NotesRichTextRecordRewrite> records)
    {
        EnsureItemAlive();
        var original = ReadRichTextRecords();
        Session.Api.ReplaceRichTextCdRecords(Parent.Handle, Name, original, records);
        _richTextRevision++;
    }

    internal void RemoveEmbeddedObject(NotesAttachmentMetadata metadata)
    {
        EnsureItemAlive();
        var records = ReadRichTextRecords();
        var rewritten = NotesRichTextCdTransform.Transform(records, record =>
            record.ElementType == NotesRichTextNavigator.ElementEmbeddedObject && string.Equals(record.LinkedObjectName, metadata.Name, StringComparison.OrdinalIgnoreCase)
                ? null
                : NotesRichTextRecordRewrite.Preserve(record));
        RewriteRichTextRecords(rewritten);
        Session.Api.RemoveItemByBlock(Parent.Handle, metadata.ItemBlock);
    }
}
