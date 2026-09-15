using DotNetToDomino;

var runtime = Environment.GetEnvironmentVariable("NOTES_RUNTIME");
var databasePath = Environment.GetEnvironmentVariable("NOTES_TEST_DATABASE");
var server = Environment.GetEnvironmentVariable("NOTES_TEST_SERVER") ?? string.Empty;
var replicaId = Environment.GetEnvironmentVariable("NOTES_TEST_REPLICA_ID");
var password = Environment.GetEnvironmentVariable("NOTES_TEST_PASSWORD") ?? "DotNetToDomino-Parity-Test";

if (string.IsNullOrWhiteSpace(runtime))
{
    Console.WriteLine("SKIP: set NOTES_RUNTIME to an HCL Notes/Domino runtime directory to run native integration tests.");
    return 0;
}

var failed = 0;
void Check(string name, Action test)
{
    try { test(); Console.WriteLine($"PASS {name}"); }
    catch (Exception ex) { failed++; Console.WriteLine($"FAIL {name}: {ex.GetType().Name}: {ex.Message}"); }
}

using var session = new NotesSession(runtime);
Check("session metadata", () =>
{
    _ = session.UserName;
    _ = session.CommonUserName;
    _ = session.Platform;
    _ = session.NotesVersion;
    _ = session.NotesBuildVersion;
    _ = session.DataDir;
});
Check("password hash/verify", () =>
{
    var digest = session.HashPassword(password);
    if (string.IsNullOrWhiteSpace(digest) || !session.VerifyPassword(password, digest)) throw new Exception("password verification failed");
});
Check("environment read/write", () =>
{
    const string name = "DotNetToDominoIntegrationTest";
    session.SetEnvironmentVar(name, 42);
    if (session.GetEnvironmentValue(name) != 42) throw new Exception("integer environment round-trip failed");
    session.SetEnvironmentVar(name, "ok");
    if (session.GetEnvironmentString(name) != "ok") throw new Exception("string environment round-trip failed");
});
Check("factory objects", () =>
{
    session.CreateName(session.UserName).Recycle();
    session.CreateDateTimeNow().Recycle();
    session.CreateStream().Recycle();
    session.CreateDXLImporter().Recycle();
    session.CreateDXLExporter().Recycle();
    session.CreateRichTextStyle().Recycle();
    session.CreateRichTextParagraphStyle().Recycle();
});
Check("address books", () =>
{
    foreach (var book in session.AddressBooks)
    {
        _ = book.Server;
        _ = book.FilePath;
        book.Recycle();
    }
});
Check("database directory", () =>
{
    using var directory = session.GetDbDirectory(server);
    var first = directory.GetFirstDatabase(NotesConst.DATABASE);
    first?.Recycle();
});

if (!string.IsNullOrWhiteSpace(databasePath))
{
    NotesDatabase? db = null;
    Check("GetDatabase", () =>
    {
        db = session.GetDatabase(server, databasePath, false) ?? throw new Exception("database not found");
        if (!db.IsOpen) throw new Exception(db.OpenError);
    });
    if (db is not null && db.IsOpen)
    {
        Check("database properties", () =>
        {
            _ = db.Title; _ = db.Categories; _ = db.TemplateName; _ = db.DesignTemplateName;
            _ = db.ReplicaID; _ = db.Size; _ = db.PercentUsed; _ = db.CurrentAccessLevel;
            _ = db.Created; _ = db.LastModified; _ = db.FileFormat;
        });
        Check("database access", () =>
        {
            using var access = db.QueryAccess();
            _ = access.Level; _ = access.LevelName; _ = access.AccessFlags; _ = access.NamesList; _ = access.Groups; _ = access.Roles;
        });
        Check("database collections/design", () =>
        {
            using var all = db.AllDocuments;
            _ = all.Count;
            _ = db.Views.Count;
            _ = db.Forms.Count;
            _ = db.Agents.Count;
            using var notes = db.CreateNoteCollection(false);
        });
        Check("document create/items", () =>
        {
            using var doc = db.CreateDocument();
            doc.ReplaceItemValue("Form", "DotNetToDominoIntegrationTest").Recycle();
            doc.ReplaceItemValue("Subject", "Integration test - not saved").Recycle();
            _ = doc.HasItem("Subject");
            _ = doc.GetItemValue("Subject");
        });
        Check("MIME open mode", () =>
        {
            using var all = db.AllDocuments;
            var first = all.GetFirstDocument();
            if (first is null) return;
            var noteId = first.NoteID;
            first.Recycle();
            session.ConvertMIME = false;
            using var raw = db.GetDocumentByID(noteId);
            session.ConvertMIME = true;
            using var converted = db.GetDocumentByID(noteId);
        });
        db.Recycle();
    }
}

if (!string.IsNullOrWhiteSpace(replicaId))
{
    Check("OpenByReplicaID", () =>
    {
        using var db = session.OpenByReplicaID(server, replicaId);
        if (!db.IsOpen) throw new Exception(db.OpenError);
    });
}

Console.WriteLine(failed == 0 ? "ALL TESTS PASSED" : $"{failed} TEST(S) FAILED");
return failed == 0 ? 0 : 1;
