#Requires -Version 7.4
<#
    E2E-Strecken mit Playwright (SCHACH-SPEC 15.2, Ticket #40). NUR ZUM NACHLESEN.

    Auf den Windows-Entwicklungsrechnern laeuft kein Docker (Docker Desktop stuerzt ab). Die
    Strecken laufen darum nur in GitHub Actions (.github/workflows/e2e.yml). Dieses Skript zeigt
    denselben Ablauf fuer einen Rechner mit Docker und startet ohne -IchHabeDocker nichts.

        pwsh scripts/run-e2e.ps1               # zeigt nur die Schritte
        pwsh scripts/run-e2e.ps1 -IchHabeDocker  # fuehrt sie aus (Linux/macOS mit Docker)

    Testbetrieb: tests/Schach.E2E/compose.e2e.yml (Engine-Fixture, Ersatzsprueche, Mails als
    Dateien in tests/Schach.E2E/.mail, aus denen der Test den Anmeldecode liest).
#>
param(
    [switch] $IchHabeDocker,
    [switch] $Aufraeumen
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repo = Split-Path -Parent $PSScriptRoot
$e2e = Join-Path $repo 'tests/Schach.E2E'
$mail = Join-Path $e2e '.mail'
$compose = @('-f', 'docker-compose.yml', '-f', 'tests/Schach.E2E/compose.e2e.yml', '--profile', 'full')

$schritte = @(
    "mkdir -p tests/Schach.E2E/.mail && chmod 0777 tests/Schach.E2E/.mail",
    "SCHACH_ENGINE_FAKE_MODEL=1 docker compose $($compose -join ' ') build",
    "SCHACH_ENGINE_FAKE_MODEL=1 docker compose $($compose -join ' ') up -d --wait --wait-timeout 300",
    "cd tests/Schach.E2E && npm ci && npx playwright install --with-deps chromium",
    "cd tests/Schach.E2E && npx playwright test",
    "docker compose $($compose -join ' ') down -v"
)

if (-not $IchHabeDocker) {
    Write-Host 'E2E laeuft in GitHub Actions (.github/workflows/e2e.yml). Schritte:'
    $schritte | ForEach-Object { Write-Host "  $_" }
    exit 0
}

Push-Location $repo
try {
    New-Item -ItemType Directory -Force $mail | Out-Null
    if (-not $IsWindows) { & chmod 0777 $mail }
    $env:SCHACH_ENGINE_FAKE_MODEL = '1'
    & docker compose @compose build
    if ($LASTEXITCODE -ne 0) { throw 'docker compose build fehlgeschlagen' }
    & docker compose @compose up -d --wait --wait-timeout 300
    if ($LASTEXITCODE -ne 0) { throw 'docker compose up fehlgeschlagen' }

    Push-Location $e2e
    try {
        & npm ci
        & npx playwright install chromium
        & npx playwright test
        $rc = $LASTEXITCODE
    } finally {
        Pop-Location
    }
} finally {
    if ($Aufraeumen) { & docker compose @compose down -v }
    Pop-Location
}
exit $rc
