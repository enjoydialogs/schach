import { useMemo, useState } from 'react'
import { Chessboard, type ChessboardOptions } from 'react-chessboard'
import type { Farbe } from '../api/types'
import { legeAb, tippeFeld, umwandlungsZug, zielfelder, type BrettAktion } from './auswahl'
import { usePalette } from '../components/palette'
import { cburnett } from './figurensatz'
import { feldStile, figurAuf, zieleMitArt } from './markierung'
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

/** Brett nach SCHACH-SPEC 10.3: react-chessboard 5 mit Cburnett-Figuren, Tippen-Tippen und Ziehen. */
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

  const fuehreAus = (aktion: BrettAktion): boolean => {
    if (aktion.art === 'zug') {
      onZug(aktion.uci)
      return true
    }
    if (aktion.art === 'umwandlung') setUmwandlungStand({ fen, von: aktion.von, nach: aktion.nach })
    return false
  }

  const istEigeneFigur = (feld: string) => figurAuf(fen, feld)?.farbe === eigene

  const stile = useMemo(() => {
    const ziele = auswahl !== null && zeigeZiele ? zieleMitArt(fen, auswahl, zielfelder(auswahl, erlaubteZuege)) : []
    return feldStile({
      letzterZug: props.letzterZug,
      auswahl,
      ziele,
      schach: props.schach,
      warnungen: props.warnungen,
      tippFeld: props.tippFeld,
    })
  }, [fen, auswahl, zeigeZiele, erlaubteZuege, props.letzterZug, props.schach, props.warnungen, props.tippFeld])

  const options: ChessboardOptions = {
    id: 'brett',
    position: fen,
    boardOrientation: orientierung === 'Weiss' ? 'white' : 'black',
    pieces: cburnett,
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
    // Koordinaten in der Gegenfarbe des Feldes; die Bibliothek setzt sonst Braun fest.
    lightSquareNotationStyle: { color: 'var(--board-dark)', fontWeight: 600 },
    darkSquareNotationStyle: { color: 'var(--board-light)', fontWeight: 600 },
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
      <Chessboard options={options} />
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
