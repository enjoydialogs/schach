import { expect, test, type Response } from '@playwright/test'
import { BASIS_URL } from '../helfer/basis'

// Strecke 7 (SCHACH-SPEC 15.2, 12.3, 15.4 Punkt 9): Impressum und Datenschutz erreichbar, null
// Fremd-Hosts im Netzwerk, CSP-Header.

const EIGENER_HOST = new URL(BASIS_URL).host

function pruefeHeader(antwort: Response | null, pfad: string): void {
  expect(antwort, pfad).not.toBeNull()
  expect(antwort!.status(), pfad).toBe(200)
  const h = antwort!.headers()
  const csp = h['content-security-policy'] ?? ''
  expect(csp, `CSP auf ${pfad}`).toContain("default-src 'self'")
  expect(csp, `CSP auf ${pfad}`).toContain("connect-src 'self'")
  expect(csp, `CSP auf ${pfad}`).toContain("frame-ancestors 'none'")
  expect(csp, `CSP auf ${pfad}`).toContain("object-src 'none'")
  // Keine Inline-Skripte erlaubt (SecurityHeaders.cs).
  const scriptSrc = csp.split(';').map((d) => d.trim()).find((d) => d.startsWith('script-src'))
  expect(scriptSrc, `script-src auf ${pfad}`).toBe("script-src 'self'")
  expect(h['x-content-type-options'], pfad).toBe('nosniff')
  expect(h['referrer-policy'], pfad).toBe('no-referrer')
}

test('Rechtstexte erreichbar, keine Fremd-Hosts, CSP gesetzt', async ({ page, context }) => {
  // Kontext statt Seite: so zaehlen auch Anfragen des Service Workers mit.
  const fremde: string[] = []
  context.on('request', (r) => {
    const url = new URL(r.url())
    if ((url.protocol === 'http:' || url.protocol === 'https:') && url.host !== EIGENER_HOST) fremde.push(r.url())
  })

  pruefeHeader(await page.goto('/'), '/')
  await page.waitForLoadState('networkidle')

  // Ueber die Fusszeile (Weg des Nutzers).
  await page.locator('footer.fusszeile').getByRole('link', { name: 'Impressum' }).click()
  await page.waitForURL((url) => url.pathname === '/impressum')
  await expect(page.getByRole('heading', { level: 1, name: 'Impressum' })).toBeVisible()

  await page.locator('footer.fusszeile').getByRole('link', { name: 'Datenschutz' }).click()
  await page.waitForURL((url) => url.pathname === '/datenschutz')
  await expect(page.getByRole('heading', { level: 1, name: 'Datenschutzerklärung' })).toBeVisible()
  await expect(page.locator('.recht__stand')).toHaveText(/^Stand: /)

  // Direkt aufgerufen (Deep Link, SPA-Rueckfall) mit denselben Headern.
  pruefeHeader(await page.goto('/impressum'), '/impressum')
  await expect(page.getByRole('heading', { level: 1, name: 'Impressum' })).toBeVisible()
  pruefeHeader(await page.goto('/datenschutz'), '/datenschutz')
  await expect(page.getByRole('heading', { level: 1, name: 'Datenschutzerklärung' })).toBeVisible()

  // Neue Partie (Gegnerliste) laedt Avatare und Daten ebenfalls nur vom eigenen Host.
  await page.goto('/neu')
  await expect(page.locator('button.gegnerzeile')).toHaveCount(8)
  await page.waitForLoadState('networkidle')

  expect(fremde, 'Anfragen an Fremd-Hosts').toEqual([])
})
