using System.Reflection;
using DotNetToDomino;

var failures = new List<string>();
var executedChecks = 0;
void Check(string name, Action action)
{
    var filter = Environment.GetEnvironmentVariable("NOTES_TEST_FILTER");
    if (!string.IsNullOrWhiteSpace(filter) && !filter.Split(';').Any(part => name.Contains(part, StringComparison.OrdinalIgnoreCase))) return;
    Console.WriteLine($"RUN {name}");
    executedChecks++;
    try { action(); Console.WriteLine($"PASS {name}"); }
    catch (Exception ex) { failures.Add($"{name}: {ex.GetType().FullName}: {ex.Message}"); Console.Error.WriteLine($"FAIL {failures[^1]}"); }
}

void ExpectException<TException>(string name, Action action) where TException : Exception
{
    try
    {
        action();
        throw new InvalidOperationException($"{name}: expected {typeof(TException).Name}, but no exception was thrown.");
    }
    catch (TException)
    {
        Console.WriteLine($"PASS {name}");
    }
}

Check("NuGet assembly loaded", () =>
{
    if (typeof(NotesSession).Assembly.GetName().Name != "DotNetToDomino") throw new InvalidOperationException("Wrong assembly loaded.");
    var toByteArray = typeof(NotesEmbeddedObject).GetMethod(nameof(NotesEmbeddedObject.ToByteArray), Type.EmptyTypes)
        ?? throw new InvalidOperationException("NotesEmbeddedObject.ToByteArray is missing.");
    if (toByteArray.ReturnType != typeof(byte[])) throw new InvalidOperationException($"NotesEmbeddedObject.ToByteArray must return byte[], not {toByteArray.ReturnType.FullName}.");
    if (typeof(NotesStream).GetProperty(nameof(NotesStream.Position))?.PropertyType != typeof(int)) throw new InvalidOperationException("NotesStream.Position must be int.");
    if (typeof(NotesStream).GetMethod(nameof(NotesStream.Write), new[] { typeof(string) }) is null) throw new InvalidOperationException("NotesStream.Write(string) is missing.");
    if (typeof(NotesDXLImporter).GetMethod(nameof(NotesDXLImporter.Import), new[] { typeof(string), typeof(NotesDatabase) }) is null) throw new InvalidOperationException("NotesDXLImporter.Import is missing.");
    if (typeof(NotesDXLExporter).GetProperty(nameof(NotesDXLExporter.CleanedDXL))?.PropertyType != typeof(bool)) throw new InvalidOperationException("NotesDXLExporter.CleanedDXL is missing.");
    if (typeof(NotesRichTextStyle).GetField(nameof(NotesRichTextStyle.StyleNoChange))?.GetValue(null) is not int styleNoChange || styleNoChange != 255) throw new InvalidOperationException("NotesRichTextStyle.StyleNoChange must be 255.");
    if (typeof(NotesColorObject).GetProperty(nameof(NotesColorObject.NotesColor))?.PropertyType != typeof(int)) throw new InvalidOperationException("NotesColorObject.NotesColor must be int.");
    if (typeof(NotesForm).GetProperty(nameof(NotesForm.Parent))?.PropertyType != typeof(NotesDatabase)) throw new InvalidOperationException("NotesForm.Parent is missing.");
    foreach (var property in new[] { nameof(NotesForm.Name), nameof(NotesForm.NoteID), nameof(NotesForm.NotesURL) })
        if (typeof(NotesForm).GetProperty(property)?.PropertyType != typeof(string)) throw new InvalidOperationException($"NotesForm.{property} is missing.");
    if (typeof(NotesDocumentCollection).GetProperty(nameof(NotesDocumentCollection.Parent))?.PropertyType != typeof(NotesDatabase)) throw new InvalidOperationException("NotesDocumentCollection.Parent is missing.");
    if (typeof(NotesNoteCollection).GetProperty(nameof(NotesNoteCollection.Parent))?.PropertyType != typeof(NotesDatabase)) throw new InvalidOperationException("NotesNoteCollection.Parent is missing.");
    if (typeof(NotesView).GetProperty(nameof(NotesView.Parent))?.PropertyType != typeof(NotesDatabase)) throw new InvalidOperationException("NotesView.Parent is missing.");
    if (typeof(NotesViewColumn).GetProperty(nameof(NotesViewColumn.Parent))?.PropertyType != typeof(NotesView)) throw new InvalidOperationException("NotesViewColumn.Parent is missing.");
    if (typeof(NotesViewEntry).GetProperty(nameof(NotesViewEntry.Parent))?.PropertyType != typeof(NotesView)) throw new InvalidOperationException("NotesViewEntry.Parent is missing.");
    if (typeof(NotesRichTextItem).GetMethod(nameof(NotesRichTextItem.AppendParagraphStyle), new[] { typeof(NotesRichTextParagraphStyle) }) is null) throw new InvalidOperationException("NotesRichTextItem.AppendParagraphStyle is missing.");
    if (typeof(NotesRichTextItem).GetMethod(nameof(NotesRichTextItem.Update), Type.EmptyTypes) is null) throw new InvalidOperationException("NotesRichTextItem.Update is missing.");
    if (typeof(NotesRichTextItem).GetMethod(nameof(NotesRichTextItem.AppendDocLink), new[] { typeof(NotesDocument), typeof(string) }) is null) throw new InvalidOperationException("NotesRichTextItem.AppendDocLink is missing.");
    if (typeof(NotesRichTextItem).GetMethod(nameof(NotesRichTextItem.AppendNotesURL), new[] { typeof(string), typeof(string) }) is null) throw new InvalidOperationException("NotesRichTextItem.AppendNotesURL is missing.");
    if (typeof(NotesRichTextItem).GetMethod(nameof(NotesRichTextItem.AppendObject), new[] { typeof(string) }) is null) throw new InvalidOperationException("NotesRichTextItem.AppendObject is missing.");
    if (typeof(NotesRichTextItem).GetMethod(nameof(NotesRichTextItem.AppendTable), new[] { typeof(int), typeof(int) }) is null) throw new InvalidOperationException("NotesRichTextItem.AppendTable is missing.");
    if (typeof(NotesDateTime).GetMethod(nameof(NotesDateTime.SetAnyDate), Type.EmptyTypes) is null || typeof(NotesDateTime).GetMethod(nameof(NotesDateTime.SetAnyTime), Type.EmptyTypes) is null) throw new InvalidOperationException("NotesDateTime wildcard methods are missing.");
    foreach (var property in new[] { nameof(NotesDateTime.DateOnly), nameof(NotesDateTime.TimeOnly), nameof(NotesDateTime.GMTTime), nameof(NotesDateTime.ZoneTime) })
        if (typeof(NotesDateTime).GetProperty(property)?.PropertyType != typeof(string)) throw new InvalidOperationException($"NotesDateTime.{property} is missing.");
    if (typeof(NotesMail).GetProperty(nameof(NotesMail.IsMIME))?.PropertyType != typeof(bool) || typeof(NotesMail).GetProperty(nameof(NotesMail.AttachmentCount))?.PropertyType != typeof(int) || !typeof(NotesMail).GetMethods().Any(method => method.Name == "AddAttachment")) throw new InvalidOperationException("NotesMail MIME/attachment state surface is missing.");
    if (typeof(NotesRichTextItem).GetMethod("AttachFile") is null) throw new InvalidOperationException("NotesRichTextItem.AttachFile surface is missing.");
    if (typeof(NotesRichTextTable).GetMethod("Remove") is null || typeof(NotesRichTextDocLink).GetMethod("Remove") is null) throw new InvalidOperationException("Rich-text linked-object removal surface is missing.");
    if (typeof(NotesRichTextDocLink).GetProperty(nameof(NotesRichTextDocLink.HotSpotTextStyle)) is null ||
        typeof(NotesRichTextDocLink).GetMethod(nameof(NotesRichTextDocLink.RemoveLinkage)) is null ||
        typeof(NotesRichTextDocLink).GetMethod(nameof(NotesRichTextDocLink.SetHotSpotTextStyle)) is null)
        throw new InvalidOperationException("Rich-text doclink hotspot surface is missing.");
    if (typeof(NotesRichTextTable).GetMethod(nameof(NotesRichTextTable.RemoveRow), new[] { typeof(int) }) is null) throw new InvalidOperationException("NotesRichTextTable.RemoveRow is missing.");
    if (typeof(NotesRichTextTable).GetMethod(nameof(NotesRichTextTable.SetColor), new[] { typeof(uint) }) is null || typeof(NotesRichTextTable).GetMethod(nameof(NotesRichTextTable.SetAlternateColor), new[] { typeof(uint) }) is null) throw new InvalidOperationException("NotesRichTextTable color mutation surface is missing.");
    if (typeof(NotesDocumentCollection).GetMethod("StampAllMulti", new[] { typeof(NotesDocument) }) is null || typeof(NotesNoteCollection).GetMethod("StampAllMulti", new[] { typeof(NotesDocument) }) is null) throw new InvalidOperationException("Collection StampAllMulti source-document surface is missing.");
    if (typeof(NotesDocumentCollection).GetMethod("Merge", new[] { typeof(NotesDocumentCollection) }) is null || typeof(NotesDocumentCollection).GetMethod("Subtract", new[] { typeof(NotesDocumentCollection) }) is null) throw new InvalidOperationException("NotesDocumentCollection set-operation surface is missing.");
    if (typeof(NotesDocumentCollection).GetMethod("Clone") is null || typeof(NotesDocumentCollection).GetMethod("Contains", new[] { typeof(NotesDocument) }) is null) throw new InvalidOperationException("NotesDocumentCollection Clone/Contains surface is missing.");
    if (typeof(NotesEmbeddedObject).GetMethod("Remove") is null) throw new InvalidOperationException("NotesEmbeddedObject.Remove surface is missing.");
    if (typeof(NotesMail).GetMethod("SetBodyRichText") is null) throw new InvalidOperationException("NotesMail.SetBodyRichText surface is missing.");
    if (typeof(NotesMail).GetMethod("SetBodyMIME") is null) throw new InvalidOperationException("NotesMail.SetBodyMIME surface is missing.");
    if (typeof(NotesMail).GetMethod("SetBodyHTML") is null) throw new InvalidOperationException("NotesMail.SetBodyHTML surface is missing.");
    if (typeof(NotesMail).GetProperty("Sender") is null || typeof(NotesMail).GetProperty("OnBehalfOf") is null || !typeof(NotesMail).GetMethods().Any(method => method.Name == "SetMIME") || typeof(NotesMail).GetMethod("ClearAttachments") is null) throw new InvalidOperationException("NotesMail sender/attachment compatibility surface is missing.");
    if (typeof(NotesMail).GetMethod("AddAttachment", new[] { typeof(string), typeof(string), typeof(string) }) is null) throw new InvalidOperationException("NotesMail custom attachment metadata surface is missing.");
    if (typeof(NotesMail).GetMethod("SetMIME", new[] { typeof(byte[]), typeof(string) }) is null || typeof(NotesMail).GetMethod("SetMIME", new[] { typeof(NotesStream), typeof(string) }) is null) throw new InvalidOperationException("NotesMail binary MIME overloads are missing.");
    if (typeof(NotesMail).GetMethod("SetMIME", new[] { typeof(string), typeof(string) }) is null || typeof(NotesMail).GetMethod("SetMIMEBody", new[] { typeof(string), typeof(string) }) is null) throw new InvalidOperationException("NotesMail text MIME overloads are missing.");
    if (!typeof(NotesMail).GetMethods().Any(method => method.Name == "AddMimeAttachment")) throw new InvalidOperationException("NotesMail.AddMimeAttachment surface is missing.");
    foreach (var property in new[] { nameof(NotesDatabaseAccess.NamesList), nameof(NotesDatabaseAccess.Groups), nameof(NotesDatabaseAccess.Roles) })
        if (typeof(NotesDatabaseAccess).GetProperty(property)?.PropertyType != typeof(string[])) throw new InvalidOperationException($"NotesDatabaseAccess.{property} must return string[].");
    foreach (var name in new[] { nameof(NotesConst.REPLICA_CANDIDATE), nameof(NotesConst.TEMPLATE_CANDIDATE), nameof(NotesConst.DATABASE), nameof(NotesConst.TEMPLATE) })
        if (typeof(NotesConst).GetField(name)?.FieldType != typeof(int)) throw new InvalidOperationException($"NotesConst.{name} is missing.");
    if (typeof(NotesSession).GetMethod(nameof(NotesSession.SetEnvironmentVar), new[] { typeof(string), typeof(object), typeof(bool) }) is null) throw new InvalidOperationException("NotesSession.SetEnvironmentVar is missing.");
    if (typeof(NotesSession).GetProperty(nameof(NotesSession.ConvertMIME))?.PropertyType != typeof(bool)) throw new InvalidOperationException("NotesSession.ConvertMIME is missing.");
    var getDocumentById = typeof(NotesDatabase).GetMethod(nameof(NotesDatabase.GetDocumentByID), new[] { typeof(uint) }) ?? throw new InvalidOperationException("NotesDatabase.GetDocumentByID(uint) is missing.");
    if (new NullabilityInfoContext().Create(getDocumentById.ReturnParameter).ReadState != NullabilityState.Nullable) throw new InvalidOperationException("NotesDatabase.GetDocumentByID(uint) must be annotated as nullable.");
    var nullableLookups = new[]
    {
        ("GetView", typeof(NotesDatabase).GetMethod(nameof(NotesDatabase.GetView), new[] { typeof(string) })),
        ("GetAgent", typeof(NotesDatabase).GetMethod(nameof(NotesDatabase.GetAgent), new[] { typeof(string) })),
        ("GetDocumentByUNID", typeof(NotesDatabase).GetMethod(nameof(NotesDatabase.GetDocumentByUNID), new[] { typeof(string) })),
        ("OpenView", typeof(NotesDatabase).GetMethod(nameof(NotesDatabase.OpenView), new[] { typeof(string) })),
        ("OpenDocumentByUNID", typeof(NotesDatabase).GetMethod(nameof(NotesDatabase.OpenDocumentByUNID), new[] { typeof(string) })),
        ("OpenDocumentByNoteId", typeof(NotesDatabase).GetMethod(nameof(NotesDatabase.OpenDocumentByNoteId), new[] { typeof(uint) })),
        ("OpenDocumentByNoteIdString", typeof(NotesDatabase).GetMethod(nameof(NotesDatabase.OpenDocumentByNoteId), new[] { typeof(string) })),
        ("GetDocumentByNoteId", typeof(NotesDatabase).GetMethod(nameof(NotesDatabase.GetDocumentByNoteId), new[] { typeof(uint) })),
        ("GetDocumentByNoteIdString", typeof(NotesDatabase).GetMethod(nameof(NotesDatabase.GetDocumentByNoteId), new[] { typeof(string) })),
        ("GetDocumentByIDString", typeof(NotesDatabase).GetMethod(nameof(NotesDatabase.GetDocumentByID), new[] { typeof(string) })),
        ("OpenDocumentByUNID", typeof(NotesDatabase).GetMethod(nameof(NotesDatabase.OpenDocumentByUNID), new[] { typeof(string) })),
        ("GetModifiedDocuments", typeof(NotesDatabase).GetMethod(nameof(NotesDatabase.GetModifiedDocuments), Type.EmptyTypes)),
        ("GetModifiedDocumentsSince", typeof(NotesDatabase).GetMethod(nameof(NotesDatabase.GetModifiedDocuments), new[] { typeof(NotesDateTime) })),
        ("GetModifiedDocumentsSinceClass", typeof(NotesDatabase).GetMethod(nameof(NotesDatabase.GetModifiedDocuments), new[] { typeof(NotesDateTime), typeof(int) })),
        ("NotesSession.GetDatabase", typeof(NotesSession).GetMethod(nameof(NotesSession.GetDatabase), new[] { typeof(string), typeof(string), typeof(bool) }))
    };
    foreach (var (name, method) in nullableLookups)
        if (method is null || new NullabilityInfoContext().Create(method.ReturnParameter).ReadState != NullabilityState.Nullable) throw new InvalidOperationException($"NotesDatabase.{name} must be annotated as nullable.");
    var nullableObjectLookups = new[]
    {
        ("NotesDocument.GetFirstItem", typeof(NotesDocument).GetMethod(nameof(NotesDocument.GetFirstItem), new[] { typeof(string) })),
        ("NotesDocument.GetMIMEEntity", typeof(NotesDocument).GetMethod(nameof(NotesDocument.GetMIMEEntity), new[] { typeof(string) })),
        ("NotesView.GetFirstDocument", typeof(NotesView).GetMethod(nameof(NotesView.GetFirstDocument), Type.EmptyTypes)),
        ("NotesDocumentCollection.GetFirstDocument", typeof(NotesDocumentCollection).GetMethod(nameof(NotesDocumentCollection.GetFirstDocument), Type.EmptyTypes)),
        ("NotesMIMEEntity.GetFirstChildEntity", typeof(NotesMIMEEntity).GetMethod(nameof(NotesMIMEEntity.GetFirstChildEntity), Type.EmptyTypes)),
        ("NotesMIMEEntity.GetNextSibling", typeof(NotesMIMEEntity).GetMethod(nameof(NotesMIMEEntity.GetNextSibling), Type.EmptyTypes)),
        ("NotesMIMEEntity.GetPrevSibling", typeof(NotesMIMEEntity).GetMethod(nameof(NotesMIMEEntity.GetPrevSibling), Type.EmptyTypes)),
        ("NotesMIMEEntity.GetParentEntity", typeof(NotesMIMEEntity).GetMethod(nameof(NotesMIMEEntity.GetParentEntity), Type.EmptyTypes)),
        ("NotesMIMEEntity.GetNextEntity", typeof(NotesMIMEEntity).GetMethod(nameof(NotesMIMEEntity.GetNextEntity), Type.EmptyTypes)),
        ("NotesMIMEEntity.GetNextEntitySearch", typeof(NotesMIMEEntity).GetMethod(nameof(NotesMIMEEntity.GetNextEntity), new[] { typeof(int) })),
        ("NotesView.GetEntryByKey", typeof(NotesView).GetMethod(nameof(NotesView.GetEntryByKey), new[] { typeof(string) })),
        ("NotesDBDirectory.GetFirstDatabase", typeof(NotesDBDirectory).GetMethod(nameof(NotesDBDirectory.GetFirstDatabase), new[] { typeof(int) })),
        ("NotesDBDirectory.GetNextDatabase", typeof(NotesDBDirectory).GetMethod(nameof(NotesDBDirectory.GetNextDatabase), Type.EmptyTypes)),
        ("NotesViewNavigator.GetCurrent", typeof(NotesViewNavigator).GetMethod(nameof(NotesViewNavigator.GetCurrent), Type.EmptyTypes)),
        ("NotesViewNavigator.GetFirst", typeof(NotesViewNavigator).GetMethod(nameof(NotesViewNavigator.GetFirst), Type.EmptyTypes)),
        ("NotesViewNavigator.GetLast", typeof(NotesViewNavigator).GetMethod(nameof(NotesViewNavigator.GetLast), Type.EmptyTypes)),
        ("NotesViewNavigator.GetNext", typeof(NotesViewNavigator).GetMethod(nameof(NotesViewNavigator.GetNext), Type.EmptyTypes)),
        ("NotesViewNavigator.GetPrev", typeof(NotesViewNavigator).GetMethod(nameof(NotesViewNavigator.GetPrev), Type.EmptyTypes)),
        ("NotesViewNavigator.GetFirstDocument", typeof(NotesViewNavigator).GetMethod(nameof(NotesViewNavigator.GetFirstDocument), Type.EmptyTypes)),
        ("NotesViewNavigator.GetLastDocument", typeof(NotesViewNavigator).GetMethod(nameof(NotesViewNavigator.GetLastDocument), Type.EmptyTypes)),
        ("NotesViewNavigator.GetNextDocument", typeof(NotesViewNavigator).GetMethod(nameof(NotesViewNavigator.GetNextDocument), Type.EmptyTypes)),
        ("NotesViewNavigator.GetPrevDocument", typeof(NotesViewNavigator).GetMethod(nameof(NotesViewNavigator.GetPrevDocument), Type.EmptyTypes)),
        ("NotesViewNavigator.GetNextCategory", typeof(NotesViewNavigator).GetMethod(nameof(NotesViewNavigator.GetNextCategory), Type.EmptyTypes)),
        ("NotesViewNavigator.GetPrevCategory", typeof(NotesViewNavigator).GetMethod(nameof(NotesViewNavigator.GetPrevCategory), Type.EmptyTypes)),
        ("NotesViewNavigator.GetFirstEntry", typeof(NotesViewEntryCollection).GetMethod(nameof(NotesViewEntryCollection.GetFirstEntry), Type.EmptyTypes)),
        ("NotesViewEntryCollection.GetLastEntry", typeof(NotesViewEntryCollection).GetMethod(nameof(NotesViewEntryCollection.GetLastEntry), Type.EmptyTypes)),
        ("NotesDocumentCollection.GetLastDocument", typeof(NotesDocumentCollection).GetMethod(nameof(NotesDocumentCollection.GetLastDocument), Type.EmptyTypes)),
        ("NotesDocumentCollection.GetNthDocument", typeof(NotesDocumentCollection).GetMethod(nameof(NotesDocumentCollection.GetNthDocument), new[] { typeof(int) })),
        ("NotesDocumentCollection.GetDocument", typeof(NotesDocumentCollection).GetMethod(nameof(NotesDocumentCollection.GetDocument), new[] { typeof(uint) })),
        ("NotesDocumentCollection.GetPrevDocument", typeof(NotesDocumentCollection).GetMethod(nameof(NotesDocumentCollection.GetPrevDocument), new[] { typeof(NotesDocument) })),
        ("NotesDocumentCollection.GetNextDocument", typeof(NotesDocumentCollection).GetMethod(nameof(NotesDocumentCollection.GetNextDocument), new[] { typeof(NotesDocument) })),
        ("NotesDocumentCollection.GetDocumentByObject", typeof(NotesDocumentCollection).GetMethod(nameof(NotesDocumentCollection.GetDocument), new[] { typeof(NotesDocument) })),
        ("NotesView.GetLastDocument", typeof(NotesView).GetMethod(nameof(NotesView.GetLastDocument), Type.EmptyTypes)),
        ("NotesView.GetNextDocument", typeof(NotesView).GetMethod(nameof(NotesView.GetNextDocument), new[] { typeof(NotesDocument) })),
        ("NotesView.GetPrevDocument", typeof(NotesView).GetMethod(nameof(NotesView.GetPrevDocument), new[] { typeof(NotesDocument) })),
        ("NotesView.GetDocumentByKey", typeof(NotesView).GetMethod(nameof(NotesView.GetDocumentByKey), new[] { typeof(string), typeof(bool) })),
        ("NotesViewEntryCollection.GetNextEntry", typeof(NotesViewEntryCollection).GetMethod(nameof(NotesViewEntryCollection.GetNextEntry), new[] { typeof(NotesViewEntry) })),
        ("NotesViewEntryCollection.GetPrevEntry", typeof(NotesViewEntryCollection).GetMethod(nameof(NotesViewEntryCollection.GetPrevEntry), new[] { typeof(NotesViewEntry) })),
        ("NotesViewEntryCollection.GetNthEntry", typeof(NotesViewEntryCollection).GetMethod(nameof(NotesViewEntryCollection.GetNthEntry), new[] { typeof(int) })),
        ("NotesViewEntryCollection.GetEntry", typeof(NotesViewEntryCollection).GetMethod(nameof(NotesViewEntryCollection.GetEntry), new[] { typeof(NotesDocument) })),
        ("NotesViewNavigator.GetNextEntry", typeof(NotesViewNavigator).GetMethod(nameof(NotesViewNavigator.GetNext), new[] { typeof(NotesViewEntry) })),
        ("NotesViewNavigator.GetPrevEntry", typeof(NotesViewNavigator).GetMethod(nameof(NotesViewNavigator.GetPrev), new[] { typeof(NotesViewEntry) })),
        ("NotesViewNavigator.GetNextDocumentEntry", typeof(NotesViewNavigator).GetMethod(nameof(NotesViewNavigator.GetNextDocument), new[] { typeof(NotesViewEntry) })),
        ("NotesViewNavigator.GetPrevDocumentEntry", typeof(NotesViewNavigator).GetMethod(nameof(NotesViewNavigator.GetPrevDocument), new[] { typeof(NotesViewEntry) })),
        ("NotesViewNavigator.GetNextCategoryEntry", typeof(NotesViewNavigator).GetMethod(nameof(NotesViewNavigator.GetNextCategory), new[] { typeof(NotesViewEntry) })),
        ("NotesViewNavigator.GetPrevCategoryEntry", typeof(NotesViewNavigator).GetMethod(nameof(NotesViewNavigator.GetPrevCategory), new[] { typeof(NotesViewEntry) })),
        ("NotesViewNavigator.GetEntry", typeof(NotesViewNavigator).GetMethod(nameof(NotesViewNavigator.GetEntry), new[] { typeof(NotesDocument) })),
        ("NotesViewNavigator.GetChild", typeof(NotesViewNavigator).GetMethod(nameof(NotesViewNavigator.GetChild), Type.EmptyTypes)),
        ("NotesViewNavigator.GetChildEntry", typeof(NotesViewNavigator).GetMethod(nameof(NotesViewNavigator.GetChild), new[] { typeof(NotesViewEntry) })),
        ("NotesViewNavigator.GetParent", typeof(NotesViewNavigator).GetMethod(nameof(NotesViewNavigator.GetParent), Type.EmptyTypes)),
        ("NotesViewNavigator.GetParentEntry", typeof(NotesViewNavigator).GetMethod(nameof(NotesViewNavigator.GetParent), new[] { typeof(NotesViewEntry) })),
        ("NotesViewNavigator.GetNextSibling", typeof(NotesViewNavigator).GetMethod(nameof(NotesViewNavigator.GetNextSibling), Type.EmptyTypes)),
        ("NotesViewNavigator.GetPrevSibling", typeof(NotesViewNavigator).GetMethod(nameof(NotesViewNavigator.GetPrevSibling), Type.EmptyTypes)),
        ("NotesViewNavigator.GetNextSiblingEntry", typeof(NotesViewNavigator).GetMethod(nameof(NotesViewNavigator.GetNextSibling), new[] { typeof(NotesViewEntry) })),
        ("NotesViewNavigator.GetPrevSiblingEntry", typeof(NotesViewNavigator).GetMethod(nameof(NotesViewNavigator.GetPrevSibling), new[] { typeof(NotesViewEntry) })),
        ("NotesViewNavigator.GetNth", typeof(NotesViewNavigator).GetMethod(nameof(NotesViewNavigator.GetNth), new[] { typeof(int) })),
        ("NotesViewNavigator.GetPos", typeof(NotesViewNavigator).GetMethod(nameof(NotesViewNavigator.GetPos), new[] { typeof(string), typeof(string) }))
    };
    foreach (var (name, method) in nullableObjectLookups)
        if (method is null || new NullabilityInfoContext().Create(method.ReturnParameter).ReadState != NullabilityState.Nullable) throw new InvalidOperationException($"{name} must be annotated as nullable.");
    var nullableProperties = new[]
    {
        ("NotesViewEntry.Document", typeof(NotesViewEntry).GetProperty(nameof(NotesViewEntry.Document))),
        ("NotesDocumentCollection.FirstNoteId", typeof(NotesDocumentCollection).GetProperty(nameof(NotesDocumentCollection.FirstNoteId)))
    };
    foreach (var (name, property) in nullableProperties)
        if (property is null || new NullabilityInfoContext().Create(property).ReadState != NullabilityState.Nullable) throw new InvalidOperationException($"{name} must be annotated as nullable.");
    if (typeof(NotesAgentResult).GetProperty(nameof(NotesAgentResult.Success))?.PropertyType != typeof(bool) || typeof(NotesAgentResult).GetProperty(nameof(NotesAgentResult.Status))?.PropertyType != typeof(int)) throw new InvalidOperationException("NotesAgentResult.Success/Status are missing.");
    if (typeof(NotesMIMEHeader).GetMethod(nameof(NotesMIMEHeader.SetParamVal), new[] { typeof(string), typeof(string) }) is null) throw new InvalidOperationException("NotesMIMEHeader.SetParamVal is missing.");
    if (typeof(NotesMIMEEntity).GetMethod(nameof(NotesMIMEEntity.GetChildren), Type.EmptyTypes) is null) throw new InvalidOperationException("NotesMIMEEntity.GetChildren is missing.");
    if (typeof(NotesRichTextNavigator).GetMethod(nameof(NotesRichTextNavigator.SetPositionAtEnd), new[] { typeof(object) }) is null) throw new InvalidOperationException("NotesRichTextNavigator.SetPositionAtEnd is missing.");
    if (typeof(NotesRichTextRange).GetMethod(nameof(NotesRichTextRange.SetStyle), new[] { typeof(NotesRichTextStyle) }) is null) throw new InvalidOperationException("NotesRichTextRange.SetStyle is missing.");
    foreach (var property in new[] { "RowLabels", "Color", "AlternateColor" })
        if (typeof(NotesRichTextTable).GetProperty(property) is null) throw new InvalidOperationException($"NotesRichTextTable.{property} is missing from the public API.");
});
Check("public API surface", () =>
{
    var types = typeof(NotesSession).Assembly.GetExportedTypes().Where(t => t.Namespace == "DotNetToDomino").ToArray();
    foreach (var type in types.Where(t => !t.IsAbstract && typeof(NotesObject).IsAssignableFrom(t)))
    {
        if (!typeof(IDisposable).IsAssignableFrom(type) || type.GetMethod(nameof(NotesObject.Recycle)) is null || type.GetProperty(nameof(NotesObject.IsRecycled))?.PropertyType != typeof(bool))
            throw new InvalidOperationException($"Notes object {type.Name} does not expose the required Recycle/IsRecycled/IDisposable lifecycle contract.");
    }
    if (types.Length == 0) throw new InvalidOperationException("No exported types found.");
    var richTextTransform = typeof(NotesSession).Assembly.GetType("DotNetToDomino.Interop.NotesRichTextCdTransform", throwOnError: false)
        ?? throw new InvalidOperationException("Rich-text CD transform helper is missing.");
    foreach (var methodName in new[] { "Preserve", "Transform", "GroupCanonicalSegments", "ValidateForPersistence" })
        if (richTextTransform.GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static) is null)
            throw new InvalidOperationException($"Rich-text transform method {methodName} is missing.");
    var nativeApi = typeof(NotesSession).Assembly.GetType("DotNetToDomino.Interop.NotesNativeApi", throwOnError: false)
        ?? throw new InvalidOperationException("Native API type is missing.");
    if (nativeApi.GetMethod("ReplaceRichTextCdRecords", BindingFlags.NonPublic | BindingFlags.Instance) is null)
        throw new InvalidOperationException("Native rich-text rewrite entry point is missing.");
    var mimeOwner = typeof(NotesSession).Assembly.GetType("DotNetToDomino.NotesMimeDirectoryOwner", throwOnError: false)
        ?? throw new InvalidOperationException("MIME directory owner is missing.");
    if (!typeof(IDisposable).IsAssignableFrom(mimeOwner) || mimeOwner.GetMethod(nameof(IDisposable.Dispose)) is null)
        throw new InvalidOperationException("MIME directory owner must implement IDisposable.");
    foreach (var type in types)
    {
        if (type.FullName!.Contains("XPScript", StringComparison.OrdinalIgnoreCase) || type.FullName.Contains("LSArray", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Forbidden reference type leaked into public API: {type.FullName}.");
        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
        {
            if (property.GetMethod is null && property.SetMethod is null) throw new InvalidOperationException($"{type.Name}.{property.Name} has no accessor.");
            EnsureCSharpType(property.PropertyType, $"{type.Name}.{property.Name}");
        }
        foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static).Where(m => !m.IsSpecialName))
        {
            if (method.ContainsGenericParameters) throw new InvalidOperationException($"{type.Name}.{method.Name} has open generic parameters.");
            EnsureCSharpType(method.ReturnType, $"{type.Name}.{method.Name} return type");
            foreach (var parameter in method.GetParameters()) EnsureCSharpType(parameter.ParameterType, $"{type.Name}.{method.Name} parameter {parameter.Name}");
        }
    }
    Console.WriteLine($"  Audited {types.Length} exported types.");
});

void EnsureCSharpType(Type type, string member)
{
    if (type.FullName?.Contains("XPScript", StringComparison.OrdinalIgnoreCase) == true || type.FullName?.Contains("LSArray", StringComparison.OrdinalIgnoreCase) == true)
        throw new InvalidOperationException($"Forbidden XPScript type leaked through {member}: {type.FullName}.");
    foreach (var argument in type.IsArray ? new[] { type.GetElementType()! } : type.IsGenericType ? type.GetGenericArguments() : Array.Empty<Type>()) EnsureCSharpType(argument, member);
}

var runtime = Environment.GetEnvironmentVariable("NOTES_RUNTIME");
if (string.IsNullOrWhiteSpace(runtime)) failures.Add("NOTES_RUNTIME is not set; native full tests cannot run.");
else
{
    var server = Environment.GetEnvironmentVariable("NOTES_TEST_SERVER") ?? string.Empty;
    var databasePath = Environment.GetEnvironmentVariable("NOTES_TEST_DATABASE");
    using var session = new NotesSession(runtime,
        Environment.GetEnvironmentVariable("NOTES_TEST_INI"),
        Environment.GetEnvironmentVariable("NOTES_TEST_ID_PASSWORD"));
    // Run the previously crashing ACL path before broader object tests.
    if (!string.IsNullOrWhiteSpace(databasePath)) Check("ACL native regression", () =>
    {
        using var database = session.GetDatabase(server, databasePath, false) ?? throw new InvalidOperationException("Database not found.");
        using var access = database.QueryAccess();
        if (access.NamesList.Length == 0 || access.Level < 0 || access.Level > 6) throw new InvalidOperationException("Invalid ACL result.");
    });
    if (!string.IsNullOrWhiteSpace(databasePath) && (Environment.GetEnvironmentVariable("NOTES_TEST_FILTER")?.Contains("inline image formats native", StringComparison.OrdinalIgnoreCase) ?? false)) Check("inline image formats native", () =>
    {
        using var database = session.OpenDatabase(server, databasePath);
        var cases = new[] {
            ("png", "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=", new ushort[] { 153, 125, 124 }),
            ("gif", "R0lGODlhAQABAIAAAAAAAP///ywAAAAAAQABAAACAUwAOw==", new ushort[] { 153, 125, 124 })
        };
        foreach (var test in cases)
        {
            using var document = database.CreateDocument();
            var path = Path.Combine(Path.GetTempPath(), "dotnet-domino-inline." + test.Item1);
            File.WriteAllBytes(path, Convert.FromBase64String(test.Item2));
            try
            {
                Console.WriteLine($"  Creating {test.Item1} inline image");
                using var body = document.CreateRichTextItem("Body"); body.EmbedImageInline(path); document.Save();
                using var reopened = database.GetDocumentByID(document.NoteId)!; using var item = reopened.GetFirstItem("Body")!; using var rich = item.GetRichTextItem()!;
                var records = ((System.Collections.IEnumerable)typeof(NotesRichTextItem).GetMethod("ReadRichTextRecords", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(rich, null)!).Cast<object>();
                var signatures = records.Select(r => (ushort)r.GetType().GetProperty("Signature", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.GetValue(r)!).ToArray();
                Console.WriteLine($"  {test.Item1}: {string.Join(",", signatures.Select(s => $"0x{s:X4}"))}");
                if (!test.Item3.All(signatures.Contains)) throw new InvalidOperationException($"Missing expected {test.Item1} inline image record.");
            }
            finally { try { if (!document.IsNewNote) document.Remove(true); } finally { File.Delete(path); } }
        }
    });
    if (!string.IsNullOrWhiteSpace(databasePath) && (Environment.GetEnvironmentVariable("NOTES_TEST_FILTER")?.Contains("inline image extraction native", StringComparison.OrdinalIgnoreCase) ?? false)) Check("inline image extraction native", () =>
    {
        using var database = session.OpenDatabase(server, databasePath); var output = Path.Combine(Path.GetTempPath(), "dotnet-domino-inline-export"); Directory.CreateDirectory(output); var exported = 0;
        try
        {
            var collection = database.AllDocuments;
            for (var i = 1; i <= collection.Count; i++) using (var document = collection.GetNthDocument(i))
            {
                var subject = document?.GetFirstItem("Subject")?.Text?.Trim();
                if (document is null || (subject?.Contains("InlineImage", StringComparison.OrdinalIgnoreCase) != true && subject?.Equals("RT-Image-Multiple", StringComparison.OrdinalIgnoreCase) != true)) continue;
                using var body = document.GetFirstItem("Body") as NotesRichTextItem ?? throw new InvalidOperationException("Inline image document has no rich text Body.");
                var paths = body.ExtractInlineImages(output); if (body.InlineImageCount != paths.Count) throw new InvalidOperationException($"InlineImageCount={body.InlineImageCount} but exported {paths.Count}."); exported += paths.Count; foreach (var path in paths) { if (new FileInfo(path).Length == 0) throw new InvalidOperationException("Extracted inline image was empty."); Console.WriteLine($"  Exported {document.GetFirstItem("Subject")?.Text}: {Path.GetExtension(path)}"); if (string.Equals(Path.GetExtension(path), ".bin", StringComparison.OrdinalIgnoreCase)) continue; using var roundTrip = database.CreateDocument(); using var rt = roundTrip.CreateRichTextItem("Body"); rt.EmbedImageInline(path); roundTrip.Save(); if (roundTrip.NoteId == 0) throw new InvalidOperationException("Round-trip inline image note was not saved."); roundTrip.Remove(true); }
                if (subject.Equals("RT-Image-Multiple", StringComparison.OrdinalIgnoreCase) && body.InlineImageCount != 4) throw new InvalidOperationException($"RT-Image-Multiple expected 4 inline images, found {body.InlineImageCount}.");
            }
            Console.WriteLine($"  Exported {exported} inline images individually."); if (exported < 4) throw new InvalidOperationException("Expected PNG, JPG, BMP and GIF inline images.");
        }
        finally { if (Directory.Exists(output)) Directory.Delete(output, true); }
    });
    if (!string.IsNullOrWhiteSpace(databasePath) && (Environment.GetEnvironmentVariable("NOTES_TEST_FILTER")?.Contains("document attachments native", StringComparison.OrdinalIgnoreCase) ?? false)) Check("document attachments native", () =>
    {
        using var database = session.OpenDatabase(server, databasePath); var collection = database.AllDocuments;
        var foundAttachment = false; var foundDuplicateNames = false;
        for (var i = 1; i <= collection.Count; i++) using (var document = collection.GetNthDocument(i))
        {
            var subject = document?.GetFirstItem("Subject")?.Text?.Trim();
            if (document is null || (subject != "RT-Attachment" && subject != "RT-Attachments-DuplicateNames")) continue;
            foundAttachment = true;
            var attachments = document.Attachments; if (attachments.Count == 0 || document.AttachmentCount != attachments.Count) throw new InvalidOperationException("Document attachment list was empty or inconsistent.");
            if (subject == "RT-Attachments-DuplicateNames") { foundDuplicateNames = true; if (attachments.Count < 2) throw new InvalidOperationException("Duplicate-name attachment document did not expose both attachments."); }
            foreach (var attachment in attachments) using (attachment) { var a = attachment.ToByteArray(); var b = attachment.Bytes; if (string.IsNullOrWhiteSpace(attachment.Name) || attachment.FileSize <= 0 || a.Length == 0 || !a.AsSpan().SequenceEqual(b)) throw new InvalidOperationException("Attachment metadata or byte-array access was invalid."); using var byName = document.GetAttachment(attachment.Name) ?? throw new InvalidOperationException("GetAttachment(name) returned null."); var c = document.GetAttachmentBytes(attachment.Name); if (!c.AsSpan().SequenceEqual(a)) throw new InvalidOperationException("GetAttachmentBytes(name) differed."); Console.WriteLine($"  Attachment {attachment.Name}: {attachment.FileSize} bytes, source={attachment.Source}"); }
        }
        if (!foundAttachment) throw new InvalidOperationException("RT-Attachment document was not found.");
        if (!foundDuplicateNames) throw new InvalidOperationException("RT-Attachments-DuplicateNames document was not found.");
    });
    if (!string.IsNullOrWhiteSpace(databasePath) && (Environment.GetEnvironmentVariable("NOTES_TEST_FILTER")?.Contains("rich text collections native", StringComparison.OrdinalIgnoreCase) ?? false)) Check("rich text collections native", () =>
    {
        using var database = session.OpenDatabase(server, databasePath); var collection = database.AllDocuments;
        var tables = 0; var links = 0; var images = 0; var attachments = 0; var hotspots = 0; var nonEmptyHotspotText = 0; var databaseLinks = 0; var viewLinks = 0; var documentLinks = 0;
        for (var i = 1; i <= collection.Count; i++) using (var document = collection.GetNthDocument(i))
        {
            if (document is null) continue;
            using var body = document.GetFirstItem("Body") as NotesRichTextItem;
            if (body is null) continue;
            tables += body.TableCount; links += body.LinkCount; images += body.InlineImageCount; attachments += body.AttachmentCount; hotspots += body.HotspotCount;
            using (var hotspotNavigator = body.CreateNavigator())
            {
                var navigatedHotspots = 0;
                for (var found = hotspotNavigator.FindFirstElement(NotesRichTextNavigator.ElementHotspot); found; found = hotspotNavigator.FindNextElement(NotesRichTextNavigator.ElementHotspot))
                {
                    if (hotspotNavigator.GetElement() is not NotesRichTextHotspot hotspot) throw new InvalidOperationException("Hotspot navigator did not return a hotspot object.");
                    _ = hotspot.HotspotType; _ = hotspot.Flags; if (hotspot.Text.Length > 0) nonEmptyHotspotText++; navigatedHotspots++;
                }
                var nonFileHotspots = body.Hotspots.Count(hotspot => !hotspot.IsFileHotspot);
                if (navigatedHotspots != nonFileHotspots) throw new InvalidOperationException("Hotspot navigator count did not match non-file hotspot collection.");
            }
            using (var imageNavigator = body.CreateNavigator())
            {
                var navigatedImages = 0;
                for (var found = imageNavigator.FindFirstElement(NotesRichTextNavigator.ElementInlineImage); found; found = imageNavigator.FindNextElement(NotesRichTextNavigator.ElementInlineImage))
                {
                    var image = imageNavigator.GetInlineImage();
                    if (image.Length == 0) throw new InvalidOperationException("Navigator returned an empty inline image.");
                    navigatedImages++;
                }
                if (navigatedImages != body.InlineImageCount) throw new InvalidOperationException("Inline-image navigator count did not match collection.");
            }
            using (var attachmentNavigator = body.CreateNavigator())
            {
                var navigatedAttachments = 0;
                for (var found = attachmentNavigator.FindFirstElement(NotesRichTextNavigator.ElementEmbeddedObject); found; found = attachmentNavigator.FindNextElement(NotesRichTextNavigator.ElementEmbeddedObject))
                {
                    if (attachmentNavigator.GetElement() is not NotesEmbeddedObject attachment) throw new InvalidOperationException("Embedded-object navigator did not return an attachment.");
                    if (attachment.FileSize < 0 || attachment.Name.Length == 0) throw new InvalidOperationException("Embedded-object metadata was invalid.");
                    if (attachment.ToByteArray().LongLength != attachment.FileSize) throw new InvalidOperationException("Embedded-object byte-array length did not match FileSize.");
                    navigatedAttachments++;
                }
                if (navigatedAttachments != body.AttachmentCount) throw new InvalidOperationException("Embedded-object navigator count did not match collection.");
            }
            if (body.HotspotCount > 0 && body.GetHotspot(0).HotspotType < 0) throw new InvalidOperationException("Indexed hotspot access returned invalid metadata.");
            if (body.AttachmentCount > 0 && body.GetEmbeddedObject(0).Name.Length == 0) throw new InvalidOperationException("Indexed embedded-object access returned no name.");
            using (var cellNavigator = body.CreateNavigator())
            {
                var expectedCells = body.Tables.Sum(table => table.Cells.Count);
                var navigatedCells = 0;
                for (var found = cellNavigator.FindFirstElement(NotesRichTextNavigator.ElementTableCell); found; found = cellNavigator.FindNextElement(NotesRichTextNavigator.ElementTableCell))
                {
                    if (cellNavigator.GetElement() is not NotesRichTextTableCell cell) throw new InvalidOperationException("Table-cell navigator did not return a cell.");
                    _ = cell.Row; _ = cell.Column; navigatedCells++;
                }
                if (navigatedCells != expectedCells) throw new InvalidOperationException("Table-cell navigator count did not match table collections.");
            }
            foreach (var link in body.Links) { if (link.IsDatabaseLink) databaseLinks++; else if (link.IsViewLink) viewLinks++; else if (link.IsDocumentLink) documentLinks++; }
            if (body.Tables.Count != body.TableCount || body.Links.Count != body.LinkCount) throw new InvalidOperationException("Rich-text collection count did not match enumeration."); foreach (var table in body.Tables) { _ = table.RowHeights; foreach (var cell in table.Cells) { _ = cell.Text; using var cellRange = cell.CreateRange(); _ = cellRange.TextParagraph; } }
            if (document.GetFirstItem("Subject")?.Text == "RT Lists" && body.ParagraphCount < 20) throw new InvalidOperationException("RT Lists paragraph enumeration was unexpectedly short."); if (document.GetFirstItem("Subject")?.Text == "RT Lists" && (body.Paragraphs.Count != body.ParagraphCount || !body.Paragraphs.Any(paragraph => paragraph.Text == "Bullet 1"))) throw new InvalidOperationException("Typed paragraph enumeration did not expose RT Lists content."); if (document.GetFirstItem("Subject")?.Text == "RT Lists") { using var listNavigator = body.CreateNavigator(); var foundParagraphRange = false; for (var hasParagraph = listNavigator.FindFirstElement(NotesRichTextNavigator.ElementTextParagraph); hasParagraph; hasParagraph = listNavigator.FindNextElement(NotesRichTextNavigator.ElementTextParagraph)) { using var listRange = listNavigator.GetElement() as NotesRichTextRange ?? throw new InvalidOperationException("Paragraph navigator did not return a range."); _ = listRange.ParagraphStyleId; if (listRange.TextParagraph.Contains("Bullet 1", StringComparison.Ordinal)) { foundParagraphRange = true; break; } } if (!foundParagraphRange) throw new InvalidOperationException("Paragraph range text was not readable."); }
            if (document.GetFirstItem("Subject")?.Text == "RT-Tabbed Table" && (!body.HasTabbedTable || body.TabbedTableRecordCount == 0)) throw new InvalidOperationException("Tabbed-table records were not detected."); if (document.GetFirstItem("Subject")?.Text == "RT-Tabbed Table" && !body.Tables.Any(table => table.IsTabbedTable && table.TabbedTableRecordCount > 0)) throw new InvalidOperationException("Tabbed-table metadata was not exposed on the table."); if (document.GetFirstItem("Subject")?.Text == "RT-Table-RowHeights") { var rowHeights = body.Tables.SelectMany(table => table.RowHeights).ToArray(); Console.WriteLine($"  RowHeights={string.Join(',', rowHeights)}"); }
        }
        Console.WriteLine($"  Tables={tables}, Links={links} (database={databaseLinks}, view={viewLinks}, document={documentLinks}), Hotspots={hotspots} (text={nonEmptyHotspotText}), InlineImages={images}, Attachments={attachments}");
        if (nonEmptyHotspotText == 0) throw new InvalidOperationException("Hotspot text extraction returned no visible text.");
        if (tables == 0 || links == 0 || images == 0 || attachments == 0) throw new InvalidOperationException("Expected all rich-text element categories in regression database.");
    });
    if (!string.IsNullOrWhiteSpace(databasePath)) Check("rich text append page break and horizontal rule native", () =>
    {
        using var database = session.OpenDatabase(server, databasePath);
        using var document = database.CreateDocument();
        try
        {
            using var body = document.CreateRichTextItem("Body");
            body.AppendText("before");
            body.AppendNewLine();
            body.AppendText("newline-after");
            body.AppendPageBreak();
            body.AppendText("after");
            body.AppendHorizontalRule();
            body.AppendText("end");
            document.Save();
            using var reopened = database.GetDocumentByID(document.NoteId)!;
            using var item = reopened.GetFirstItem("Body")!;
            using var rich = item.GetRichTextItem()!;
            var records = (System.Collections.IEnumerable)typeof(NotesRichTextItem).GetMethod("ReadRichTextRecords", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(rich, null)!;
            var list = records.Cast<object>().ToArray();
            var textValues = list.Select(r => (string?)r.GetType().GetProperty("Text", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.GetValue(r)).Where(value => value is not null).ToArray();
            Console.WriteLine($"  Text records: {string.Join(" | ", textValues.Select(value => value!.Replace("\r", "<CR>").Replace("\n", "<LF>")))}");
            if (!textValues.Any(value => value!.Contains("newline-after", StringComparison.Ordinal))) throw new InvalidOperationException("AppendNewLine text did not persist.");
            var signatures = list.Select(r => (ushort)r.GetType().GetProperty("Signature", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.GetValue(r)!).ToArray();
            Console.WriteLine($"  Append break/rule signatures: {string.Join(",", signatures.Select(s => $"0x{s:X4}"))}");
            foreach (var record in list.Where(r => (ushort)r.GetType().GetProperty("Signature", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.GetValue(r)! == 0xFF82))
            {
                var bytes = (byte[])record.GetType().GetProperty("Data", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.GetValue(record)!;
                Console.WriteLine($"  PABDEF: {Convert.ToHexString(bytes)}");
            }
            if (!signatures.Contains((ushort)0xFFC9)) throw new InvalidOperationException("AppendHorizontalRule did not create CDHRULE.");
            if (!signatures.Contains((ushort)129)) throw new InvalidOperationException("AppendPageBreak did not create a paragraph record.");
        }
        finally { if (!document.IsNewNote) document.Remove(true); }
    });
    if (!string.IsNullOrWhiteSpace(databasePath)) Check("rich text list type via DXL", () =>
    {
        using var database = session.OpenDatabase(server, databasePath); var collection = database.AllDocuments;
        for (var i = 1; i <= collection.Count; i++) using (var document = collection.GetNthDocument(i))
        {
            if (document?.GetFirstItem("Subject")?.Text?.Trim() != "RT Lists") continue;
            using var body = document.GetFirstItem("Body")?.GetRichTextItem() ?? throw new InvalidOperationException("RT Lists Body missing.");
            using var navigator = body.CreateNavigator();
            while (navigator.FindNextElement(NotesRichTextNavigator.ElementTextParagraph))
            {
                using var range = navigator.GetElement() as NotesRichTextRange ?? throw new InvalidOperationException("List paragraph range missing.");
                var expectedList = range.TextParagraph switch { var text when text.Contains("Bullet 1", StringComparison.Ordinal) => NotesRichTextListType.Bullet, var text when text.Contains("Number 1", StringComparison.Ordinal) => NotesRichTextListType.Number, var text when text.Contains("Check 1", StringComparison.Ordinal) => NotesRichTextListType.Uncheck, var text when text.Contains("Circle 1", StringComparison.Ordinal) => NotesRichTextListType.Circle, var text when text.Contains("Square 1", StringComparison.Ordinal) => NotesRichTextListType.Square, var text when text.Contains("Upper 1", StringComparison.Ordinal) => NotesRichTextListType.AlphaUpper, var text when text.Contains("Lower 1", StringComparison.Ordinal) => NotesRichTextListType.AlphaLower, _ => NotesRichTextListType.None }; if (expectedList != NotesRichTextListType.None && range.ListType != expectedList) throw new InvalidOperationException($"Expected {expectedList}, got {range.ListType}.");
            }
            return;
        }
        throw new InvalidOperationException("RT Lists document not found.");
    });
    if (!string.IsNullOrWhiteSpace(databasePath) && (Environment.GetEnvironmentVariable("NOTES_TEST_FILTER")?.Contains("rich text body dump", StringComparison.OrdinalIgnoreCase) ?? false)) Check("rich text body dump", () =>
    {
        using var database = session.OpenDatabase(server, databasePath);
        var collection = database.AllDocuments;
        var count = 0;
        for (var i = 1; i <= collection.Count; i++)
        {
            using var document = collection.GetNthDocument(i);
            if (document is null) continue;
            var body = document.GetFirstItem("Body") as NotesRichTextItem;
            if (body is null) continue;
            count++;
            using (body)
            {
                var records = ((System.Collections.IEnumerable)typeof(NotesRichTextItem).GetMethod("ReadRichTextRecords", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(body, null)!).Cast<object>().ToArray();
                var signatures = records.Select(r => (ushort)r.GetType().GetProperty("Signature", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.GetValue(r)!).ToArray();
                var subject = document.GetFirstItem("Subject")?.Text ?? "(no subject)";
                var imageRecords = signatures.Count(s => s is 166 or 705 or 706 or 32850);
                Console.WriteLine($"  Body {document.NoteId:X8} [{subject}]: {string.Join(",", signatures.Select(s => $"0x{s:X4}"))}");
                if (imageRecords > 0) Console.WriteLine($"    Image records detected: {imageRecords} (CDGRAPHIC/CDIMAGEHEADER/CDIMAGESEGMENT/CDIMAGEHEADER2)");
                if (subject.Contains("Attachment", StringComparison.OrdinalIgnoreCase) || subject.Contains("InlineImage", StringComparison.OrdinalIgnoreCase))
                {
                    foreach (var record in records)
                    {
                        var sig = (ushort)record.GetType().GetProperty("Signature", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.GetValue(record)!;
                        var data = (byte[])record.GetType().GetProperty("Data", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.GetValue(record)!;
                        Console.WriteLine($"    0x{sig:X4}: {data.Length} bytes");
                        if (subject.Contains("InlineImagePNG", StringComparison.OrdinalIgnoreCase) && sig is 125 or 82) Console.WriteLine($"      {Convert.ToHexString(data)}");
                    }
                }
                if (subject.Equals("RT Lists", StringComparison.OrdinalIgnoreCase))
                {
                    using var dxlDumpExporter = session.CreateDXLExporter(); dxlDumpExporter.OutputDOCTYPE = false; using var dxlDump = dxlDumpExporter.Export(document); Console.WriteLine($"    DXL lists: {System.Text.Encoding.UTF8.GetString(dxlDump.ToArray()).Replace('\r',' ').Replace('\n',' ')}");
                    foreach (var record in records)
                    {
                        var sig = (ushort)record.GetType().GetProperty("Signature", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.GetValue(record)!;
                        if (sig is not (0x0081 or 0xFF85)) continue;
                        var data = (byte[])record.GetType().GetProperty("Data", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.GetValue(record)!;
                        Console.WriteLine($"    RT Lists 0x{sig:X4}: {Convert.ToHexString(data)}");
                    }
                    using var exporter = session.CreateDXLExporter();
                    using var dxl = exporter.Export(document);
                    foreach (var line in dxl.ReadText().Split('\n').Where(line => line.Contains("par", StringComparison.OrdinalIgnoreCase) || line.Contains("list", StringComparison.OrdinalIgnoreCase)))
                        Console.WriteLine($"    DXL: {line.Trim()}");
                }
            }
        }
        Console.WriteLine($"  Inspected {count} Body rich-text documents.");
    });
    if (!string.IsNullOrWhiteSpace(databasePath) && (Environment.GetEnvironmentVariable("NOTES_TEST_FILTER")?.Contains("inline image native", StringComparison.OrdinalIgnoreCase) ?? false)) Check("inline image native", () =>
    {
        using var database = session.OpenDatabase(server, databasePath);
        using var document = database.CreateDocument();
        var path = Path.Combine(Path.GetTempPath(), "dotnet-domino-inline.bmp");
        var bmp = new byte[58]; bmp[0] = (byte)'B'; bmp[1] = (byte)'M'; BitConverter.GetBytes(58).CopyTo(bmp, 2); BitConverter.GetBytes(54).CopyTo(bmp, 10); BitConverter.GetBytes(40).CopyTo(bmp, 14); BitConverter.GetBytes(1).CopyTo(bmp, 18); BitConverter.GetBytes(1).CopyTo(bmp, 22); BitConverter.GetBytes((short)1).CopyTo(bmp, 26); BitConverter.GetBytes((short)24).CopyTo(bmp, 28); BitConverter.GetBytes(4).CopyTo(bmp, 34); File.WriteAllBytes(path, bmp);
        try
        {
            using var body = document.CreateRichTextItem("Body"); body.EmbedImageInline(path); document.Save();
            using var reopened = database.GetDocumentByID(document.NoteId)!; using var item = reopened.GetFirstItem("Body")!; using var rich = item.GetRichTextItem()!;
            var records = ((System.Collections.IEnumerable)typeof(NotesRichTextItem).GetMethod("ReadRichTextRecords", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(rich, null)!).Cast<object>().ToArray();
            var signatures = records.Select(r => (ushort)r.GetType().GetProperty("Signature", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.GetValue(r)!).ToArray();
            Console.WriteLine($"  Generated inline image: {string.Join(",", signatures.Select(s => $"0x{s:X4}"))}");
            if (!signatures.Contains((ushort)153) || !signatures.Contains((ushort)125) || !signatures.Contains((ushort)124)) throw new InvalidOperationException("Notes did not persist the expected inline image records.");
        }
        finally { try { if (!document.IsNewNote) document.Remove(true); } finally { File.Delete(path); } }
    });
    ObjectPropertySmokeTests.RoundTrips(session, server, databasePath, Check);
    ObjectPropertySmokeTests.Run(session, server, databasePath, Check);
    Check("rich text font mappings", () => { using var database = session.OpenDatabase(server, databasePath!); using var document = database.CreateDocument(); using var body = document.CreateRichTextItem("Body"); if (body.GetFont("Arial") != 0 || body.GetFont("Times New Roman") != 1 || body.GetFont("Courier New") != 2) throw new InvalidOperationException("Standard rich-text font mappings are incorrect."); if (body.GetFont("Unknown font") != 0) throw new InvalidOperationException("Unknown rich-text font did not fall back to the default font."); Console.WriteLine("PASS Unknown rich-text font fallback"); });
    Check("session properties", () => { _ = session.UserName; _ = session.CommonUserName; _ = session.Platform; _ = session.NotesVersion; _ = session.NotesBuildVersion; _ = session.DataDir; });
    Check("session factories", () => { using var name = session.CreateName(session.UserName); using var date = session.CreateDateTimeNow(); date.SetNow(); date.ConvertToZone(date.TimeZone); if (date.TimeDifferenceDouble(date) != 0) throw new InvalidOperationException("Date/time self difference was not zero."); using var stream = session.CreateStream(); using var importer = session.CreateDXLImporter(); using var exporter = session.CreateDXLExporter(); using var style = session.CreateRichTextStyle(); using var paragraph = session.CreateRichTextParagraphStyle(); using var mail = session.CreateMail(); ExpectException<ArgumentOutOfRangeException>("Negative stream read", () => stream.Read(-1)); ExpectException<ArgumentOutOfRangeException>("Invalid DXL importer option", () => importer.ACLImportOption = 3); ExpectException<ArgumentOutOfRangeException>("Invalid DXL exporter option", () => exporter.RichTextOption = 2); ExpectException<ArgumentOutOfRangeException>("Invalid rich text effects", () => style.Effects = 6); ExpectException<ArgumentOutOfRangeException>("Invalid rich text font size", () => style.FontSize = 251); ExpectException<ArgumentOutOfRangeException>("Invalid rich text color", () => style.NotesColor = 256); ExpectException<ArgumentOutOfRangeException>("Invalid rich text font", () => style.NotesFont = 256); ExpectException<ArgumentOutOfRangeException>("Invalid paragraph alignment", () => paragraph.Alignment = 5); ExpectException<ArgumentOutOfRangeException>("Invalid paragraph pagination", () => paragraph.Pagination = 3); ExpectException<ArgumentOutOfRangeException>("Negative paragraph margin", () => paragraph.LeftMargin = -1); ExpectException<NotesException>("Mail send without recipients", () => mail.Send()); try { mail.SendTo = null!; throw new InvalidOperationException("Null SendTo was accepted."); } catch (ArgumentNullException ex) when (ex.ParamName == "SendTo") { } try { mail.DeliveryPriority = "X"; throw new InvalidOperationException("Invalid DeliveryPriority was accepted."); } catch (ArgumentOutOfRangeException ex) when (ex.ParamName == "value") { } ExpectException<ArgumentNullException>("Null From", () => mail.From = null!); ExpectException<ArgumentNullException>("Null ReplyTo", () => mail.ReplyTo = null!); ExpectException<ArgumentNullException>("Null Principal", () => mail.Principal = null!); ExpectException<ArgumentNullException>("Null Subject", () => mail.Subject = null!); ExpectException<ArgumentNullException>("Null DeliveryPriority", () => mail.DeliveryPriority = null!); ExpectException<ArgumentNullException>("Null DeliveryReport", () => mail.DeliveryReport = null!); });
    var liveMailTo = Environment.GetEnvironmentVariable("NOTES_TEST_MAIL_TO");
    if (string.Equals(Environment.GetEnvironmentVariable("NOTES_TEST_MAIL_LIVE"), "1", StringComparison.Ordinal) && !string.IsNullOrWhiteSpace(liveMailTo))
        Check("live mail delivery matrix", () =>
        {
            foreach (var priority in new[] { "H", "N", "L" })
            foreach (var report in new[] { "N", "B", "C" })
            {
                using var mail = session.CreateMail();
                mail.SendTo = new[] { liveMailTo.Trim() };
                mail.Subject = $"DotNetToDomino live mail test {priority}/{report}";
                mail.SetBodyText($"Live NotesMail delivery test for priority {priority} and report {report}.");
                mail.DeliveryPriority = priority;
                mail.DeliveryReport = report;
                mail.ReturnReceipt = report == "C";
                mail.Send();
            }
        });
    var testAgentName = Environment.GetEnvironmentVariable("NOTES_TEST_AGENT");
    if (!string.IsNullOrWhiteSpace(databasePath) && !string.IsNullOrWhiteSpace(testAgentName))
        Check("agent output", () =>
        {
            using var database = session.GetDatabase(server, databasePath, false) ?? throw new InvalidOperationException("Database not found.");
            using var result = database.RunAgent(testAgentName.Trim());
            if (!result.Success || result.Status != 0 || !string.Equals(result.Output.Trim(), "STATUS OK", StringComparison.Ordinal))
                throw new InvalidOperationException($"Unexpected agent result: Success={result.Success}, Status={result.Status}, Output='{result.Output}'.");
        });
    Check("password round trip", () => { const string password = "DotNetToDomino-FullTest"; var digest = session.HashPassword(password); if (!session.VerifyPassword(password, digest)) throw new InvalidOperationException("Password verification failed."); });
    Check("address book and directory", () => { foreach (var book in session.AddressBooks) using (book) { _ = book.Server; _ = book.FilePath; } using var directory = session.GetDbDirectory(server); _ = directory.Name; using var database = directory.GetFirstDatabase(NotesConst.DATABASE); if (database is not null) { _ = database.FilePath; _ = database.FileName; } using var next = directory.GetNextDatabase(); if (next is not null) { _ = next.FilePath; _ = next.FileName; } });
    if (!string.IsNullOrWhiteSpace(databasePath)) Check("rich text range surface", () =>
    {
        using var database = session.GetDatabase(server, databasePath, false) ?? throw new InvalidOperationException("Database not found.");
        ExpectException<ArgumentNullException>("Database Title null", () => database.Title = null!);
        ExpectException<ArgumentNullException>("Database Categories null", () => database.Categories = null!);
        ExpectException<ArgumentNullException>("Database TemplateName null", () => database.SetTemplateName(null!));
        ExpectException<ArgumentNullException>("Database DesignTemplateName null", () => database.SetDesignTemplateName(null!));
        ExpectException<ArgumentNullException>("Database ReplicaID null", () => database.SetReplicaId(null!));
        using var document = database.CreateDocument();
        using var richText = document.CreateRichTextItem("Body");
        richText.AppendText("Rich text full test");
        richText.AppendTable(2, 2);
        using (var tableNavigator = richText.CreateNavigator())
        {
            if (!tableNavigator.FindFirstElement(NotesRichTextNavigator.ElementTable)) throw new InvalidOperationException("Appended rich-text table was not found.");
            using var table = tableNavigator.GetElement() as NotesRichTextTable ?? throw new InvalidOperationException("Table navigator returned an unexpected element type.");
            if (table.RowCount != 2 || table.ColumnCount != 2) throw new InvalidOperationException($"Appended rich-text table dimensions were incorrect: {table.RowCount}x{table.ColumnCount}.");
            using var cell = table.GetCell(0, 1) ?? throw new InvalidOperationException("Appended rich-text table cell was not found.");
            if (cell.Row != 0 || cell.Column != 1 || cell.RowSpan != 1 || cell.ColumnSpan != 1 || cell.LeftMargin < 0 || cell.RightMargin < 0 || cell.FractionalWidth < 0 || cell.Border < 0 || cell.Flags != 2 || !cell.UsesV42Borders || cell.V42Border < 0 || cell.BackgroundColor < 0) throw new InvalidOperationException("Rich-text table cell coordinates or metadata were incorrect.");
            if (table.Cells.Count != 4) throw new InvalidOperationException($"Appended rich-text table cell collection was incorrect: {table.Cells.Count}.");
        }
        using var paragraphStyle = session.CreateRichTextParagraphStyle();
        paragraphStyle.Alignment = 1;
        paragraphStyle.LeftMargin = 120;
        richText.AppendParagraphStyle(paragraphStyle);
        ExpectException<ArgumentNullException>("Rich text attachment path null", () => richText.AttachFile(null!));
        var sourcePath = Path.Combine(Path.GetTempPath(), "DotNetToDomino-FullTest-" + Guid.NewGuid().ToString("N") + ".bin");
        var extractedPath = Path.Combine(Path.GetTempPath(), "DotNetToDomino-FullTest-Extracted-" + Guid.NewGuid().ToString("N") + ".bin");
        try
        {
            var sourceBytes = new byte[] { 0, 1, 2, 127, 128, 254, 255 };
            File.WriteAllBytes(sourcePath, sourceBytes);
            using var embedded = richText.AttachFile(sourcePath);
            if (!sourceBytes.SequenceEqual(embedded.ToByteArray())) throw new InvalidOperationException("Embedded attachment bytes did not round-trip.");
            if (!richText.SaveAttachment(Path.GetFileName(sourcePath), extractedPath) || !sourceBytes.SequenceEqual(File.ReadAllBytes(extractedPath))) throw new InvalidOperationException("Rich-text attachment extraction failed.");
            embedded.Remove();
            ExpectException<ObjectDisposedException>("Removed embedded object", () => embedded.ToByteArray());
        }
        finally
        {
            if (File.Exists(sourcePath)) File.Delete(sourcePath);
            if (File.Exists(extractedPath)) File.Delete(extractedPath);
        }
        using var navigator = richText.CreateNavigator();
        if (navigator.FindFirstElement(NotesRichTextNavigator.ElementTextRun))
        {
            using var range = richText.CreateRange();
            range.SetBegin(navigator);
            range.SetEnd(navigator);
            using var style = session.CreateRichTextStyle();
            style.Bold = -1;
            range.SetStyle(style);
        }
    });
    if (!string.IsNullOrWhiteSpace(databasePath)) Check("rich text AppendRTItem native", () =>
    {
        using var database = session.OpenDatabase(server, databasePath); using var document = database.CreateDocument();
        try
        {
            using var source = document.CreateRichTextItem("Source"); source.AppendText("SOURCE-Åäö"); source.AppendTable(2, 2);
            using var destination = document.CreateRichTextItem("Destination"); destination.AppendText("BEFORE-"); destination.AppendRTItem(source); destination.AppendText("-AFTER");
            document.Save();
            using var reopened = database.GetDocumentByID(document.NoteId)!; using var exporter = session.CreateDXLExporter(); exporter.OutputDOCTYPE = false; exporter.RichTextOption = 0; using var dxl = exporter.Export(reopened); using var bytes = new MemoryStream(dxl.ToArray());
            var richTexts = System.Xml.Linq.XDocument.Load(bytes).Descendants().Where(e => e.Name.LocalName == "richtext").ToArray();
            var destinationXml = richTexts.First(e => e.Parent?.Attribute("name")?.Value == "Destination");
            if (!destinationXml.Value.Contains("BEFORE-", StringComparison.Ordinal) || !destinationXml.Value.Contains("SOURCE-Åäö", StringComparison.Ordinal) || !destinationXml.Value.Contains("-AFTER", StringComparison.Ordinal) || destinationXml.Descendants().Count(e => e.Name.LocalName == "tablecell") != 4) throw new InvalidOperationException("AppendRTItem lost source rich text content.");
        }
        finally { if (!document.IsNewNote) document.Remove(true); }
    });
    if (!string.IsNullOrWhiteSpace(databasePath)) Check("rich text table persistence and native DXL", () =>
    {
        using var database = session.OpenDatabase(server, databasePath);
        using (var invalidDocument = database.CreateDocument())
        using (var invalidBody = invalidDocument.CreateRichTextItem("Body"))
        {
            try { invalidBody.AppendTable(0, 1); throw new InvalidOperationException("AppendTable accepted zero rows."); }
            catch (ArgumentOutOfRangeException) { }
            try { invalidBody.AppendTable(1, 256); throw new InvalidOperationException("AppendTable accepted 256 columns."); }
            catch (ArgumentOutOfRangeException) { }
        }
        foreach (var (rows, columns) in new[] { (1, 1), (1, 2), (2, 2), (3, 4), (1, 64), (2, 64), (255, 1) })
        {
            using var document = database.CreateDocument();
            try
            {
                using (var body = document.CreateRichTextItem("Body"))
                {
                    body.AppendText("Before table");
                    body.AppendTable(rows, columns);
                    body.AppendText("After table");
                }
                document.Save();
                using var reopened = database.GetDocumentByID(document.NoteId) ?? throw new InvalidOperationException("Saved table document missing.");
                using var item = reopened.GetFirstItem("Body") ?? throw new InvalidOperationException("Saved Body missing.");
                using var richText = item.GetRichTextItem() ?? throw new InvalidOperationException("Saved Body is not rich text.");
                using var nav = richText.CreateNavigator();
                if (!nav.FindFirstElement(NotesRichTextNavigator.ElementTable)) throw new InvalidOperationException("Saved table missing.");
                using var table = nav.GetElement() as NotesRichTextTable ?? throw new InvalidOperationException("Unexpected saved table type.");
                if (table.RowCount != rows || table.ColumnCount != columns || table.Cells.Count != rows * columns)
                    throw new InvalidOperationException($"Saved table does not match {rows}x{columns}.");
                if ((table.PreTableRowCount != 0 && table.PreTableRowCount != rows) || (table.PreTableColumnCount != 0 && table.PreTableColumnCount != columns))
                    throw new InvalidOperationException("CDPRETABLEBEGIN dimensions disagree with the table cells.");
                if (table.PreTableColumnSizingBits1 < 0 || table.PreTableColumnSizingBits2 < 0 || table.PreTableViewerType < 0 || table.PreTableMinRowHeight < 0)
                    throw new InvalidOperationException("CDPRETABLEBEGIN metadata returned an invalid negative value.");
                using var lastCell = table.GetCell(rows - 1, columns - 1) ?? throw new InvalidOperationException("Last saved cell missing.");
                if (lastCell.Row != rows - 1 || lastCell.Column != columns - 1 ||
                    lastCell.LeftMargin != 120 + (columns - 1) * 480 || lastCell.RightMargin != 420 + (columns - 1) * 480 ||
                    lastCell.RowSpan != 1 || lastCell.ColumnSpan != 1 || !lastCell.UsesV42Borders || lastCell.V42Border != 0)
                    throw new InvalidOperationException("Saved cell fields differ from the native BSIG layout.");
                using var exporter = session.CreateDXLExporter();
                exporter.OutputDOCTYPE = false;
                exporter.RichTextOption = 0;
                using var dxl = exporter.Export(reopened);
                using var xmlBytes = new MemoryStream(dxl.ToArray());
                var xml = System.Xml.Linq.XDocument.Load(xmlBytes);
                var exported = xml.Descendants().Single(e => e.Name.LocalName == "table");
                var exportedRows = exported.Elements().Where(e => e.Name.LocalName == "tablerow").ToArray();
                if (exportedRows.Length != rows || exportedRows.Any(row => row.Elements().Count(e => e.Name.LocalName == "tablecell") != columns))
                    throw new InvalidOperationException($"Notes DXL export does not confirm {rows}x{columns}: {exported}");
                Console.WriteLine($"VERIFIED saved table and Notes DXL {rows}x{columns}");
            }
            finally { if (!document.IsNewNote) document.Remove(true); }
        }
    });
    if (!string.IsNullOrWhiteSpace(databasePath)) Check("rich text table colors native persistence", () =>
    {
        using var database = session.OpenDatabase(server, databasePath);
        using var document = database.CreateDocument();
        try
        {
            using (var body = document.CreateRichTextItem("Body")) body.AppendTable(2, 2);
            document.Save();
            using var reopened = database.GetDocumentByID(document.NoteId) ?? throw new InvalidOperationException("Color test document missing.");
            using (var item = reopened.GetFirstItem("Body") ?? throw new InvalidOperationException("Color test Body missing."))
            using (var body = item.GetRichTextItem() ?? throw new InvalidOperationException("Color test Body is not rich text."))
            using (var navigator = body.CreateNavigator())
            {
                if (!navigator.FindFirstElement(NotesRichTextNavigator.ElementTable)) throw new InvalidOperationException("Color test table missing.");
                using var table = navigator.GetElement() as NotesRichTextTable ?? throw new InvalidOperationException("Color test table type mismatch.");
                table.SetColor(0x00112233);
                table.SetAlternateColor(0x00445566);
            }
            reopened.Save();
            using var saved = database.GetDocumentByID(document.NoteId) ?? throw new InvalidOperationException("Color test document did not reopen.");
            using var savedItem = saved.GetFirstItem("Body") ?? throw new InvalidOperationException("Saved color Body missing.");
            using var savedBody = savedItem.GetRichTextItem() ?? throw new InvalidOperationException("Saved color Body is not rich text.");
            using var savedNavigator = savedBody.CreateNavigator();
            if (!savedNavigator.FindFirstElement(NotesRichTextNavigator.ElementTable)) throw new InvalidOperationException("Saved color table missing.");
            using var savedTable = savedNavigator.GetElement() as NotesRichTextTable ?? throw new InvalidOperationException("Saved color table type mismatch.");
            if (savedTable.StyleColor1 != 0x00112233 || savedTable.StyleColor2 != 0x00445566 || savedTable.Color != 0x00112233 || savedTable.AlternateColor != 0x00445566)
                throw new InvalidOperationException($"Table colors did not persist: {savedTable.StyleColor1:X8}/{savedTable.StyleColor2:X8}.");
        }
        finally { if (!document.IsNewNote) document.Remove(true); }
    });
    if (!string.IsNullOrWhiteSpace(databasePath)) Check("rich text table target row mutations native", () =>
    {
        using var database = session.OpenDatabase(server, databasePath); using var document = database.CreateDocument();
        try
        {
            using (var body = document.CreateRichTextItem("Body")) body.AppendTable(4, 1);
            document.Save();
            using (var reopened = database.GetDocumentByID(document.NoteId)!) using (var item = reopened.GetFirstItem("Body")!) using (var body = item.GetRichTextItem()!) using (var navigator = body.CreateNavigator())
            { navigator.FindFirstElement(NotesRichTextNavigator.ElementTable); using var table = navigator.GetElement() as NotesRichTextTable ?? throw new InvalidOperationException(); table.AddRow(2, 1); reopened.Save(); }
            using (var expanded = database.GetDocumentByID(document.NoteId)!) using (var item = expanded.GetFirstItem("Body")!) using (var body = item.GetRichTextItem()!) using (var navigator = body.CreateNavigator())
            { navigator.FindFirstElement(NotesRichTextNavigator.ElementTable); using var table = navigator.GetElement() as NotesRichTextTable ?? throw new InvalidOperationException(); if (table.RowCount != 6) throw new InvalidOperationException($"AddRow produced {table.RowCount} rows."); try { table.RemoveRow(5, 2); throw new InvalidOperationException("RemoveRow accepted a range beyond the last row."); } catch (ArgumentOutOfRangeException) { } table.RemoveRow(2, 1); expanded.Save(); }
            using var final = database.GetDocumentByID(document.NoteId)!; using var finalItem = final.GetFirstItem("Body")!; using var finalBody = finalItem.GetRichTextItem()!; using var finalNavigator = finalBody.CreateNavigator(); finalNavigator.FindFirstElement(NotesRichTextNavigator.ElementTable); using var finalTable = finalNavigator.GetElement() as NotesRichTextTable ?? throw new InvalidOperationException(); if (finalTable.RowCount != 4) throw new InvalidOperationException($"RemoveRow produced {finalTable.RowCount} rows.");
            using var spanDocument = database.CreateDocument();
            using (var spanBody = spanDocument.CreateRichTextItem("Body")) spanBody.AppendTable(2, 1);
            spanDocument.Save();
            using (var spanItem = spanDocument.GetFirstItem("Body")!) using (var spanBody = spanItem.GetRichTextItem()!) using (var spanNavigator = spanBody.CreateNavigator())
            { spanNavigator.FindFirstElement(NotesRichTextNavigator.ElementTable); using var spanTable = (NotesRichTextTable)spanNavigator.GetElement()!; using var origin = spanTable.GetCell(0, 0)!; origin.SetSpan(2, 1); }
            using (var spanItem = spanDocument.GetFirstItem("Body")!) using (var spanBody = spanItem.GetRichTextItem()!) using (var spanNavigator = spanBody.CreateNavigator())
            { spanNavigator.FindFirstElement(NotesRichTextNavigator.ElementTable); using var spanTable = (NotesRichTextTable)spanNavigator.GetElement()!; using var covered = spanTable.GetCell(1, 0) ?? throw new InvalidOperationException("A spanned table cell was not found."); if (covered.Row != 0 || covered.Column != 0) throw new InvalidOperationException("GetCell did not resolve a spanned cell."); spanDocument.Save(); }
            spanDocument.Remove(true);
        }
        finally { if (!document.IsNewNote) document.Remove(true); }
    });
    if (!string.IsNullOrWhiteSpace(databasePath)) Check("rich text table row removal native DXL", () =>
    {
        using var database = session.OpenDatabase(server, databasePath);
        using var document = database.CreateDocument();
        try
        {
            using (var sourceBody = document.CreateRichTextItem("Body")) { sourceBody.AppendTable(3, 2); }
            document.Save();
            using var reopened = database.GetDocumentByID(document.NoteId) ?? throw new InvalidOperationException("Row-removal document missing.");
            using var item = reopened.GetFirstItem("Body") ?? throw new InvalidOperationException("Row-removal Body missing.");
            using var body = item.GetRichTextItem() ?? throw new InvalidOperationException("Row-removal Body is not rich text.");
            using (var nav = body.CreateNavigator())
            {
                if (!nav.FindFirstElement(NotesRichTextNavigator.ElementTable)) throw new InvalidOperationException("Row-removal table missing.");
                using var table = nav.GetElement() as NotesRichTextTable ?? throw new InvalidOperationException("Unexpected row-removal table type.");
                table.SetRightToLeft(true);
            }
            using (var nav = body.CreateNavigator())
            {
                if (!nav.FindFirstElement(NotesRichTextNavigator.ElementTable)) throw new InvalidOperationException("RTL table missing.");
                using var table = nav.GetElement() as NotesRichTextTable ?? throw new InvalidOperationException("Unexpected RTL table type.");
                if (!table.RightToLeft) throw new InvalidOperationException("Table RightToLeft flag did not persist in the CD record.");
                table.SetStyle(0x23);
            }
            using (var nav = body.CreateNavigator())
            {
                if (!nav.FindFirstElement(NotesRichTextNavigator.ElementTable)) throw new InvalidOperationException("Styled table missing.");
                using var table = nav.GetElement() as NotesRichTextTable ?? throw new InvalidOperationException("Unexpected styled table type.");
                if (!table.RightToLeft || (table.Style & 0x23) != 0x23) throw new InvalidOperationException("Table style or direction flag did not persist.");
                table.AddRow();
            }
            using (var nav = body.CreateNavigator())
            {
                if (!nav.FindFirstElement(NotesRichTextNavigator.ElementTable)) throw new InvalidOperationException("Added-row table missing.");
                using var table = nav.GetElement() as NotesRichTextTable ?? throw new InvalidOperationException("Unexpected added-row table type.");
                if (table.RowCount != 4 || table.Cells.Count != 8) throw new InvalidOperationException("AddRow did not clone the final table row.");
                table.AddRow(2);
            }
            using (var nav = body.CreateNavigator())
            {
                if (!nav.FindFirstElement(NotesRichTextNavigator.ElementTable)) throw new InvalidOperationException("Multi-added-row table missing.");
                using var table = nav.GetElement() as NotesRichTextTable ?? throw new InvalidOperationException("Unexpected multi-added-row table type.");
                if (table.RowCount != 6 || table.Cells.Count != 12) throw new InvalidOperationException("AddRow(count) did not append the requested rows.");
                using (var editableCell = table.GetCell(1, 1) ?? throw new InvalidOperationException("Editable table cell missing."))
                {
                    editableCell.SetBorder(7);
                    editableCell.SetBackgroundColor(42);
                    editableCell.SetMargins(111, 222);
                    editableCell.SetSpan(2, 2);
                    editableCell.SetFractionalWidth(333);
                    editableCell.SetV42Border(444);
                    editableCell.SetFlags(6);
                }
            }
            using (var nav = body.CreateNavigator())
            {
                if (!nav.FindFirstElement(NotesRichTextNavigator.ElementTable)) throw new InvalidOperationException("Updated row-removal table missing.");
                using var table = nav.GetElement() as NotesRichTextTable ?? throw new InvalidOperationException("Unexpected updated row-removal table type.");
                using (var updatedCell = table.GetCell(1, 1) ?? throw new InvalidOperationException("Updated table cell missing."))
                    if (updatedCell.Border != 7 || updatedCell.BackgroundColor != 42 || updatedCell.LeftMargin != 111 || updatedCell.RightMargin != 222 || updatedCell.FractionalWidth != 333 || updatedCell.RowSpan != 2 || updatedCell.ColumnSpan != 2 || updatedCell.V42Border != 444 || updatedCell.Flags != 6)
                        throw new InvalidOperationException("Table cell writable fields did not persist in the CD record.");
                table.RemoveRow(1);
            }
            reopened.Save();
            using var saved = database.GetDocumentByID(document.NoteId) ?? throw new InvalidOperationException("Row-removal save missing.");
            using var savedItem = saved.GetFirstItem("Body") ?? throw new InvalidOperationException("Row-removal saved Body missing.");
            using var savedBody = savedItem.GetRichTextItem() ?? throw new InvalidOperationException("Row-removal saved Body is not rich text.");
            using var savedNav = savedBody.CreateNavigator();
            if (!savedNav.FindFirstElement(NotesRichTextNavigator.ElementTable)) throw new InvalidOperationException("Row-removal saved table missing.");
            using var savedTable = savedNav.GetElement() as NotesRichTextTable ?? throw new InvalidOperationException("Unexpected saved row-removal table type.");
            if (savedTable.RowCount != 5 || savedTable.ColumnCount != 2 || savedTable.Cells.Count != 10)
                throw new InvalidOperationException($"RemoveRow produced {savedTable.RowCount}x{savedTable.ColumnCount} with {savedTable.Cells.Count} cells.");
            Console.WriteLine("VERIFIED rich-text table RemoveRow and native save/reopen");
        }
        finally { if (!document.IsNewNote) document.Remove(true); }
    });
    if (!string.IsNullOrWhiteSpace(databasePath)) Check("rich text multiple table structure", () =>
    {
        using var database = session.OpenDatabase(server, databasePath);
        using var document = database.CreateDocument();
        try
        {
            using (var sourceBody = document.CreateRichTextItem("Body"))
            {
                sourceBody.AppendTable(2, 2);
                using var paragraph = session.CreateRichTextParagraphStyle();
                sourceBody.AppendParagraphStyle(paragraph);
                sourceBody.AppendTable(3, 1);
            }
            document.Save();
            using var reopened = database.GetDocumentByID(document.NoteId)!;
            using var item = reopened.GetFirstItem("Body")!;
            using var body = item.GetRichTextItem()!;
            using var nav = body.CreateNavigator();
            if (!nav.FindFirstElement(NotesRichTextNavigator.ElementTable)) throw new InvalidOperationException("First table missing.");
            using var first = nav.GetElement() as NotesRichTextTable ?? throw new InvalidOperationException("First table type mismatch.");
            if (first.RowCount != 2) throw new InvalidOperationException($"First table has {first.RowCount} rows.");
            using (var firstCell = first.GetCell(0, 0) ?? throw new InvalidOperationException("First table cell missing.")) firstCell.AppendText("CELL-APPEND");
            using var refreshedAfterCellAppend = body.CreateNavigator();
            if (!refreshedAfterCellAppend.FindFirstElement(NotesRichTextNavigator.ElementTable)) throw new InvalidOperationException("First table missing after cell append.");
            using var firstAfterCellAppend = refreshedAfterCellAppend.GetElement() as NotesRichTextTable ?? throw new InvalidOperationException("First table type mismatch after cell append.");
            using var cellStyle = session.CreateRichTextStyle(); cellStyle.Bold = -1; cellStyle.FontSize = 15;
            using (var styledCell = firstAfterCellAppend.GetCell(0, 1) ?? throw new InvalidOperationException("Second table cell missing.")) styledCell.AppendText("STYLE-CELL", cellStyle);
            using var refreshedAfterStyledCell = body.CreateNavigator();
            if (!refreshedAfterStyledCell.FindFirstElement(NotesRichTextNavigator.ElementTable)) throw new InvalidOperationException("First table missing after styled cell append.");
            using var tableBeforeAdd = refreshedAfterStyledCell.GetElement() as NotesRichTextTable ?? throw new InvalidOperationException("First table type mismatch before AddRow."); using var replacementCell = tableBeforeAdd.GetCell(0, 1) ?? throw new InvalidOperationException("Second table cell missing after append."); if (replacementCell.FindAndReplace("STYLE-CELL", "STYLE-REPLACED") != 1) throw new InvalidOperationException("Cell-scoped FindAndReplace did not replace the cell text."); replacementCell.Dispose(); using var refreshedAfterReplace = body.CreateNavigator(); if (!refreshedAfterReplace.FindFirstElement(NotesRichTextNavigator.ElementTable)) throw new InvalidOperationException("First table missing after cell replacement."); using var tableBeforeAddRefreshed = refreshedAfterReplace.GetElement() as NotesRichTextTable ?? throw new InvalidOperationException("First table type mismatch after replacement.");
            tableBeforeAddRefreshed.AddRow();
            using var afterAdd = body.CreateNavigator();
            if (!afterAdd.FindFirstElement(NotesRichTextNavigator.ElementTable)) throw new InvalidOperationException("First table missing after AddRow.");
            using var added = afterAdd.GetElement() as NotesRichTextTable ?? throw new InvalidOperationException("Added first table type mismatch.");
            if (added.RowCount != 3) throw new InvalidOperationException("AddRow did not update the first table.");
            added.RemoveRow(0);
            body.Update();
            using var saved = database.GetDocumentByID(document.NoteId) ?? throw new InvalidOperationException("Multi-table document did not reopen.");
            using var savedItem = saved.GetFirstItem("Body") ?? throw new InvalidOperationException("Saved multi-table Body missing.");
            using var savedBody = savedItem.GetRichTextItem() ?? throw new InvalidOperationException("Saved multi-table Body is not rich text.");
            using var savedNav = savedBody.CreateNavigator();
            if (!savedNav.FindFirstElement(NotesRichTextNavigator.ElementTable) || !savedNav.FindNextElement(NotesRichTextNavigator.ElementTable)) throw new InvalidOperationException("Second table missing after mutation.");
            using var savedSecond = savedNav.GetElement() as NotesRichTextTable ?? throw new InvalidOperationException("Saved second table type mismatch.");
            if (savedSecond.RowCount != 3 || savedSecond.Cells.Count != 3) throw new InvalidOperationException($"Mutation changed the second table: {savedSecond.RowCount}x{savedSecond.ColumnCount}, cells={savedSecond.Cells.Count}.");
            Console.WriteLine("VERIFIED explicit paragraph separates multiple tables");
        }
        finally { if (!document.IsNewNote) document.Remove(true); }
    });
    if (!string.IsNullOrWhiteSpace(databasePath)) Check("rich text multi-segment table removal", () =>
    {
        using var database = session.OpenDatabase(server, databasePath);
        using var document = database.CreateDocument();
        try
        {
            var markers = Enumerable.Range(0, 1800).Select(i => $"PREFIX-{i:D4}-" + new string('x', 60)).ToArray();
            using (var body = document.CreateRichTextItem("Body"))
            {
                body.AppendText(string.Join("\r\n", markers));
                body.AppendTable(3, 4);
                body.AppendText("SUFFIX-AFTER-REMOVED-TABLE");
            }
            document.Save();
            using (var reopened = database.GetDocumentByID(document.NoteId)!)
            {
                using var item = reopened.GetFirstItem("Body")!;
                using var body = item.GetRichTextItem()!;
                var records = (System.Collections.IEnumerable)typeof(NotesRichTextItem)
                    .GetMethod("ReadRichTextRecords", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(body, null)!;
                var segments = records.Cast<object>().Select(record => (int)record.GetType()
                    .GetProperty("SegmentIndex", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(record)!).Distinct().Count();
                if (segments < 2) throw new InvalidOperationException("Fixture did not produce multiple physical composite segments.");
                using var nav = body.CreateNavigator();
                if (!nav.FindFirstElement(NotesRichTextNavigator.ElementTable)) throw new InvalidOperationException("Multi-segment table missing.");
                using var table = nav.GetElement() as NotesRichTextTable ?? throw new InvalidOperationException("Unexpected table type.");
                table.Remove();
                reopened.Save();
            }
            using var saved = database.GetDocumentByID(document.NoteId)!;
            using var exporter = session.CreateDXLExporter();
            exporter.OutputDOCTYPE = false;
            exporter.RichTextOption = 0;
            using var dxl = exporter.Export(saved);
            using var bytes = new MemoryStream(dxl.ToArray());
            var xml = System.Xml.Linq.XDocument.Load(bytes);
            if (xml.Descendants().Any(e => e.Name.LocalName == "table")) throw new InvalidOperationException("Removed table persisted.");
            var text = xml.Root!.Value;
            if (markers.Any(marker => !text.Contains(marker, StringComparison.Ordinal)) || !text.Contains("SUFFIX-AFTER-REMOVED-TABLE", StringComparison.Ordinal))
                throw new InvalidOperationException("Table removal lost surrounding text across composite segments.");
            Console.WriteLine("VERIFIED multi-segment table removal preserves all 1800 prefix markers and suffix");
        }
        finally { if (!document.IsNewNote) document.Remove(true); }
    });
    if (!string.IsNullOrWhiteSpace(databasePath)) Check("rich text stale linked references across wrappers", () =>
    {
        using var database = session.OpenDatabase(server, databasePath);
        using var document = database.CreateDocument();
        using var body = document.CreateRichTextItem("Body");
        body.AppendText("Start");
        body.AppendTable(1, 2);
        body.AppendTable(3, 4);
        using var aliasItem = document.GetFirstItem("Body")!;
        using var alias = aliasItem.GetRichTextItem()!;
        using var firstNav = body.CreateNavigator();
        using var aliasNav = alias.CreateNavigator();
        if (!firstNav.FindFirstElement(NotesRichTextNavigator.ElementTable) || !aliasNav.FindFirstElement(NotesRichTextNavigator.ElementTable))
            throw new InvalidOperationException("Table fixture missing.");
        using var first = (NotesRichTextTable)firstNav.GetElement()!;
        using var stale = (NotesRichTextTable)aliasNav.GetElement()!;
        using var staleCell = stale.GetCell(0, 1)!;
        first.Remove();
        ExpectException<InvalidOperationException>("Stale aliased table read", () => _ = stale.RowCount);
        ExpectException<InvalidOperationException>("Stale aliased cell read", () => _ = staleCell.Column);
        ExpectException<InvalidOperationException>("Stale aliased table remove", () => stale.Remove());
        if (!aliasNav.FindFirstElement(NotesRichTextNavigator.ElementTable)) throw new InvalidOperationException("Aliased navigator did not refresh.");
        using var remaining = (NotesRichTextTable)aliasNav.GetElement()!;
        if (remaining.RowCount != 3 || remaining.ColumnCount != 4) throw new InvalidOperationException("Remaining table changed through stale reference.");
        body.AppendText("End");
        ExpectException<InvalidOperationException>("Stale table after append", () => _ = remaining.ColumnCount);
    });
    if (!string.IsNullOrWhiteSpace(databasePath)) Check("rich text begin end insert native", () =>
    {
        using var database = session.OpenDatabase(server, databasePath);
        using var document = database.CreateDocument();
        try
        {
            using var body = document.CreateRichTextItem("Body");
            body.AppendText("INSERT-A");
            body.AppendText("-B");
            using var navigator = body.CreateNavigator();
            if (!navigator.FindFirstElement(NotesRichTextNavigator.ElementTextRun)) throw new InvalidOperationException("Text run fixture missing.");
            body.BeginInsert(navigator);
            body.AppendNewLine();
            body.AppendText("INSERT-X");
            body.AppendText("INSERT-Y");
            body.EndInsert();
            using var range = body.CreateRange();
            using var rangeNavigator = body.CreateNavigator();
            if (!rangeNavigator.FindFirstElement(NotesRichTextNavigator.ElementTextRun)) throw new InvalidOperationException("Range insertion fixture missing.");
            range.SetBegin(rangeNavigator);
            range.SetEnd(rangeNavigator);
            body.BeginInsert(range);
            body.AppendText("INSERT-RANGE-");
            body.EndInsert();
            body.BeginInsert(range, atEnd: true);
            body.AppendText("INSERT-RANGE-END");
            body.EndInsert();
            using var sectionNavigator = body.CreateNavigator();
            if (!sectionNavigator.FindFirstElement(NotesRichTextNavigator.ElementTextRun)) throw new InvalidOperationException("Section insertion fixture missing.");
            body.BeginInsert(sectionNavigator);
            body.BeginSection("Inserted section");
            body.AppendText("Inserted section text");
            body.EndSection();
            body.EndInsert();
            var insertionAttachment = Path.Combine(Path.GetTempPath(), "richtext-insert-" + Guid.NewGuid().ToString("N") + ".bin");
            File.WriteAllBytes(insertionAttachment, new byte[] { 1, 2, 3 });
            try
            {
                using var embedded = body.AttachFile(insertionAttachment);
                body.BeginInsert(embedded);
                body.AppendText("INSERT-EMBEDDED");
                body.EndInsert();
            }
            finally { if (File.Exists(insertionAttachment)) File.Delete(insertionAttachment); }
            body.AppendTable(1, 1);
            using var elementNavigator = body.CreateNavigator();
            if (!elementNavigator.FindFirstElement(NotesRichTextNavigator.ElementTable)) throw new InvalidOperationException("Element insertion table fixture missing.");
            using var tableElement = (NotesRichTextTable)elementNavigator.GetElement()!;
            body.BeginInsert(tableElement);
            body.AppendText("INSERT-ELEMENT");
            body.EndInsert();
            document.Save();
            using var exporter = session.CreateDXLExporter();
            exporter.OutputDOCTYPE = false;
            exporter.RichTextOption = 0;
            using var dxl = exporter.Export(document);
            if (dxl.Length == 0) throw new InvalidOperationException("Insertion document exported no DXL.");
            ExpectException<InvalidOperationException>("Duplicate rich-text EndInsert", body.EndInsert);
        }
        finally { if (!document.IsNewNote) document.Remove(true); }
    });
    if (!string.IsNullOrWhiteSpace(databasePath)) Check("rich text paragraph style native DXL", () =>
    {
        using var database = session.OpenDatabase(server, databasePath);
        using var document = database.CreateDocument();
        using var body = document.CreateRichTextItem("Body");
        body.AppendText("PLAIN-BEFORE");
        using var style = session.CreateRichTextParagraphStyle();
        style.Alignment = 3;
        style.LeftMargin = 1440;
        style.FirstLineLeftMargin = 1440;
        body.AppendParagraphStyle(style);
        body.AppendText("CENTERED-AFTER");
        using var exporter = session.CreateDXLExporter();
        exporter.OutputDOCTYPE = false;
        exporter.RichTextOption = 0;
        using var dxl = exporter.Export(document);
        using var bytes = new MemoryStream(dxl.ToArray());
        var xml = System.Xml.Linq.XDocument.Load(bytes);
        var paragraph = xml.Descendants().Single(e => e.Name.LocalName == "par" && e.Value.Contains("CENTERED-AFTER", StringComparison.Ordinal));
        var definition = xml.Descendants().Single(e => e.Name.LocalName == "pardef" && (string?)e.Attribute("id") == (string?)paragraph.Attribute("def"));
        if ((string?)definition.Attribute("align") != "center") throw new InvalidOperationException("Appended paragraph style did not apply to following text.");
        if ((string?)definition.Attribute("leftmargin") != "1in") throw new InvalidOperationException("Paragraph left margin was not exported as one inch.");
        if (xml.Descendants().Where(e => e.Name.LocalName == "font").Any(font => (string?)font.Attribute("size") != "10pt"))
            throw new InvalidOperationException("Default text font must be 10pt instead of 0pt.");
    });
    if (!string.IsNullOrWhiteSpace(databasePath)) Check("rich text append style no-change native DXL", () =>
    {
        using var database = session.OpenDatabase(server, databasePath);
        using var document = database.CreateDocument();
        using var body = document.CreateRichTextItem("Body");
        using var initial = session.CreateRichTextStyle();
        initial.FontSize = 18;
        initial.NotesColor = 2;
        body.AppendStyle(initial);
        body.AppendText("DIRECT-STYLED", initial);
        using var unchanged = session.CreateRichTextStyle();
        body.AppendStyle(unchanged);
        body.AppendText("LARGE-RED");
        using var bold = session.CreateRichTextStyle();
        bold.Bold = -1;
        body.AppendStyle(bold);
        body.AppendText("BOLD-LARGE-RED");
        body.AppendStyle(unchanged);
        body.AppendText("UNCHANGED-BOLD-LARGE-RED");
        using var plain = session.CreateRichTextStyle();
        plain.Bold = 0;
        body.AppendStyle(plain);
        body.AppendText("PLAIN-LARGE-RED");
        using var aliasItem = document.GetFirstItem("Body")!;
        using var alias = aliasItem.GetRichTextItem()!;
        alias.AppendStyle(bold);
        alias.AppendText("ALIAS-BOLD-LARGE-RED");
        using var exporter = session.CreateDXLExporter();
        exporter.OutputDOCTYPE = false;
        exporter.RichTextOption = 0;
        using var dxl = exporter.Export(document);
        using var bytes = new MemoryStream(dxl.ToArray());
        var xml = System.Xml.Linq.XDocument.Load(bytes);
        foreach (var (marker, expectedBold) in new[] { ("LARGE-RED", false), ("DIRECT-STYLED", false), ("BOLD-LARGE-RED", true), ("UNCHANGED-BOLD-LARGE-RED", true), ("PLAIN-LARGE-RED", false), ("ALIAS-BOLD-LARGE-RED", true) })
        {
            var run = xml.Descendants().Single(e => e.Name.LocalName == "run" && e.Value == marker);
            var font = run.Elements().Single(e => e.Name.LocalName == "font");
            if ((string?)font.Attribute("size") != "18pt" || (string?)font.Attribute("color") != "red" ||
                (((string?)font.Attribute("style"))?.Contains("bold", StringComparison.Ordinal) == true) != expectedBold)
                throw new InvalidOperationException($"STYLE_NO_CHANGE changed prior attributes for {marker}: {font}");
        }
    });
    if (!string.IsNullOrWhiteSpace(databasePath)) Check("rich text passthru HTML native DXL", () =>
    {
        using var database = session.OpenDatabase(server, databasePath);
        using var document = database.CreateDocument();
        try
        {
            using var body = document.CreateRichTextItem("Body");
            using var raw = session.CreateRichTextStyle();
            raw.PassThruHTML = -1;
            body.AppendStyle(raw);
            body.AppendText("<b>RAW-HTML-ONE</b>");
            using var unchanged = session.CreateRichTextStyle();
            body.AppendStyle(unchanged);
            body.AppendText("<i>RAW-HTML-TWO</i>");
            using var plain = session.CreateRichTextStyle();
            plain.PassThruHTML = 0;
            body.AppendStyle(plain);
            body.AppendText("<b>LITERAL-AFTER-HTML</b>");
            document.Save();
            using var reopened = database.GetDocumentByID(document.NoteId)!;
            using var savedItem = reopened.GetFirstItem("Body")!;
            using var savedBody = savedItem.GetRichTextItem()!;
            using var exporter = session.CreateDXLExporter();
            exporter.OutputDOCTYPE = false;
            exporter.RichTextOption = 0;
            using var dxl = exporter.Export(reopened);
            using var bytes = new MemoryStream(dxl.ToArray());
            var xml = System.Xml.Linq.XDocument.Load(bytes);
            var rawHtml = string.Concat(xml.Descendants().Where(e => e.Name.LocalName == "run" && (string?)e.Attribute("html") == "true").Select(e => e.Value));
            if (!rawHtml.Contains("<b>RAW-HTML-ONE</b>", StringComparison.Ordinal) || !rawHtml.Contains("<i>RAW-HTML-TWO</i>", StringComparison.Ordinal) || rawHtml.Contains("LITERAL-AFTER-HTML", StringComparison.Ordinal))
                throw new InvalidOperationException($"Passthru HTML boundaries were not represented in Notes DXL: {xml}");
            var html = savedBody.ConvertToHTML();
            if (!html.Contains("<b>RAW-HTML-ONE</b>", StringComparison.Ordinal) || !html.Contains("&lt;b&gt;LITERAL-AFTER-HTML&lt;/b&gt;", StringComparison.Ordinal))
                throw new InvalidOperationException($"Notes HTML conversion did not distinguish raw and literal HTML: {html}");
        }
        finally { if (!document.IsNewNote) document.Remove(true); }
    });
    if (!string.IsNullOrWhiteSpace(databasePath)) Check("rich text compact native persistence", () =>
    {
        using var database = session.OpenDatabase(server, databasePath);
        using var document = database.CreateDocument();
        try
        {
            using var body = document.CreateRichTextItem("Body");
            var expected = string.Concat(Enumerable.Range(0, 100).Select(i => $"Compact-{i:D3}-Åäö"));
            foreach (var text in Enumerable.Range(0, 100).Select(i => $"Compact-{i:D3}-Åäö")) body.AppendText(text);
            int RecordBytes()
            {
                var records = (System.Collections.IEnumerable)typeof(NotesRichTextItem).GetMethod("ReadRichTextRecords", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(body, null)!;
                return records.Cast<object>().Sum(record => ((byte[])record.GetType().GetProperty("Data", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(record)!).Length);
            }
            var before = RecordBytes();
            body.Compact();
            var after = RecordBytes();
            if (after >= before) throw new InvalidOperationException($"Compact did not reduce redundant text record bytes: {before} -> {after}.");
            body.Compact();
            if (RecordBytes() != after) throw new InvalidOperationException("Repeated Compact was not idempotent.");
            document.Save();
            using var reopened = database.GetDocumentByID(document.NoteId)!;
            using var exporter = session.CreateDXLExporter();
            exporter.OutputDOCTYPE = false;
            exporter.RichTextOption = 0;
            using var dxl = exporter.Export(reopened);
            using var bytes = new MemoryStream(dxl.ToArray());
            var xml = System.Xml.Linq.XDocument.Load(bytes);
            var textValue = string.Concat(xml.Descendants().Where(e => e.Name.LocalName == "richtext").Select(e => e.Value));
            if (textValue != expected) throw new InvalidOperationException("Compact changed persisted Unicode text.");
            Console.WriteLine($"VERIFIED Compact bytes {before} -> {after}, Unicode persistence and repeat call");
        }
        finally { if (!document.IsNewNote) document.Remove(true); }
    });
    if (!string.IsNullOrWhiteSpace(databasePath)) Check("rich text compact mixed content and flags", () =>
    {
        using var database = session.OpenDatabase(server, databasePath);
        using var document = database.CreateDocument();
        var attachment = Path.Combine(Path.GetTempPath(), "richtext-compact-" + Guid.NewGuid().ToString("N") + ".bin");
        try
        {
            var payload = Enumerable.Range(0, 4096).Select(i => (byte)(i % 251)).ToArray();
            File.WriteAllBytes(attachment, payload);
            using var body = document.CreateRichTextItem("Body");
            body.AppendText("PREFIX");
            body.IsSigned = false;
            body.IsEncrypted = false;
            body.IsProtected = true;
            for (var i = 0; i < 100; i++) body.AppendText($"Text-{i:D3}-Åäö");
            body.AppendTable(3, 4);
            using var style = session.CreateRichTextStyle();
            style.Bold = -1;
            style.FontSize = 18;
            body.AppendStyle(style);
            body.AppendText("BOLD-SUFFIX");
            style.PassThruHTML = -1;
            body.AppendStyle(style);
            body.AppendText("<em>HTML-SUFFIX</em>");
            using (var embedded = body.AttachFile(attachment)) { }
            using var exporter = session.CreateDXLExporter();
            exporter.OutputDOCTYPE = false;
            exporter.RichTextOption = 0;
            System.Xml.Linq.XElement Snapshot(NotesDocument doc)
            {
                using var stream = exporter.Export(doc);
                using var bytes = new MemoryStream(stream.ToArray());
                return System.Xml.Linq.XDocument.Load(bytes).Descendants().Single(e => e.Name.LocalName == "richtext");
            }
            var before = Snapshot(document);
            body.Compact();
            body.Compact();
            if (body.IsSigned || body.IsEncrypted || !body.IsProtected) throw new InvalidOperationException("Compact changed item flags.");
            document.Save();
            using var reopened = database.GetDocumentByID(document.NoteId)!;
            using var savedItem = reopened.GetFirstItem("Body")!;
            using var savedBody = savedItem.GetRichTextItem()!;
            if (!System.Xml.Linq.XNode.DeepEquals(before, Snapshot(reopened))) throw new InvalidOperationException("Compact changed mixed rich-text DXL.");
            using var savedAttachment = savedBody.GetEmbeddedObject(Path.GetFileName(attachment))!;
            if (!savedAttachment.ToByteArray().SequenceEqual(payload)) throw new InvalidOperationException("Compact changed attachment bytes.");
        }
        finally
        {
            if (!document.IsNewNote) document.Remove(true);
            if (File.Exists(attachment)) File.Delete(attachment);
        }
    });
    NotesItemRegressionTests.Run(session, server, databasePath, Check);
    DatabaseLifecycleRegressionTests.Run(session, Check);
    AclRegressionTests.Run(session, Check);
    NotesItemCryptoTests.Run(session, server, databasePath, Check);
    if (!string.IsNullOrWhiteSpace(databasePath)) Check("item surface", () =>
    {
        using var database = session.GetDatabase(server, databasePath, false) ?? throw new InvalidOperationException("Database not found.");
        using var document = database.CreateDocument();
        try { _ = document.GetFirstItem(null!); throw new InvalidOperationException("Null item name was accepted."); }
        catch (ArgumentNullException ex) when (ex.ParamName == "itemName") { }
        try { document.SetString(null!, "value"); throw new InvalidOperationException("Null SetString item name was accepted."); }
        catch (ArgumentNullException ex) when (ex.ParamName == "n") { }
        document.SetString("EmptyStringItem", string.Empty);
        document.SetValue("EmptyValueItem", string.Empty);
        if (document.GetString("EmptyStringItem") != string.Empty || document.GetString("EmptyValueItem") != string.Empty)
            throw new InvalidOperationException("Empty string item values did not round-trip.");
        try { document.SetDateTime("DateItem", null!); throw new InvalidOperationException("Null SetDateTime value was accepted."); }
        catch (ArgumentNullException ex) when (ex.ParamName == "v") { }
        try { document.RemoveItem(null!); throw new InvalidOperationException("Null RemoveItem name was accepted."); }
        catch (ArgumentNullException ex) when (ex.ParamName == "n") { }
        document.ReplaceItemValue("FullTestItem", "value").Recycle();
        using var item = document.GetFirstItem("FullTestItem") ?? throw new InvalidOperationException("Item was not created.");
        if (item.Text != "value" || item.Values.Length != 1) throw new InvalidOperationException("Item value round trip failed.");
        item.Remove();
        try { _ = item.Text; throw new InvalidOperationException("Removed item remained usable."); }
        catch (ObjectDisposedException) { }
    });
    if (!string.IsNullOrWhiteSpace(databasePath)) Check("rich text database view document links persistence", () =>
    {
        using var database = session.OpenDatabase(server, databasePath);
        using var document = database.CreateDocument();
        var views = database.Views;
        try
        {
            var view = views.FirstOrDefault() ?? throw new InvalidOperationException("Link test requires a view.");
            using var body = document.CreateRichTextItem("Body");
            body.AppendDocLink(database, "database-target");
            body.AppendDocLink(view, "view-target");
            document.Save();
            body.AppendDocLink(document, "document-target");
            body.Compact();
            document.Save();
            using var reopened = database.GetDocumentByID(document.NoteId)!;
            using var exporter = session.CreateDXLExporter();
            exporter.OutputDOCTYPE = false;
            exporter.RichTextOption = 0;
            using var dxl = exporter.Export(reopened);
            using var bytes = new MemoryStream(dxl.ToArray());
            var xml = System.Xml.Linq.XDocument.Load(bytes);
            var links = xml.Descendants().Where(e => e.Name.LocalName is "doclink" or "viewlink" or "databaselink").ToArray();
            if (links.Length != 3) throw new InvalidOperationException("Expected three persisted links.");
            if (links.Any(e => !string.Equals((string?)e.Attribute("database"), database.ReplicaID, StringComparison.OrdinalIgnoreCase))) throw new InvalidOperationException("Link database replica ID mismatch.");
            if (!links.Any(e => e.Name.LocalName == "databaselink" && (string?)e.Attribute("description") == "database-target")) throw new InvalidOperationException("Database link missing.");
            if (!links.Any(e => string.Equals((string?)e.Attribute("view"), view.UniversalID, StringComparison.OrdinalIgnoreCase))) throw new InvalidOperationException("View link UNID mismatch.");
            if (!links.Any(e => string.Equals((string?)e.Attribute("document"), document.UniversalId, StringComparison.OrdinalIgnoreCase))) throw new InvalidOperationException("Document link UNID mismatch.");
            using var savedItem = reopened.GetFirstItem("Body")!;
            using var savedBody = savedItem.GetRichTextItem()!;
            using var navigator = savedBody.CreateNavigator();
            using var databaseLink = navigator.GetFirstElement(5) as NotesRichTextDocLink ?? throw new InvalidOperationException("Database link missing from navigator.");
            using var viewLink = navigator.GetNextElement(5) as NotesRichTextDocLink ?? throw new InvalidOperationException("View link missing from navigator.");
            if (databaseLink.DbReplicaID != database.ReplicaID || !string.IsNullOrEmpty(databaseLink.DocUNID) || !string.IsNullOrEmpty(databaseLink.ViewUNID)) throw new InvalidOperationException("Navigator database link target mismatch.");
            if (viewLink.DbReplicaID != database.ReplicaID || viewLink.ViewUNID != view.UniversalID || !string.IsNullOrEmpty(viewLink.DocUNID)) throw new InvalidOperationException("Navigator view link target mismatch.");
            viewLink.Remove();
            savedBody.Compact();
            reopened.Save();
            using var removedReopened = database.GetDocumentByID(document.NoteId)!;
            using var removedDxl = exporter.Export(removedReopened);
            using var removedBytes = new MemoryStream(removedDxl.ToArray());
            var remainingLinks = System.Xml.Linq.XDocument.Load(removedBytes).Descendants().Where(e => e.Name.LocalName is "doclink" or "viewlink" or "databaselink").ToArray();
            if (remainingLinks.Length != 2 || remainingLinks.Any(e => e.Name.LocalName == "viewlink") || !remainingLinks.Any(e => e.Name.LocalName == "databaselink") || !remainingLinks.Any(e => e.Name.LocalName == "doclink")) throw new InvalidOperationException("Removing view link changed adjacent links.");
        }
        finally
        {
            foreach (var view in views) view.Dispose();
            if (!document.IsNewNote) document.Remove(true);
        }
    });
    if (!string.IsNullOrWhiteSpace(databasePath)) Check("rich text cross database document link", () =>
    {
        using var database = session.OpenDatabase(server, databasePath);
        var path = "DotNetToDominoLinkTarget-" + Guid.NewGuid().ToString("N") + ".nsf";
        using var targetDatabase = session.GetDatabase("", path, true)!;
        targetDatabase.Create();
        using var document = database.CreateDocument();
        try
        {
            using var target = targetDatabase.CreateDocument();
            target.ReplaceItemValue("Subject", "Cross database link target").Recycle();
            target.Save();
            if (targetDatabase.ReplicaID == database.ReplicaID) throw new InvalidOperationException("Test requires distinct replicas.");
            using var body = document.CreateRichTextItem("Body");
            body.AppendDocLink(target, "Cross database Åäö");
            body.Compact();
            document.Save();
            using var reopened = database.GetDocumentByID(document.NoteId)!;
            using var item = reopened.GetFirstItem("Body")!;
            using var savedBody = item.GetRichTextItem()!;
            using var navigator = savedBody.CreateNavigator();
            using var link = navigator.GetFirstElement(5) as NotesRichTextDocLink ?? throw new InvalidOperationException("Document link missing from navigator.");
            if (link.DbReplicaID != targetDatabase.ReplicaID || link.DocUNID != target.UniversalID || link.DisplayComment != "Cross database Åäö") throw new InvalidOperationException($"Navigator link target mismatch: {link.DbReplicaID}/{link.DocUNID}/{link.DisplayComment}");
            using var exporter = session.CreateDXLExporter();
            exporter.OutputDOCTYPE = false;
            exporter.RichTextOption = 0;
            using var dxl = exporter.Export(reopened);
            using var bytes = new MemoryStream(dxl.ToArray());
            var xml = System.Xml.Linq.XDocument.Load(bytes);
            var exported = xml.Descendants().Single(e => e.Name.LocalName == "doclink");
            if ((string?)exported.Attribute("database") != targetDatabase.ReplicaID || (string?)exported.Attribute("document") != target.UniversalID) throw new InvalidOperationException("Cross database DXL target mismatch.");
        }
        finally
        {
            if (!document.IsNewNote) document.Remove(true);
            targetDatabase.Recycle();
            using var cleanup = session.GetDatabase("", path, false);
            if (cleanup?.IsOpen == true) cleanup.Remove();
        }
    });
    if (!string.IsNullOrWhiteSpace(databasePath)) Check("rich text legacy CDLINK2 reference list", () =>
    {
        using var database = session.OpenDatabase(server, databasePath);
        using var document = database.CreateDocument();
        try
        {
            document.Save();
            using var body = document.CreateRichTextItem("Body");
            body.AppendDocLink(document, "Legacy link");
            var records = (System.Collections.IEnumerable)typeof(NotesRichTextItem).GetMethod("ReadRichTextRecords", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(body, null)!;
            var exported = records.Cast<object>().Select(r => (byte[])r.GetType().GetProperty("Data", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(r)!).Single(b => b.Length >= 44 && b[0] == 0x92 && b[1] == 0xff);
            var references = new byte[82];
            references[0] = 2;
            // Leave the first entry empty: resolving index 1 must use the second entry.
            Array.Copy(exported, 4, references, 42, 40);
            var legacy = new byte[exported.Length - 38];
            legacy[0] = 0x91; legacy[1] = 0xff;
            legacy[2] = (byte)legacy.Length; legacy[3] = (byte)(legacy.Length >> 8);
            legacy[4] = 1;
            Array.Copy(exported, 44, legacy, 6, exported.Length - 44);
            var api = typeof(NotesSession).GetProperty("Api", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(session)!;
            var apiType = api.GetType();
            var handle = typeof(NotesDocument).GetProperty("Handle", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(document)!;
            using var thread = (IDisposable)apiType.GetMethod("EnterThread", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(api, null)!;
            var append = (Delegate)apiType.GetMethod("Resolve", BindingFlags.Instance | BindingFlags.NonPublic)!.MakeGenericMethod(apiType.GetNestedType("Ops_NSFItemAppend", BindingFlags.NonPublic)!).Invoke(api, new object[] { "NSFItemAppend" })!;
            void AppendRaw(string name, ushort type, byte[] data)
            {
                var namePointer = System.Runtime.InteropServices.Marshal.StringToHGlobalAnsi(name);
                var valuePointer = System.Runtime.InteropServices.Marshal.AllocHGlobal(data.Length);
                try
                {
                    System.Runtime.InteropServices.Marshal.Copy(data, 0, valuePointer, data.Length);
                    var status = (ushort)append.DynamicInvoke(handle, (ushort)0, namePointer, (ushort)name.Length, type, valuePointer, (uint)data.Length)!;
                    if (status != 0) throw new InvalidOperationException($"Native fixture append failed: {status:X4}");
                }
                finally { System.Runtime.InteropServices.Marshal.FreeHGlobal(valuePointer); System.Runtime.InteropServices.Marshal.FreeHGlobal(namePointer); }
            }
            body.Remove();
            AppendRaw("$Links", 7, references);
            AppendRaw("Body", 1, legacy);
            document.Save();
            using var reopened = database.GetDocumentByID(document.NoteId)!;
            using var item = reopened.GetFirstItem("Body")!;
            using var savedBody = item.GetRichTextItem()!;
            using var navigator = savedBody.CreateNavigator();
            using var link = navigator.GetFirstElement(5) as NotesRichTextDocLink ?? throw new InvalidOperationException("Legacy link missing.");
            if (link.DbReplicaID != database.ReplicaID || link.DocUNID != document.UniversalID || link.DisplayComment != "Legacy link") throw new InvalidOperationException("Legacy link target mismatch.");
            using var exporter = session.CreateDXLExporter();
            exporter.OutputDOCTYPE = false; exporter.RichTextOption = 0;
            using var dxl = exporter.Export(reopened);
            using var bytes = new MemoryStream(dxl.ToArray());
            var xml = System.Xml.Linq.XDocument.Load(bytes);
            if ((string?)xml.Descendants().SingleOrDefault(e => e.Name.LocalName == "doclink")?.Attribute("document") != document.UniversalID) throw new InvalidOperationException($"Notes did not independently resolve legacy link: {xml}");
            using (var destination = database.CreateDocument())
            {
                try
                {
                    using var copied = savedBody.CopyToDocument(destination, "CopiedBody");
                    destination.Save();
                    using var copiedReopened = database.GetDocumentByID(destination.NoteId)!;
                    using var copiedDxl = exporter.Export(copiedReopened);
                    using var copiedBytes = new MemoryStream(copiedDxl.ToArray());
                    var copiedXml = System.Xml.Linq.XDocument.Load(copiedBytes);
                    if ((string?)copiedXml.Descendants().SingleOrDefault(e => e.Name.LocalName == "doclink")?.Attribute("document") != document.UniversalID) throw new InvalidOperationException("CopyToDocument lost the legacy link target.");
                }
                finally { if (!destination.IsNewNote) destination.Remove(true); }
            }
            using var linksItem = reopened.GetFirstItem("$Links")!;
            linksItem.Remove();
            ExpectException<InvalidOperationException>("Missing legacy link list", () => { _ = link.DocUNID; });
        }
        finally { if (!document.IsNewNote) document.Remove(true); }
    });
    if (!string.IsNullOrWhiteSpace(databasePath)) Check("rich text multi segment item copy", () =>
    {
        using var database = session.OpenDatabase(server, databasePath);
        using var source = database.CreateDocument();
        using var destination = database.CreateDocument();
        try
        {
            using var body = source.CreateRichTextItem("Body");
            var expected = string.Concat(Enumerable.Range(0, 1800).Select(i => $"COPY-{i:D4}-Åäö-" + new string('x', 60)));
            foreach (var text in Enumerable.Range(0, 1800).Select(i => $"COPY-{i:D4}-Åäö-" + new string('x', 60))) body.AppendText(text);
            body.AppendTable(2, 3);
            body.IsProtected = true;
            source.Save();
            using var copied = body.CopyToDocument(destination, "RenamedBody");
            destination.Save();
            using var reopened = database.GetDocumentByID(destination.NoteId)!;
            using var savedItem = reopened.GetFirstItem("RenamedBody")!;
            if (!savedItem.IsProtected) throw new InvalidOperationException("Rich text copy lost item flags.");
            using var exporter = session.CreateDXLExporter();
            exporter.OutputDOCTYPE = false; exporter.RichTextOption = 0;
            using var dxl = exporter.Export(reopened);
            using var bytes = new MemoryStream(dxl.ToArray());
            var xml = System.Xml.Linq.XDocument.Load(bytes);
            var richText = xml.Descendants().Single(e => e.Name.LocalName == "richtext");
            if (richText.Value != expected || richText.Descendants().Count(e => e.Name.LocalName == "tablecell") != 6) throw new InvalidOperationException("Multi-segment copy lost text or table cells.");
        }
        finally
        {
            if (!source.IsNewNote) source.Remove(true);
            if (!destination.IsNewNote) destination.Remove(true);
        }
    });
    foreach (var crossDatabase in new[] { false, true })
    if (!string.IsNullOrWhiteSpace(databasePath)) Check("rich text attachment item copy " + (crossDatabase ? "cross database" : "same database"), () =>
    {
        using var database = session.OpenDatabase(server, databasePath);
        var targetPath = "DotNetToDominoAttachmentCopy-" + Guid.NewGuid().ToString("N") + ".nsf";
        using var targetDatabase = crossDatabase ? session.GetDatabase("", targetPath, true)! : null;
        if (targetDatabase is not null) targetDatabase.Create();
        using var source = database.CreateDocument();
        using var destination = (targetDatabase ?? database).CreateDocument();
        var path = Path.Combine(Path.GetTempPath(), "richtext-copy-" + Guid.NewGuid().ToString("N") + ".bin");
        try
        {
            var payload = Enumerable.Range(0, 100000).Select(i => (byte)(i % 251)).ToArray();
            File.WriteAllBytes(path, payload);
            using var body = source.CreateRichTextItem("Body");
            body.AppendText("Before attachment");
            using (var attachment = body.AttachFile(path)) { }
            body.AppendText("After attachment");
            using var sourceEmbedded = body.GetEmbeddedObject(Path.GetFileName(path)) ?? throw new InvalidOperationException("Source embedded object is missing.");
            using var appendedObjectBody = destination.CreateRichTextItem("AppendedObjectBody");
            using var appendedObject = appendedObjectBody.AppendObject(sourceEmbedded);
            source.Save();
            using (var conflicting = database.CreateDocument())
            {
                using var conflictBody = conflicting.CreateRichTextItem("CopiedBody");
                conflictBody.AppendText("Keep existing content");
                File.WriteAllBytes(path, new byte[] { 9, 8, 7 });
                using var conflictAttachment = conflictBody.AttachFile(path);
                ExpectException<IOException>("Different attachment name collision", () => { using var rejected = body.CopyToDocument(conflicting, "CopiedBody"); });
                if (!conflictAttachment.ToByteArray().SequenceEqual(new byte[] { 9, 8, 7 })) throw new InvalidOperationException("Rejected copy changed existing attachment.");
                using var conflictExporter = session.CreateDXLExporter();
                conflictExporter.OutputDOCTYPE = false; conflictExporter.RichTextOption = 0;
                using var conflictDxl = conflictExporter.Export(conflicting);
                using var conflictBytes = new MemoryStream(conflictDxl.ToArray());
                var conflictXml = System.Xml.Linq.XDocument.Load(conflictBytes);
                if (!conflictXml.Descendants().Where(e => e.Name.LocalName == "richtext").Any(e => e.Value.Contains("Keep existing content", StringComparison.Ordinal))) throw new InvalidOperationException($"Rejected copy changed existing rich text: {conflictXml}");
            }
            using var copied = body.CopyToDocument(destination, "CopiedBody");
            using var copiedAgain = body.CopyToDocument(destination, "SecondCopy");
            destination.Save();
            source.Remove(true);
            using var reopened = (targetDatabase ?? database).GetDocumentByID(destination.NoteId)!;
            using var item = reopened.GetFirstItem("CopiedBody")!;
            using var savedBody = item.GetRichTextItem()!;
            using var savedAttachment = savedBody.GetEmbeddedObject(Path.GetFileName(path)) ?? throw new InvalidOperationException("Copied attachment is missing.");
            if (!savedAttachment.ToByteArray().SequenceEqual(payload)) throw new InvalidOperationException("Copied attachment bytes differ.");
            using var secondItem = reopened.GetFirstItem("SecondCopy")!;
            using var secondBody = secondItem.GetRichTextItem()!;
            using var secondAttachment = secondBody.GetEmbeddedObject(Path.GetFileName(path))!;
            if (!secondAttachment.ToByteArray().SequenceEqual(payload)) throw new InvalidOperationException("Repeated copy attachment bytes differ.");
            using var appendedItem = reopened.GetFirstItem("AppendedObjectBody")!;
            using var appendedBody = appendedItem.GetRichTextItem()!;
            using var appendedAttachment = appendedBody.GetEmbeddedObject(Path.GetFileName(path)) ?? throw new InvalidOperationException("AppendObject attachment is missing.");
            if (!appendedAttachment.ToByteArray().SequenceEqual(payload)) throw new InvalidOperationException("AppendObject attachment bytes differ.");
        }
        finally
        {
            if (!source.IsRecycled && !source.IsNewNote) source.Remove(true);
            if (!destination.IsNewNote) destination.Remove(true);
            if (File.Exists(path)) File.Delete(path);
            if (targetDatabase is not null)
            {
                targetDatabase.Recycle();
                using var cleanup = session.GetDatabase("", targetPath, false);
                if (cleanup?.IsOpen == true) cleanup.Remove();
            }
        }
    });
    if (!string.IsNullOrWhiteSpace(databasePath)) Check("rich text range style native readback", () =>
    {
        using var database = session.OpenDatabase(server, databasePath);
        using var document = database.CreateDocument();
        try
        {
            using var body = document.CreateRichTextItem("Body");
            using var style = session.CreateRichTextStyle();
            style.Bold = -1; style.Italic = -1; style.Underline = -1; style.Strikethrough = -1;
            style.FontSize = 18; style.NotesFont = 2; style.NotesColor = 2; style.Effects = 4;
            body.AppendStyle(style); body.AppendText("Styled first run Åäö");
            style.Bold = 0; style.Italic = 0; style.Underline = 0; style.Strikethrough = 0;
            style.FontSize = 12; style.NotesColor = 0; style.Effects = 0;
            body.AppendStyle(style); body.AppendText("Plain second run");
            document.Save();
            using var reopened = database.GetDocumentByID(document.NoteId)!;
            using var item = reopened.GetFirstItem("Body")!;
            using var savedBody = item.GetRichTextItem()!;
            using var range = savedBody.CreateRange();
            using var actual = range.Style;
            if (actual.Bold != -1 || actual.Italic != -1 || actual.Underline != -1 || actual.Strikethrough != -1 || actual.FontSize != 18 || actual.NotesFont != 2 || actual.NotesColor != 2 || actual.Effects != 4 || actual.PassThruHTML != 0) throw new InvalidOperationException("Range.Style did not return the first text run formatting.");
            using var navigator = savedBody.CreateNavigator();
            if (!navigator.FindFirstString("Plain second run")) throw new InvalidOperationException("Second run missing.");
            range.SetBegin(navigator);
            using var second = range.Style;
            if (second.Bold != 0 || second.Italic != 0 || second.Underline != 0 || second.Strikethrough != 0 || second.FontSize != 12 || second.Effects != 0) throw new InvalidOperationException("Range.Style ignored the range beginning.");
        }
        finally { if (!document.IsNewNote) document.Remove(true); }
    });
    if (!string.IsNullOrWhiteSpace(databasePath)) Check("rich text range partial removal native", () =>
    {
        using var database = session.OpenDatabase(server, databasePath);
        using var document = database.CreateDocument();
        try
        {
            using var body = document.CreateRichTextItem("Body");
            body.AppendText("abcdef");
            using var range = body.CreateRange();
            using var navigator = body.CreateNavigator();
            navigator.FindFirstString("abcdef");
            range.SetBegin(navigator);
            var begin = typeof(NotesRichTextRange).GetField("_beginOffset", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var end = typeof(NotesRichTextRange).GetField("_endOffset", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var beginRecord = typeof(NotesRichTextRange).GetField("_beginRecord", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var endRecord = typeof(NotesRichTextRange).GetField("_endRecord", BindingFlags.Instance | BindingFlags.NonPublic)!;
            begin.SetValue(range, 2); end.SetValue(range, 4); endRecord.SetValue(range, beginRecord.GetValue(range));
            range.Remove();
            document.Save();
            using var reopened = database.GetDocumentByID(document.NoteId)!;
            using var item = reopened.GetFirstItem("Body")!;
            if (!string.Equals(item.Text, "abef", StringComparison.Ordinal)) throw new InvalidOperationException($"Partial range removal returned '{item.Text}'.");
        }
        finally { if (!document.IsNewNote) document.Remove(true); }
    });
    if (!string.IsNullOrWhiteSpace(databasePath)) Check("rich text range find and replace native", () =>
    {
        using var database = session.OpenDatabase(server, databasePath);
        using var document = database.CreateDocument();
        try
        {
            using var body = document.CreateRichTextItem("Body");
            body.AppendText("alpha beta alpha");
            using var range = body.CreateRange();
            if (range.FindAndReplace("alpha", "omega", NotesConst.RT_FIND_CASEINSENSITIVE | NotesConst.RT_REPL_ALL) != 2) throw new InvalidOperationException("Case-insensitive FindAndReplace returned the wrong count.");
            body.AppendText(" Café CAFE");
            range.Reset();
            if (range.FindAndReplace("cafe", "tea", NotesConst.RT_FIND_CASEINSENSITIVE | NotesConst.RT_FIND_ACCENTINSENSITIVE | NotesConst.RT_REPL_PRESERVECASE | NotesConst.RT_REPL_ALL) != 2) throw new InvalidOperationException("Accent-insensitive preserve-case FindAndReplace returned the wrong count.");
            body.AppendText(" alpha ALPHA ＡＢＣ ABC");
            range.Reset();
            if (range.FindAndReplace("alpha", "z") != 1) throw new InvalidOperationException("Default FindAndReplace must replace only the next occurrence.");
            range.Reset();
            if (range.FindAndReplace("alpha", "q", NotesConst.RT_FIND_CASESENSITIVE | NotesConst.RT_REPL_ALL) != 0) throw new InvalidOperationException("Case-sensitive FindAndReplace matched an uppercase value.");
            range.Reset();
            if (range.FindAndReplace("ABC", "xyz", NotesConst.RT_FIND_PITCHINSENSITIVE | NotesConst.RT_REPL_ALL) != 2) throw new InvalidOperationException("Pitch-insensitive FindAndReplace did not match full-width text.");
            range.Reset();
            try { range.FindAndReplace("x", "y", 32); throw new InvalidOperationException("Unknown FindAndReplace options were accepted."); }
            catch (ArgumentOutOfRangeException) { }
            document.Save();
            using var reopened = database.GetDocumentByID(document.NoteId)!;
            using var item = reopened.GetFirstItem("Body")!;
            if (item.Text != "omega beta omega Tea TEA z ALPHA xyz xyz") throw new InvalidOperationException($"FindAndReplace persisted '{item.Text}'.");
        }
        finally { if (!document.IsNewNote) document.Remove(true); }
    });
    if (!string.IsNullOrWhiteSpace(databasePath)) Check("rich text range cross segment style", () =>
    {
        using var database = session.OpenDatabase(server, databasePath);
        using var document = database.CreateDocument();
        try
        {
            using var body = document.CreateRichTextItem("Body");
            var values = Enumerable.Range(0, 1900).Select(i => $"SEG-{i:D4}-" + new string('x', 65)).ToArray();
            foreach (var value in values) body.AppendText(value);
            var records = (System.Collections.IEnumerable)typeof(NotesRichTextItem).GetMethod("ReadRichTextRecords", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(body, null)!;
            if (records.Cast<object>().Select(r => (int)r.GetType().GetProperty("SegmentIndex", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(r)!).Distinct().Count() < 2) throw new InvalidOperationException("Range fixture did not cross physical segments.");
            using var navigator = body.CreateNavigator();
            if (!navigator.FindFirstString("SEG-0002-")) throw new InvalidOperationException("Range start marker missing.");
            using var range = body.CreateRange();
            range.SetBegin(navigator);
            if (!navigator.FindFirstString("SEG-1700-")) throw new InvalidOperationException("Range end marker missing.");
            range.SetEnd(navigator);
            using var style = session.CreateRichTextStyle();
            style.NotesColor = 2; style.Bold = -1;
            range.SetStyle(style);
            document.Save();
            using var reopened = database.GetDocumentByID(document.NoteId)!;
            using var exporter = session.CreateDXLExporter(); exporter.OutputDOCTYPE = false; exporter.RichTextOption = 0;
            using var dxl = exporter.Export(reopened); using var bytes = new MemoryStream(dxl.ToArray());
            var richText = System.Xml.Linq.XDocument.Load(bytes).Descendants().Single(e => e.Name.LocalName == "richtext");
            if (!richText.Value.Contains("SEG-0002-", StringComparison.Ordinal) || !richText.Value.Contains("SEG-1700-", StringComparison.Ordinal)) throw new InvalidOperationException("Cross-segment SetStyle lost text.");
            if (!richText.Value.Contains("SEG-1700-", StringComparison.Ordinal)) throw new InvalidOperationException("Cross-segment SetStyle lost later text.");
        }
        finally { if (!document.IsNewNote) document.Remove(true); }
    });
    if (!string.IsNullOrWhiteSpace(databasePath)) Check("rich text section expansion native DXL", () =>
    {
        if (!databasePath.Contains("section-test", StringComparison.OrdinalIgnoreCase)) return;
        using var database = session.OpenDatabase(server, databasePath);
        using var notes = database.CreateNoteCollection(false);
        notes.SelectDocuments = true;
        notes.BuildCollection();
        var noteId = notes.FirstOrDefault();
        if (noteId == 0) throw new InvalidOperationException("Section DXL database has no documents.");
        using var document = database.GetDocumentByID(noteId) ?? throw new InvalidOperationException("Section DXL document missing.");
        using var item = document.GetFirstItem("Body") ?? throw new InvalidOperationException("Section DXL Body item missing.");
        using var body = item.GetRichTextItem() ?? throw new InvalidOperationException("Section DXL Body is not rich text.");
        using var navigator = body.CreateNavigator();
        if (!navigator.FindFirstElement(NotesRichTextNavigator.ElementSection)) throw new InvalidOperationException("Section CD record missing.");
        using var section = (NotesRichTextSection)navigator.GetElement()!;
        var initialExpanded = section.IsExpanded;
        section.IsExpanded = !initialExpanded;
        section.Title = "Updated section title";
        body.Update();
        using var reopened = database.GetDocumentByID(noteId)!;
        using var reopenedItem = reopened.GetFirstItem("Body")!;
        using var reopenedBody = reopenedItem.GetRichTextItem()!;
        using var reopenedNavigator = reopenedBody.CreateNavigator();
        if (!reopenedNavigator.FindFirstElement(NotesRichTextNavigator.ElementSection)) throw new InvalidOperationException("Section missing after update.");
        using var reopenedSection = (NotesRichTextSection)reopenedNavigator.GetElement()!;
        if (reopenedSection.IsExpanded == initialExpanded) throw new InvalidOperationException("Section expansion change was not persisted.");
        if (reopenedSection.Title != "Updated section title") throw new InvalidOperationException("Section title change was not persisted.");
    });
    if (!string.IsNullOrWhiteSpace(databasePath)) Check("rich text table cell clear native DXL", () =>
    {
        if (!databasePath.Contains("section-test", StringComparison.OrdinalIgnoreCase)) return;
        using var database = session.OpenDatabase(server, databasePath);
        using var notes = database.CreateNoteCollection(false);
        notes.SelectDocuments = true;
        notes.BuildCollection();
        var noteId = notes.FirstOrDefault();
        if (noteId == 0) throw new InvalidOperationException("Table DXL database has no document.");
        using var document = database.GetDocumentByID(noteId)!;
        using var item = document.GetFirstItem("Body") ?? throw new InvalidOperationException("Table DXL Body item missing.");
        using var body = item.GetRichTextItem()!;
        using var navigator = body.CreateNavigator();
        if (!navigator.FindFirstElement(NotesRichTextNavigator.ElementTable)) throw new InvalidOperationException("Table CD record missing.");
        using var table = (NotesRichTextTable)navigator.GetElement()!;
        var beforeRows = table.RowCount; var beforeColumns = table.ColumnCount;
        using var cell = table.GetCell(0, 0) ?? throw new InvalidOperationException("First table cell missing.");
        cell.Remove();
        body.Update();
        using var reopened = database.GetDocumentByID(noteId)!;
        using var reopenedItem = reopened.GetFirstItem("Body")!;
        using var reopenedBody = reopenedItem.GetRichTextItem()!;
        using var reopenedNavigator = reopenedBody.CreateNavigator();
        if (!reopenedNavigator.FindFirstElement(NotesRichTextNavigator.ElementTable)) throw new InvalidOperationException("Table missing after cell clear.");
        using var reopenedTable = (NotesRichTextTable)reopenedNavigator.GetElement()!;
        if (reopenedTable.RowCount != beforeRows || reopenedTable.ColumnCount != beforeColumns) throw new InvalidOperationException("Cell clear changed table dimensions.");
    });
    if (!string.IsNullOrWhiteSpace(databasePath)) Check("rich text append section native", () =>
    {
        using var database = session.OpenDatabase(server, databasePath);
        using var document = database.CreateDocument();
        try
        {
            using var body = document.CreateRichTextItem("Body");
            body.AppendSection("Created section", expanded: true);
            body.AppendSection("Collapsed section", expanded: false);
            document.Save();
            using var reopened = database.GetDocumentByID(document.NoteId)!;
            using var item = reopened.GetFirstItem("Body")!;
            using var savedBody = item.GetRichTextItem()!;
            using var navigator = savedBody.CreateNavigator();
            if (!navigator.FindFirstElement(NotesRichTextNavigator.ElementSection)) throw new InvalidOperationException("Appended section was not found.");
            using var section = (NotesRichTextSection)navigator.GetElement()!;
            if (!section.IsExpanded || section.Title != "Created section") throw new InvalidOperationException("Appended section metadata did not persist.");
            using (var titleStyle = session.CreateRichTextStyle()) { titleStyle.Bold = -1; titleStyle.FontSize = 16; section.TitleStyle = titleStyle; }
            reopened.Save();
            using var styled = database.GetDocumentByID(document.NoteId)!;
            using var styledItem = styled.GetFirstItem("Body")!;
            using var styledBody = styledItem.GetRichTextItem()!;
            using var styledNavigator = styledBody.CreateNavigator();
            if (!styledNavigator.FindFirstElement(NotesRichTextNavigator.ElementSection)) throw new InvalidOperationException("Styled section was not found.");
            using var styledSection = (NotesRichTextSection)styledNavigator.GetElement()!;
            using var persistedStyle = styledSection.TitleStyle;
            if (persistedStyle.Bold != -1 || persistedStyle.FontSize != 16) throw new InvalidOperationException("Section TitleStyle did not persist.");
            if (!styledNavigator.FindNextElement(NotesRichTextNavigator.ElementSection)) throw new InvalidOperationException("Second appended section was not found.");
            using var collapsed = (NotesRichTextSection)styledNavigator.GetElement()!;
            if (collapsed.IsExpanded || collapsed.Title != "Collapsed section") throw new InvalidOperationException("Collapsed section metadata did not persist.");
        }
        finally { if (!document.IsNewNote) document.Remove(true); }
    });
    if (!string.IsNullOrWhiteSpace(databasePath)) Check("rich text begin end section native", () =>
    {
        using var database = session.OpenDatabase(server, databasePath);
        using var document = database.CreateDocument();
        try
        {
            using var body = document.CreateRichTextItem("Body");
            body.BeginSection("Stateful section");
            body.AppendText("Text inside section");
            body.BeginSection("Nested section");
            body.AppendText("Nested text");
            body.EndSection();
            body.EndSection();
            document.Save();
            using var reopened = database.GetDocumentByID(document.NoteId)!;
            using var item = reopened.GetFirstItem("Body")!;
            using var savedBody = item.GetRichTextItem()!;
            using var navigator = savedBody.CreateNavigator();
            if (!navigator.FindFirstElement(NotesRichTextNavigator.ElementSection)) throw new InvalidOperationException("Stateful section was not found.");
            using var section = (NotesRichTextSection)navigator.GetElement()!;
            if (section.Title != "Stateful section") throw new InvalidOperationException("Stateful section title did not persist.");
            using var range = savedBody.CreateRange();
            if (!navigator.FindFirstString("Text inside section")) throw new InvalidOperationException("Section content did not persist.");
            range.SetBegin(navigator);
            if (!string.Equals(range.TextRun, "Text inside section", StringComparison.Ordinal)) throw new InvalidOperationException("Section content text did not persist.");
            if (!navigator.FindFirstElement(NotesRichTextNavigator.ElementSection) || !navigator.FindNextElement(NotesRichTextNavigator.ElementSection)) throw new InvalidOperationException("Nested section was not found.");
            using var nested = (NotesRichTextSection)navigator.GetElement()!;
            if (nested.Title != "Nested section") throw new InvalidOperationException("Nested section title did not persist.");
        }
        finally { if (!document.IsNewNote) document.Remove(true); }
    });
    if (!string.IsNullOrWhiteSpace(databasePath)) Check("rich text open section update guard", () =>
    {
        using var database = session.OpenDatabase(server, databasePath);
        using var document = database.CreateDocument();
        try
        {
            using var body = document.CreateRichTextItem("Body");
            body.BeginSection("Unclosed");
            ExpectException<InvalidOperationException>("Open section update guard", body.Update);
            body.EndSection();
        }
        finally { if (!document.IsNewNote) document.Remove(true); }
    });
    if (!string.IsNullOrWhiteSpace(databasePath)) Check("rich text open section compact guard", () =>
    {
        using var database = session.OpenDatabase(server, databasePath);
        using var document = database.CreateDocument();
        try
        {
            using var body = document.CreateRichTextItem("Body");
            body.BeginSection("Unclosed compact");
            ExpectException<InvalidOperationException>("Open section compact guard", body.Compact);
            body.EndSection();
            body.Compact();
        }
        finally { if (!document.IsNewNote) document.Remove(true); }
    });
    if (!string.IsNullOrWhiteSpace(databasePath)) Check("rich text unmatched section end guard", () =>
    {
        using var database = session.OpenDatabase(server, databasePath);
        using var document = database.CreateDocument();
        try
        {
            using var body = document.CreateRichTextItem("Body");
            ExpectException<InvalidOperationException>("Unmatched section end guard", body.EndSection);
        }
        finally { if (!document.IsNewNote) document.Remove(true); }
    });
    if (!string.IsNullOrWhiteSpace(databasePath)) Check("rich text document save section guard", () =>
    {
        using var database = session.OpenDatabase(server, databasePath);
        using var document = database.CreateDocument();
        try
        {
            using var body = document.CreateRichTextItem("Body");
            body.BeginSection("Unclosed save");
            ExpectException<InvalidOperationException>("Open section document save guard", document.Save);
            body.EndSection();
            document.Save();
        }
        finally { if (!document.IsNewNote) document.Remove(true); }
    });
    if (!string.IsNullOrWhiteSpace(databasePath)) Check("rich text document save insertion guard", () =>
    {
        using var database = session.OpenDatabase(server, databasePath);
        using var document = database.CreateDocument();
        try
        {
            using var body = document.CreateRichTextItem("Body");
            body.AppendText("Insertion guard");
            using var navigator = body.CreateNavigator();
            if (!navigator.FindFirstElement(NotesRichTextNavigator.ElementTextRun)) throw new InvalidOperationException("Insertion guard fixture missing.");
            document.Save();
            body.BeginInsert(navigator);
            ExpectException<InvalidOperationException>("Open insertion document save guard", document.Save);
            body.EndInsert();
            document.Save();
        }
        finally { if (!document.IsNewNote) document.Remove(true); }
    });
    if (!string.IsNullOrWhiteSpace(databasePath)) Check("view navigation", () =>
    {
        using var database = session.GetDatabase(server, databasePath, false) ?? throw new InvalidOperationException("Database not found.");
        foreach (var view in database.Views)
        {
            using (view)
            {
                using var first = view.GetFirstDocument();
                using var last = view.GetLastDocument();
                ExpectException<ArgumentOutOfRangeException>("Negative view max level", () => view.CreateViewNavMaxLevel(-1));
                using var navigator = view.CreateViewNav(0);
                ExpectException<ArgumentOutOfRangeException>("Negative view cache size", () => navigator.BufferMaxEntries = -1);
                ExpectException<ArgumentOutOfRangeException>("Oversized view cache size", () => navigator.BufferMaxEntries = 513);
                ExpectException<ArgumentException>("Blank view position", () => navigator.GetPos("   "));
                ExpectException<ArgumentNullException>("Null view position separator", () => navigator.GetPos("1", null!));
                if (last is not null)
                {
                    using var previous = view.GetPrevDocument(last);
                }
            }
        }
    });
    Check("argument validation", () =>
    {
        try { _ = session.HashPassword(null!); throw new InvalidOperationException("Null password was accepted."); }
        catch (ArgumentNullException ex) when (ex.ParamName == "password") { }
        try { _ = session.VerifyPassword("password", null!); throw new InvalidOperationException("Null password hash was accepted."); }
        catch (ArgumentNullException ex) when (ex.ParamName == "hashedPassword") { }
        try { _ = session.CreateName(null!); throw new InvalidOperationException("Null name was accepted."); }
        catch (ArgumentNullException ex) when (ex.ParamName == "value") { }
        using (var dateTime = session.CreateDateTime(null!))
        {
            if (dateTime.Value is null) throw new InvalidOperationException("CreateDateTime(null) did not create a current date/time value.");
        }
        using (var blankDateTime = session.CreateDateTime("   "))
        {
            if (blankDateTime.Value is null) throw new InvalidOperationException("CreateDateTime(blank) did not create a current date/time value.");
        }
        using var directory = session.GetDbDirectory(server);
        try { _ = directory.GetFirstDatabase(11); throw new InvalidOperationException("Invalid database type was accepted."); }
        catch (ArgumentOutOfRangeException ex) when (ex.ParamName == "type") { }
        try { _ = directory.OpenDatabase(null!); throw new InvalidOperationException("Null database path was accepted."); }
        catch (ArgumentException ex) when (ex.ParamName == "filePath") { }
        try { _ = session.OpenDatabase(server, null!); throw new InvalidOperationException("Null session database path was accepted."); }
        catch (ArgumentNullException ex) when (ex.ParamName == "filePath") { }
        try { _ = session.OpenByReplicaID(server, null!); throw new InvalidOperationException("Null replica ID was accepted."); }
        catch (ArgumentNullException ex) when (ex.ParamName == "replicaId") { }
        if (!string.IsNullOrWhiteSpace(databasePath))
        {
            using var database = session.GetDatabase(server, databasePath, false) ?? throw new InvalidOperationException("Database not found.");
            using var document = database.CreateDocument();
            try { _ = document.CopyToDatabase(null!); throw new InvalidOperationException("Null copy database was accepted."); }
            catch (ArgumentNullException ex) when (ex.ParamName == "d") { }
        }
        if (!string.IsNullOrWhiteSpace(databasePath))
        {
            using var database = session.GetDatabase(server, databasePath, false) ?? throw new InvalidOperationException("Database not found.");
            try { _ = database.Search(null!); throw new InvalidOperationException("Null formula was accepted."); }
            catch (ArgumentNullException ex) when (ex.ParamName == "formula") { }
            try { _ = database.FTSearch("   "); throw new InvalidOperationException("Blank full-text query was accepted."); }
            catch (ArgumentException ex) when (ex.ParamName == "query") { }
            try { _ = database.Search("@All", -1); throw new InvalidOperationException("Negative maxResults was accepted."); }
            catch (ArgumentOutOfRangeException ex) when (ex.ParamName == "maxResults") { }
            try { _ = database.GetDocumentByUNID(null!); throw new InvalidOperationException("Null UNID was accepted."); }
            catch (ArgumentNullException ex) when (ex.ParamName == "unid") { }
            try { _ = database.GetDocumentByID(0); throw new InvalidOperationException("Zero Note ID was accepted."); }
            catch (ArgumentOutOfRangeException ex) when (ex.ParamName == "id") { }
        }
    });
    if (!string.IsNullOrWhiteSpace(databasePath)) Check("null argument traps", () =>
    {
        using var database = session.GetDatabase(server, databasePath, false) ?? throw new InvalidOperationException("Database not found.");
        using var document = database.CreateDocument();
        ExpectException<ArgumentNullException>("SetValue null", () => document.SetValue("NullValue", null));
        ExpectException<ArgumentNullException>("SetString null", () => document.SetString("NullString", null!));
        ExpectException<ArgumentNullException>("UniversalId null", () => document.UniversalId = null!);
        using var stream = session.CreateStream();
        ExpectException<ArgumentNullException>("Write null", () => stream.Write((string)null!));
        ExpectException<ArgumentNullException>("WriteText null", () => stream.WriteText(null!));
    });
    if (!string.IsNullOrWhiteSpace(databasePath)) Check("agent null argument trap", () =>
    {
        using var database = session.GetDatabase(server, databasePath, false) ?? throw new InvalidOperationException("Database not found.");
        var agent = database.Agents.FirstOrDefault();
        if (agent is not null) ExpectException<ArgumentNullException>("ServerName null", () => agent.ServerName = null!);
    });
    if (!string.IsNullOrWhiteSpace(databasePath)) Check("DXL argument traps", () =>
    {
        using var database = session.GetDatabase(server, databasePath, false) ?? throw new InvalidOperationException("Database not found.");
        using var importer = session.CreateDXLImporter();
        using var exporter = session.CreateDXLExporter();
        ExpectException<ArgumentNullException>("DXL import path null", () => importer.Import((string)null!, database));
        ExpectException<ArgumentNullException>("DXL design name null", () => exporter.ExportDesignElement(database, null!, "form"));
        ExpectException<ArgumentNullException>("DXL design type null", () => exporter.ExportDesignElement(database, "Main", null!));
        ExpectException<ArgumentNullException>("DXL export path null", () => exporter.ExportDatabaseDesign(database, null!));
    });
    Check("exception trapping", () =>
    {
        ExpectException<ArgumentNullException>("null password is trapped", () => session.HashPassword(null!));
        ExpectException<ArgumentNullException>("null name is trapped", () => session.CreateName(null!));
        ExpectException<ArgumentNullException>("null replica ID is trapped", () => session.OpenByReplicaID(server, null!));
        Console.WriteLine("PASS exception trapping continued after invalid calls");
    });
    if (!string.IsNullOrWhiteSpace(databasePath)) Check("database and document surface", () => { using var database = session.GetDatabase(server, databasePath, false) ?? throw new InvalidOperationException("Database not found."); if (!database.IsOpen) throw new InvalidOperationException(database.OpenError); _ = database.Title; _ = database.Categories; _ = database.TemplateName; _ = database.DesignTemplateName; _ = database.ReplicaID; _ = database.Size; _ = database.PercentUsed; _ = database.CurrentAccessLevel; _ = database.Created; _ = database.LastModified; _ = database.FileFormat; using var access = database.QueryAccess(); _ = access.Level; _ = access.LevelName; _ = access.AccessFlags; _ = access.NamesList; _ = access.Groups; _ = access.Roles; using var document = database.CreateDocument(); document.ReplaceItemValue("Form", "FullTest").Recycle(); document.ReplaceItemValue("Subject", "Full test").Recycle(); if (!document.HasItem("Subject") || document.GetString("Subject") != "Full test") throw new InvalidOperationException("Document item round trip failed."); using var mime = document.CreateMIMEEntity(); var bodyItem = document.GetFirstItem("Body") ?? throw new InvalidOperationException("MIME Body item was not created."); using var itemMime = bodyItem.GetMIMEEntity() ?? throw new InvalidOperationException("NotesItem.GetMIMEEntity did not open the MIME item."); if (!itemMime.ContentType.Equals(mime.ContentType, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("NotesItem MIME entity did not resolve the same content type."); _ = mime.GetChildren(); _ = mime.GetHeaders(); using var content = session.CreateStream(); content.WriteText("MIME full test"); content.Position = 0; mime.SetContentFromText(content, "text/plain; charset=utf-8", 1727); try { mime.EncodeContent(11); throw new InvalidOperationException("Invalid MIME encoding was accepted."); } catch (ArgumentOutOfRangeException ex) when (ex.ParamName == "encoding") { } });
    if (!string.IsNullOrWhiteSpace(databasePath)) Check("NotesItem MIME property", () =>
    {
        using var database = session.GetDatabase(server, databasePath, false) ?? throw new InvalidOperationException("Database not found.");
        using var document = database.CreateDocument();
        using var mime = document.CreateMIMEEntity();
        using var item = document.GetFirstItem("Body") ?? throw new InvalidOperationException("MIME Body item was not created.");
        if (!item.IsMIME) throw new InvalidOperationException("NotesItem.IsMIME returned false for a native MIME item.");
        if (item.GetMIMEEntity() is null) throw new InvalidOperationException("NotesItem.GetMIMEEntity returned null.");
    });
    if (!string.IsNullOrWhiteSpace(databasePath)) Check("nested MIME child guards", () =>
    {
        using var database = session.GetDatabase(server, databasePath, false) ?? throw new InvalidOperationException("Database not found.");
        using var document = database.CreateDocument();
        using var mime = document.CreateMIMEEntity();
        const string boundary = "dotnet-child-test";
        var raw = "--" + boundary + "\r\nContent-Type: text/plain\r\nX-Test: remove-me\r\n\r\nfirst\r\n--" + boundary + "\r\nContent-Type: multipart/mixed; boundary=\"inner-test\"\r\n\r\n--inner-test\r\nContent-Type: text/plain\r\n\r\nnested\r\n--inner-test--\r\n--" + boundary + "--\r\n";
        using var stream = session.CreateStream();
        stream.Charset = "UTF-8";
        stream.WriteText(raw);
        stream.Position = 0;
        mime.SetContentFromBytes(stream, "multipart/mixed; boundary=\"" + boundary + "\"", 1725);
        var children = mime.GetChildren();
        if (children.Count != 2) throw new InvalidOperationException($"Expected two MIME child entities, found {children.Count}.");
        using var nested = children[1].GetFirstChildEntity() ?? throw new InvalidOperationException("Nested MIME child was not found.");
        if (!nested.ContentAsText.Contains("nested", StringComparison.Ordinal)) throw new InvalidOperationException("Nested MIME child content read failed.");
        using var child = children[0];
        if (!child.ContentAsText.Contains("first", StringComparison.Ordinal)) throw new InvalidOperationException("Child content read failed.");
        using var childTypeHeader = child.GetNthHeader("Content-Type") ?? throw new InvalidOperationException("Child Content-Type header was not found.");
        if (!childTypeHeader.GetHeaderVal().Contains("text/plain", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Child header lookup failed.");
        child.RemoveHeaders("X-Test");
    });
    if (!string.IsNullOrWhiteSpace(databasePath)) Check("MIME child creation", () =>
    {
        using var database = session.GetDatabase(server, databasePath, false) ?? throw new InvalidOperationException("Database not found.");
        using var document = database.CreateDocument();
        using var mime = document.CreateMIMEEntity();
        const string boundary = "dotnet-child-create-test";
        var raw = "--" + boundary + "\r\nContent-Type: text/plain\r\n\r\nfirst\r\n--" + boundary + "--\r\n";
        using var stream = session.CreateStream();
        stream.Charset = "UTF-8";
        stream.WriteText(raw);
        stream.Position = 0;
        mime.SetContentFromBytes(stream, "multipart/mixed; boundary=\"" + boundary + "\"", 1725);
        using var created = mime.CreateChildEntity();
        if (mime.GetChildren().Count != 2) throw new InvalidOperationException("CreateChildEntity did not add a direct child.");
        if (!created.ContentType.Equals("text", StringComparison.OrdinalIgnoreCase) || !created.ContentSubType.Equals("plain", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Created MIME child has an unexpected content type: " + created.ContentType + "/" + created.ContentSubType);
    });
    if (!string.IsNullOrWhiteSpace(databasePath)) Check("MIME child append", () =>
    {
        using var database = session.GetDatabase(server, databasePath, false) ?? throw new InvalidOperationException("Database not found.");
        using var document = database.CreateDocument();
        using var mime = document.CreateMIMEEntity();
        const string boundary = "dotnet-append-test";
        using var stream = session.CreateStream();
        stream.Charset = "UTF-8";
        stream.WriteText("--" + boundary + "\r\nContent-Type: text/plain\r\n\r\nfirst\r\n--" + boundary + "\r\nContent-Type: text/plain\r\n\r\nsecond\r\n--" + boundary + "--\r\n");
        stream.Position = 0;
        mime.SetContentFromBytes(stream, "multipart/mixed; boundary=\"" + boundary + "\"", 1725);
        var children = mime.GetChildren();
        if (children.Count != 2) throw new InvalidOperationException("Append test MIME children were not created.");
        mime.AppendChildEntity(children[0]);
    });
    if (!string.IsNullOrWhiteSpace(databasePath)) Check("user document hotspot links", () =>
    {
        using var database = session.GetDatabase(server, databasePath, false) ?? throw new InvalidOperationException("Database not found.");
        using var view = database.GetView("testdocs") ?? throw new InvalidOperationException("View testdocs not found.");
        using var document = view.GetFirstDocument() ?? throw new InvalidOperationException("View testdocs has no documents.");
        NotesDocument? target = null;
        for (var current = document; current is not null; current = view.GetNextDocument(current))
        {
            var requestedSubject = Environment.GetEnvironmentVariable("NOTES_TEST_DOCUMENT") ?? "testdoc 3";
            if (current.GetString("Subject").Equals(requestedSubject, StringComparison.OrdinalIgnoreCase)) { target = current; break; }
        }
        using (target ?? throw new InvalidOperationException("Requested test document not found."))
        using (var item = target.GetFirstItem("Body") ?? throw new InvalidOperationException("Body item not found."))
        using (var body = item.GetRichTextItem() ?? throw new InvalidOperationException("Body is not rich text."))
        using (var navigator = body.CreateNavigator())
        {
            var links = new List<NotesRichTextDocLink>();
            for (var link = navigator.GetFirstElement(NotesRichTextNavigator.ElementDocLink) as NotesRichTextDocLink; link is not null; link = navigator.GetNextElement(NotesRichTextNavigator.ElementDocLink) as NotesRichTextDocLink)
                links.Add(link);
            if (links.Count < 1) throw new InvalidOperationException("No document links found.");
            Console.WriteLine($"  Found {links.Count} links: {string.Join(" | ", links.Select(x => x.HotSpotText))}");
            foreach (var (link, index) in links.Select((value, index) => (value, index + 1)))
                Console.WriteLine($"  Link {index}: replica={link.DbReplicaID}, view={link.ViewUNID}, doc={link.DocUNID}, comment='{link.DisplayComment}', server='{link.ServerHint}', hotspot='{link.HotSpotText}'");
            var databaseHotspot = links.FirstOrDefault(link => (string.IsNullOrWhiteSpace(link.DocUNID) || link.DocUNID.Trim('0').Length == 0) && !string.IsNullOrWhiteSpace(link.DbReplicaID));
            var replacementHotspot = Environment.GetEnvironmentVariable("NOTES_TEST_HOTSPOT_TEXT") ?? "testad hotspot Åäö";
            var expectedVisibleHotspot = Environment.GetEnvironmentVariable("NOTES_TEST_SET_HOTSPOT_TEXT") == "1" ? replacementHotspot : "hotspot länk";
            if (databaseHotspot is not null && Environment.GetEnvironmentVariable("NOTES_TEST_SET_HOTSPOT_TEXT") != "1" && string.Equals(Environment.GetEnvironmentVariable("NOTES_TEST_DOCUMENT"), "testdoc 2", StringComparison.OrdinalIgnoreCase) && databaseHotspot.HotSpotText != expectedVisibleHotspot)
                throw new InvalidOperationException($"Database hotspot visible text was not decoded from the HOTLINK span: '{databaseHotspot.HotSpotText}'.");
            if (string.Equals(Environment.GetEnvironmentVariable("NOTES_TEST_DOCUMENT"), "testdoc 2", StringComparison.OrdinalIgnoreCase) && links.Count >= 3 && !string.IsNullOrEmpty(links[1].HotSpotText))
                throw new InvalidOperationException($"A preceding doclink incorrectly inherited hotspot text: '{links[1].HotSpotText}'.");
            if (databaseHotspot is not null)
            {
                using var hotspotStyle = databaseHotspot.HotSpotTextStyle;
                if (hotspotStyle.Bold is < -1 or > 255 || hotspotStyle.FontSize is < 0 or > 255)
                    throw new InvalidOperationException("Hotspot style returned an invalid native value.");
                Console.WriteLine($"  Hotspot style: font={hotspotStyle.NotesFont}, size={hotspotStyle.FontSize}, bold={hotspotStyle.Bold}, color={hotspotStyle.NotesColor}");
                if (Environment.GetEnvironmentVariable("NOTES_TEST_SET_HOTSPOT_STYLE") == "1")
                {
                    var originalBold = hotspotStyle.Bold;
                    using var changedStyle = session.CreateRichTextStyle();
                    changedStyle.Bold = int.TryParse(Environment.GetEnvironmentVariable("NOTES_TEST_HOTSPOT_BOLD"), out var requestedBold) ? requestedBold : -1;
                    databaseHotspot.SetHotSpotTextStyle(changedStyle);
                    body.Update(); target.Save();
                    using var reopenedStyleDocument = database.GetDocumentByUNID(target.UniversalID) ?? throw new InvalidOperationException("Hotspot style test document could not be reopened.");
                    using var reopenedStyleItem = reopenedStyleDocument.GetFirstItem("Body") ?? throw new InvalidOperationException("Hotspot style Body item missing after reopen.");
                    using var reopenedStyleBody = reopenedStyleItem.GetRichTextItem() ?? throw new InvalidOperationException("Hotspot style Body is not rich text after reopen.");
                    using var reopenedStyleNavigator = reopenedStyleBody.CreateNavigator();
                    var reopenedStyleLink = reopenedStyleNavigator.GetFirstElement(NotesRichTextNavigator.ElementDocLink) as NotesRichTextDocLink;
                    while (reopenedStyleLink is not null && !(string.IsNullOrWhiteSpace(reopenedStyleLink.DocUNID) || reopenedStyleLink.DocUNID.Trim('0').Length == 0))
                    {
                        var next = reopenedStyleNavigator.GetNextElement(NotesRichTextNavigator.ElementDocLink) as NotesRichTextDocLink;
                        reopenedStyleLink.Dispose(); reopenedStyleLink = next;
                    }
                    if (reopenedStyleLink is null || reopenedStyleLink.HotSpotTextStyle.Bold != changedStyle.Bold) throw new InvalidOperationException("Hotspot bold style did not persist after native save/reopen.");
                    reopenedStyleLink.Dispose();
                    Console.WriteLine("  VERIFIED hotspot style mutation after native save/reopen.");
                    return;
                }
            }
            if (databaseHotspot is not null && Environment.GetEnvironmentVariable("NOTES_TEST_SET_HOTSPOT_TEXT") == "1")
            {
                databaseHotspot.SetHotSpotText(replacementHotspot);
                body.Update(); target.Save();
                using var reopenedHotspotDocument = database.GetDocumentByUNID(target.UniversalID) ?? throw new InvalidOperationException("Hotspot text test document could not be reopened.");
                using var reopenedHotspotItem = reopenedHotspotDocument.GetFirstItem("Body") ?? throw new InvalidOperationException("Hotspot text Body item missing after reopen.");
                using var reopenedHotspotBody = reopenedHotspotItem.GetRichTextItem() ?? throw new InvalidOperationException("Hotspot text Body is not rich text after reopen.");
                using var reopenedHotspotNavigator = reopenedHotspotBody.CreateNavigator();
                NotesRichTextDocLink? reopenedHotspotLink = reopenedHotspotNavigator.GetFirstElement(NotesRichTextNavigator.ElementDocLink) as NotesRichTextDocLink;
                while (reopenedHotspotLink is not null && !(string.IsNullOrWhiteSpace(reopenedHotspotLink.DocUNID) || reopenedHotspotLink.DocUNID.Trim('0').Length == 0))
                {
                    var next = reopenedHotspotNavigator.GetNextElement(NotesRichTextNavigator.ElementDocLink) as NotesRichTextDocLink;
                    reopenedHotspotLink.Dispose(); reopenedHotspotLink = next;
                }
                if (reopenedHotspotLink is null || reopenedHotspotLink.HotSpotText != replacementHotspot) throw new InvalidOperationException($"Visible hotspot text did not persist after native save/reopen: '{reopenedHotspotLink?.HotSpotText}'.");
                reopenedHotspotLink.Dispose();
                Console.WriteLine("  VERIFIED visible hotspot text mutation after native save/reopen.");
                return;
            }
            var positionRecord = typeof(NotesRichTextDocLink).GetMethod("PositionRecord", BindingFlags.Instance | BindingFlags.NonPublic);
            if (Environment.GetEnvironmentVariable("NOTES_TEST_DUMP_CD") == "1")
            {
                var read = typeof(NotesRichTextItem).GetMethod("ReadRichTextRecords", BindingFlags.Instance | BindingFlags.NonPublic)!;
                foreach (var record in (System.Collections.IEnumerable)read.Invoke(body, null)!)
                {
                    var type = record.GetType();
                    object? Field(string name) => type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(record);
                    var bytes = (byte[])Field("Data")!;
                    var textValue = Field("Text") as string;
                    Console.WriteLine($"  CD {Field("SegmentIndex")}:{Field("RecordIndex")} sig={Field("Signature")} len={bytes.Length} textLen={textValue?.Length ?? 0} text='{textValue}' header={Convert.ToHexString(bytes.AsSpan(0, Math.Min(bytes.Length, 12)))}");
                }
            }
            foreach (var (link, index) in links.Select((value, index) => (value, index + 1)))
            {
                var record = positionRecord?.Invoke(link, null);
                var signature = record?.GetType().GetProperty("Signature", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(record);
                var data = record?.GetType().GetProperty("Data", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(record) as byte[];
                Console.WriteLine($"  Record {index}: signature={signature}, bytes={data?.Length}");
            }
            using var exporter = session.CreateDXLExporter();
            exporter.OutputDOCTYPE = false;
            using var exported = exporter.Export(target);
            var xml = System.Xml.Linq.XDocument.Load(new MemoryStream(exported.ToArray()));
            var dxlLinks = xml.Descendants().Where(e => e.Name.LocalName is "doclink" or "viewlink" or "databaselink").ToArray();
            Console.WriteLine($"  DXL links: {string.Join(", ", dxlLinks.GroupBy(e => e.Name.LocalName).Select(g => g.Key + "=" + g.Count()))}");
            foreach (var (element, index) in dxlLinks.Select((value, index) => (value, index + 1)))
                Console.WriteLine($"  DXL link {index}: <{element.Name.LocalName}> {string.Join(" ", element.Attributes().Select(a => a.Name.LocalName + "='" + a.Value + "'"))} text='{element.Value.Trim()}'");
            if (string.Equals(Environment.GetEnvironmentVariable("NOTES_TEST_MUTATE_LINK"), "1", StringComparison.Ordinal))
            {
                var linkKind = Environment.GetEnvironmentVariable("NOTES_TEST_LINK_KIND") ?? "document";
                var targetLink = linkKind.Equals("database", StringComparison.OrdinalIgnoreCase)
                    ? links.FirstOrDefault(link => (string.IsNullOrWhiteSpace(link.DocUNID) || link.DocUNID.Trim('0').Length == 0) && !string.IsNullOrWhiteSpace(link.DbReplicaID))
                    : links.FirstOrDefault(link => !string.IsNullOrWhiteSpace(link.DocUNID));
                if (targetLink is null) throw new InvalidOperationException($"No {linkKind} link available for mutation.");
                var selectedIndex = links.IndexOf(targetLink);
                var expected = new System.Xml.Linq.XElement(dxlLinks[selectedIndex]);
                var readRecords = typeof(NotesRichTextItem).GetMethod("ReadRichTextRecords", BindingFlags.Instance | BindingFlags.NonPublic)!;
                byte[][] Snapshot() => ((System.Collections.IEnumerable)readRecords.Invoke(body, null)!).Cast<object>()
                    .Select(r => (byte[])r.GetType().GetProperty("Data", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.GetValue(r)!).Select(bytes => bytes.ToArray()).ToArray();
                var beforeRecords = Snapshot();
                var selectedRecord = positionRecord!.Invoke(targetLink, null)!;
                var selectedBytes = (byte[])selectedRecord.GetType().GetProperty("Data", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.GetValue(selectedRecord)!;
                var physicalIndex = Array.FindIndex(beforeRecords, bytes => bytes.SequenceEqual(selectedBytes));
                if (physicalIndex < 0) throw new InvalidOperationException("Selected link record not found.");
                targetLink.SetLinkMetadata("Verified Åäö", targetLink.ServerHint, "verified anchor");
                var afterRecords = Snapshot();
                if (beforeRecords.Length != afterRecords.Length || beforeRecords.Where((bytes, index) => index != physicalIndex && !bytes.SequenceEqual(afterRecords[index])).Any())
                    throw new InvalidOperationException("Metadata mutation changed an unrelated CD record.");
                if (!beforeRecords[physicalIndex].AsSpan(0, 2).SequenceEqual(afterRecords[physicalIndex].AsSpan(0, 2)) ||
                    !beforeRecords[physicalIndex].AsSpan(4, 2).SequenceEqual(afterRecords[physicalIndex].AsSpan(4, 2)))
                    throw new InvalidOperationException("Metadata mutation changed signature or LinkID.");
                body.Update();
                target.Save();
                using var reopened = database.GetDocumentByUNID(target.UniversalID) ?? throw new InvalidOperationException("Mutated document could not be reopened.");
                using var reopenedItem = reopened.GetFirstItem("Body") ?? throw new InvalidOperationException("Mutated Body item missing.");
                using var reopenedBody = reopenedItem.GetRichTextItem() ?? throw new InvalidOperationException("Mutated Body is not rich text.");
                using var reopenedNavigator = reopenedBody.CreateNavigator();
                using var finalExport = exporter.Export(reopened);
                var finalXml = System.Xml.Linq.XDocument.Load(new MemoryStream(finalExport.ToArray()));
                var finalLinks = finalXml.Descendants().Where(e => e.Name.LocalName is "doclink" or "viewlink" or "databaselink").ToArray();
                NotesRichTextDocLink? finalMetadata = reopenedNavigator.GetFirstElement(NotesRichTextNavigator.ElementDocLink) as NotesRichTextDocLink;
                for (var i = 0; i < selectedIndex; i++)
                {
                    finalMetadata?.Dispose();
                    finalMetadata = reopenedNavigator.GetNextElement(NotesRichTextNavigator.ElementDocLink) as NotesRichTextDocLink;
                }
                using (finalMetadata)
                    if (finalMetadata is null || finalMetadata.DisplayComment != "Verified Åäö" || finalMetadata.Anchor != "verified anchor")
                        throw new InvalidOperationException("Link metadata did not persist after reopen.");
                if (finalLinks.Length != dxlLinks.Length) throw new InvalidOperationException("Mutation changed link count.");
                expected.Attribute("description")?.Remove(); expected.Attribute("anchor")?.Remove();
                finalLinks[selectedIndex].Attribute("description")?.Remove(); finalLinks[selectedIndex].Attribute("anchor")?.Remove();
                for (var i = 0; i < dxlLinks.Length; i++)
                    if (!System.Xml.Linq.XNode.DeepEquals(i == selectedIndex ? expected : dxlLinks[i], finalLinks[i]))
                        throw new InvalidOperationException($"Unexpected DXL change in link {i}: {finalLinks[i]}");
                Console.WriteLine("  VERIFIED metadata after native save/reopen; visible text, link targets and unrelated records preserved.");
            }
            foreach (var link in links) link.Dispose();
        }
    });
    if (!string.IsNullOrWhiteSpace(databasePath)) Check("rich text NotesURL UNID", () =>
    {
        using var database = session.GetDatabase(server, databasePath, false) ?? throw new InvalidOperationException("Database not found.");
        using var source = database.CreateDocument();
        source.SetString("Subject", "NotesURL target");
        source.Save();
        using var document = database.CreateDocument();
        using var body = document.CreateRichTextItem("Body");
        var localPath = databasePath.Replace('\\', '/').TrimStart('/');
        body.AppendNotesURL($"notes:///{localPath}/0/{source.UniversalID}", "UNID URL");
        body.Update();
        document.Save();
        using var reopened = database.GetDocumentByUNID(document.UniversalID) ?? throw new InvalidOperationException("NotesURL document did not reopen.");
        using var item = reopened.GetFirstItem("Body") ?? throw new InvalidOperationException("NotesURL Body missing.");
        using var richText = item.GetRichTextItem() ?? throw new InvalidOperationException("NotesURL Body is not rich text.");
        using var navigator = richText.CreateNavigator();
        using var link = navigator.GetFirstElement(NotesRichTextNavigator.ElementDocLink) as NotesRichTextDocLink ?? throw new InvalidOperationException("NotesURL did not create a document link.");
        if (!link.DocUNID.Equals(source.UniversalID, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException($"NotesURL resolved {link.DocUNID}, expected {source.UniversalID}.");
        document.Remove(true);
        source.Remove(true);
    });
    if (!string.IsNullOrWhiteSpace(databasePath)) Check("rich text NotesURL database and view", () =>
    {
        using var database = session.GetDatabase(server, databasePath, false) ?? throw new InvalidOperationException("Database not found.");
        using var document = database.CreateDocument();
        using var body = document.CreateRichTextItem("Body");
        var localPath = databasePath.Replace('\\', '/').TrimStart('/');
        body.AppendNotesURL($"notes:///{localPath}", "database URL");
        body.AppendNotesURL($"notes:///{localPath}/testdocs", "view URL");
        body.Update();
        document.Save();
        using var reopened = database.GetDocumentByUNID(document.UniversalID) ?? throw new InvalidOperationException("NotesURL database/view document did not reopen.");
        using var item = reopened.GetFirstItem("Body") ?? throw new InvalidOperationException("NotesURL database/view Body missing.");
        using var richText = item.GetRichTextItem() ?? throw new InvalidOperationException("NotesURL database/view Body is not rich text.");
        using var navigator = richText.CreateNavigator();
        using var first = navigator.GetFirstElement(NotesRichTextNavigator.ElementDocLink) as NotesRichTextDocLink ?? throw new InvalidOperationException("Database URL did not create a link.");
        using var second = navigator.GetNextElement(NotesRichTextNavigator.ElementDocLink) as NotesRichTextDocLink ?? throw new InvalidOperationException("View URL did not create a link.");
        if (first.DocUNID.Trim('0').Length != 0 || second.DocUNID.Trim('0').Length != 0) throw new InvalidOperationException("Database/view URL unexpectedly resolved a document.");
        document.Remove(true);
    });
    if (!string.IsNullOrWhiteSpace(databasePath)) Check("rich text doclink RemoveLinkage", () =>
    {
        using var database = session.GetDatabase(server, databasePath, false) ?? throw new InvalidOperationException("Database not found.");
        using var source = database.CreateDocument(); source.SetString("Subject", "RemoveLinkage target"); source.Save();
        using var document = database.CreateDocument();
        using var body = document.CreateRichTextItem("Body"); body.AppendDocLink(source, "remove target"); body.Update(); document.Save();
        using var reopened = database.GetDocumentByUNID(document.UniversalID) ?? throw new InvalidOperationException("RemoveLinkage document did not reopen.");
        using var item = reopened.GetFirstItem("Body") ?? throw new InvalidOperationException("RemoveLinkage Body missing.");
        using var richText = item.GetRichTextItem() ?? throw new InvalidOperationException("RemoveLinkage Body is not rich text.");
        using var navigator = richText.CreateNavigator();
        using var link = navigator.GetFirstElement(NotesRichTextNavigator.ElementDocLink) as NotesRichTextDocLink ?? throw new InvalidOperationException("RemoveLinkage link missing.");
        link.RemoveLinkage(); richText.Update(); reopened.Save();
        using var final = database.GetDocumentByUNID(document.UniversalID) ?? throw new InvalidOperationException("RemoveLinkage result missing.");
        using var finalItem = final.GetFirstItem("Body") ?? throw new InvalidOperationException("RemoveLinkage result Body missing.");
        using var finalBody = finalItem.GetRichTextItem() ?? throw new InvalidOperationException("RemoveLinkage result is not rich text.");
        using var finalNavigator = finalBody.CreateNavigator();
        using var finalLink = finalNavigator.GetFirstElement(NotesRichTextNavigator.ElementDocLink) as NotesRichTextDocLink ?? throw new InvalidOperationException("RemoveLinkage removed the hotspot record.");
        if (finalLink.DocUNID.Trim('0').Length != 0 || finalLink.DisplayComment != "remove target") throw new InvalidOperationException("RemoveLinkage did not retain hotspot text while clearing the target.");
        document.Remove(true); source.Remove(true);
    });
}

if (executedChecks == 0) failures.Add("No test groups matched NOTES_TEST_FILTER.");
var runLabel = string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("NOTES_TEST_FILTER")) ? "FULL TEST" : "SELECTED TESTS";
if (failures.Count != 0) { Console.Error.WriteLine($"{runLabel} FAILED ({failures.Count} error(s), {executedChecks} checks)"); Environment.ExitCode = 1; } else Console.WriteLine($"{runLabel} PASSED ({executedChecks} checks)");
