#Requires -Version 7.4
<#
    secrets stage: run gitleaks on the push range. ALWAYS runs — no path filter.

    Config: .gitleaks.toml at repo root carries the allowlist for placeholder patterns the
    compose stage legitimately uses.
#>
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false

$thisDir = Split-Path -Parent $PSCommandPath
. (Join-Path $thisDir '..\lib\Log.ps1')
$script:StageName = 'secrets'

Write-StageBanner 'secrets'

if (-not (Get-Command gitleaks -ErrorAction SilentlyContinue)) {
    # Oeffentliches Repo: in CI ist gitleaks Pflicht (SCHACH-SPEC 13.5), lokal nur eine Warnung.
    if ($env:CI -eq 'true') {
        Write-StageFail 'gitleaks not installed in CI'
        exit 1
    }
    Write-StageWarnVerdict 'gitleaks not installed; secrets stage degraded. Install: winget install gitleaks'
    exit 0
}

$cfg = @()
if (Test-Path -LiteralPath '.gitleaks.toml') { $cfg = @('--config=.gitleaks.toml') }

$remoteSha = $env:GIT_PRE_PUSH_REMOTE_SHA
if ($remoteSha -and $remoteSha -ne '0000000000000000000000000000000000000000') {
    Write-StageStep "scanning commits $remoteSha..HEAD (pre-push range)"
    & gitleaks git @cfg --no-banner --exit-code=1 --log-opts="$remoteSha..HEAD"
    if ($LASTEXITCODE -eq 0) {
        Write-StagePass 'no leaks in push range'
        exit 0
    } else {
        Write-StageFail 'gitleaks found secrets in the push range'
        exit 1
    }
}

Write-StageStep 'scanning repository history and working tree'
$rc = 0
& gitleaks git @cfg --no-banner --exit-code=1
if ($LASTEXITCODE -ne 0) { $rc = 1 }
& gitleaks dir @cfg --no-banner --exit-code=1 .
if ($LASTEXITCODE -ne 0) { $rc = 1 }

if ($rc -eq 0) { Write-StagePass 'no leaks'; exit 0 }
Write-StageFail 'gitleaks found secrets'
exit 1
