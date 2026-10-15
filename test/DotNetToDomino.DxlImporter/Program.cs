using DotNetToDomino;

static void Usage()
{
    Console.Error.WriteLine("Usage: DotNetToDomino.DxlImporter <runtimeDirectory> <dxlFile> <targetNsf> [server|local|--local] [notesIni]");
    Environment.ExitCode = 2;
}

if (args.Length < 3 || args.Length > 5) { Usage(); return; }

var runtime = Path.GetFullPath(args[0]);
var dxlPath = Path.GetFullPath(args[1]);
var targetPath = args[2].Trim();
var server = args.Length >= 4 ? args[3].Trim() : string.Empty;
if (server.Equals("--local", StringComparison.OrdinalIgnoreCase) || server.Equals("local", StringComparison.OrdinalIgnoreCase)) server = string.Empty;
if (Path.IsPathRooted(server) || server.EndsWith(".ini", StringComparison.OrdinalIgnoreCase))
    throw new ArgumentException("The server argument contains a file path. Windows PowerShell may have dropped an empty argument. Use --local instead of \"\" before the notes.ini argument.");
var notesIni = args.Length == 5 ? Path.GetFullPath(args[4]) : null;

if (!File.Exists(dxlPath)) throw new FileNotFoundException("DXL file was not found.", dxlPath);
if (targetPath.Length == 0) throw new ArgumentException("Target NSF path cannot be empty.", nameof(args));
Console.WriteLine($"Database arguments: server='{server}', path='{targetPath}', notesIni='{notesIni ?? "(default)"}'");
var wrapperAssembly = typeof(NotesSession).Assembly;
Console.WriteLine($"DotNetToDomino version: {System.Reflection.CustomAttributeExtensions.GetCustomAttribute<System.Reflection.AssemblyInformationalVersionAttribute>(wrapperAssembly)?.InformationalVersion}; assembly: {wrapperAssembly.Location}");

var runtimeInfo = NotesRuntimeDiagnostics.Inspect(runtime);
Console.WriteLine($"Native runtime: {runtimeInfo.NativeLibraryPath}");
Console.WriteLine($"Exists: {runtimeInfo.Exists}, size: {runtimeInfo.FileSize}, version: {runtimeInfo.FileVersion}, file architecture: {runtimeInfo.FileArchitecture}, process architecture: {runtimeInfo.ProcessArchitecture}");
if (!runtimeInfo.Exists) throw new FileNotFoundException("The expected Notes native library was not found.", runtimeInfo.NativeLibraryPath);
if (!runtimeInfo.ArchitectureMatches) throw new BadImageFormatException($"Native Notes runtime architecture '{runtimeInfo.FileArchitecture}' does not match process architecture '{runtimeInfo.ProcessArchitecture}'.");

using var session = new NotesSession(runtime, notesIni, Environment.GetEnvironmentVariable("NOTES_TEST_ID_PASSWORD"));
Console.WriteLine($"Notes session: user='{session.UserName}', dataDir='{session.DataDir}'");
using (var existing = session.GetDatabase(string.Empty, "test.nsf", false))
{
    if (existing is null || !existing.IsOpen)
        throw new NotesException($"Unable to open local test.nsf: {existing?.OpenError ?? "database not found"}");
    Console.WriteLine($"Opened local test.nsf: title='{existing.Title}', replicaId={existing.ReplicaID}");
}
using var database = session.OpenDatabase(server, targetPath);
if (!database.IsOpen)
{
    database.Create(server, targetPath, open: true);
    if (!database.IsOpen) throw new NotesException($"Unable to create target database: {database.OpenError}");
}
foreach (NotesView v in database.Views) {
    Console.WriteLine($"View: {v.Name}");
}

using var importer = session.CreateDXLImporter();
// DesignImportOption=6 means replace-or-create design elements, including agents.
// The C API default is to ignore design elements.
importer.DesignImportOption = 6;
importer.Import(dxlPath, database);
// DXL restores design/code; establish ownership with the authenticated ID explicitly.
database.Sign();
using (var agents = database.CreateNoteCollection(false))
{
    agents.SelectAgents = true;
    agents.BuildCollection();
    foreach (var noteId in agents)
    {
        using var design = database.GetDocumentByID(noteId.ToString("X")) ?? throw new NotesException("Imported agent could not be reopened.");
        if (!design.IsSigned || design.Signer.Length == 0) throw new NotesException("Imported agent signature could not be verified.");
        Console.WriteLine($"Signed agent: {design.DesignTitle}; signer: {design.Signer}; verifier: {design.Verifier}");
    }
}
Console.WriteLine($"DXL imported successfully into {server}/{targetPath}.");
Console.WriteLine($"Imported notes: {importer.ImportedNoteCount}");
