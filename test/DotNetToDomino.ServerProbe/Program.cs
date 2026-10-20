using DotNetToDomino;
using System.Runtime.InteropServices;

Console.WriteLine($"Current directory: {Environment.CurrentDirectory}");
Console.WriteLine($"Process architecture: {RuntimeInformation.ProcessArchitecture}");
Console.WriteLine($"Base directory: {AppContext.BaseDirectory}");
Console.WriteLine($"PATH: {Environment.GetEnvironmentVariable("PATH")}");
Console.WriteLine($"NOTES_RUNTIME: {Environment.GetEnvironmentVariable("NOTES_RUNTIME")}");
Console.WriteLine($"NOTES_INI: {Environment.GetEnvironmentVariable("NOTES_INI")}");

var requiredNativePath = @"C:\Program Files\Microsoft Visual Studio\18\Community\Common7\IDE\PrivateAssemblies\runtimes\win-x64\native";
var pathEntries = (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
    .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);
if (!pathEntries.Any(path => string.Equals(path.TrimEnd('\\'), requiredNativePath.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase)))
{
    Console.Error.WriteLine($"ERROR: PATH must contain the Visual Studio native runtime directory: {requiredNativePath}");
    Console.Error.WriteLine("Add it to PATH before running the server probe or configure Visual Studio to inherit it.");
    return 2;
}

var runtime = args.Length > 0 ? args[0] : @"D:\Program Files\HCL\Notes";
var notesIni = args.Length > 1 ? args[1] : Path.Combine(runtime, "notes.ini");
var password = args.Length > 2
    ? args[2]
    : File.ReadLines(notesIni)
        .First(line => line.StartsWith("ID_Pass=", StringComparison.Ordinal))
        .Substring("ID_Pass=".Length);

using var session = new NotesSession(runtime, notesIni, password);

foreach (var serverName in new[] { "QSProd1", "QSProd1/QNOVA" })
{
    var ping = session.PingServer(serverName);
    Console.WriteLine($"Ping {serverName}: reachable={ping.IsReachable}, status=0x{ping.Status:X4}");

    using var database = session.GetDatabase(serverName, "names.nsf", createOnFail: false);
    if (database is not null && database.IsOpen)
        Console.WriteLine($"Database names.nsf is open on {serverName}: {database.Title}");
    else
        Console.WriteLine($"Database names.nsf failed on {serverName}: {database?.OpenError}");
}

return 0;
