# XPScript to C# type mapping

XPScript is a behavioral reference only. Its runtime types must never appear in the public .NET API.

| XPScript type | Required C# representation |
|---|---|
| `LSArray` | A concrete CLR array, for example `string[]`, `object?[]`, or `NotesDocument[]` |
| `NotesDateTime` | `NotesDateTime` |
| `NotesName` | `NotesName` |
| Notes object collection | A .NET array or a .NET collection interface containing the corresponding C# Notes type |
| XPScript binary array | `byte[]` |

The full-test CLI rejects public `XPScript` and `LSArray` types. Any future API port that corresponds to `LSArray` must use a CLR `T[]` return/property type.
