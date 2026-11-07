import type { CSSProperties } from 'react'

// Overlays nach SCHACH-SPEC 10.3. Farben kommen aus palette.css; react-chessboard bekommt sie als
// `squareStyles`. Farbflaechen sind `linear-gradient`-Ebenen, damit die Feldfarbe darunter bleibt.

export interface Ziel {
  feld: string
  /** Schlagfeld (Ring) statt leerem Feld (Punkt). */
  schlag: boolean
}

export interface Markierungen {
  letzterZug?: { von: string; nach: string } | null
  auswahl?: string | null
  ziele?: readonly Ziel[]
  schach?: string | null
  warnungen?: readonly string[]
  tippFeld?: string | null
}

const flaeche = (farbe: string) => `linear-gradient(${farbe}, ${farbe})`

const PUNKT = 'radial-gradient(circle, var(--overlay-ziel) 19%, transparent 21%)'
const RING = 'radial-gradient(circle, transparent 79%, var(--overlay-ziel) 80%)'
const SCHACH =
  'radial-gradient(ellipse at center, rgba(255, 0, 0, 1) 0%, rgba(231, 0, 0, 1) 25%, rgba(169, 0, 0, 0) 89%, rgba(158, 0, 0, 0) 100%)'

/** Stile je Feld. Oberste Ebene zuerst: Ziel, Schach, dann Farbflaechen. */
export function feldStile(m: Markierungen): Record<string, CSSProperties> {
  const ebenen = new Map<string, string[]>()
  const schatten = new Map<string, string>()
  const lege = (feld: string | null | undefined, ebene: string) => {
    if (!feld) return
    const liste = ebenen.get(feld) ?? []
    liste.push(ebene)
    ebenen.set(feld, liste)
  }

  for (const ziel of m.ziele ?? []) lege(ziel.feld, ziel.schlag ? RING : PUNKT)
  lege(m.schach, SCHACH)
  lege(m.auswahl, flaeche('var(--overlay-auswahl)'))
  for (const feld of m.warnungen ?? []) lege(feld, flaeche('var(--overlay-warnung)'))
  if (m.letzterZug) {
    lege(m.letzterZug.von, flaeche('var(--overlay-letzter-zug)'))
    lege(m.letzterZug.nach, flaeche('var(--overlay-letzter-zug)'))
  }
  if (m.tippFeld) schatten.set(m.tippFeld, 'var(--tipp-ring)')

  const stile: Record<string, CSSProperties> = {}
  for (const [feld, liste] of ebenen) stile[feld] = { backgroundImage: liste.join(', ') }
  for (const [feld, wert] of schatten) stile[feld] = { ...stile[feld], boxShadow: wert }
  return stile
}

export type FigurFarbe = 'w' | 'b'

/** Figur auf einem Feld aus dem FEN-Brettfeld, z. B. `{ farbe: 'w', typ: 'n' }`. */
export function figurAuf(fen: string, feld: string): { farbe: FigurFarbe; typ: string } | null {
  const reihen = fen.split(' ')[0]?.split('/') ?? []
  const spalte = feld.charCodeAt(0) - 97
  const reihe = reihen[8 - Number(feld[1])]
  if (reihe === undefined || spalte < 0 || spalte > 7) return null
  let x = 0
  for (const zeichen of reihe) {
    if (/\d/.test(zeichen)) {
      x += Number(zeichen)
      continue
    }
    if (x === spalte) {
      return { farbe: zeichen === zeichen.toUpperCase() ? 'w' : 'b', typ: zeichen.toLowerCase() }
    }
    x += 1
  }
  return null
}

/** Ziele mit Punkt oder Ring. En passant zaehlt als Schlag. */
export function zieleMitArt(fen: string, von: string, zielfelder: readonly string[]): Ziel[] {
  const ep = fen.split(' ')[3]
  const bauer = figurAuf(fen, von)?.typ === 'p'
  return zielfelder.map((feld) => ({
    feld,
    schlag: figurAuf(fen, feld) !== null || (bauer && feld === ep),
  }))
}
