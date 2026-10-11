# XPScript Notes surface gap audit

This inventory compares the current `xpagedeveloper/XPscript` compiler/runtime sources with the public surface in this repository. It records candidates for implementation; a candidate is not considered complete until its native ABI, lifecycle, and runtime behavior have been verified.

Reference sources are under `XPscript/src/XPScript.Compiler` and the related Notes samples.

## Confirmed missing or incomplete areas

| Area | XPScript evidence | Dot-NET-to-Domino status | Next work |
| --- | --- | --- | --- |
| `NotesMail` | `NotesMailPostProcessor.cs`, `NotesMailSenderSemanticsPostProcessor.cs`, `samples/notes-mail-surface.xps` | No public `NotesMail` type or `NotesSession.CreateMail()` factory found | Port recipient, subject, body, delivery options, rich-text body, and send validation through the native mail path |
| MIME headers | `NotesMimeChildHeaderReadPostProcessor.cs`, `NotesMimeEntityDataHeaderPostProcessor.cs` | `NotesMIMEHeader` exists only as a placeholder; `GetNthHeader` and `CreateHeader` remain unsupported | Verify native header lookup, header lifetime, duplicate/continuation semantics, and mutation ABI |
| MIME child mutation | `NotesMimeChildMutationPostProcessor.cs`, `NotesMimeEmptyChildBootstrapPostProcessor.cs` | Child traversal exists, but child creation/removal and content mutation remain unsupported | Port child creation/removal and ownership invalidation |
| MIME root mutation | `NotesMimeRootMutationPostProcessor.cs` | Root reads are present; `SetContentFromText`, `SetContentFromBytes`, `EncodeContent`, `DecodeContent`, and `Preamble` remain unsupported | Verify native MIME stream/entity mutation and encoding behavior |
| Notes rich-text structural writes | `NotesRichTextStructuralWriteSurfacePostProcessor.cs` | Structural objects expose `Remove()` as unsupported in the current implementation | Verify native CD-record rewrite/delete semantics before implementing |

## Areas requiring member-by-member comparison

These areas have public C# coverage, but XPScript contains additional postprocessors or compatibility aliases that must be checked against the complete current surface:

- `NotesDocument` LotusScript aliases, `ComputeWithForm`, send overloads, and collection operations.
- `NotesDatabase` LotusScript aliases and full-text/design metadata.
- `NotesView`/`NotesViewNavigator` cache, history, and navigation limits.
- `NotesRichTextItem`, navigator/range mutation, attachments, HTML, MIME, and linked objects.
- `NotesDxlImporter`/`NotesDxlExporter` result properties and overloads.
- `NotesNoteCollection` provenance and `StampAllMulti` behavior.

## Object-by-object inventory

| XPScript object | C# object | Assessment |
| --- | --- | --- |
| `NotesSession` | `NotesSession` | Present; environment, password, database-open, address-book, DB-directory, MIME-open, and factory behavior have been audited in part. `CreateMail()` is missing. |
| `NotesDatabase` | `NotesDatabase` | Present; core open/create/copy, metadata, access, full-text, design, profile, document, agent, and alias surfaces are present. A complete member sweep remains. |
| `NotesDatabaseAccess` | `NotesDatabaseAccess` | Present; native ACL snapshot path was corrected. Live Domino ACL verification remains. |
| `NotesDbDirectory` | `NotesDBDirectory` | Present with a .NET naming difference; enumeration/open behavior needs full parity verification. |
| `NotesDocument` | `NotesDocument` | Present; metadata, item, MIME, response, send/save/remove and ComputeWithForm surfaces exist. Overload and runtime-error parity still needs a complete sweep. |
| `NotesDocumentCollection` | `NotesDocumentCollection` | Present; search/modified/provenance and collection operations exist. Stamp/provenance edge cases remain. |
| `NotesNoteCollection` | `NotesNoteCollection` | Present; selection and set operations exist. XPScript provenance and `StampAllMulti` require focused review. |
| `NotesItem` / `NotesRichTextItem` | Same | Present; item and rich-text entrypoints exist, but native mutation/attachment/HTML coverage is incomplete. |
| `NotesMIMEEntity` / `NotesMIMEHeader` | Same | Entity is partial; header is a placeholder and child/header/mutation APIs are not implemented. |
| `NotesMail` | `NotesMail` | Initial text-mail surface is implemented, including `NotesSession.CreateMail()`, recipients, headers, delivery options, and native document send. MIME, rich-text, and attachments remain open. |
| `NotesView` / `NotesViewColumn` | Same | Present; navigation and column surfaces exist. Cache/history/max-level and column edge semantics need comparison. |
| `NotesViewEntry` / collections / navigator | Same | Present; broad surface exists. XPScript-specific navigation/cache behavior needs validation. |
| `NotesAgent` / result | Same | Present; runtime parity branch was merged. Live native verification remains. |
| `NotesForm` | Same | Present as a design-note wrapper; full property/method parity needs review. |
| `NotesStream` | Same | Present; stream ABI and boundary behavior need comparison against `NotesStreamPostProcessor`. |
| `NotesName` / `NotesDateTime` | Same | Present; conversion and formatting edge cases need comparison. |
| `NotesDXLImporter` / `NotesDXLExporter` | Same | Present; result-property and overload parity needs review. |
| Rich-text linked objects | Same family | Section, table, doclink, color, navigator, range and style types are present; structural mutation remains incomplete. |
| `NotesEmbeddedObject` | Same | Present; binary-array and attachment semantics need comparison. |

## Scope rule

Only behavior confirmed in the XPScript implementation and backed by a verified Domino native contract should be added to the public API. Unsupported members should remain explicit rather than returning fabricated values or silently doing nothing.
