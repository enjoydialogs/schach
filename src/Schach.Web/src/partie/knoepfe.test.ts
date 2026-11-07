import { knoepfe, zugnummer, type KnopfKontext } from './knoepfe'
import { zug, zustand } from './testdaten'

const FREI: KnopfKontext = { gast: false, gesperrt: false, tippFertig: false }
const nachE4E5 = {
  halbzug: 2,
  fen: 'rnbqkbnr/pppp1ppp/8/4p3/4P3/8/PPPP1PPP/RNBQKBNR w KQkq - 0 2',
  zugfolge: [zug(1, 'e2e4', 'e4'), zug(2, 'e7e5', 'e5')],
}

const zustaende = (k: ReturnType<typeof knoepfe>) => Object.fromEntries(k.map((knopf) => [knopf.art, knopf.aktiv]))

describe('Knopfleiste ohne Bedenkzeit', () => {
  it('zeigt vier Knoepfe; Zuruecknehmen erst nach eigenem Zug', () => {
    const k = knoepfe(zustand(), FREI)
    expect(k.map((knopf) => knopf.art)).toEqual(['zuruecknehmen', 'tipp', 'remis', 'aufgeben'])
    expect(zustaende(k)).toEqual({ zuruecknehmen: false, tipp: true, remis: true, aufgeben: true })
  })

  it('erlaubt Zuruecknehmen nach eigenem Zug', () => {
    expect(zustaende(knoepfe(zustand(nachE4E5), FREI)).zuruecknehmen).toBe(true)
  })

  it('zaehlt als Schwarz nur eigene Zuege', () => {
    const z = zustand({ spielerFarbe: 'Schwarz', halbzug: 1, zugfolge: [zug(1, 'e2e4', 'e4')] })
    expect(zustaende(knoepfe(z, FREI)).zuruecknehmen).toBe(false)
  })

  it('erlaubt Tipp nur am Zug und nicht nach dem Pfeil', () => {
    expect(zustaende(knoepfe(zustand({ amZug: false, erlaubteZuege: [] }), FREI)).tipp).toBe(false)
    expect(zustaende(knoepfe(zustand(), { ...FREI, tippFertig: true })).tipp).toBe(false)
  })

  it('sperrt Remis bis zur Zugnummer aus remisAngebotMoeglichAbZug', () => {
    expect(zustaende(knoepfe(zustand({ ...nachE4E5, remisAngebotMoeglichAbZug: 3 }), FREI)).remis).toBe(false)
    expect(zustaende(knoepfe(zustand({ ...nachE4E5, remisAngebotMoeglichAbZug: 2 }), FREI)).remis).toBe(true)
  })

  it('schaltet bei gesperrter Eingabe alles aus', () => {
    expect(Object.values(zustaende(knoepfe(zustand(nachE4E5), { ...FREI, gesperrt: true })))).toEqual([
      false,
      false,
      false,
      false,
    ])
  })
})

describe('Knopfleiste mit Bedenkzeit', () => {
  it('zeigt nur Remis anbieten und Aufgeben', () => {
    const k = knoepfe(zustand({ ...nachE4E5, bedenkzeit: 'Fischer5Plus3' }), FREI)
    expect(k).toEqual([
      { art: 'remis', aktiv: true },
      { art: 'aufgeben', aktiv: true },
    ])
  })
})

describe('Knopfleiste nach Partieende', () => {
  const beendet = zustand({ status: 'Beendet', ergebnis: 'SchwarzGewinnt', grund: 'Aufgabe', amZug: false })

  it('zeigt dem Spieler Nachspielen und Neue Partie', () => {
    expect(knoepfe(beendet, FREI).map((knopf) => knopf.art)).toEqual(['nachspielen', 'neuePartie'])
  })

  it('zeigt dem Gast nur Neue Partie', () => {
    expect(knoepfe(beendet, { ...FREI, gast: true }).map((knopf) => knopf.art)).toEqual(['neuePartie'])
  })
})

describe('zugnummer', () => {
  it('liest FEN-Feld 6', () => {
    expect(zugnummer(nachE4E5.fen)).toBe(2)
    expect(zugnummer('kaputt')).toBe(1)
  })
})
