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

## Scope rule

Only behavior confirmed in the XPScript implementation and backed by a verified Domino native contract should be added to the public API. Unsupported members should remain explicit rather than returning fabricated values or silently doing nothing.
