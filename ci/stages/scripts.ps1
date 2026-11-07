#Requires -Version 7.4
<#
    scripts stage: shellcheck + CRLF guard on shell scripts. Deploy ships bash scripts
    (deploy/db-init/*.sh, deploy/ops/*.sh); shellcheck + LF line endings keep them loadable
    on Linux.
#>
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false

$thisDir  = Split-Path -Parent $PSCommandPath
$repoRoot = Split-Path -Parent (Split-Path -Parent $thisDir)
Set-Location -LiteralPath $repoRoot

. (Join-Path $thisDir '..\lib\Log.ps1')
. (Join-Path $thisDir '..\lib\Diff.ps1')
$script:StageName = 'scripts'

Write-StageBanner 'scripts'

$changed = if ($env:LOCAL_CI_ALL -eq '1') {
    & git ls-files '*.sh' 'ci/hooks/*'
} else {
    Get-ChangedPaths
}

$targets = @(foreach ($p in $changed) {
    if (-not (Test-Path -LiteralPath $p)) { continue }
    if ($p -like '*.sh' -or $p -like 'ci/hooks/*' -or $p -like 'deploy/ops/*.sh' -or $p -like 'deploy/db-init/*.sh') {
        $p
    } else {
        # Shebang sniff — picks up ci/hooks/pre-push.
        $firstLine = (Get-Content -LiteralPath $p -TotalCount 1 -ErrorAction SilentlyContinue) ?? ''
        if ($firstLine -like '#!*sh*' -or $firstLine -like '#!*bash*') { $p }
    }
})

if ($targets.Count -eq 0) {
    Write-StageSkip 'no shell scripts in change set'
    exit 0
}

Write-StageStep "checking $($targets.Count) shell script(s)"
$rc = 0

foreach ($p in $targets) {
    # CRLF audit — a CR in the shebang reads as "bad interpreter: /bin/bash^M".
    $bytes = [System.IO.File]::ReadAllBytes($p)
    if ($bytes -contains 13) {
        Write-StageFail "$p has CRLF line endings - add to .gitattributes: '$p text eol=lf' and renormalize"
        $rc = 1
    }
    # deploy/ scripts must be executable (Postgres entrypoint rule).
    if ($p -like 'deploy/*') {
        $treeMode = (& git ls-files --stage $p | ForEach-Object { ($_ -split '\s+')[0] }) | Select-Object -First 1
        if ($treeMode -and $treeMode -ne '100755') {
            Write-StageWarn "$p is not executable in git tree; run: git update-index --chmod=+x $p"
        }
    }
}

# shellcheck
if (Get-Command shellcheck -ErrorAction SilentlyContinue) {
    foreach ($p in $targets) {
        & shellcheck -e SC1091 $p
        if ($LASTEXITCODE -ne 0) {
            Write-StageFail "shellcheck failures in $p"
            $rc = 1
        }
    }
} else {
    Write-StageWarn 'shellcheck not installed; skipping lint'
}

# bash -n syntax guard
if (Get-Command bash -ErrorAction SilentlyContinue) {
    foreach ($p in $targets) {
        & bash -n $p 2>$null
        if ($LASTEXITCODE -ne 0) {
            Write-StageFail "$p has a bash syntax error"
            $rc = 1
        }
    }
}

if ($rc -eq 0) {
    Write-StagePass "shell scripts clean ($($targets.Count) file(s))"
    exit 0
}
Write-StageFail 'shell script problems found'
exit 1
