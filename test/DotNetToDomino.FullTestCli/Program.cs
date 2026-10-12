using System.Reflection;
using DotNetToDomino;

var failures = new List<string>();
void Check(string name, Action action)
{
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
        ("NotesDocument.GetMIMEEntity", typeof(NotesDocument).GetMethod(nameof(NotesDocument.GetMIMEEntity), Type.EmptyTypes)),
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
    using var session = new NotesSession(runtime);
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
    Check("password round trip", () => { const string password = "DotNetToDomino-FullTest"; var digest = session.HashPassword(password); if (!session.VerifyPassword(password, digest)) throw new InvalidOperationException("Password verification failed."); });
    Check("address book and directory", () => { foreach (var book in session.AddressBooks) using (book) { _ = book.Server; _ = book.FilePath; } using var directory = session.GetDbDirectory(server); _ = directory.Name; using var database = directory.GetFirstDatabase(NotesConst.DATABASE); if (database is not null) { _ = database.FilePath; _ = database.FileName; } using var next = directory.GetNextDatabase(); if (next is not null) { _ = next.FilePath; _ = next.FileName; } });
    if (!string.IsNullOrWhiteSpace(databasePath)) Check("rich text range surface", () =>
    {
        using var database = session.GetDatabase(server, databasePath, false) ?? throw new InvalidOperationException("Database not found.");
        ExpectException<ArgumentNullException>("Database Title null", () => database.Title = null!);
        ExpectException<ArgumentNullException>("Database Categories null", () => database.Categories = null!);
        ExpectException<ArgumentNullException>("Database TemplateName null", () => database.TemplateName = null!);
        ExpectException<ArgumentNullException>("Database DesignTemplateName null", () => database.DesignTemplateName = null!);
        ExpectException<ArgumentNullException>("Database ReplicaID null", () => database.SetReplicaId(null!));
        using var document = database.CreateDocument();
        using var richText = document.CreateRichTextItem("Body");
        richText.AppendText("Rich text full test");
        using var paragraphStyle = session.CreateRichTextParagraphStyle();
        ExpectException<NotSupportedException>("AppendParagraphStyle ABI guard", () => richText.AppendParagraphStyle(paragraphStyle));
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
            style.Bold = 1;
            range.SetStyle(style);
        }
    });
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
    if (!string.IsNullOrWhiteSpace(databasePath)) Check("database and document surface", () => { using var database = session.GetDatabase(server, databasePath, false) ?? throw new InvalidOperationException("Database not found."); if (!database.IsOpen) throw new InvalidOperationException(database.OpenError); _ = database.Title; _ = database.Categories; _ = database.TemplateName; _ = database.DesignTemplateName; _ = database.ReplicaID; _ = database.Size; _ = database.PercentUsed; _ = database.CurrentAccessLevel; _ = database.Created; _ = database.LastModified; _ = database.FileFormat; using var access = database.QueryAccess(); _ = access.Level; _ = access.LevelName; _ = access.AccessFlags; _ = access.NamesList; _ = access.Groups; _ = access.Roles; using var document = database.CreateDocument(); document.ReplaceItemValue("Form", "FullTest").Recycle(); document.ReplaceItemValue("Subject", "Full test").Recycle(); if (!document.HasItem("Subject") || document.GetString("Subject") != "Full test") throw new InvalidOperationException("Document item round trip failed."); using var mime = document.CreateMIMEEntity(); _ = mime.GetChildren(); _ = mime.GetHeaders(); using var content = session.CreateStream(); content.WriteText("MIME full test"); mime.SetContentFromText(content, "text/plain; charset=utf-8", 1727); try { mime.EncodeContent(11); throw new InvalidOperationException("Invalid MIME encoding was accepted."); } catch (ArgumentOutOfRangeException ex) when (ex.ParamName == "encoding") { } });
    if (!string.IsNullOrWhiteSpace(databasePath)) Check("nested MIME child guards", () =>
    {
        using var database = session.GetDatabase(server, databasePath, false) ?? throw new InvalidOperationException("Database not found.");
        using var document = database.CreateDocument();
        using var mime = document.CreateMIMEEntity();
        const string boundary = "dotnet-child-test";
        var raw = "--" + boundary + "\r\nContent-Type: text/plain\r\n\r\nfirst\r\n--" + boundary + "\r\nContent-Type: text/plain\r\n\r\nsecond\r\n--" + boundary + "--\r\n";
        using var stream = session.CreateStream();
        stream.WriteText(raw);
        mime.SetContentFromBytes(stream, "multipart/mixed; boundary=\"" + boundary + "\"", 1725);
        var children = mime.GetChildren();
        if (children.Count != 2) throw new InvalidOperationException($"Expected two MIME child entities, found {children.Count}.");
        using var child = children[0];
        ExpectException<NotSupportedException>("Child content read guard", () => _ = child.ContentAsText);
        using var childContent = session.CreateStream();
        childContent.WriteText("child update");
        ExpectException<NotSupportedException>("Child content write guard", () => child.SetContentFromText(childContent, "text/plain", 1725));
        ExpectException<NotSupportedException>("Child header lookup guard", () => child.GetHeaders());
        ExpectException<NotSupportedException>("Child header creation guard", () => child.CreateHeader("X-Test"));
        ExpectException<NotSupportedException>("Child header removal guard", () => child.RemoveHeaders("X-Test"));
        ExpectException<NotSupportedException>("Child removal guard", child.Remove);
    });
}

if (failures.Count != 0) { Console.Error.WriteLine($"FULL TEST FAILED ({failures.Count} error(s))"); Environment.ExitCode = 1; } else Console.WriteLine("FULL TEST PASSED");
