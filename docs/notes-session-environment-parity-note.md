# NotesSession environment parity source mapping

XPScript source of truth: `src/XPScript.Compiler/NotesSessionEnvironmentPostProcessor.cs`.

The .NET wrapper intentionally maps XPScript runtime errors to `NotesException` with the same numeric error codes:

- empty environment variable name: 5
- unsupported environment variable value type: 13

Supported values for `SetEnvironmentVar` are string, integral numeric CLR types, and `DateTime`. Floating-point values, booleans, null and arbitrary objects are rejected rather than converted with `Convert.ToString`.
