# NotesAgent runtime parity

This update aligns Dot-NET-to-Domino with the current XPScript NotesAgent runtime implementation rather than only its public surface.

Included behavior:

- `NotesDatabase.OpenByReplicaID(replicaId)` and `OpenByReplicaID(server, replicaId)` convenience aliases.
- `NotesAgent.RunWithDocumentContext(NotesDocument)` passes the open document handle through `AgentSetDocumentContext`.
- Agent lookup falls back to `NIFFindPrivateDesignNote` after `NIFFindDesignNote`.
- Agent execution is resolved by design note ID and preserves unsigned-agent sign/update/retry behavior.
- `NotesAgent.Remove()` deletes the agent design note.
- `NSFNoteDelete` uses the correct database-handle + NOTEID ABI.
- `NotesDocument.Remove()` closes/recycles the open note before deleting it by database handle and NOTEID.

Properties that are still constant/no-op in XPScript remain constant/no-op here (`IsEnabled`, `Trigger`, `Target`, `ParameterDocID`, `HasRunSinceModified`, `ProhibitDesignUpdate`, `HttpURL`, `OnBehalfOf`, etc.) rather than being presented as native-backed functionality.
