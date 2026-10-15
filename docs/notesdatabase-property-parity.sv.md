# NotesDatabase: properties jämfört med HCL 14.5

Jämförelse 2026-10-13 mot [HCL NotesDatabase](https://help.hcl-software.com/dom_designer/14.5.0/basic/H_NOTESDATABASE_CLASS.html). Listan gäller publika C#-properties, inte alla metod-overloads eller alla beteendedetaljer.

## Finns

`ACL` (ny implementation), `Agents`, `AllDocuments`, `Categories`, `Created`, `CurrentAccessLevel`, `DesignTemplateName`, `FileFormat`, `FileName`, `FilePath`, `Forms`, `IsFTIndexed`, `IsOpen`, `LastFTIndexed`, `LastModified`, `Parent`, `PercentUsed`, `ReplicaID`, `Server`, `Size`, `TemplateName`, `Title`, `Views`.

`DesignTemplateName` har även en setter som .NET-utökning; HCL:s LotusScript-property är read-only. `CurrentAccessLevel` är read-only. Att en property finns innebär inte full semantisk likhet: t.ex. kräver flera av våra properties en öppen databas och lokal `FilePath` behåller den angivna sökvägen.

## Implementerade egenskaper

| Område | Properties som saknas |
|---|---|
`ACLActivityLog`, `Managers`, `IsCurrentAccessPublicReader`, `IsCurrentAccessPublicWriter`, `MaxSize`, `SizeQuota`, `SizeWarning`, `FTIndexFrequency`, `IsInMultiDbIndexing`, `IsMultiDbSearch`, `ListInDbCatalog`, `Type`, `IsConfigurationDirectory`, `IsDirectoryCatalog`, `IsDesignLockingEnabled`, `IsDocumentLockingEnabled`, `IsInService`, `IsPendingDelete`, `ReplicationInfo`, `EncryptionStrength`, `IsLocallyEncrypted`, `FolderReferencesEnabled`, `LimitRevisions`, `LimitUpdatedBy`, `NotesURL`, `UndeleteExpireTime`.

Kvoter, ACL, replikering och DXL-baserade databasalternativ använder de motsvarande native-anropen och testas på en temporär databas. Read-only-egenskaperna exponeras utan setter. `TemplateName` och `DesignTemplateName` kan därför inte tilldelas som C#-properties; de har separata .NET-metoder `SetTemplateName` och `SetDesignTemplateName` för den native-funktionalitet som finns i Domino.

## Implementerade med begränsad semantik

`IsPrivateAddressBook` och `IsPublicAddressBook` använder databasens typ tillsammans med om databasen öppnades lokalt eller via en server. Det motsvarar den användbara fristående C#-semantiken, men är inte samma sak som Notes-klientens användar- och Domino Directory-kontext.

## Medvetet inte implementerade

Följande kräver klient- eller agentkontext, eller en dokumenterad native-struktur som inte är stabil över Notes-versioner:

| Property | Orsak |
|---|---|
| `UnprocessedDocuments` | Kräver aktuell agent-/urvalscontext. Ett fristående program har ingen sådan implicit collection. |
| `DelayUpdates` | HCL-propertyn styr klientens serverbatchning. NSF C API erbjuder ingen säker per-databas motsvarighet. |
| `IsClusterReplication` | Kräver Domino-klusterstatus, inte en vanlig NSF-databasflagga. |
| `HttpURL` | Kan inte härledas säkert för lokala eller serverlösa databaser utan HTTP-konfiguration. |
| `IsLink` | Kräver filsystemets länkmetadata. För serverdatabaser finns den informationen bara på Domino-servern. |
| `LastFixup` | C API dokumenterar inte en stabil struktur för att läsa detta värde. |

HCL-sidan placerar även `Encrypt` och `Decrypt` i sin property-lista, men dessa är metoder. De är inte medräknade som properties och saknas också som publika databasmetoder här.
