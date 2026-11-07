#Requires -Version 7.4
<#
    Logging helpers. Dot-sourced by every stage script and by local-ci.ps1.

    Each stage writes a banner at start and a one-line verdict at end. All other output goes to
    the stage's own log file. The summary parser is dumb on purpose; wording matters.

    ASCII markers only — survive every console, log viewer and CI web UI.
#>

function Get-Ts {
    [DateTime]::UtcNow.ToString('yyyy-MM-ddTHH:mm:ssZ')
}

function Write-StageInfo {
    param([string]$Message)
    $stage = ${script:StageName} ?? 'ci'
    Write-Host "[$(Get-Ts)] ${stage}: $Message"
}

function Write-StageStep {
    param([string]$Message)
    $stage = ${script:StageName} ?? 'ci'
    Write-Host "[$(Get-Ts)] ${stage}: $Message" -ForegroundColor Cyan
}

function Write-StageWarn {
    param([string]$Message)
    $stage = ${script:StageName} ?? 'ci'
    Write-Host "[$(Get-Ts)] ${stage}: $Message" -ForegroundColor Yellow
}

function Write-StageError {
    param([string]$Message)
    $stage = ${script:StageName} ?? 'ci'
    Write-Host "[$(Get-Ts)] ${stage}: $Message" -ForegroundColor Red
}

function Write-StageBanner {
    param([string]$Message)
    Write-Host ''
    Write-Host "[$(Get-Ts)] BANNER:=== $Message ===" -ForegroundColor Blue
}

# Verdicts the summary table reads. Keep the EXACT strings — the parser matches `VERDICT: STATE`.
function Write-StagePass {
    param([string]$Message = '')
    $stage = ${script:StageName} ?? 'ci'
    Write-Host "[$(Get-Ts)] ${stage}: VERDICT: PASS $Message" -ForegroundColor Green
}

function Write-StageFail {
    param([string]$Message = '')
    $stage = ${script:StageName} ?? 'ci'
    Write-Host "[$(Get-Ts)] ${stage}: VERDICT: FAIL $Message" -ForegroundColor Red
}

function Write-StageSkip {
    param([string]$Message = '')
    $stage = ${script:StageName} ?? 'ci'
    Write-Host "[$(Get-Ts)] ${stage}: VERDICT: SKIP $Message" -ForegroundColor DarkGray
}

function Write-StageWarnVerdict {
    param([string]$Message = '')
    $stage = ${script:StageName} ?? 'ci'
    Write-Host "[$(Get-Ts)] ${stage}: VERDICT: WARN $Message" -ForegroundColor Yellow
}
