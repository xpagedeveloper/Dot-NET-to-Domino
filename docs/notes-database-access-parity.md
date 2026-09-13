# NotesDatabaseAccess parity audit

Reference: current XPScript `NotesDatabaseAccessPostProcessor` on `main`.

## Public/runtime parity

Dot-NET-to-Domino matches the implemented XPScript surface for `NotesDatabase.QueryAccess()` / `QueryAccess(userName)`, the effective user/access properties, expanded names/groups/roles, and the exposed ACL capability flags. The target intentionally represents XPScript `LSArray` string lists as .NET `IReadOnlyList<string>`.

## Native implementation

The physical path is `NSFBuildNamesList` -> lock `NAMES_LIST` -> `NSFDbReadACL` -> `ACLLookupAccess` -> `ListGetText` for roles, followed by deterministic unlock/free of the role list, ACL and names-list handles.

The parity correction adds checks that the names-list and ACL handles are non-zero, that the names list contains an effective user, that the packed 14-byte `NAMES_LIST` header and entries stay inside the returned allocation, that entries are NUL terminated, and that a non-empty role-list handle can actually be locked. These checks match the physical XPScript implementation instead of the previous audit's API-only conclusion.

The .NET wrapper retains native-sized `nint` handles. Calling convention is `Winapi`; access level and access flags are `ushort`.

## ACL constants

The capability masks were verified against HCL Domino C API/JNX constants:

- `ACL_FLAG_AUTHOR_NOCREATE` = `0x0001`
- `ACL_FLAG_SERVER` = `0x0002`
- `ACL_FLAG_NODELETE` = `0x0004`
- `ACL_FLAG_CREATE_PRAGENT` = `0x0008`
- `ACL_FLAG_CREATE_PRFOLDER` = `0x0010`
- `ACL_FLAG_PERSON` = `0x0020`
- `ACL_FLAG_GROUP` = `0x0040`
- `ACL_FLAG_CREATE_FOLDER` = `0x0080`
- `ACL_FLAG_CREATE_LOTUSSCRIPT` = `0x0100`
- `ACL_FLAG_PUBLICREADER` = `0x0200`
- `ACL_FLAG_PUBLICWRITER` = `0x0400`
- `ACL_FLAG_NOREPLICATE` = `0x1000`

The public property names are wrapper-friendly names, but their masks map to these Domino constants.

## Lifecycle and verification

`NotesDatabaseAccess` owns no native handle after construction; it is a managed snapshot. CI on the first corrected head completed Restore, Build and Pack with zero build warnings/errors. The workflow failed only when `actions/upload-artifact` attempted to create the package artifact because the repository's GitHub Actions artifact storage quota was full.

A live HCL Notes/Domino runtime test is still required to prove remote/local ACL lookup and effective group/role expansion against a real ACL.

## Result

The previous statement that no code change was required was incorrect. The physical parity issues above are corrected in this branch. Final workflow success still has to be checked on the exact final head.