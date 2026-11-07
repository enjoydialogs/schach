import { expect, test } from '@playwright/test'
import { zustand } from '../helfer/api'
import { meldeAnPerCode } from '../helfer/konto'
import { neueAdresse } from '../helfer/mail'
import { eigenerZugAnzeige, spieleZugTippenTippen, startePartieGegen } from '../helfer/partie'

// Strecke 3 (SCHACH-SPEC 15.2, 10.3 Punkt 8): Spieler-Partie ohne Uhr → Tipp → Pille „Zählt nicht
// für die Elo“ → Zurücknehmen.
test('Spieler-Partie mit Tipp und Zurücknehmen', async ({ page }) => {
  await meldeAnPerCode(page, neueAdresse('tipp'))
  await page.getByRole('link', { name: 'Neue Partie' }).click()
  const id = await startePartieGegen(page, 'Finn', 'Ohne Uhr')
  await expect(page.locator('.partie__eigene .pille')).toHaveCount(0)

  await spieleZugTippenTippen(page, id)
  const leiste = page.locator('.partie__knoepfe')

  // Erster Tipp fragt einmal nach (Partie zaehlt danach nicht mehr).
  const tippAntwort = page.waitForResponse((r) => r.request().method() === 'POST' && r.url().endsWith(`/partien/${id}/tipp`))
  await leiste.getByRole('button', { name: 'Tipp' }).click()
  const frage = page.getByRole('dialog', { name: 'Partie zählt dann nicht für die Elo' })
  await expect(frage).toBeVisible()
  await frage.getByRole('button', { name: 'Tipp zeigen' }).click()
  expect((await tippAntwort).status()).toBe(200)

  await expect(page.getByTestId('statuszeile')).toContainText(/Zieh mit (dem|der) /)
  await expect(page.locator('.partie__eigene .pille')).toHaveText('Zählt nicht für die Elo')
  expect((await zustand(page, id)).zaehltFuerElo).toBe(false)

  // Zuruecknehmen fragt nicht mehr: die Partie zaehlt schon nicht.
  const vorher = await zustand(page, id)
  const zurueck = page.waitForResponse(
    (r) => r.request().method() === 'POST' && r.url().endsWith(`/partien/${id}/zuruecknehmen`),
  )
  await leiste.getByRole('button', { name: 'Zurücknehmen' }).click()
  await expect(page.getByRole('dialog', { name: 'Partie zählt dann nicht für die Elo' })).toHaveCount(0)
  expect((await zurueck).status()).toBe(200)
  await expect.poll(async () => (await zustand(page, id)).halbzug).toBe(vorher.halbzug - 2)
  await expect(eigenerZugAnzeige(page)).toBeVisible()
  await expect(page.locator('.partie__eigene .pille')).toHaveText('Zählt nicht für die Elo')

  // Remis-Angebot gegen den echten Server (SCHACH-SPEC 3.5): vor Zug 20 lehnt jeder Gegner ab, ohne
  // Engine. Die Ablehnung sperrt weitere Angebote fuer 5 Zuege; der Knopf zeigt das sofort.
  const remis = page.waitForResponse(
    (r) => r.request().method() === 'POST' && r.url().endsWith(`/partien/${id}/remis-angebot`),
  )
  await leiste.getByRole('button', { name: 'Remis anbieten' }).click()
  expect((await remis).status()).toBe(200)
  await expect(leiste.getByRole('button', { name: 'Remis anbieten' })).toBeDisabled()
  const nachRemis = await zustand(page, id)
  expect(nachRemis.status).toBe('Offen')
  expect(nachRemis.remisAngebotMoeglichAbZug).toBe(6)
})
