namespace NotesToDotNet;

/// <summary>Represents an error raised by the managed Notes/Domino wrapper.</summary>
public sealed class NotesException : Exception
{
    public NotesException(string message) : base(message)
    {
    }

    public NotesException(string message, Exception innerException) : base(message, innerException)
    {
    }

    public NotesException(int status, string message) : base(message)
    {
        Status = status;
    }

    /// <summary>Native Notes status code when one is available.</summary>
    public int? Status { get; }
}
