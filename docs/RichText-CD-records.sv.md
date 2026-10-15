# RichText: tabellrecords och verifiering

`NotesRichTextItem.AppendTable(rows, columns)` stöder 1–255 rader och
1–64 kolumner. Följande exempel skapar en tabell och sparar dokumentet:

```csharp
using var document = database.CreateDocument();
using var body = document.CreateRichTextItem("Body");
body.AppendText("Före tabellen");
body.AppendTable(3, 4);
body.AppendText("Efter tabellen");
document.Save();
```

## Record-layout

HCL C API och HCL JNX definierar BSIG som två bytes: signatur följd av
recordens totala längd. Det skiljer sig från WSIG, som har fyra bytes.
Tabellrecordsen använder BSIG:

| Record | Längd | Viktiga offsets (bytes) |
| --- | ---: | --- |
| CDTABLEBEGIN | 14 | LeftMargin 2, intercell-avstånd 4/6/8/10, Flags 12 |
| CDTABLECELL | 18 | Row 2, Column 3, LeftMargin 4, RightMargin 6, FractionalWidth 8, Border 10, Flags 11, v42Border 12, RowSpan 14, ColumnSpan 15, BackgroundColor 16 |
| CDTABLEEND | 6 | Fyra nollställda reservbytes efter BSIG |

`CDTABLECELL_USE_BKGCOLOR` är `0x01` och
`CDTABLECELL_USE_V42BORDERS` är `0x02`.

Den tidigare skrivaren använde fyra bytes för BSIG. Läsaren hade samma
förskjutning, så interna round-trip-kontroller kunde missa felet.
Skrivare och läsare använder nu layouten ovan. Fler-radsbegränsningen är borttagen.

## Tester

`DotNetToDomino.FullTestCli`, filtret `rich text`, verifierar dimensioner,
cellmetadata och sista cellen efter spara/återöppna. Dessutom kontrolleras
tablerow/tablecell i HCL Notes egen strukturerade DXL-export för varje storlek.
Tillfälliga sparade testdokument tas bort efter kontrollen.

Filtret innehåller även ett borttagningstest med mer än 100 KB text i flera
fysiska composite-segment. Efter tabellborttagning, spara och återöppning
kontrolleras via DXL att alla 1800 prefixmarkörer samt suffixet finns kvar.
Testet misslyckas med den tidigare implementationen, som använde recordnumret
utan segmentnummer. Borttagning identifierar nu varje record med båda värdena.
Sektioner använder samma korrigerade borttagningsfunktion och återvinns efter
lyckad borttagning.

## Referenser efter ändringar

RichText-itemobjekt som tillhör samma öppna `NotesDocument` delar en
ändringsräknare per itemnamn. Navigatorer läser om innehållet när räknaren
ändras, även om ändringen gjordes genom ett annat itemobjekt.

Tabeller, celler, sektioner och doclinks är positionsbundna. Efter en ändring
av iteminnehållet kan gamla positioner identifiera helt andra records.
De gamla objekten ger därför `InvalidOperationException`. Hämta objekten
igen med navigatorn efter en append eller omskrivning. En sektions egen
headeruppdatering uppdaterar dess räknare så att objektet kan fortsätta användas.

Räknaren gäller samma öppna dokumentobjekt. Den upptäcker inte ändringar via
extern native-kod eller ett separat öppnat dokumenthandle.

## Styckeformat och standardfont

`AppendParagraphStyle` använder `CompoundTextDefineStyle` och
`CompoundTextAddParagraphExt`. Efterföljande text ärver styckets format.
Native DXL-testet verifierar centrering (`Alignment = 3`) och vänstermarginalen
1440 twips, vilket exporteras som en tum.

Vanlig text och nya stycken använder nu en explicit standardfont:
Swiss/Helvetica, svart, normal och 10 punkter (`FONTID = 0x0A000001`).
Det tidigare värdet noll exporterades som 0 punkter. Även `AppendStyle` med
oförändrad fontfamilj använder Swiss som grund. Explicit vald fontfamilj och
storlek fortsätter att användas.

`AppendStyle` bevarar tidigare egenskaper för fält med `STYLE_NO_CHANGE` (255).
Det gäller även flera stilanrop innan text skrivs. Vid native textskrivning
används senaste CDTEXT-recordens FONTID som grund, så att en andra itemreferens
kan lägga till fetstil utan att förlora storlek, färg och fontfamilj.
DXL-testet kontrollerar 18-punkters röd text med fetstil, oförändrad stil och
explicit borttagning av fetstil, inklusive en andra itemreferens.

```csharp
using var sizeAndColor = session.CreateRichTextStyle();
sizeAndColor.FontSize = 18;
sizeAndColor.NotesColor = 2; // röd
body.AppendStyle(sizeAndColor);
body.AppendText("Stor röd text");
using var boldOnly = session.CreateRichTextStyle();
boldOnly.Bold = -1;
body.AppendStyle(boldOnly);
body.AppendText("Fet text med samma storlek och färg");
```

Detta verifierar native lagring och DXL-tolkning. Det är inte ett visuellt
test av Notes-editorn eller bevis för att alla övriga RichText-metoder är färdiga.

## PassThruHTML

`NotesRichTextStyle.PassThruHTML = -1` gör efterföljande `AppendText` till
pass-through HTML. `0` återgår till vanlig text och `255` behåller föregående
inställning. Skrivaren lägger balanserade CDHTMLBEGIN/CDHTMLEND-records runt
HTML-texten. Båda har åtta bytes: WSIG och fyra nollställda reservbytes.

Native-testet sparar och återöppnar dokumentet och kontrollerar både DXL:s
`run html="true"` och Notes HTML-konvertering. HTML-taggar behålls i HTML-läge;
taggar som skrivs efter att läget stängts av exporteras som vanlig escapad text.

```csharp
using var htmlStyle = session.CreateRichTextStyle();
htmlStyle.PassThruHTML = -1;
body.AppendStyle(htmlStyle);
body.AppendText("<strong>HTML-innehåll</strong>");
htmlStyle.PassThruHTML = 0;
body.AppendStyle(htmlStyle);
body.AppendText("<strong>Detta visas som text</strong>");
```

## Dokument-, vy- och databaslänkar

`AppendDocLink` har overloads för `NotesDocument`, `NotesView` och
`NotesDatabase`. Målet måste tillhöra samma `NotesSession`. Dokumentlänkar
är inte längre begränsade till samma databasreplika.

```csharp
body.AppendDocLink(database, "Öppna databasen");
body.AppendDocLink(view, "Öppna vyn");
body.AppendDocLink(targetDocument, "Öppna dokumentet");
body.Compact();
document.Save();
```

Native-testet kontrollerar alla tre länktypernas DXL efter Compact,
spara och återöppna, inklusive replica-ID och dokumentets/vyns UNID.
Ett separat native-test skapar en tillfällig databas med annat replica-ID och
verifierar dokumentlänken både via navigatorns properties och oberoende DXL
efter spara/återöppna. Testdatabasen tas bort efteråt. Navigatorns databas-
och vylänkar kontrolleras också, inklusive nollställda dokument-/vy-UNID där
de inte används. Borttagning av den mittersta länken verifieras efter Compact
och återöppning, med de intilliggande länkarna bevarade.

`NotesRichTextDocLink` läser också CDLINK2-records via dokumentets separata
`$Links`-item (TYPE_NOTELINK_LIST). Indexet är nollbaserat. Saknad lista,
fel datatyp, trunkerad lista och ogiltigt index ger ett hanterat undantag.
Ett native-test skriver en binär CDLINK2/$Links-fixture, sparar och återöppnar
den och jämför navigatorns mål med Notes egen DXL-export. Saknad lista testas
också. Detta kompletterar testerna av länkar skapade via compound-text-API:et.

## Kopiera rich text-items

```csharp
using var copied = body.CopyToDocument(destinationDocument, "CopiedBody");
destinationDocument.Save();
```

Kopieringen tar med samtliga CD-segment. CDLINK2-länkar omvandlas till
självständiga CDLINKEXPORT2-records med mål från källdokumentets `$Links`.
Destinationsdokumentets egen referenslista behöver därför inte ersättas.
Länkarnas mål löses innan destinationen ändras. Båda dokumenten måste tillhöra
samma session. Befintliga navigatorpositioner i destinationsfältet blir ogiltiga.

Native-tester verifierar kopierad CDLINK2-länk via Notes DXL efter återöppning,
och ett fält med över 100 KB Unicode-text, tabell och skyddsflagga.
Flagguppdatering på rich text appliceras på samtliga fysiska segment i samma
ordning, så att ändring av exempelvis `IsProtected` inte flyttar första segmentet
sist eller ger olika flaggor på fortsättningssegmenten.
`CopyToDocument` kopierar även de `$FILE`-objekt som fältets bilagehotspots
refererar till. En befintlig bilaga med samma interna namn och identiska bytes
återanvänds; olika bytes ger `IOException` innan destinationsfältet ändras.
Native-testet jämför en bilaga på 100000 bytes efter återöppning och borttagning
av källdokumentet. Upprepad kopiering och skydd av befintligt innehåll vid
namnkrock testas också.

Kopiering av externa `$Fonts`-beroenden och övriga embedded objects, exempelvis
OLE-objekt, är ännu inte verifierad som komplett.

## Compact

`body.Compact()` bygger om itemets CD-ström via Notes native compound-text-
skrivare, även när inga textrecords behöver slås ihop. Gamla itemsegment ersätts
av den ompackade strömmen. Intilliggande CDTEXT-records med samma FONTID slås
dessutom ihop inom en gräns på 16000 bytes per record.

Styckegränser, HTML-gränser, hotspots, tabellrecords och okända CD-records
behålls. Itemets logiska flaggor återställs efter omskrivningen utan att flytta
första segmentet efter fortsättningssegmenten. Befintliga navigatorpositioner
och länkade element måste hämtas på nytt efter anropet.

```csharp
// Efter redigering av text, tabeller och bilagor:
body.Compact();
document.Save();
```

Native-testerna verifierar minskning från 2300 till 1508 CD-recordbytes,
Unicode efter spara/återöppna, upprepade anrop samt oförändrat DXL-innehåll
med tabell, fontformat, HTML och bilaga. Bilagans bytes och itemets signerings-,
krypterings- och skyddsflaggor kontrolleras också.

Detta verifierar den implementerade ompackningen och textoptimeringen.
Exakt byte-för-byte-paritet med LotusScripts interna Compact och dess hantering
av alla tänkbara äldre eller okända recordvarianter har inte verifierats.

## Referenser

- [HCL C API: BSIG](https://opensource.hcltechsw.com/domino-c-api-docs/reference/Data/BSIG/)
- [HCL Designer: RichTextItem Compact](https://help.hcl-software.com/dom_designer/10.0.1/reference/r_domino_RichTextItem_compact.html)
- [HCL C API: CDHTMLBEGIN](https://opensource.hcltechsw.com/domino-c-api-docs/reference/Data/CDHTMLBEGIN/)
- [HCL C API: CDHTMLEND](https://opensource.hcltechsw.com/domino-c-api-docs/reference/Data/CDHTMLEND/)
- [HCL C API: CDTABLECELL](https://opensource.hcltechsw.com/domino-c-api-docs/reference/Data/CDTABLECELL/)
- [HCL C API: canonical format](https://opensource.hcltechsw.com/domino-c-api-docs/howto/user_guide/Domino_Canonical_Format/)
- [HCL JNX: BSIG](https://github.com/HCL-TECH-SOFTWARE/domino-jnx/blob/develop/domino-jnx-api/src/main/java/com/hcl/domino/richtext/structures/BSIG.java)
- [HCL JNX: CDTableCell](https://github.com/HCL-TECH-SOFTWARE/domino-jnx/blob/develop/domino-jnx-api/src/main/java/com/hcl/domino/richtext/records/CDTableCell.java)
- [HCL JNX: RichTextConstants](https://github.com/HCL-TECH-SOFTWARE/domino-jnx/blob/develop/domino-jnx-api/src/main/java/com/hcl/domino/richtext/RichTextConstants.java)

`ODSWriteMemory` är HCL:s native C API för konvertering från host-layout till
Dominos canonical-format. På Intel-plattformar kräver HCL inte denna konvertering
för dessa records. Det tidigare antagandet att avsaknaden av funktionen förklarade
tabellfelet var därför felaktigt.
