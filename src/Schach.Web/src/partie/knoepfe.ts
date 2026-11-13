import type { ZustandDto } from '../api/types'
import { de } from '../i18n/de'
import { fuelle } from './text'

// Knopfleiste (SCHACH-SPEC 10.3, 3.3, 3.5, 7.3).

export type KnopfArt = 'zuruecknehmen' | 'tipp' | 'remis' | 'aufgeben' | 'nachspielen' | 'neuePartie'

export interface Knopf {
  art: KnopfArt
  aktiv: boolean
  /** Warum der Knopf gesperrt ist; die Statuszeile zeigt ihn beim Tippen. `null` wenn aktiv. */
  grund: string | null
}

/** Warum die Eingabe gerade aus ist. Gegner denkt geht vor beschaeftigt: beides ueberlappt nach dem eigenen Zug. */
export type Sperre = 'offline' | 'gegnerDenkt' | 'beschaeftigt'

export interface KnopfKontext {
  gast: boolean
  /** Eingabe aus und warum; `null` wenn frei. */
  sperre: Sperre | null
  /** Der Tipp dieses Halbzugs zeigt schon den Pfeil. */
  tippFertig: boolean
}

/** Zugnummer aus FEN-Feld 6. */
export function zugnummer(fen: string): number {
  const wert = Number(fen.split(' ')[5])
  return Number.isFinite(wert) && wert > 0 ? wert : 1
}

const aktiv = (art: KnopfArt): Knopf => ({ art, aktiv: true, grund: null })

/** Aktiv, wenn `grund` null ist, sonst gesperrt mit diesem Grund. */
const knopf = (art: KnopfArt, grund: string | null): Knopf => ({ art, aktiv: grund === null, grund })

function sperrGrund(sperre: Sperre | null, gegner: string): string | null {
  if (sperre === 'offline') return de.status.keineVerbindung
  if (sperre === 'gegnerDenkt') return fuelle(de.knopfGrund.warteAufGegner, { name: gegner })
  if (sperre === 'beschaeftigt') return de.knopfGrund.beschaeftigt
  return null
}

function remisGrund(zustand: ZustandDto): string | null {
  const rest = zustand.remisAngebotMoeglichAbZug - zugnummer(zustand.fen)
  if (rest <= 0) return null
  return rest === 1 ? de.knopfGrund.remisInEinemZug : fuelle(de.knopfGrund.remisInZuegen, { n: rest })
}

export function knoepfe(zustand: ZustandDto, kontext: KnopfKontext): Knopf[] {
  if (zustand.status === 'Beendet') {
    // Gast nur „Neue Partie“: Gastpartien werden nicht gespeichert.
    return kontext.gast ? [aktiv('neuePartie')] : [aktiv('nachspielen'), aktiv('neuePartie')]
  }

  const gesperrt = sperrGrund(kontext.sperre, zustand.gegner.name)
  const remis = knopf('remis', gesperrt ?? remisGrund(zustand))
  const aufgeben = knopf('aufgeben', gesperrt)

  // Mit Bedenkzeit entfallen Zuruecknehmen und Tipp.
  if (zustand.bedenkzeit !== 'OhneUhr') return [remis, aufgeben]

  const eigenerZugGespielt = zustand.zugfolge.some((zug) => zug.seite === zustand.spielerFarbe)
  const tippGrund = !zustand.amZug
    ? de.knopfGrund.tippNurAmZug
    : kontext.tippFertig
      ? de.knopfGrund.tippFertig
      : null
  return [
    knopf('zuruecknehmen', gesperrt ?? (eigenerZugGespielt ? null : de.knopfGrund.zuruecknehmenErst)),
    knopf('tipp', gesperrt ?? tippGrund),
    remis,
    aufgeben,
  ]
}
