# LotusScript Notes API inventory

This is the local object-by-object inventory of the LotusScript Notes API against the C# implementation. The reference is the HCL LotusScript Notes surface, cross-checked against the local HCL C API and JNI projects compiler/runtime sources. It is deliberately separate from the HCL C API and JNI projects gap document so that LotusScript remains the API target and HCL C API and JNI projects remains an implementation reference.

## Status

- **Implemented**: the C# surface exists and local/static checks pass.
- **Partial**: the object exists, but members, semantics, native ABI, or lifecycle behavior remain incomplete.
- **Runtime required**: local review is complete enough to test, but a real Notes/Domino runtime is needed for confidence.
- **Missing**: no public C# equivalent exists yet.

## Inventory summary

| LotusScript object | C# object | Status | Main remaining work |
|---|---|---|---|
| NotesSession | NotesSession | Runtime required | Initialization, thread affinity, NotesIni/environment behavior |
| NotesDatabase | NotesDatabase | Partial | Full member sweep; full-text, design, metadata and alias edge cases |
| NotesDbDirectory | NotesDBDirectory | Runtime required | Enumeration filters and remote behavior |
| NotesDatabaseACL | NotesDatabaseAccess | Runtime required | Live ACL entries, roles and error behavior |
| NotesDocument | NotesDocument | Partial | Overloads, error parity and failed-call lifecycle |
| NotesDocumentCollection | NotesDocumentCollection | Partial | Provenance and bulk-operation edge cases |
| NotesNoteCollection | NotesNoteCollection | Partial | Selection, provenance and bulk-operation edge cases |
| NotesItem | NotesItem | Partial | Complete item-type/list/attachment matrix |
| NotesRichTextItem | NotesRichTextItem | Partial | Native composite edge cases |
| NotesRichTextNavigator/Range | NotesRichTextNavigator/Range | Partial | Structural writes and corrupt-record behavior |
| NotesMIMEEntity | NotesMIMEEntity | Partial | Child creation, content mutation and removal |
| NotesMIMEHeader | NotesMIMEHeader | Partial | Child header mutation/removal and nested multipart behavior |
| NotesMail | NotesMail | Runtime required | Delivery options and live error paths |
| NotesView/NotesViewNavigator | NotesView/NotesViewNavigator | Runtime required | History, columns and navigation edge cases |
| NotesViewEntry/Collection | NotesViewEntry/Collection | Partial | Live hierarchy and history behavior |
| NotesAgent | NotesAgent | Runtime required | Agent execution and returned output |
| NotesForm | NotesForm | Partial | Complete design-note member sweep |
| NotesEmbeddedObject | NotesEmbeddedObject | Partial | All object types, byte arrays and extraction lifecycle |
| NotesStream | NotesStream | Runtime required | Native boundaries and charset behavior |
| NotesName | NotesName | Partial | Parsed components and international names |
| NotesDateTime | NotesDateTime | Runtime required | Wildcards, zones, DST and formatting |
| NotesRichTextStyle/ParagraphStyle | C# style objects | Partial | Native paragraph composite writes |
| NotesDXLImporter/Exporter | C# DXL objects | Runtime required | Result properties, overloads and live import/export |

The machine-readable version is [lotusscript-api-inventory.json](lotusscript-api-inventory.json).

## What can be completed locally

- Reflection inventory of every public object/member, overload, nullable annotation and return type.
- Verification that public APIs expose C# types rather than LotusScript/HCL C API and JNI projects types.
- Native delegate ABI review: pointer widths, parameter order, buffer lengths and lifecycle ownership.
- MIME parser/encoding tests with byte and string fixtures.
- Rich-text record transformation and corruption tests.
- Package, XML documentation, LICENSE and NOTICE tests.

## What requires a Notes/Domino runtime

- `NotesSession` initialization and username conversion.
- Database, document, view and agent behavior.
- Mail delivery and delivery-error paths.
- MIME child creation/mutation/removal.
- Rich-text native record writes.
- DXL import/export against an NSF.

## Source references

- HCL C API and JNI parity notes
- [C# IntelliSense API reference](intellisense-api-reference.md)
- HCL Notes/Domino C API documentation and JNI projects
- [Machine-readable inventory](lotusscript-api-inventory.json)
