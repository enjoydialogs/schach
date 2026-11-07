#Requires -Version 7.4
<#
    Changed-paths detection. Dot-sourced by local-ci.ps1. Decides which stages run on an
    argument-less invocation.

    Baseline is the merge-base against origin/main — "what a reviewer would see on GitHub".
    Survives first-push-of-branch (@{push} would error there) and gives a stable answer whether
    you ran the script before or after your latest commit.

    Overrides via env or script-scope variable:
      $env:LOCAL_CI_SINCE or $script:Since  — diff against that ref
      $script:ForceAll                      — run every stage (set by --All)
      $script:OnlyStages                    — explicit stage list (set by --Only)
#>

function Get-DiffBaselineRef {
    # All inputs come through env vars so child stage processes see them (stages run via
    # Start-Process, which is a fresh pwsh with its own script scope).
    if ($env:LOCAL_CI_SINCE) { return $env:LOCAL_CI_SINCE }

    # Pre-push context: the remote SHA is exactly what GitHub already has.
    $remoteSha = $env:GIT_PRE_PUSH_REMOTE_SHA
    if ($remoteSha -and $remoteSha -ne '0000000000000000000000000000000000000000') {
        return $remoteSha
    }

    $hasMain = & git rev-parse --verify --quiet origin/main 2>$null
    if ($LASTEXITCODE -eq 0 -and $hasMain) {
        return (& git merge-base HEAD origin/main).Trim()
    }

    # No origin/main (fresh clone, mirror, offline): fall back to the first commit — the safe
    # default is "run everything".
    return (& git rev-list --max-parents=0 HEAD | Select-Object -First 1).Trim()
}

function Get-ChangedPaths {
    $base = Get-DiffBaselineRef
    $committed = (& git diff --name-only $base -- 2>$null) ?? @()
    $staged    = (& git diff --name-only --cached 2>$null) ?? @()
    $untracked = (& git ls-files --others --exclude-standard 2>$null) ?? @()
    @($committed) + @($staged) + @($untracked) | Where-Object { $_ } | Sort-Object -Unique
}

function Test-PathAgainstGlobs {
    param(
        [string]$Path,
        [string[]]$Globs
    )
    foreach ($g in $Globs) {
        # Translate shell-style **/x into a .NET-compatible wildcard. PowerShell's -like doesn't
        # know **; we handle the common cases here: `**/x` matches at any depth, `dir/**` matches
        # everything inside dir, bare `*.ext` matches on basename only but we check against the
        # full path too.
        if ($g -eq $Path) { return $true }
        if ($Path -like $g) { return $true }
        if ($g.StartsWith('**/')) {
            $suffix = $g.Substring(3)
            if ($Path -like "*/$suffix" -or $Path -like $suffix -or (Split-Path $Path -Leaf) -like $suffix) {
                return $true
            }
        }
        if ($g.EndsWith('/**')) {
            $prefix = $g.Substring(0, $g.Length - 3)
            if ($Path -like "$prefix/*" -or $Path -eq $prefix) { return $true }
        }
        if ($g -like '*/**/*') {
            # Two-sided glob like a/**/b — flatten to prefix + suffix with wildcard between.
            $parts = $g -split '/\*\*/'
            if ($parts.Count -eq 2 -and $Path -like "$($parts[0])/*/$($parts[1])") { return $true }
        }
    }
    return $false
}

function Test-DiffMatchesGlobs {
    param([string[]]$Globs)
    # Wrap both in @() — strict mode throws on .Count against $null, and Get-ChangedPaths
    # returns $null when the pipeline is empty. Caught by the first real hook push on a
    # ci-only branch where no stage paths matched anything.
    $g = @($Globs)
    if ($g.Count -eq 0) { return $true }

    $changed = @(Get-ChangedPaths)
    if ($changed.Count -eq 0) { return $false }

    foreach ($p in $changed) {
        if (Test-PathAgainstGlobs -Path $p -Globs $g) { return $true }
    }
    return $false
}
