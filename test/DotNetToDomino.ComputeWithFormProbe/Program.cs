using DotNetToDomino;

var runtime = args.Length > 0 ? args[0] : @"D:\Program Files\HCL\Notes";
var notesIni = args.Length > 1 ? args[1] : Path.Combine(runtime, "notes.ini");
var password = args.Length > 2
    ? args[2]
    : File.ReadLines(notesIni).First(line => line.StartsWith("ID_Pass=", StringComparison.Ordinal)).Substring("ID_Pass=".Length);
using var session = new NotesSession(runtime, notesIni, password);
var path = Path.Combine(Path.GetTempPath(), $"compute-with-form-probe-{Guid.NewGuid():N}.nsf");
using var database = session.OpenDatabase("", path);
database.Create();
if (!database.IsOpen) throw new InvalidOperationException($"Local test database could not be opened: {database.OpenError}");

var dxlPath = Path.Combine(Path.GetTempPath(), $"compute-with-form-probe-{Guid.NewGuid():N}.dxl");
File.WriteAllText(dxlPath, """
<?xml version="1.0" encoding="UTF-8"?>
<database xmlns="http://www.lotus.com/dxl" version="14.5">
  <form name="XPScriptCWFValid" publicaccess="false" designerversion="14.5">
    <body><richtext><pardef id="1"/><par def="1">Default: <field type="text" kind="editable" name="CWFDefault"><code event="defaultvalue"><formula>&quot;DEFAULT-VALUE&quot;</formula></code></field></par></richtext></body>
    <item name="$$ScriptName" summary="false" sign="true"><text>XPScriptCWFValid</text></item>
  </form>
  <form name="XPScriptCWFErrors" publicaccess="false" designerversion="14.5">
    <body><richtext><pardef id="1"/><par def="1">Error: <field type="text" kind="editable" name="CWFErrorOne"><code event="defaultvalue"><formula>&quot;&quot;</formula></code><code event="inputvalidation"><formula>@If(CWFErrorOne = &quot;OK&quot;; @Success; @Failure(&quot;CWFErrorOne failed&quot;))</formula></code></field></par></richtext></body>
    <item name="$$ScriptName" summary="false" sign="true"><text>XPScriptCWFErrors</text></item>
  </form>
</database>
""");
using (var importer = session.CreateDXLImporter()) importer.Import(dxlPath, database);
File.Delete(dxlPath);

using var document = database.CreateDocument();
document.ReplaceItemValue("Form", "XPScriptCWFValid").Recycle();
document.ReplaceItemValue("CWFDefault", "").Recycle();

var normalResult = document.ComputeWithForm(false, false, out var failedFields);
Console.WriteLine($"bool/out overload: result={normalResult}, failedFields=[{string.Join(", ", failedFields)}]");

object? variantFailedFields = null;
var variantResult = document.ComputeWithForm(false, false, ref variantFailedFields);
var variantValues = variantFailedFields is object[] values ? string.Join(", ", values.Select(value => Convert.ToString(value))) : "<null>";
Console.WriteLine($"Variant/ref overload: result={variantResult}, failedFields=[{variantValues}]");

var formResult = document.ComputeWithForm(null, false);
Console.WriteLine($"form-document overload: result={formResult}");

using var invalidDocument = database.CreateDocument();
invalidDocument.ReplaceItemValue("Form", "XPScriptCWFErrors").Recycle();
invalidDocument.ReplaceItemValue("CWFErrorOne", "").Recycle();
object? validationFields = null;
var validationResult = invalidDocument.ComputeWithForm(false, false, ref validationFields);
var validationValues = validationFields is object[] validationArray ? string.Join(", ", validationArray.Select(value => Convert.ToString(value))) : "<null>";
Console.WriteLine($"validation overload: result={validationResult}, failedFields=[{validationValues}]");

object? raisedValidationFields = null;
try
{
    _ = invalidDocument.ComputeWithForm(false, true, ref raisedValidationFields);
}
catch (NotesException ex)
{
    var raisedValues = raisedValidationFields is object[] raisedArray ? string.Join(", ", raisedArray.Select(value => Convert.ToString(value))) : "<null>";
    Console.WriteLine($"raise-error overload: status={ex.Status}, failedFields=[{raisedValues}]");
}

database.Remove();
