# NotesDatabaseAccess parity audit

Reference: current XPScript `NotesDatabaseAccessPostProcessor` on `main`.

## Public/runtime parity

Dot-NET-to-Domino currently matches the implemented XPScript surface for:

- `NotesDatabase.QueryAccess()` and `QueryAccess(userName)`
- `Parent`, `UserName`, `Level`, `LevelName`, `AccessFlags`
- `NamesList`, `Groups`, `Roles`
- `CannotCreateDocuments`, `IsServerEntry`, `CannotDeleteDocuments`
- `CanCreatePersonalAgents`, `CanCreatePersonalFolders`
- `IsPersonEntry`, `IsGroupEntry`, `CanCreateSharedFolders`
- `CanCreateLotusScriptAgents`, `IsPublicReader`, `IsPublicWriter`, `CannotReplicateOrCopy`

The target uses .NET `IReadOnlyList<string>` for the three list properties instead of XPScript `LSArray`; this is an intentional representation difference and does not change the Notes runtime data.

## Native implementation

The target follows the same physical runtime path as XPScript:

1. `NSFBuildNamesList`
2. lock the returned `NAMES_LIST`
3. `NSFDbReadACL`
4. `ACLLookupAccess`
5. read role names with `ListGetText`
6. unlock/free role list, ACL and names-list memory

The current target uses native-sized `nint` handles for the C API memory handles, which is preferable for the .NET wrapper ABI. Calling convention is `Winapi`; access level and access flags are `ushort`.

## Lifecycle and memory

`NotesDatabaseAccess` stores a managed snapshot of the effective access result and owns no native handle after construction. Native names-list, ACL and role-list handles are released inside `QueryDatabaseAccess` before the managed result is returned. Recycling the managed access object therefore does not need to free native memory.

## Remaining runtime verification

CI can verify compile/pack only. A live HCL Notes/Domino runtime test is still required to prove remote/local ACL lookup behavior and effective group/role expansion against a real ACL.

## Result

No code change is required for current XPScript parity. The existing implementation is retained. If XPScript changes its ACL flag semantics or native ABI, this audit must be repeated before package release.
