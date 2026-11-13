import type { ReactElement } from 'react'
import type { PieceRenderObject } from 'react-chessboard'
import type { Figurensatz } from '../components/palette'
import { Figur, type FigurProps } from './figuren'
import { FlacheFigur } from './figurenFlach'

const CODES = ['wK', 'wQ', 'wR', 'wB', 'wN', 'wP', 'bK', 'bQ', 'bR', 'bB', 'bN', 'bP'] as const

type FigurKomponente = (props: { code: string } & FigurProps) => ReactElement

/** Einzelne Figur je Figurensatz (`figuren` der Palette), z. B. fuer die Umwandlungs-Kacheln. */
export const FIGUR_JE_SATZ: Record<Figurensatz, FigurKomponente> = { cburnett: Figur, flach: FlacheFigur }

function satz(Komponente: FigurKomponente): PieceRenderObject {
  return Object.fromEntries(
    CODES.map((code) => [code, (props?: FigurProps) => <Komponente code={code} svgStyle={props?.svgStyle} />]),
  )
}

/** `pieces` fuer react-chessboard je Figurensatz: Cburnett (helle Paletten) oder flach (Nacht). */
export const FIGURENSAETZE: Record<Figurensatz, PieceRenderObject> = {
  cburnett: satz(Figur),
  flach: satz(FlacheFigur),
}
