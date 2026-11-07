import { Chess } from 'chess.js'
import type { SpruchDto, ZugDto, ZustandDto } from '../api/types'

// Was das Brett zeigt (SCHACH-SPEC 8.1, 8.6). `Zustand.fen` enthaelt den Gegnerzug schon; der
// Client zeigt ihn erst ab `gegnerZug.sichtbarAb`. Bis dahin: Stellung davor, Eingabe aus.

export interface Anzeige {
  fen: string
  letzterZug: { von: string; nach: string } | null
  /** Sichtbare Halbzuege (fuer Sprueche und Toene). */
  halbzug: number
  /** Gegnerzug noch nicht sichtbar oder auf dem Server ausstehend. */
  gegnerDenkt: boolean
}

const vonNach = (zug: ZugDto | undefined) => (zug ? { von: zug.uci.slice(0, 2), nach: zug.uci.slice(2, 4) } : null)

/** Stellung nach den gegebenen Zuegen ab Grundstellung, `null` wenn einer nicht passt. */
export function stellungNach(zuege: readonly ZugDto[]): string | null {
  const brett = new Chess()
  try {
    for (const zug of zuege) {
      brett.move({ from: zug.uci.slice(0, 2), to: zug.uci.slice(2, 4), promotion: zug.uci[4] })
    }
  } catch {
    return null
  }
  return brett.fen()
}

/**
 * Sichtbare Stellung zum Zeitpunkt `jetztServerMs`. `fenVorGegnerzug` ist die Stellung nach dem
 * eigenen (optimistischen) Zug; ohne sie spielt der Client die Zugfolge ohne den Gegnerzug nach.
 */
export function sichtbareStellung(z: ZustandDto, jetztServerMs: number, fenVorGegnerzug?: string | null): Anzeige {
  const gegnerZug = z.gegnerZug
  if (gegnerZug !== null && Date.parse(gegnerZug.sichtbarAb) > jetztServerMs) {
    const davor = z.zugfolge.filter((zug) => zug.halbzug < gegnerZug.halbzug)
    const fen = fenVorGegnerzug ?? stellungNach(davor)
    if (fen !== null) {
      return { fen, letzterZug: vonNach(davor.at(-1)), halbzug: davor.length, gegnerDenkt: true }
    }
  }
  return {
    fen: z.fen,
    letzterZug: vonNach(z.zugfolge.at(-1)),
    halbzug: z.zugfolge.length,
    gegnerDenkt: z.status === 'Offen' && z.gegnerZugAusstehend,
  }
}

/** Letzter Spruch bis zum sichtbaren Halbzug. */
export function sichtbarerSpruch(sprueche: readonly SpruchDto[], halbzug: number): SpruchDto | null {
  let letzter: SpruchDto | null = null
  for (const spruch of sprueche) {
    if (spruch.nachHalbzug <= halbzug) letzter = spruch
  }
  return letzter
}

/** Feld des Koenigs der Seite am Zug, wenn sie im Schach steht. */
export function schachFeld(fen: string): string | null {
  try {
    const brett = new Chess(fen)
    if (!brett.inCheck()) return null
    return brett.findPiece({ type: 'k', color: brett.turn() })[0] ?? null
  } catch {
    return null
  }
}

/** Stellung nach einem UCI-Zug, fuer die optimistische Anzeige. `null`, wenn chess.js ihn ablehnt. */
export function nachZug(fen: string, uci: string): { fen: string; schlag: boolean } | null {
  try {
    const brett = new Chess(fen)
    const zug = brett.move({ from: uci.slice(0, 2), to: uci.slice(2, 4), promotion: uci[4] })
    return { fen: brett.fen(), schlag: zug.isCapture() || zug.isEnPassant() }
  } catch {
    return null
  }
}
