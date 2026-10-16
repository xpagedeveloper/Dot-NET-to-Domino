namespace DotNetToDomino;

/// <summary>Base class for managed Notes objects owned by a <see cref="NotesSession"/>.</summary>
public abstract class NotesObject : IDisposable
{
    private bool _recycled;

    protected NotesObject(NotesSession session)
    {
        Session = session ?? throw new ArgumentNullException(nameof(session));
        Session.Register(this);
    }

    protected NotesSession Session { get; }

    /// <summary>Gets whether this wrapper has been recycled.</summary>
    public bool IsRecycled => _recycled;

    /// <summary>Releases resources owned by this wrapper.</summary>
    public void Recycle()
    {
        if (_recycled)
            return;

        _recycled = true;
        try
        {
            ReleaseNative();
        }
        finally
        {
            Session.Unregister(this);
        }

        GC.SuppressFinalize(this);
    }

    public void Dispose() => Recycle();

    protected void EnsureAlive()
    {
        if (_recycled)
            throw new ObjectDisposedException(GetType().Name, $"{GetType().Name} has been recycled.");

        Session.EnsureAlive();
    }

    protected static T Require<T>(T? value, string parameterName) where T : class =>
        value ?? throw new ArgumentNullException(parameterName, $"'{parameterName}' cannot be null.");

    protected static string RequireText(string? value, string parameterName) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException($"'{parameterName}' cannot be null, empty, or whitespace.", parameterName)
            : value.Trim();

    protected static int RequireRange(int value, int minimum, int maximum, string parameterName) =>
        value < minimum || value > maximum
            ? throw new ArgumentOutOfRangeException(parameterName, value, $"'{parameterName}' must be between {minimum} and {maximum}.")
            : value;

    protected abstract void ReleaseNative();
}
