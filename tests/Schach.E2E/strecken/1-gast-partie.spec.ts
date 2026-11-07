import { expect, test } from '@playwright/test'
import { sitzung, zustand } from '../helfer/api'
import { gibAuf, spieleZugTippenTippen, startePartieGegen } from '../helfer/partie'

// Strecke 1 (SCHACH-SPEC 15.2): Gast-Partie. Start → Finn → 3 Zuege per Tippen-Tippen → Aufgeben →
// Endblatt.
test('Gast-Partie gegen Finn bis zum Endblatt', async ({ page }) => {
  await page.goto('/')
  await expect(page.getByText('Du spielst als Gast.', { exact: false })).toBeVisible()
  expect((await sitzung(page)).angemeldet).toBe(false)

  await page.getByRole('link', { name: 'Neue Partie' }).click()
  const id = await startePartieGegen(page, 'Finn')
  await expect(page.locator('.partie__eigene .pille')).toHaveText('Gast')
  await expect(page.locator('.partie__kopf')).toContainText('Finn')

  for (let i = 0; i < 3; i++) {
    const z = await spieleZugTippenTippen(page, id)
    expect(z.status, `Partie nach Zug ${i + 1}`).toBe('Offen')
  }
  expect((await zustand(page, id)).halbzug).toBe(6)

  const ende = await gibAuf(page)
  await expect(ende).toContainText('Aufgabe')
  await expect(ende).toContainText('Du hast die Partie aufgegeben.')
  // Gast: Hinweis aufs Konto statt Elo-Zeile, kein „Nachspielen“.
  await expect(ende.getByRole('link', { name: /Konto anlegen/ })).toBeVisible()
  await expect(ende.getByRole('button', { name: 'Nachspielen' })).toHaveCount(0)
  await expect(ende.getByRole('button', { name: 'Neue Partie' })).toBeVisible()

  const z = await zustand(page, id)
  expect(z.status).toBe('Beendet')
  expect(z.grund).toBe('Aufgabe')

  // Schliessbar, die Stellung bleibt sichtbar.
  await ende.getByRole('button', { name: 'Schließen' }).click()
  await expect(ende).toBeHidden()
  await expect(page.getByTestId('brett')).toBeVisible()
})
