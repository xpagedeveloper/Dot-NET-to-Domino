# TODO: Current XPScript Notes runtime parity

See [`docs/xpscript-missing-notes-surface.md`](../docs/xpscript-missing-notes-surface.md) for the current cross-repository gap inventory.

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

- [~] `NotesMIMEEntity` exists and the native directory-backed core is present; several public data/header mutation members remain unsupported and are still being audited.
- [x] `NotesDocument.CreateMIMEEntity(...)` moved onto `NotesDocument` and now creates a packed native `MIME_PART` with `NSFItemAppend`, matching XPScript Body-only semantics.
- [x] `NotesDocument.GetMIMEEntity(...)` moved onto `NotesDocument` with the same current Body-only native-directory restriction as XPScript.
- [x] `NotesDocument.CloseMIMEEntities()` releases the cached native MIME directory; save/recycle paths also invalidate it before note update/close.
- [x] Port MIME entity parent/child/sibling traversal using the same native handles and ownership rules; direct child enumeration is implemented.
- [~] Port child enumeration; `AppendChildEntity` remains open because native child creation/mutation is not yet verified.
- [ ] Port entity removal and ensure handles become invalid safely after removal.
- [x] Content type/subtype and charset are native-directory backed; root `Encoding` is derived from `Content-Transfer-Encoding` like XPScript. Root multipart `Preamble` access/mutation is implemented; child-entity access remains open.
- [x] Root `ContentAsText`, `GetContentAsBytes`, `GetContentAsText`, and `GetEntityAsText` are implemented from the MIME stream; child-entity data access remains open.
- [x] Root content writers and `EncodeContent`/`DecodeContent` support 7bit/8bit/binary, quoted-printable, and base64; child-entity mutation remains open.
- [x] `ContentID` and `ContentLocation` are backed by native MIME entity access and match the current XPScript path.
- [~] Verify multipart MIME behavior and nested child entities; managed traversal and root preamble behavior are implemented, but live HCL runtime validation remains open.
- [x] MIME directory ownership is centralized and invalidated on close/save/recycle; full child/header lifetime verification remains open until the remaining native entity/header surface is complete.
- [x] Verify `NotesSession.ConvertMIME` interaction with document/item access; document open-by-note-ID and open-by-UNID pass `OPEN_RAW_MIME_PART` when conversion is disabled.

### NotesMIMEHeader

- [x] Add `NotesMIMEHeader` based on the current C# root-header implementation; lookup/value/parameter reads are implemented from the serialized MIME stream.
- [x] Port root header access from `NotesMIMEEntity`.
- [x] Port root header name/value getters.
- [x] Port root header value setters/overloads implemented by XPScript; child-entity mutation remains.
- [x] Port root header parameter access/mutation (`GetParamVal`/`SetParamVal`); child-header mutation remains pending native child-handle verification.
- [x] Port root header removal and `RemoveHeaders`; child-entity mutation remains.
- [x] Root header enumeration and duplicate-header semantics are implemented; child-entity headers remain open.
- [x] Verify header/entity handle lifetime and managed ownership; header wrappers re-read through the owning entity and entity rewrites invalidate the MIME directory before subsequent access.

### NotesDBDirectory

- [x] Add `NotesDBDirectory` using the current XPScript native implementation.
- [x] Add the corresponding `NotesSession` factory/access member.
- [x] Port directory server/name properties.
- [x] Port database enumeration/open behavior.
- [x] Port first/next database iteration semantics if present in the current public surface.
- [x] Port database type filtering and constants used by XPScript.
- [x] Verify remote vs local server path behavior through the shared native search implementation.
- [x] Verify returned `NotesDatabase` objects are unopened references and `OpenDatabase` returns the opened wrapper.
- [x] Verify native directory/search handles are released correctly by the enumerator path; live HCL runtime validation remains environment-dependent.

### NotesDatabaseAccess

- [x] Re-audit current C# `NotesDatabaseAccess` object against its native implementation.
- [x] Public string `LSArray` values map to concrete C# `string[]` values for `NamesList`, `Groups`, and `Roles`.
- [x] Verify `QueryAccess()` and `QueryAccess(userName)`.
- [x] Verify `NSFBuildNamesList`, `NSFDbReadACL`, and `ACLLookupAccess` ABI declarations.
- [x] Verify ACL handle/names-list/roles-list locking and freeing.
- [x] Verify `UserName`, `Level`, `LevelName`, `AccessFlags`, `NamesList`, `Groups`, and `Roles`.
- [x] Verify every exposed capability flag against the exact native ACL flag constants.

## P0 — NotesSession additions/changes

- [x] Re-audit all current `NotesSession` docs/source/samples against target; source/runtime paths are audited, with only live HCL runtime verification outstanding.
- [x] `HashPassword` — uses Domino `SECHashPassword`; buffer sizing and secret-memory clearing audited against XPScript.
- [x] `VerifyPassword` — uses Domino `SECVerifyPassword`; status and malformed/oversize input semantics audited against XPScript.
- [x] `AddressBooks` — `NAMEGetAddressBooks`/`OSPathNetParse` path and closed returned database wrappers audited against XPScript.
- [x] `SetEnvironmentVar` — C#-implementationens namnnormalisering, system scope, värdetyper och native setter-path är verifierade; live HCL-runtime-verifiering återstår.

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

- [x] Re-audit current XPScript `NotesDatabaseAccess` object against the implementation prepared in the parity branch.
- [x] Verify `QueryAccess()` and `QueryAccess(userName)` null/default-name behavior.
- [x] Verify `NSFBuildNamesList`, `NSFDbReadACL`, and `ACLLookupAccess` call paths, lock/unlock, and free operations against XPScript.
- [x] Verify ACL handle/names-list/roles-list locking and freeing; managed results are copied before native handles are released.
- [x] Verify `UserName`, `Level`, `LevelName`, `AccessFlags`, `NamesList`, `Groups`, and `Roles`.
- [x] Verify the exposed capability flags against the XPScript constants; no guessed numeric values were added.

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
- [x] `OpenByReplicaID` — locate/open flow and closed error wrapper match XPScript; invalid replica IDs are captured in `OpenError` through the same native-error path.
- [x] Factory/access member for `NotesDBDirectory` added directly to `NotesSession`, matching the XPScript surface.
- [x] MIME-related session behavior: `ConvertMIME=false` now opens notes with `OPEN_RAW_MIME_PART` through `NSFNoteOpenExt` / `NSFNoteOpenByUNIDExtended`, matching XPScript/JNX semantics.
- [~] Inventory any other NotesSession members added since the target baseline; no additional blocker found in the audited P0 set, but the broader runtime inventory remains open.

## P0 — NotesDocument additions/changes

- [ ] Re-audit current `NotesDocument` source/docs/samples member-by-member.
- [x] `Authors` — `$UpdatedBy` item values audited against the physical XPScript implementation.
- [x] `SentByAgent` — `$AssistMail` value `1` semantics audited against XPScript.
- [x] `NameOfProfile` — `$Name`/`$profile_` identity parsing audited against XPScript.
- [x] `Key` — profile-key parsing audited against XPScript.
- [x] `IsProfile` current semantics audited against the same `$profile_` identity parser as XPScript.
- [x] Native-backed `LastAccessed` uses `NSFNoteGetInfo` member 8 and matches XPScript.
- [x] Native/status-backed `IsDeleted` uses `NSFDbGetNoteInfoExt` and Domino deleted-note status semantics.
- [x] Native/status-backed `IsValid` is captured at document creation using `NSFDbGetNoteInfoExt`, matching XPScript.
- [~] MIME entity creation/get/close integration remains partial; document open-mode conversion is aligned but the full MIME entity/header surface is still incomplete.
- [~] MIME/rich-text/item conversion interactions: removed the extra post-open conversion; live HCL runtime coverage is still required.
- [x] `Remove` lifecycle rechecked: recycle/close precedes `NSFNoteDelete(database, NOTEID, flags)` and unsaved documents raise Notes runtime error 5.
- [x] Response-table reopen behavior rechecked: normal note opens include `OPEN_RESPONSE_ID_TABLE` (`0x1000`), matching the XPScript path.
- [ ] Inventory other document members changed in recent XPScript commits.

## P0 — NotesAgent additions/changes

- [~] Re-audit the latest XPScript `NotesAgent` implementation; physical public/native paths below audited, live HCL runtime validation remains.
- [x] `ReturnMessage` and stdout normalization — memory stdout capture and prefix normalization match XPScript.
- [x] Current `Run(noteId)` parameter-document semantics — non-zero hexadecimal NOTEID and `SetParamNoteID` native path aligned.
- [x] `RunWithDocumentContext(document, noteId)` — document context plus optional `SetParamNoteID` aligned with XPScript.
- [x] `ParameterDocID` — reflects the parameter NOTEID supplied for the most recent run, matching XPScript.
- [x] `IsEnabled` — native `AgentIsEnabled` plus pending persisted state matches XPScript.
- [x] `HasRunSinceModified` — `$AssistLastRun`/`$AssistVersion` plus `TimeDateCollate` matches XPScript.
- [x] `ServerName` getter/setter and persistence — `$MachineName`, pending state, sign/update on save match XPScript.
- [x] `IsPublic` — `$AssistFlags` private flag semantics match XPScript.
- [x] `Trigger` — `$AssistTrigger` integer conversion aligned; invalid numeric text now raises Notes error 13 instead of silently returning zero.
- [x] `OnBehalfOf` — `$OnBehalfOf` design-note text path matches XPScript.
- [x] `CommonOwner` — owner name parsing through `NotesName.Common` matches XPScript.
- [x] Agent reopen/run behavior after save/sign/update — unsigned open retries after native save/sign/update path.
- [x] Private-agent lookup fallback — `NIFFindPrivateDesignNote` fallback is present with optional-export handling.
- [x] Unsigned-agent sign/update/retry behavior — save/sign then `AgentOpen` retry matches current XPScript intent.
- [x] No remaining mutable constant/no-op agent properties found in the audited public `NotesAgent` surface; `IsNotesAgent=true` is the deliberate XPScript contract constant.

## P1 — NotesDatabase additions/changes

- [x] Re-audit all current `NotesDatabase` source/docs/samples; the C# surface and native paths are physically compared, with only live HCL runtime validation outstanding.
- [x] `Open(...)` assigned/unassigned database semantics match the current XPScript compatibility implementation.
- [x] `OpenError` behavior and reset rules match current XPScript: failed native open stores the Notes error text and successful open clears it.
- [x] `QueryAccess` integration is present and native-backed through names-list/ACL lookup.
- [x] `OpenByReplicaID` convenience aliases are present for current-server and explicit-server calls.
- [x] `Agents`, `Forms`, `Views` stale-design-note handling matches current XPScript: missing design notes are skipped and views that cannot be opened are omitted.
- [x] `CreateCopy` overload shape is source-compatible with the current XPScript 2/3/4-argument surface through C# optional parameters; includeDocuments remains the XPScript extension.
- [x] `GetModifiedDocuments` note-class validation and modified-document/design metadata are present.
- [~] Inventory database members added after the target baseline; current lifecycle, access, replica/copy, LotusScript aliases, metadata, FT-index, profile/document and design-collection surfaces audited. `IsFTIndexed`, `LastFTIndexed`, and database `Sign()` were found in current XPScript docs/samples/runtime and ported as native-backed surfaces. `Sign()` signs and updates the selected design notes, matching current XPScript. Broader surface sweep remains.

## P1 — NotesDateTime

- [x] Re-audit current `NotesDateTime` C# implementation; native GMT expansion is used for differences and Domino wildcard/time-zone operations are covered.
- [x] Port `SetNow`.
- [x] Port current `TimeDifference` behavior.
- [x] Port current `TimeDifferenceDouble` variant.
- [x] Port `ConvertToZone` using Domino `TimeLocalToGM` semantics.
- [x] Verify timezone/zone/DST handling uses Notes `TIMEDATE` expansion and `TimeLocalToGM` semantics rather than .NET approximations.
- [x] Port wildcard date/time behavior: empty construction, `SetAnyDate`, `SetAnyTime`, and `AnyDay`/`AllDay` formatting.

## P1 — NotesView / NotesViewColumn / NotesViewEntry / navigator

- [x] Re-audit current `NotesView` implementation and docs; parent lifetime checks now match XPScript.
- [x] View document navigation includes first/next/last/previous snapshot traversal; the current C# surface is verified.
- [x] Verify all recently added view properties and methods.
- [x] Re-audit `NotesViewColumn` physical `$VIEWFORMAT` parsing and formula decompile behavior.
- [x] Audit `NotesViewEntry` additions/changes.
- [x] Audit navigator positioning additions and updated entry traversal semantics.
- [x] Verify native collection buffer ownership and snapshot-vs-streaming differences.
- [x] Do not expose view properties that are only inferred/static placeholders; `IsConflict=false` is an explicit contract shared by the runtime.

## P1 — NotesItem

- [x] Re-audit current `NotesItem` implementation; the public surface and native paths match the current C# implementation and its native item paths.
- [x] Verify `Text` uses the native item-to-text conversion path.
- [x] Verify `Remove()` invalidates the managed wrapper after native item BLOCKID deletion.
- [x] Verify item type/flag mapping against the current native constants.
- [x] Inventory current item properties/functions; remaining runtime verification is native-environment dependent.

## P1 — Rich text object family

### NotesRichTextItem

- [x] Re-audit the latest XPScript rich-text implementation from physical code, not old TODOs.
- [x] `ConvertToHTML()`.
- [x] `ConvertToHTML(inlineImages)` including data-URI/image conversion behavior.
- [x] Verify styled append behavior and style-state lifetime.
- [x] Add typed `AppendParagraphStyle`; it fails closed with a clear C# exception, matching XPScript's unsupported structural rewrite behavior.
- [x] Port structural write operations that have real CD rewrite implementations; unsupported structural operations remain fail-closed.
- [x] Ensure rollback/atomicity for supported CD rewrites; canonical record identity is validated before native replacement.

### NotesRichTextNavigator

- [x] Port/verify `SetPosition` for linked rich-text and embedded elements.
- [x] Port/verify `SetPositionAtEnd` for the resolved element record.
- [x] Resolve table, paragraph, and embedded-hotspot end boundaries when positioning at end.
- [x] Verify section/table/doclink/hotspot boundary positioning; section/doclink/table-cell records are identity-backed and table/attachment hotspots use delimited spans.
- [x] Verify navigator invalidation after rich-text mutation; revision changes force record reload and clear the current position.

### NotesRichTextRange

- [x] Audit new range mutation functions; CD-record rewrite support is available for complete-record operations.
- [x] Port `Remove` for ranges aligned to complete rich-text records.
- [x] Port `SetStyle` for text-run CD records using the native rewrite path.
- [x] Port `SetStyle` using the physically implemented C# CD-record rewrite path.
- [x] Verify logical-span calculations and mutation rollback; complete-record rewrites validate canonical identity and apply through the native transform path.

### NotesRichTextSection

- [x] Verify real CD-rewrite `Remove()` implementation.
- [x] Verify `SetBarColor()`.
- [x] Verify `SetTitleStyle()`.
- [x] Verify nested section-span handling with depth tracking.

### NotesRichTextTable

- [x] Re-audit current table reader/parser against XPScript.
- [x] Confirm no table mutations are exposed without physical CD rewrite support.
- [x] Verify logical table spans/row/cell parsing.
- [x] Do not expose `RowLabels`, `Color`, or `AlternateColor`; current runtime code has no real implementations for these table members.

### NotesRichTextDocLink

- [x] Re-audit current doclink reader/mutation implementation against XPScript.
- [x] Verify replica/view/document IDs, display comment, server hint, and hotspot text parsing.
- [x] Keep unsupported hotspot style/mutation APIs unexposed until a real native rewrite is available.

### NotesEmbeddedObject

- [x] Audit recent attachment/embedded-object changes against XPScript.
- [x] Verify `ToByteArray()` and `ExtractFile()` native attachment paths.
- [x] Keep attachment `Remove` fail-closed because XPScript also requires an atomic hotspot/$FILE rewrite.
- [x] Rich-text revision invalidation is not required for the current attachment surface: `ToByteArray()` and `ExtractFile()` are read/extract operations and no mutating attachment method is exposed.

## P1 — NotesForm and design objects

- [x] Re-audit current `NotesForm` surface and implementation; `Parent`, `Name`, `NoteID`, and `NotesURL` match XPScript.
- [x] Inventory current form properties/functions; no additional form members are present in the physical XPScript runtime.
- [x] Audit newly introduced design-element objects; forms are wrappers around design-note IDs, while mutation remains exposed through DXL APIs.
- [x] Distinguish design mutation APIs: no compiler-only form mutation was added; real design mutation remains in `NotesDXLImporter`/`NotesDXLExporter`.

## P1 — NotesDocumentCollection / NotesNoteCollection

- [x] Re-audit recent collection changes.
- [x] Verify NoteID normalization changes; C# now applies the same `0x7fffffff` mask as XPScript.
- [x] Verify modified-document metadata and design/document classification.
- [x] Verify collection ownership/recycle behavior and parent lifetime checks.
- [x] Audit collection mutation/search members; no additional physical XPScript members require porting in this pass.

## P2 — Remaining Notes object inventory

Audit every current public Notes object found in XPScript `docs`, `samples`, and runtime source, including at minimum:

- [x] `NotesName` — added the missing XPScript component properties and aligned RFC821/RFC822 parsing, including address extraction and up to three comments.
- [x] `NotesStream` — aligned `Position` with XPScript's integer surface and added typed binary/text `Write` overloads; open/read/write/truncate/charset behavior matches the physical XPScript runtime.
- [x] `NotesDXLImporter` — audited against the physical XPScript runtime; C# retains the verified option validation and additionally offers a typed `NotesStream` import overload.
- [x] `NotesDXLExporter` — audited against the physical XPScript runtime; export methods, option properties, cleaned DXL, and design-folder export are present. C# additionally offers typed stream-returning overloads and `NotesNoteCollection` support.
- [x] `NotesRichTextStyle` — verified style fields, `StyleNoChange`, boolean-style values, and range validation against XPScript.
- [x] `NotesRichTextParagraphStyle` — verified alignment, spacing, margins, pagination, tab limits/types, and C# array/list mapping for `Tabs`.
- [x] `NotesColorObject` — verified `NotesColor` range 0–255 and linked-object lifetime behavior.
- [x] `NotesForm`
- [x] `NotesView`
- [x] `NotesViewColumn`
- [x] `NotesViewEntry`
- [x] `NotesViewNavigator` / equivalent navigator objects
- [x] `NotesDocumentCollection`
- [x] `NotesNoteCollection`
- [x] `NotesAgentResult` — added XPScript-compatible `Success` and `Status`; output normalization and recycle ownership were already present.
- [x] No additional public `Notes*` object was found in the current XPScript docs/runtime that is absent from this list.

For each object, add a sub-section to this TODO if differences are found.

## Public-surface cleanup

- [x] Search Dot-NET-to-Domino public Notes API for `NotSupportedException`, `NotesException(445, ...)`, constant `true`/`false`/`0`/empty-string properties, and no-op setters.
- [x] Compare every hit with current XPScript physical implementation.
- [x] Confirm deliberate constants: `NotesDateTime.IsValidDate`, `NotesAgent.IsNotesAgent`, and `NotesViewEntry.IsConflict` match XPScript contracts.
- [x] Confirm fail-closed rich-text/MIME members are not fabricated implementations; remaining unsupported members correspond to XPScript's own unsupported paths or verified native restrictions.
- [x] Replace placeholders where current C# has real native behavior; no additional replacement is justified. The remaining `NotesMail` MIME/attachment state is tracked separately as an implementation gap.

### NotesMail gap found during cleanup

- [x] Confirmed `NotesMail.IsMIME` and `AttachmentCount` are deliberate C# placeholders, not accidental constants: the C# implementation has no MIME body or attachment state yet.
- [ ] Port XPScript MIME body state (`SetBodyHTML`/MIME bytes) and attachment collection before changing these properties to dynamic values.
- [ ] Add fulltest coverage for MIME and attachment composition after that implementation exists.
- [x] Do not remove deliberate real constants that are part of the Notes contract without verifying the native/API meaning; the remaining constants are documented and tested.

### NotesSession environment audit

- [x] `GetEnvironmentString`, `GetEnvironmentValue`, and `SetEnvironmentVar` match XPScript name normalization, system scope, current-culture `DateTime` conversion, integer/string support, and invalid-type errors.

### NotesDBDirectory audit

- [x] Ported database type filtering and constants `REPLICA_CANDIDATE`, `TEMPLATE_CANDIDATE`, `DATABASE`, and `TEMPLATE` match XPScript values.
- [x] Verified local/remote server path handling is delegated to the same native database search path.
- [x] Verified enumerated database wrappers are unopened design references and `OpenDatabase` returns the opened wrapper, matching XPScript's lifecycle shape.
- [x] Verified enumeration state is cleared on recycle; live HCL runtime validation remains environment-dependent.

## Native ABI and lifecycle audit

### Completed reviews for this branch

- [x] `TimeConstant` uses Winapi calling convention, `WORD` constant selector, and an out `TIMEDATE` value; constant `2` is used for Domino wildcard time/date.
- [x] `TimeLocalToGM` uses a by-reference expanded-time structure and returns an integer status that is checked before copying the resulting `TIMEDATE`.
- [x] Root MIME stream calls use Winapi delegates with explicit `WORD` item-name lengths, `DWORD` stream flags/byte counts, and `MIMEStreamClose` in `finally` blocks.
- [x] Root MIME buffers are copied into managed arrays and freed with `Marshal.FreeHGlobal`; no native buffer ownership crosses the public C# API.

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

- [x] Build `net10.0` with zero errors.
- [x] Keep warnings at zero unless an explicitly documented unavoidable warning exists.
- [x] Pack the NuGet package successfully.
- [ ] Run existing CI on the exact final head SHA.
- [ ] Inspect CI logs rather than relying only on the PR status badge.
- [x] Review public API diff for accidental placeholders or compiler-only APIs; the fulltest reflection audit rejects leaked XPScript/`LSArray` types and the changed public members were reviewed.
- [ ] Review native ABI changes separately before merge.
- [x] Document runtime-dependent behavior that cannot be tested without an installed HCL Notes/Domino runtime.
- [ ] Merge only after the final head is green.

## Known runtime-test limitation

CI currently validates compile/pack behavior but does not provide a live HCL Notes/Domino runtime. Native MIME, ACL, agent, rich-text, DB directory, password and database behavior therefore still needs a Windows/HCL runtime integration test pass before treating native parity as fully proven.
