using DotNetToDomino.Interop;

namespace DotNetToDomino;

/// <summary>Represents an item in a Notes document.</summary>
public class NotesItem : NotesObject
{
    private bool _removed;
    internal NotesItem(NotesSession session, NotesDocument parent, string name) : base(session) { Parent = parent; Name = name; }
    public NotesDocument Parent { get; }
    public string Name { get; }
    private NotesItemMetadata Metadata { get { EnsureItemAlive(); return Session.Api.GetItemMetadata(Parent.Handle, Name); } }
    public NotesDateTime? DateTimeValue
    {
        get { var info = Metadata; return info.DataType == NotesNativeApi.NotesTypeTime ? NotesDateTime.FromNative(Session, Session.Api.GetItemTime(Parent.Handle, Name)) : null; }
        set { EnsureItemAlive(); ArgumentNullException.ThrowIfNull(value); Session.Api.SetItemTime(Parent.Handle, Name, value.NativeValue); }
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
    public string Text { get { EnsureItemAlive(); return Session.Api.GetItemText(Parent.Handle, Name); } }
    public int Type => MapType(Metadata);
    public long ValueLength => Metadata.ValueLength;
    public object?[] Values { get { EnsureItemAlive(); return Parent.GetItemValue(Name); } set { EnsureItemAlive(); Session.Api.SetItemValues(Parent.Handle, Name, value); } }
    public NotesRichTextItem? GetRichTextItem()
    {
        EnsureItemAlive();
        return Metadata.DataType == NotesNativeApi.NotesTypeComposite ? this as NotesRichTextItem ?? Parent.ParentDatabase.Own(new NotesRichTextItem(Session, Parent, Name)) : null;
    }
    public void Remove() { EnsureItemAlive(); Session.Api.DeleteItem(Parent.Handle, Name); _removed = true; }
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

    public bool SaveAttachment(string attachmentName, string outputPath)
    {
        EnsureItemAlive();
        return Session.Api.SaveRichTextAttachment(Parent.Handle, Name, attachmentName, outputPath);
    }

    public void AppendText(string value)
    {
        EnsureItemAlive();
        var text = value ?? string.Empty;
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

    public void AppendParagraphStyle(NotesRichTextParagraphStyle style)
    {
        EnsureItemAlive();
        ArgumentNullException.ThrowIfNull(style);
        throw StructuralWriteNotSupported(nameof(AppendParagraphStyle));
    }

    public void AppendTable(int rows, int columns) => throw StructuralWriteNotSupported(nameof(AppendTable));
    public void AppendTable(int rows, int columns, IReadOnlyList<string> labels) => throw StructuralWriteNotSupported(nameof(AppendTable));
    public void AppendTable(int rows, int columns, IReadOnlyList<string> labels, int leftMargin) => throw StructuralWriteNotSupported(nameof(AppendTable));
    public void AppendTable(int rows, int columns, IReadOnlyList<string> labels, int leftMargin, NotesRichTextParagraphStyle paragraphStyle) => throw StructuralWriteNotSupported(nameof(AppendTable));
    public void AppendDocLink(object link) => throw StructuralWriteNotSupported(nameof(AppendDocLink));
    public void AppendDocLink(object link, string comment) => throw StructuralWriteNotSupported(nameof(AppendDocLink));
    public void AppendDocLink(object link, string comment, string hotspotText) => throw StructuralWriteNotSupported(nameof(AppendDocLink));
    public void BeginSection(string title) => throw StructuralWriteNotSupported(nameof(BeginSection));
    public void BeginSection(string title, NotesRichTextStyle titleStyle) => throw StructuralWriteNotSupported(nameof(BeginSection));
    public void BeginSection(string title, NotesRichTextStyle titleStyle, NotesColorObject barColor) => throw StructuralWriteNotSupported(nameof(BeginSection));
    public void BeginSection(string title, NotesRichTextStyle titleStyle, NotesColorObject barColor, bool expanded) => throw StructuralWriteNotSupported(nameof(BeginSection));
    public void EndSection() => throw StructuralWriteNotSupported(nameof(EndSection));

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
        if (string.IsNullOrWhiteSpace(name)) return null;
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

    public NotesEmbeddedObject EmbedObject(int type, string className, string sourcePath) => EmbedObject(type, className, sourcePath, null);

    public NotesEmbeddedObject EmbedObject(int type, string className, string sourcePath, string? name)
    {
        EnsureItemAlive();
        if (type != NotesEmbeddedObject.EmbedAttachment)
            throw new ArgumentOutOfRangeException(nameof(type), "Only EMBED_ATTACHMENT (1454) is supported by NotesRichTextItem.EmbedObject.");
        if (!string.IsNullOrEmpty(className))
            throw new ArgumentException("The class argument must be empty for EMBED_ATTACHMENT.", nameof(className));
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);

        _ = name;
        var metadata = Session.Api.AttachRichTextFile(Parent.Handle, Name, sourcePath);
        _richTextRevision++;
        return new NotesEmbeddedObject(Session, this, metadata);
    }

    internal long RichTextRevision { get { EnsureItemAlive(); return _richTextRevision; } }
    internal void EnsureRichTextAlive() => EnsureItemAlive();
    internal IReadOnlyList<NotesRichTextRecordData> ReadRichTextRecords()
    {
        EnsureItemAlive();
        return Session.Api.GetRichTextRecords(Parent.Handle, Name);
    }

    private static NotesException StructuralWriteNotSupported(string member) =>
        new(445, member + " requires atomic composite-data rewrite support and is not supported by this runtime yet.");
}
