import { Chess } from 'chess.js'
import type { Farbe } from '../api/types'
import { ungedeckteFiguren, warnungsText } from './warnung'

// Gemeinsame FEN-Suite (SCHACH-SPEC 15.2): dieselben Faelle prueft der Server.
interface UngedecktFall {
  id: string
  fen: string
  zugfolge?: string[]
  seite: Farbe
  ungedeckt: string[]
}
const dateien = import.meta.glob<{ faelle: UngedecktFall[] }>('../../../../tests/fen-suite/ungedeckt.json', {
  eager: true,
  import: 'default',
})
const faelle = Object.values(dateien).flatMap((datei) => datei.faelle)

function stellung(fall: UngedecktFall): string {
  if (!fall.zugfolge?.length) return fall.fen
  const brett = new Chess(fall.fen)
  for (const uci of fall.zugfolge) {
    brett.move({ from: uci.slice(0, 2), to: uci.slice(2, 4), promotion: uci[4] })
  }
  return brett.fen()
}

describe('ungedeckteFiguren mit der FEN-Suite', () => {
  it('findet die Suite', () => {
    expect(faelle.length).toBeGreaterThanOrEqual(3)
  })

  it.each(faelle.map((fall) => [fall.id, fall] as const))('%s', (_id, fall) => {
    expect(ungedeckteFiguren(stellung(fall), fall.seite).map((f) => f.feld)).toEqual(fall.ungedeckt)
  })
})

describe('ungedeckteFiguren', () => {
  it('warnt nicht vor Bauern und Koenig', () => {
    // Bauer e4 und Koenig e1 angegriffen und ungedeckt, aber keine Leicht- oder Schwerfigur.
    expect(ungedeckteFiguren('4k3/8/8/3p4/4P3/8/8/r3K3 w - - 0 1', 'Weiss')).toEqual([])
  })

  it('zaehlt den Koenig als Deckung', () => {
    expect(ungedeckteFiguren('4k3/8/8/8/8/8/3N4/b3K3 w - - 0 1', 'Weiss')).toEqual([])
  })

  it('ignoriert Fesselungen', () => {
    // Laeufer d2 ist vom Turm d8 an den Koenig d1 gefesselt, deckt den Springer c3 aber statisch.
    expect(ungedeckteFiguren('3rk3/8/8/b7/8/2N5/3B4/3K4 w - - 0 1', 'Weiss')).toEqual([])
  })

  it('liefert bei ungueltiger FEN keine Warnung', () => {
    expect(ungedeckteFiguren('kein fen', 'Weiss')).toEqual([])
  })
})

describe('warnungsText', () => {
  it('nennt Figur und Feld', () => {
    expect(warnungsText({ feld: 'c3', figur: 'Springer' })).toBe('Dein Springer auf c3 ist angegriffen und ungedeckt.')
    expect(warnungsText({ feld: 'd5', figur: 'Dame' })).toBe('Deine Dame auf d5 ist angegriffen und ungedeckt.')
  })
})
