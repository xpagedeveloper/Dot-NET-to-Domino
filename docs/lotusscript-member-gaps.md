# LotusScript Notes member gaps

This is the member-level gap list for the LotusScript-compatible surface represented by the local XPscript Notes sources. It is intentionally gap-only: members not listed here are currently present in the C# surface or are still awaiting a source-level confirmation. `runtime` means the member exists but needs live Notes/Domino verification; `partial` means the member exists but behavior or native support is incomplete; `missing` means no public C# member exists.

This list is the actionable companion to [lotusscript-api-inventory.json](lotusscript-api-inventory.json). It must not be interpreted as a complete HCL product inventory for Notes classes that neither this repository nor XPscript currently models.

## Complexity scale

| Step | Meaning |
|---:|---|
| 1 | Managed property, alias, validation, or simple C# mapping; no new native ABI. |
| 2 | One small native wrapper or an already known native operation with clear ownership. |
| 3 | Several native calls, metadata parsing, lifecycle handling, or multiple overloads. |
| 4 | Composite records, MIME trees, callbacks, native handles, or ABI-sensitive ownership. |
| 5 | Large Domino semantics, security/mail/database behavior, undocumented ABI, or mandatory live-runtime design. |

## NotesSession

| Member | Status | Notes |
|---|---|---|
| `CurrentDatabase` | missing | No current database context property. |
| `CurrentAgent` | missing | No current agent context property. |
| `DocumentContext` | missing | No document-context property. |
| `EffectiveUserName` | implemented | In a standalone C API session this is the ID-file identity (`UserName`); there is no implicit agent signer context to override it. |
| `International` | missing | No NotesInternational wrapper/property. |
| `LastExitStatus` | missing | No process/session exit-status property. |
| `AddressBooks` | runtime | Public surface exists; live address-book parsing and wrapper behavior remain unverified. |
| `ConvertMIME` | runtime | Public property exists; interaction with every document-open path needs live verification. |

Complexity: `CurrentDatabase` 3, `CurrentAgent` 3, `DocumentContext` 3, `EffectiveUserName` 2, `International` 5, `LastExitStatus` 2, `AddressBooks` 3, `ConvertMIME` 3.

## NotesDatabase

| Member | Status | Notes |
|---|---|---|
| `ACL` | partial | `QueryAccess()`/`NotesDatabaseAccess` exists, but LotusScript-style ACL property naming and complete ACL member parity need review. |
| `IsPrivateAddressBook` | missing | No public property. |
| `IsPublicAddressBook` | missing | No public property. |
| `IsTemplate` | missing | No public property. |
| `OpenIfModified` | missing | No conditional-open method. |
| `Compact` | missing | No compact operation. |
| `Fixup` | missing | No fixup operation. |
| `CreateReplica` | missing | No replica-creation method at the public NotesDatabase level. |
| `OpenMail` | missing | No mail-database convenience method. |
| `DesignTemplateName` | runtime | Property exists; native metadata and setter behavior need live verification. |
| `Views`, `Forms`, `Agents` | runtime | Collections exist; complete design inventory and aliases require runtime verification. |
| `FTSearch`/full-text members | runtime | Implemented surface exists; index, ranking and edge behavior need runtime verification. |

Complexity: `ACL` 4, `IsPrivateAddressBook` 3, `IsPublicAddressBook` 3, `IsTemplate` 2, `OpenIfModified` 3, `Compact` 5, `Fixup` 5, `CreateReplica` 5, `OpenMail` 3, metadata/design collections 3, full-text behavior 4.

## NotesDocument

| Member | Status | Notes |
|---|---|---|
| `ParentDocument` | missing | C# exposes `ParentDocumentUNID`, but not a parent-document object property. |
| `SignOnSend` | missing | No public document property. |
| `IsEncrypted` | missing | No document-level encryption property. |
| `IsSigned` | missing | No document-level signature property. |
| `EncryptOnSend` | missing | No public send-encryption property. |
| `SaveMessageOnSend` | missing | No public mail-save property. |
| `VerifyPassword`/document security members | missing | No document security convenience surface. |
| `Items` | partial | Public C# array/list mapping exists; complete item-object identity and live mutation behavior need testing. |
| `ComputeWithForm` | runtime | Overloads exist; native failed-field and raise-error parity needs runtime verification. |
| `Send` overloads | runtime | Overloads exist; recipient/default/error semantics need live verification. |
| `CreateMIMEEntity` | partial | Root Body creation exists; child entity creation is not complete. |

Complexity: `ParentDocument` 2, security/send flags 4, `Items` 3, `ComputeWithForm` 4, `Send` overloads 4, `CreateMIMEEntity` 4.

## NotesItem / NotesRichTextItem

| Member | Status | Notes |
|---|---|---|
| `IsReaders`/`IsAuthors`/`IsEncrypted`/`IsProtected`/`IsSigned`/`IsSummary` | runtime | Properties exist; all flag combinations need native runtime verification. |
| `AppendParagraphStyle` | partial | Public member exists but intentionally fails closed pending safe composite-record ABI support. |
| `AppendTable` | partial | Creates an empty table using canonical CDTABLEBEGIN/CELL/END records; live Notes validation and labels/styles remain. |
| `AppendDocLink` | implemented | Uses `CompoundTextAddDocLink` with replica ID and document UNID; live Notes verification remains. |
| `AppendObject` | partial | File-object embedding is implemented through the native attachment path; OLE and other object types remain. |
| `AppendNotesURL` | partial | Local `notes://` document URLs are resolved and written as doclinks; view/database/remote URL variants remain. |
| `Update` | implemented | Persists the owning document, which is the native storage unit for RichText. |
| attachment metadata edge cases | partial | Attachment extraction and byte-array mapping exist; all embedded object types remain to be verified. |

Complexity: item flags 2, `AppendParagraphStyle` 4, `AppendTable` 4, `AppendDocLink` 4, `AppendObject` 5, `AppendNotesURL` 4, `Update` 3, attachment edge cases 4.

## MIME entities and headers

| Member | Status | Notes |
|---|---|---|
| `AppendChildEntity` | missing | Child creation is not exposed. |
| `CreateChildEntity` | missing | No public child-entity factory. |
| `SetContentFromText` on child | missing | Root content is supported; child content mutation is not. |
| `SetContentFromBytes` on child | missing | Root content is supported; child content mutation is not. |
| `Remove` on child entity | missing | Child removal is not implemented. |
| `CreateHeader` on child | partial | Header creation exists only where the native entity ABI is safe; child mutation remains blocked. |
| `SetHeaderVal`/`SetParamVal` on child | missing | Child-header mutation is not implemented. |
| nested multipart write operations | missing | Parser/read traversal exists; safe native write support is missing. |

Complexity: `AppendChildEntity` 5, `CreateChildEntity` 5, child text/bytes content 5, child `Remove` 5, child `CreateHeader` 4, child header setters 5, nested multipart writes 5.

## NotesMail

| Member | Status | Notes |
|---|---|---|
| `SendTo`, `CopyTo`, `BlindCopyTo` | runtime | Implemented; live recipient normalization and delivery behavior need verification. |
| `ReplyTo`, `Principal`, `Subject` defaults | runtime | Default/empty-value behavior needs live Domino verification. |
| `DeliveryPriority`, `DeliveryReport`, `ReturnReceipt` | runtime | Implemented and range-validated; all delivery combinations need live verification. |
| child MIME composition | missing | Depends on MIME child-entity write support. |

Complexity: recipient properties 2, default headers 3, delivery options 4, child MIME composition 5.

## NotesView / NotesViewNavigator

| Member | Status | Notes |
|---|---|---|
| history/current-entry semantics | runtime | Navigation surface exists; live history behavior is not verified. |
| column formula/value edge cases | runtime | Columns exist; type conversion and missing-value behavior need live tests. |
| `Goto*`/`Get*` parity | runtime | Broad methods exist; all overloads and boundary positions need runtime verification. |
| hierarchy edge cases | runtime | Parent/child/sibling behavior needs a real categorized view. |

Complexity: history/current-entry semantics 4, column values 3, `Goto*`/`Get*` parity 3, hierarchy edge cases 4.

## NotesAgent / DXL / support objects

| Member or area | Status | Notes |
|---|---|---|
| agent `Run` overloads | runtime | Implemented; requires a real agent and Notes runtime. |
| agent result output/status | runtime | Implemented; needs live `STATUS OK` agent verification. |
| DXL importer result members | runtime | Properties exist; result log/count and imported design behavior need live verification. |
| DXL exporter result members | runtime | Properties/overloads exist; exported XML and cleanup behavior need live verification. |
| `NotesInternational` | missing | No public C# object. |
| `NotesRichTextTab` | partial | Rich-text tab support exists as an internal linked object; complete LotusScript property parity needs review. |

Complexity: agent run/result 4, DXL importer 4, DXL exporter 4, `NotesInternational` 5, `NotesRichTextTab` 3.

## Objects with no identified complete absence

Within the Notes object set currently represented by XPscript, no entire primary object was found with no C# counterpart. The C# counterparts exist for `NotesSession`, `NotesDatabase`, `NotesDbDirectory`, `NotesDocument`, collections, items, rich text, MIME, mail, views, agents, forms, streams, names, date/time, DXL and embedded objects.

The missing entries above are therefore primarily members and behaviors, not whole primary objects. `NotesInternational` is the clearest support object currently missing entirely.


