# NotesSession GetDatabase parity audit

XPScript source of truth: `src/XPScript.Compiler/NotesDatabaseCreateCompatibilityPostProcessor.cs`.

The current wrapper matches the implemented XPScript behavior:

- `GetDatabase(server, filePath)` defaults `createOnFail` to true.
- Opening is attempted through the normal database-open path.
- If opening succeeds, the open wrapper is returned.
- If opening fails and `createOnFail` is true, the closed wrapper is returned with its open error retained.
- If opening fails and `createOnFail` is false, the failed wrapper is recycled and null is returned.
- Empty file path creates a closed, assigned/unassigned wrapper rather than forcing an open attempt.

No code change was required for this member in this audit.
