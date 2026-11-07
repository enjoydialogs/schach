import type { GegnerDto, ZugDto, ZustandDto } from '../api/types'

// Feste Testdaten fuer die Vitest-Tests des Partie-Bildschirms.

export const ILSE: GegnerDto = {
  key: 'ilse',
  name: 'Ilse',
  elo: 600,
  stufe: 'Anfaenger',
  persoenlichkeit: 'Gemuetlich',
  vorstellung: 'Keine Eile. Wir spielen in aller Ruhe, ja?',
  bilanz: null,
}

export const START_FEN = 'rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1'

export function zug(halbzug: number, uci: string, san: string): ZugDto {
  return {
    halbzug,
    seite: halbzug % 2 === 1 ? 'Weiss' : 'Schwarz',
    uci,
    san,
    gespieltAm: '2026-11-07T10:00:00.000Z',
  }
}

export function zustand(teil: Partial<ZustandDto> = {}): ZustandDto {
  return {
    id: 'p1',
    status: 'Offen',
    spielerFarbe: 'Weiss',
    gegner: ILSE,
    bedenkzeit: 'OhneUhr',
    halbzug: 0,
    fen: START_FEN,
    zugfolge: [],
    erlaubteZuege: ['e2e4', 'd2d4'],
    amZug: true,
    gegnerZugAusstehend: false,
    gegnerZug: null,
    uhr: null,
    zaehltFuerElo: true,
    remisAngebotMoeglichAbZug: 1,
    sprueche: [],
    ergebnis: null,
    grund: null,
    eloVorher: null,
    eloNachher: null,
    pgn: null,
    ...teil,
  }
}
