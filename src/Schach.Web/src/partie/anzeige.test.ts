import { nachZug, schachFeld, sichtbarerSpruch, sichtbareStellung } from './anzeige'
import { START_FEN, zug, zustand } from './testdaten'

const NACH_E4 = 'rnbqkbnr/pppppppp/8/8/4P3/8/PPPP1PPP/RNBQKBNR b KQkq - 0 1'
const NACH_E4E5 = 'rnbqkbnr/pppp1ppp/8/4p3/4P3/8/PPPP1PPP/RNBQKBNR w KQkq - 0 2'
const T = Date.parse('2026-11-07T10:00:02.000Z')

const mitGegnerzug = zustand({
  halbzug: 2,
  fen: NACH_E4E5,
  zugfolge: [zug(1, 'e2e4', 'e4'), zug(2, 'e7e5', 'e5')],
  gegnerZug: { ...zug(2, 'e7e5', 'e5'), sichtbarAb: '2026-11-07T10:00:03.000Z', spruch: null },
})

describe('sichtbareStellung', () => {
  it('zeigt vor sichtbarAb die Stellung ohne Gegnerzug und meldet Nachdenken', () => {
    expect(sichtbareStellung(mitGegnerzug, T)).toEqual({
      fen: NACH_E4,
      letzterZug: { von: 'e2', nach: 'e4' },
      halbzug: 1,
      gegnerDenkt: true,
    })
  })

  it('nimmt die optimistische Stellung, wenn vorhanden', () => {
    expect(sichtbareStellung(mitGegnerzug, T, 'optimistisch').fen).toBe('optimistisch')
  })

  it('zeigt ab sichtbarAb den Gegnerzug', () => {
    expect(sichtbareStellung(mitGegnerzug, T + 1_000)).toEqual({
      fen: NACH_E4E5,
      letzterZug: { von: 'e7', nach: 'e5' },
      halbzug: 2,
      gegnerDenkt: false,
    })
  })

  it('meldet Nachdenken bei ausstehendem Gegnerzug', () => {
    const z = zustand({ halbzug: 1, fen: NACH_E4, zugfolge: [zug(1, 'e2e4', 'e4')], gegnerZugAusstehend: true })
    expect(sichtbareStellung(z, T).gegnerDenkt).toBe(true)
  })

  it('zeigt die Grundstellung ohne letzten Zug', () => {
    expect(sichtbareStellung(zustand(), T)).toMatchObject({ fen: START_FEN, letzterZug: null, halbzug: 0 })
  })
})

describe('sichtbarerSpruch', () => {
  const sprueche = [
    { nachHalbzug: 0, ereignis: 'Partiebeginn' as const, text: 'Na dann.' },
    { nachHalbzug: 2, ereignis: 'Zug' as const, text: 'Ach so.' },
  ]

  it('nimmt den letzten Spruch bis zum sichtbaren Halbzug', () => {
    expect(sichtbarerSpruch(sprueche, 1)?.text).toBe('Na dann.')
    expect(sichtbarerSpruch(sprueche, 2)?.text).toBe('Ach so.')
    expect(sichtbarerSpruch([], 2)).toBeNull()
  })
})

describe('schachFeld', () => {
  it('findet den Koenig im Schach', () => {
    expect(schachFeld('rnbqkbnr/ppppp2p/5p2/6pQ/4P3/8/PPPP1PPP/RNB1KBNR b KQkq - 1 3')).toBe('e8')
    expect(schachFeld(START_FEN)).toBeNull()
  })
})

describe('nachZug', () => {
  it('zieht optimistisch und erkennt Schlaege', () => {
    expect(nachZug(START_FEN, 'e2e4')).toEqual({ fen: NACH_E4, schlag: false })
    expect(nachZug('4k3/8/8/3p4/4P3/8/8/4K3 w - - 0 1', 'e4d5')?.schlag).toBe(true)
    expect(nachZug(START_FEN, 'e2e5')).toBeNull()
  })
})
