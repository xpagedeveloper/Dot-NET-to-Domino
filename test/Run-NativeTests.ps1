param(
    [string]$Runtime = 'D:\Program Files\HCL\Notes',
    [string]$Database = 'test.nsf',
    [string]$Configuration = 'Release',
    [string]$AgentName = 'DotNetToDominoTestAgent',
    [string[]]$Filters = @('ACL native regression', 'agent output', 'property round trip', 'object property', 'session properties', 'session factories', 'password round trip', 'address book and directory', 'rich text range surface', 'item surface', 'view navigation', 'argument validation', 'null argument traps', 'agent null argument trap', 'DXL argument traps', 'exception trapping', 'database and document surface', 'nested MIME child guards'),
    [int]$TimeoutSeconds = 45,
    [switch]$ShowOutput
)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$testBinary = Join-Path $PSScriptRoot "DotNetToDomino.FullTestCli\bin\$Configuration\net10.0\DotNetToDomino.FullTestCli.dll"
$logDirectory = Join-Path $projectRoot 'artifacts\native-tests'
New-Item -ItemType Directory -Force -Path $logDirectory | Out-Null
$env:NOTES_RUNTIME = $Runtime
$env:NOTES_TEST_DATABASE = $Database
$env:NOTES_TEST_SERVER = ''
$env:NOTES_TEST_AGENT = $AgentName
$env:NOTES_TEST_MAIL_LIVE = '0'
$failed = 0
foreach ($filter in $Filters) {
    $env:NOTES_TEST_FILTER = $filter
    $logName = $filter -replace '[^A-Za-z0-9-]', '-'
    $stdoutPath = Join-Path $logDirectory ($logName + '.out.log')
    $stderrPath = Join-Path $logDirectory ($logName + '.err.log')
    Write-Output "RUN isolated: $filter"
    $testProcess = Start-Process -FilePath 'dotnet' -ArgumentList @('"' + $testBinary + '"') -WorkingDirectory $projectRoot -WindowStyle Hidden -RedirectStandardOutput $stdoutPath -RedirectStandardError $stderrPath -PassThru
    if (!$testProcess.WaitForExit($TimeoutSeconds * 1000)) {
        $testProcess.Kill($true)
        $testProcess.WaitForExit()
        $failed++
        Write-Output "TIMEOUT: $filter"
        & (Join-Path $Runtime 'nsd.exe') -kill
    } else {
        $testProcess.WaitForExit()
        if ($testProcess.ExitCode -ne 0) { $failed++ }
        Write-Output "EXIT $($testProcess.ExitCode): $filter"
        if ($testProcess.ExitCode -lt 0) { & (Join-Path $Runtime 'nsd.exe') -kill }
    }
    if ($ShowOutput) { Get-Content -LiteralPath $stdoutPath }
    else { Get-Content -LiteralPath $stdoutPath | Where-Object { $_ -match '^(FAIL|NOT EXERCISED|SELECTED TESTS|FULL TEST)|PANIC|Fatal error' } }
    if ($ShowOutput) { Get-Content -LiteralPath $stderrPath }
    else { Get-Content -LiteralPath $stderrPath -TotalCount 40 }
}
Write-Output "Isolated native groups: $($Filters.Count - $failed)/$($Filters.Count) passed. Logs: $logDirectory"
exit [int]($failed -gt 0)
