using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace DotNetToDomino;

public sealed class NotesDXLImporter : NotesObject
{
    private uint _handle;
    internal NotesDXLImporter(NotesSession session) : base(session) => _handle = Session.Api.CreateDxlImporter();
    public int ACLImportOption { get => Session.Api.GetDxlImporterWord(_handle, 1); set => Session.Api.SetDxlImporterWord(_handle, 1, value); }
    public int DesignImportOption { get => Session.Api.GetDxlImporterWord(_handle, 2); set => Session.Api.SetDxlImporterWord(_handle, 2, value); }
    public int DocumentImportOption { get => Session.Api.GetDxlImporterWord(_handle, 3); set => Session.Api.SetDxlImporterWord(_handle, 3, value); }
    public bool CreateFTIndex { get => Session.Api.GetDxlImporterBool(_handle, 4); set => Session.Api.SetDxlImporterBool(_handle, 4, value); }
    public bool CreateFullTextIndex { get => CreateFTIndex; set => CreateFTIndex = value; }
    public bool ReplaceDbProperties { get => Session.Api.GetDxlImporterBool(_handle, 5); set => Session.Api.SetDxlImporterBool(_handle, 5, value); }
    public int InputValidationOption { get => Session.Api.GetDxlImporterInt(_handle, 6); set => Session.Api.SetDxlImporterInt(_handle, 6, value); }
    public bool ReplicaRequiredForReplaceOrUpdate { get => Session.Api.GetDxlImporterBool(_handle, 7); set => Session.Api.SetDxlImporterBool(_handle, 7, value); }
    public bool ReplicaRequired { get => ReplicaRequiredForReplaceOrUpdate; set => ReplicaRequiredForReplaceOrUpdate = value; }
    public bool ExitOnFirstFatalError { get => Session.Api.GetDxlImporterBool(_handle, 8); set => Session.Api.SetDxlImporterBool(_handle, 8, value); }
    public int UnknownTokenLogOption { get => Session.Api.GetDxlImporterWord(_handle, 9); set => Session.Api.SetDxlImporterWord(_handle, 9, value); }
    public string Log { get { EnsureAlive(); return Session.Api.GetDxlImporterLog(_handle); } }
    public int ImportedNoteCount { get { EnsureAlive(); return Session.Api.GetDxlImporterNoteCount(_handle); } }
    public void Import(NotesStream stream, NotesDatabase database)
    {
        EnsureAlive(); ArgumentNullException.ThrowIfNull(stream); ArgumentNullException.ThrowIfNull(database);
        Session.Api.ImportDxl(_handle, stream.AsReadableStream(), database.Handle);
    }
    public void Import(string filePath, NotesDatabase database)
    {
        EnsureAlive(); ArgumentException.ThrowIfNullOrWhiteSpace(filePath); ArgumentNullException.ThrowIfNull(database);
        Session.Api.ImportDxlFile(_handle, filePath, database.Handle);
    }
    protected override void ReleaseNative() { var handle = _handle; _handle = 0; if (handle != 0 && Session.Api.IsInitialized) Session.Api.DeleteDxlImporter(handle); }
}

public sealed class NotesDXLExporter : NotesObject
{
    private static readonly HashSet<string> RemovedElements = new(StringComparer.OrdinalIgnoreCase)
    { "noteinfo", "updatedby", "revisions", "wassignedby", "agentrun", "agentmodified", "designchange", "databaseinfo" };
    private static readonly HashSet<string> RemovedAttributes = new(StringComparer.OrdinalIgnoreCase)
    { "replicaid", "maintenanceversion", "milestonebuild" };
    private uint _handle;
    internal NotesDXLExporter(NotesSession session) : base(session) => _handle = Session.Api.CreateDxlExporter();

    public bool CleanedDXL { get; set; }
    public bool ExportDesignToFolders { get; set; }
    public int RichTextOption { get => Session.Api.GetDxlExporterInt(_handle, 6); set => Session.Api.SetDxlExporterInt(_handle, 6, value); }
    public int ValidationStyle { get => Session.Api.GetDxlExporterInt(_handle, 8); set => Session.Api.SetDxlExporterInt(_handle, 8, value); }
    public int MIMEOption { get => Session.Api.GetDxlExporterInt(_handle, 11); set => Session.Api.SetDxlExporterInt(_handle, 11, value); }
    public bool ForceNoteFormat { get => Session.Api.GetDxlExporterBool(_handle, 30); set => Session.Api.SetDxlExporterBool(_handle, 30, value); }
    public bool ExitOnFirstFatalError { get => Session.Api.GetDxlExporterBool(_handle, 31); set => Session.Api.SetDxlExporterBool(_handle, 31, value); }
    public bool OutputDOCTYPE { get => Session.Api.GetDxlExporterBool(_handle, 34); set => Session.Api.SetDxlExporterBool(_handle, 34, value); }
    public bool ConvertNotesBitmapsToGIF { get => Session.Api.GetDxlExporterBool(_handle, 35); set => Session.Api.SetDxlExporterBool(_handle, 35, value); }
    public bool OmitRichtextAttachments { get => Session.Api.GetDxlExporterBool(_handle, 36); set => Session.Api.SetDxlExporterBool(_handle, 36, value); }
    public bool OmitRichTextAttachments { get => OmitRichtextAttachments; set => OmitRichtextAttachments = value; }
    public bool OmitOLEObjects { get => Session.Api.GetDxlExporterBool(_handle, 37); set => Session.Api.SetDxlExporterBool(_handle, 37, value); }
    public bool OmitMiscFileObjects { get => Session.Api.GetDxlExporterBool(_handle, 38); set => Session.Api.SetDxlExporterBool(_handle, 38, value); }
    public bool OmitRichtextPictures { get => Session.Api.GetDxlExporterBool(_handle, 39); set => Session.Api.SetDxlExporterBool(_handle, 39, value); }
    public bool OmitPictures { get => OmitRichtextPictures; set => OmitRichtextPictures = value; }

    public NotesStream Export(NotesDocument document) => ToStream(Prepare(Session.Api.ExportDxlDocument(_handle, document?.Handle ?? throw new ArgumentNullException(nameof(document)))));
    public NotesStream Export(NotesDocumentCollection documents) { ArgumentNullException.ThrowIfNull(documents); return ToStream(Prepare(Session.Api.ExportDxlCollection(_handle, documents.Parent.Handle, documents.NoteIds))); }
    public NotesStream Export(NotesNoteCollection notes) { ArgumentNullException.ThrowIfNull(notes); return ToStream(Prepare(Session.Api.ExportDxlCollection(_handle, notes.Parent.Handle, notes.NoteIds))); }
    public NotesStream ExportDatabaseDesign(NotesDatabase database) { ArgumentNullException.ThrowIfNull(database); return ToStream(Prepare(Session.Api.ExportDxlDesign(_handle, database.Handle))); }
    public NotesStream ExportDesignElement(NotesDatabase database, string name, string designType) { ArgumentNullException.ThrowIfNull(database); return ToStream(Prepare(Session.Api.ExportDxlDesignElement(_handle, database.Handle, name, designType))); }

    public void ExportDocument(NotesDocument document, string filePath) => Write(filePath, Session.Api.ExportDxlDocument(_handle, document?.Handle ?? throw new ArgumentNullException(nameof(document))));
    public void ExportDocumentCollection(NotesDocumentCollection documents, string filePath) { ArgumentNullException.ThrowIfNull(documents); Write(filePath, Session.Api.ExportDxlCollection(_handle, documents.Parent.Handle, documents.NoteIds)); }
    public void Export(NotesNoteCollection notes, string filePath) { ArgumentNullException.ThrowIfNull(notes); Write(filePath, Session.Api.ExportDxlCollection(_handle, notes.Parent.Handle, notes.NoteIds)); }
    public void ExportDesignElement(NotesDatabase database, string name, string designType, string filePath) { ArgumentNullException.ThrowIfNull(database); Write(filePath, Session.Api.ExportDxlDesignElement(_handle, database.Handle, name, designType)); }
    public void ExportDatabaseDesign(NotesDatabase database, string filePath)
    {
        ArgumentNullException.ThrowIfNull(database);
        var bytes = Session.Api.ExportDxlDesign(_handle, database.Handle);
        if (!ExportDesignToFolders) { Write(filePath, bytes); return; }
        WriteDesignFolders(filePath, Prepare(bytes));
    }

    protected override void ReleaseNative() { var handle = _handle; _handle = 0; if (handle != 0 && Session.Api.IsInitialized) Session.Api.DeleteDxlExporter(handle); }

    private NotesStream ToStream(byte[] bytes) { EnsureAlive(); var stream = new NotesStream(Session); stream.Replace(bytes); return stream; }
    private void Write(string filePath, byte[] bytes) { EnsureAlive(); Session.Api.WriteDxlBytes(filePath, Prepare(bytes)); }
    private byte[] Prepare(byte[] bytes) => CleanedDXL ? Clean(bytes) : bytes;

    private static byte[] Clean(byte[] bytes)
    {
        using var input = new MemoryStream(bytes);
        var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Parse, XmlResolver = null };
        using var reader = XmlReader.Create(input, settings);
        var document = XDocument.Load(reader, LoadOptions.None);
        if (document.Root is null) return bytes;
        foreach (var element in document.Root.DescendantsAndSelf().ToList())
        {
            if (RemovedElements.Contains(element.Name.LocalName) && element != document.Root) { element.Remove(); continue; }
            foreach (var attribute in element.Attributes().Where(a => RemovedAttributes.Contains(a.Name.LocalName)).ToList()) attribute.Remove();
        }
        using var output = new MemoryStream();
        using (var writer = XmlWriter.Create(output, new XmlWriterSettings { Encoding = new UTF8Encoding(false), Indent = true, OmitXmlDeclaration = false })) document.Save(writer);
        return output.ToArray();
    }

    private static void WriteDesignFolders(string rootPath, byte[] bytes)
    {
        var root = Path.GetFullPath(rootPath);
        Directory.CreateDirectory(root);
        using var input = new MemoryStream(bytes);
        var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Parse, XmlResolver = null };
        using var reader = XmlReader.Create(input, settings);
        var document = XDocument.Load(reader);
        if (document.Root is null) return;
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var index = 0;
        foreach (var element in document.Root.Elements())
        {
            index++;
            var type = SafeName(element.Name.LocalName);
            var folder = Path.Combine(root, type);
            Directory.CreateDirectory(folder);
            var name = element.Attribute("name")?.Value;
            if (string.IsNullOrWhiteSpace(name)) name = element.Descendants().FirstOrDefault(e => e.Name.LocalName.Equals("noteinfo", StringComparison.OrdinalIgnoreCase))?.Attribute("unid")?.Value;
            if (string.IsNullOrWhiteSpace(name)) name = type + "-" + index.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var baseName = SafeName(name);
            var candidate = baseName + ".dxl";
            var suffix = 1;
            while (!used.Add(Path.Combine(type, candidate))) candidate = baseName + "-" + (++suffix).ToString(System.Globalization.CultureInfo.InvariantCulture) + ".dxl";
            new XDocument(new XElement(element)).Save(Path.Combine(folder, candidate));
        }
    }

    private static string SafeName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var result = new string((value ?? string.Empty).Select(c => invalid.Contains(c) || c is '/' or '\\' ? '_' : c).ToArray()).Trim();
        return result.Length == 0 ? "unnamed" : result;
    }
}
