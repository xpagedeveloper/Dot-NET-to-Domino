# Databaser: titel, replica-ID och kopiering

`Title` är databasens visningsnamn i Notes. `FilePath` och `FileName` anger NSF-filens namn och är skrivskyddade properties. `ReplicaID` är också skrivskyddad som property; ändra den med `SetReplicaId(...)`.

Starta en session med den installerade Notes-versionens arkitektur och ett autentiserat Notes-ID. I exemplen kommer lösenordet från miljön och skrivs inte i källkod.

```csharp
using DotNetToDomino;

using var session = new NotesSession(
    @"D:\Program Files\HCL\Notes",
    @"D:\Program Files\HCL\Notes\notes.ini",
    Environment.GetEnvironmentVariable("NOTES_TEST_ID_PASSWORD"));
using var database = session.GetDatabase("", "example.nsf", false)
    ?? throw new InvalidOperationException("Databasen saknas.");

database.Title = "Min databas Åäö";
database.Categories = "Exempel, Test";
Console.WriteLine(database.Title);
```

Tomt servernamn betyder lokal databas. Relativa NSF-sökvägar löses mot Notes datakatalog. `GetDatabase(..., false)` returnerar `null` om databasen saknas. För att skapa en ny databas hämtar man först en wrapper med `true`, kontrollerar `IsOpen` och anropar `Create()`:

Från `0.1.0-alpha.8` kan man även ange `"local"` som servernamn. Det normaliseras till `""` innan databasöppning, skapande, kopiering, replica-ID-sökning och katalogåtkomst. Matchningen ignorerar stora/små bokstäver och omgivande blanksteg. `NotesDatabase.Server` returnerar då tom sträng. Exempel: `session.GetDatabase("local", "example.nsf", false)`.

Notes C API får det relativa databasnamnet utan att .NET lägger till Data-sökvägen. En fullständig fysisk sökväg kan också anges för en lokal databas, även utanför Notes Data. Då ska server vara tom. För en fjärrdatabas byggs Notes nätverkssökväg med server och relativt databasnamn.

```csharp
using var created = session.GetDatabase("", "new-example.nsf", true)!;
if (!created.IsOpen) created.Create();
created.Title = "Ny databas";
```

## Kopiera och ge kopian ett eget namn och replica-ID

```csharp
var originalReplica = database.ReplicaID;
using (var copy = database.CreateCopy("", "example-copy.nsf", includeDocuments: true))
{
    copy.Title = "Min separata kopia";
    copy.SetReplicaId("01234567:89ABCDEF"); // Exempel: välj ett eget unikt ID.
    Console.WriteLine($"{copy.FileName}: {copy.Title}, {copy.ReplicaID}");
}

using var reopened = session.GetDatabase("", "example-copy.nsf", false)!;
Console.WriteLine($"Original: {database.Title}, {database.ReplicaID}");
Console.WriteLine($"Kopia: {reopened.Title}, {reopened.ReplicaID}");
if (database.ReplicaID != originalReplica)
    throw new InvalidOperationException("Originalets ID ändrades oväntat.");
```

Metadata-setters skriver direkt; någon `NotesDatabase.Save()` behövs inte. `NotesDocument.Save()` krävs däremot för dokumentändringar. Stäng och återöppna databasen för att kontrollera bestående metadata.

`includeDocuments` är `false` som standard: då kopieras designen. Sätt `true` för att även kopiera dokument. Ange en ledig destination. `CreateCopy` skapar redan ett nytt replica-ID; `SetReplicaId` behövs bara när du vill ange ett bestämt eget ID. Replica-ID består av 16 hexadecimala tecken; kolon och bindestreck accepteras som avskiljare. Ett separat replica-ID gör att kopian inte identifieras som samma replika som originalet. Använd ett unikt ID för verkliga databaser.

Om källdatabasen saknar design helt kan en design-only-kopiering ge Notes-felet `0x023C` (inga valda dokument att kopiera). Använd en källa med design för design-only-kopiering.

För ett nytt **filnamn** skapar man kopian under den nya NSF-sökvägen. Det finns ingen API-metod för att byta en befintlig NSF-fils namn på plats. Ändring av `Title` ändrar enbart visningsnamnet.

## Körbart exempel

[DatabaseExample](../test/DotNetToDomino.DatabaseExample/Program.cs) kopierar en befintlig databas, ändrar kopians titel och replica-ID och skriver ut original och kopia efter återöppning. Originalet behålls; den skapade kopian behålls också.

```powershell
dotnet run --project test/DotNetToDomino.DatabaseExample -c Release -- `
  'D:\Program Files\HCL\Notes' `
  'example.nsf' 'example-copy.nsf' 'Separat kopia' '0123456789ABCDEF'
```

Sätt `NOTES_TEST_INI` och `NOTES_TEST_ID_PASSWORD` i den anropande processen vid behov. Testa med egna testdatabaser först.

## Regressionstester

```powershell
dotnet build test/DotNetToDomino.FullTestCli -c Release
./test/Run-NativeTests.ps1 -Filters 'database lifecycle regression' -ShowOutput
```

Testerna skapar unikt namngivna lokala databaser och tar bort dem efteråt. De kontrollerar metadata efter återöppning, replica-ID och ogiltiga värden, kopiering med respektive utan dokument samt oberoende titlar, filnamn, replica-ID och dokumentinnehåll på original och kopia.

Verifierat 2026-10-13 med HCL Notes 12.0.2 x64: alla fyra databasfall passerar utan krascher. Både formulär och vyer finns i designkopian, dokument följer endast med när `includeDocuments: true`, och ändrad titel, replica-ID och dokumenttext i kopian påverkar inte originalet.

API-referens: [HCL NSFDbCreateAndCopy](https://opensource.hcltechsw.com/domino-c-api-docs/reference/Func/NSFDbCreateAndCopy/).
