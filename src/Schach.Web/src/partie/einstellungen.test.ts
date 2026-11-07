import { leseEinstellungen, schreibeEinstellungen, SpeicherSchluessel } from './einstellungen'

afterEach(() => localStorage.clear())

describe('Einstellungen', () => {
  it('sind ohne Eintrag alle an', () => {
    expect(leseEinstellungen()).toEqual({
      erlaubteZuegeAnzeigen: true,
      vorUngedecktenWarnen: true,
      toeneUndVibration: true,
    })
  })

  it('gelten bei Parse-Fehler als an', () => {
    localStorage.setItem('einstellungen', '{kaputt')
    expect(leseEinstellungen().vorUngedecktenWarnen).toBe(true)
  })

  it('lesen nur false als aus, fehlende Werte als an', () => {
    localStorage.setItem('einstellungen', JSON.stringify({ toeneUndVibration: false, vorUngedecktenWarnen: 'nein' }))
    expect(leseEinstellungen()).toEqual({
      erlaubteZuegeAnzeigen: true,
      vorUngedecktenWarnen: true,
      toeneUndVibration: false,
    })
  })

  it('schreiben JSON unter dem Schluessel einstellungen', () => {
    schreibeEinstellungen({ erlaubteZuegeAnzeigen: false, vorUngedecktenWarnen: true, toeneUndVibration: true })
    expect(SpeicherSchluessel.einstellungen).toBe('einstellungen')
    expect(JSON.parse(localStorage.getItem('einstellungen')!)).toEqual({
      erlaubteZuegeAnzeigen: false,
      vorUngedecktenWarnen: true,
      toeneUndVibration: true,
    })
  })
})
