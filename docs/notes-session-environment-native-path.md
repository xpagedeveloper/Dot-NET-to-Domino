# NotesSession environment native path

The managed session resolves the XPScript-compatible variable name and delegates reading/writing to `NotesNativeApi`. The parity correction in this branch is deliberately made before the native call, matching XPScript's type/name validation boundary.
