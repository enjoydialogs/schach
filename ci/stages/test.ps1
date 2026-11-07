#Requires -Version 7.4
<#
    test-Stufe: dotnet test --no-build auf Microsoft.Testing.Platform (nach GladiusEtPilum).

    Container-Tests (Testcontainers, [Trait("Category","Container")]) laufen nur mit
    SCHACH_CONTAINER_TESTS=1. Die CI auf ubuntu-latest setzt die Variable; lokal bleibt sie aus,
    dann melden sich diese Tests als uebersprungen und Docker wird nicht angefasst.
#>
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false

$thisDir  = Split-Path -Parent $PSCommandPath
$repoRoot = Split-Path -Parent (Split-Path -Parent $thisDir)
Set-Location -LiteralPath $repoRoot

. (Join-Path $thisDir '..\lib\Log.ps1')
. (Join-Path $thisDir '..\config.ps1')
$script:StageName = 'test'

Write-StageBanner 'test'

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) { Write-StageFail 'dotnet is not on PATH'; exit 1 }

$containerTests = $env:SCHACH_CONTAINER_TESTS -eq '1'
if ($containerTests) {
    & docker info *>&1 | Out-Null
    if ($LASTEXITCODE -ne 0) {
        Write-StageFail 'SCHACH_CONTAINER_TESTS=1, but the docker daemon is not reachable'
        exit 1
    }
    Write-StageInfo 'container tests: ON (SCHACH_CONTAINER_TESTS=1)'
} else {
    Write-StageInfo 'container tests: OFF (set SCHACH_CONTAINER_TESTS=1 to run them; CI does)'
}

# MTP unter `dotnet test --solution` schreibt alle TRX-Dateien in TestResults/ an der Wurzel.
$resultsDir = Join-Path (Resolve-Path '.').Path 'TestResults'
if (Test-Path -LiteralPath $resultsDir) { Remove-Item -Recurse -Force -LiteralPath $resultsDir }

Write-StageStep "dotnet test --solution $($script:Solution) --no-build -c Release (MTP)"
& dotnet test --solution $script:Solution --configuration Release --no-build --report-trx 2>&1 | Out-Host
if ($LASTEXITCODE -eq 0) {
    $count = @(Get-ChildItem -LiteralPath $resultsDir -Filter '*.trx' -Recurse -ErrorAction SilentlyContinue).Count
    Write-StagePass "tests green ($count TRX file(s))"
    exit 0
}
Write-StageFail "tests failed - TRX under $resultsDir"
exit 1
