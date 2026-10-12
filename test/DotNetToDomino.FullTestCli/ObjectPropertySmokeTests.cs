using System.Collections;
using System.Reflection;
using DotNetToDomino;

internal static class ObjectPropertySmokeTests
{
    // Traverse real instances rather than merely auditing their reflected API.
    // Reference identity prevents Parent/session links from causing cycles.
    internal static void Run(NotesSession session, string server, string? databasePath, Action<string, Action> check)
    {
        var filter = Environment.GetEnvironmentVariable("NOTES_TEST_FILTER");
        if (!string.IsNullOrWhiteSpace(filter) && !filter.Contains("object property", StringComparison.OrdinalIgnoreCase)) return;
        var visited = new HashSet<object>(ReferenceEqualityComparer.Instance);
        var readProperties = new HashSet<string>();
        var documentIds = new HashSet<(string, uint)>();
        var collectionContents = new HashSet<string>();
        void Read(object? instance)
        {
            if (instance is NotesDatabase { IsOpen: false }) return;
            if (instance is null || instance is string || instance.GetType().IsValueType || !visited.Add(instance)) return;
            if (instance is NotesDocument { NoteId: not 0 } note && !documentIds.Add((note.ParentDatabase.FilePath, note.NoteId))) return;
            if (instance is NotesDocumentCollection collection)
            {
                using var until = collection.UntilTime;
                var key = collection.Parent.FilePath + "|" + (until is not null) + "|" + string.Join(",", collection);
                if (!collectionContents.Add(key)) return;
            }
            if (instance is IEnumerable sequence && (instance is Array || instance.GetType().Namespace != typeof(NotesSession).Namespace))
            {
                foreach (var value in sequence) Read(value);
                return;
            }
            if (instance.GetType().Namespace != typeof(NotesSession).Namespace) return;
            foreach (var property in instance.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
                         .Where(p => p.CanRead && p.GetIndexParameters().Length == 0).OrderBy(p => p.Name))
            {
                object? value = null;
                readProperties.Add(instance.GetType().Name + "." + property.Name);
                check($"object property {instance.GetType().Name}.{property.Name}", () =>
                {
                    try { value = property.GetValue(instance); }
                    catch (TargetInvocationException ex) when (instance is NotesDocumentCollection &&
                        property.Name is "Documents" or "DesignElements" && ex.InnerException is InvalidOperationException &&
                        ex.InnerException.Message.Contains("GetModifiedDocuments", StringComparison.Ordinal))
                    { Console.WriteLine($"EXPECTED GUARD {property.Name}: requires a modified-documents collection"); }
                    catch (TargetInvocationException ex) when (ex.InnerException is not null)
                    { System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex.InnerException).Throw(); }
                });
                Read(value);
            }
            if (instance is IEnumerable notesSequence) foreach (var value in notesSequence) Read(value);
        }

        Read(session);
        Read(session.CreateName("CN=Full Test/OU=Development/O=DotNetToDomino/C=SE"));
        Read(session.CreateName("Full Test <fulltest@example.invalid> (Test)"));
        Read(session.CreateDateTimeNow());
        Read(session.CreateStream());
        Read(session.CreateDXLImporter());
        Read(session.CreateDXLExporter());
        Read(session.CreateRichTextStyle());
        var paragraph = session.CreateRichTextParagraphStyle();
        paragraph.SetTab(720, NotesRichTextParagraphStyle.TabLeft);
        Read(paragraph);
        Read(session.CreateMail());
        Read(session.GetDbDirectory(server));

        if (string.IsNullOrWhiteSpace(databasePath)) return;
        using var database = session.GetDatabase(server, databasePath, false)
            ?? throw new InvalidOperationException("Test database not found.");
        Read(database);
        Read(database.GetModifiedDocuments(null, NotesConst.DBMOD_DOC_ALL));
        using var noteCollection = database.CreateNoteCollection(true);
        noteCollection.BuildCollection();
        Read(noteCollection);
        check("object property agent result", () => Read(database.RunAgent("DotNetToDominoTestAgent")));
        using var documents = database.AllDocuments;
        foreach (var fixtureNoteId in documents)
        {
            using (var fixtureDocument = database.GetDocumentByID(fixtureNoteId))
            using (var body = fixtureDocument?.GetFirstItem("Body")?.GetRichTextItem())
            {
                if (body is null) continue;
                using var elements = body.CreateNavigator();
                foreach (var type in new[] { NotesRichTextNavigator.ElementTable, NotesRichTextNavigator.ElementSection, NotesRichTextNavigator.ElementDocLink })
                    if (elements.FindFirstElement(type)) Read(elements.GetElement());
            }
        }
        check("object property database access", () => Read(database.QueryAccess()));
        using var document = database.CreateDocument();
        document.SetString("Subject", "Full test property traversal åäö");
        Read(document);
        Read(document.GetFirstItem("Subject"));
        using var richText = document.CreateRichTextItem("Body");
        richText.AppendText("Property smoke test");
        var attachmentPath = Path.Combine(Path.GetTempPath(), "DotNetToDominoProperty-" + Guid.NewGuid().ToString("N") + ".bin");
        try
        {
            File.WriteAllBytes(attachmentPath, [0, 1, 2, 255]);
            using var attachment = richText.AttachFile(attachmentPath);
            Read(attachment);
            if (!attachment.ToByteArray().SequenceEqual(new byte[] { 0, 1, 2, 255 })) throw new InvalidOperationException("Attachment round trip failed.");
        }
        finally { File.Delete(attachmentPath); }
        Read(richText);
        Read(richText.CreateNavigator());
        Read(richText.CreateRange());
        using var mimeDocument = database.CreateDocument();
        using var mime = mimeDocument.CreateMIMEEntity();
        using var mimeContent = session.CreateStream();
        mimeContent.Charset = "UTF-8";
        mimeContent.WriteText("--property-boundary\r\nContent-Type: text/plain\r\n\r\nfirst\r\n--property-boundary--\r\n");
        mimeContent.Position = 0;
        mime.SetContentFromBytes(mimeContent, "multipart/mixed; boundary=property-boundary", 1725);
        Read(mime);
        Read(mime.GetHeaders());
        foreach (var view in database.Views)
        {
            Read(view);
            Read(view.CreateViewNav());
            Read(view.CreateViewEntryCollection());
        }
        // Report uninstantiated object types honestly, never as passed coverage.
        var testedTypes = visited.Select(value => value.GetType()).ToHashSet();
        foreach (var type in typeof(NotesSession).Assembly.GetExportedTypes()
                     .Where(type => typeof(NotesObject).IsAssignableFrom(type) && !type.IsAbstract && !testedTypes.Contains(type)))
            Console.WriteLine($"NOT EXERCISED object properties: {type.Name} (no instance in fixture)");
        Console.WriteLine($"Property coverage: {testedTypes.Count(type => typeof(NotesObject).IsAssignableFrom(type))} object types, {readProperties.Count} distinct properties.");
    }

    internal static void RoundTrips(NotesSession session, string server, string? databasePath, Action<string, Action> check)
    {
        var filter = Environment.GetEnvironmentVariable("NOTES_TEST_FILTER");
        if (!string.IsNullOrWhiteSpace(filter) && !filter.Contains("property round trip", StringComparison.OrdinalIgnoreCase)) return;
        void Set(object instance, string name, params object[] values)
        {
            var property = instance.GetType().GetProperty(name)!;
            check($"property round trip {instance.GetType().Name}.{name}", () =>
            {
                var original = property.GetValue(instance);
                try
                {
                    foreach (var value in values)
                    {
                        property.SetValue(instance, value);
                        var actual = property.GetValue(instance);
                        if (!Equals(value, actual)) throw new InvalidOperationException($"Expected '{value}', got '{actual}'.");
                    }
                }
                finally { property.SetValue(instance, original); }
            });
        }
        using var style = session.CreateRichTextStyle();
        foreach (var name in new[] { "Bold", "Italic", "PassThruHTML", "Strikethrough", "Underline" }) Set(style, name, -1, 0, 255);
        Set(style, "Effects", 0, 5, 255); Set(style, "FontSize", 1, 250, 255);
        Set(style, "NotesColor", 0, 255); Set(style, "NotesFont", 0, 255);
        using var paragraph = session.CreateRichTextParagraphStyle();
        Set(paragraph, "Alignment", 0, 4); Set(paragraph, "InterLineSpacing", 0, 4);
        Set(paragraph, "Pagination", 0, 1, 2);
        foreach (var name in new[] { "FirstLineLeftMargin", "LeftMargin", "RightMargin", "SpacingAbove", "SpacingBelow" }) Set(paragraph, name, 0, 720);
        using var importer = session.CreateDXLImporter();
        Set(importer, "ACLImportOption", 1, 5, 9, 10);
        Set(importer, "DesignImportOption", 1, 2, 5, 6);
        Set(importer, "DocumentImportOption", 1, 2, 5, 6, 9, 10);
        Set(importer, "InputValidationOption", 0, 1, 2); Set(importer, "UnknownTokenLogOption", 1, 2, 3, 4);
        foreach (var name in new[] { "CreateFTIndex", "ReplaceDbProperties", "ReplicaRequiredForReplaceOrUpdate", "ExitOnFirstFatalError" }) Set(importer, name, false, true);
        using var exporter = session.CreateDXLExporter();
        foreach (var property in typeof(NotesDXLExporter).GetProperties().Where(p => p.CanWrite && p.PropertyType == typeof(bool))) Set(exporter, property.Name, false, true);
        Set(exporter, "RichTextOption", 0, 1); Set(exporter, "ValidationStyle", 0, 1, 2); Set(exporter, "MIMEOption", 0, 1);
        using var mail = session.CreateMail();
        foreach (var name in new[] { "From", "ReplyTo", "Principal", "OnBehalfOf", "Subject", "Body" }) Set(mail, name, "FullTest", "");
        Set(mail, "DeliveryPriority", "H", "N", "L"); Set(mail, "DeliveryReport", "N", "B", "C"); Set(mail, "ReturnReceipt", false, true);
        check("property round trip NotesMail recipients", () =>
        {
            mail.SendTo = new[] { "recipient@example.invalid" };
            mail.CopyTo = new[] { "copy@example.invalid" };
            mail.BlindCopyTo = new[] { "blind@example.invalid" };
            if (mail.SendTo.Single() != "recipient@example.invalid" || mail.CopyTo.Single() != "copy@example.invalid" || mail.BlindCopyTo.Single() != "blind@example.invalid")
                throw new InvalidOperationException("Recipient properties did not round-trip.");
        });
        using var stream = session.CreateStream();
        Set(stream, "Charset", "UTF-8", "Unicode");
        stream.Write(new byte[] { 0, 1, 2, 255 });
        Set(stream, "Position", 0, 1, 4);
        Set(session, "ConvertMIME", false, true);
        if (string.IsNullOrWhiteSpace(databasePath)) return;
        using var database = session.GetDatabase(server, databasePath, false) ?? throw new InvalidOperationException("Test database not found.");
        foreach (var name in new[] { "Title", "Categories", "TemplateName", "DesignTemplateName" }) Set(database, name, "FullTest", "");
        using var document = database.CreateDocument();
        using var item = document.ReplaceItemValue("RoundTripItem", "value");
        foreach (var property in typeof(NotesItem).GetProperties().Where(p => p.CanWrite && p.PropertyType == typeof(bool))) Set(item, property.Name, false, true);
        check("property round trip NotesItem flag value preservation", () =>
        {
            if (item.Text != "value" || document.Items.Count(i => i.Name == "RoundTripItem") != 1)
                throw new InvalidOperationException("Changing item flags lost the value or duplicated the item.");
        });
        check("property round trip NotesItem.Values", () =>
        {
            item.Values = new object?[] { "first", "second" };
            if (!item.Values.Select(Convert.ToString).SequenceEqual(new[] { "first", "second" })) throw new InvalidOperationException("Item values did not round-trip.");
        });
    }
}
