#Requires -Version 7.4
<#
    vulns stage: dotnet list package --vulnerable --include-transitive. Fails on >= High,
    warns on lower.
#>
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false

$thisDir  = Split-Path -Parent $PSCommandPath
$repoRoot = Split-Path -Parent (Split-Path -Parent $thisDir)
Set-Location -LiteralPath $repoRoot

. (Join-Path $thisDir '..\lib\Log.ps1')
. (Join-Path $thisDir '..\config.ps1')
$script:StageName = 'vulns'

Write-StageBanner 'vulns'

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) { Write-StageFail 'dotnet is not on PATH'; exit 1 }

$sln = @($script:Solution) |
       Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
if (-not $sln) { Write-StageFail 'no solution file found'; exit 1 }

Write-StageStep "dotnet list package --vulnerable --include-transitive against $sln"
& dotnet restore $sln *>&1 | Out-Null

$out = & dotnet list $sln package --vulnerable --include-transitive 2>&1 | Out-String
$out | Out-Host

if ($out -match 'no vulnerable packages') {
    Write-StagePass 'no vulnerable packages'
    exit 0
}

$sevRank = @{ Low = 1; Moderate = 2; High = 3; Critical = 4 }
$failAt  = $sevRank[$script:VulnFailSeverity] ?? 3

$worst = 0
foreach ($line in ($out -split '\r?\n')) {
    foreach ($sev in 'Critical','High','Moderate','Low') {
        if ($line -match "\b$sev\b") {
            $rank = $sevRank[$sev]
            if ($rank -gt $worst) { $worst = $rank }
            break
        }
    }
}

if ($worst -ge $failAt) {
    Write-StageFail "vulnerable packages found at >= $($script:VulnFailSeverity) - update pins in Directory.Packages.props"
    exit 1
} elseif ($worst -gt 0) {
    Write-StageWarnVerdict "vulnerable packages found below $($script:VulnFailSeverity) - consider updating"
    exit 0
}
Write-StagePass 'no vulnerable packages (parser saw no severity match)'
exit 0
