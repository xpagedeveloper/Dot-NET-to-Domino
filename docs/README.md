# Dot-NET-to-Domino

Native .NET wrappers for the HCL Notes/Domino C API.

Dot-NET-to-Domino provides a managed object model inspired by the HCL Notes object model while calling the native Notes/Domino C API directly.

## Goals

- Native HCL Notes/Domino C API access from .NET.
- Familiar objects such as `NotesSession`, `NotesDatabase`, `NotesView`, and `NotesDocument`.
- Deterministic cleanup through `IDisposable` and Notes-compatible `Recycle()` methods.
- No Java, JNI, JNA, or JNX dependency.
- HCL native runtime binaries are not redistributed in the NuGet package.

## Installation

```bash
dotnet add package DotNetToDomino
```

## Usage

```csharp
using DotNetToDomino;

using var session = new NotesSession(
    @"C:\Program Files\HCL\Notes",
    @"C:\Program Files\HCL\Notes\notes.ini",
    Environment.GetEnvironmentVariable("NOTES_TEST_ID_PASSWORD"));

Console.WriteLine(session.UserName);
```

The HCL Notes/Domino runtime must be installed separately. The package does not include HCL native libraries or Notes ID files.

## Status

Early development. The package includes the public object model, lifecycle support, native runtime loader, and an expanding C API interop layer.

## License and notices

This project is licensed under the [Apache License, Version 2.0](https://www.apache.org/licenses/LICENSE-2.0).

### NOTICE

DotNetToDomino  
Copyright 2026 xpagedeveloper

This project is licensed under the Apache License, Version 2.0.


### Third-party reference notices

`DotNetToDomino` is an independent .NET wrapper around the HCL Notes/Domino native API. The projects and documentation below are implementation references only; their source code and binaries are not copied into or redistributed by this repository.

The commercial HCL Notes/Domino runtime, native libraries and product documentation are not included. HCL, Notes, Domino, .NET and Microsoft marks belong to their respective owners. References are for attribution, interoperability and compatibility identification only.

#### HCL Domino C API documentation

Copyright HCL Software.

The [HCL Domino C API documentation](https://opensource.hcltechsw.com/domino-c-api-docs/) is used as the primary reference for native signatures, constants, ABI details and MIME behavior. HCL's open-source catalog lists it under the [Apache License, Version 2.0](https://www.apache.org/licenses/LICENSE-2.0). This project does not redistribute the HCL C API toolkit or documentation archive.

#### HCL Domino JNX

Copyright 2019–2021 HCL. The [Domino JNX project](https://github.com/HCL-TECH-SOFTWARE/domino-jnx) is used as a secondary reference for Notes behavior and native API mapping. It is licensed under the [Apache License, Version 2.0](https://www.apache.org/licenses/LICENSE-2.0).

#### Domino JNA

Copyright Klehmann and contributors. The [Domino JNA project](https://github.com/klehmann/domino-jna) is used as a reference for MIME serialization and Notes item writeback. It is licensed under the [Apache License, Version 2.0](https://www.apache.org/licenses/LICENSE-2.0).

#### .NET platform and HCL runtime

The library is built on the [.NET platform](https://github.com/dotnet/runtime), generally distributed under the [MIT License](https://github.com/dotnet/runtime/blob/main/LICENSE.TXT). This package does not redistribute the .NET runtime; self-contained applications must retain notices from their runtime distribution.


