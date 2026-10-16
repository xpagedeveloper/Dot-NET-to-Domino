# Lokal databas: Create och DXL-import

Verifierat 2026-10-14 med Notes 12.0.2 x64. Från DotNetToDomino 0.1.0-alpha.8 kan `local` användas i både biblioteket och CLI-programmet; `--local` stöds också i CLI-programmet.

## Två olika felorsaker

`0x0110 Access to data denied` uppstod när testprocessen kördes i filsystemssandboxen. Samma .NET-skrivtest fungerade i vanlig PowerShell och utanför sandboxen för både Windows Temp och Notes Data. Ändrad NTFS-ACL löser inte sandboxens begränsningar. Kör native-testet med åtkomst till Notes Data.

`0x0803 Unable to find path to server` vid den manuella DXL-körningen kan orsakas av Windows PowerShells argumenthantering. Ett tomt native-argument `""` tappas i dess äldre argumenthantering. Då flyttas nästa argument, sökvägen till `notes.ini`, till serverparametern. Detta reproducerades med Windows PowerShell 5.1. Använd `--local` som serverargument; importerprogrammet konverterar det till tom server före API-anropet och avvisar en ini-sökväg i serverparametern.

Microsoft beskriver ändringen för tomma argument i [about_Parsing](https://learn.microsoft.com/en-us/powershell/module/microsoft.powershell.core/about/about_parsing?view=powershell-7.5).

## Kör från PowerShell

Bygg först från repositoryt:

```powershell
$repo = 'D:\VisualStudio\VisualStudio git\Dot-NET-to-Domino'
dotnet build "$repo\test\DotNetToDomino.DxlImporter\DotNetToDomino.DxlImporter.csproj" -c Release
```

Starta sedan från Notes installationskatalog. Sätt `NOTES_TEST_ID_PASSWORD` separat till ID-lösenordet före körningen.

```powershell
Set-Location 'D:\Program Files\HCL\Notes'
try {
    dotnet "$repo\test\DotNetToDomino.DxlImporter\bin\Release\net10.0\DotNetToDomino.DxlImporter.dll" `
        'D:\Program Files\HCL\Notes' `
        "$repo\test\DotNetToDomino.DxlImporter\fixtures\DotNetToDominoTest.dxl" `
        'test1946.nsf' `
        'local' `
        'D:\Program Files\HCL\Notes\notes.ini'
}
finally {
    Remove-Item Env:NOTES_TEST_ID_PASSWORD -ErrorAction SilentlyContinue
}
```

Programmet visar den laddade wrapper-versionen, DLL-sökvägen, serverparametern och databasnamnet. DXL-importprojektet använder `ProjectReference` och aktuell källkod. PackageConsumer använder den lokala NuGet-källan och visar paketets version.

## Verifierat beteende

ACL-regressionstestet skapade en ny databas med `NSFDbCreateExtended`, ändrade och sparade ACL, återöppnade databasen och tog bort den. DXL-testet öppnade först `test.nsf`, skapade `test1946.nsf`, importerade fyra noter och verifierade agentens signerare och certifierare. `test1946.nsf` sparades i Notes Data.

Native-parametrarna följer [NSFDbCreateExtended](https://opensource.hcltechsw.com/domino-c-api-docs/reference/Func/NSFDbCreateExtended/): LMBCS-sökväg med avslutande nollbyte, `DBCLASS_NOTEFILE=0xFF01`, `ForceCreation=FALSE`, options 0, encryption 0 och `DWORD MaxFileSize=0`. Testsvar 0 verifierar skapandet; tidigare nekad åtkomst var inte bevis för fel i `NSFDbCreate`-signaturen.

`nsd -kill` behövs inte för en normal testkörning. En hanterad .NET-exception bevisar inte att Notes-klienten har kraschat.
