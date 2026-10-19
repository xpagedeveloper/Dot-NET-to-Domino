# Local database: creation and DXL import

Verified on 2026-10-14 with Notes 12.0.2 x64. DotNetToDomino supports `local` as a server name in the library and CLI; the CLI also accepts `--local`.

## Two distinct failure causes

`0x0110 Access to data denied` can occur when tests run inside a filesystem sandbox. Run native tests with access to Notes Data.

`0x0803 Unable to find path to server` during manual DXL execution can result from Windows PowerShell dropping an empty native argument. Use `--local` as the server argument; the importer converts it to an empty server before the API call.

See Microsoft's [about_Parsing](https://learn.microsoft.com/en-us/powershell/module/microsoft.powershell.core/about/about_parsing?view=powershell-7.5) documentation.

## Run from PowerShell

```powershell
$repo = 'D:\VisualStudio\VisualStudio git\Dot-NET-to-Domino'
dotnet build "$repo\test\DotNetToDomino.DxlImporter\DotNetToDomino.DxlImporter.csproj" -c Release
```

Set `NOTES_TEST_ID_PASSWORD` separately before running the importer from the Notes installation directory.

## Verified behavior

The ACL regression test creates a database with `NSFDbCreateExtended`, changes and saves its ACL, reopens it, and removes it. The DXL test imports notes into a new database and verifies the agent signer and certifier.

Native parameters follow [NSFDbCreateExtended](https://opensource.hcltechsw.com/domino-c-api-docs/reference/Func/NSFDbCreateExtended/). A managed .NET exception does not prove that the Notes client crashed.
