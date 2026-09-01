namespace NotesToDotNet;

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

    protected abstract void ReleaseNative();
}
