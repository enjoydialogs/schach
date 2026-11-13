import { ansageNachTipp, beschreibeFeld, bewegeCursor, istCursorTaste, koenigsfeld } from './tastatur'

const START = 'rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1'

describe('bewegeCursor', () => {
  it('geht mit Weiss unten in Bildschirmrichtung', () => {
    expect(bewegeCursor('e2', 'ArrowUp', 'Weiss')).toBe('e3')
    expect(bewegeCursor('e2', 'ArrowDown', 'Weiss')).toBe('e1')
    expect(bewegeCursor('e2', 'ArrowRight', 'Weiss')).toBe('f2')
    expect(bewegeCursor('e2', 'ArrowLeft', 'Weiss')).toBe('d2')
  })

  it('spiegelt mit Schwarz unten', () => {
    expect(bewegeCursor('e7', 'ArrowUp', 'Schwarz')).toBe('e6')
    expect(bewegeCursor('e7', 'ArrowDown', 'Schwarz')).toBe('e8')
    expect(bewegeCursor('e7', 'ArrowRight', 'Schwarz')).toBe('d7')
    expect(bewegeCursor('e7', 'ArrowLeft', 'Schwarz')).toBe('f7')
  })

  it('bleibt am Rand stehen', () => {
    expect(bewegeCursor('a1', 'ArrowLeft', 'Weiss')).toBe('a1')
    expect(bewegeCursor('a1', 'ArrowDown', 'Weiss')).toBe('a1')
    expect(bewegeCursor('h8', 'ArrowUp', 'Weiss')).toBe('h8')
    expect(bewegeCursor('h8', 'ArrowLeft', 'Schwarz')).toBe('h8')
    expect(bewegeCursor('a1', 'ArrowUp', 'Schwarz')).toBe('a1')
  })

  it('springt mit Pos1 und Ende an den Rand der Reihe', () => {
    expect(bewegeCursor('e4', 'Home', 'Weiss')).toBe('a4')
    expect(bewegeCursor('e4', 'End', 'Weiss')).toBe('h4')
    expect(bewegeCursor('e4', 'Home', 'Schwarz')).toBe('h4')
    expect(bewegeCursor('e4', 'End', 'Schwarz')).toBe('a4')
  })

  it('kennt nur Cursortasten', () => {
    expect(istCursorTaste('ArrowUp')).toBe(true)
    expect(istCursorTaste('Enter')).toBe(false)
  })
})

describe('koenigsfeld', () => {
  it('findet den Koenig im FEN', () => {
    expect(koenigsfeld(START, 'w')).toBe('e1')
    expect(koenigsfeld(START, 'b')).toBe('e8')
    expect(koenigsfeld('6k1/8/8/8/8/8/8/K7 w - - 0 1', 'b')).toBe('g8')
  })

  it('faellt ohne Koenig auf die Grundstellung zurueck', () => {
    expect(koenigsfeld('8/8/8/8/8/8/8/8 w - - 0 1', 'w')).toBe('e1')
  })
})

describe('beschreibeFeld', () => {
  it('nennt Feld und Figur mit Farbe', () => {
    expect(beschreibeFeld(START, 'e2')).toBe('e2, weißer Bauer')
    expect(beschreibeFeld(START, 'g8')).toBe('g8, schwarzer Springer')
    expect(beschreibeFeld(START, 'e4')).toBe('e4, leer')
  })

  it('beugt die Dame weiblich', () => {
    expect(beschreibeFeld(START, 'd1')).toBe('d1, weiße Dame')
    expect(beschreibeFeld(START, 'd8')).toBe('d8, schwarze Dame')
  })

  it('haengt „Zug möglich“ an', () => {
    expect(beschreibeFeld(START, 'e4', true)).toBe('e4, leer, Zug möglich')
  })
})

describe('ansageNachTipp', () => {
  const keine = { art: 'keine' } as const

  it('sagt Auswahl, Zug und Abwahl an', () => {
    expect(ansageNachTipp(START, null, 'g1', { auswahl: 'g1', aktion: keine })).toBe('Springer g1 gewählt')
    expect(ansageNachTipp(START, 'e2', 'e4', { auswahl: null, aktion: { art: 'zug', uci: 'e2e4' } })).toBe(
      'Zug e2 nach e4',
    )
    expect(ansageNachTipp(START, 'e2', 'e5', { auswahl: null, aktion: keine })).toBe('Auswahl aufgehoben')
  })

  it('meldet ein Feld ohne eigene Figur', () => {
    expect(ansageNachTipp(START, null, 'e7', { auswahl: null, aktion: keine })).toBe('e7, keine eigene Figur')
  })

  it('schweigt bei der Umwandlung, der Dialog nennt sich selbst', () => {
    expect(ansageNachTipp(START, 'a7', 'a8', { auswahl: null, aktion: { art: 'umwandlung', von: 'a7', nach: 'a8' } })).toBe('')
  })
})
