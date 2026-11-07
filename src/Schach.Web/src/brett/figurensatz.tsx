import type { PieceRenderObject } from 'react-chessboard'
import { Figur, type FigurProps } from './figuren'

const CODES = ['wK', 'wQ', 'wR', 'wB', 'wN', 'wP', 'bK', 'bQ', 'bR', 'bB', 'bN', 'bP'] as const

/** `pieces` fuer react-chessboard: alle 12 Cburnett-Figuren. */
export const cburnett: PieceRenderObject = Object.fromEntries(
  CODES.map((code) => [code, (props?: FigurProps) => <Figur code={code} svgStyle={props?.svgStyle} />]),
)
