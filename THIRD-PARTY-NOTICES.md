# Third-party notices

## Current release

The `DotNetToDomino` NuGet package currently has no third-party NuGet package dependencies. Its managed implementation uses only the .NET 10 base class libraries and direct P/Invoke-style bindings to the HCL Notes/Domino native runtime.

The HCL Notes/Domino runtime, native libraries, SDK headers, and HCL documentation are not redistributed by this repository or its NuGet package. Users must obtain and license the applicable HCL Notes/Domino product separately.

The project source and managed library are licensed under Apache License 2.0; see [`LICENSE`](LICENSE).

## Dependency policy

Before adding an external NuGet package or copied/generated third-party source, record all of the following in this file in the same change:

1. Package or project name and exact version.
2. Package URL and source repository.
3. License identifier and a link to the license text.
4. Copyright and notice text when required by that license.
5. Whether the dependency is shipped in the runtime NuGet package or used only by tests/build tooling.

Dependencies used only by tests or build tooling must be kept out of the runtime package whenever possible. Do not copy HCL native binaries or proprietary HCL material into this repository or package.

## Review rule

Every new `PackageReference`, vendored source file, generated artifact based on external code, or runtime native dependency must receive a license/notice review before merge. If a package has transitive dependencies, inspect and record the transitive license obligations as well.
