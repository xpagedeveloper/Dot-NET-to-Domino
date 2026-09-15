using DotNetToDomino;

// Runtime smoke-test sample for NotesSession environment parity.
// Supply the Notes/Domino runtime directory for the machine under test.
using var session = new NotesSession(args[0]);

session.SetEnvironmentVar("XPSCRIPT_PARITY_TEST", 42);
if (session.GetEnvironmentString("XPSCRIPT_PARITY_TEST") != "42")
    throw new Exception("Environment string parity failed.");
if (session.GetEnvironmentValue("XPSCRIPT_PARITY_TEST") != 42)
    throw new Exception("Environment integer parity failed.");

var rejected = false;
try
{
    session.SetEnvironmentVar("XPSCRIPT_PARITY_TEST", true);
}
catch (NotesException ex) when (ex.ErrorCode == 13)
{
    rejected = true;
}
if (!rejected)
    throw new Exception("Unsupported environment value type was not rejected.");
