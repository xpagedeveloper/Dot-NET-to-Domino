# Dot-NET-to-Domino

Native .NET wrappers for the HCL Notes/Domino C API.

Dot-NET-to-Domino provides a managed object model inspired by the HCL Notes object model while calling the native Notes/Domino C API directly, with a familiar syntax.

## Goals

- Native HCL Notes/Domino C API access from .NET.
- Familiar objects such as `NotesSession`, `NotesDatabase`, `NotesView`, and `NotesDocument`.
- Deterministic cleanup through `IDisposable` and Notes-compatible `Recycle()` methods.
- No Java, JNI, JNA, or JNX dependency.

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

## Null safety

The NuGet assembly enables C# nullable reference annotations and treats compiler warnings as errors when building this repository. APIs that may not find a Notes object return nullable types, for example `NotesDocument?`:

```csharp
var document = db.GetDocumentByUNID(unid)
    ?? throw new InvalidOperationException("The document could not be opened.");

Console.WriteLine(document.ParentDatabase.ReplicaID);
```

Required arguments are validated before native calls and produce clear `ArgumentNullException`, `ArgumentException`, or `ArgumentOutOfRangeException` errors. A member access on a variable that is already null happens in the caller before the library can run; use nullable warnings and an explicit null check as shown above.

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

## License notes

- [License] (License)
- [Project NOTICE](NOTICE)
- [Third-party reference notices and licenses](THIRD-PARTY-NOTICES.md)

## Reference implementation

The HCL C API and JNI projects repository is used as a read-only knowledge source for Notes/Domino C API behavior, object semantics, handle ownership, and lifecycle rules. Dot-NET-to-Domino is a standalone .NET library and does not depend on HCL C API and JNI projects at runtime.
