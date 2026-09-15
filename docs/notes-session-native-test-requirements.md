# NotesSession native test requirements

Compile/pack CI is necessary but not sufficient for the NotesSession P0 parity work. Native verification should run with a real supported HCL Notes/Domino runtime and a disposable test configuration.

Required runtime checks:

- environment read/write with user and system variable names
- integer and string environment values and rejection of unsupported types
- GetDatabase successful open, failed open with createOnFail true, and failed open with createOnFail false
- AddressBooks enumeration
- OpenByReplicaID for existing and missing replicas
- DBDirectory enumeration/factory behavior
- ConvertMIME true/false when opening documents containing MIME items

Native tests must not modify production notes.ini or production databases.
