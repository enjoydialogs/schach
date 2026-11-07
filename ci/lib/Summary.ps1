#Requires -Version 7.4
<#
    Reads the per-stage logs and prints the final pass/fail table. Dot-sourced by local-ci.ps1.

    Parser is deliberately simple: last `VERDICT: <STATE>` line per log file wins. Timings come
    from timings.tsv (one `stage<TAB>seconds` per line, written by local-ci.ps1).
#>

function Show-RunSummary {
    param(
        [Parameter(Mandatory)][string]$RunId
    )
    $logsDir = Join-Path '.local-ci/logs' $RunId
    if (-not (Test-Path -LiteralPath $logsDir)) {
        Write-Host "summary: no logs dir at $logsDir" -ForegroundColor Red
        return $false
    }

    $results   = @{}
    $durations = @{}

    foreach ($f in Get-ChildItem -LiteralPath $logsDir -Filter '*.log' -File) {
        $stage = $f.BaseName
        $lastVerdict = Select-String -LiteralPath $f.FullName -Pattern 'VERDICT: (PASS|FAIL|WARN|SKIP)' |
                       Select-Object -Last 1
        if ($lastVerdict) {
            $results[$stage] = ($lastVerdict.Matches[0].Groups[1].Value)
        } else {
            $results[$stage] = 'UNKNOWN'
        }
    }

    $timingsFile = Join-Path $logsDir 'timings.tsv'
    if (Test-Path -LiteralPath $timingsFile) {
        foreach ($line in Get-Content -LiteralPath $timingsFile) {
            $parts = $line -split "`t"
            if ($parts.Count -eq 2) { $durations[$parts[0]] = [int]$parts[1] }
        }
    }

    Write-Host ''
    Write-Host "=== local-ci summary - run $RunId ===" -ForegroundColor Blue
    $colors = @{ PASS = 'Green'; FAIL = 'Red'; WARN = 'Yellow'; SKIP = 'DarkGray' }

    $fails = 0; $warns = 0; $passes = 0; $skips = 0
    $widthStage = (@($script:Stages | ForEach-Object { $_.Length }) + 8 | Measure-Object -Maximum).Maximum

    Write-Host ("{0,-$widthStage}  {1,-6}  {2,8}  {3}" -f 'STAGE','STATE','SECONDS','LOG')
    Write-Host ("{0,-$widthStage}  {1,-6}  {2,8}  {3}" -f ('-' * $widthStage), '------', '-------', '---')

    foreach ($stage in $script:Stages) {
        $state = if ($results.ContainsKey($stage)) { $results[$stage] } else { 'SKIP' }
        $secs  = if ($durations.ContainsKey($stage)) { $durations[$stage] } else { '-' }
        $color = $colors[$state] ?? 'Yellow'

        switch ($state) {
            'PASS' { $passes++ }
            'FAIL' { $fails++  }
            'WARN' { $warns++  }
            'SKIP' { $skips++  }
        }

        $logPath = Join-Path $logsDir "$stage.log"
        Write-Host ("{0,-$widthStage}  " -f $stage) -NoNewline
        Write-Host ("{0,-6}" -f $state) -NoNewline -ForegroundColor $color
        Write-Host ("  {0,8}  {1}" -f $secs, $logPath)
    }

    Write-Host ''
    $v = ${script:LocalCiVersion} ?? '?'
    Write-Host "pass:$passes  fail:$fails  warn:$warns  skip:$skips  version:$v"

    return ($fails -eq 0)
}

