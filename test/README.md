# C# integration test

`test/DotNetToDomino.Integration` is a deliberately small console application that compiles against the public wrapper API and exercises a broad native runtime surface.

Required:

- `NOTES_RUNTIME`: HCL Notes/Domino program directory.

Optional:

- `NOTES_TEST_DATABASE`: NSF path used for database/document tests.
- `NOTES_TEST_SERVER`: server name; empty means local.
- `NOTES_TEST_REPLICA_ID`: replica ID for OpenByReplicaID.
- `NOTES_TEST_PASSWORD`: harmless password used only for HashPassword/VerifyPassword.

Run with:

```shell
dotnet run --project test/DotNetToDomino.Integration/DotNetToDomino.Integration.csproj
```

The test does not save the document it creates and does not delete databases or documents. Environment-variable round-trip writes `$DotNetToDominoIntegrationTest` to the active Notes environment, so use a dedicated test Notes/Domino installation when running native integration tests.

CI builds the project to catch public API/compiler regressions. Native execution requires an installed HCL Notes/Domino runtime and is intentionally not run on the generic GitHub-hosted runner.
