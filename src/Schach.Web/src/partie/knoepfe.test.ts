import { knoepfe, zugnummer, type KnopfKontext } from './knoepfe'
import { zug, zustand } from './testdaten'

const FREI: KnopfKontext = { gast: false, sperre: null, tippFertig: false }
const nachE4E5 = {
  halbzug: 2,
  fen: 'rnbqkbnr/pppp1ppp/8/4p3/4P3/8/PPPP1PPP/RNBQKBNR w KQkq - 0 2',
  zugfolge: [zug(1, 'e2e4', 'e4'), zug(2, 'e7e5', 'e5')],
}

const zustaende = (k: ReturnType<typeof knoepfe>) => Object.fromEntries(k.map((knopf) => [knopf.art, knopf.aktiv]))
const gruende = (k: ReturnType<typeof knoepfe>) => Object.fromEntries(k.map((knopf) => [knopf.art, knopf.grund]))

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
    expect(Object.values(zustaende(knoepfe(zustand(nachE4E5), { ...FREI, sperre: 'beschaeftigt' })))).toEqual([
      false,
      false,
      false,
      false,
    ])
  })
})

describe('Gruende gesperrter Knoepfe', () => {
  it('hat fuer aktive Knoepfe keinen Grund', () => {
    expect(gruende(knoepfe(zustand(nachE4E5), FREI))).toEqual({
      zuruecknehmen: null,
      tipp: null,
      remis: null,
      aufgeben: null,
    })
  })

  it('erklaert Zuruecknehmen vor dem ersten eigenen Zug', () => {
    expect(gruende(knoepfe(zustand(), FREI)).zuruecknehmen).toBe('Zurücknehmen geht nach deinem ersten Zug.')
  })

  it('erklaert Tipp ausserhalb des eigenen Zugs und nach dem Pfeil', () => {
    expect(gruende(knoepfe(zustand({ amZug: false, erlaubteZuege: [] }), FREI)).tipp).toBe(
      'Tipp gibt es, wenn du am Zug bist.',
    )
    expect(gruende(knoepfe(zustand(), { ...FREI, tippFertig: true })).tipp).toBe('Mehr Tipp gibt es für diesen Zug nicht.')
  })

  it('zaehlt die Zuege bis zum naechsten Remis-Angebot', () => {
    const remis = (ab: number) => gruende(knoepfe(zustand({ ...nachE4E5, remisAngebotMoeglichAbZug: ab }), FREI)).remis
    expect(remis(5)).toBe('Remis kannst du in 3 Zügen wieder anbieten.')
    expect(remis(3)).toBe('Remis kannst du in einem Zug wieder anbieten.')
    expect(remis(2)).toBeNull()
  })

  it('nennt bei gesperrter Eingabe fuer alle Knoepfe denselben Grund', () => {
    const grund = (sperre: KnopfKontext['sperre']) =>
      new Set(Object.values(gruende(knoepfe(zustand(nachE4E5), { ...FREI, sperre }))))
    expect(grund('gegnerDenkt')).toEqual(new Set(['Warte, bis Ilse gezogen hat.']))
    expect(grund('beschaeftigt')).toEqual(new Set(['Einen Moment, das läuft noch.']))
    expect(grund('offline')).toEqual(new Set(['Keine Verbindung']))
  })

  it('laesst die Sperre vor dem eigenen Grund gelten', () => {
    expect(gruende(knoepfe(zustand(), { ...FREI, sperre: 'gegnerDenkt' })).zuruecknehmen).toBe(
      'Warte, bis Ilse gezogen hat.',
    )
  })
})

describe('Knopfleiste mit Bedenkzeit', () => {
  it('zeigt nur Remis anbieten und Aufgeben', () => {
    const k = knoepfe(zustand({ ...nachE4E5, bedenkzeit: 'Fischer5Plus3' }), FREI)
    expect(k).toEqual([
      { art: 'remis', aktiv: true, grund: null },
      { art: 'aufgeben', aktiv: true, grund: null },
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
