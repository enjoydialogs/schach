import { zug, zustand } from '../partie/testdaten'
import { deutscheSan, pgnDateiname, spruchAn, stellungen, zugText } from './zugfolge'

describe('deutscheSan', () => {
  it('uebersetzt Figurenbuchstaben und Umwandlung', () => {
    expect(deutscheSan('Nf3')).toBe('Sf3')
    expect(deutscheSan('Bxe5+')).toBe('Lxe5+')
    expect(deutscheSan('Rad1')).toBe('Tad1')
    expect(deutscheSan('Qh5#')).toBe('Dh5#')
    expect(deutscheSan('Kxf2')).toBe('Kxf2')
    expect(deutscheSan('exd8=Q+')).toBe('exd8=D+')
    expect(deutscheSan('b8=N')).toBe('b8=S')
    expect(deutscheSan('O-O-O')).toBe('O-O-O')
    expect(deutscheSan('bxc3')).toBe('bxc3')
  })
})

describe('zugText', () => {
  it('nummeriert Weiss und Schwarz', () => {
    expect(zugText(zug(23, 'g1f3', 'Nf3'))).toBe('12. Sf3')
    expect(zugText(zug(24, 'g8f6', 'Nf6'))).toBe('12. … Sf6')
  })
})

describe('stellungen', () => {
  it('spielt die Zugfolge mit chess.js nach, Halbzug 0 ist die Grundstellung', () => {
    const s = stellungen([zug(1, 'e2e4', 'e4'), zug(2, 'e7e5', 'e5')])
    expect(s).toHaveLength(3)
    expect(s[0]).toEqual({ fen: 'rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1', letzterZug: null })
    expect(s[2]?.fen).toBe('rnbqkbnr/pppp1ppp/8/4p3/4P3/8/PPPP1PPP/RNBQKBNR w KQkq - 0 2')
    expect(s[2]?.letzterZug).toEqual({ von: 'e7', nach: 'e5' })
  })
})

describe('spruchAn', () => {
  it('zeigt nur den Spruch an seinem Halbzug, bei mehreren den letzten', () => {
    const sprueche = [
      { nachHalbzug: 0, ereignis: 'Partiebeginn' as const, text: 'Los', stimme: null },
      { nachHalbzug: 4, ereignis: 'Zug' as const, text: 'Hm', stimme: null },
      { nachHalbzug: 4, ereignis: 'FigurGewonnen' as const, text: 'Danke', stimme: null },
    ]
    expect(spruchAn(sprueche, 0)?.text).toBe('Los')
    expect(spruchAn(sprueche, 2)).toBeNull()
    expect(spruchAn(sprueche, 4)?.text).toBe('Danke')
  })
})

describe('pgnDateiname', () => {
  it('nennt Gegner und Datum des letzten Zugs', () => {
    const z = zustand({ zugfolge: [{ ...zug(1, 'e2e4', 'e4'), gespieltAm: '2026-11-07T22:30:00.000Z' }] })
    expect(pgnDateiname(z)).toBe('schach-ilse-2026-11-07.pgn')
  })
})
