using System.Globalization;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace DotNetToDomino;

public sealed class NotesDXLImporter : NotesObject
{
    private uint _handle;
    internal NotesDXLImporter(NotesSession session) : base(session) => _handle = Session.Api.CreateDxlImporter();

    public int ACLImportOption { get => Session.Api.GetDxlImporterInt(_handle, 1); set => Session.Api.SetDxlImporterInt(_handle, 1, ValidateOption(value, nameof(ACLImportOption), 1, 5, 9, 10)); }
    public int DesignImportOption { get => Session.Api.GetDxlImporterInt(_handle, 2); set => Session.Api.SetDxlImporterInt(_handle, 2, ValidateOption(value, nameof(DesignImportOption), 1, 2, 5, 6)); }
    public int DocumentImportOption { get => Session.Api.GetDxlImporterInt(_handle, 3); set => Session.Api.SetDxlImporterInt(_handle, 3, ValidateOption(value, nameof(DocumentImportOption), 1, 2, 5, 6, 9, 10)); }
    public bool CreateFTIndex { get => Session.Api.GetDxlImporterBool(_handle, 4); set => Session.Api.SetDxlImporterBool(_handle, 4, value); }
    public bool CreateFullTextIndex { get => CreateFTIndex; set => CreateFTIndex = value; }
    public bool ReplaceDbProperties { get => Session.Api.GetDxlImporterBool(_handle, 5); set => Session.Api.SetDxlImporterBool(_handle, 5, value); }
    public int InputValidationOption { get => Session.Api.GetDxlImporterInt(_handle, 6); set => Session.Api.SetDxlImporterInt(_handle, 6, ValidateOption(value, nameof(InputValidationOption), 0, 1, 2)); }
    public bool ReplicaRequiredForReplaceOrUpdate { get => Session.Api.GetDxlImporterBool(_handle, 7); set => Session.Api.SetDxlImporterBool(_handle, 7, value); }
    public bool ReplicaRequired { get => ReplicaRequiredForReplaceOrUpdate; set => ReplicaRequiredForReplaceOrUpdate = value; }
    public bool ExitOnFirstFatalError { get => Session.Api.GetDxlImporterBool(_handle, 8); set => Session.Api.SetDxlImporterBool(_handle, 8, value); }
    public int UnknownTokenLogOption { get => Session.Api.GetDxlImporterInt(_handle, 9); set => Session.Api.SetDxlImporterInt(_handle, 9, ValidateOption(value, nameof(UnknownTokenLogOption), 1, 2, 3, 4)); }
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

    private static int ValidateOption(int value, string name, params int[] allowed)
    {
        if (allowed.Contains(value)) return value;
        throw new ArgumentOutOfRangeException(name, value, $"{name} must be one of: {string.Join(", ", allowed)}.");
    }
}

public sealed class NotesDXLExporter : NotesObject
{
    private static readonly HashSet<string> RemovedElements = new(StringComparer.OrdinalIgnoreCase)
    { "noteinfo", "updatedby", "revisions", "wassignedby", "agentrun", "agentmodified", "designchange", "databaseinfo" };
    private static readonly HashSet<string> RemovedAttributes = new(StringComparer.OrdinalIgnoreCase)
    { "replicaid", "maintenanceversion", "milestonebuild" };
    private static readonly IReadOnlyDictionary<string, string> DesignFolders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["agent"] = "agents", ["form"] = "forms", ["view"] = "views", ["subform"] = "subforms",
        ["folder"] = "folders", ["scriptlibrary"] = "scriptlibraries", ["sharedfield"] = "sharedfields",
        ["sharedactions"] = "sharedactions", ["page"] = "pages", ["frameset"] = "framesets",
        ["outline"] = "outlines", ["navigator"] = "navigators", ["imageresource"] = "images",
        ["image"] = "images", ["fileresource"] = "resources", ["stylesheetresource"] = "resources",
        ["javaresource"] = "resources", ["xpage"] = "xpages", ["customcontrol"] = "customcontrols"
    };

    private uint _handle;
    internal NotesDXLExporter(NotesSession session) : base(session) => _handle = Session.Api.CreateDxlExporter();

    public bool CleanedDXL { get; set; }
    public bool ExportDesignToFolders { get; set; }
    public string Log { get { EnsureAlive(); return Session.Api.GetDxlExporterLog(_handle); } }
    public int RichTextOption { get => Session.Api.GetDxlExporterInt(_handle, 6); set => Session.Api.SetDxlExporterInt(_handle, 6, ValidateOption(value, nameof(RichTextOption), 0, 1)); }
    public int ValidationStyle { get => Session.Api.GetDxlExporterInt(_handle, 8); set => Session.Api.SetDxlExporterInt(_handle, 8, ValidateOption(value, nameof(ValidationStyle), 0, 1, 2)); }
    public int MIMEOption { get => Session.Api.GetDxlExporterInt(_handle, 11); set => Session.Api.SetDxlExporterInt(_handle, 11, ValidateOption(value, nameof(MIMEOption), 0, 1)); }
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
        WriteDesignFolders(filePath, bytes);
    }

    protected override void ReleaseNative() { var handle = _handle; _handle = 0; if (handle != 0 && Session.Api.IsInitialized) Session.Api.DeleteDxlExporter(handle); }

    private NotesStream ToStream(byte[] bytes) { EnsureAlive(); var stream = new NotesStream(Session); stream.Replace(bytes); return stream; }
    private void Write(string filePath, byte[] bytes) { EnsureAlive(); Session.Api.WriteDxlBytes(filePath, Prepare(bytes)); }
    private byte[] Prepare(byte[] bytes) => CleanedDXL ? Clean(bytes) : bytes;

    private static int ValidateOption(int value, string name, params int[] allowed)
    {
        if (allowed.Contains(value)) return value;
        throw new ArgumentOutOfRangeException(name, value, $"{name} must be one of: {string.Join(", ", allowed)}.");
    }

    private static byte[] Clean(byte[] bytes)
    {
        var document = LoadXml(bytes);
        if (document.Root is null) return bytes;
        CleanDocument(document);
        return Serialize(document);
    }

    private static void CleanDocument(XDocument document)
    {
        if (document.Root is null) return;
        foreach (var element in document.Root.DescendantsAndSelf().ToList())
        {
            if (element != document.Root && RemovedElements.Contains(element.Name.LocalName)) { element.Remove(); continue; }
            if (element.Name.LocalName.Equals("item", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(element.Attribute("name")?.Value, "$ClassData", StringComparison.OrdinalIgnoreCase))
            {
                element.Remove();
                continue;
            }
            foreach (var attribute in element.Attributes().Where(a => RemovedAttributes.Contains(a.Name.LocalName)).ToList()) attribute.Remove();
            SortAttributes(element);
        }
    }

    private void WriteDesignFolders(string rootPath, byte[] rawBytes)
    {
        var root = Path.GetFullPath(rootPath);
        Directory.CreateDirectory(root);
        var document = LoadXml(rawBytes);
        if (document.Root is null) return;
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var index = 0;
        foreach (var original in document.Root.Elements())
        {
            index++;
            var folderName = ResolveDesignFolder(original.Name.LocalName);
            var folder = Path.Combine(root, folderName);
            Directory.CreateDirectory(folder);
            var name = ResolveDesignName(original, index);
            var baseName = SafeName(name);
            var candidate = baseName + ".dxl";
            var suffix = 1;
            while (!used.Add(Path.Combine(folderName, candidate))) candidate = baseName + "-" + (++suffix).ToString(CultureInfo.InvariantCulture) + ".dxl";
            var filePath = Path.Combine(folder, candidate);
            var times = ReadDesignTimes(original);
            var single = new XDocument(new XDeclaration("1.0", "utf-8", null), new XElement(original));
            if (CleanedDXL) CleanDocument(single);
            File.WriteAllBytes(filePath, Serialize(single));
            ApplyFileTimes(filePath, times.Created, times.Modified ?? times.Revised);
        }
    }

    private static string ResolveDesignFolder(string localName)
    {
        if (DesignFolders.TryGetValue(localName, out var known)) return known;
        var safe = SafeName(localName).ToLowerInvariant();
        if (safe.Length == 0) return "design";
        return safe.EndsWith('s') ? safe : safe + "s";
    }

    private static string ResolveDesignName(XElement element, int index)
    {
        var name = element.Attribute("name")?.Value;
        if (string.IsNullOrWhiteSpace(name)) name = element.Attribute("title")?.Value;
        if (string.IsNullOrWhiteSpace(name)) name = FindNamedItemText(element, "$TITLE");
        if (string.IsNullOrWhiteSpace(name)) name = FindNamedItemText(element, "$$ScriptName");
        if (!string.IsNullOrWhiteSpace(name))
        {
            var pipe = name.IndexOf('|');
            if (pipe >= 0) name = name[..pipe];
        }
        return string.IsNullOrWhiteSpace(name) ? element.Name.LocalName + "-" + index.ToString(CultureInfo.InvariantCulture) : name.Trim();
    }

    private static string? FindNamedItemText(XElement element, string itemName)
    {
        var item = element.Descendants().FirstOrDefault(e => e.Name.LocalName.Equals("item", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(e.Attribute("name")?.Value, itemName, StringComparison.OrdinalIgnoreCase));
        if (item is null) return null;
        var text = item.Descendants().FirstOrDefault(e => e.Name.LocalName.Equals("text", StringComparison.OrdinalIgnoreCase) || e.Name.LocalName.Equals("textlist", StringComparison.OrdinalIgnoreCase));
        return text?.Value?.Trim();
    }

    private static (DateTime? Created, DateTime? Modified, DateTime? Revised) ReadDesignTimes(XElement element)
    {
        var noteInfo = element.DescendantsAndSelf().FirstOrDefault(e => e.Name.LocalName.Equals("noteinfo", StringComparison.OrdinalIgnoreCase));
        return noteInfo is null
            ? (null, null, null)
            : (ParseDxlTime(noteInfo.Attribute("created")?.Value), ParseDxlTime(noteInfo.Attribute("modified")?.Value), ParseDxlTime(noteInfo.Attribute("revised")?.Value));
    }

    private static DateTime? ParseDxlTime(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var text = value.Trim();
        var comma = text.IndexOf(',');
        if (comma >= 0) text = text[..comma] + "." + text[(comma + 1)..];
        if (DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var offset)) return offset.UtcDateTime;
        var core = value.Trim();
        if (core.Length >= 15 && DateTime.TryParseExact(core[..15], "yyyyMMdd'T'HHmmss", CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var local)) return local.ToUniversalTime();
        return null;
    }

    private static void ApplyFileTimes(string filePath, DateTime? createdUtc, DateTime? modifiedUtc)
    {
        if (createdUtc.HasValue)
        {
            try { File.SetCreationTimeUtc(filePath, DateTime.SpecifyKind(createdUtc.Value, DateTimeKind.Utc)); } catch { }
        }
        if (modifiedUtc.HasValue)
        {
            try { File.SetLastWriteTimeUtc(filePath, DateTime.SpecifyKind(modifiedUtc.Value, DateTimeKind.Utc)); } catch { }
        }
    }

    private static XDocument LoadXml(byte[] bytes)
    {
        using var input = new MemoryStream(bytes);
        var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Parse, XmlResolver = null };
        using var reader = XmlReader.Create(input, settings);
        return XDocument.Load(reader, LoadOptions.None);
    }

    private static byte[] Serialize(XDocument document)
    {
        using var output = new MemoryStream();
        var settings = new XmlWriterSettings
        {
            Encoding = new UTF8Encoding(false),
            Indent = true,
            OmitXmlDeclaration = false,
            NewLineChars = "\n",
            NewLineHandling = NewLineHandling.Replace
        };
        using (var writer = XmlWriter.Create(output, settings)) document.Save(writer);
        return output.ToArray();
    }

    private static void SortAttributes(XElement element)
    {
        var attributes = element.Attributes()
            .Select(a => new XAttribute(a))
            .OrderBy(a => a.IsNamespaceDeclaration ? 0 : 1)
            .ThenBy(a => a.Name.NamespaceName, StringComparer.Ordinal)
            .ThenBy(a => a.Name.LocalName, StringComparer.Ordinal)
            .ToArray();
        element.RemoveAttributes();
        foreach (var attribute in attributes) element.Add(attribute);
    }

    private static string SafeName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var result = new string((value ?? string.Empty).Select(c => invalid.Contains(c) || c is '/' or '\\' ? '_' : c).ToArray()).Trim();
        return result.Length == 0 ? "unnamed" : result;
    }
}
