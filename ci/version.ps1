#Requires -Version 7.4
<#
.SYNOPSIS
    Compute the version string once per run. Dot-sourced by local-ci.ps1 at startup; also runs
    standalone from release.yml.

.DESCRIPTION
    Format: <BaseVersion>+build.<count>.sha.<short>[.dirty]
      <count> = `git rev-list --count HEAD` — monotonic across every machine, no state file.
      <short> = `git rev-parse --short=12 HEAD` — same length GitHub uses in URLs.
      .dirty  = appended when the working tree has uncommitted changes.

    BaseVersion comes from ci/config.ps1. It is NOT bumped by this script.
    Writes `.local-ci/version.txt` so MSBuild and Dockerfiles can read it without re-running git.

.EXAMPLE
    .\ci\version.ps1
    0.0.0+build.416.sha.66810ff5b6c9.dirty
#>
[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$thisDir = Split-Path -Parent $PSCommandPath
. (Join-Path $thisDir 'config.ps1')

# Require a git repo.
& git rev-parse --git-dir *>&1 | Out-Null
if ($LASTEXITCODE -ne 0) {
    Write-Error 'ci/version.ps1: not inside a git repository'
    exit 1
}

$count = (& git rev-list --count HEAD 2>$null).Trim()
if (-not $count) { $count = '0' }
$short = (& git rev-parse --short=12 HEAD 2>$null).Trim()
if (-not $short) { $short = 'nocommit' }

$dirty = ''
# A dirty tree earns a +dirty suffix — a locally built image can't be mistaken for the commit
# it almost is. Pre-push always runs on committed commits only, so this only shows up during
# interactive dev runs.
$unstaged = & git diff --quiet HEAD -- 2>$null
$isUnstagedDirty = ($LASTEXITCODE -ne 0)
$staged   = & git diff --cached --quiet 2>$null
$isStagedDirty = ($LASTEXITCODE -ne 0)
if ($isUnstagedDirty -or $isStagedDirty) { $dirty = '.dirty' }

$script:LocalCiVersion = "$($script:BaseVersion)+build.$count.sha.$short$dirty"

$null = New-Item -ItemType Directory -Path '.local-ci' -Force
Set-Content -LiteralPath '.local-ci/version.txt' -Value $script:LocalCiVersion -Encoding utf8 -NoNewline

# Called directly: print and exit. Useful in release.yml and `docker build --build-arg`.
if ($MyInvocation.InvocationName -ne '.') {
    Write-Output $script:LocalCiVersion
}
