import { defineConfig, devices } from '@playwright/test'
import { BASIS_URL } from './helfer/basis'

// E2E-Strecken (SCHACH-SPEC 15.2): Chromium, Pixel-7-Profil, ein Worker, gegen
// `docker compose --profile full` mit tests/Schach.E2E/compose.e2e.yml. Laeuft in
// .github/workflows/e2e.yml; Ablauf zum Nachlesen in scripts/run-e2e.ps1.

export default defineConfig({
  testDir: './strecken',
  fullyParallel: false,
  workers: 1,
  forbidOnly: !!process.env.CI,
  retries: 0,
  timeout: 90_000,
  expect: { timeout: 15_000 },
  reporter: [['list'], ['html', { open: 'never' }]],
  use: {
    baseURL: BASIS_URL,
    locale: 'de-DE',
    timezoneId: 'Europe/Berlin',
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
    video: 'retain-on-failure',
  },
  projects: [
    {
      name: 'pixel-7',
      use: { ...devices['Pixel 7'] },
    },
  ],
})
