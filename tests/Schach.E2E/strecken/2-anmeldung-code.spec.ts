import { expect, test } from '@playwright/test'
import { sitzung } from '../helfer/api'
import { meldeAnPerCode } from '../helfer/konto'
import { neueAdresse } from '../helfer/mail'

// Strecke 2 (SCHACH-SPEC 15.2): Anmeldung per Code. Neue Adresse = neues Konto mit Elo 800.
test('Anmeldung per Code, Profil zeigt Elo 800', async ({ page }) => {
  const adresse = neueAdresse('anmeldung')
  await meldeAnPerCode(page, adresse)

  await expect(page.locator('.startkopf a.chip')).toHaveText('Elo 800')
  const s = await sitzung(page)
  expect(s.spieler?.email).toBe(adresse)
  expect(s.spieler?.elo).toBe(800)

  await page.locator('.startkopf a.chip').click()
  await page.waitForURL((url) => url.pathname === '/profil')
  await expect(page.getByTestId('profil-elo')).toHaveText('800')
  await expect(page.locator('.profil__email')).toHaveText(adresse)
  await expect(page.getByText(/^Spieler seit /)).toBeVisible()
})
