# Dot-NET-to-Domino

Native .NET wrappers for the HCL Notes/Domino C API.

Dot-NET-to-Domino provides a managed object model inspired by the HCL Notes object model while calling the native Notes/Domino C API directly.

## Goals

- Native HCL Notes/Domino C API access from .NET.
- Familiar objects such as `NotesSession`, `NotesDatabase`, `NotesView`, and `NotesDocument`.
- Deterministic cleanup through `IDisposable` and Notes-compatible `Recycle()` methods.
- No Java, JNI, JNA, or JNX dependency.
- HCL native runtime binaries are not redistributed in the NuGet package.

## NuGet package

Package ID: `DotNetToDomino`

```bash
dotnet add package DotNetToDomino
```

## Namespace

```csharp
using DotNetToDomino;
```

## Status

Early development. The package skeleton, public object model, lifecycle, native runtime loader, and initial C API interop layer are in place. Native bindings are being implemented per Notes object.

## Basic usage

```csharp
using DotNetToDomino;

using var session = new NotesSession(@"C:\Program Files\HCL\Notes");
using var db = session.OpenDatabase("", "names.nsf");

Console.WriteLine(db.IsOpen);
```

The HCL Notes or Domino native runtime must already be installed and licensed on the machine running the application.

## Repository layout

```text
DotNetToDomino.sln
src/
  DotNetToDomino/
    DotNetToDomino.csproj
    Internal/
    Interop/
```

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

## Reference implementation

The XPscript repository is used as a read-only knowledge source for Notes/Domino C API behavior, object semantics, handle ownership, and lifecycle rules. Dot-NET-to-Domino is a standalone .NET library and does not depend on XPscript at runtime.
