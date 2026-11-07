#Requires -Version 7.4
<#
    build-Stufe: dotnet build -c Release. TreatWarningsAsErrors steht in Directory.Build.props,
    jede neue Warnung laesst die Stufe scheitern.
#>
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false

$thisDir  = Split-Path -Parent $PSCommandPath
$repoRoot = Split-Path -Parent (Split-Path -Parent $thisDir)
Set-Location -LiteralPath $repoRoot

. (Join-Path $thisDir '..\lib\Log.ps1')
. (Join-Path $thisDir '..\config.ps1')
$script:StageName = 'build'

Write-StageBanner 'build'

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) { Write-StageFail 'dotnet is not on PATH'; exit 1 }

$msbuildArgs = @()
if (Test-Path -LiteralPath '.local-ci/version.txt') {
    $v = (Get-Content -LiteralPath '.local-ci/version.txt' -Raw).Trim()
    $msbuildArgs += "-p:InformationalVersion=$v"
    Write-StageInfo "stamping InformationalVersion=$v"
}

Write-StageStep "dotnet build $($script:Solution) -c Release"
& dotnet build $script:Solution --configuration Release @msbuildArgs 2>&1 | Out-Host
if ($LASTEXITCODE -eq 0) { Write-StagePass 'build clean'; exit 0 }
Write-StageFail 'build failed - see output above'
exit 1
