# NotesSession GetDatabase parity audit

HCL C API and JNI projects source of truth: `src/HCL C API and JNI projects.Compiler/NotesDatabaseCreateCompatibilityPostProcessor.cs`.

The current wrapper matches the implemented HCL C API and JNI projects behavior:

- `GetDatabase(server, filePath)` defaults `createOnFail` to true.
- Opening is attempted through the normal database-open path.
- If opening succeeds, the open wrapper is returned.
- If opening fails and `createOnFail` is true, the closed wrapper is returned with its open error retained.
- If opening fails and `createOnFail` is false, the failed wrapper is recycled and null is returned.
- Empty file path creates a closed, assigned/unassigned wrapper rather than forcing an open attempt.

No code change was required for this member in this audit.
