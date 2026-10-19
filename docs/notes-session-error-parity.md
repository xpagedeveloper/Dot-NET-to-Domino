# NotesSession environment error parity

The wrapper uses `NotesException` to expose HCL C API and JNI projects-compatible numeric runtime errors for the environment surface. Error 5 denotes an invalid/empty variable name. Error 13 denotes a type mismatch for unsupported `SetEnvironmentVar` values.
