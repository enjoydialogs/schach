import { expect, test } from '@playwright/test'
import { apiGet, sitzung, zustand, type KontoExport } from '../helfer/api'
import { meldeAnPerCode } from '../helfer/konto'
import { neueAdresse } from '../helfer/mail'
import { gibAuf, startePartieGegen } from '../helfer/partie'

// Strecke 6 (SCHACH-SPEC 15.2, 9.6, 15.4 Punkt 9): Konto loeschen → `/sitzung` abgemeldet, Partien weg.
test('Konto löschen: abgemeldet, Partien weg', async ({ page }) => {
  const adresse = neueAdresse('loeschen')
  await meldeAnPerCode(page, adresse)

  // Eine beendete, gewertete Partie: senkt die Elo unter 800 und steht im Export.
  await page.getByRole('link', { name: 'Neue Partie' }).click()
  const id = await startePartieGegen(page, 'Finn')
  await gibAuf(page)
  expect((await zustand(page, id)).status).toBe('Beendet')

  const vorher = await apiGet<KontoExport>(page, '/konto/export')
  expect(vorher.status).toBe(200)
  expect(vorher.body?.partien.map((p) => p.id)).toContain(id)
  const kontoId = vorher.body!.konto.id
  expect(vorher.body!.elo).toBeLessThan(800)

  await page.goto('/profil')
  await page.getByRole('button', { name: 'Konto löschen' }).click()
  const frage = page.getByRole('dialog', { name: 'Konto endgültig löschen?' })
  await expect(frage).toBeVisible()
  const loeschen = page.waitForResponse((r) => r.request().method() === 'DELETE' && r.url().endsWith('/api/v1/konto'))
  await frage.getByRole('button', { name: 'Endgültig löschen' }).click()
  expect((await loeschen).status()).toBe(204)

  await page.waitForURL((url) => url.pathname === '/')
  await expect(page.getByRole('status').filter({ hasText: 'Dein Konto ist gelöscht.' })).toBeVisible()
  await expect(page.locator('.startkopf a.chip')).toHaveText('Anmelden')

  // `/sitzung` sagt abgemeldet, die Partie gibt es nicht mehr, der Export verlangt eine Anmeldung.
  const s = await sitzung(page)
  expect(s.angemeldet).toBe(false)
  expect(s.spieler).toBeNull()
  expect((await apiGet(page, `/partien/${id}`)).status).toBe(404)
  expect((await apiGet(page, '/konto/export')).status).toBe(401)

  // Dieselbe Adresse ergibt ein frisches Konto: neue ID, Elo 800, keine Partien.
  await meldeAnPerCode(page, adresse, 2)
  const neu = await apiGet<KontoExport>(page, '/konto/export')
  expect(neu.status).toBe(200)
  expect(neu.body!.konto.id).not.toBe(kontoId)
  expect(neu.body!.elo).toBe(800)
  expect(neu.body!.partien).toEqual([])
})
