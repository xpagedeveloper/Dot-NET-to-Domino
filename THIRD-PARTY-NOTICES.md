# Third-party reference notices

`DotNetToDomino` is an independent .NET wrapper around the HCL Notes/Domino native API. The projects and documentation below are implementation references only; their source code and binaries are not copied into or redistributed by this repository.

## HCL Domino C API documentation

Copyright HCL Software.

The [HCL Domino C API documentation](https://opensource.hcltechsw.com/domino-c-api-docs/) is used as the primary reference for native signatures, constants, ABI details and MIME behavior. HCL's open-source catalog lists it under the [Apache License, Version 2.0](https://www.apache.org/licenses/LICENSE-2.0). This project does not redistribute the HCL C API toolkit or documentation archive.

## HCL Domino JNX

Copyright 2019–2021 HCL. The [Domino JNX project](https://github.com/HCL-TECH-SOFTWARE/domino-jnx) is used as a secondary reference for Notes behavior and native API mapping. It is licensed under the [Apache License, Version 2.0](https://www.apache.org/licenses/LICENSE-2.0). No JNX source or binary is distributed here.

## Domino JNA

Copyright Klehmann and contributors. The [Domino JNA project](https://github.com/klehmann/domino-jna) is used as a reference for MIME serialization and Notes item writeback. It is licensed under the [Apache License, Version 2.0](https://www.apache.org/licenses/LICENSE-2.0). No Domino JNA source or binary is distributed here.

## NuGet dependencies

The runtime and integration test projects currently declare no external `PackageReference` dependencies. They use the .NET 10 base class libraries and direct native bindings to the separately installed HCL runtime. If a package is added later, this section must record its exact version, source URL, copyright holder, license and required NOTICE/redistribution material, including reviewed transitive dependencies.

## .NET platform and HCL runtime

The library is built on the [.NET platform](https://github.com/dotnet/runtime), generally distributed under the [MIT License](https://github.com/dotnet/runtime/blob/main/LICENSE.TXT). This package does not redistribute the .NET runtime; self-contained applications must retain notices from their runtime distribution.

The commercial HCL Notes/Domino runtime, native libraries and product documentation are not included. HCL, Notes and Domino marks belong to their respective owners. References are for attribution, interoperability and compatibility identification only.
