import type { TippDto } from '../api/types'
import { aktuellerTipp, naechsterTippSchritt, tippMarkierung, tippText, zeigePfeil, type TippStand } from './tipp'

const SPRINGER: TippDto = { uci: 'c3e5', von: 'c3', nach: 'e5', figur: 'Springer' }
const stufe1: TippStand = { halbzug: 6, stufe: 1, tipp: SPRINGER }

describe('Tipp-Stufen', () => {
  it('ruft beim ersten Tippen den Server', () => {
    expect(naechsterTippSchritt(null, 6)).toBe('abrufen')
  })

  it('zeigt beim zweiten Tippen den Pfeil ohne Serveraufruf', () => {
    expect(naechsterTippSchritt(stufe1, 6)).toBe('pfeil')
    expect(zeigePfeil(stufe1)).toEqual({ ...stufe1, stufe: 2 })
  })

  it('ist nach dem Pfeil fertig', () => {
    expect(naechsterTippSchritt(zeigePfeil(stufe1), 6)).toBe('fertig')
  })

  it('vergisst den Tipp nach einem neuen Halbzug', () => {
    expect(aktuellerTipp(stufe1, 8)).toBeNull()
    expect(naechsterTippSchritt(zeigePfeil(stufe1), 8)).toBe('abrufen')
  })
})

describe('Tipp-Texte', () => {
  it('nennt zuerst nur die Figur, dann den Zug', () => {
    expect(tippText(stufe1)).toBe('Zieh mit dem Springer')
    expect(tippText(zeigePfeil(stufe1))).toBe('Springer c3 nach e5')
  })

  it('beugt Dame und Bauer', () => {
    expect(tippText({ ...stufe1, tipp: { ...SPRINGER, figur: 'Dame' } })).toBe('Zieh mit der Dame')
    expect(tippText({ ...stufe1, tipp: { uci: 'e2e4', von: 'e2', nach: 'e4', figur: 'Bauer' } })).toBe(
      'Zieh mit dem Bauern',
    )
  })
})

describe('Tipp-Markierung', () => {
  it('zeigt erst den Ring, dann Ring und Pfeil', () => {
    expect(tippMarkierung(null)).toEqual({ feld: null, pfeil: null })
    expect(tippMarkierung(stufe1)).toEqual({ feld: 'c3', pfeil: null })
    expect(tippMarkierung(zeigePfeil(stufe1))).toEqual({ feld: 'c3', pfeil: { von: 'c3', nach: 'e5' } })
  })
})
