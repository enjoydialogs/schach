import { feldStile, figurAuf, zieleMitArt } from './markierung'

const START = 'rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1'

describe('figurAuf', () => {
  it('liest Figuren aus dem FEN', () => {
    expect(figurAuf(START, 'e1')).toEqual({ farbe: 'w', typ: 'k' })
    expect(figurAuf(START, 'g8')).toEqual({ farbe: 'b', typ: 'n' })
    expect(figurAuf(START, 'e4')).toBeNull()
  })
})

describe('zieleMitArt', () => {
  it('setzt Punkte auf leere Felder und Ringe auf Schlagfelder', () => {
    const fen = 'rnbqkbnr/ppp1pppp/8/3p4/4P3/8/PPPP1PPP/RNBQKBNR w KQkq - 0 2'
    expect(zieleMitArt(fen, 'e4', ['e5', 'd5'])).toEqual([
      { feld: 'e5', schlag: false },
      { feld: 'd5', schlag: true },
    ])
  })

  it('zaehlt en passant als Schlag', () => {
    const fen = 'rnbqkbnr/ppp1p1pp/8/3pPp2/8/8/PPPP1PPP/RNBQKBNR w KQkq f6 0 3'
    expect(zieleMitArt(fen, 'e5', ['f6', 'e6'])).toEqual([
      { feld: 'f6', schlag: true },
      { feld: 'e6', schlag: false },
    ])
  })
})

describe('feldStile', () => {
  it('ist leer ohne Markierungen', () => {
    expect(feldStile({})).toEqual({})
  })

  it('legt letzten Zug, Auswahl, Ziele, Schach, Warnung und Tipp-Ring auf die Felder', () => {
    const stile = feldStile({
      letzterZug: { von: 'e2', nach: 'e4' },
      auswahl: 'g1',
      ziele: [
        { feld: 'f3', schlag: false },
        { feld: 'h3', schlag: true },
      ],
      schach: 'e8',
      warnungen: ['c3'],
      tippFeld: 'b1',
    })

    expect(stile.e2?.backgroundImage).toContain('--overlay-letzter-zug')
    expect(stile.e4?.backgroundImage).toContain('--overlay-letzter-zug')
    expect(stile.g1?.backgroundImage).toContain('--overlay-auswahl')
    expect(stile.f3?.backgroundImage).toMatch(/^radial-gradient\(circle, var\(--overlay-ziel\) 19%/)
    expect(stile.h3?.backgroundImage).toMatch(/^radial-gradient\(circle, transparent 79%/)
    expect(stile.e8?.backgroundImage).toContain('rgba(255, 0, 0, 1)')
    expect(stile.c3?.backgroundImage).toContain('--overlay-warnung')
    expect(stile.b1?.boxShadow).toBe('var(--tipp-ring)')
  })

  it('stapelt Ziel ueber letztem Zug auf demselben Feld', () => {
    const stile = feldStile({ letzterZug: { von: 'e2', nach: 'e4' }, ziele: [{ feld: 'e4', schlag: true }] })
    const ebenen = stile.e4?.backgroundImage ?? ''
    expect(ebenen.indexOf('transparent 79%')).toBeLessThan(ebenen.indexOf('--overlay-letzter-zug'))
  })
})

describe('feldStile: Tastatur-Cursor', () => {
  it('legt den Cursor-Ring als Schatten auf das Feld', () => {
    expect(feldStile({ cursor: 'e4' }).e4).toEqual({ boxShadow: 'var(--overlay-cursor)' })
  })

  it('behaelt Auswahl-Flaeche und Cursor zusammen', () => {
    const stil = feldStile({ cursor: 'e2', auswahl: 'e2' }).e2
    expect(stil?.backgroundImage).toContain('--overlay-auswahl')
    expect(stil?.boxShadow).toBe('var(--overlay-cursor)')
  })

  it('rueckt den Tipp-Ring unter dem Cursor nach innen', () => {
    expect(feldStile({ cursor: 'g1', tippFeld: 'g1' }).g1?.boxShadow).toBe(
      'var(--overlay-cursor), var(--tipp-ring-unter-cursor)',
    )
    expect(feldStile({ cursor: 'e2', tippFeld: 'g1' }).g1?.boxShadow).toBe('var(--tipp-ring)')
  })
})
