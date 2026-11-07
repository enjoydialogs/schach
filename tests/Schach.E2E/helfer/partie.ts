import { expect, type Locator, type Page } from '@playwright/test'
import { zustand, type Zustand } from './api'

// Partie-Bausteine ueber die Oberflaeche (Texte aus src/Schach.Web/src/i18n/de.ts).

export type Bedenkzeit = 'Ohne Uhr' | '15+10' | '10+5' | '5+3' | '90 s je Zug'

/** `/neu` → Gegner → Sheet „Partie gegen <name>“ → Weiß, Bedenkzeit → „Partie starten“. Liefert die Partie-ID. */
export async function startePartieGegen(page: Page, name: string, bedenkzeit: Bedenkzeit = 'Ohne Uhr'): Promise<string> {
  await expect(page).toHaveURL(/\/neu$/)
  await page
    .locator('button.gegnerzeile', { has: page.locator('.zeile__titel', { hasText: new RegExp(`^${name}`) }) })
    .click()
  const sheet = page.getByRole('dialog', { name: `Partie gegen ${name}` })
  await expect(sheet).toBeVisible()
  // Weiß ausdruecklich: Vorgabe ist Zufall, und als Weiss beginnt der Spieler.
  await sheet.locator('label.segment', { hasText: 'Weiß' }).click()
  await expect(sheet.getByRole('radio', { name: 'Weiß' })).toBeChecked()
  await sheet.locator('label.radiozeile', { hasText: bedenkzeit }).click()
  await sheet.getByRole('button', { name: 'Partie starten' }).click()
  await page.waitForURL(/\/partie\/[0-9a-f-]{36}$/)
  const id = new URL(page.url()).pathname.split('/').pop()!
  await expect(page.getByTestId('brett')).toBeVisible()
  return id
}

/** Der Spieler ist am Zug und die Oberflaeche nimmt Eingaben an. */
export function eigenerZugAnzeige(page: Page): Locator {
  return page.locator('.partie__eigene [aria-label="am Zug"]')
}

export function feld(page: Page, name: string): Locator {
  return page.locator(`[data-square="${name}"]`)
}

/**
 * Ein erlaubter Zug aus der Serverliste, ohne Rochade und Umwandlung (die Antworten des Gegners
 * sind nicht fest, darum kein fest verdrahteter Zug).
 */
export function waehleZug(z: Zustand): { von: string; nach: string } {
  const kandidaten = z.erlaubteZuege.filter((uci) => uci.length === 4 && !/^e[18][cg][18]$/.test(uci))
  const uci = kandidaten[0]
  if (uci === undefined) throw new Error(`Kein einfacher Zug in ${z.erlaubteZuege.join(' ')}`)
  return { von: uci.slice(0, 2), nach: uci.slice(2, 4) }
}

/**
 * Spielt einen Zug per Tippen-Tippen (Feld antippen, Ziel antippen) und wartet, bis der Gegner
 * geantwortet hat und der Spieler wieder am Zug ist. Liefert den Zustand danach.
 */
export async function spieleZugTippenTippen(page: Page, id: string): Promise<Zustand> {
  await expect(eigenerZugAnzeige(page)).toBeVisible()
  const vorher = await zustand(page, id)
  const { von, nach } = waehleZug(vorher)

  const antwort = page.waitForResponse(
    (r) => r.request().method() === 'POST' && r.url().endsWith(`/api/v1/partien/${id}/zuege`),
  )
  await feld(page, von).click()
  await feld(page, nach).click()
  expect((await antwort).status(), `POST /zuege ${von}${nach}`).toBe(200)

  let danach = vorher
  await expect
    .poll(async () => {
      danach = await zustand(page, id)
      return danach.status === 'Beendet' || (danach.halbzug >= vorher.halbzug + 2 && danach.amZug)
    }, { message: `Gegnerantwort auf ${von}${nach}`, timeout: 30_000 })
    .toBe(true)
  if (danach.status === 'Offen') await expect(eigenerZugAnzeige(page)).toBeVisible()
  return danach
}

/**
 * „Aufgeben“ in der Knopfleiste, dann im Dialog bestaetigen. Liefert das Endblatt und den Zustand
 * aus der Antwort (eine Gast-Partie loescht der Server beim Beenden, SCHACH-SPEC 8.11).
 */
export async function gibAuf(page: Page): Promise<{ ende: Locator; zustand: Zustand }> {
  await page.locator('.partie__knoepfe').getByRole('button', { name: 'Aufgeben' }).click()
  const frage = page.getByRole('dialog', { name: 'Partie aufgeben?' })
  await expect(frage).toBeVisible()
  const antwort = page.waitForResponse((r) => r.request().method() === 'POST' && /\/api\/v1\/partien\/[^/]+\/aufgeben$/.test(r.url()))
  await frage.getByRole('button', { name: 'Aufgeben' }).click()
  const a = await antwort
  expect(a.status(), 'POST /aufgeben').toBe(200)
  const ende = page.getByRole('dialog', { name: 'Du hast verloren' })
  await expect(ende).toBeVisible()
  return { ende, zustand: (await a.json()) as Zustand }
}
