# Integer environment values

Integral environment values are formatted with invariant culture before the native write. `GetEnvironmentValue` parses invariant signed 32-bit integers and returns null for empty, non-numeric, or out-of-range text, matching the current XPScript implementation.
