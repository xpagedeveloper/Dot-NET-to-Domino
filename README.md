# DotNetToDomino

Native .NET wrappers for the HCL Notes/Domino C API.

DotNetToDomino provides a managed object model inspired by the HCL Notes object model while calling the native Notes/Domino C API directly.

## Goals

- Native HCL Notes/Domino C API access from .NET.
- Familiar objects such as `NotesSession`, `NotesDatabase`, `NotesView`, and `NotesDocument`.
- Deterministic cleanup through `IDisposable` and Notes-compatible `Recycle()` methods.
- No Java, JNI, JNA, or JNX dependency.
- HCL native runtime binaries are not redistributed in the NuGet package.

## Status

Early development. Native runtime initialization, thread scopes, LMBCS conversion, user identity lookup, canonical name handling, and native database open/close are being implemented first.

## Basic usage

```csharp
using DotNetToDomino;

using var session = new NotesSession(@"C:\Program Files\HCL\Notes");
using var db = session.OpenDatabase("", "names.nsf");

Console.WriteLine(db.IsOpen);
```

The HCL Notes or Domino native runtime must already be installed and licensed on the machine running the application.

## Initial object model

- `NotesSession`
- `NotesDatabase`
- `NotesView`
- `NotesDocumentCollection`
- `NotesNoteCollection`
- `NotesDocument`
- `NotesItem`
- `NotesRichTextItem`
- `NotesName`
- `NotesDateTime`
- `NotesAgent`
- `NotesAgentResult`
- `NotesStream`
- `NotesDXLImporter`
- `NotesDXLExporter`
