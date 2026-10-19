# NotesSession environment parity source mapping

HCL C API and JNI projects source of truth: `src/HCL C API and JNI projects.Compiler/NotesSessionEnvironmentPostProcessor.cs`.

The .NET wrapper intentionally maps HCL C API and JNI projects runtime errors to `NotesException` with the same numeric error codes:

- empty environment variable name: 5
- unsupported environment variable value type: 13

Supported values for `SetEnvironmentVar` are string, integral numeric CLR types, and `DateTime`. Floating-point values, booleans, null and arbitrary objects are rejected rather than converted with `Convert.ToString`.
