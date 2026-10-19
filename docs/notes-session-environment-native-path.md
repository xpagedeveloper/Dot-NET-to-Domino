# NotesSession environment native path

The managed session resolves the HCL C API and JNI projects-compatible variable name and delegates reading/writing to `NotesNativeApi`. The parity correction in this branch is deliberately made before the native call, matching HCL C API and JNI projects's type/name validation boundary.
