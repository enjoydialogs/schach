import { useEffect, useMemo, useRef, useState, type KeyboardEvent } from 'react'
import { Chessboard, type ChessboardOptions } from 'react-chessboard'
import type { Farbe } from '../api/types'
import { legeAb, tippeFeld, umwandlungsZug, zielfelder, type BrettAktion } from './auswahl'
import { usePalette } from '../components/palette'
import { t } from '../i18n/de'
import { FIGURENSAETZE } from './figurensatz'
import { feldStile, figurAuf, zieleMitArt } from './markierung'
import { ansageNachTipp, beschreibeFeld, bewegeCursor, istCursorTaste, koenigsfeld } from './tastatur'
import { Umwandlung } from './Umwandlung'
import './brett.css'

// Tipp-Pfeil in Akzentfarbe. react-chessboard setzt die Farbe als SVG-Attribut, dort greift keine
// CSS-Variable; der Wert kommt darum aus der gewaehlten Palette (components/palette.ts).

export interface BrettProps {
  /** Angezeigte Stellung (FEN vom Server, optimistisch fortgeschrieben). */
  fen: string
  /** Brett von der Seite des Spielers (`boardOrientation`). */
  orientierung: Farbe
  /** UCI-Liste vom Server; leer, wenn der Spieler nicht am Zug ist. */
  erlaubteZuege: readonly string[]
  /** Tippen und Ziehen erlaubt. */
  eingabeAktiv: boolean
  /** Lernhilfe „Erlaubte Zuege anzeigen“ (Punkte und Ringe). */
  zeigeZiele: boolean
  letzterZug?: { von: string; nach: string } | null
  /** Feld des Koenigs im Schach; immer markiert. */
  schach?: string | null
  warnungen?: readonly string[]
  tippFeld?: string | null
  tippPfeil?: { von: string; nach: string } | null
  onZug: (uci: string) => void
}

/** Figuren-Elemente von dnd-kit; jedes waere sonst ein eigener Tab-Stopp (tabIndex 0). */
const FIGUR_SELEKTOR = '[aria-roledescription="draggable"]'

/** Nimmt alle Figuren aus der Tab-Reihenfolge; react-chessboard haengt nach Zuegen neue ein. */
function figurenAusTabFolge(wurzel: HTMLElement) {
  for (const figur of wurzel.querySelectorAll<HTMLElement>(FIGUR_SELEKTOR)) {
    if (figur.tabIndex !== -1) figur.tabIndex = -1
  }
}

/**
 * Brett nach SCHACH-SPEC 10.3: react-chessboard 5, Figurensatz je Palette, Tippen-Tippen, Ziehen
 * und Tastatur (ein Tab-Stopp, Pfeile bewegen einen Cursor, Enter/Leertaste wirkt wie Tippen).
 */
export function Brett(props: BrettProps) {
  const { fen, orientierung, erlaubteZuege, eingabeAktiv, zeigeZiele, onZug } = props
  const eigene = orientierung === 'Weiss' ? 'w' : 'b'
  const palette = usePalette()

  // Auswahl und offene Umwandlung gelten nur fuer die Stellung, in der sie entstanden sind.
  const [auswahlStand, setAuswahlStand] = useState<{ fen: string; feld: string } | null>(null)
  const [umwandlungStand, setUmwandlungStand] = useState<{ fen: string; von: string; nach: string } | null>(null)
  const auswahl = eingabeAktiv && auswahlStand?.fen === fen ? auswahlStand.feld : null
  const umwandlung = eingabeAktiv && umwandlungStand?.fen === fen ? umwandlungStand : null

  const waehle = (feld: string | null) => setAuswahlStand(feld === null ? null : { fen, feld })

  // Tastatur: Cursor bleibt ueber Zuege hinweg stehen. Sichtbar nur, solange die Tastatur bedient
  // wird; Tippen und Ziehen schalten ihn aus, damit Touch genau wie vorher aussieht.
  const flaeche = useRef<HTMLDivElement>(null)
  const zeigerGedrueckt = useRef(false)
  const [cursorFeld, setCursorFeld] = useState<string | null>(null)
  const [tastatur, setTastatur] = useState(false)
  const [ansage, setAnsage] = useState('')
  const cursor = cursorFeld ?? auswahl ?? koenigsfeld(fen, eigene)

  useEffect(() => {
    const wurzel = flaeche.current
    if (!wurzel) return
    figurenAusTabFolge(wurzel)
    const beobachter = new MutationObserver(() => figurenAusTabFolge(wurzel))
    beobachter.observe(wurzel, { childList: true, subtree: true, attributes: true, attributeFilter: ['tabindex'] })
    return () => beobachter.disconnect()
  }, [])

  const fuehreAus = (aktion: BrettAktion): boolean => {
    if (aktion.art === 'zug') {
      onZug(aktion.uci)
      return true
    }
    if (aktion.art === 'umwandlung') setUmwandlungStand({ fen, von: aktion.von, nach: aktion.nach })
    return false
  }

  const istEigeneFigur = (feld: string) => figurAuf(fen, feld)?.farbe === eigene

  const ziele = auswahl !== null ? zielfelder(auswahl, erlaubteZuege) : []

  const taste = (e: KeyboardEvent<HTMLDivElement>) => {
    if (e.altKey || e.ctrlKey || e.metaKey) return
    let neu: string | null = null
    if (istCursorTaste(e.key)) {
      neu = bewegeCursor(cursor, e.key, orientierung)
      setCursorFeld(neu)
      setAnsage(beschreibeFeld(fen, neu, zeigeZiele && ziele.includes(neu)))
    } else if (e.key === 'Enter' || e.key === ' ') {
      if (!eingabeAktiv) {
        setAnsage(t('brett.ansageKeineEingabe'))
      } else {
        const ergebnis = tippeFeld(auswahl, cursor, { erlaubteZuege, istEigeneFigur })
        waehle(ergebnis.auswahl)
        fuehreAus(ergebnis.aktion)
        setAnsage(ansageNachTipp(fen, auswahl, cursor, ergebnis))
      }
    } else if (e.key === 'Escape' && auswahl !== null) {
      waehle(null)
      setAnsage(t('brett.ansageAbgewaehlt'))
    } else {
      return
    }
    // Eigene Tasten nicht an die Figur weitergeben: dnd-kit startet sonst mit Enter/Leertaste
    // einen eigenen Tastatur-Zug.
    e.preventDefault()
    e.stopPropagation()
    setTastatur(true)
  }

  const stile = useMemo(() => {
    const ziele = auswahl !== null && zeigeZiele ? zieleMitArt(fen, auswahl, zielfelder(auswahl, erlaubteZuege)) : []
    return feldStile({
      cursor: tastatur ? cursor : null,
      letzterZug: props.letzterZug,
      auswahl,
      ziele,
      schach: props.schach,
      warnungen: props.warnungen,
      tippFeld: props.tippFeld,
    })
  }, [
    fen,
    auswahl,
    zeigeZiele,
    erlaubteZuege,
    tastatur,
    cursor,
    props.letzterZug,
    props.schach,
    props.warnungen,
    props.tippFeld,
  ])

  const options: ChessboardOptions = {
    id: 'brett',
    position: fen,
    boardOrientation: orientierung === 'Weiss' ? 'white' : 'black',
    pieces: FIGURENSAETZE[palette.figuren],
    allowDrawingArrows: false,
    dragActivationDistance: 8,
    animationDurationInMs: 200,
    showNotation: true,
    allowDragging: eingabeAktiv,
    allowDragOffBoard: false,
    clearArrowsOnClick: false,
    arrows: props.tippPfeil
      ? [{ startSquare: props.tippPfeil.von, endSquare: props.tippPfeil.nach, color: palette.akzent }]
      : [],
    squareStyles: stile,
    lightSquareStyle: { backgroundColor: 'var(--board-light)' },
    darkSquareStyle: { backgroundColor: 'var(--board-dark)' },
    // Koordinaten aus der Palette (hell: Gegenfarbe des Feldes, Nacht: leise); die Bibliothek setzt
    // sonst Braun fest.
    lightSquareNotationStyle: { color: 'var(--notation-auf-hell)', fontWeight: 600 },
    darkSquareNotationStyle: { color: 'var(--notation-auf-dunkel)', fontWeight: 600 },
    canDragPiece: ({ piece }) => eingabeAktiv && piece.pieceType[0] === eigene,
    onSquareClick: ({ square }) => {
      if (!eingabeAktiv) return
      const ergebnis = tippeFeld(auswahl, square, { erlaubteZuege, istEigeneFigur })
      waehle(ergebnis.auswahl)
      fuehreAus(ergebnis.aktion)
    },
    onPieceDrag: ({ square }) => {
      if (eingabeAktiv && square !== null) waehle(square)
    },
    onPieceDrop: ({ sourceSquare, targetSquare }) => {
      waehle(null)
      if (!eingabeAktiv) return false
      return fuehreAus(legeAb(sourceSquare, targetSquare, erlaubteZuege))
    },
  }

  return (
    <div className="brett" data-testid="brett">
      <div
        ref={flaeche}
        className="brett__flaeche"
        role="application"
        tabIndex={0}
        aria-label={t('brett.tastaturLabel')}
        onKeyDownCapture={taste}
        onPointerDownCapture={() => {
          zeigerGedrueckt.current = true
          setTastatur(false)
        }}
        onFocus={(e) => {
          // Fokus auf einer Figur (Klick) zurueck auf das Brett holen: die Figur verschwindet nach
          // dem Zug, und nur hier hoert die Tastatursteuerung zu.
          if (e.target !== e.currentTarget) {
            e.currentTarget.focus({ preventScroll: true })
            return
          }
          const perTab = !zeigerGedrueckt.current
          zeigerGedrueckt.current = false
          if (perTab) {
            setTastatur(true)
            setAnsage(beschreibeFeld(fen, cursor))
          }
        }}
        onBlur={(e) => {
          if (e.currentTarget.contains(e.relatedTarget)) return
          zeigerGedrueckt.current = false
          setTastatur(false)
        }}
      >
        <Chessboard options={options} />
      </div>
      <div className="sr-only" aria-live="polite" aria-atomic="true" data-testid="brett-ansage">
        {ansage}
      </div>
      {umwandlung !== null && (
        <Umwandlung
          farbe={eigene}
          onAbbruch={() => setUmwandlungStand(null)}
          onWahl={(figur) => {
            setUmwandlungStand(null)
            const uci = umwandlungsZug(umwandlung.von, umwandlung.nach, figur, erlaubteZuege)
            if (uci !== null) onZug(uci)
          }}
        />
      )}
    </div>
  )
}
