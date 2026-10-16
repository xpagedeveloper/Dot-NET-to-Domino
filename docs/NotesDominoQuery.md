# NotesDominoQuery

`NotesDatabase.CreateDominoQuery()` now creates a session-owned
`NotesDominoQuery` configuration object with the DQL execution options from the
LotusScript and Java APIs:

```csharp
using var query = database.CreateDominoQuery();
query.MaxScanDocs = 100_000;
query.MaxScanEntries = 50_000;
query.TimeoutSec = 60;
query.NoViews = true;
```

The implementation now calls the C API function `NSFQueryDBExt2`, the same native
entry point exposed by JNX's `INotesCAPI`, for `Execute`, `Explain`, and `Parse`.
The returned ID table is converted to `NotesDocumentCollection`. Index-management
methods are intentionally not part of this .NET API because JNX does not expose a
corresponding C API entry point.
