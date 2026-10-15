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
        if (typeof(NotesRichTextTable).GetProperty(property) is not null) throw new InvalidOperationException($"Unsupported NotesRichTextTable.{property} leaked into the public API.");
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
    ObjectPropertySmokeTests.RoundTrips(session, server, databasePath, Check);
    ObjectPropertySmokeTests.Run(session, server, databasePath, Check);
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
    if (!string.IsNullOrWhiteSpace(databasePath)) Check("rich text table persistence and native DXL", () =>
    {
        using var database = session.OpenDatabase(server, databasePath);
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
        foreach (var (marker, expectedBold) in new[] { ("LARGE-RED", false), ("BOLD-LARGE-RED", true), ("UNCHANGED-BOLD-LARGE-RED", true), ("PLAIN-LARGE-RED", false), ("ALIAS-BOLD-LARGE-RED", true) })
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
        }
        finally
        {
            foreach (var view in views) view.Dispose();
            if (!document.IsNewNote) document.Remove(true);
        }
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
}

if (executedChecks == 0) failures.Add("No test groups matched NOTES_TEST_FILTER.");
var runLabel = string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("NOTES_TEST_FILTER")) ? "FULL TEST" : "SELECTED TESTS";
if (failures.Count != 0) { Console.Error.WriteLine($"{runLabel} FAILED ({failures.Count} error(s), {executedChecks} checks)"); Environment.ExitCode = 1; } else Console.WriteLine($"{runLabel} PASSED ({executedChecks} checks)");
