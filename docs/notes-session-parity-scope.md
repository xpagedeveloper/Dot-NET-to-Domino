# Remaining NotesSession P0 parity scope

Completed/physically compared in the current parity work:

- HashPassword
- VerifyPassword
- GetEnvironmentString
- GetEnvironmentValue
- SetEnvironmentVar
- GetDatabase

Still requiring physical comparison against current XPScript implementation and native behavior:

1. AddressBooks
2. OpenByReplicaID
3. NotesDBDirectory factory/access member
4. ConvertMIME behavior and propagation

Do not mark the NotesSession P0 TODO complete until these four areas have been checked at implementation/native-call level and final-head CI has been inspected.
