import type { Page } from '@playwright/test'

// API-Blick aus der Seite heraus: `fetch` im Browser nimmt dessen Cookies (Sitzung, Gast) mit,
// genau wie die App. Nur lesend; schreiben tut der Test ueber die Oberflaeche.

export interface Antwort<T> {
  status: number
  body: T | null
}

/** Ausschnitt aus `ZustandDto` (src/Schach.Web/src/api/types.ts), soweit die Strecken ihn brauchen. */
export interface Zustand {
  id: string
  status: 'Offen' | 'Beendet'
  spielerFarbe: 'Weiss' | 'Schwarz'
  bedenkzeit: string
  halbzug: number
  fen: string
  erlaubteZuege: string[]
  amZug: boolean
  gegnerZugAusstehend: boolean
  zaehltFuerElo: boolean
  remisAngebotMoeglichAbZug: number
  ergebnis: string | null
  grund: string | null
  pgn: string | null
}

export interface Sitzung {
  angemeldet: boolean
  spieler: { email: string; elo: number } | null
}

export interface KontoExport {
  konto: { id: string; email: string }
  elo: number
  partien: { id: string }[]
}

export async function apiGet<T>(page: Page, pfad: string): Promise<Antwort<T>> {
  return (await page.evaluate(async (p) => {
    const r = await fetch(`/api/v1${p}`, { credentials: 'same-origin', headers: { Accept: 'application/json' } })
    const text = await r.text()
    let body: unknown = null
    try {
      body = text.length > 0 ? JSON.parse(text) : null
    } catch {
      body = null
    }
    return { status: r.status, body }
  }, pfad)) as Antwort<T>
}

export async function zustand(page: Page, id: string): Promise<Zustand> {
  const a = await apiGet<Zustand>(page, `/partien/${id}`)
  if (a.status !== 200 || a.body === null) throw new Error(`GET /partien/${id} lieferte ${a.status}`)
  return a.body
}

export async function sitzung(page: Page): Promise<Sitzung> {
  const a = await apiGet<Sitzung>(page, '/sitzung')
  if (a.status !== 200 || a.body === null) throw new Error(`GET /sitzung lieferte ${a.status}`)
  return a.body
}
