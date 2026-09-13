# NotesSession environment runtime smoke test

Run this against a machine with an HCL Notes/Domino runtime installed after building the wrapper.

```csharp
using DotNetToDomino;

using var session = new NotesSession(runtimeDirectory);
session.SetEnvironmentVar("XPSCRIPT_PARITY_TEST", 42);

if (session.GetEnvironmentString("XPSCRIPT_PARITY_TEST") != "42")
    throw new Exception("Environment string parity failed.");
if (session.GetEnvironmentValue("XPSCRIPT_PARITY_TEST") != 42)
    throw new Exception("Environment integer parity failed.");

try
{
    session.SetEnvironmentVar("XPSCRIPT_PARITY_TEST", true);
    throw new Exception("Boolean value should have raised Notes error 13.");
}
catch (NotesException ex) when (ex.ErrorCode == 13)
{
}
```

This test requires a real Notes/Domino runtime because the environment calls use the native runtime API.
