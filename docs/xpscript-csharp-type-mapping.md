# XPScript to C# type mapping

XPScript is a behavioral reference only. Its runtime types must never appear in the public .NET API.

| XPScript type | Required C# representation |
|---|---|
| `LSArray` | A concrete CLR array, for example `string[]`, `object?[]`, or `NotesDocument[]` |
| `NotesDateTime` | `NotesDateTime` |
| `NotesName` | `NotesName` |
| Notes object collection | A .NET array or a .NET collection interface containing the corresponding C# Notes type |
| XPScript binary array | `byte[]` |

An XPScript string `LSArray` is exposed as a concrete `string[]`, such as `NotesDatabaseAccess.NamesList`, `Groups`, and `Roles`.

For example, `NotesEmbeddedObject.ToByteArray()` returns exactly `byte[]`. The native attachment bytes are copied into a managed CLR byte array before crossing the public API boundary.

The full-test CLI rejects public `XPScript` and `LSArray` types. Any future API port that corresponds to `LSArray` must use a CLR `T[]` return/property type.
