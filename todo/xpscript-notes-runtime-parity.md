# TODO: Current XPScript Notes runtime parity

Reference project: `xpagedeveloper/XPscript`
Target project: `xpagedeveloper/Dot-NET-to-Domino`

Baseline target: `aac605230669166c1a0c44efaa6198ed31d37500` (`main` when this TODO was created).

## Rules for this work

For every Notes object/member below, do not compare only names or signatures. Inspect the physical XPScript implementation behind the member and trace it through its native helper(s).

For every member:

1. Find the public surface in XPScript source, `docs`, and `samples`.
2. Read the actual runtime/postprocessor implementation.
3. Identify the Notes/Domino C API calls and constants used.
4. Verify handle ownership, NOTEID/UNID semantics, memory allocation/locking/freeing, recycle/close behavior, and error/status handling.
5. Compare the corresponding Dot-NET-to-Domino implementation.
6. If XPScript is unclear, use HCL Domino JNX next and Notes/Domino C API documentation as the final contract.
7. Implement only real behavior. Do not expose static placeholders, no-op setters, fabricated values, or methods whose only implementation is `NotSupported`.
8. Preserve useful .NET-specific behavior only where it does not misrepresent Notes runtime semantics.
9. Add tests/build coverage where practical and require clean CI before merge.

Status markers:

- `[ ]` not audited/implemented
- `[~]` partially implemented or requires parity verification
- `[x]` verified equivalent
- `[!]` API/ABI/lifecycle issue requiring special review

## P0 — New Notes objects

### NotesMIMEEntity

- [ ] Add `NotesMIMEEntity` based on the current XPScript implementation.
- [ ] Add `NotesDocument.CreateMIMEEntity(...)` overloads supported by XPScript.
- [ ] Add `NotesDocument.GetMIMEEntity(...)` overloads supported by XPScript.
- [ ] Add `NotesDocument.CloseMIMEEntities(...)` with the same save/recycle semantics.
- [ ] Port MIME entity parent/child/sibling traversal using the same native handles and ownership rules.
- [ ] Port child enumeration and `AppendChildEntity` behavior.
- [ ] Port entity removal and ensure handles become invalid safely after removal.
- [ ] Port content type/subtype, charset, encoding and preamble semantics that are physically implemented.
- [ ] Port content stream/text readers and writers that are physically implemented.
- [ ] Port MIME encoding/decoding behavior, including current encoding classification logic.
- [ ] Port Content-ID and Content-Location behavior where implemented.
- [ ] Verify multipart MIME behavior and nested child entities.
- [ ] Verify MIME handles are closed exactly once and cannot outlive their document/session.
- [ ] Verify `NotesSession.ConvertMIME` interaction with document/item access.

### NotesMIMEHeader

- [ ] Add `NotesMIMEHeader` based on current XPScript code.
- [ ] Port header creation/access from `NotesMIMEEntity`.
- [ ] Port header name/value getters.
- [ ] Port header value setters/overloads implemented by XPScript.
- [ ] Port header parameter access/mutation if implemented.
- [ ] Port header removal and `RemoveHeaders` behavior.
- [ ] Verify header enumeration and duplicate-header semantics.
- [ ] Verify header/entity handle lifetime and native memory ownership.

### NotesDBDirectory

- [ ] Add `NotesDBDirectory` using the current XPScript native implementation.
- [ ] Add the corresponding `NotesSession` factory/access member.
- [ ] Port directory server/name properties.
- [ ] Port database enumeration/open behavior.
- [ ] Port first/next database iteration semantics if present in the current public surface.
- [ ] Port database type filtering and constants used by XPScript.
- [ ] Verify remote vs local server path behavior.
- [ ] Verify returned `NotesDatabase` objects are open/closed exactly as XPScript specifies.
- [ ] Verify native directory/search handles are released correctly.

### NotesDatabaseAccess

- [ ] Re-audit current XPScript `NotesDatabaseAccess` object against the implementation prepared in the parity branch.
- [ ] Verify `QueryAccess()` and `QueryAccess(userName)`.
- [ ] Verify `NSFBuildNamesList`, `NSFDbReadACL`, `ACLLookupAccess` ABI declarations.
- [ ] Verify ACL handle/names-list/roles-list locking and freeing.
- [ ] Verify `UserName`, `Level`, `LevelName`, `AccessFlags`, `NamesList`, `Groups`, `Roles`.
- [ ] Verify every capability flag against the exact Domino ACL flag constants; do not keep guessed numeric values.

## P0 — NotesSession additions/changes

- [~] Re-audit all current `NotesSession` docs/source/samples against target. Source/runtime paths below were audited; live HCL runtime verification remains outstanding.
- [x] `HashPassword` — uses Domino `SECHashPassword`; buffer sizing and secret-memory clearing audited against XPScript.
- [x] `VerifyPassword` — uses Domino `SECVerifyPassword`; status and malformed/oversize input semantics audited against XPScript.
- [x] `AddressBooks` — `NAMEGetAddressBooks`/`OSPathNetParse` path and closed returned database wrappers audited against XPScript.
- [~] `SetEnvironmentVar` — `# TODO: Current XPScript Notes runtime parity

Reference project: `xpagedeveloper/XPscript`
Target project: `xpagedeveloper/Dot-NET-to-Domino`

Baseline target: `aac605230669166c1a0c44efaa6198ed31d37500` (`main` when this TODO was created).

## Rules for this work

For every Notes object/member below, do not compare only names or signatures. Inspect the physical XPScript implementation behind the member and trace it through its native helper(s).

For every member:

1. Find the public surface in XPScript source, `docs`, and `samples`.
2. Read the actual runtime/postprocessor implementation.
3. Identify the Notes/Domino C API calls and constants used.
4. Verify handle ownership, NOTEID/UNID semantics, memory allocation/locking/freeing, recycle/close behavior, and error/status handling.
5. Compare the corresponding Dot-NET-to-Domino implementation.
6. If XPScript is unclear, use HCL Domino JNX next and Notes/Domino C API documentation as the final contract.
7. Implement only real behavior. Do not expose static placeholders, no-op setters, fabricated values, or methods whose only implementation is `NotSupported`.
8. Preserve useful .NET-specific behavior only where it does not misrepresent Notes runtime semantics.
9. Add tests/build coverage where practical and require clean CI before merge.

Status markers:

- `[ ]` not audited/implemented
- `[~]` partially implemented or requires parity verification
- `[x]` verified equivalent
- `[!]` API/ABI/lifecycle issue requiring special review

## P0 — New Notes objects

### NotesMIMEEntity

- [ ] Add `NotesMIMEEntity` based on the current XPScript implementation.
- [ ] Add `NotesDocument.CreateMIMEEntity(...)` overloads supported by XPScript.
- [ ] Add `NotesDocument.GetMIMEEntity(...)` overloads supported by XPScript.
- [ ] Add `NotesDocument.CloseMIMEEntities(...)` with the same save/recycle semantics.
- [ ] Port MIME entity parent/child/sibling traversal using the same native handles and ownership rules.
- [ ] Port child enumeration and `AppendChildEntity` behavior.
- [ ] Port entity removal and ensure handles become invalid safely after removal.
- [ ] Port content type/subtype, charset, encoding and preamble semantics that are physically implemented.
- [ ] Port content stream/text readers and writers that are physically implemented.
- [ ] Port MIME encoding/decoding behavior, including current encoding classification logic.
- [ ] Port Content-ID and Content-Location behavior where implemented.
- [ ] Verify multipart MIME behavior and nested child entities.
- [ ] Verify MIME handles are closed exactly once and cannot outlive their document/session.
- [ ] Verify `NotesSession.ConvertMIME` interaction with document/item access.

### NotesMIMEHeader

- [ ] Add `NotesMIMEHeader` based on current XPScript code.
- [ ] Port header creation/access from `NotesMIMEEntity`.
- [ ] Port header name/value getters.
- [ ] Port header value setters/overloads implemented by XPScript.
- [ ] Port header parameter access/mutation if implemented.
- [ ] Port header removal and `RemoveHeaders` behavior.
- [ ] Verify header enumeration and duplicate-header semantics.
- [ ] Verify header/entity handle lifetime and native memory ownership.

### NotesDBDirectory

- [ ] Add `NotesDBDirectory` using the current XPScript native implementation.
- [ ] Add the corresponding `NotesSession` factory/access member.
- [ ] Port directory server/name properties.
- [ ] Port database enumeration/open behavior.
- [ ] Port first/next database iteration semantics if present in the current public surface.
- [ ] Port database type filtering and constants used by XPScript.
- [ ] Verify remote vs local server path behavior.
- [ ] Verify returned `NotesDatabase` objects are open/closed exactly as XPScript specifies.
- [ ] Verify native directory/search handles are released correctly.

### NotesDatabaseAccess

- [ ] Re-audit current XPScript `NotesDatabaseAccess` object against the implementation prepared in the parity branch.
- [ ] Verify `QueryAccess()` and `QueryAccess(userName)`.
- [ ] Verify `NSFBuildNamesList`, `NSFDbReadACL`, `ACLLookupAccess` ABI declarations.
- [ ] Verify ACL handle/names-list/roles-list locking and freeing.
- [ ] Verify `UserName`, `Level`, `LevelName`, `AccessFlags`, `NamesList`, `Groups`, `Roles`.
- [ ] Verify every capability flag against the exact Domino ACL flag constants; do not keep guessed numeric values.

## P0 — NotesSession additions/changes

- [~] Re-audit all current `NotesSession` docs/source/samples against target. Source/runtime paths below were audited; live HCL runtime verification remains outstanding.
- [x] `HashPassword` — uses Domino `SECHashPassword`; buffer sizing and secret-memory clearing audited against XPScript.
- [x] `VerifyPassword` — uses Domino `SECVerifyPassword`; status and malformed/oversize input semantics audited against XPScript.
- [x] `AddressBooks` — `NAMEGetAddressBooks`/`OSPathNetParse` path and closed returned database wrappers audited against XPScript.
 prefix/system-variable semantics and `OSSetEnvironmentVariable` native path match; date-to-string formatting still needs live/runtime compatibility verification.
- [x] `GetEnvironmentString` — native read and `# TODO: Current XPScript Notes runtime parity

Reference project: `xpagedeveloper/XPscript`
Target project: `xpagedeveloper/Dot-NET-to-Domino`

Baseline target: `aac605230669166c1a0c44efaa6198ed31d37500` (`main` when this TODO was created).

## Rules for this work

For every Notes object/member below, do not compare only names or signatures. Inspect the physical XPScript implementation behind the member and trace it through its native helper(s).

For every member:

1. Find the public surface in XPScript source, `docs`, and `samples`.
2. Read the actual runtime/postprocessor implementation.
3. Identify the Notes/Domino C API calls and constants used.
4. Verify handle ownership, NOTEID/UNID semantics, memory allocation/locking/freeing, recycle/close behavior, and error/status handling.
5. Compare the corresponding Dot-NET-to-Domino implementation.
6. If XPScript is unclear, use HCL Domino JNX next and Notes/Domino C API documentation as the final contract.
7. Implement only real behavior. Do not expose static placeholders, no-op setters, fabricated values, or methods whose only implementation is `NotSupported`.
8. Preserve useful .NET-specific behavior only where it does not misrepresent Notes runtime semantics.
9. Add tests/build coverage where practical and require clean CI before merge.

Status markers:

- `[ ]` not audited/implemented
- `[~]` partially implemented or requires parity verification
- `[x]` verified equivalent
- `[!]` API/ABI/lifecycle issue requiring special review

## P0 — New Notes objects

### NotesMIMEEntity

- [ ] Add `NotesMIMEEntity` based on the current XPScript implementation.
- [ ] Add `NotesDocument.CreateMIMEEntity(...)` overloads supported by XPScript.
- [ ] Add `NotesDocument.GetMIMEEntity(...)` overloads supported by XPScript.
- [ ] Add `NotesDocument.CloseMIMEEntities(...)` with the same save/recycle semantics.
- [ ] Port MIME entity parent/child/sibling traversal using the same native handles and ownership rules.
- [ ] Port child enumeration and `AppendChildEntity` behavior.
- [ ] Port entity removal and ensure handles become invalid safely after removal.
- [ ] Port content type/subtype, charset, encoding and preamble semantics that are physically implemented.
- [ ] Port content stream/text readers and writers that are physically implemented.
- [ ] Port MIME encoding/decoding behavior, including current encoding classification logic.
- [ ] Port Content-ID and Content-Location behavior where implemented.
- [ ] Verify multipart MIME behavior and nested child entities.
- [ ] Verify MIME handles are closed exactly once and cannot outlive their document/session.
- [ ] Verify `NotesSession.ConvertMIME` interaction with document/item access.

### NotesMIMEHeader

- [ ] Add `NotesMIMEHeader` based on current XPScript code.
- [ ] Port header creation/access from `NotesMIMEEntity`.
- [ ] Port header name/value getters.
- [ ] Port header value setters/overloads implemented by XPScript.
- [ ] Port header parameter access/mutation if implemented.
- [ ] Port header removal and `RemoveHeaders` behavior.
- [ ] Verify header enumeration and duplicate-header semantics.
- [ ] Verify header/entity handle lifetime and native memory ownership.

### NotesDBDirectory

- [ ] Add `NotesDBDirectory` using the current XPScript native implementation.
- [ ] Add the corresponding `NotesSession` factory/access member.
- [ ] Port directory server/name properties.
- [ ] Port database enumeration/open behavior.
- [ ] Port first/next database iteration semantics if present in the current public surface.
- [ ] Port database type filtering and constants used by XPScript.
- [ ] Verify remote vs local server path behavior.
- [ ] Verify returned `NotesDatabase` objects are open/closed exactly as XPScript specifies.
- [ ] Verify native directory/search handles are released correctly.

### NotesDatabaseAccess

- [ ] Re-audit current XPScript `NotesDatabaseAccess` object against the implementation prepared in the parity branch.
- [ ] Verify `QueryAccess()` and `QueryAccess(userName)`.
- [ ] Verify `NSFBuildNamesList`, `NSFDbReadACL`, `ACLLookupAccess` ABI declarations.
- [ ] Verify ACL handle/names-list/roles-list locking and freeing.
- [ ] Verify `UserName`, `Level`, `LevelName`, `AccessFlags`, `NamesList`, `Groups`, `Roles`.
- [ ] Verify every capability flag against the exact Domino ACL flag constants; do not keep guessed numeric values.

## P0 — NotesSession additions/changes

- [~] Re-audit all current `NotesSession` docs/source/samples against target. Source/runtime paths below were audited; live HCL runtime verification remains outstanding.
- [x] `HashPassword` — uses Domino `SECHashPassword`; buffer sizing and secret-memory clearing audited against XPScript.
- [x] `VerifyPassword` — uses Domino `SECVerifyPassword`; status and malformed/oversize input semantics audited against XPScript.
- [x] `AddressBooks` — `NAMEGetAddressBooks`/`OSPathNetParse` path and closed returned database wrappers audited against XPScript.
- [~] `SetEnvironmentVar` — `# TODO: Current XPScript Notes runtime parity

Reference project: `xpagedeveloper/XPscript`
Target project: `xpagedeveloper/Dot-NET-to-Domino`

Baseline target: `aac605230669166c1a0c44efaa6198ed31d37500` (`main` when this TODO was created).

## Rules for this work

For every Notes object/member below, do not compare only names or signatures. Inspect the physical XPScript implementation behind the member and trace it through its native helper(s).

For every member:

1. Find the public surface in XPScript source, `docs`, and `samples`.
2. Read the actual runtime/postprocessor implementation.
3. Identify the Notes/Domino C API calls and constants used.
4. Verify handle ownership, NOTEID/UNID semantics, memory allocation/locking/freeing, recycle/close behavior, and error/status handling.
5. Compare the corresponding Dot-NET-to-Domino implementation.
6. If XPScript is unclear, use HCL Domino JNX next and Notes/Domino C API documentation as the final contract.
7. Implement only real behavior. Do not expose static placeholders, no-op setters, fabricated values, or methods whose only implementation is `NotSupported`.
8. Preserve useful .NET-specific behavior only where it does not misrepresent Notes runtime semantics.
9. Add tests/build coverage where practical and require clean CI before merge.

Status markers:

- `[ ]` not audited/implemented
- `[~]` partially implemented or requires parity verification
- `[x]` verified equivalent
- `[!]` API/ABI/lifecycle issue requiring special review

## P0 — New Notes objects

### NotesMIMEEntity

- [ ] Add `NotesMIMEEntity` based on the current XPScript implementation.
- [ ] Add `NotesDocument.CreateMIMEEntity(...)` overloads supported by XPScript.
- [ ] Add `NotesDocument.GetMIMEEntity(...)` overloads supported by XPScript.
- [ ] Add `NotesDocument.CloseMIMEEntities(...)` with the same save/recycle semantics.
- [ ] Port MIME entity parent/child/sibling traversal using the same native handles and ownership rules.
- [ ] Port child enumeration and `AppendChildEntity` behavior.
- [ ] Port entity removal and ensure handles become invalid safely after removal.
- [ ] Port content type/subtype, charset, encoding and preamble semantics that are physically implemented.
- [ ] Port content stream/text readers and writers that are physically implemented.
- [ ] Port MIME encoding/decoding behavior, including current encoding classification logic.
- [ ] Port Content-ID and Content-Location behavior where implemented.
- [ ] Verify multipart MIME behavior and nested child entities.
- [ ] Verify MIME handles are closed exactly once and cannot outlive their document/session.
- [ ] Verify `NotesSession.ConvertMIME` interaction with document/item access.

### NotesMIMEHeader

- [ ] Add `NotesMIMEHeader` based on current XPScript code.
- [ ] Port header creation/access from `NotesMIMEEntity`.
- [ ] Port header name/value getters.
- [ ] Port header value setters/overloads implemented by XPScript.
- [ ] Port header parameter access/mutation if implemented.
- [ ] Port header removal and `RemoveHeaders` behavior.
- [ ] Verify header enumeration and duplicate-header semantics.
- [ ] Verify header/entity handle lifetime and native memory ownership.

### NotesDBDirectory

- [ ] Add `NotesDBDirectory` using the current XPScript native implementation.
- [ ] Add the corresponding `NotesSession` factory/access member.
- [ ] Port directory server/name properties.
- [ ] Port database enumeration/open behavior.
- [ ] Port first/next database iteration semantics if present in the current public surface.
- [ ] Port database type filtering and constants used by XPScript.
- [ ] Verify remote vs local server path behavior.
- [ ] Verify returned `NotesDatabase` objects are open/closed exactly as XPScript specifies.
- [ ] Verify native directory/search handles are released correctly.

### NotesDatabaseAccess

- [ ] Re-audit current XPScript `NotesDatabaseAccess` object against the implementation prepared in the parity branch.
- [ ] Verify `QueryAccess()` and `QueryAccess(userName)`.
- [ ] Verify `NSFBuildNamesList`, `NSFDbReadACL`, `ACLLookupAccess` ABI declarations.
- [ ] Verify ACL handle/names-list/roles-list locking and freeing.
- [ ] Verify `UserName`, `Level`, `LevelName`, `AccessFlags`, `NamesList`, `Groups`, `Roles`.
- [ ] Verify every capability flag against the exact Domino ACL flag constants; do not keep guessed numeric values.

## P0 — NotesSession additions/changes

- [~] Re-audit all current `NotesSession` docs/source/samples against target. Source/runtime paths below were audited; live HCL runtime verification remains outstanding.
- [x] `HashPassword` — uses Domino `SECHashPassword`; buffer sizing and secret-memory clearing audited against XPScript.
- [x] `VerifyPassword` — uses Domino `SECVerifyPassword`; status and malformed/oversize input semantics audited against XPScript.
- [x] `AddressBooks` — `NAMEGetAddressBooks`/`OSPathNetParse` path and closed returned database wrappers audited against XPScript.
 prefix/system-variable semantics and `OSSetEnvironmentVariable` native path match; date-to-string formatting still needs live/runtime compatibility verification.
/system naming semantics audited against XPScript.
- [x] `GetEnvironmentValue` — 32-bit integer conversion plus missing/non-numeric `null` behavior audited against XPScript.
- [x] `GetDatabase` — `createOnFail` and closed-wrapper/null behavior audited against current XPScript implementation.
- [x] `OpenByReplicaID` — locate/open flow, closed error wrapper and invalid replica-ID runtime error semantics aligned with XPScript.
- [x] Factory/access member for `NotesDBDirectory` added directly to `NotesSession`, matching the XPScript surface.
- [x] MIME-related session behavior: `ConvertMIME=false` now opens notes with `OPEN_RAW_MIME_PART` through `NSFNoteOpenExt` / `NSFNoteOpenByUNIDExtended`, matching XPScript/JNX semantics.
- [~] Inventory any other NotesSession members added since the target baseline; no additional blocker found in the audited P0 set, but the broader runtime inventory remains open.

## P0 — NotesDocument additions/changes

- [ ] Re-audit current `NotesDocument` source/docs/samples member-by-member.
- [ ] `Authors`.
- [ ] `SentByAgent`.
- [ ] `NameOfProfile`.
- [ ] `Key`.
- [ ] `IsProfile` current semantics.
- [ ] Native-backed `LastAccessed`.
- [ ] Native/status-backed `IsDeleted`.
- [ ] Native/status-backed `IsValid`.
- [ ] MIME entity creation/get/close integration.
- [ ] Verify MIME/rich-text/item conversion interactions.
- [ ] Recheck `Remove` lifecycle after the corrected `NSFNoteDelete(database, NOTEID, flags)` ABI.
- [ ] Recheck response-table reopen behavior before package publication.
- [ ] Inventory other document members changed in recent XPScript commits.

## P0 — NotesAgent additions/changes

- [ ] Re-audit the latest XPScript `NotesAgent` implementation; do not rely on the September 4 target snapshot.
- [ ] `ReturnMessage` and stdout normalization.
- [ ] Current `Run(noteId)` parameter-document semantics.
- [ ] `RunWithDocumentContext(document, noteId)` current semantics.
- [ ] `ParameterDocID` if now native-backed/real; omit if still placeholder.
- [ ] `IsEnabled` if now native-backed/real.
- [ ] `HasRunSinceModified` if now native-backed/real.
- [ ] `ServerName` getter/setter and persistence behavior.
- [ ] `IsPublic` current implementation.
- [ ] `Trigger` current implementation.
- [ ] `OnBehalfOf` current implementation.
- [ ] `CommonOwner` current implementation.
- [ ] Agent reopen/run behavior after save/sign/update.
- [ ] Private-agent lookup fallback.
- [ ] Unsigned-agent sign/update/retry behavior.
- [ ] Remove any remaining constant/no-op agent properties not backed by real XPScript behavior.

## P1 — NotesDatabase additions/changes

- [ ] Re-audit all current `NotesDatabase` source/docs/samples.
- [ ] `Open(...)` assigned/unassigned database semantics.
- [ ] `OpenError` behavior and reset rules.
- [ ] `QueryAccess` integration.
- [ ] `OpenByReplicaID` convenience aliases.
- [ ] `Agents`, `Forms`, `Views` stale-design-note handling.
- [ ] `CreateCopy` overload shape versus optional-parameter target API.
- [ ] `GetModifiedDocuments` note-class constants and metadata.
- [ ] Inventory any database members added after the target baseline.

## P1 — NotesDateTime

- [ ] Re-audit current XPScript `NotesDateTime` implementation.
- [ ] Port `SetNow` if missing.
- [ ] Port current `TimeDifference` behavior.
- [ ] Port current `TimeDifferenceDouble`/related variants if public and implemented.
- [ ] Verify timezone/zone/DST handling uses Notes `TIMEDATE` semantics rather than .NET approximations.
- [ ] Verify wildcard date/time behavior if exposed.

## P1 — NotesView / NotesViewColumn / NotesViewEntry / navigator

- [ ] Re-audit current `NotesView` implementation and docs.
- [ ] Verify all recently added view properties and methods.
- [ ] Re-audit `NotesViewColumn` physical `$VIEWFORMAT` parsing and formula decompile behavior.
- [ ] Audit `NotesViewEntry` additions/changes.
- [ ] Audit navigator positioning additions and updated entry traversal semantics.
- [ ] Verify native collection buffer ownership and snapshot-vs-streaming differences.
- [ ] Do not expose view properties that are only inferred/static placeholders.

## P1 — NotesItem

- [ ] Re-audit current `NotesItem` implementation.
- [ ] Verify `Text` uses `NSFItemConvertToText` with XPScript-equivalent buffer/error semantics.
- [ ] Verify `Remove()` uses the same item BLOCKID deletion lifecycle as XPScript.
- [ ] Verify item type/flag mapping against Domino constants.
- [ ] Inventory new item properties/functions added in recent commits.

## P1 — Rich text object family

### NotesRichTextItem

- [ ] Re-audit the latest XPScript rich-text implementation from physical code, not old TODOs.
- [ ] `ConvertToHTML()`.
- [ ] `ConvertToHTML(inlineImages)` including data-URI/image conversion behavior.
- [ ] Verify styled append behavior and style-state lifetime.
- [ ] Port only structural write operations that now have real CD rewrite implementations.
- [ ] Ensure rollback/atomicity matches XPScript when CD rewrite fails.

### NotesRichTextNavigator

- [ ] Port/verify `SetPosition`.
- [ ] Port/verify `SetPositionAtEnd`.
- [ ] Verify section/table/doclink/hotspot boundary positioning.
- [ ] Verify navigator invalidation after rich-text mutation.

### NotesRichTextRange

- [ ] Audit new range mutation functions.
- [ ] Port `Remove` if physically implemented.
- [ ] Port `SetStyle` if physically implemented.
- [ ] Verify logical-span calculations and mutation rollback.

### NotesRichTextSection

- [ ] Verify real CD-rewrite `Remove()` implementation.
- [ ] Verify `SetBarColor()`.
- [ ] Verify `SetTitleStyle()`.
- [ ] Verify nested section-span handling.

### NotesRichTextTable

- [ ] Re-audit current table reader/parser.
- [ ] Port newly implemented table mutations only where physical CD rewrite exists.
- [ ] Verify logical table spans/row/cell parsing.
- [ ] Do not expose `RowLabels`, `Color`, `AlternateColor` unless current XPScript has real implementations.

### NotesRichTextDocLink

- [ ] Re-audit current doclink reader/mutation implementation.
- [ ] Do not expose `HotSpotTextStyle` unless current XPScript has a real implementation.

### NotesEmbeddedObject

- [ ] Audit recent attachment/embedded-object changes.
- [ ] Port attachment `Remove` if now implemented.
- [ ] Verify attachment extraction/save/remove lifecycle and rich-text revision invalidation.

## P1 — NotesForm and design objects

- [ ] Re-audit current `NotesForm` surface and implementation.
- [ ] Inventory any new form properties/functions added recently.
- [ ] Audit any newly introduced design-element objects in XPScript.
- [ ] For design mutation APIs, distinguish real native/DXL implementation from compiler-only scaffolding.

## P1 — NotesDocumentCollection / NotesNoteCollection

- [ ] Re-audit recent collection changes.
- [ ] Verify NoteID normalization changes.
- [ ] Verify modified-document metadata and design/document classification.
- [ ] Verify collection ownership/recycle behavior.
- [ ] Audit any new collection mutation/search members.

## P2 — Remaining Notes object inventory

Audit every current public Notes object found in XPScript `docs`, `samples`, and runtime source, including at minimum:

- [ ] `NotesName`
- [ ] `NotesStream`
- [ ] `NotesDXLImporter`
- [ ] `NotesDXLExporter`
- [ ] `NotesRichTextStyle`
- [ ] `NotesRichTextParagraphStyle`
- [ ] `NotesColorObject`
- [ ] `NotesForm`
- [ ] `NotesView`
- [ ] `NotesViewColumn`
- [ ] `NotesViewEntry`
- [ ] `NotesViewNavigator` / equivalent navigator objects
- [ ] `NotesDocumentCollection`
- [ ] `NotesNoteCollection`
- [ ] `NotesAgentResult`
- [ ] Any additional `Notes*` object present in current XPScript docs/runtime but absent from this list.

For each object, add a sub-section to this TODO if differences are found.

## Public-surface cleanup

- [ ] Search Dot-NET-to-Domino public Notes API for `NotSupportedException`, `NotesException(445, ...)`, constant `true`/`false`/`0`/empty-string properties, and no-op setters.
- [ ] Compare every hit with current XPScript physical implementation.
- [ ] Remove public members that are only unsupported/static placeholders in both implementations.
- [ ] Replace placeholders where XPScript has since gained real native behavior.
- [ ] Do not remove deliberate real constants that are part of the Notes contract without verifying the native/API meaning.

## Native ABI and lifecycle audit

For every newly ported native function:

- [ ] Confirm calling convention.
- [ ] Confirm exact parameter widths (`WORD`, `DWORD`, `NOTEID`, handles, pointers).
- [ ] Confirm signed/unsigned types.
- [ ] Confirm ownership of returned memory/handles.
- [ ] Confirm `OSLockObject`/`OSUnlockObject` pairing.
- [ ] Confirm `OSMemFree`/specialized free function requirements.
- [ ] Confirm LMBCS conversion and buffer lengths.
- [ ] Confirm status-code masking/text handling.
- [ ] Confirm open/close/recycle order.
- [ ] Confirm behavior when optional exports are absent on older runtimes.

## Verification and merge criteria

- [ ] Build `net10.0` with zero errors.
- [ ] Keep warnings at zero unless an explicitly documented unavoidable warning exists.
- [ ] Pack the NuGet package successfully.
- [ ] Run existing CI on the exact final head SHA.
- [ ] Inspect CI logs rather than relying only on the PR status badge.
- [ ] Review public API diff for accidental placeholders or compiler-only APIs.
- [ ] Review native ABI changes separately before merge.
- [ ] Document runtime-dependent behavior that cannot be tested without an installed HCL Notes/Domino runtime.
- [ ] Merge only after the final head is green.

## Known runtime-test limitation

CI currently validates compile/pack behavior but does not provide a live HCL Notes/Domino runtime. Native MIME, ACL, agent, rich-text, DB directory, password and database behavior therefore still needs a Windows/HCL runtime integration test pass before treating native parity as fully proven.
