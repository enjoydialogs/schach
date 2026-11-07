// Tippen-Tippen und Ziehen (SCHACH-SPEC 10.3). Reine Logik ohne React: Quelle der erlaubten Zuege
// ist immer die Serverliste `erlaubteZuege` (UCI, Umwandlung mit fuenftem Zeichen).

export type Umwandlungsfigur = 'q' | 'r' | 'b' | 'n'

export type BrettAktion =
  | { art: 'keine' }
  | { art: 'zug'; uci: string }
  | { art: 'umwandlung'; von: string; nach: string }

export interface AuswahlKontext {
  erlaubteZuege: readonly string[]
  /** true, wenn auf dem Feld eine eigene Figur steht (Spieler am Zug). */
  istEigeneFigur: (feld: string) => boolean
}

export interface AuswahlErgebnis {
  /** Gewaehltes Feld danach, `null` = nichts gewaehlt. */
  auswahl: string | null
  aktion: BrettAktion
}

const KEINE: BrettAktion = { art: 'keine' }

/** Zielfelder einer Figur aus der Serverliste, ohne Doppel (Umwandlung = ein Ziel). */
export function zielfelder(von: string, erlaubteZuege: readonly string[]): string[] {
  const ziele = new Set<string>()
  for (const zug of erlaubteZuege) {
    if (zug.startsWith(von)) ziele.add(zug.slice(2, 4))
  }
  return [...ziele]
}

/** Zug von `von` nach `nach`, falls die Serverliste ihn kennt. Umwandlung braucht die Auswahl. */
export function zugFuer(von: string, nach: string, erlaubteZuege: readonly string[]): BrettAktion {
  const passend = erlaubteZuege.filter((zug) => zug.slice(0, 4) === von + nach)
  if (passend.length === 0) return KEINE
  if (passend.some((zug) => zug.length === 5)) return { art: 'umwandlung', von, nach }
  return { art: 'zug', uci: passend[0]! }
}

/** UCI eines Umwandlungszugs, nur wenn die Serverliste ihn enthaelt. */
export function umwandlungsZug(
  von: string,
  nach: string,
  figur: Umwandlungsfigur,
  erlaubteZuege: readonly string[],
): string | null {
  const uci = von + nach + figur
  return erlaubteZuege.includes(uci) ? uci : null
}

/**
 * Ein Tippen auf ein Feld. leer → eigene Figur waehlen; gewaehlt → legales Ziel sendet den Zug,
 * andere eigene Figur waehlt neu, alles andere waehlt ab (SCHACH-SPEC 10.3).
 */
export function tippeFeld(auswahl: string | null, feld: string, kontext: AuswahlKontext): AuswahlErgebnis {
  if (auswahl === null) {
    return { auswahl: kontext.istEigeneFigur(feld) ? feld : null, aktion: KEINE }
  }
  if (feld === auswahl) return { auswahl: null, aktion: KEINE }

  const aktion = zugFuer(auswahl, feld, kontext.erlaubteZuege)
  if (aktion.art !== 'keine') return { auswahl: null, aktion }
  if (kontext.istEigeneFigur(feld)) return { auswahl: feld, aktion: KEINE }
  return { auswahl: null, aktion: KEINE }
}

/** Ablegen nach dem Ziehen. Illegal oder neben dem Brett: `keine`, die Figur schnappt zurueck. */
export function legeAb(von: string, nach: string | null, erlaubteZuege: readonly string[]): BrettAktion {
  if (nach === null || nach === von) return KEINE
  return zugFuer(von, nach, erlaubteZuege)
}
