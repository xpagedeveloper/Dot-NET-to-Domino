$ErrorActionPreference = 'Stop'

$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$file = Join-Path $root 'docs/intellisense-api-reference.md'
$errors = [System.Collections.Generic.List[string]]::new()

if (-not (Test-Path -LiteralPath $file -PathType Leaf)) { throw 'IntelliSense API reference is missing.' }

$lines = Get-Content -LiteralPath $file
$rows = $lines | Where-Object { $_ -match '^\| `[^`]+` \|' }
$members = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)

foreach ($row in $rows) {
    if (([regex]::Matches($row, '\|')).Count -ne 6) { $errors.Add("API row must contain exactly five columns: $row"); continue }
    $match = [regex]::Match($row, '^\| `([^`]+)` \|')
    if (-not $members.Add($match.Groups[1].Value)) { $errors.Add("Duplicate API member: $($match.Groups[1].Value)") }
    if ($row -notmatch '\[[^\]]+\]\(([^)]+)\)') { $errors.Add("API row is missing an example link: $row"); continue }
    $relative = $Matches[1].Replace('/', [IO.Path]::DirectorySeparatorChar)
    $resolved = [IO.Path]::GetFullPath((Join-Path (Split-Path -Parent $file) $relative))
    if (-not (Test-Path -LiteralPath $resolved -PathType Leaf)) { $errors.Add("Example link does not exist: $relative") }
}

if ($rows.Count -eq 0) { $errors.Add('IntelliSense API reference contains no API rows.') }
if ($errors.Count -gt 0) { $errors | ForEach-Object { Write-Host "ERROR: $_" }; throw "Validation failed with $($errors.Count) error(s)." }

Write-Host "INTELLISENSE-API-ROWS=$($rows.Count)"
Write-Host 'INTELLISENSE-API-DOCS=OK'
