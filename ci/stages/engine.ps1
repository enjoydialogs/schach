#Requires -Version 7.4
<#
    engine-Stufe: pytest des Engine-Dienstes mit SCHACH_ENGINE_FAKE_MODEL=1 (SCHACH-SPEC 15.2).
    Meldet SKIP, falls engine/pyproject.toml fehlt. Installiert in engine/.venv (gitignored).
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

# Erstes Python ab 3.12, das wirklich startet. Unter Windows ist `python3` oft nur der
# Store-Platzhalter in WindowsApps, der nichts ausfuehrt.
$python = @('python3', 'python') | Where-Object {
    (Get-Command $_ -ErrorAction SilentlyContinue) -and
    ((& $_ -c 'import sys; print(int(sys.version_info >= (3, 12)))' 2>$null) -eq '1')
} | Select-Object -First 1
if (-not $python) { Write-StageFail 'no python >= 3.12 on PATH'; exit 1 }

Set-Location -LiteralPath 'engine'
$env:SCHACH_ENGINE_FAKE_MODEL = '1'

# Eigene venv (gitignored): Ubuntu 24.04 verweigert pip im System-Python (PEP 668).
$venv = '.venv'
if (-not (Test-Path -LiteralPath $venv)) {
    Write-StageStep "$python -m venv $venv"
    & $python -m venv $venv 2>&1 | Out-Host
    if ($LASTEXITCODE -ne 0) { Write-StageFail 'venv creation failed'; exit 1 }
}
$venvPython = if ($IsWindows) { Join-Path $venv 'Scripts/python.exe' } else { Join-Path $venv 'bin/python' }

Write-StageStep 'pip install -e .[test]'
& $venvPython -m pip install --quiet --disable-pip-version-check -e '.[test]' 2>&1 | Out-Host
if ($LASTEXITCODE -ne 0) { Write-StageFail 'pip install failed'; exit 1 }

Write-StageStep 'pytest'
& $venvPython -m pytest 2>&1 | Out-Host
if ($LASTEXITCODE -eq 0) { Write-StagePass 'pytest green'; exit 0 }
Write-StageFail 'pytest failed'
exit 1
