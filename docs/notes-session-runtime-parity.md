# NotesSession runtime parity audit

Reference: current HCL C API and JNI projects physical runtime implementation on `main`, including `NotesSessionEnvironmentPostProcessor` and `NotesDatabaseCreateCompatibilityPostProcessor`.

## Verified surface

- `HashPassword` / `VerifyPassword`: native SEC password APIs and length/error handling were aligned previously.
- `GetEnvironmentString`: resolves non-system names with a `$` prefix and reads through the native environment API.
- `GetEnvironmentValue`: returns an integer only when the native environment text is a valid invariant integer; empty/non-integer values return null.
- `SetEnvironmentVar`: accepts strings, integral numeric types and DateTime only. Other values raise runtime error 13. Empty names raise runtime error 5. Non-system names receive the `$` prefix.
- `GetDatabase`: returns an open database when available; with `createOnFail=true` it returns a closed wrapper when open fails; with `false` it recycles the failed wrapper and returns null.

## Changes in this audit

The target previously used unrestricted `Convert.ToString` for `SetEnvironmentVar`, which accepted booleans, floating-point values and arbitrary objects. HCL C API and JNI projects explicitly rejects those values. The target now uses the same supported type set and Notes-style runtime errors.

Environment-name validation was also aligned to Notes runtime error 5 instead of a .NET `ArgumentException`.

## Still to verify

The following NotesSession items remain separate physical/native audits before the P0 NotesSession section is complete:

- `AddressBooks` native list parsing and database wrapper semantics.
- `OpenByReplicaID` locate/open/error-state behavior.
- `NotesDBDirectory` factory/access surface.
- `ConvertMIME` propagation into document open/item behavior.

A live Notes/Domino runtime test is required in addition to compile/pack CI for native environment and database behavior.
