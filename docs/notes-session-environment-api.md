# NotesSession environment API parity

`GetEnvironmentString`, `GetEnvironmentValue`, and `SetEnvironmentVar` follow XPScript's `$`-prefix rule: unless `system` is true or the supplied name already begins with `$`, the wrapper prefixes the name with `$` before invoking the native Notes environment API.
