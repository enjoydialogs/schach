import { Chess } from 'chess.js'
import type { SpruchDto, ZugDto, ZustandDto } from '../api/types'
import { isoDatum } from '../components/datei'
import { de } from '../i18n/de'
import { fuelle } from '../partie/text'

// Logik von Nachspielen (SCHACH-SPEC 10.8): chess.js spielt die Zugfolge nach. Anzeige in
// deutscher Notation (S, L, T, D, K); das PGN bleibt in Standardnotation.

const FIGUR_DE: Record<string, string> = { N: 'S', B: 'L', R: 'T', Q: 'D', K: 'K' }

/** SAN in deutscher Notation: „Nf3“ → „Sf3“, „e8=Q+“ → „e8=D+“. */
export function deutscheSan(san: string): string {
  return san.replace(/^[NBRQK]/, (f) => FIGUR_DE[f]!).replace(/=([NBRQ])/, (_, f: string) => `=${FIGUR_DE[f]!}`)
}

/** „12. Sf3“ fuer Weiss, „12. … Sf6“ fuer Schwarz. */
export function zugText(zug: ZugDto): string {
  const nr = Math.ceil(zug.halbzug / 2)
  const vorlage = zug.seite === 'Weiss' ? de.nachspielen.zugWeiss : de.nachspielen.zugSchwarz
  return fuelle(vorlage, { nr, san: deutscheSan(zug.san) })
}

export interface Stellung {
  fen: string
  letzterZug: { von: string; nach: string } | null
}

/** Stellungen nach 0..n Halbzuegen. Ein unpassender Zug beendet die Liste (nie ein Absturz). */
export function stellungen(zugfolge: readonly ZugDto[]): Stellung[] {
  const brett = new Chess()
  const liste: Stellung[] = [{ fen: brett.fen(), letzterZug: null }]
  for (const zug of zugfolge) {
    try {
      brett.move({ from: zug.uci.slice(0, 2), to: zug.uci.slice(2, 4), promotion: zug.uci[4] })
    } catch {
      break
    }
    liste.push({ fen: brett.fen(), letzterZug: { von: zug.uci.slice(0, 2), nach: zug.uci.slice(2, 4) } })
  }
  return liste
}

/** Spruch genau an diesem Halbzug; bei mehreren der letzte (10.8: „Spruch an seinem Halbzug“). */
export function spruchAn(sprueche: readonly SpruchDto[], halbzug: number): SpruchDto | null {
  let gefunden: SpruchDto | null = null
  for (const s of sprueche) if (s.nachHalbzug === halbzug) gefunden = s
  return gefunden
}

/**
 * `schach-oscar-2026-11-07.pgn`. Der Zustand traegt kein `beendetAm`; als Datum dient der letzte
 * Zug, ohne Zuege der heutige Tag.
 */
export function pgnDateiname(z: ZustandDto): string {
  const zeitpunkt = z.zugfolge.at(-1)?.gespieltAm ?? Date.now()
  return `schach-${z.gegner.key}-${isoDatum(zeitpunkt)}.pgn`
}
