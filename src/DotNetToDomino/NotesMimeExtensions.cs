namespace DotNetToDomino;

/// <summary>Notes MIME entry points matching the XPScript document/item surface.</summary>
public static class NotesMimeExtensions
{
    public static NotesMIMEEntity? GetMIMEEntity(this NotesDocument document, string itemName = "Body")
    {
        ArgumentNullException.ThrowIfNull(document);
        itemName = string.IsNullOrWhiteSpace(itemName) ? "Body" : itemName.Trim();
        var session = document.ParentDatabase.Parent;
        if (!session.Api.IsMimeItem(document.Handle, itemName)) return null;
        return new NotesMIMEEntity(session, document, itemName);
    }

    public static NotesMIMEEntity CreateMIMEEntity(this NotesDocument document, string itemName = "Body")
    {
        ArgumentNullException.ThrowIfNull(document);
        itemName = string.IsNullOrWhiteSpace(itemName) ? "Body" : itemName.Trim();
        if (document.HasItem(itemName)) throw new InvalidOperationException($"Notes item '{itemName}' already exists.");
        var session = document.ParentDatabase.Parent;
        var initial = System.Text.Encoding.UTF8.GetBytes("Content-Type: text/plain; charset=UTF-8\r\nContent-Transfer-Encoding: 8bit\r\n\r\n");
        session.Api.WriteMimeStream(document.Handle, itemName, initial);
        return new NotesMIMEEntity(session, document, itemName);
    }

    public static void CloseMIMEEntities(this NotesDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        _ = document.Handle;
    }

    public static NotesMIMEEntity? GetMIMEEntity(this NotesItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return item.Parent.GetMIMEEntity(item.Name);
    }
}
