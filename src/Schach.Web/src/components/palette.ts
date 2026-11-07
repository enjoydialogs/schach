import { useSyncExternalStore } from 'react'
import { paletteId } from './SpeicherSchluessel'

// Grundfarbe, vom Spieler gewaehlt (SCHACH-SPEC 10.3). Die Farben selbst stehen in
// styles/palette.css unter `:root[data-palette=…]`; hier nur die Werte, die ausserhalb von CSS
// gebraucht werden (theme-color, Tipp-Pfeil, Farbmuster der Auswahl). palette.test.ts prueft,
// dass sie mit palette.css uebereinstimmen.

export const PALETTEN = [
  { id: 'lila', grund: '#f4f2fb', brett: '#a898d8', akzent: '#1c715b' },
  { id: 'pink', grund: '#fcf1f6', brett: '#e09cba', akzent: '#1b6a7e' },
  { id: 'gruen', grund: '#eff7f2', brett: '#8ec6a7', akzent: '#6a45b0' },
  { id: 'blau', grund: '#f0f4fb', brett: '#94afde', akzent: '#a3336b' },
] as const

export type Palette = (typeof PALETTEN)[number]
export type PaletteId = Palette['id']

export const STANDARD_PALETTE: PaletteId = 'lila'

function finde(id: string | null): Palette {
  return PALETTEN.find((p) => p.id === id) ?? PALETTEN[0]
}

/** Gespeicherte Wahl; fehlend oder unbekannt gilt als Standard. */
export function lesePalette(): PaletteId {
  return finde(paletteId.lies()).id
}

let aktuell: PaletteId = lesePalette()
const hoerer = new Set<() => void>()

/** Setzt `data-palette` am <html> und die theme-color der Browserleiste. */
export function wendePaletteAn(id: PaletteId = aktuell): void {
  const p = finde(id)
  document.documentElement.dataset.palette = p.id
  document.querySelector('meta[name="theme-color"]')?.setAttribute('content', p.grund)
}

/** Waehlt eine Grundfarbe: speichern, anwenden, Abonnenten benachrichtigen. */
export function setzePalette(id: PaletteId): void {
  aktuell = finde(id).id
  paletteId.setze(aktuell)
  wendePaletteAn(aktuell)
  for (const h of hoerer) h()
}

/** Nur fuer Tests: liest den Speicher neu. */
export function ladePaletteNeu(): void {
  aktuell = lesePalette()
  wendePaletteAn(aktuell)
  for (const h of hoerer) h()
}

function abonniere(h: () => void): () => void {
  hoerer.add(h)
  return () => hoerer.delete(h)
}

/** Aktuelle Grundfarbe als React-State. */
export function usePalette(): Palette {
  return finde(useSyncExternalStore(abonniere, () => aktuell))
}
