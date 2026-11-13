import { Chess } from 'chess.js'
import type { Figur } from '../api/types'
import type { Uebung } from './katalog'

// Ablauf einer Uebung als reine Funktionen; der Bildschirm steuert nur die Verzoegerungen.
// Phasen: `gegner` (Vorzug oder Antwort steht aus), `amZug`, `falsch` (anderer Zug wird kurz
// gezeigt, dann zurueckgenommen), `fertig`.

export type Phase = 'gegner' | 'amZug' | 'falsch' | 'fertig'

export interface Stand {
  fen: string
  /** Index des aktuellen Schritts; nach dem letzten gleich `schritte.length`. */
  schritt: number
  phase: Phase
  /** Zug des Gegners, der in Phase `gegner` als naechstes kommt. */
  ausstehend: string | null
  letzterZug: { von: string; nach: string } | null
  /** Was der Gegner gerade sagt. `null`: allgemeiner Satz nach einem anderen Zug. */
  text: string | null
  /** Tipp-Stufe fuer den aktuellen Schritt: 0 aus, 1 Figur, 2 Pfeil. */
  tipp: 0 | 1 | 2
  /** Stellung vor einem anderen Zug, fuer die Ruecknahme in Phase `falsch`. */
  vorher: { fen: string; letzterZug: { von: string; nach: string } | null } | null
}

const vonNach = (uci: string) => ({ von: uci.slice(0, 2), nach: uci.slice(2, 4) })

/** Stellung nach einem UCI-Zug; `null`, wenn chess.js ihn ablehnt. */
export function ziehe(fen: string, uci: string): string | null {
  try {
    const brett = new Chess(fen)
    brett.move({ from: uci.slice(0, 2), to: uci.slice(2, 4), promotion: uci[4] })
    return brett.fen()
  } catch {
    return null
  }
}

/** Alle erlaubten Zuege als UCI, Umwandlungen mit Figur (`e7e8q`). */
export function erlaubteZuege(fen: string): string[] {
  try {
    return new Chess(fen).moves({ verbose: true }).map((z) => z.from + z.to + (z.promotion ?? ''))
  } catch {
    return []
  }
}

export function starte(uebung: Uebung): Stand {
  return {
    fen: uebung.fen,
    schritt: 0,
    phase: uebung.vorzug !== undefined ? 'gegner' : 'amZug',
    ausstehend: uebung.vorzug ?? null,
    letzterZug: null,
    text: uebung.einleitung,
    tipp: 0,
    vorher: null,
  }
}

/** Der ausstehende Zug des Gegners wird gespielt. */
export function gegnerZieht(stand: Stand, uebung: Uebung): Stand {
  if (stand.phase !== 'gegner' || stand.ausstehend === null) return stand
  const fen = ziehe(stand.fen, stand.ausstehend)
  if (fen === null) return stand
  return {
    ...stand,
    fen,
    letzterZug: vonNach(stand.ausstehend),
    ausstehend: null,
    phase: stand.schritt >= uebung.schritte.length ? 'fertig' : 'amZug',
  }
}

/** Zug des Spielers: richtig geht es weiter, ein anderer erlaubter Zug wird kurz gezeigt. */
export function spielerZieht(stand: Stand, uebung: Uebung, uci: string): Stand {
  const schritt = uebung.schritte[stand.schritt]
  if (stand.phase !== 'amZug' || schritt === undefined) return stand
  const fen = ziehe(stand.fen, uci)
  if (fen === null) return stand
  if (!schritt.zuege.includes(uci)) {
    return {
      ...stand,
      fen,
      letzterZug: vonNach(uci),
      phase: 'falsch',
      text: schritt.hinweis ?? null,
      vorher: { fen: stand.fen, letzterZug: stand.letzterZug },
    }
  }
  const naechster = stand.schritt + 1
  const ende = naechster >= uebung.schritte.length
  return {
    ...stand,
    fen,
    letzterZug: vonNach(uci),
    schritt: naechster,
    phase: schritt.antwort !== undefined ? 'gegner' : ende ? 'fertig' : 'amZug',
    ausstehend: schritt.antwort ?? null,
    text: schritt.erklaerung,
    tipp: 0,
    vorher: null,
  }
}

/** Nimmt den anderen Zug zurueck; der Hinweis bleibt stehen. */
export function nimmZurueck(stand: Stand): Stand {
  if (stand.phase !== 'falsch' || stand.vorher === null) return stand
  return { ...stand, fen: stand.vorher.fen, letzterZug: stand.vorher.letzterZug, phase: 'amZug', vorher: null }
}

/** Naechste Tipp-Stufe: erst die Figur, dann der Pfeil. */
export function naechsterTipp(stand: Stand): Stand {
  if (stand.phase !== 'amZug') return stand
  return { ...stand, tipp: stand.tipp === 0 ? 1 : 2 }
}

const FIGUR: Record<string, Figur> = {
  k: 'Koenig',
  q: 'Dame',
  r: 'Turm',
  b: 'Laeufer',
  n: 'Springer',
  p: 'Bauer',
}

/** Erster Loesungszug des aktuellen Schritts mit Figur, fuer Tipp-Ring, Pfeil und Text. */
export function loesung(stand: Stand, uebung: Uebung): { von: string; nach: string; figur: Figur } | null {
  const uci = uebung.schritte[stand.schritt]?.zuege[0]
  if (uci === undefined) return null
  const figur = new Chess(stand.fen).get(uci.slice(0, 2) as Parameters<Chess['get']>[0])
  if (figur === undefined) return null
  return { ...vonNach(uci), figur: FIGUR[figur.type] ?? 'Bauer' }
}
