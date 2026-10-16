# Hotspotundersökning 2026-10-15

## Verifierade fynd

HCL C API och JNX beskriver de tre variabla strängarna i CDLINK2 och
CDLINKEXPORT2 som kommentar, serverhint och valfritt ankare i måldokumentet.
Den tredje strängen är inte hotspotens synliga text. Nuvarande HotSpotText
och SetLinkText blandar ihop dessa begrepp; tidigare test med DXL-attributet
`anchor` verifierar därför ankaret, inte ändring av synlig hotspottext.

En ny read-only-körning mot kopian av testdoc 2 visar tre CDLINK2-records
och följande sekvens runt databaslänken (recordindex inom segment 0):

| Index | Signatur | Betydelse |
| --- | --- | --- |
| 27 | 221 | CDBEGIN, deklarerar V4HOTSPOTBEGIN |
| 28 | 65406 | V5HOTSPOTBEGIN, typ 32 (RCLINK), flags 8 |
| 29 | 65526 | Ytterligare record; inte tolkat i denna undersökning |
| 30 | 65453 | V4HOTSPOTBEGIN, typ 11 (HOTLINK), flags 8 |
| 31–33 | 221 / 65425 / 222 | Inramat CDLINK2 |
| 34–37 | 221 / 65413 / 210 / 222 | Synlig text med CDTEXT och formatering |
| 38 | 174 | V4HOTSPOTEND |
| 39 | 222 | CDEND |

Nuvarande ValidateCompoundText räknar V5- och V4-begin som två oberoende
öppningar men ser bara ett avslut här. Det förklarar det hanterade felet
"Rich-text embedded object has no matching end record" utan att bevisa
att Notes har förkastat dokumentet eller att alla länkar måste konverteras.
Valideringen behöver förstå denna versionerade representation innan den
kan användas som bevis på korrupt data. Den observerade representationen
hanteras nu som en sammanhängande hotspotregion.

## Alternativ till att normalisera alla länkar

JNX CDLink2.setTexts ändrar variabeldata på det enskilda CDLINK2-recordet.
LinkID och $Links-listan behöver då inte ändras för kommentar/hint/ankare.
För synlig text bör ändringen avgränsas till CDTEXT inom den valda hotspotens
gränser, med övriga länkar, ankare och mål bevarade. Läsningen av denna CDTEXT
är nu verifierad; skrivning kräver separat bevarande av formatposterna.

## Källor

- https://opensource.hcltechsw.com/domino-c-api-docs/reference/Data/CDLINKEXPORT2/
- https://opensource.hcltechsw.com/domino-c-api-docs/reference/Data/CDHOTSPOTEND/
- https://github.com/HCL-TECH-SOFTWARE/domino-jnx/blob/develop/domino-jnx-api/src/main/java/com/hcl/domino/richtext/records/CDLink2.java
- https://github.com/HCL-TECH-SOFTWARE/domino-jnx/blob/develop/domino-jnx-api/src/main/java/com/hcl/domino/richtext/records/CDLinkExport2.java
- https://github.com/HCL-TECH-SOFTWARE/domino-jnx/blob/develop/domino-jnx-api/src/main/java/com/hcl/domino/richtext/RichTextConstants.java

JNI-koden bakom Notes Java-klientens RichTextDoclink har inte verifierats i
denna undersökning. JNX är en separat implementation ovanpå C API/JNA.

## Genomförd rättning och native-test

`SetLinkMetadata(comment, serverHint, anchor)` ändrar nu endast den valda
länkpostens variabeldata. Signatur och CDLINK2-index bevaras. `Anchor`
exponerar ankartext uttryckligen. Den äldre `SetLinkText` delegerar till samma
metod; dess tredje argument är fortfarande ankartext, inte synlig text.
`HotSpotText` läser nu synlig CDTEXT inom den omgivande V4 HOTLINK-regionen;
`Anchor` läser fortsatt den tredje CDLINK2-strängen.
`SetHotSpotText` ändrar en enkel CDTEXT-run och bevarar dess stil. Native-testet
visade att testdoc 2 har flera text-/formatruns; den varianten avvisas därför
med ett tydligt fel tills varje runs format kan bevaras säkert.

Valideringen känner nu igen just sekvensen V5 RCLINK / resource / V4 HOTLINK
och räknar denna som en enda region. Övriga hotspot-begin måste fortfarande
ha ett avslut. Denna rättning är avgränsad till det observerade formatet.

Native-test på `test/richtext-database-link-probe.nsf`, testdoc 2, passerade:
Unicode-kommentar och ankare lästes tillbaka efter save/reopen. Alla andra
CD-records var byteidentiska före/efter mutation; signatur och LinkID var
oförändrade. Notes DXL bekräftade oförändrade länkmål och synlig text samt
oförändrade grannlänkar. Ingen masskonvertering av CDLINK2 behövdes.
Ett separat riktat test verifierade dessutom att `HotSpotText` returnerar
`hotspot länk` efter native läsning av testdoc 2.
Ett andra native-test ändrade hotspotens fetstil, sparade och öppnade om
dokumentet; `HotSpotTextStyle.Bold` lästes tillbaka som `-1` och återställdes
sedan till ursprungsvärdet.
