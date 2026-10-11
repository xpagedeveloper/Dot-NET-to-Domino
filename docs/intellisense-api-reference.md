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
| `NotesSession.CreateMail` | `session.CreateMail()` | none | Creates a mail memo builder. | [mail parity](xpscript-missing-notes-surface.md) |
| `NotesSession.GetDbDirectory` | `session.GetDbDirectory(server)` | `server`: optional Domino server. | Opens a database directory enumerator. | [TODO](../todo/xpscript-notes-runtime-parity.md) |
| `NotesDBDirectory.Name` | `directory.Name` | none | Gets the directory server name. | [TODO](../todo/xpscript-notes-runtime-parity.md) |
| `NotesDBDirectory.GetFirstDatabase` | `directory.GetFirstDatabase(type)` | `type`: database filter constant. | Starts database enumeration. | [TODO](../todo/xpscript-notes-runtime-parity.md) |
| `NotesDBDirectory.GetNextDatabase` | `directory.GetNextDatabase()` | none | Gets the next database in the enumeration. | [TODO](../todo/xpscript-notes-runtime-parity.md) |
| `NotesDBDirectory.OpenDatabase` | `directory.OpenDatabase(filePath)` | `filePath`: database path. | Opens a database from the directory server. | [TODO](../todo/xpscript-notes-runtime-parity.md) |
| `NotesMail.SendTo` | `mail.SendTo = recipients` | recipients: primary recipients. | Sets primary mail recipients. | [mail parity](xpscript-missing-notes-surface.md) |
| `NotesMail.Subject` | `mail.Subject = subject` | subject: message subject. | Sets the mail subject. | [mail parity](xpscript-missing-notes-surface.md) |
| `NotesMail.Body` | `mail.Body = body` | body: plain-text message body. | Sets the plain-text body. | [mail parity](xpscript-missing-notes-surface.md) |
| `NotesMail.Send` | `mail.Send()` | none | Builds and sends a memo through the configured Notes mail database. | [mail parity](xpscript-missing-notes-surface.md) |
| `NotesDatabase` | `session.OpenDatabase(server, filePath)` | `server`, `filePath`: database location. | Represents an NSF database. | [README](../README.md) |
| `NotesDatabase.QueryAccess` | `database.QueryAccess()` | none | Returns a managed snapshot of effective ACL access. | [ACL parity](notes-database-access-parity.md) |
| `NotesDatabase.GetDocumentByID` | `database.GetDocumentByID(noteId)` | `noteId`: hexadecimal or numeric note ID. | Opens a document by note ID. | [README](../README.md) |
| `NotesDatabase.GetDocumentByUNID` | `database.GetDocumentByUNID(unid)` | `unid`: 32-character universal ID. | Opens a document by universal ID. | [README](../README.md) |
| `NotesDatabase.GetMIMEEntity` | `document.GetMIMEEntity(itemName)` | `itemName`: currently the Body item. | Opens a MIME entity directory. | [MIME parity](../todo/xpscript-notes-runtime-parity.md) |
| `NotesDocument.ReplaceItemValue` | `document.ReplaceItemValue(name, value)` | `name`, `value`: item name and value. | Replaces or creates a document item. | [README](../README.md) |
| `NotesItem.Text` | `item.Text` | none | Converts the item value to Notes text using the native API. | [TODO](../todo/xpscript-notes-runtime-parity.md) |
| `NotesItem.Values` | `item.Values` | none | Reads or replaces item values. | [TODO](../todo/xpscript-notes-runtime-parity.md) |
| `NotesItem.Remove` | `item.Remove()` | none | Removes the item and invalidates its wrapper. | [TODO](../todo/xpscript-notes-runtime-parity.md) |
| `NotesDocument.Save` | `document.Save(force, makeResponse)` | `force`, `makeResponse`: save options. | Saves a document through the native API. | [README](../README.md) |
| `NotesDocument.ComputeWithForm` | `document.ComputeWithForm(form, raiseError)` | `form`, `raiseError`: compute options. | Computes document fields using a form. | [TODO](../todo/xpscript-notes-runtime-parity.md) |
| `NotesMIMEEntity.ContentAsText` | `entity.ContentAsText` | none | Reads root MIME content as text. | [MIME parity](../todo/xpscript-notes-runtime-parity.md) |
| `NotesMIMEEntity.GetContentAsBytes` | `entity.GetContentAsBytes(stream)` | `stream`: destination Notes stream. | Reads root MIME content as bytes. | [MIME parity](../todo/xpscript-notes-runtime-parity.md) |
| `NotesMIMEEntity.GetNthHeader` | `entity.GetNthHeader(name, occurrence)` | `name`, `occurrence`: header lookup. | Looks up a MIME header; native header support remains incomplete. | [MIME parity](../todo/xpscript-notes-runtime-parity.md) |
| `NotesMIMEEntity.GetHeaders` | `entity.GetHeaders(name)` | `name`: optional header-name filter. | Enumerates root MIME headers and preserves duplicate occurrence order. | [MIME parity](../todo/xpscript-notes-runtime-parity.md) |
| `NotesMIMEHeader.HeaderName` | `header.HeaderName` | none | Gets the root MIME header name. | [MIME parity](../todo/xpscript-notes-runtime-parity.md) |
| `NotesMIMEHeader.GetHeaderVal` | `header.GetHeaderVal()` | none | Gets the root MIME header value. | [MIME parity](../todo/xpscript-notes-runtime-parity.md) |
| `NotesMIMEHeader.GetParamVal` | `header.GetParamVal(name)` | `name`: MIME parameter name. | Gets a root MIME header parameter. | [MIME parity](../todo/xpscript-notes-runtime-parity.md) |
| `NotesMIMEHeader.SetHeaderVal` | `header.SetHeaderVal(value)` | `value`: replacement value. | Replaces a root MIME header value. | [MIME parity](../todo/xpscript-notes-runtime-parity.md) |
| `NotesMIMEHeader.SetHeaderValAndParams` | `header.SetHeaderValAndParams(value)` | `value`: replacement value and parameters. | Replaces a root MIME header value and parameters. | [MIME parity](../todo/xpscript-notes-runtime-parity.md) |
| `NotesMIMEHeader.AddValText` | `header.AddValText(value)` | `value`: text to append. | Appends text to a root MIME header value. | [MIME parity](../todo/xpscript-notes-runtime-parity.md) |
| `NotesMIMEHeader.Remove` | `header.Remove()` | none | Removes a root MIME header. | [MIME parity](../todo/xpscript-notes-runtime-parity.md) |
| `NotesMIMEEntity.RemoveHeaders` | `entity.RemoveHeaders(name)` | `name`: header name. | Removes all matching root MIME headers. | [MIME parity](../todo/xpscript-notes-runtime-parity.md) |
| `NotesMIMEEntity.SetContentFromText` | `entity.SetContentFromText(stream, contentType, encoding)` | `stream`, `contentType`, `encoding`: text content and MIME encoding. | Replaces root MIME content from text. | [MIME parity](../todo/xpscript-notes-runtime-parity.md) |
| `NotesMIMEEntity.SetContentFromBytes` | `entity.SetContentFromBytes(stream, contentType, encoding)` | `stream`, `contentType`, `encoding`: byte content and MIME encoding. | Replaces root MIME content from bytes. | [MIME parity](../todo/xpscript-notes-runtime-parity.md) |
| `NotesMIMEEntity.EncodeContent` | `entity.EncodeContent(encoding)` | `encoding`: MIME encoding constant. | Re-encodes decoded root content. | [MIME parity](../todo/xpscript-notes-runtime-parity.md) |
| `NotesMIMEEntity.DecodeContent` | `entity.DecodeContent()` | none | Decodes root content and stores it as 8bit. | [MIME parity](../todo/xpscript-notes-runtime-parity.md) |
| `NotesMIMEEntity.Preamble` | `entity.Preamble` | none | Reads or replaces the root multipart preamble. | [MIME parity](../todo/xpscript-notes-runtime-parity.md) |
| `NotesMIMEEntity.GetChildren` | `entity.GetChildren()` | none | Enumerates direct child MIME entities using the native MIME directory. | [MIME parity](../todo/xpscript-notes-runtime-parity.md) |
| `NotesView.GetFirstDocument` | `view.GetFirstDocument()` | none | Returns the first document in a view. | [README](../README.md) |
| `NotesView.GetLastDocument` | `view.GetLastDocument()` | none | Returns the last document in a view. | [TODO](../todo/xpscript-notes-runtime-parity.md) |
| `NotesView.GetPrevDocument` | `view.GetPrevDocument(document)` | `document`: current document. | Returns the preceding document in a view. | [TODO](../todo/xpscript-notes-runtime-parity.md) |
| `NotesView.CreateViewNav` | `view.CreateViewNav()` | none | Creates a view navigator. | [README](../README.md) |
| `NotesStream.Read` | `stream.Read(maxBytes)` | `maxBytes`: requested byte count. | Reads bytes from a Notes stream. | [README](../README.md) |
| `NotesSession.CreateStream` | `session.CreateStream()` | none | Creates a Notes stream. | [README](../README.md) |
| `NotesDateTime.SetNow` | `dateTime.SetNow()` | none | Replaces the value with the current Domino time/date. | [TODO](../todo/xpscript-notes-runtime-parity.md) |
| `NotesDateTime.TimeDifference` | `dateTime.TimeDifference(other)` | `other`: another NotesDateTime. | Returns the GMT difference in seconds. | [TODO](../todo/xpscript-notes-runtime-parity.md) |
| `NotesDateTime.TimeDifferenceDouble` | `dateTime.TimeDifferenceDouble(other)` | `other`: another NotesDateTime. | Returns the GMT difference in seconds as double. | [TODO](../todo/xpscript-notes-runtime-parity.md) |
| `NotesDateTime.ConvertToZone` | `dateTime.ConvertToZone(zone)` | `zone`: Domino time-zone number. | Converts the Notes time/date value using Domino zone semantics. | [TODO](../todo/xpscript-notes-runtime-parity.md) |
| `NotesRichTextNavigator.SetPosition` | `navigator.SetPosition(element)` | `element`: linked rich-text element. | Positions the navigator at a rich-text element from the same item. | [TODO](../todo/xpscript-notes-runtime-parity.md) |
| `NotesRichTextNavigator.SetPositionAtEnd` | `navigator.SetPositionAtEnd(element)` | `element`: linked rich-text element. | Positions at the end of a rich-text element. | [TODO](../todo/xpscript-notes-runtime-parity.md) |
| `NotesRichTextRange.Remove` | `range.Remove()` | none | Removes a range covering complete rich-text records. | [TODO](../todo/xpscript-notes-runtime-parity.md) |
| `NotesRichTextRange.SetStyle` | `range.SetStyle(style)` | `style`: NotesRichTextStyle. | Applies character styling to text-run CD records in the range. | [TODO](../todo/xpscript-notes-runtime-parity.md) |
| `NotesRichTextSection.Remove` | `section.Remove()` | none | Removes the complete nested section span. | [TODO](../todo/xpscript-notes-runtime-parity.md) |
| `NotesRichTextSection.SetBarColor` | `section.SetBarColor(color)` | `color`: NotesColorObject. | Updates the section bar color through CD rewrite. | [TODO](../todo/xpscript-notes-runtime-parity.md) |
| `NotesRichTextSection.SetTitleStyle` | `section.SetTitleStyle(style)` | `style`: NotesRichTextStyle. | Updates section title styling through CD rewrite. | [TODO](../todo/xpscript-notes-runtime-parity.md) |
| `NotesRichTextTable.RowCount` | `table.RowCount` | none | Gets the parsed table row count. | [TODO](../todo/xpscript-notes-runtime-parity.md) |
| `NotesRichTextTable.ColumnCount` | `table.ColumnCount` | none | Gets the parsed table column count. | [TODO](../todo/xpscript-notes-runtime-parity.md) |
| `NotesRichTextTable.RightToLeft` | `table.RightToLeft` | none | Gets the table direction flag. | [TODO](../todo/xpscript-notes-runtime-parity.md) |

