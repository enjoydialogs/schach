import { leseEinstellungen, schreibeEinstellungen, SpeicherSchluessel } from './einstellungen'

afterEach(() => localStorage.clear())

describe('Einstellungen', () => {
  it('sind ohne Eintrag alle an, bei voller Lautstaerke', () => {
    expect(leseEinstellungen()).toEqual({
      erlaubteZuegeAnzeigen: true,
      vorUngedecktenWarnen: true,
      toeneUndVibration: true,
      spruecheVorlesen: true,
      toeneLautstaerke: 1,
      spruecheLautstaerke: 1,
    })
  })

  it('gelten bei Parse-Fehler als an', () => {
    localStorage.setItem('einstellungen', '{kaputt')
    expect(leseEinstellungen().vorUngedecktenWarnen).toBe(true)
    expect(leseEinstellungen().spruecheLautstaerke).toBe(1)
  })

  it('lesen nur false als aus, fehlende Werte als an', () => {
    localStorage.setItem('einstellungen', JSON.stringify({ toeneUndVibration: false, vorUngedecktenWarnen: 'nein' }))
    expect(leseEinstellungen()).toEqual({
      erlaubteZuegeAnzeigen: true,
      vorUngedecktenWarnen: true,
      toeneUndVibration: false,
      spruecheVorlesen: true,
      toeneLautstaerke: 1,
      spruecheLautstaerke: 1,
    })
  })

  it('lesen eine gespeicherte Lautstaerke', () => {
    localStorage.setItem('einstellungen', JSON.stringify({ toeneLautstaerke: 0.3, spruecheLautstaerke: 0.6 }))
    expect(leseEinstellungen()).toMatchObject({ toeneLautstaerke: 0.3, spruecheLautstaerke: 0.6 })
  })

  it.each([
    ['laut', 1],
    [null, 1],
    [-1, 0.1],
    [0, 0.1],
    [2, 1],
  ])('begrenzen die Lautstaerke %j auf %d', (gespeichert, erwartet) => {
    localStorage.setItem('einstellungen', JSON.stringify({ spruecheLautstaerke: gespeichert }))
    expect(leseEinstellungen().spruecheLautstaerke).toBe(erwartet)
  })

  it('schreiben JSON unter dem Schluessel einstellungen', () => {
    schreibeEinstellungen({
      erlaubteZuegeAnzeigen: false,
      vorUngedecktenWarnen: true,
      toeneUndVibration: true,
      spruecheVorlesen: false,
      toeneLautstaerke: 0.5,
      spruecheLautstaerke: 0.7,
    })
    expect(SpeicherSchluessel.einstellungen).toBe('einstellungen')
    expect(JSON.parse(localStorage.getItem('einstellungen')!)).toEqual({
      erlaubteZuegeAnzeigen: false,
      vorUngedecktenWarnen: true,
      toeneUndVibration: true,
      spruecheVorlesen: false,
      toeneLautstaerke: 0.5,
      spruecheLautstaerke: 0.7,
    })
  })
})
