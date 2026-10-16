# Rich-text navigator insertion

All insertion overloads that accept `NotesRichTextNavigator` insert before the
navigator's current compound-text record. The position can be inside a
paragraph, table cell, section, link hotspot, or between other rich-text
elements.

```csharp
using var navigator = body.CreateNavigator();
if (!navigator.FindFirstString("IMAGE_POSITION"))
    throw new InvalidOperationException("Insertion marker was not found.");

body.AppendText(navigator, "Inserted text");
body.EmbedImageInline(navigator, imagePath);
body.AppendDocLink(navigator, targetDocument, "Open document", "Open");
body.EndInsert();
body.Update();
```

The same model is available for tables, attachments, rich-text items,
paragraph styles, sections, page breaks, horizontal rules, Notes URLs and
database/view/document links.

Hotspots can be enumerated read-only through the navigator:

```csharp
using var hotspots = body.CreateNavigator();
for (var found = hotspots.FindFirstElement(NotesRichTextNavigator.ElementHotspot);
     found;
     found = hotspots.FindNextElement(NotesRichTextNavigator.ElementHotspot))
{
    var hotspot = (NotesRichTextHotspot)hotspots.GetElement()!;
    Console.WriteLine($"{hotspot.HotspotType}: {hotspot.Flags}");
}
```

File hotspots are exposed as `NotesEmbeddedObject`; other hotspot records are
exposed as `NotesRichTextHotspot` so their type and flags can be inspected
without interpreting an unknown payload.

Inline images can be enumerated in the same way. `GetInlineImage()` returns
the image at the current navigator position, including its byte array:

```csharp
for (var found = navigator.FindFirstElement(NotesRichTextNavigator.ElementInlineImage);
     found;
     found = navigator.FindNextElement(NotesRichTextNavigator.ElementInlineImage))
{
    var image = navigator.GetInlineImage();
    File.WriteAllBytes($"inline-{image.Index}.bin", image.ToByteArray());
}
```

Table cells are also positionable elements:

```csharp
for (var found = navigator.FindFirstElement(NotesRichTextNavigator.ElementTableCell);
     found;
     found = navigator.FindNextElement(NotesRichTextNavigator.ElementTableCell))
{
    var cell = (NotesRichTextTableCell)navigator.GetElement()!;
    Console.WriteLine($"{cell.Row},{cell.Column}: {cell.Text}");
}
```

## Enumerating elements

```csharp
foreach (var table in body.Tables) { }
foreach (var image in body.InlineImages) { }
foreach (var attachment in body.Attachments) { }
foreach (var link in body.Links) { }
```

`Tables` and `Links` are navigator-backed and returned in compound-text order.
Inline images are returned in image-record order; attachments are returned in
Notes attachment order.

For duplicate attachment names, Domino assigns an effective stored name such as
`mytestdoc.002.txt`. `NotesEmbeddedObject.Name` and `Source` expose that name;
the C API does not provide a separate original filename for the renamed copy.

### Läsa list- och paragraftext

`NotesRichTextItem.Paragraphs` returnerar en stabil, nollbaserad ordning med
`NotesRichTextParagraph.Index` och `Text`. Den kombinerar `CDTEXT`-runs under
varje `CDPARAGRAPH`, vilket gör det möjligt att loopa igenom listinnehåll även
när Notes inte lagrar någon separat listtyp i compound text.

```csharp
foreach (var paragraph in body.Paragraphs)
    Console.WriteLine($"{paragraph.Index}: {paragraph.Text}");
```

### Formaterad text i tabellceller

```csharp
using var style = session.CreateRichTextStyle();
style.Bold = -1;
style.FontSize = 14;

using var cell = table.GetCell(0, 0);
cell.AppendText("Viktig celltext", style);
cell.Clear(); // tömmer innehållet och behåller cellen
```

`cell.CreateRange()` returnerar en range över cellens innehåll för läsning,
formatering och partiell borttagning.

Cellspecifik text ersättning använder:

```csharp
var count = cell.FindAndReplace("gammal", "ny", NotesConst.RT_REPL_ALL);
```

Sökningen begränsas till cellens egna richtext-records.
