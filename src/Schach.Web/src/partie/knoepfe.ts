import type { ZustandDto } from '../api/types'

// Knopfleiste (SCHACH-SPEC 10.3, 3.3, 3.5, 7.3).

export type KnopfArt = 'zuruecknehmen' | 'tipp' | 'remis' | 'aufgeben' | 'nachspielen' | 'neuePartie'

export interface Knopf {
  art: KnopfArt
  aktiv: boolean
}

export interface KnopfKontext {
  gast: boolean
  /** Eingabe aus: Anfrage laeuft, Gegner denkt sichtbar nach oder keine Verbindung. */
  gesperrt: boolean
  /** Der Tipp dieses Halbzugs zeigt schon den Pfeil. */
  tippFertig: boolean
}

/** Zugnummer aus FEN-Feld 6. */
export function zugnummer(fen: string): number {
  const wert = Number(fen.split(' ')[5])
  return Number.isFinite(wert) && wert > 0 ? wert : 1
}

export function knoepfe(zustand: ZustandDto, kontext: KnopfKontext): Knopf[] {
  if (zustand.status === 'Beendet') {
    // Gast nur „Neue Partie“: Gastpartien werden nicht gespeichert.
    return kontext.gast
      ? [{ art: 'neuePartie', aktiv: true }]
      : [
          { art: 'nachspielen', aktiv: true },
          { art: 'neuePartie', aktiv: true },
        ]
  }

  const frei = !kontext.gesperrt
  const remis: Knopf = {
    art: 'remis',
    aktiv: frei && zugnummer(zustand.fen) >= zustand.remisAngebotMoeglichAbZug,
  }
  const aufgeben: Knopf = { art: 'aufgeben', aktiv: frei }

  // Mit Bedenkzeit entfallen Zuruecknehmen und Tipp.
  if (zustand.bedenkzeit !== 'OhneUhr') return [remis, aufgeben]

  const eigenerZugGespielt = zustand.zugfolge.some((zug) => zug.seite === zustand.spielerFarbe)
  return [
    { art: 'zuruecknehmen', aktiv: frei && eigenerZugGespielt },
    { art: 'tipp', aktiv: frei && zustand.amZug && !kontext.tippFertig },
    remis,
    aufgeben,
  ]
}
