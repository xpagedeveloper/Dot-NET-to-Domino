# Signera Notes-dokument i C#

`NotesDocument.Sign()` signerar dokumentet med det Notes-ID som sessionen autentiserats med. Anropa därefter `Save()` för att spara signaturen i NSF-databasen.

```csharp
using DotNetToDomino;

using var session = new NotesSession(
    @"D:\Program Files\HCL\Notes",
    @"D:\Program Files\HCL\Notes\notes.ini",
    Environment.GetEnvironmentVariable("NOTES_TEST_ID_PASSWORD"));
using var database = session.GetDatabase("", "example.nsf", false)
    ?? throw new InvalidOperationException("Databasen saknas.");
using var document = database.CreateDocument();
document.ReplaceItemValue("Subject", "Signerat exempel").Recycle();
document.Sign();
document.Save();

Console.WriteLine(document.IsSigned);
Console.WriteLine(document.Signer);
Console.WriteLine(document.Verifier);
```

`IsSigned` visar att dokumentet innehåller `$Signature`, inte att signaturen är giltig. `Signer` returnerar signerarens namn från native signaturverifiering. `Verifier` returnerar namnet på certifikatet/certifieraren som verifierade signaturen, inte namnet på den användare som läser dokumentet. Utan signatur eller vid misslyckad verifiering returnerar båda namnproperties tom sträng.

Ändra dokumentets innehåll före `Sign()`. Efter innehållsändringar måste det signeras på nytt. Native Notes kan cacha en tidigare verifiering i ett öppet dokument; verifiera sparade ändringar genom att stänga och återöppna dokumentet. En krypterad signatur verifieras efter dekryptering.

## Livslängd och recycle

Alla NotesObject-wrapperklasser implementerar `IDisposable`. `using var` anropar `Dispose()` automatiskt när det omgivande scopet avslutas, även vid undantag. `Dispose()` anropar `Recycle()` och är idempotent.

```csharp
using (var document = database.CreateDocument())
{
    document.ReplaceItemValue("Subject", "Kortlivat dokument").Recycle();
    document.Sign();
    document.Save();
} // Dokumentets native handle stängs här.
```

`variable = null` eller tilldelning av ett annat objekt anropar inte `Dispose()`. Biblioteket kan inte observera C#-variabeltilldelningar. Sessionen/databasen håller ägarreferenser till wrapperobjekt för att kunna stänga dem ordnat; dessa referenser gör att GC inte automatiskt upptäcker att anropande kod slutat använda dem. Ingen GC-baserad automatisk recycle finns idag. Ägaren återvinner kvarvarande barn när databasen/sessionen stängs.

Använd `using` för den kod som äger ett objekt. Om två variabler pekar på samma wrapper och den ena återvinner den blir objektet oanvändbart även via den andra variabeln. Återvinn därför inte ett objekt som annan kod fortfarande använder.

## Verifiering

De native regressionstesterna använder den publika `Sign()`-metoden och kontrollerar unsigned properties, signerarens namn, verifierarens beständighet efter sparning/återöppning, ogiltig signatur efter ändrat innehåll samt signerat/krypterat innehåll efter dekryptering.

Referenser: [HCL Sign](https://www.help.hcl-software.com/dom_designer/9.0.1/reference/r_domino_Document_sign.html), [Verifier](https://help.hcl-software.com/dom_designer/12.0.2/basic/H_VERIFIER_PROPERTY.html), [NSFNoteVerifySignature](https://opensource.hcltechsw.com/domino-c-api-docs/reference/Func/NSFNoteVerifySignature/).
