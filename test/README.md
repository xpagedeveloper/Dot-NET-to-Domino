# C# integration test

`test/DotNetToDomino.Integration` is a deliberately small console application that compiles against the public wrapper API and exercises a broad native runtime surface.

Required:

- `NOTES_RUNTIME`: HCL Notes/Domino program directory.

Optional:

- `NOTES_TEST_DATABASE`: NSF path used for database/document tests.
- `NOTES_TEST_SERVER`: server name; empty means local.
- `NOTES_TEST_REPLICA_ID`: replica ID for OpenByReplicaID.
- `NOTES_TEST_PASSWORD`: harmless password used only for HashPassword/VerifyPassword.
- `NOTES_TEST_MAIL_TO`: recipient used only by the opt-in live NotesMail matrix.
- `NOTES_TEST_MAIL_LIVE=1`: explicitly enables nine live delivery tests (three priorities × three reports); it sends real mail and must never be enabled against production recipients accidentally.
- `NOTES_TEST_AGENT`: optional agent name; when set, FullTestCli runs it and requires `NotesAgentResult.Output == "STATUS OK"`.

Run with:

```shell
dotnet run --project test/DotNetToDomino.Integration/DotNetToDomino.Integration.csproj
```

The test does not save the document it creates and does not delete databases or documents. Environment-variable round-trip writes `$DotNetToDominoIntegrationTest` to the active Notes environment, so use a dedicated test Notes/Domino installation when running native integration tests.

CI builds the project to catch public API/compiler regressions. Native execution requires an installed HCL Notes/Domino runtime and is intentionally not run on the generic GitHub-hosted runner.

## Full NuGet test CLI

`DotNetToDomino.FullTestCli` references the library project directly so the solution restores without requiring a pre-existing local package, audits the exported API by reflection, and runs native smoke tests. `DotNetToDomino.PackageConsumer` and `DotNetToDomino.DxlImporter` consume the newest locally packed `0.1.0-alpha.*` NuGet version automatically during restore. Build the library first so `src/DotNetToDomino/publish` contains the current package. Failures are printed with the fully qualified C# exception type and return exit code `1`.

The CLI also rejects any public API type, property, return value, or parameter that leaks an XPScript/`LSArray` type. XPScript is used only as a behavioral reference; the exposed surface must use C#/.NET types.

The required mapping is documented in [`docs/xpscript-csharp-type-mapping.md`](../docs/xpscript-csharp-type-mapping.md); in particular, `LSArray` maps to a concrete C# `T[]`.

Build the package and run it with `NOTES_RUNTIME` set:

```shell
dotnet build DotNetToDomino.sln -c Release
dotnet run --project test/DotNetToDomino.FullTestCli/DotNetToDomino.FullTestCli.csproj --no-restore
```

Optionally set `NOTES_TEST_DATABASE` and `NOTES_TEST_SERVER` for database/document tests. The CLI fails when `NOTES_RUNTIME` is missing so an accidental non-native run cannot be reported as a successful full test.

The live NotesMail matrix is skipped unless both `NOTES_TEST_MAIL_LIVE=1` and `NOTES_TEST_MAIL_TO` are set. It verifies `H/N/L` delivery priority, `N/B/C` delivery report, and requests a return receipt for the confirmation case.

## Creating a native test database from DXL

The fixture [`test/DotNetToDomino.DxlImporter/fixtures/DotNetToDominoTest.dxl`](DotNetToDomino.DxlImporter/fixtures/DotNetToDominoTest.dxl) contains a form, document, and `DotNetToDominoTestAgent` that prints `STATUS OK`. Build and run the importer on a Notes/Domino client:

```shell
dotnet run --project test/DotNetToDomino.DxlImporter/DotNetToDomino.DxlImporter.csproj -- \
  "C:\Program Files\HCL\Notes" \
  test/DotNetToDomino.DxlImporter/fixtures/DotNetToDominoTest.dxl \
  "C:\Notes\Data\DotNetToDominoTest.nsf"
```

The importer creates the target NSF if it does not exist, then imports the DXL. Use `NOTES_TEST_AGENT=DotNetToDominoTestAgent` when running FullTestCli afterward.
