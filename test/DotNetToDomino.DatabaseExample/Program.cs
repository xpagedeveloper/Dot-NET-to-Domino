using DotNetToDomino;

if (args.Length != 5)
{
    Console.Error.WriteLine("Usage: DatabaseExample <Notes runtime> <source.nsf> <copy.nsf> <copy title> <new replica ID>");
    return 2;
}
using var session = new NotesSession(args[0], Environment.GetEnvironmentVariable("NOTES_TEST_INI"), Environment.GetEnvironmentVariable("NOTES_TEST_ID_PASSWORD"));
var sourcePath = args[1];
var copyPath = args[2];
using var original = session.GetDatabase("", sourcePath, false) ?? throw new InvalidOperationException("Source database not found.");
var replicaId = args[4].Replace(":", "").Replace("-", "").Trim().ToUpperInvariant();
if (replicaId.Length != 16 || replicaId.Any(c => !Uri.IsHexDigit(c)) || replicaId == original.ReplicaID)
    throw new ArgumentException("Specify a different replica ID with 16 hexadecimal characters.");
if (string.Equals(Path.GetFullPath(sourcePath, session.DataDir), Path.GetFullPath(copyPath, session.DataDir), StringComparison.OrdinalIgnoreCase))
    throw new ArgumentException("Copy must use a different file path.");
using (var copy = original.CreateCopy("", copyPath, includeDocuments: true))
{
    copy.Title = args[3];
    copy.SetReplicaId(replicaId);
}
using var reopened = session.GetDatabase("", copyPath, false) ?? throw new InvalidOperationException("Cannot reopen the copy.");
if (reopened.Title != args[3] || reopened.ReplicaID != replicaId)
    throw new InvalidOperationException("Copy metadata did not persist.");
Console.WriteLine($"Original: {original.FilePath}; title={original.Title}; replica={original.ReplicaID}");
Console.WriteLine($"Copy:     {reopened.FilePath}; title={reopened.Title}; replica={reopened.ReplicaID}");
return 0;
