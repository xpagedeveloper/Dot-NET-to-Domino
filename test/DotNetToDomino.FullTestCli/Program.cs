using System.Reflection;
using DotNetToDomino;

var failures = new List<string>();
void Check(string name, Action action)
{
    try { action(); Console.WriteLine($"PASS {name}"); }
    catch (Exception ex) { failures.Add($"{name}: {ex.GetType().FullName}: {ex.Message}"); Console.Error.WriteLine($"FAIL {failures[^1]}"); }
}

Check("NuGet assembly loaded", () =>
{
    if (typeof(NotesSession).Assembly.GetName().Name != "DotNetToDomino") throw new InvalidOperationException("Wrong assembly loaded.");
});
Check("public API surface", () =>
{
    var types = typeof(NotesSession).Assembly.GetExportedTypes().Where(t => t.Namespace == "DotNetToDomino").ToArray();
    if (types.Length == 0) throw new InvalidOperationException("No exported types found.");
    foreach (var type in types)
    {
        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
            if (property.GetMethod is null && property.SetMethod is null) throw new InvalidOperationException($"{type.Name}.{property.Name} has no accessor.");
        foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static).Where(m => !m.IsSpecialName))
            if (method.ContainsGenericParameters) throw new InvalidOperationException($"{type.Name}.{method.Name} has open generic parameters.");
    }
    Console.WriteLine($"  Audited {types.Length} exported types.");
});

var runtime = Environment.GetEnvironmentVariable("NOTES_RUNTIME");
if (string.IsNullOrWhiteSpace(runtime)) failures.Add("NOTES_RUNTIME is not set; native full tests cannot run.");
else
{
    var server = Environment.GetEnvironmentVariable("NOTES_TEST_SERVER") ?? string.Empty;
    var databasePath = Environment.GetEnvironmentVariable("NOTES_TEST_DATABASE");
    using var session = new NotesSession(runtime);
    Check("session properties", () => { _ = session.UserName; _ = session.CommonUserName; _ = session.Platform; _ = session.NotesVersion; _ = session.NotesBuildVersion; _ = session.DataDir; });
    Check("session factories", () => { using var name = session.CreateName(session.UserName); using var date = session.CreateDateTimeNow(); date.SetNow(); date.ConvertToZone(date.TimeZone); if (date.TimeDifferenceDouble(date) != 0) throw new InvalidOperationException("Date/time self difference was not zero."); using var stream = session.CreateStream(); using var importer = session.CreateDXLImporter(); using var exporter = session.CreateDXLExporter(); using var style = session.CreateRichTextStyle(); using var paragraph = session.CreateRichTextParagraphStyle(); using var mail = session.CreateMail(); try { mail.SendTo = null!; throw new InvalidOperationException("Null SendTo was accepted."); } catch (ArgumentNullException ex) when (ex.ParamName == "SendTo") { } try { mail.DeliveryPriority = "X"; throw new InvalidOperationException("Invalid DeliveryPriority was accepted."); } catch (ArgumentOutOfRangeException ex) when (ex.ParamName == "value") { } });
    Check("password round trip", () => { const string password = "DotNetToDomino-FullTest"; var digest = session.HashPassword(password); if (!session.VerifyPassword(password, digest)) throw new InvalidOperationException("Password verification failed."); });
    Check("address book and directory", () => { foreach (var book in session.AddressBooks) using (book) { _ = book.Server; _ = book.FilePath; } using var directory = session.GetDbDirectory(server); _ = directory.Name; using var database = directory.GetFirstDatabase(NotesConst.DATABASE); if (database is not null) { _ = database.FilePath; _ = database.FileName; } using var next = directory.GetNextDatabase(); if (next is not null) { _ = next.FilePath; _ = next.FileName; } });
    if (!string.IsNullOrWhiteSpace(databasePath)) Check("item surface", () =>
    {
        using var database = session.GetDatabase(server, databasePath, false) ?? throw new InvalidOperationException("Database not found.");
        using var document = database.CreateDocument();
        document.ReplaceItemValue("FullTestItem", "value").Recycle();
        using var item = document.GetFirstItem("FullTestItem") ?? throw new InvalidOperationException("Item was not created.");
        if (item.Text != "value" || item.Values.Length != 1) throw new InvalidOperationException("Item value round trip failed.");
        item.Remove();
        try { _ = item.Text; throw new InvalidOperationException("Removed item remained usable."); }
        catch (ObjectDisposedException) { }
    });
    if (!string.IsNullOrWhiteSpace(databasePath)) Check("view navigation", () =>
    {
        using var database = session.GetDatabase(server, databasePath, false) ?? throw new InvalidOperationException("Database not found.");
        foreach (var view in database.Views)
        {
            using (view)
            {
                using var first = view.GetFirstDocument();
                using var last = view.GetLastDocument();
                if (last is not null)
                {
                    using var previous = view.GetPrevDocument(last);
                }
            }
        }
    });
    Check("argument validation", () =>
    {
        using var directory = session.GetDbDirectory(server);
        try { _ = directory.GetFirstDatabase(11); throw new InvalidOperationException("Invalid database type was accepted."); }
        catch (ArgumentOutOfRangeException ex) when (ex.ParamName == "type") { }
        try { _ = directory.OpenDatabase(null!); throw new InvalidOperationException("Null database path was accepted."); }
        catch (ArgumentException ex) when (ex.ParamName == "filePath") { }
    });
    if (!string.IsNullOrWhiteSpace(databasePath)) Check("database and document surface", () => { using var database = session.GetDatabase(server, databasePath, false) ?? throw new InvalidOperationException("Database not found."); if (!database.IsOpen) throw new InvalidOperationException(database.OpenError); _ = database.Title; _ = database.Categories; _ = database.TemplateName; _ = database.DesignTemplateName; _ = database.ReplicaID; _ = database.Size; _ = database.PercentUsed; _ = database.CurrentAccessLevel; _ = database.Created; _ = database.LastModified; _ = database.FileFormat; using var access = database.QueryAccess(); _ = access.Level; _ = access.LevelName; _ = access.AccessFlags; _ = access.NamesList; _ = access.Groups; _ = access.Roles; using var document = database.CreateDocument(); document.ReplaceItemValue("Form", "FullTest").Recycle(); document.ReplaceItemValue("Subject", "Full test").Recycle(); if (!document.HasItem("Subject") || document.GetString("Subject") != "Full test") throw new InvalidOperationException("Document item round trip failed."); using var mime = document.CreateMIMEEntity(); _ = mime.GetChildren(); _ = mime.GetHeaders(); using var content = session.CreateStream(); content.WriteText("MIME full test"); mime.SetContentFromText(content, "text/plain; charset=utf-8", 1727); try { mime.EncodeContent(11); throw new InvalidOperationException("Invalid MIME encoding was accepted."); } catch (ArgumentOutOfRangeException ex) when (ex.ParamName == "encoding") { } });
}

if (failures.Count != 0) { Console.Error.WriteLine($"FULL TEST FAILED ({failures.Count} error(s))"); Environment.ExitCode = 1; } else Console.WriteLine("FULL TEST PASSED");
