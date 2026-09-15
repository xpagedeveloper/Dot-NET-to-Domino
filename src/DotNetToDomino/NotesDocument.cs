using System.Collections;
using System.Globalization;
using DotNetToDomino.Interop;

namespace DotNetToDomino;

/// <summary>Represents an open Notes document.</summary>
public sealed class NotesDocument : NotesObject
{
    private nint _handle;
    private NotesViewRow? _viewRow;
    private string _knownUniversalId;
    private readonly bool _isValidAtCreation;
    private NotesMimeDirectoryOwner? _mimeDirectoryOwner;

    internal NotesDocument(NotesSession session, NotesDatabase parent, nint handle, uint? knownNoteId, string? knownUnid) : base(session)
    {
        ParentDatabase = parent; _handle = handle;
        try { NoteId = knownNoteId ?? Session.Api.GetNoteId(handle); _isValidAtCreation = NoteId == 0 || Session.Api.IsDocumentValid(parent.Handle, NoteId); _knownUniversalId = knownUnid ?? (handle == 0 ? string.Empty : Session.Api.GetUnid(handle)); }
        catch { _handle = 0; try { if (handle != 0) Session.Api.CloseNote(handle); } finally { Session.Unregister(this); } throw; }
    }

    public NotesDatabase ParentDatabase { get; } public NotesDatabase Parent => ParentDatabase; public uint NoteId { get; private set; } public string NoteIdHex => NoteId.ToString("X8", CultureInfo.InvariantCulture); public string NoteID => NoteIdHex;
    public string UniversalId { get { EnsureAlive(); return _handle == 0 ? _knownUniversalId : Session.Api.GetUnid(_handle); } set { EnsureOpen(); Session.Api.SetUnid(_handle, value); _knownUniversalId = value; } } public string UniversalID { get => UniversalId; set => UniversalId = value; }
    public IReadOnlyList<NotesItem> Items { get { EnsureOpen(); return Session.Api.GetAllItemNames(_handle).Select(GetFirstItem).Where(x => x is not null).Cast<NotesItem>().ToArray(); } }
    public IReadOnlyList<string> Authors { get { EnsureAlive(); if (_handle == 0 || !HasItem("$UpdatedBy")) return []; return GetItemValue("$UpdatedBy").Select(v => Convert.ToString(v, CultureInfo.InvariantCulture) ?? string.Empty).ToArray(); } }
    public bool SentByAgent { get { EnsureAlive(); return _handle != 0 && HasItem("$AssistMail") && GetItemValue("$AssistMail").Any(v => Convert.ToString(v, CultureInfo.InvariantCulture)?.Trim() == "1"); } }
    public object?[] ColumnValues { get { EnsureAlive(); return _viewRow?.GetColumnValues() ?? []; } } public long Size { get { EnsureOpen(); return Session.Api.GetDocumentSize(_handle); } } public NotesDateTime Created { get { EnsureOpen(); return NotesDateTime.FromNative(Session, Session.Api.GetDocumentCreated(_handle)); } } public NotesDateTime LastModified { get { EnsureOpen(); return NotesDateTime.FromNative(Session, Session.Api.GetDocumentLastModified(_handle)); } } public NotesDateTime LastAccessed { get { EnsureOpen(); return NotesDateTime.FromNative(Session, Session.Api.GetDocumentLastAccessed(_handle)); } }
    public bool IsNewNote => NoteId == 0; public bool IsResponse { get { EnsureOpen(); return Session.Api.GetParentNoteId(_handle) != 0; } } public bool IsDeleted { get { EnsureAlive(); return NoteId != 0 && Session.Api.IsDocumentDeleted(ParentDatabase.Handle, NoteId); } } public bool IsValid { get { EnsureAlive(); return _isValidAtCreation; } } public bool IsProfile { get { EnsureAlive(); return TryGetProfileIdentity(out _, out _); } } public string NameOfProfile { get { EnsureAlive(); return TryGetProfileIdentity(out var n, out _) ? n : string.Empty; } } public string Key { get { EnsureAlive(); return TryGetProfileIdentity(out _, out var k) ? k : string.Empty; } }
    public bool IsDesign { get { EnsureOpen(); return ResolveDesignType().Length != 0; } } public string DesignType { get { EnsureOpen(); return ResolveDesignType(); } } public string DesignTitle { get { EnsureOpen(); var n = IsDesign ? GetDesignNames() : []; return n.Length == 0 ? string.Empty : n[0]; } } public string DesignAlias { get { EnsureOpen(); var n = IsDesign ? GetDesignNames() : []; return n.Length <= 1 ? string.Empty : string.Join("|", n.Skip(1)); } }
    public string ParentDocumentUNID { get { EnsureOpen(); var id = Session.Api.GetParentNoteId(_handle); if (id == 0) return string.Empty; using var p = ParentDatabase.GetDocumentByID(id); return p?.UniversalId ?? string.Empty; } }
    public NotesDocumentCollection Responses { get { EnsureOpen(); return ParentDatabase.Own(new NotesDocumentCollection(Session, ParentDatabase, Session.Api.GetResponseIds(_handle))); } }
    internal nint Handle { get { EnsureOpen(); return _handle; } } internal NotesSession SessionForMime => Session; internal void AttachViewRow(NotesViewRow row) { EnsureAlive(); _viewRow = row; }
    internal NotesMimeDirectoryOwner GetMimeDirectoryOwner() { EnsureOpen(); return _mimeDirectoryOwner ??= new NotesMimeDirectoryOwner(Session.Api, _handle); }
    internal void InvalidateMimeDirectory() { var o = _mimeDirectoryOwner; _mimeDirectoryOwner = null; o?.Dispose(); }
    public void CloseMIMEEntities() { EnsureOpen(); InvalidateMimeDirectory(); }

    public NotesItem? GetFirstItem(string itemName) { EnsureOpen(); if (string.IsNullOrWhiteSpace(itemName) || !Session.Api.HasItem(_handle, itemName)) return null; var m = Session.Api.GetItemMetadata(_handle, itemName); return ParentDatabase.Own(m.DataType == NotesNativeApi.NotesTypeComposite ? new NotesRichTextItem(Session, this, m.Name) : new NotesItem(Session, this, m.Name)); }
    public NotesItem CreateNotesItem(string itemName) { EnsureOpen(); ArgumentException.ThrowIfNullOrWhiteSpace(itemName); Session.Api.SetItemText(_handle, itemName, string.Empty); return ParentDatabase.Own(new NotesItem(Session, this, itemName)); }
    public NotesRichTextItem CreateRichTextItem(string itemName) { EnsureOpen(); ArgumentException.ThrowIfNullOrWhiteSpace(itemName); if (Session.Api.HasItem(_handle, itemName)) throw new InvalidOperationException($"Notes item '{itemName}' already exists."); Session.Api.CreateRichTextItem(_handle, itemName); return ParentDatabase.Own(new NotesRichTextItem(Session, this, itemName)); }
    public NotesItem ReplaceItemValue(string itemName, object? value) { SetValue(itemName, value); return GetFirstItem(itemName) ?? ParentDatabase.Own(new NotesItem(Session, this, itemName)); }
    public bool HasItem(string itemName) { EnsureOpen(); ArgumentException.ThrowIfNullOrWhiteSpace(itemName); return Session.Api.HasItem(_handle, itemName); } public object? GetValue(string n) { var v=GetItemValue(n); return v.Length switch {0=>null,1=>v[0],_=>v}; } public object?[] GetItemValue(string n){EnsureOpen();ArgumentException.ThrowIfNullOrWhiteSpace(n);return Session.Api.GetItemValues(_handle,n,td=>NotesDateTime.FromNative(Session,td));} public string GetString(string n){EnsureOpen();return Session.Api.GetItemText(_handle,n);} public double GetNumber(string n){EnsureOpen();return Session.Api.GetItemNumber(_handle,n);} public NotesDateTime GetDateTime(string n){EnsureOpen();return NotesDateTime.FromNative(Session,Session.Api.GetItemTime(_handle,n));}
    public void SetValue(string n,object? v){EnsureOpen();Session.Api.SetItemValue(_handle,n,v);} public void SetString(string n,string v){EnsureOpen();Session.Api.SetItemText(_handle,n,v??string.Empty);} public void SetNumber(string n,double v){EnsureOpen();Session.Api.SetItemNumber(_handle,n,v);} public void SetDateTime(string n,NotesDateTime v){EnsureOpen();Session.Api.SetItemTime(_handle,n,v.NativeValue);} public void RemoveItem(string n){EnsureOpen();Session.Api.DeleteItem(_handle,n);} public bool SaveAttachment(string n,string p){EnsureOpen();return Session.Api.SaveAttachment(_handle,n,p);}
    public bool ComputeWithForm(bool d,bool r){EnsureOpen();var ok=Session.Api.ComputeDocumentWithForm(_handle);if(!ok&&r)throw new NotesException(5,"ComputeWithForm validation failed.");return ok;} public bool ComputeWithForm(bool d,bool r,out string[] f){EnsureOpen();var x=Session.Api.ComputeDocumentWithFormAndCollectErrors(_handle);f=x.FailedFields;if(!x.Success&&r)throw new NotesException(5,"ComputeWithForm validation failed.");return x.Success;}
    public void MarkRead(string? u=null){EnsureOpen();if(!IsNewNote)Session.Api.SetDocumentUnread(ParentDatabase.Handle,NoteId,string.IsNullOrWhiteSpace(u)?Session.Username:u,false);} public void MarkUnread(string? u=null){EnsureOpen();if(!IsNewNote)Session.Api.SetDocumentUnread(ParentDatabase.Handle,NoteId,string.IsNullOrWhiteSpace(u)?Session.Username:u,true);} public void PutInFolder(string f,bool c=false){EnsureOpen();if(!IsNewNote)Session.Api.PutDocumentInFolder(ParentDatabase.Handle,NoteId,f,c);} public void RemoveFromFolder(string f){EnsureOpen();if(!IsNewNote)Session.Api.RemoveDocumentFromFolder(ParentDatabase.Handle,NoteId,f);}
    public void MakeResponse(NotesDocument p){EnsureOpen();ArgumentNullException.ThrowIfNull(p);if(!string.Equals(ParentDatabase.ReplicaID,p.ParentDatabase.ReplicaID,StringComparison.OrdinalIgnoreCase))throw new ArgumentException("MakeResponse requires documents in the same database replica.",nameof(p));Session.Api.MakeResponse(_handle,p.Handle);} public NotesDocument CopyToDatabase(NotesDatabase d){EnsureOpen();var c=Session.Api.CopyDocumentToDatabase(_handle,d.Handle);return d.Own(new NotesDocument(Session,d,c,Session.Api.GetNoteId(c),null));} public void CopyAllItems(NotesDocument d,bool r=false){EnsureOpen();Session.Api.CopyAllItems(_handle,d.Handle,r);} public NotesItem CopyItem(NotesItem i,string? n=null){EnsureOpen();var t=string.IsNullOrWhiteSpace(n)?i.Name:n.Trim();Session.Api.CopyItemToDocument(i.Parent.Handle,i.Name,_handle,t);return GetFirstItem(t)!;}
    public void Send() => Send(false, null);
    public void Send(bool attachForm) => Send(attachForm, null);
    public void Send(bool attachForm, object? recipients)
    {
        EnsureOpen();
        string[]? values = recipients switch
        {
            null => null,
            string text => [text],
            IEnumerable sequence => sequence.Cast<object?>().Select(value => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty).ToArray(),
            _ => [Convert.ToString(recipients, CultureInfo.InvariantCulture) ?? string.Empty]
        };
        Session.Api.SendNote(_handle, values);
    }
    public void Save(){EnsureOpen();InvalidateMimeDirectory();Session.Api.SaveNote(_handle);NoteId=Session.Api.GetNoteId(_handle);_knownUniversalId=Session.Api.GetUnid(_handle);} public bool Remove(bool force=false){EnsureOpen();if(NoteId==0)throw new NotesException(5,"Cannot remove an unsaved NotesDocument.");var db=ParentDatabase.Handle;var id=NoteId;var api=Session.Api;Recycle();return api.DeleteNote(db,id,force);}
    protected override void ReleaseNative(){InvalidateMimeDirectory();var h=_handle;_handle=0;_viewRow=null;try{if(h!=0&&Session.Api.IsInitialized)Session.Api.CloseNote(h);}finally{ParentDatabase.UnregisterChild(this);}}

    private bool TryGetProfileIdentity(out string profileName,out string key){profileName=string.Empty;key=string.Empty;if(_handle==0||!HasItem("$Name"))return false;var value=GetString("$Name");const string prefix="$profile_";if(!value.StartsWith(prefix,StringComparison.OrdinalIgnoreCase)||value.Length<prefix.Length+3||!int.TryParse(value.AsSpan(prefix.Length,3),out var len))return false;var off=prefix.Length+3;if(value.Length<off+len)return false;profileName=value.Substring(off,len);var k=off+len;if(k<value.Length&&value[k]=='_')k++;if(k<value.Length)key=value[k..];return true;}
    private string ResolveDesignType(){var c=Session.Api.GetNoteClass(_handle);var f=HasItem("$Flags")?GetString("$Flags"):string.Empty;if((c&2)!=0)return"HelpAbout";if((c&4)!=0){if(f.Contains('U'))return"Subform";if(f.Contains('#'))return"Frameset";if(f.Contains('W'))return"Page";if(f.Contains('i'))return"ImageResource";if(f.Contains('@'))return"JavaResource";if(f.Contains('='))return"StyleSheetResource";if(f.Contains('K'))return"XPage";if(f.Contains(';'))return"CustomControl";if(f.Contains('y'))return"SharedActions";if(f.Contains('g'))return"FileResource";return"Form";}if((c&8)!=0){if(f.Contains('F'))return"Folder";if(f.Contains('G'))return"Navigator";if(f.Contains('^'))return"SharedColumn";return"View";}if((c&0x10)!=0)return"Icon";if((c&0x20)!=0)return"DesignCollection";if((c&0x80)!=0)return"HelpIndex";if((c&0x100)!=0)return"HelpUsing";if((c&0x200)!=0){if(f.Contains('s'))return"ScriptLibrary";if(f.Contains('h'))return"JavaScriptLibrary";if(f.Contains('t'))return"DatabaseScript";if(f.Contains('k'))return"DataConnection";if(f.Contains('{'))return"WebService";if(f.Contains('z'))return"Servlet";return"Agent";}if((c&0x400)!=0)return"SharedField";if((c&0x800)!=0)return"ReplicationFormula";return string.Empty;}
    private string[] GetDesignNames(){if(!HasItem("$TITLE"))return[];return GetItemValue("$TITLE").SelectMany(v=>(Convert.ToString(v,CultureInfo.InvariantCulture)??string.Empty).Split('|')).Select(x=>x.Trim()).Where(x=>x.Length>0).ToArray();}
    private void EnsureOpen(){EnsureAlive();if(_handle==0)throw new InvalidOperationException("NotesDocument is not open.");}
}
