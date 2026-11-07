#Requires -Version 7.4
<#
    engine-Stufe: pytest des Engine-Dienstes mit SCHACH_ENGINE_FAKE_MODEL=1 (SCHACH-SPEC 15.2).
    Meldet SKIP, solange engine/pyproject.toml fehlt; den Dienst legt Session 0c an.
#>
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false

$thisDir  = Split-Path -Parent $PSCommandPath
$repoRoot = Split-Path -Parent (Split-Path -Parent $thisDir)
Set-Location -LiteralPath $repoRoot

. (Join-Path $thisDir '..\lib\Log.ps1')
$script:StageName = 'engine'

Write-StageBanner 'engine'

if (-not (Test-Path -LiteralPath 'engine/pyproject.toml')) {
    Write-StageSkip 'engine/pyproject.toml missing (engine service arrives in phase 0c)'
    exit 0
}

$python = @('python3', 'python') | Where-Object { Get-Command $_ -ErrorAction SilentlyContinue } | Select-Object -First 1
if (-not $python) { Write-StageFail 'python is not on PATH'; exit 1 }

Set-Location -LiteralPath 'engine'
$env:SCHACH_ENGINE_FAKE_MODEL = '1'

Write-StageStep "$python -m pip install -e .[test]"
& $python -m pip install --quiet -e '.[test]' 2>&1 | Out-Host
if ($LASTEXITCODE -ne 0) { Write-StageFail 'pip install failed'; exit 1 }

Write-StageStep "$python -m pytest"
& $python -m pytest 2>&1 | Out-Host
if ($LASTEXITCODE -eq 0) { Write-StagePass 'pytest green'; exit 0 }
Write-StageFail 'pytest failed'
exit 1
