namespace DotNetToDomino;

/// <summary>NotesSession database-directory parity members.</summary>
public static class NotesSessionDbDirectoryExtensions
{
    public static NotesDBDirectory GetDbDirectory(this NotesSession session, string server)
    {
        ArgumentNullException.ThrowIfNull(session);
        session.EnsureAlive();
        return new NotesDBDirectory(session, server?.Trim() ?? string.Empty);
    }
}
