import { readFile } from 'node:fs/promises'
import { expect, test } from '@playwright/test'
import { zustand } from '../helfer/api'
import { meldeAnPerCode } from '../helfer/konto'
import { neueAdresse } from '../helfer/mail'
import { gibAuf, spieleZugTippenTippen, startePartieGegen } from '../helfer/partie'

// Strecke 5 (SCHACH-SPEC 15.2, 10.8): Nachspielen einer beendeten Partie plus PGN-Download.
test('Nachspielen und PGN herunterladen', async ({ page }) => {
  // Ohne Web Share faellt „PGN teilen“ auf den Download zurueck (components/datei.ts).
  await page.addInitScript(() => {
    Object.defineProperty(navigator, 'canShare', { value: undefined, configurable: true })
    Object.defineProperty(navigator, 'share', { value: undefined, configurable: true })
  })

  await meldeAnPerCode(page, neueAdresse('pgn'))
  await page.getByRole('link', { name: 'Neue Partie' }).click()
  const id = await startePartieGegen(page, 'Finn')
  await spieleZugTippenTippen(page, id)
  await spieleZugTippenTippen(page, id)
  const { ende } = await gibAuf(page)
  // Spieler-Partie ohne Tipp zaehlt: Elo-Zeile aus eloVorher/eloNachher des Servers (SCHACH-SPEC 10.3).
  await expect(ende.locator('.ende__elo')).toHaveText(/^Elo \d+ \(.+\)$/)

  await ende.getByRole('button', { name: 'Nachspielen' }).click()
  await page.waitForURL((url) => url.pathname === `/nachspielen/${id}`)
  await expect(page.getByRole('heading', { name: /^Gegen Finn · Niederlage/ })).toBeVisible()

  const zuege = page.getByRole('list', { name: 'Züge' }).getByRole('button')
  await expect(zuege).toHaveCount(4)
  // Nachspielen beginnt in der Grundstellung.
  const knoepfe = page.locator('.partie__knoepfe')
  await expect(knoepfe.getByRole('button', { name: 'Anfang' })).toBeDisabled()
  await knoepfe.getByRole('button', { name: 'Vor' }).click()
  await expect(zuege.first()).toHaveAttribute('aria-current', 'true')
  await knoepfe.getByRole('button', { name: 'Ende' }).click()
  await expect(knoepfe.getByRole('button', { name: 'Ende' })).toBeDisabled()
  await expect(zuege.last()).toHaveAttribute('aria-current', 'true')
  await knoepfe.getByRole('button', { name: 'Anfang' }).click()
  await expect(knoepfe.getByRole('button', { name: 'Anfang' })).toBeDisabled()

  const pgn = (await zustand(page, id)).pgn
  expect(pgn).not.toBeNull()

  await page.getByRole('button', { name: 'Menü' }).click()
  const download = page.waitForEvent('download')
  await page.getByRole('button', { name: 'PGN teilen' }).click()
  const datei = await download
  expect(datei.suggestedFilename()).toMatch(/^schach-finn-\d{4}-\d{2}-\d{2}\.pgn$/)
  const inhalt = await readFile(await datei.path(), 'utf8')
  expect(inhalt).toBe(pgn)
  expect(inhalt).toContain('[Result "0-1"]')

  // Partienliste gegen den echten Server (`GET /partien?limit`, SCHACH-SPEC 10.7): die eine
  // beendete Partie steht dort und fuehrt zurueck ins Nachspielen.
  const liste = page.waitForResponse(
    (r) => r.request().method() === 'GET' && new URL(r.url()).pathname === '/api/v1/partien',
  )
  await page.goto('/partien')
  expect((await liste).status()).toBe(200)
  const zeilen = page.locator('ul.liste a.partiezeile')
  await expect(zeilen).toHaveCount(1)
  await expect(zeilen.first()).toHaveAttribute('href', `/nachspielen/${id}`)
  await expect(zeilen.first()).toContainText('Finn')
  await expect(zeilen.first()).toContainText('Niederlage')
  await zeilen.first().click()
  await page.waitForURL((url) => url.pathname === `/nachspielen/${id}`)
})
