namespace DotNetToDomino;

/// <summary>Notes MIME convenience entry points not present directly on the owning object.</summary>
public static class NotesMimeExtensions
{
    public static NotesMIMEEntity? GetMIMEEntity(this NotesItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return item.GetMIMEEntity();
    }
}
