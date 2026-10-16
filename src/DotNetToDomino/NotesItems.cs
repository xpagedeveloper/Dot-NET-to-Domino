using System.Text;
using DotNetToDomino.Interop;
using System.Buffers.Binary;

namespace DotNetToDomino;

/// <summary>Describes one paragraph in a rich-text item.</summary>
public sealed record NotesRichTextParagraph(int Index, string Text);

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
        return Metadata.DataType == NotesNativeApi.NotesTypeComposite ? this as NotesRichTextItem ?? Parent.RegisterRichText(Parent.ParentDatabase.Own(new NotesRichTextItem(Session, Parent, Name))) : null;
    }
    public void Remove() { EnsureItemAlive(); Session.Api.RemoveItemByBlock(Parent.Handle, Name); _removed = true; }
    public NotesItem CopyToDocument(NotesDocument destination, string? newName = null)
    {
        EnsureItemAlive(); ArgumentNullException.ThrowIfNull(destination);
        if (!ReferenceEquals(Session, destination.ParentDatabase.Parent)) throw new ArgumentException("The destination must belong to the same NotesSession.", nameof(destination));
        var targetName = string.IsNullOrWhiteSpace(newName) ? Name : newName.Trim();
        Session.Api.CopyItemToDocument(Parent.Handle, Name, destination.Handle, targetName, copyAttachments: true);
        destination.AdvanceRichTextRevision(targetName);
        return destination.GetFirstItem(targetName) ?? throw new InvalidOperationException("Copied NotesItem could not be reopened.");
    }
    protected void EnsureItemAlive() { EnsureAlive(); if (_removed) throw new ObjectDisposedException(nameof(NotesItem), "NotesItem has been removed from its document."); _ = Parent.Handle; }
    internal bool IsRemoved => _removed;
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
    private NotesRichTextStyleState? _appendStyleState;
    private int _sectionDepth;
    private int? _insertRecord;
    private long _preparedRevision = -1;

    internal NotesRichTextItem(NotesSession session, NotesDocument parent, string name) : base(session, parent, name) { }

    /// <summary>Returns the Domino font-face number used by a named rich-text
    /// font family. Domino stores a face number, not a Windows font name. Unknown names use Domino's default Swiss face (font number 0).</summary>
    public int GetFont(string fontName)
    {
        EnsureItemAlive();
        ArgumentException.ThrowIfNullOrWhiteSpace(fontName);
        return fontName.Trim().ToUpperInvariant() switch
        {
            "ARIAL" or "HELVETICA" or "SWISS" or "SANS-SERIF" => 0,
            "TIMES" or "TIMES NEW ROMAN" or "ROMAN" or "SERIF" => 1,
            "COURIER" or "COURIER NEW" or "MODERN" or "MONOSPACE" => 2,
            "SCRIPT" => 3,
            "DECORATIVE" => 4,
            _ => 0
        };
    }

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
        var before = _insertRecord is null ? null : ReadRichTextRecords();
        var metadata = Session.Api.AttachRichTextFile(Parent.Handle, Name, sourcePath);
        if (before is null) Parent.AdvanceRichTextRevision(Name);
        else RepositionAppendedRecords(before);
        return new NotesEmbeddedObject(Session, this, metadata);
    }

    /// <summary>Embeds an image file in this rich-text item using Notes' image attachment path.</summary>
    /// <remarks>The image is stored as a rich-text embedded object and remains available to
    /// Notes clients. Inline CDGRAPHIC rendering is handled by Notes when the object is opened.</remarks>
    public NotesEmbeddedObject EmbedImage(string sourcePath)
    {
        EnsureItemAlive();
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        sourcePath = sourcePath.Trim();
        if (!File.Exists(sourcePath)) throw new FileNotFoundException("Image source file was not found.", sourcePath);
        if (!IsSupportedImageFile(sourcePath)) throw new ArgumentException("Supported image formats are PNG, JPEG, GIF, BMP, and TIFF.", nameof(sourcePath));
        return AttachFile(sourcePath);
    }

    /// <summary>Embeds an image as inline CDGRAPHIC/CDIMAGE records.</summary>
    public void EmbedImageInline(string sourcePath)
    {
        EnsureItemAlive(); ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath); sourcePath = Path.GetFullPath(sourcePath.Trim());
        if (!File.Exists(sourcePath)) throw new FileNotFoundException("Image source file was not found.", sourcePath);
        var data = File.ReadAllBytes(sourcePath); if (!TryImageInfo(data, out var type, out var width, out var height)) throw new ArgumentException("Unsupported or invalid image format.", nameof(sourcePath));
        AppendWithInsertion(() => Session.Api.AppendRichTextImage(Parent.Handle, Name, data, type, width, height));
    }

    /// <summary>Embeds an inline image at the navigator's current compound-text
    /// position. The image is inserted before the current record.</summary>
    public void EmbedImageInline(NotesRichTextNavigator navigator, string sourcePath)
    {
        EnsureItemAlive();
        ArgumentNullException.ThrowIfNull(navigator);
        if (!ReferenceEquals(navigator.RichTextItem, this))
            throw new ArgumentException("The navigator must belong to this rich-text item.", nameof(navigator));
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        sourcePath = Path.GetFullPath(sourcePath.Trim());
        if (!File.Exists(sourcePath)) throw new FileNotFoundException("Image source file was not found.", sourcePath);
        var data = File.ReadAllBytes(sourcePath);
        if (!TryImageInfo(data, out var type, out var width, out var height))
            throw new ArgumentException("Unsupported or invalid image format.", nameof(sourcePath));
        if (_insertRecord is not null) throw new InvalidOperationException("Rich-text insertion is already active.");
        _insertRecord = navigator.CurrentIndex;
        AppendWithInsertion(() => Session.Api.AppendRichTextImage(Parent.Handle, Name, data, type, width, height));
    }

    private static bool TryImageInfo(byte[] data, out ushort type, out ushort width, out ushort height)
    {
        type = width = height = 0;
        if (data.Length >= 24 && data.AsSpan(0, 8).SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A })) { type = 3; width = (ushort)Math.Min(ushort.MaxValue, BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(16, 4))); height = (ushort)Math.Min(ushort.MaxValue, BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(20, 4))); return true; }
        if (data.Length >= 10 && (data.AsSpan(0, 6).SequenceEqual("GIF89a"u8) || data.AsSpan(0, 6).SequenceEqual("GIF87a"u8))) { type = 1; width = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(6, 2)); height = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(8, 2)); return true; }
        if (data.Length >= 26 && data[0] == 0x42 && data[1] == 0x4D) { type = 3; width = (ushort)Math.Min(ushort.MaxValue, Math.Abs(BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(18, 4)))); height = (ushort)Math.Min(ushort.MaxValue, Math.Abs(BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(22, 4)))); return true; }
        if (data.Length >= 4 && data[0] == 0xFF && data[1] == 0xD8)
        {
            for (var p = 2; p + 9 < data.Length; )
            {
                if (data[p] != 0xFF) { p++; continue; }
                while (p < data.Length && data[p] == 0xFF) p++;
                if (p >= data.Length) break;
                var marker = data[p++];
                if (marker is 0xD8 or 0xD9) continue;
                if (p + 1 >= data.Length) break;
                var length = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(p, 2));
                if (length < 2 || p + length > data.Length) break;
                if ((marker is >= 0xC0 and <= 0xC3) || (marker is >= 0xC5 and <= 0xC7) || (marker is >= 0xC9 and <= 0xCB) || (marker is >= 0xCD and <= 0xCF))
                { height = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(p + 3, 2)); width = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(p + 5, 2)); type = 2; return width != 0 && height != 0; }
                p += length;
            }
        }
        return false;
    }

    private static bool IsSupportedImageFile(string path)
    {
        using var stream = File.OpenRead(path);
        Span<byte> header = stackalloc byte[12];
        var read = stream.Read(header);
        if (read >= 8 && header[..8].SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A })) return true;
        if (read >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF) return true;
        if (read >= 6 && header[..6].SequenceEqual("GIF89a"u8) || read >= 6 && header[..6].SequenceEqual("GIF87a"u8)) return true;
        if (read >= 2 && header[0] == 0x42 && header[1] == 0x4D) return true;
        if (read >= 4 && header[..4].SequenceEqual(new byte[] { 0x49, 0x49, 0x2A, 0x00 }) || read >= 4 && header[..4].SequenceEqual(new byte[] { 0x4D, 0x4D, 0x00, 0x2A })) return true;
        return false;
    }

    /// <summary>Embeds a file object in this rich-text item.</summary>
    public NotesEmbeddedObject AppendObject(string sourcePath) => AttachFile(sourcePath);

    /// <summary>Embeds an object using LotusScript-compatible embed type constants.</summary>
    public NotesEmbeddedObject AppendObject(int type, string? className, string sourcePath, string? objectName = null)
    {
        EnsureItemAlive();
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        if (type != NotesConst.EMBED_ATTACHMENT)
            throw new NotSupportedException("OLE object and OLE object-link embedding require the Notes client OLE subsystem; the C API wrapper currently supports EMBED_ATTACHMENT only.");
        var before = _insertRecord is null ? null : ReadRichTextRecords();
        var metadata = Session.Api.AttachRichTextFile(Parent.Handle, Name, sourcePath, objectName);
        if (before is null) Parent.AdvanceRichTextRevision(Name); else RepositionAppendedRecords(before);
        return new NotesEmbeddedObject(Session, this, metadata);
    }

    /// <summary>Copies an existing embedded attachment into this rich-text item.</summary>
    public NotesEmbeddedObject AppendObject(NotesEmbeddedObject source)
    {
        EnsureItemAlive();
        ArgumentNullException.ThrowIfNull(source);
        if (!ReferenceEquals(source.Parent.Session, Session)) throw new ArgumentException("The source embedded object must belong to the same NotesSession.", nameof(source));
        var temporaryDirectory = Path.Combine(Path.GetTempPath(), "dotnet-to-domino-richtext-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temporaryDirectory);
        var temporary = Path.Combine(temporaryDirectory, source.Name);
        try
        {
            File.WriteAllBytes(temporary, source.ToByteArray());
            return AttachFile(temporary);
        }
        finally { try { if (Directory.Exists(temporaryDirectory)) Directory.Delete(temporaryDirectory, true); } catch { } }
    }

    /// <summary>Copies an existing embedded object before the navigator's current record.</summary>
    public NotesEmbeddedObject AppendObject(NotesRichTextNavigator navigator, NotesEmbeddedObject source)
    {
        BeginInsert(navigator);
        return AppendObject(source);
    }

    public void AppendText(string value)
    {
        EnsureItemAlive();
        ArgumentNullException.ThrowIfNull(value);
        var text = value;
        AppendWithInsertion(() =>
        {
            if (_appendStyleState is NotesRichTextStyleState style) Session.Api.AppendRichTextStyled(Parent.Handle, Name, text, style);
            else Session.Api.AppendRichText(Parent.Handle, Name, text);
        });
    }

    /// <summary>Appends text using the supplied character style.</summary>
    public void AppendText(string value, NotesRichTextStyle style)
    {
        ArgumentNullException.ThrowIfNull(style);
        AppendStyle(style);
        AppendText(value);
    }

    /// <summary>Inserts styled text before the navigator's current record.</summary>
    public void AppendText(NotesRichTextNavigator navigator, string value, NotesRichTextStyle style)
    {
        BeginInsert(navigator);
        AppendText(value, style);
    }
    /// <summary>Inserts text before the navigator's current record. Call
    /// <see cref="EndInsert"/> when all content at that position has been added.</summary>
    public void AppendText(NotesRichTextNavigator navigator, string value)
    {
        BeginInsert(navigator);
        AppendText(value);
    }

    /// <summary>Inserts an attachment before the navigator's current record.</summary>
    public NotesEmbeddedObject AppendObject(NotesRichTextNavigator navigator, string sourcePath)
    {
        BeginInsert(navigator);
        return AttachFile(sourcePath);
    }

    /// <summary>Inserts a table before the navigator's current record.</summary>
    public void AppendTable(NotesRichTextNavigator navigator, int rows, int columns)
    {
        BeginInsert(navigator);
        AppendTable(rows, columns);
    }

    /// <summary>Appends a paragraph break using the same compound-text delimiter as Notes.</summary>
    public void AppendNewLine()
    {
        EnsureItemAlive();
        AppendText("\r\n");
    }

    public void AppendNewLine(NotesRichTextNavigator navigator)
    {
        BeginInsert(navigator);
        AppendNewLine();
    }

    /// <summary>Appends a page-break paragraph marker.</summary>
    public void AppendPageBreak()
    {
        EnsureItemAlive();
        using var style = Session.CreateRichTextParagraphStyle();
        style.Pagination = 1; // PABFLAG_PAGINATE_BEFORE from JNX/editods.h
        AppendParagraphStyle(style);
    }

    /// <summary>Appends a page break using the supplied paragraph style.</summary>
    public void AppendPageBreak(NotesRichTextParagraphStyle style)
    {
        EnsureItemAlive();
        ArgumentNullException.ThrowIfNull(style);
        style.Pagination = 1;
        AppendParagraphStyle(style);
    }

    /// <summary>Appends a horizontal-rule paragraph marker.</summary>
    public void AppendHorizontalRule()
    {
        EnsureItemAlive();
        AppendWithInsertion(() => Session.Api.AppendRichTextHorizontalRule(Parent.Handle, Name));
    }

    public void AppendHorizontalRule(NotesRichTextNavigator navigator)
    {
        BeginInsert(navigator);
        AppendHorizontalRule();
    }

    public void AppendPageBreak(NotesRichTextNavigator navigator, NotesRichTextParagraphStyle? style = null)
    {
        BeginInsert(navigator);
        if (style is null) AppendPageBreak(); else AppendPageBreak(style);
    }

    /// <summary>Starts inserting subsequent text and paragraph content before the navigator's current record.</summary>
    public void BeginInsert(NotesRichTextNavigator navigator)
    {
        EnsureItemAlive();
        ArgumentNullException.ThrowIfNull(navigator);
        if (!ReferenceEquals(navigator.RichTextItem, this)) throw new ArgumentException("The navigator must belong to this rich-text item.", nameof(navigator));
        if (_insertRecord is not null) throw new InvalidOperationException("Rich-text insertion is already active.");
        _insertRecord = navigator.CurrentIndex;
    }

    /// <summary>Starts inserting before the first record covered by a rich-text range.</summary>
    public void BeginInsert(NotesRichTextRange range)
        => BeginInsert(range, atEnd: false);

    /// <summary>Starts inserting at the beginning or end of a rich-text range.</summary>
    public void BeginInsert(NotesRichTextRange range, bool atEnd)
    {
        EnsureItemAlive();
        ArgumentNullException.ThrowIfNull(range);
        if (!ReferenceEquals(range.RichTextItem, this)) throw new ArgumentException("The range must belong to this rich-text item.", nameof(range));
        if (_insertRecord is not null) throw new InvalidOperationException("Rich-text insertion is already active.");
        _insertRecord = atEnd ? range.EndRecord + 1 : range.BeginRecord;
    }

    /// <summary>Starts inserting before a concrete rich-text element.</summary>
    public void BeginInsert(NotesRichTextLinkedObject element)
    {
        EnsureItemAlive();
        ArgumentNullException.ThrowIfNull(element);
        if (!ReferenceEquals(element.PositionOwner, this)) throw new ArgumentException("The element must belong to this rich-text item.", nameof(element));
        if (_insertRecord is not null) throw new InvalidOperationException("Rich-text insertion is already active.");
        var position = element.PositionRecord();
        var records = ReadRichTextRecords();
        var index = Enumerable.Range(0, records.Count).FirstOrDefault(i =>
            records[i].SegmentIndex == position.SegmentIndex && records[i].RecordIndex == position.RecordIndex && records[i].Signature == position.Signature, -1);
        if (index < 0) throw new InvalidOperationException("The rich-text element no longer exists.");
        _insertRecord = index;
    }

    /// <summary>Starts inserting before an embedded object.</summary>
    public void BeginInsert(NotesEmbeddedObject element)
    {
        EnsureItemAlive();
        ArgumentNullException.ThrowIfNull(element);
        if (!ReferenceEquals(element.Parent, this)) throw new ArgumentException("The embedded object must belong to this rich-text item.", nameof(element));
        if (_insertRecord is not null) throw new InvalidOperationException("Rich-text insertion is already active.");
        var records = ReadRichTextRecords();
        var index = Enumerable.Range(0, records.Count).FirstOrDefault(i =>
            records[i].ElementType == NotesRichTextNavigator.ElementEmbeddedObject &&
            string.Equals(records[i].LinkedObjectName, element.Name, StringComparison.OrdinalIgnoreCase), -1);
        if (index < 0) throw new InvalidOperationException("The embedded object no longer exists.");
        _insertRecord = index;
    }

    /// <summary>Ends the current insertion mode and restores normal append behavior.</summary>
    public void EndInsert()
    {
        EnsureItemAlive();
        if (_insertRecord is null) throw new InvalidOperationException("Rich-text insertion is not active.");
        _insertRecord = null;
    }

    /// <summary>Appends the composite contents of another rich-text item.</summary>
    public void AppendRTItem(NotesRichTextItem source)
    {
        EnsureItemAlive();
        ArgumentNullException.ThrowIfNull(source);
        source.EnsureItemAlive();
        if (!ReferenceEquals(Session, source.Session)) throw new ArgumentException("The source rich-text item must belong to the same NotesSession.", nameof(source));
        AppendWithInsertion(() => Session.Api.AppendRichTextItem(Parent.Handle, Name, source.Parent.Handle, source.Name));
    }

    /// <summary>Inserts a complete rich-text item before the navigator's current record.</summary>
    public void AppendRTItem(NotesRichTextNavigator navigator, NotesRichTextItem source)
    {
        BeginInsert(navigator);
        AppendRTItem(source);
    }

    public void AppendStyle(NotesRichTextStyle style)
    {
        EnsureItemAlive();
        ArgumentNullException.ThrowIfNull(style);
        var next = style.ExportState();
        if (_appendStyleState is NotesRichTextStyleState previous)
        {
            static int Merge(int value, int existing) => value == NotesRichTextStyle.StyleNoChange ? existing : value;
            next = new NotesRichTextStyleState(
                Merge(next.Bold, previous.Bold), Merge(next.Effects, previous.Effects), Merge(next.FontSize, previous.FontSize),
                Merge(next.Italic, previous.Italic), Merge(next.NotesColor, previous.NotesColor), Merge(next.NotesFont, previous.NotesFont),
                Merge(next.PassThruHtml, previous.PassThruHtml), Merge(next.Strikethrough, previous.Strikethrough), Merge(next.Underline, previous.Underline));
        }
        _appendStyleState = next;
    }

    /// <summary>Starts applying a character style at the navigator's current position.</summary>
    public void AppendStyle(NotesRichTextNavigator navigator, NotesRichTextStyle style)
    {
        BeginInsert(navigator);
        AppendStyle(style);
    }

    /// <summary>
    /// Persists the current document, including all changes made to this rich-text item.
    /// Rich-text edits are stored in the owning note; there is no separate native item
    /// handle to update independently.
    /// </summary>
    public void Update()
    {
        EnsureItemAlive();
        if (_sectionDepth != 0) throw new InvalidOperationException("A rich-text section is still open. Call EndSection() before Update().");
        Parent.Save();
        Parent.AdvanceRichTextRevision(Name);
    }

    internal void PrepareForDocumentSave()
    {
        EnsureItemAlive();
        var revision = Parent.GetRichTextRevision(Name);
        if (_sectionDepth != 0) throw new InvalidOperationException("A rich-text section is still open. Call EndSection() before saving the document.");
        if (_insertRecord is not null) throw new InvalidOperationException("Rich-text insertion is still active. Call EndInsert() before saving the document.");
        if (revision == _preparedRevision) return;
        ValidateCompoundText();
        _preparedRevision = revision;
    }

    /// <summary>Checks the compound-text stream before it is persisted.</summary>
    /// <remarks>
    /// The C API can expose a partially edited CD stream after a managed transform.
    /// Saving such a stream lets Notes reject (or, on some releases, repair) the note
    /// later. Validate its structural begin/end pairs at the managed save boundary so
    /// callers get a deterministic exception while the rich-text item is inspectable.
    /// </remarks>
    private void ValidateCompoundText()
    {
        var records = ReadRichTextRecords();
        var tableDepth = 0;
        var sectionDepth = 0;
        var embeddedDepth = 0;
        for (var recordIndex = 0; recordIndex < records.Count; recordIndex++)
        {
            var record = records[recordIndex];
            // Notes writes a V5 RCLINK/resource prefix before the V4 HOTLINK
            // representation of the same region. Its single end is V4HOTSPOTEND.
            // Recognize only this observed, versioned sequence; other V5 regions
            // still require their own matching end.
            if (record.Signature == unchecked((ushort)-130) && record.Data.Length >= 12 &&
                record.Data[4] == 32 && record.Data[5] == 0 && recordIndex + 2 < records.Count &&
                records[recordIndex + 1].Signature == 0xfff6 &&
                records[recordIndex + 2].Signature == unchecked((ushort)-83) &&
                records[recordIndex + 2].Data.Length >= 12 &&
                records[recordIndex + 2].Data[4] == 11 && records[recordIndex + 2].Data[5] == 0)
                continue;
            switch (record.Signature)
            {
                case 163: case 207: tableDepth++; break;
                case 165: case 209:
                    if (--tableDepth < 0) throw new NotesException(91, "Rich-text table end record has no matching begin record.");
                    break;
                case unchecked((ushort)-84): sectionDepth++; break;
                case unchecked((ushort)-82):
                    if (--sectionDepth < 0) throw new NotesException(91, "Rich-text section end record has no matching begin record.");
                    break;
                case unchecked((ushort)-87): case unchecked((ushort)-83): case unchecked((ushort)-130): embeddedDepth++; break;
                case 170: case 174: case 127:
                    if (--embeddedDepth < 0) throw new NotesException(91, "Rich-text embedded-object end record has no matching begin record.");
                    break;
            }
        }
        if (tableDepth != 0) throw new NotesException(91, "Rich-text table has no matching end record.");
        if (sectionDepth != 0) throw new NotesException(91, "Rich-text section has no matching end record.");
        if (embeddedDepth != 0) throw new NotesException(91, "Rich-text embedded object has no matching end record.");
    }

    /// <summary>Adds a document link to another document in this rich-text item.</summary>
    public void AppendDocLink(NotesDocument document, string? comment = null)
    {
        EnsureItemAlive();
        ArgumentNullException.ThrowIfNull(document);
        if (!ReferenceEquals(Session, document.ParentDatabase.Parent))
            throw new ArgumentException("The linked document must belong to the same NotesSession.", nameof(document));
        AppendWithInsertion(() => Session.Api.AppendRichTextDocLink(Parent.Handle, Name, document.ParentDatabase.Handle, document.UniversalId, comment ?? string.Empty));
    }

    public void AppendDocLink(NotesRichTextNavigator navigator, NotesDocument document, string? comment = null)
    {
        BeginInsert(navigator);
        AppendDocLink(document, comment);
    }

    public void AppendDocLink(NotesRichTextNavigator navigator, NotesDocument document, string? comment, string? hotspotText)
    {
        BeginInsert(navigator);
        AppendDocLink(document, comment, hotspotText);
    }

    public void AppendNotesURL(NotesRichTextNavigator navigator, string notesUrl, string? comment = null)
    {
        BeginInsert(navigator);
        AppendNotesURL(notesUrl, comment);
    }

    public void AppendNotesURL(NotesRichTextNavigator navigator, string notesUrl, string? comment, string? hotspotText)
    {
        BeginInsert(navigator);
        AppendNotesURL(notesUrl, comment);
        if (hotspotText is null) return;
        using var links = CreateNavigator();
        using var link = links.GetLastElement(NotesRichTextNavigator.ElementDocLink) as NotesRichTextDocLink
            ?? throw new InvalidOperationException("The appended Notes URL link could not be located.");
        link.SetHotSpotText(hotspotText);
    }

    /// <summary>Appends a document link with optional clickable hotspot text.</summary>
    public void AppendDocLink(NotesDocument document, string? comment, string? hotspotText)
    {
        AppendDocLink(document, comment);
        if (hotspotText is null) return;
        using var navigator = CreateNavigator();
        using var link = navigator.GetLastElement(NotesRichTextNavigator.ElementDocLink) as NotesRichTextDocLink
            ?? throw new InvalidOperationException("The appended document link could not be located.");
        link.SetHotSpotText(hotspotText);
    }

    /// <summary>Adds a link to a Notes database.</summary>
    public void AppendDocLink(NotesDatabase database, string? comment = null)
    {
        EnsureItemAlive();
        ArgumentNullException.ThrowIfNull(database);
        if (!ReferenceEquals(Session, database.Parent)) throw new ArgumentException("The linked database must belong to the same NotesSession.", nameof(database));
        AppendWithInsertion(() => Session.Api.AppendRichTextDocLink(Parent.Handle, Name, database.Handle, null, comment ?? string.Empty));
    }

    public void AppendDocLink(NotesRichTextNavigator navigator, NotesDatabase database, string? comment = null)
    {
        BeginInsert(navigator);
        AppendDocLink(database, comment);
    }

    public void AppendDocLink(NotesRichTextNavigator navigator, NotesDatabase database, string? comment, string? hotspotText)
    {
        BeginInsert(navigator);
        AppendDocLink(database, comment, hotspotText);
    }

    public void AppendDocLink(NotesDatabase database, string? comment, string? hotspotText)
    {
        AppendDocLink(database, comment);
        if (hotspotText is null) return;
        using var navigator = CreateNavigator();
        using var link = navigator.GetLastElement(NotesRichTextNavigator.ElementDocLink) as NotesRichTextDocLink
            ?? throw new InvalidOperationException("The appended database link could not be located.");
        link.SetHotSpotText(hotspotText);
    }

    /// <summary>Adds a link to a Notes view or folder.</summary>
    public void AppendDocLink(NotesView view, string? comment = null)
    {
        EnsureItemAlive();
        ArgumentNullException.ThrowIfNull(view);
        if (!ReferenceEquals(Session, view.Parent.Parent)) throw new ArgumentException("The linked view must belong to the same NotesSession.", nameof(view));
        AppendWithInsertion(() => Session.Api.AppendRichTextDocLink(Parent.Handle, Name, view.Parent.Handle, null, comment ?? string.Empty, view.UniversalID));
    }

    public void AppendDocLink(NotesRichTextNavigator navigator, NotesView view, string? comment = null)
    {
        BeginInsert(navigator);
        AppendDocLink(view, comment);
    }

    public void AppendDocLink(NotesRichTextNavigator navigator, NotesView view, string? comment, string? hotspotText)
    {
        BeginInsert(navigator);
        AppendDocLink(view, comment, hotspotText);
    }

    public void AppendDocLink(NotesView view, string? comment, string? hotspotText)
    {
        AppendDocLink(view, comment);
        if (hotspotText is null) return;
        using var navigator = CreateNavigator();
        using var link = navigator.GetLastElement(NotesRichTextNavigator.ElementDocLink) as NotesRichTextDocLink
            ?? throw new InvalidOperationException("The appended view link could not be located.");
        link.SetHotSpotText(hotspotText);
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
        if (parts.Length < 1)
            throw new ArgumentException("The Notes URL must contain a database path.", nameof(notesUrl));
        var databaseEnd = Array.FindIndex(parts, part => Uri.UnescapeDataString(part).EndsWith(".nsf", StringComparison.OrdinalIgnoreCase) || Uri.UnescapeDataString(part).EndsWith(".ntf", StringComparison.OrdinalIgnoreCase));
        if (databaseEnd < 0) databaseEnd = parts.Length <= 2 ? 0 : parts.Length - 3;
        var filePath = Uri.UnescapeDataString(string.Join('/', parts[..(databaseEnd + 1)]));
        var suffix = parts[(databaseEnd + 1)..];
        if (suffix.Length > 2) throw new ArgumentException("The Notes URL contains an unsupported path suffix.", nameof(notesUrl));
        if (suffix.Length == 1 && !suffix[0].Equals("0", StringComparison.OrdinalIgnoreCase))
        {
            using var viewDatabase = Session.OpenDatabase(uri.Host, filePath);
            if (!viewDatabase.IsOpen) throw new NotesException(4091, $"Unable to open Notes URL database '{filePath}'.");
            using var view = viewDatabase.GetView(Uri.UnescapeDataString(suffix[0])) ?? throw new NotesException(4091, $"Unable to open Notes URL view '{suffix[0]}'.");
            AppendDocLink(view, comment);
            return;
        }
        using var database = Session.OpenDatabase(uri.Host, filePath);
        if (!database.IsOpen) throw new NotesException(4091, $"Unable to open Notes URL database '{filePath}'.");
        if (suffix.Length == 0 || (suffix.Length == 1 && suffix[0] == "0")) { AppendDocLink(database, comment); return; }
        var identifier = Uri.UnescapeDataString(parts[^1]);
        using var document = identifier.Length == 32
            ? database.GetDocumentByUNID(identifier)
            : uint.TryParse(identifier, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out var noteId)
                ? database.GetDocumentByID(noteId)
                : null;
        if (document is null) throw new NotesException(4091, $"Unable to open Notes URL document '{identifier}'.");
        AppendDocLink(document, comment);
    }

    /// <summary>Appends an empty Domino RichText table with the requested dimensions.</summary>
    public void AppendTable(int rows, int columns)
    {
        EnsureItemAlive();
        if (rows is < 1 or > byte.MaxValue) throw new ArgumentOutOfRangeException(nameof(rows), "Rich-text tables support 1 through 255 rows.");
        if (columns is < 1 or > byte.MaxValue) throw new ArgumentOutOfRangeException(nameof(columns), "Rich-text tables support 1 through 255 columns.");
        AppendWithInsertion(() => Session.Api.AppendRichTextTable(Parent.Handle, Name, rows, columns));
    }

    public void AppendParagraphStyle(NotesRichTextParagraphStyle style)
    {
        EnsureItemAlive();
        ArgumentNullException.ThrowIfNull(style);
        AppendWithInsertion(() => Session.Api.AppendRichTextParagraphStyle(Parent.Handle, Name, style.ExportState()));
    }

    /// <summary>Inserts a paragraph style at the navigator's current position.</summary>
    public void AppendParagraphStyle(NotesRichTextNavigator navigator, NotesRichTextParagraphStyle style)
    {
        BeginInsert(navigator);
        AppendParagraphStyle(style);
    }

    /// <summary>Appends an empty collapsible section with a text title.</summary>
    public void AppendSection(string title, bool expanded = true)
    {
        EnsureItemAlive();
        ArgumentNullException.ThrowIfNull(title);
        AppendWithInsertion(() => Session.Api.AppendRichTextSection(Parent.Handle, Name, title, expanded));
    }

    public void AppendSection(NotesRichTextNavigator navigator, string title, bool expanded = true)
    {
        BeginInsert(navigator);
        AppendSection(title, expanded);
    }

    public void BeginSection(string title, bool expanded = true)
    {
        EnsureItemAlive();
        ArgumentNullException.ThrowIfNull(title);
        AppendWithInsertion(() => Session.Api.BeginRichTextSection(Parent.Handle, Name, title, expanded));
        _sectionDepth++;
    }

    public void BeginSection(NotesRichTextNavigator navigator, string title, bool expanded = true)
    {
        BeginInsert(navigator);
        BeginSection(title, expanded);
    }

    public void EndSection()
    {
        EnsureItemAlive();
        if (_sectionDepth == 0) throw new InvalidOperationException("No rich-text section is currently open.");
        AppendWithInsertion(() => Session.Api.EndRichTextSection(Parent.Handle, Name));
        _sectionDepth--;
    }

    /// <summary>Repacks the composite stream and coalesces adjacent text records with identical fonts. Invalidates existing rich-text positions.</summary>
    public void Compact()
    {
        EnsureItemAlive();
        if (_sectionDepth != 0) throw new InvalidOperationException("A rich-text section is still open. Call EndSection() before Compact().");
        ValidateCompoundText();
        var records = ReadRichTextRecords();
        var compacted = NotesRichTextCdTransform.Compact(records);
        RewriteRichTextRecords(compacted);
        ValidateCompoundText();
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

    /// <summary>Reads an embedded attachment by its filename.</summary>
    public byte[] GetAttachmentBytes(string name)
    {
        EnsureItemAlive(); ArgumentException.ThrowIfNullOrWhiteSpace(name);
        using var attachment = GetEmbeddedObject(name.Trim()) ?? throw new FileNotFoundException("Rich-text attachment was not found.", name);
        return attachment.ToByteArray();
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

    /// <summary>All file attachments found in this rich-text item.</summary>
    public IReadOnlyList<NotesEmbeddedObject> Attachments => EmbeddedObjects;
    public NotesEmbeddedObject GetEmbeddedObject(int index) => EmbeddedObjects[index];
    public int AttachmentCount => EmbeddedObjects.Count;

    /// <summary>Returns embedded objects whose bytes are recognized as common image formats.</summary>
    public IReadOnlyList<NotesEmbeddedObject> EmbeddedImages
    {
        get
        {
            EnsureItemAlive();
            return EmbeddedObjects.Where(IsImage).ToArray();
        }
    }

    /// <summary>Returns all tables in document order.</summary>
    public IReadOnlyList<NotesRichTextTable> Tables
    {
        get
        {
            EnsureItemAlive();
            using var navigator = CreateNavigator();
            var result = new List<NotesRichTextTable>();
            if (!navigator.FindFirstElement(NotesRichTextNavigator.ElementTable)) return result;
            do
            {
                if (navigator.GetElement() is NotesRichTextTable table) result.Add(table);
            }
            while (navigator.FindNextElement(NotesRichTextNavigator.ElementTable));
            return result;
        }
    }

    public int TableCount => Tables.Count;

    /// <summary>Returns all document, database and view links in document order.</summary>
    public IReadOnlyList<NotesRichTextDocLink> Links
    {
        get
        {
            EnsureItemAlive();
            using var navigator = CreateNavigator();
            var result = new List<NotesRichTextDocLink>();
            if (!navigator.FindFirstElement(NotesRichTextNavigator.ElementDocLink)) return result;
            do
            {
                if (navigator.GetElement() is NotesRichTextDocLink link) result.Add(link);
            }
            while (navigator.FindNextElement(NotesRichTextNavigator.ElementDocLink));
            return result;
        }
    }

    public int LinkCount => Links.Count;

    /// <summary>Returns all hotspot begin records in compound-text order.</summary>
    /// <remarks>The C API stores hotspot kinds as CD records rather than as a
    /// separate collection. The returned objects expose the verified type and
    /// flags without guessing at the hotspot payload.</remarks>
    public IReadOnlyList<NotesRichTextHotspot> Hotspots
    {
        get
        {
            EnsureItemAlive();
            return ReadRichTextRecords()
                .Where(record => record.Signature is 0xFFA9 or 0xFFAD or 0xFF7E)
                .Select(record => new NotesRichTextHotspot(Session, this, record))
                .ToArray();
        }
    }

    public int HotspotCount => Hotspots.Count;
    public NotesRichTextHotspot GetHotspot(int index) => Hotspots[index];

    /// <summary>Returns text paragraphs in compound-text order, including empty paragraphs.</summary>
    public IReadOnlyList<string> ParagraphTexts => ReadParagraphs().Select(paragraph => paragraph.Text).ToArray();

    public int ParagraphCount => ParagraphTexts.Count;

    /// <summary>Returns paragraph text with stable zero-based positions for navigator-based processing.</summary>
    public IReadOnlyList<NotesRichTextParagraph> Paragraphs => ReadParagraphs();

    private IReadOnlyList<NotesRichTextParagraph> ReadParagraphs()
    {
        EnsureItemAlive();
        var records = ReadRichTextRecords();
        var result = new List<NotesRichTextParagraph>();
        StringBuilder? text = null;
        foreach (var record in records)
        {
            if (record.ElementType == NotesRichTextNavigator.ElementTextParagraph)
            {
                if (text is not null) result.Add(new NotesRichTextParagraph(result.Count, text.ToString() ?? string.Empty));
                text = new StringBuilder(record.Text);
            }
            else if (text is not null && record.ElementType == NotesRichTextNavigator.ElementTextRun)
            {
                text.Append(record.Text);
            }
        }
        if (text is not null) result.Add(new NotesRichTextParagraph(result.Count, text.ToString() ?? string.Empty));
        return result;
    }
    /// <summary>Indicates whether this item contains Notes tabbed-table records.</summary>
    public bool HasTabbedTable => ReadRichTextRecords().Any(record => record.Signature == 0xFFE3);

    /// <summary>Returns the number of tabbed-table records detected in this item.</summary>
    public int TabbedTableRecordCount => ReadRichTextRecords().Count(record => record.Signature == 0xFFE3);

    /// <summary>Gets the number of inline images stored in this rich-text item.</summary>
    public int InlineImageCount => ReadRichTextRecords().Count(record => record.Signature is 125 or 149);

    /// <summary>Gets each inline image in document order; use <see cref="NotesInlineImage.SaveAs"/> to export one image.</summary>
    public IReadOnlyList<NotesInlineImage> InlineImages => ReadInlineImages();
    public NotesInlineImage GetInlineImage(int index) => InlineImages[index];

    private IReadOnlyList<NotesInlineImage> ReadInlineImages()
    {
        var records = ReadRichTextRecords(); var result = new List<NotesInlineImage>();
        for (var i = 0; i < records.Count; i++)
        {
            var isImageHeader = records[i].Signature is 125 or 149;
            if (!isImageHeader || records[i].Data.Length < 12) continue;
            var type = records[i].Signature == 149 ? (ushort)3 : BinaryPrimitives.ReadUInt16LittleEndian(records[i].Data.AsSpan(6, 2)); var bytes = new List<byte>(); var start = i + 1;
            if (start < records.Count && records[start].Signature == 82) start++;
            var segmentSignature = records[i].Signature == 149 ? (ushort)150 : (ushort)124;
            for (var j = start; j < records.Count && records[j].Signature == segmentSignature; j++) { var data = records[j].Data; var n = data.Length >= 10 ? BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(6, 2)) : 0; var available = n == 0 || n > data.Length - 10 ? Math.Max(0, data.Length - 10) : n; bytes.AddRange(data.AsSpan(10, available).ToArray()); i = j; }
            result.Add(new NotesInlineImage(result.Count + 1, type, bytes.ToArray()));
        }
        return result;
    }

    /// <summary>Extracts all recognized embedded images to a directory and returns the created paths.</summary>
    public IReadOnlyList<string> ExtractEmbeddedImages(string directory)
    {
        EnsureItemAlive();
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        directory = Path.GetFullPath(directory.Trim());
        Directory.CreateDirectory(directory);
        var result = new List<string>();
        foreach (var image in EmbeddedImages)
        {
            var path = Path.Combine(directory, image.Name);
            image.ExtractFile(path);
            result.Add(path);
        }
        return result;
    }

    /// <summary>Extracts each inline CDGRAPHIC image in document order.</summary>
    public IReadOnlyList<string> ExtractInlineImages(string directory)
    {
        EnsureItemAlive(); ArgumentException.ThrowIfNullOrWhiteSpace(directory); directory = Path.GetFullPath(directory.Trim()); Directory.CreateDirectory(directory);
        var result = new List<string>(); foreach (var image in InlineImages) { var path = Path.Combine(directory, $"inline-{image.Index:D3}{image.Extension}"); image.SaveAs(path); result.Add(path); }
        return result;
    }

    private static bool IsImage(NotesEmbeddedObject embedded)
    {
        try
        {
            var bytes = embedded.ToByteArray();
            return IsImageHeader(bytes);
        }
        catch (ObjectDisposedException) { return false; }
    }

    private static bool IsImageHeader(ReadOnlySpan<byte> data)
    {
        return data.Length >= 8 && data[..8].SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A })
            || data.Length >= 3 && data[0] == 0xFF && data[1] == 0xD8 && data[2] == 0xFF
            || data.Length >= 6 && (data[..6].SequenceEqual("GIF89a"u8) || data[..6].SequenceEqual("GIF87a"u8))
            || data.Length >= 2 && data[0] == 0x42 && data[1] == 0x4D
            || data.Length >= 4 && (data[..4].SequenceEqual(new byte[] { 0x49, 0x49, 0x2A, 0x00 }) || data[..4].SequenceEqual(new byte[] { 0x4D, 0x4D, 0x00, 0x2A }));
    }

    internal long RichTextRevision { get { EnsureItemAlive(); return Parent.GetRichTextRevision(Name); } }
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
        Parent.AdvanceRichTextRevision(Name);
    }

    private void AppendWithInsertion(Action append)
    {
        var before = ReadRichTextRecords();
        append();
        if (_insertRecord is null)
        {
            Parent.AdvanceRichTextRevision(Name);
            return;
        }
        RepositionAppendedRecords(before);
    }

    private void RepositionAppendedRecords(IReadOnlyList<NotesRichTextRecordData> before)
    {
        if (_insertRecord is not int insertion) { Parent.AdvanceRichTextRevision(Name); return; }
        var after = ReadRichTextRecords();
        var oldKeys = before.Select(r => (r.SegmentIndex, r.RecordIndex, r.Signature)).ToHashSet();
        var inserted = after.Where(r => !oldKeys.Contains((r.SegmentIndex, r.RecordIndex, r.Signature))).Select(NotesRichTextRecordRewrite.Preserve).ToArray();
        if (inserted.Length == 0) throw new NotesException(91, "Rich-text insertion produced no compound-text records.");
        insertion = Math.Clamp(insertion, 0, before.Count);
        var replacement = before.Take(insertion).Select(NotesRichTextRecordRewrite.Preserve)
            .Concat(inserted)
            .Concat(before.Skip(insertion).Select(NotesRichTextRecordRewrite.Preserve))
            .ToArray();
        // The newly appended records carry physical identities after the old stream.
        // Once moved into the middle they would violate the CD transform ordering
        // invariant, so assign a fresh monotonically ordered identity to the rebuilt stream.
        var ordered = replacement.Select((record, index) =>
            new NotesRichTextRecordRewrite(0, index, record.Signature, record.Data)).ToArray();
        RewriteRichTextRecords(ordered);
        _insertRecord = insertion + inserted.Length;
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
