$ErrorActionPreference = 'Stop'
$root = Join-Path ([IO.Path]::GetTempPath()) ('runiq-badge-tests-' + [guid]::NewGuid())
New-Item -ItemType Directory -Path $root | Out-Null

# Verifies aggregation, failure detection, skipped tests, and incomplete reports with isolated TRX fixtures.
function Test-BadgeScenario {
    param($Name, $Reports, $Outcome, $ExpectedColor, $ExpectedMessage, $ExpectedCount = 0)

    $directory = Join-Path $root $Name
    New-Item -ItemType Directory -Path $directory | Out-Null
    $index = 0
    foreach ($report in $Reports) {
        $report | Set-Content (Join-Path $directory "$index.trx")
        $index++
    }

    $output = Join-Path $directory 'nested/tests.json'
    $summary = Join-Path $directory 'summary.md'
    & "$PSScriptRoot/update-test-badge.ps1" -ResultsDirectory $directory -OutputPath $output `
        -TestOutcome $Outcome -ExpectedResultCount $ExpectedCount -SummaryPath $summary
    $badge = Get-Content $output -Raw | ConvertFrom-Json
    if ($badge.schemaVersion -ne 1 -or $badge.label -ne 'tests' -or
        $badge.color -ne $ExpectedColor -or $badge.message -ne $ExpectedMessage) {
        throw "${Name}: unexpected badge $(ConvertTo-Json $badge -Compress)"
    }
    if ((Get-Content $summary -Raw) -notmatch [regex]::Escape($ExpectedMessage)) {
        throw "${Name}: summary does not match the badge."
    }
    Write-Host "PASS: $Name"
}

$passed = '<TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010"><ResultSummary outcome="Completed"><Counters total="3" executed="3" passed="3" failed="0" notExecuted="0" /></ResultSummary></TestRun>'
$failed = '<TestRun><ResultSummary outcome="Failed"><Counters total="2" executed="2" passed="1" failed="1" notExecuted="0" /></ResultSummary></TestRun>'
$skipped = '<TestRun><ResultSummary outcome="Completed"><Counters total="2" executed="1" passed="1" failed="0" notExecuted="1" /></ResultSummary></TestRun>'
$aborted = '<TestRun><ResultSummary outcome="Aborted"><Counters total="3" executed="3" passed="2" failed="0" notExecuted="0" /></ResultSummary></TestRun>'
$empty = '<TestRun><ResultSummary outcome="Completed"><Counters total="0" executed="0" passed="0" failed="0" /></ResultSummary></TestRun>'

try {
    Test-BadgeScenario 'aggregate' @($passed, $passed) 'success' 'brightgreen' '6 run, 6 passed, 0 failed, 0 skipped' 2
    Test-BadgeScenario 'failed-tests' @($passed, $failed) 'failure' 'red' 'run failed; 5 run, 4 passed, 1 failed, 0 skipped'
    Test-BadgeScenario 'skipped-tests' @($passed, $skipped) 'success' 'yellow' '4 run, 4 passed, 0 failed, 1 skipped'
    Test-BadgeScenario 'no-reports' @() 'skipped' 'lightgrey' 'no results'
    Test-BadgeScenario 'empty-report' @($empty) 'success' 'lightgrey' 'no results'
    Test-BadgeScenario 'missing-counters' @('<TestRun />') 'success' 'lightgrey' 'no results'
    Test-BadgeScenario 'host-failure' @($passed) 'failure' 'red' 'run failed; 3 run, 3 passed, 0 failed, 0 skipped'
    Test-BadgeScenario 'failure-without-reports' @() 'failure' 'red' 'run failed; no results'
    Test-BadgeScenario 'aborted-report' @($aborted) 'success' 'orange' 'incomplete; 3 run, 2 passed, 0 failed, 0 skipped'
    Test-BadgeScenario 'missing-project' @($passed) 'success' 'orange' 'incomplete; 3 run, 3 passed, 0 failed, 0 skipped' 2
    Test-BadgeScenario 'cancelled-run' @($passed) 'cancelled' 'orange' 'incomplete; 3 run, 3 passed, 0 failed, 0 skipped'
    Test-BadgeScenario 'partial-invalid-reports' @($passed, '<TestRun />') 'success' 'orange' 'incomplete; 3 run, 3 passed, 0 failed, 0 skipped'
}
finally {
    # Only remove the unique test directory created under the system temporary directory.
    $resolved = [IO.Path]::GetFullPath($root)
    $tempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    if (-not $resolved.StartsWith($tempRoot, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Refusing cleanup outside the temporary directory.'
    }
    Remove-Item -LiteralPath $resolved -Recurse -Force
}
