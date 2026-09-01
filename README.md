# NotesToDotNet

Native .NET wrappers for the HCL Notes/Domino C API.

NotesToDotNet provides a managed object model inspired by the HCL Notes object model while calling the native Notes/Domino C API directly.

## Goals

- Native HCL Notes/Domino C API access from .NET.
- Familiar objects such as `NotesSession`, `NotesDatabase`, `NotesView`, and `NotesDocument`.
- Deterministic cleanup through `IDisposable` and Notes-compatible `Recycle()` methods.
- No Java, JNI, JNA, or JNX dependency.
- HCL native runtime binaries are not redistributed in the NuGet package.

## Status

Early development. The package skeleton, public object model, lifecycle, and native runtime loader are established first. Native C API bindings will then be implemented per object.

## Basic usage

```csharp
using NotesToDotNet;

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
