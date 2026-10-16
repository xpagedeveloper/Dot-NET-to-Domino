# NotesInternational

`NotesSession.International` exposes the regional settings for the native Notes
session. The object is created together with `NotesSession`, is read-only, and is
recycled automatically when the session is recycled.

```csharp
using var session = new NotesSession(notesDirectory, notesIni, password);
var intl = session.International;

Console.WriteLine($"Locale: {intl.Locale}");
Console.WriteLine($"Date: {intl.Today} ({intl.DateFormat})");
Console.WriteLine($"Currency: {intl.CurrencySymbol}");
Console.WriteLine($"Monday is: {intl.GetWeekdayName(2)}");
```

The standalone C API has no handle equivalent to the LotusScript
`NotesInternational` object. The wrapper therefore reports the regional settings
selected by the process/user culture, while keeping Notes-compatible names and
one-based weekday/month helpers. It does not change the process culture or any
Notes settings.

All properties are read-only. Access after `session.Recycle()` raises
`ObjectDisposedException`, like other session-owned Notes objects.
