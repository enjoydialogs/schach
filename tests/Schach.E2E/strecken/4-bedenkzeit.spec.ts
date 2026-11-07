import { expect, test } from '@playwright/test'
import { meldeAnPerCode } from '../helfer/konto'
import { neueAdresse } from '../helfer/mail'
import { eigenerZugAnzeige, spieleZugTippenTippen, startePartieGegen } from '../helfer/partie'

// Strecke 4 (SCHACH-SPEC 15.2, 10.3 Punkt 6): Partie 5+3 ohne Zurücknehmen und Tipp, Uhren laufen.
test('Partie 5+3: kein Zurücknehmen, kein Tipp, Uhren laufen', async ({ page }) => {
  test.fixme(
    true,
    '#31 (Zeitablauf lazy, Uhr im Partie-Kern) und #38 (Web gegen echte API: Uhr, Polling) fehlen auf main.',
  )

  await meldeAnPerCode(page, neueAdresse('uhr'))
  await page.getByRole('link', { name: 'Neue Partie' }).click()
  const id = await startePartieGegen(page, 'Finn', '5+3')

  const leiste = page.locator('.partie__knoepfe')
  await expect(leiste.getByRole('button', { name: 'Aufgeben' })).toBeVisible()
  await expect(leiste.getByRole('button', { name: 'Zurücknehmen' })).toHaveCount(0)
  await expect(leiste.getByRole('button', { name: 'Tipp' })).toHaveCount(0)

  const eigeneUhr = page.getByRole('timer', { name: 'Deine Uhr' })
  const gegnerUhr = page.getByRole('timer', { name: 'Uhr von Finn' })
  await expect(eigeneUhr).toBeVisible()
  await expect(gegnerUhr).toBeVisible()

  await spieleZugTippenTippen(page, id)
  await expect(eigenerZugAnzeige(page)).toBeVisible()

  // Die eigene Uhr laeuft, solange der Spieler am Zug ist.
  const vorher = await eigeneUhr.innerText()
  await expect.poll(() => eigeneUhr.innerText(), { timeout: 5_000 }).not.toBe(vorher)
  await expect(eigeneUhr).toHaveClass(/uhr--aktiv/)
})
