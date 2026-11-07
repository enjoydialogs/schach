#Requires -Version 7.4
<#
    web-Stufe: src/Schach.Web mit npm ci, lint (oxlint), test (Vitest), build (tsc + Vite).
#>
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false

$thisDir  = Split-Path -Parent $PSCommandPath
$repoRoot = Split-Path -Parent (Split-Path -Parent $thisDir)
Set-Location -LiteralPath $repoRoot

. (Join-Path $thisDir '..\lib\Log.ps1')
$script:StageName = 'web'

Write-StageBanner 'web'

$webDir = 'src/Schach.Web'
if (-not (Test-Path -LiteralPath (Join-Path $webDir 'package.json'))) {
    Write-StageSkip "no $webDir/package.json"
    exit 0
}
if (-not (Get-Command npm -ErrorAction SilentlyContinue)) { Write-StageFail 'npm is not on PATH'; exit 1 }

Set-Location -LiteralPath $webDir
foreach ($cmd in @('ci', 'run lint', 'test', 'run build')) {
    Write-StageStep "npm $cmd"
    & npm @($cmd -split ' ') 2>&1 | Out-Host
    if ($LASTEXITCODE -ne 0) {
        Write-StageFail "npm $cmd failed"
        exit 1
    }
}

Write-StagePass 'npm ci, lint, test and build green'
exit 0
