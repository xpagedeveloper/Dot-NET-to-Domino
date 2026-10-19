# NotesSession environment compatibility

For compatibility with HCL C API and JNI projects, `SetEnvironmentVar` is deliberately stricter than general .NET string conversion. Callers should pass a string, integral value, or DateTime. Use an explicit string conversion in application code when another representation is intentionally required.
