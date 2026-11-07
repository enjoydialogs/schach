#Requires -Version 7.4
<#
    format-Stufe: dotnet format --verify-no-changes ueber die ganze Loesung. Anders als Kronrat
    (Altbestand) startet schach formatiert, also scheitert jede Abweichung, auch mit -All.
#>
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false

$thisDir  = Split-Path -Parent $PSCommandPath
$repoRoot = Split-Path -Parent (Split-Path -Parent $thisDir)
Set-Location -LiteralPath $repoRoot

. (Join-Path $thisDir '..\lib\Log.ps1')
. (Join-Path $thisDir '..\config.ps1')
$script:StageName = 'format'

Write-StageBanner 'format'

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) { Write-StageFail 'dotnet is not on PATH'; exit 1 }

Write-StageStep "dotnet format $($script:Solution) --verify-no-changes"
& dotnet format $script:Solution --verify-no-changes 2>&1 | Out-Host
if ($LASTEXITCODE -eq 0) { Write-StagePass 'formatting clean'; exit 0 }
Write-StageFail "formatting drift. Fix: dotnet format $($script:Solution)"
exit 1
