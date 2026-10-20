using System.Diagnostics;
using DotNetToDomino;

var runtime = args.Length > 0 ? args[0] : @"D:\Program Files\HCL\Notes";
var notesIni = args.Length > 1 ? args[1] : Path.Combine(runtime, "notes.ini");
var password = args.Length > 2
    ? args[2]
    : File.ReadLines(notesIni)
        .First(line => line.StartsWith("ID_Pass=", StringComparison.Ordinal))
        .Substring("ID_Pass=".Length);

using var session = new NotesSession(runtime, notesIni, password);
using var database = session.GetDatabase("", "names.nsf", createOnFail: false)
    ?? throw new InvalidOperationException("Could not open local names.nsf.");

if (!database.IsOpen)
    throw new InvalidOperationException(database.OpenError);

// These locals are intentionally kept alive so they can be inspected in Visual Studio.
var title = database.Title;
var fileName = database.FileName;
var replicaId = database.ReplicaID;
var firstView = database.Views.FirstOrDefault();
var navigator = firstView?.CreateViewNav();
var firstEntry = navigator?.GetFirst();
var firstDocument = firstEntry?.Document;
var firstItem = firstDocument?.Items.FirstOrDefault();
var acl = database.ACL;
var access = database.QueryAccess();
var replication = database.ReplicationInfo;
var sessionUser = session.UserName;

Console.WriteLine($"Database: {title}");
Console.WriteLine($"File: {fileName}");
Console.WriteLine($"Replica ID: {replicaId}");
Console.WriteLine($"First view: {firstView?.Name ?? "(none)"}");
Console.WriteLine($"First document: {firstDocument?.UniversalId ?? "(none)"}");
Console.WriteLine($"First item: {firstItem?.Name ?? "(none)"}");
Console.WriteLine($"Session user: {sessionUser}");
Console.WriteLine("Debugger probe is ready. Inspect the locals now.");

if (Debugger.IsAttached)
    Debugger.Break();
else
{
    Console.WriteLine("Attach the debugger now, then press Enter to continue.");
    Console.ReadLine();
}

// Keep all objects alive until after inspection.
GC.KeepAlive(session);
GC.KeepAlive(database);
GC.KeepAlive(firstView);
GC.KeepAlive(navigator);
GC.KeepAlive(firstEntry);
GC.KeepAlive(firstDocument);
GC.KeepAlive(firstItem);
GC.KeepAlive(acl);
GC.KeepAlive(access);
GC.KeepAlive(replication);
