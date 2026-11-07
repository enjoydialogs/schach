import { legeAb, tippeFeld, umwandlungsZug, zielfelder, type AuswahlKontext } from './auswahl'

// Grundstellung, Weiss am Zug: eigene Figuren auf Reihe 1 und 2.
const START: AuswahlKontext = {
  erlaubteZuege: ['e2e4', 'e2e3', 'g1f3', 'g1h3', 'b1c3', 'b1a3', 'd2d4'],
  istEigeneFigur: (feld) => feld.endsWith('1') || feld.endsWith('2'),
}

describe('Tippen-Tippen', () => {
  it('waehlt aus leerem Zustand eine eigene Figur', () => {
    expect(tippeFeld(null, 'e2', START)).toEqual({ auswahl: 'e2', aktion: { art: 'keine' } })
  })

  it('waehlt nichts beim Tippen auf ein leeres oder gegnerisches Feld', () => {
    expect(tippeFeld(null, 'e4', START).auswahl).toBeNull()
    expect(tippeFeld(null, 'e7', START).auswahl).toBeNull()
  })

  it('sendet den Zug beim Tippen auf ein legales Ziel und waehlt ab', () => {
    expect(tippeFeld('e2', 'e4', START)).toEqual({ auswahl: null, aktion: { art: 'zug', uci: 'e2e4' } })
  })

  it('waehlt beim Tippen auf eine andere eigene Figur neu', () => {
    expect(tippeFeld('e2', 'g1', START)).toEqual({ auswahl: 'g1', aktion: { art: 'keine' } })
  })

  it('waehlt ab beim erneuten Tippen auf dasselbe Feld', () => {
    expect(tippeFeld('e2', 'e2', START)).toEqual({ auswahl: null, aktion: { art: 'keine' } })
  })

  it('waehlt ab beim Tippen auf ein illegales Ziel', () => {
    expect(tippeFeld('e2', 'e5', START)).toEqual({ auswahl: null, aktion: { art: 'keine' } })
  })

  it('nutzt nur die Serverliste, nicht die Regeln', () => {
    const ohneE4 = { ...START, erlaubteZuege: ['e2e3'] }
    expect(tippeFeld('e2', 'e4', ohneE4).aktion).toEqual({ art: 'keine' })
  })
})

describe('Zielfelder', () => {
  it('liefert die Ziele einer Figur aus der Serverliste', () => {
    expect(zielfelder('g1', START.erlaubteZuege).sort()).toEqual(['f3', 'h3'])
    expect(zielfelder('a1', START.erlaubteZuege)).toEqual([])
  })

  it('zaehlt die vier Umwandlungen als ein Ziel', () => {
    expect(zielfelder('a7', ['a7a8q', 'a7a8r', 'a7a8b', 'a7a8n'])).toEqual(['a8'])
  })
})

describe('Umwandlung', () => {
  const UMWANDLUNG: AuswahlKontext = {
    erlaubteZuege: ['a7a8q', 'a7a8r', 'a7a8b', 'a7a8n', 'a7b8q', 'a7b8r', 'a7b8b', 'a7b8n', 'e1e2'],
    istEigeneFigur: (feld) => feld === 'a7' || feld === 'e1',
  }

  it('oeffnet die Auswahl statt sofort zu ziehen', () => {
    expect(tippeFeld('a7', 'a8', UMWANDLUNG).aktion).toEqual({ art: 'umwandlung', von: 'a7', nach: 'a8' })
    expect(legeAb('a7', 'b8', UMWANDLUNG.erlaubteZuege)).toEqual({ art: 'umwandlung', von: 'a7', nach: 'b8' })
  })

  it('baut den UCI-Zug mit der gewaehlten Figur', () => {
    expect(umwandlungsZug('a7', 'a8', 'n', UMWANDLUNG.erlaubteZuege)).toBe('a7a8n')
    expect(umwandlungsZug('a7', 'a6', 'q', UMWANDLUNG.erlaubteZuege)).toBeNull()
  })
})

describe('Ziehen', () => {
  it('nimmt einen legalen Zug an', () => {
    expect(legeAb('b1', 'c3', START.erlaubteZuege)).toEqual({ art: 'zug', uci: 'b1c3' })
  })

  it('schnappt zurueck bei illegalem Ziel, gleichem Feld oder neben dem Brett', () => {
    expect(legeAb('b1', 'b3', START.erlaubteZuege)).toEqual({ art: 'keine' })
    expect(legeAb('b1', 'b1', START.erlaubteZuege)).toEqual({ art: 'keine' })
    expect(legeAb('b1', null, START.erlaubteZuege)).toEqual({ art: 'keine' })
  })
})
