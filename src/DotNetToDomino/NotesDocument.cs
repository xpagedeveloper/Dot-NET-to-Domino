using System.Globalization;
using DotNetToDomino.Interop;

namespace DotNetToDomino;

/// <summary>Represents an open Notes document.</summary>
public sealed class NotesDocument : NotesObject
{
    private nint _handle;
    private NotesViewRow? _viewRow;

    internal NotesDocument(NotesSession session, NotesDatabase parent, nint handle, uint? knownNoteId, string? knownUnid) : base(session)
    {
        ParentDatabase = parent;
        _handle = handle;
        try
        {
            NoteId = knownNoteId ?? Session.Api.GetNoteId(handle);
            _knownUniversalId = knownUnid ?? (handle == 0 ? string.Empty : Session.Api.GetUnid(handle));
            if (handle != 0 && session.ConvertMIME) Session.Api.ConvertMimePartsToComposite(handle);
        }
        catch
        {
            _handle = 0;
            try { if (handle != 0) Session.Api.CloseNote(handle); } finally { Session.Unregister(this); }
            throw;
        }
    }

    private string _knownUniversalId;
    public NotesDatabase ParentDatabase { get; }
    public NotesDatabase Parent => ParentDatabase;
    public uint NoteId { get; private set; }
    public string NoteIdHex => NoteId.ToString("X8", CultureInfo.InvariantCulture);
    public string NoteID => NoteIdHex;
    public string UniversalId
    {
        get { EnsureAlive(); return _handle == 0 ? _knownUniversalId : Session.Api.GetUnid(_handle); }
        set { EnsureOpen(); Session.Api.SetUnid(_handle, value); _knownUniversalId = value; }
    }
    public string UniversalID { get => UniversalId; set => UniversalId = value; }
    public IReadOnlyList<NotesItem> Items
    {
        get
        {
            EnsureOpen();
            return Session.Api.GetAllItemNames(_handle).Select(GetFirstItem).Where(item => item is not null).Cast<NotesItem>().ToArray();
        }
    }
    public object?[] ColumnValues { get { EnsureAlive(); return _viewRow?.GetColumnValues() ?? []; } }
    public long Size { get { EnsureOpen(); return Session.Api.GetDocumentSize(_handle); } }
    public NotesDateTime Created { get { EnsureOpen(); return NotesDateTime.FromNative(Session, Session.Api.GetDocumentCreated(_handle)); } }
    public NotesDateTime LastModified { get { EnsureOpen(); return NotesDateTime.FromNative(Session, Session.Api.GetDocumentLastModified(_handle)); } }
    public bool IsNewNote => NoteId == 0;
    public bool IsResponse { get { EnsureOpen(); return Session.Api.GetParentNoteId(_handle) != 0; } }
    public bool IsDeleted => (NoteId & 0x80000000u) != 0;
    public bool IsValid { get { EnsureAlive(); return NoteId == 0 || !IsDeleted; } }
    public bool IsProfile
    {
        get
        {
            EnsureOpen();
            return HasItem("$Name") && GetString("$Name").StartsWith("$profile_", StringComparison.OrdinalIgnoreCase);
        }
    }
    public bool IsDesign { get { EnsureOpen(); return ResolveDesignType().Length != 0; } }
    public string DesignType { get { EnsureOpen(); return ResolveDesignType(); } }
    public string DesignTitle
    {
        get
        {
            EnsureOpen();
            if (!IsDesign) return string.Empty;
            var names = GetDesignNames();
            return names.Length == 0 ? string.Empty : names[0];
        }
    }
    public string DesignAlias
    {
        get
        {
            EnsureOpen();
            if (!IsDesign) return string.Empty;
            var names = GetDesignNames();
            return names.Length <= 1 ? string.Empty : string.Join("|", names.Skip(1));
        }
    }
    public string ParentDocumentUNID
    {
        get
        {
            EnsureOpen();
            var parentId = Session.Api.GetParentNoteId(_handle);
            if (parentId == 0) return string.Empty;
            using var parent = ParentDatabase.GetDocumentByID(parentId);
            return parent?.UniversalId ?? string.Empty;
        }
    }
    public NotesDocumentCollection Responses { get { EnsureOpen(); return ParentDatabase.Own(new NotesDocumentCollection(Session, ParentDatabase, Session.Api.GetResponseIds(_handle))); } }
    internal nint Handle { get { EnsureOpen(); return _handle; } }
    internal void AttachViewRow(NotesViewRow row) { EnsureAlive(); _viewRow = row; }

    public NotesItem? GetFirstItem(string itemName)
    {
        EnsureOpen();
        if (string.IsNullOrWhiteSpace(itemName) || !Session.Api.HasItem(_handle, itemName)) return null;
        var metadata = Session.Api.GetItemMetadata(_handle, itemName);
        return ParentDatabase.Own(metadata.DataType == NotesNativeApi.NotesTypeComposite ? new NotesRichTextItem(Session, this, metadata.Name) : new NotesItem(Session, this, metadata.Name));
    }
    public NotesItem CreateNotesItem(string itemName)
    {
        EnsureOpen(); ArgumentException.ThrowIfNullOrWhiteSpace(itemName);
        Session.Api.SetItemText(_handle, itemName, string.Empty);
        return ParentDatabase.Own(new NotesItem(Session, this, itemName));
    }
    public NotesRichTextItem CreateRichTextItem(string itemName)
    {
        EnsureOpen(); ArgumentException.ThrowIfNullOrWhiteSpace(itemName);
        if (Session.Api.HasItem(_handle, itemName)) throw new InvalidOperationException($"Notes item '{itemName}' already exists.");
        Session.Api.CreateRichTextItem(_handle, itemName);
        return ParentDatabase.Own(new NotesRichTextItem(Session, this, itemName));
    }
    public NotesItem ReplaceItemValue(string itemName, object? value) { SetValue(itemName, value); return GetFirstItem(itemName) ?? ParentDatabase.Own(new NotesItem(Session, this, itemName)); }
    public bool HasItem(string itemName) { EnsureOpen(); ArgumentException.ThrowIfNullOrWhiteSpace(itemName); return Session.Api.HasItem(_handle, itemName); }
    public object? GetValue(string itemName) { var values = GetItemValue(itemName); return values.Length switch { 0 => null, 1 => values[0], _ => values }; }
    public object?[] GetItemValue(string itemName) { EnsureOpen(); ArgumentException.ThrowIfNullOrWhiteSpace(itemName); return Session.Api.GetItemValues(_handle, itemName, td => NotesDateTime.FromNative(Session, td)); }
    public string GetString(string itemName) { EnsureOpen(); ArgumentException.ThrowIfNullOrWhiteSpace(itemName); return Session.Api.GetItemText(_handle, itemName); }
    public double GetNumber(string itemName) { EnsureOpen(); ArgumentException.ThrowIfNullOrWhiteSpace(itemName); return Session.Api.GetItemNumber(_handle, itemName); }
    public NotesDateTime GetDateTime(string itemName) { EnsureOpen(); ArgumentException.ThrowIfNullOrWhiteSpace(itemName); return NotesDateTime.FromNative(Session, Session.Api.GetItemTime(_handle, itemName)); }
    public void SetValue(string itemName, object? value) { EnsureOpen(); ArgumentException.ThrowIfNullOrWhiteSpace(itemName); Session.Api.SetItemValue(_handle, itemName, value); }
    public void SetString(string itemName, string value) { EnsureOpen(); ArgumentException.ThrowIfNullOrWhiteSpace(itemName); Session.Api.SetItemText(_handle, itemName, value ?? string.Empty); }
    public void SetNumber(string itemName, double value) { EnsureOpen(); ArgumentException.ThrowIfNullOrWhiteSpace(itemName); Session.Api.SetItemNumber(_handle, itemName, value); }
    public void SetDateTime(string itemName, NotesDateTime value) { EnsureOpen(); ArgumentException.ThrowIfNullOrWhiteSpace(itemName); ArgumentNullException.ThrowIfNull(value); Session.Api.SetItemTime(_handle, itemName, value.NativeValue); }
    public void RemoveItem(string itemName) { EnsureOpen(); ArgumentException.ThrowIfNullOrWhiteSpace(itemName); Session.Api.DeleteItem(_handle, itemName); }
    public bool SaveAttachment(string attachmentName, string outputPath) { EnsureOpen(); return Session.Api.SaveAttachment(_handle, attachmentName, outputPath); }

    public bool ComputeWithForm(bool doDataTypes, bool raiseError)
    {
        EnsureOpen();
        _ = doDataTypes;
        var success = Session.Api.ComputeDocumentWithForm(_handle);
        if (!success && raiseError) throw new NotesException(5, "ComputeWithForm validation failed.");
        return success;
    }

    public bool ComputeWithForm(bool doDataTypes, bool raiseError, out string[] failedFields)
    {
        EnsureOpen();
        _ = doDataTypes;
        var result = Session.Api.ComputeDocumentWithFormAndCollectErrors(_handle);
        failedFields = result.FailedFields;
        if (!result.Success && raiseError) throw new NotesException(5, "ComputeWithForm validation failed.");
        return result.Success;
    }

    public void MarkRead(string? userName = null) { EnsureOpen(); if (!IsNewNote) Session.Api.SetDocumentUnread(ParentDatabase.Handle, NoteId, string.IsNullOrWhiteSpace(userName) ? Session.Username : userName, false); }
    public void MarkUnread(string? userName = null) { EnsureOpen(); if (!IsNewNote) Session.Api.SetDocumentUnread(ParentDatabase.Handle, NoteId, string.IsNullOrWhiteSpace(userName) ? Session.Username : userName, true); }
    public void PutInFolder(string folderName, bool createOnFail = false) { EnsureOpen(); ArgumentException.ThrowIfNullOrWhiteSpace(folderName); if (!IsNewNote) Session.Api.PutDocumentInFolder(ParentDatabase.Handle, NoteId, folderName, createOnFail); }
    public void RemoveFromFolder(string folderName) { EnsureOpen(); ArgumentException.ThrowIfNullOrWhiteSpace(folderName); if (!IsNewNote) Session.Api.RemoveDocumentFromFolder(ParentDatabase.Handle, NoteId, folderName); }
    public void MakeResponse(NotesDocument parentDocument)
    {
        EnsureOpen(); ArgumentNullException.ThrowIfNull(parentDocument);
        if (!string.Equals(ParentDatabase.ReplicaID, parentDocument.ParentDatabase.ReplicaID, StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("MakeResponse requires documents in the same database replica.", nameof(parentDocument));
        Session.Api.MakeResponse(_handle, parentDocument.Handle);
    }
    public NotesDocument CopyToDatabase(NotesDatabase destination)
    {
        EnsureOpen(); ArgumentNullException.ThrowIfNull(destination);
        if (!destination.IsOpen) throw new InvalidOperationException("CopyToDatabase requires an open destination database.");
        var copy = Session.Api.CopyDocumentToDatabase(_handle, destination.Handle);
        return destination.Own(new NotesDocument(Session, destination, copy, Session.Api.GetNoteId(copy), null));
    }
    public void CopyAllItems(NotesDocument destination, bool replace = false) { EnsureOpen(); ArgumentNullException.ThrowIfNull(destination); Session.Api.CopyAllItems(_handle, destination.Handle, replace); }
    public NotesItem CopyItem(NotesItem item, string? newName = null)
    {
        EnsureOpen(); ArgumentNullException.ThrowIfNull(item);
        var targetName = string.IsNullOrWhiteSpace(newName) ? item.Name : newName.Trim();
        Session.Api.CopyItemToDocument(item.Parent.Handle, item.Name, _handle, targetName);
        return GetFirstItem(targetName) ?? throw new InvalidOperationException("Copied NotesItem could not be reopened.");
    }
    public void Save() { EnsureOpen(); Session.Api.SaveNote(_handle); NoteId = Session.Api.GetNoteId(_handle); _knownUniversalId = Session.Api.GetUnid(_handle); }
    public bool Remove(bool force = false) { EnsureOpen(); var removed = Session.Api.DeleteNote(_handle, force); if (removed) NoteId |= 0x80000000u; return removed; }

    protected override void ReleaseNative()
    {
        var handle = _handle; _handle = 0; _viewRow = null;
        try { if (handle != 0 && Session.Api.IsInitialized) Session.Api.CloseNote(handle); }
        finally { ParentDatabase.UnregisterChild(this); }
    }

    private string ResolveDesignType()
    {
        var noteClass = Session.Api.GetNoteClass(_handle);
        var flags = HasItem("$Flags") ? GetString("$Flags") : string.Empty;
        if ((noteClass & 0x0002) != 0) return "HelpAbout";
        if ((noteClass & 0x0004) != 0)
        {
            if (flags.Contains('U')) return "Subform";
            if (flags.Contains('#')) return "Frameset";
            if (flags.Contains('W')) return "Page";
            if (flags.Contains('i')) return "ImageResource";
            if (flags.Contains('@')) return "JavaResource";
            if (flags.Contains('=')) return "StyleSheetResource";
            if (flags.Contains('K')) return "XPage";
            if (flags.Contains(';')) return "CustomControl";
            if (flags.Contains('y')) return "SharedActions";
            if (flags.Contains('g')) return "FileResource";
            return "Form";
        }
        if ((noteClass & 0x0008) != 0)
        {
            if (flags.Contains('F')) return "Folder";
            if (flags.Contains('G')) return "Navigator";
            if (flags.Contains('^')) return "SharedColumn";
            return "View";
        }
        if ((noteClass & 0x0010) != 0) return "Icon";
        if ((noteClass & 0x0020) != 0) return "DesignCollection";
        if ((noteClass & 0x0080) != 0) return "HelpIndex";
        if ((noteClass & 0x0100) != 0) return "HelpUsing";
        if ((noteClass & 0x0200) != 0)
        {
            if (flags.Contains('s')) return "ScriptLibrary";
            if (flags.Contains('h')) return "JavaScriptLibrary";
            if (flags.Contains('t')) return "DatabaseScript";
            if (flags.Contains('k')) return "DataConnection";
            if (flags.Contains('{')) return "WebService";
            if (flags.Contains('z')) return "Servlet";
            return "Agent";
        }
        if ((noteClass & 0x0400) != 0) return "SharedField";
        if ((noteClass & 0x0800) != 0) return "ReplicationFormula";
        return string.Empty;
    }

    private string[] GetDesignNames()
    {
        if (!HasItem("$TITLE")) return [];
        var names = new List<string>();
        foreach (var value in GetItemValue("$TITLE"))
        {
            var text = Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
            foreach (var part in text.Split('|'))
            {
                var name = part.Trim();
                if (name.Length > 0) names.Add(name);
            }
        }
        return names.ToArray();
    }

    private void EnsureOpen() { EnsureAlive(); if (_handle == 0) throw new InvalidOperationException("NotesDocument is not open."); }
}
