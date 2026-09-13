using DotNetToDomino.Interop;

namespace DotNetToDomino;

internal sealed class NotesMimeDirectoryOwner : IDisposable
{
    private NotesNativeApi? _api;
    private nint _directory;

    internal NotesMimeDirectoryOwner(NotesNativeApi api, nint note)
    {
        _api = api ?? throw new ArgumentNullException(nameof(api));
        _directory = api.OpenMimeDirectory(note);
    }

    internal bool IsAlive => _directory != 0;
    internal nint RootEntity { get { EnsureAlive(); return _api!.GetMimeRootEntity(_directory); } }
    internal nint FirstSubpart(nint entity) { EnsureAlive(); return _api!.GetMimeFirstSubpart(_directory, entity); }
    internal nint NextSibling(nint entity) { EnsureAlive(); return _api!.GetMimeNextSibling(_directory, entity); }
    internal nint PrevSibling(nint entity) { EnsureAlive(); return _api!.GetMimePrevSibling(_directory, entity); }
    internal nint Parent(nint entity) { EnsureAlive(); return _api!.GetMimeParent(_directory, entity); }
    internal nint IterateNext(nint top, nint previous) { EnsureAlive(); return _api!.IterateMimeNext(_directory, top, previous); }
    internal int ContentTypeSymbol(nint entity) { EnsureAlive(); return _api!.GetMimeEntityContentTypeSymbol(entity); }
    internal int ContentSubtypeSymbol(nint entity) { EnsureAlive(); return _api!.GetMimeEntityContentSubtypeSymbol(entity); }
    internal string TypeParam(nint entity, int symbol) { EnsureAlive(); return _api!.GetMimeEntityTypeParam(entity, symbol); }

    public void Dispose()
    {
        var directory = _directory;
        _directory = 0;
        var api = _api;
        _api = null;
        if (directory != 0 && api is not null) api.FreeMimeDirectory(directory);
    }

    private void EnsureAlive()
    {
        if (_directory == 0 || _api is null) throw new ObjectDisposedException(nameof(NotesMimeDirectoryOwner));
    }
}
