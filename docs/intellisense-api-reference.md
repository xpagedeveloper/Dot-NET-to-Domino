# Machine-readable IntelliSense API reference

This file is the canonical machine-readable supplement for the public .NET API. One public object, property, method, or factory occupies one row. Tooling can consume the table without parsing prose documentation.

The required columns are `Member`, `Syntax`, `Parameters`, `Description`, and `Example`.

| Member | Syntax | Parameters | Description | Example |
|---|---|---|---|---|
| `NotesSession` | `new NotesSession(runtimeDirectory)` | `runtimeDirectory`: Notes/Domino runtime directory. | Creates a native Notes session. | [README](../README.md) |
| `NotesSession.OpenDatabase` | `session.OpenDatabase(server, filePath)` | `server`, `filePath`: database location. | Opens a Notes database. | [README](../README.md) |
| `NotesSession.HashPassword` | `session.HashPassword(password)` | `password`: password text. | Creates a Domino password digest. | [session parity](notes-session-runtime-parity.md) |
| `NotesSession.VerifyPassword` | `session.VerifyPassword(password, digest)` | `password`, `digest`: password and digest. | Verifies a Domino password digest. | [session parity](notes-session-runtime-parity.md) |
| `NotesSession.GetEnvironmentString` | `session.GetEnvironmentString(name)` | `name`: Notes environment variable. | Reads a Notes environment string. | [session parity](notes-session-environment-api.md) |
| `NotesSession.GetEnvironmentValue` | `session.GetEnvironmentValue(name)` | `name`: Notes environment variable. | Reads an invariant 32-bit integer environment value. | [environment sample](../samples/notes-session-environment-parity.cs) |
| `NotesSession.SetEnvironmentVar` | `session.SetEnvironmentVar(name, value)` | `name`, `value`: variable name and supported value. | Writes a Notes environment variable. | [environment sample](../samples/notes-session-environment-parity.cs) |
| `NotesDatabase` | `session.OpenDatabase(server, filePath)` | `server`, `filePath`: database location. | Represents an NSF database. | [README](../README.md) |
| `NotesDatabase.QueryAccess` | `database.QueryAccess()` | none | Returns a managed snapshot of effective ACL access. | [ACL parity](notes-database-access-parity.md) |
| `NotesDatabase.GetDocumentByID` | `database.GetDocumentByID(noteId)` | `noteId`: hexadecimal or numeric note ID. | Opens a document by note ID. | [README](../README.md) |
| `NotesDatabase.GetDocumentByUNID` | `database.GetDocumentByUNID(unid)` | `unid`: 32-character universal ID. | Opens a document by universal ID. | [README](../README.md) |
| `NotesDatabase.GetMIMEEntity` | `document.GetMIMEEntity(itemName)` | `itemName`: currently the Body item. | Opens a MIME entity directory. | [MIME parity](../todo/xpscript-notes-runtime-parity.md) |
| `NotesDocument.ReplaceItemValue` | `document.ReplaceItemValue(name, value)` | `name`, `value`: item name and value. | Replaces or creates a document item. | [README](../README.md) |
| `NotesDocument.Save` | `document.Save(force, makeResponse)` | `force`, `makeResponse`: save options. | Saves a document through the native API. | [README](../README.md) |
| `NotesDocument.ComputeWithForm` | `document.ComputeWithForm(form, raiseError)` | `form`, `raiseError`: compute options. | Computes document fields using a form. | [TODO](../todo/xpscript-notes-runtime-parity.md) |
| `NotesMIMEEntity.ContentAsText` | `entity.ContentAsText` | none | Reads root MIME content as text. | [MIME parity](../todo/xpscript-notes-runtime-parity.md) |
| `NotesMIMEEntity.GetContentAsBytes` | `entity.GetContentAsBytes(stream)` | `stream`: destination Notes stream. | Reads root MIME content as bytes. | [MIME parity](../todo/xpscript-notes-runtime-parity.md) |
| `NotesMIMEEntity.GetNthHeader` | `entity.GetNthHeader(name, occurrence)` | `name`, `occurrence`: header lookup. | Looks up a MIME header; native header support remains incomplete. | [MIME parity](../todo/xpscript-notes-runtime-parity.md) |
| `NotesView.GetFirstDocument` | `view.GetFirstDocument()` | none | Returns the first document in a view. | [README](../README.md) |
| `NotesView.CreateViewNav` | `view.CreateViewNav()` | none | Creates a view navigator. | [README](../README.md) |
| `NotesStream.Read` | `stream.Read(maxBytes)` | `maxBytes`: requested byte count. | Reads bytes from a Notes stream. | [README](../README.md) |
| `NotesSession.CreateStream` | `session.CreateStream()` | none | Creates a Notes stream. | [README](../README.md) |

