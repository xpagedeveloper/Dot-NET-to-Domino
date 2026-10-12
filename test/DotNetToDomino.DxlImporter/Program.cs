using DotNetToDomino;

static void Usage()
{
    Console.Error.WriteLine("Usage: DotNetToDomino.DxlImporter <runtimeDirectory> <dxlFile> <targetNsf> [server] [notesIni]");
    Environment.ExitCode = 2;
}

if (args.Length < 3 || args.Length > 5) { Usage(); return; }

var runtime = Path.GetFullPath(args[0]);
var dxlPath = Path.GetFullPath(args[1]);
var targetPath = args[2].Trim();
var server = args.Length >= 4 ? args[3].Trim() : string.Empty;
var notesIni = args.Length == 5 ? Path.GetFullPath(args[4]) : null;

if (!File.Exists(dxlPath)) throw new FileNotFoundException("DXL file was not found.", dxlPath);
if (targetPath.Length == 0) throw new ArgumentException("Target NSF path cannot be empty.", nameof(args));

using var session = new NotesSession(runtime, notesIni);
using var database = session.OpenDatabase(server, targetPath);
if (!database.IsOpen)
{
    database.Create(server, targetPath, open: true);
    if (!database.IsOpen) throw new NotesException($"Unable to create target database: {database.OpenError}");
}

using var importer = session.CreateDXLImporter();
importer.Import(dxlPath, database);
Console.WriteLine($"DXL imported successfully into {server}/{targetPath}.");
Console.WriteLine($"Imported notes: {importer.ImportedNoteCount}");
