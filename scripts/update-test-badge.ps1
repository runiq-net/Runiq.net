param(
    [Parameter(Mandatory = $true)]
    [string]$ResultsDirectory,

    [Parameter(Mandatory = $true)]
    [string]$OutputPath
)

$ErrorActionPreference = 'Stop'

$resultFiles = @()
if (Test-Path -LiteralPath $ResultsDirectory) {
    $resultFiles = Get-ChildItem -LiteralPath $ResultsDirectory -Filter '*.trx' -File -Recurse
}

$total = 0
$passed = 0
$failed = 0
$skipped = 0

foreach ($file in $resultFiles) {
    [xml]$trx = Get-Content -LiteralPath $file.FullName -Raw
    $counters = $trx.TestRun.ResultSummary.Counters

    if ($null -eq $counters) {
        continue
    }

    $total += [int]$counters.total
    $passed += [int]$counters.passed
    $failed += [int]$counters.failed

    $notExecuted = 0
    if ($null -ne $counters.notExecuted) {
        $notExecuted = [int]$counters.notExecuted
    }

    $skipped += $notExecuted
}

if ($resultFiles.Count -eq 0) {
    $message = 'no results'
    $color = 'lightgrey'
}
elseif ($failed -gt 0) {
    $message = "$passed/$total passing, $failed failed"
    $color = 'red'
}
elseif ($skipped -gt 0) {
    $message = "$passed/$total passing, $skipped skipped"
    $color = 'yellow'
}
else {
    $message = "$passed/$total passing"
    $color = 'brightgreen'
}

$badge = [ordered]@{
    schemaVersion = 1
    label = 'tests'
    message = $message
    color = $color
}

$outputDirectory = Split-Path -Path $OutputPath -Parent
if ($outputDirectory -and -not (Test-Path -LiteralPath $outputDirectory)) {
    New-Item -ItemType Directory -Path $outputDirectory | Out-Null
}

$badge | ConvertTo-Json -Depth 3 | Set-Content -LiteralPath $OutputPath -Encoding utf8
