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

## Referenser

- [HCL C API: BSIG](https://opensource.hcltechsw.com/domino-c-api-docs/reference/Data/BSIG/)
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
