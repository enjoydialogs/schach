import { eloAenderung, eloZeile, erklaerung, grundLabel, sichtAuf, titel } from './erklaerung'
import { de } from '../i18n/de'

describe('sichtAuf', () => {
  it('leitet Sieg, Niederlage und Remis aus Ergebnis und Farbe ab', () => {
    expect(sichtAuf('WeissGewinnt', 'Weiss')).toBe('sieg')
    expect(sichtAuf('WeissGewinnt', 'Schwarz')).toBe('niederlage')
    expect(sichtAuf('SchwarzGewinnt', 'Schwarz')).toBe('sieg')
    expect(sichtAuf('Remis', 'Weiss')).toBe('remis')
  })
})

describe('erklaerung', () => {
  it('setzt den Gegnernamen ein', () => {
    expect(erklaerung('Matt', 'sieg', 'Ilse')).toBe(
      'Ilse ist matt: Der König steht im Schach und hat keinen Ausweg mehr.',
    )
    expect(erklaerung('Vereinbarung', 'remis', 'Oscar')).toBe('Du hast Remis angeboten und Oscar hat angenommen.')
  })

  it('liefert null fuer eine Kombination ohne Eintrag', () => {
    expect(erklaerung('Patt', 'sieg', 'Ilse')).toBeNull()
  })

  it('haelt alle Saetze unter 90 Zeichen (mit laengstem Namen)', () => {
    for (const saetze of Object.values(de.partieende.erklaerung)) {
      for (const satz of Object.values(saetze)) {
        expect(satz.replace('{name}', 'Sementa').length).toBeLessThanOrEqual(90)
      }
    }
  })
})

describe('Titel und Grund', () => {
  it('benennt Ausgang und Grund', () => {
    expect(titel('sieg')).toBe('Du hast gewonnen')
    expect(grundLabel('FuenfzigZuege')).toBe('50-Züge-Regel')
  })
})

describe('Elo', () => {
  it('zeigt die Aenderung mit Vorzeichen und echtem Minus', () => {
    expect(eloAenderung(800, 812)).toBe('+12')
    expect(eloAenderung(800, 792)).toBe('−8')
    expect(eloAenderung(800, 800)).toBe('±0')
  })

  it('baut die Elo-Zeile fuer zaehlende, nicht zaehlende und Gastpartien', () => {
    expect(eloZeile({ zaehltFuerElo: true, eloVorher: 800, eloNachher: 812, gast: false })).toBe('Elo 812 (+12)')
    expect(eloZeile({ zaehltFuerElo: false, eloVorher: null, eloNachher: null, gast: false })).toBe('Elo: zählt nicht')
    expect(eloZeile({ zaehltFuerElo: false, eloVorher: null, eloNachher: null, gast: true })).toBeNull()
  })
})
