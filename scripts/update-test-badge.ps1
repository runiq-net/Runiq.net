param(
    [Parameter(Mandatory = $true)]
    [string]$ResultsDirectory,

    [Parameter(Mandatory = $true)]
    [string]$OutputPath,

    [ValidateSet('success', 'failure', 'skipped', 'cancelled', 'unknown')]
    [string]$TestOutcome = 'unknown',

    [string]$SummaryPath,

    [int]$ExpectedResultCount = 0
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
$executed = 0
$validResults = 0
$incomplete = $false

foreach ($file in $resultFiles) {
    [xml]$trx = Get-Content -LiteralPath $file.FullName -Raw
    $counters = $trx.TestRun.ResultSummary.Counters

    if ($null -eq $counters) {
        $incomplete = $true
        continue
    }

    $validResults++
    $total += [int]$counters.total
    $executed += [int]$counters.executed
    $passed += [int]$counters.passed
    $failed += [int]$counters.failed
    if ($trx.TestRun.ResultSummary.outcome -notin @('Completed', 'Passed') -or
        [int]$counters.executed -ne ([int]$counters.passed + [int]$counters.failed)) {
        $incomplete = $true
    }

    $notExecuted = 0
    if ($null -ne $counters.notExecuted) {
        $notExecuted = [int]$counters.notExecuted
    }

    $skipped += $notExecuted
}

$counts = "$executed run, $passed passed, $failed failed, $skipped skipped"
if ($validResults -eq 0 -or $total -eq 0) {
    $message = 'no results'
    $color = 'lightgrey'
    if ($TestOutcome -eq 'failure') {
        $message = 'run failed; no results'
        $color = 'red'
    }
}
elseif ($failed -gt 0 -or $TestOutcome -eq 'failure') {
    $message = "run failed; $counts"
    $color = 'red'
}
elseif ($incomplete -or $validResults -lt $ExpectedResultCount -or $TestOutcome -in @('skipped', 'cancelled')) {
    $message = "incomplete; $counts"
    $color = 'orange'
}
elseif ($skipped -gt 0) {
    $message = $counts
    $color = 'yellow'
}
else {
    $message = $counts
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

if ($SummaryPath) {
    @"
## .NET test results

**$message**

| Metric | Count |
| --- | ---: |
| Discovered | $total |
| Executed | $executed |
| Passed | $passed |
| Failed | $failed |
| Skipped | $skipped |
| TRX reports | $validResults |

Test step outcome: $TestOutcome. Counts come from this run's TRX reports and exclude dashboard JavaScript tests.
Download the **test-results** artifact for individual test results. Missing or incomplete reports are not a passing run.
"@ | Add-Content -LiteralPath $SummaryPath -Encoding utf8
}
