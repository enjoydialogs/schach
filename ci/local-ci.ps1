#Requires -Version 7.4
<#
.SYNOPSIS
    Einstieg der CI, lokal wie in .github/workflows/ci.yml (nach Kronrat). Siehe ci/README.md.

.DESCRIPTION
    Ohne Argumente: nur die Stufen, deren Pfade sich gegen den merge-base mit origin/main geaendert
    haben. Jede Stufe laeuft als eigener pwsh-Prozess und schreibt nach .local-ci/logs/<run>/.

.PARAMETER All
    Alle Stufen. So laeuft die Cloud-CI.

.PARAMETER Since
    Vergleichsbasis statt origin/main.

.PARAMETER Only
    Kommagetrennte Stufennamen, ohne Aenderungserkennung.

.PARAMETER NoColor
    Ausgabe ohne ANSI-Farben.

.EXAMPLE
    pwsh ci/local-ci.ps1

.EXAMPLE
    pwsh ci/local-ci.ps1 -All

.EXAMPLE
    pwsh ci/local-ci.ps1 -Only web,build
#>
[CmdletBinding()]
param(
    [switch]$All,
    [string]$Since,
    [string]$Only,
    [switch]$NoColor
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false

if ($NoColor) {
    $PSStyle.OutputRendering = 'PlainText'
}

$ThisDir  = $PSScriptRoot
$RepoRoot = Split-Path -Parent $ThisDir
Set-Location -LiteralPath $RepoRoot

. (Join-Path $ThisDir 'config.ps1')
. (Join-Path $ThisDir 'lib\Log.ps1')
. (Join-Path $ThisDir 'lib\Diff.ps1')
. (Join-Path $ThisDir 'lib\Summary.ps1')

# Schalter fuer die Kindprozesse der Stufen (frische pwsh, kein geteilter Scope).
if ($Since) { $env:LOCAL_CI_SINCE = $Since }
if ($All)   { $env:LOCAL_CI_ALL   = '1' }
# Keine langlebigen MSBuild-Knoten: die Stufen sollen enden, wenn ihr Befehl endet.
$env:MSBUILDDISABLENODEREUSE = '1'
$env:DOTNET_CLI_USE_MSBUILD_SERVER = '0'

$script:ForceAll   = $All.IsPresent
$script:OnlyStages = if ($Only) { @($Only.Split(',').Trim()) } else { @() }

# Version zuerst: .local-ci/version.txt fuer MSBuild und Image-Labels.
. (Join-Path $ThisDir 'version.ps1')

$RunId   = "$([DateTime]::UtcNow.ToString('yyyyMMddTHHmmss'))Z-$PID"
$LogsDir = Join-Path '.local-ci\logs' $RunId
$null    = New-Item -ItemType Directory -Path $LogsDir -Force
'' | Set-Content -LiteralPath (Join-Path $LogsDir 'timings.tsv')

$selected = @(
    if (@($script:OnlyStages).Count -gt 0) {
        $script:OnlyStages
    } elseif ($script:ForceAll) {
        $script:Stages
    } else {
        foreach ($s in $script:Stages) {
            $globs = @($script:StagePaths[$s] ?? @())
            if ($s -eq 'secrets' -or (Test-DiffMatchesGlobs -Globs $globs)) { $s }
        }
    }
)

foreach ($s in $selected) {
    $script:StageName = 'ci'
    if (-not (Test-Path -LiteralPath (Join-Path $ThisDir "stages\$s.ps1"))) {
        Write-StageError "unknown stage: $s (ci/stages/$s.ps1 missing)"
        exit 2
    }
}

$script:StageName = 'ci'
Write-StageStep "repo:     $script:RepoName"
Write-StageStep "version:  $script:LocalCiVersion"
Write-StageStep "run:      $RunId"
Write-StageStep "stages:   $(if ($selected) { $selected -join ' ' } else { '<none>' })"
Write-StageStep "baseline: $(Get-DiffBaselineRef)"

if ($selected.Count -eq 0) {
    Write-StageWarn 'no stages matched the change set - nothing to do'
    exit 0
}

# Eine Stufe als eigener Prozess; stdout und stderr landen in .local-ci/logs/<run>/<stage>.log.
function Invoke-Stage {
    param([string]$Stage, [string]$StagesDir, [string]$LogsDir)

    $stageScript = Join-Path $StagesDir "$Stage.ps1"
    $log   = Join-Path $LogsDir "$Stage.log"
    $start = [DateTime]::UtcNow

    # Nicht `Start-Process -Wait`: das wartet auf den ganzen Prozessbaum, und MSBuild-Knoten oder der
    # Compiler-Server leben nach `dotnet build` weiter. WaitForExit wartet nur auf die Stufe selbst.
    $proc = Start-Process -FilePath 'pwsh' -ArgumentList @('-NoProfile', '-NoLogo', '-File', $stageScript) `
                          -NoNewWindow -PassThru `
                          -RedirectStandardOutput $log -RedirectStandardError "$log.err"
    $proc.WaitForExit()

    # Windows gibt den stderr-Handle nach Start-Process manchmal kurz verzoegert frei.
    if (Test-Path -LiteralPath "$log.err") {
        try {
            Add-Content -LiteralPath $log -Value ((Get-Content -LiteralPath "$log.err" -Raw -ErrorAction SilentlyContinue) ?? '') -ErrorAction SilentlyContinue
        } catch { }
        $attempts = 0
        while ((Test-Path -LiteralPath "$log.err") -and ($attempts -lt 10)) {
            try { Remove-Item -LiteralPath "$log.err" -Force -ErrorAction Stop; break }
            catch { Start-Sleep -Milliseconds 50; $attempts++ }
        }
    }

    $secs = [int]([DateTime]::UtcNow - $start).TotalSeconds
    Add-Content -LiteralPath (Join-Path $LogsDir 'timings.tsv') -Value "$Stage`t$secs"

    if (-not (Select-String -LiteralPath $log -Pattern 'VERDICT: (PASS|FAIL|WARN|SKIP)' -Quiet)) {
        $ts = [DateTime]::UtcNow.ToString('yyyy-MM-ddTHH:mm:ssZ')
        Add-Content -LiteralPath $log -Value "[$ts] ${Stage}: VERDICT: FAIL exit code $($proc.ExitCode), no verdict logged"
    }

    return $proc.ExitCode
}

$stagesDir = Join-Path $ThisDir 'stages'
$invokeStageDef = ${function:Invoke-Stage}.ToString()
$overallRc = 0

foreach ($group in $script:ParallelGroups) {
    $toRun = @($group | Where-Object { $_ -in $selected })

    foreach ($s in @($group | Where-Object { $_ -notin $selected })) {
        $ts = [DateTime]::UtcNow.ToString('yyyy-MM-ddTHH:mm:ssZ')
        @("[$ts] BANNER:=== $s ===", "[$ts] ${s}: VERDICT: SKIP not in change set") |
            Set-Content -LiteralPath (Join-Path $LogsDir "$s.log")
        Add-Content -LiteralPath (Join-Path $LogsDir 'timings.tsv') -Value "$s`t0"
    }

    if ($toRun.Count -eq 0) { continue }

    if ($toRun.Count -eq 1) {
        $rc = Invoke-Stage -Stage $toRun[0] -StagesDir $stagesDir -LogsDir $LogsDir
        if ($rc -ne 0) { $overallRc = 1 }
    } else {
        # Fehler sammeln, nicht beim ersten abbrechen.
        $rcs = $toRun | ForEach-Object -ThrottleLimit $toRun.Count -Parallel {
            ${function:Invoke-Stage} = $using:invokeStageDef
            Invoke-Stage -Stage $_ -StagesDir $using:stagesDir -LogsDir $using:LogsDir
        }
        foreach ($rc in $rcs) { if ($rc -ne 0) { $overallRc = 1 } }
    }
}

# Logs der Stufen in die Konsole (CI liest sie im Job-Log), dann die Tabelle.
if ($env:CI -eq 'true') {
    foreach ($s in $selected) {
        $log = Join-Path $LogsDir "$s.log"
        if (Test-Path -LiteralPath $log) {
            Write-Host "::group::$s"
            Get-Content -LiteralPath $log | Write-Host
            Write-Host '::endgroup::'
        }
    }
}

if (-not (Show-RunSummary -RunId $RunId)) { $overallRc = 1 }

exit $overallRc
