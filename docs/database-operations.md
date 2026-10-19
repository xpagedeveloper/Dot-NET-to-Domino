# Databases: title, replica ID, and copying

`Title` is the database display name in Notes. `FilePath` and `FileName` identify the NSF file and are read-only. `ReplicaID` is also read-only; change it with `SetReplicaId(...)`.

An empty server name means a local database. Relative NSF paths resolve against the Notes data directory. `GetDatabase(..., false)` returns `null` when the database is missing. The server name `local` is normalized to an empty server before database operations.

```csharp
using var database = session.GetDatabase("", "example.nsf", false)
    ?? throw new InvalidOperationException("The database is missing.");
database.Title = "My database";
database.Categories = "Example, Test";
```

## Copy and assign a separate name and replica ID

```csharp
var originalReplica = database.ReplicaID;
using (var copy = database.CreateCopy("", "example-copy.nsf", includeDocuments: true))
{
    copy.Title = "My separate copy";
    copy.SetReplicaId("01234567:89ABCDEF");
}
```

Metadata setters write directly; `NotesDatabase.Save()` is not required. `NotesDocument.Save()` is required for document changes. `includeDocuments` defaults to `false`, so only the design is copied. Use `true` to copy documents. `CreateCopy` creates a new replica ID; call `SetReplicaId` only when a specific ID is required.

For a new file name, create the copy at the new NSF path. There is no API method for renaming an existing NSF file in place. Changing `Title` changes only the display name.

## Regression tests

```powershell
dotnet build test/DotNetToDomino.FullTestCli -c Release
./test/Run-NativeTests.ps1 -Filters 'database lifecycle regression' -ShowOutput
```

See [HCL NSFDbCreateAndCopy](https://opensource.hcltechsw.com/domino-c-api-docs/reference/Func/NSFDbCreateAndCopy/).
