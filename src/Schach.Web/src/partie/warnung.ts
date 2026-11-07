import { Chess, SQUARES, type Color } from 'chess.js'
import type { Farbe } from '../api/types'
import { de } from '../i18n/de'
import { fuelle } from './text'

// Warnung vor ungedeckter Figur (SCHACH-SPEC 7.2): eigene S, L, T, D, die mindestens ein
// gegnerischer Stein angreift und keine eigene Figur deckt. Statisch, Fesselungen ignoriert.

const BETROFFEN = { n: 'Springer', b: 'Laeufer', r: 'Turm', q: 'Dame' } as const
type BetroffenTyp = keyof typeof BETROFFEN

export interface UngedeckteFigur {
  feld: string
  figur: (typeof BETROFFEN)[BetroffenTyp]
}

const farbeVon = (seite: Farbe): Color => (seite === 'Weiss' ? 'w' : 'b')

/** Ungedeckte, angegriffene eigene Figuren, nach Feld sortiert. Ungueltige FEN: keine Warnung. */
export function ungedeckteFiguren(fen: string, seite: Farbe): UngedeckteFigur[] {
  let brett: Chess
  try {
    brett = new Chess(fen)
  } catch {
    return []
  }
  const eigene = farbeVon(seite)
  const gegner: Color = eigene === 'w' ? 'b' : 'w'
  const funde: UngedeckteFigur[] = []
  for (const feld of SQUARES) {
    const stein = brett.get(feld)
    if (!stein || stein.color !== eigene || !(stein.type in BETROFFEN)) continue
    if (brett.attackers(feld, gegner).length > 0 && brett.attackers(feld, eigene).length === 0) {
      funde.push({ feld, figur: BETROFFEN[stein.type as BetroffenTyp] })
    }
  }
  return funde.sort((a, b) => a.feld.localeCompare(b.feld))
}

/** Zeile unter dem Brett zur ersten Warnung, z. B. „Dein Springer auf c3 ist angegriffen und ungedeckt.“ */
export function warnungsText(figur: UngedeckteFigur): string {
  return fuelle(de.warnung[figur.figur], { feld: figur.feld })
}
