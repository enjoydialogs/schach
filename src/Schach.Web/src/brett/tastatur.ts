// Brett per Tastatur (SCHACH-SPEC 10.3, Brett). Reine Logik ohne React: Cursor bewegen und kurze
// deutsche Ansagen fuer die Live-Region. Enter/Leertaste nutzt `tippeFeld` aus auswahl.ts.

import type { Farbe } from '../api/types'
import { t } from '../i18n/de'
import { fuelle } from '../partie/text'
import type { AuswahlErgebnis } from './auswahl'
import { figurAuf, type FigurFarbe } from './markierung'

/** Tasten, die den Cursor bewegen. Richtung wie auf dem Bildschirm. */
export type CursorTaste = 'ArrowUp' | 'ArrowDown' | 'ArrowLeft' | 'ArrowRight' | 'Home' | 'End'

const CURSOR_TASTEN: readonly string[] = ['ArrowUp', 'ArrowDown', 'ArrowLeft', 'ArrowRight', 'Home', 'End']

export function istCursorTaste(taste: string): taste is CursorTaste {
  return CURSOR_TASTEN.includes(taste)
}

const begrenze = (wert: number) => Math.min(7, Math.max(0, wert))

/**
 * Naechstes Cursorfeld. Pfeile gehen ein Feld in Bildschirmrichtung und bleiben am Rand stehen;
 * Pos1/Ende springen an den linken/rechten Rand der Reihe. Mit Schwarz unten ist alles gespiegelt.
 */
export function bewegeCursor(feld: string, taste: CursorTaste, orientierung: Farbe): string {
  const spiegel = orientierung === 'Weiss' ? 1 : -1
  let linie = feld.charCodeAt(0) - 97
  let reihe = Number(feld[1]) - 1
  switch (taste) {
    case 'ArrowUp':
      reihe += spiegel
      break
    case 'ArrowDown':
      reihe -= spiegel
      break
    case 'ArrowRight':
      linie += spiegel
      break
    case 'ArrowLeft':
      linie -= spiegel
      break
    case 'Home':
      linie = spiegel === 1 ? 0 : 7
      break
    case 'End':
      linie = spiegel === 1 ? 7 : 0
      break
  }
  return String.fromCharCode(97 + begrenze(linie)) + String(begrenze(reihe) + 1)
}

/** Feld des eigenen Koenigs aus dem FEN, sonst die Grundstellung. */
export function koenigsfeld(fen: string, farbe: FigurFarbe): string {
  const ziel = farbe === 'w' ? 'K' : 'k'
  const reihen = fen.split(' ')[0]?.split('/') ?? []
  for (const [index, reihe] of reihen.entries()) {
    let linie = 0
    for (const zeichen of reihe) {
      if (/\d/.test(zeichen)) {
        linie += Number(zeichen)
        continue
      }
      if (zeichen === ziel) return String.fromCharCode(97 + linie) + String(8 - index)
      linie += 1
    }
  }
  return farbe === 'w' ? 'e1' : 'e8'
}

const FIGUR_NAME: Record<string, string> = {
  k: t('brett.figurKoenig'),
  q: t('brett.umwandlungDame'),
  r: t('brett.umwandlungTurm'),
  b: t('brett.umwandlungLaeufer'),
  n: t('brett.umwandlungSpringer'),
  p: t('brett.figurBauer'),
}

/** „weißer Bauer“, „schwarze Dame“: Dame ist das einzige Femininum. */
function figurMitFarbe(farbe: FigurFarbe, typ: string): string {
  const weiblich = typ === 'q'
  const adjektiv =
    farbe === 'w'
      ? t(weiblich ? 'brett.farbeWeissF' : 'brett.farbeWeissM')
      : t(weiblich ? 'brett.farbeSchwarzF' : 'brett.farbeSchwarzM')
  return `${adjektiv} ${FIGUR_NAME[typ] ?? typ}`
}

/** „e4, weißer Bauer“ oder „e5, leer“; mit `zielMoeglich` folgt „Zug möglich“. */
export function beschreibeFeld(fen: string, feld: string, zielMoeglich = false): string {
  const figur = figurAuf(fen, feld)
  const text =
    figur === null
      ? fuelle(t('brett.ansageLeer'), { feld })
      : fuelle(t('brett.ansageFigur'), { feld, figur: figurMitFarbe(figur.farbe, figur.typ) })
  return zielMoeglich ? `${text}, ${t('brett.ansageZielMoeglich')}` : text
}

/**
 * Ansage nach Enter/Leertaste auf `feld`. Leer bei Umwandlung: der Dialog nennt sich selbst.
 */
export function ansageNachTipp(fen: string, vorher: string | null, feld: string, ergebnis: AuswahlErgebnis): string {
  const { aktion } = ergebnis
  if (aktion.art === 'zug') return fuelle(t('brett.ansageZug'), { von: aktion.uci.slice(0, 2), nach: aktion.uci.slice(2, 4) })
  if (aktion.art === 'umwandlung') return ''
  if (ergebnis.auswahl !== null) {
    const typ = figurAuf(fen, ergebnis.auswahl)?.typ ?? ''
    return fuelle(t('brett.ansageGewaehlt'), { figur: FIGUR_NAME[typ] ?? typ, feld: ergebnis.auswahl })
  }
  if (vorher !== null) return t('brett.ansageAbgewaehlt')
  return fuelle(t('brett.ansageKeineFigur'), { feld })
}
