import type { TippDto } from '../api/types'
import { de } from '../i18n/de'
import { fuelle } from './text'

// Tipp in zwei Stufen (SCHACH-SPEC 7.3). Der Client haelt das Ergebnis je `halbzug`; das zweite
// Tippen ruft den Server nicht.

export interface TippStand {
  halbzug: number
  stufe: 1 | 2
  tipp: TippDto
}

/** Stand fuer den aktuellen Halbzug; ein alter Tipp gilt nicht mehr. */
export function aktuellerTipp(stand: TippStand | null, halbzug: number): TippStand | null {
  return stand !== null && stand.halbzug === halbzug ? stand : null
}

/** Was das naechste Tippen auf „Tipp“ tut. */
export function naechsterTippSchritt(stand: TippStand | null, halbzug: number): 'abrufen' | 'pfeil' | 'fertig' {
  const aktuell = aktuellerTipp(stand, halbzug)
  if (aktuell === null) return 'abrufen'
  return aktuell.stufe === 1 ? 'pfeil' : 'fertig'
}

/** Zweite Stufe ohne Serveraufruf. */
export function zeigePfeil(stand: TippStand): TippStand {
  return { ...stand, stufe: 2 }
}

/** Statuszeile: „Zieh mit dem Springer“, dann „Springer c3 nach e5“. */
export function tippText(stand: TippStand): string {
  const { tipp } = stand
  if (stand.stufe === 1) return de.tipp.stufe1[tipp.figur]
  return fuelle(de.tipp.stufe2, { figur: de.tipp.figur[tipp.figur], von: tipp.von, nach: tipp.nach })
}

/** Markierungen auf dem Brett: Ring auf der Figur, ab Stufe 2 zusaetzlich der Pfeil. */
export function tippMarkierung(stand: TippStand | null): {
  feld: string | null
  pfeil: { von: string; nach: string } | null
} {
  if (stand === null) return { feld: null, pfeil: null }
  return {
    feld: stand.tipp.von,
    pfeil: stand.stufe === 2 ? { von: stand.tipp.von, nach: stand.tipp.nach } : null,
  }
}
