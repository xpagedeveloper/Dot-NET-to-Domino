# LotusScript Notes objects missing from this repository

The earlier inventory only covered the Notes object families already represented by the XPscript runtime. This expanded baseline compares the repository's public C# types with the broader LotusScript backend object families. These objects are not currently exposed as public DotNetToDomino types unless explicitly marked as present.

Complexity uses the same 1–5 scale as [lotusscript-member-gaps.md](lotusscript-member-gaps.md). The list is a work inventory: each candidate still needs member-by-member confirmation against the HCL LotusScript Language Reference before implementation.

## Entire object families currently absent

| LotusScript object | Complexity | Why it matters / likely scope |
|---|---:|---|
| `NotesACL` | 4 | Database ACL container; current `NotesDatabaseAccess` is an access snapshot, not the full ACL API. |
| `NotesACLEntry` | 3 | ACL name, level, roles, reader/authentication flags and mutation. |
| `NotesDateRange` | 2 | Start/end `NotesDateTime` value object. |
| `NotesInternational` | 5 | Locale, charset, date/time and number-format information. |
| `NotesLog` | 3 | Log destination and message/error reporting. |
| `NotesNewsletter` | 4 | Newsletter creation and delivery from document collections. |
| `NotesAdministrationProcess` | 5 | Domino administration requests and server-side workflows. |
| `NotesRegistration` | 5 | Person/server registration and ID-file operations. |
| `NotesDOMParser` and DOM node classes | 5 | XML DOM parsing/manipulation; unrelated to the native NSF object model. |
| `NotesSAXParser` and SAX support classes | 5 | XML event parsing; unrelated to the native NSF object model. |
| `NotesXSLTransformer` | 5 | XSL transformation runtime; separate XML subsystem. |

## Object families present but substantially incomplete

| Object | Main missing members/areas | Complexity |
|---|---|---:|
| `NotesSession` | current context (`CurrentDatabase`, `CurrentAgent`, `DocumentContext`), effective user, international settings, exit status | 2–5 |
| `NotesDatabase` | complete ACL object, address-book flags, template/open-if-modified, compact/fixup, replica creation, mail helpers | 3–5 |
| `NotesDocument` | security flags, parent object, send flags, encryption/signing helpers, complete item/response semantics | 3–5 |
| `NotesItem` | complete item type matrix, list coercion, item flag/error parity | 3–4 |
| `NotesRichTextItem` | table/doclink/object append operations, paragraph-style native write, full composite record coverage | 4–5 |
| `NotesMIMEEntity` | child creation, child content writes, child removal, nested multipart mutation | 5 |
| `NotesMail` | complete delivery and delegated-sender semantics, child MIME composition | 4–5 |
| `NotesView`/`NotesViewNavigator` | history, hierarchy, columns, unread/cache semantics and boundary behavior | 4–5 |
| `NotesAgent` | full scheduling/design properties, run context and output/error semantics | 4–5 |
| `NotesDXLImporter`/`NotesDXLExporter` | complete options, result properties and error/log semantics | 4 |
| `NotesEmbeddedObject` | OLE/object types, native extraction and lifecycle edge cases | 4–5 |
| `NotesName` | complete parsed components, international/RFC822 behavior | 3–4 |
| `NotesDateTime` | complete wildcard, locale, zone and DST semantics | 3–4 |
| `NotesRichTextTable` | table cell/row wrappers, complete table mutation and properties | 4 |
| `NotesRichTextTab` | tab stop properties and complete write semantics | 3 |
| `NotesRichTextParagraphStyle` | native paragraph-style writes | 4 |
| `NotesRichTextDocLink` | complete linked-object properties and mutation | 4 |
| `NotesRichTextSection` | complete section properties and mutation | 4 |
| `NotesMIMEHeader` | child-header creation, mutation and removal | 5 |

## Present and comparatively covered

These object families have public C# counterparts, but still require live behavior tests: `NotesSession`, `NotesDatabase`, `NotesDbDirectory`, `NotesDocument`, `NotesDocumentCollection`, `NotesNoteCollection`, `NotesItem`, `NotesRichTextItem`, `NotesMIMEEntity`, `NotesMIMEHeader`, `NotesMail`, `NotesView`, `NotesViewColumn`, `NotesViewEntry`, `NotesViewEntryCollection`, `NotesViewNavigator`, `NotesAgent`, `NotesAgentResult`, `NotesForm`, `NotesEmbeddedObject`, `NotesStream`, `NotesName`, `NotesDateTime`, `NotesDXLImporter`, and `NotesDXLExporter`.

## Scope decision

The following families should not be silently added as stubs: `NotesAdministrationProcess`, `NotesRegistration`, XML DOM/SAX/XSL classes and `NotesLog`. They need separate design decisions because they expose server administration, identity management, XML runtimes or logging infrastructure rather than the Notes document/database API. They remain explicit TODOs until their native/runtime contracts are selected.

The next implementation step is to normalize the HCL LotusScript reference into a per-object member catalog and compare every member against reflection output from the C# assembly. The existing [lotusscript-member-gaps.md](lotusscript-member-gaps.md) remains the actionable list for the object families already in scope.
