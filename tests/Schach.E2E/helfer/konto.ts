import { expect, type Page } from '@playwright/test'
import { sitzung } from './api'
import { leseCode } from './mail'

/**
 * Anmeldung per Code (SCHACH-SPEC 9.5, 10.10): Adresse eingeben, „Link senden“, Code aus der Mail
 * eintippen, „Anmelden“. Endet auf der Startseite mit Elo-Chip. `anzahl`: die wievielte Mail an
 * diese Adresse (nach einem Konto-Loeschen zaehlt die alte mit).
 */
export async function meldeAnPerCode(page: Page, adresse: string, anzahl = 1): Promise<void> {
  await page.goto('/anmelden')
  await page.getByLabel('E-Mail-Adresse').fill(adresse)
  await page.getByRole('button', { name: 'Link senden' }).click()
  await expect(page.getByText('Mail geschickt.', { exact: false })).toBeVisible()

  const code = await leseCode(adresse, anzahl)
  await page.getByLabel('Code aus der Mail').fill(code)
  await page.getByRole('button', { name: 'Anmelden', exact: true }).click()

  await page.waitForURL((url) => url.pathname === '/')
  await expect(page.locator('.startkopf a.chip', { hasText: /^Elo \d+$/ })).toBeVisible()
  expect((await sitzung(page)).angemeldet).toBe(true)
}
