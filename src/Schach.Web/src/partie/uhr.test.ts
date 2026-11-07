import type { UhrDto } from '../api/types'
import { formatiereUhr, restMs, uhrVersatz, uhrZustand } from './uhr'

const UHR: UhrDto = {
  spielerRestMs: 300_000,
  gegnerRestMs: 290_000,
  laeuftFuer: 'Spieler',
  amZugSeit: '2026-11-07T10:00:00.000Z',
  serverZeit: '2026-11-07T10:00:05.000Z',
}
const T0 = Date.parse('2026-11-07T10:00:05.000Z')

describe('formatiereUhr', () => {
  it('zeigt mm:ss', () => {
    expect(formatiereUhr(300_000)).toBe('05:00')
    expect(formatiereUhr(90_000)).toBe('01:30')
    expect(formatiereUhr(9_000)).toBe('00:09')
  })

  it('rundet angefangene Sekunden auf und endet bei 00:00', () => {
    expect(formatiereUhr(59_001)).toBe('01:00')
    expect(formatiereUhr(400)).toBe('00:01')
    expect(formatiereUhr(0)).toBe('00:00')
    expect(formatiereUhr(-50)).toBe('00:00')
  })
})

describe('restMs', () => {
  it('zieht bei der laufenden Seite die Zeit seit amZugSeit ab', () => {
    // Client und Server gleich: 5 s seit amZugSeit.
    expect(restMs(UHR, 'Spieler', T0, 0)).toBe(295_000)
    expect(restMs(UHR, 'Spieler', T0 + 1_000, 0)).toBe(294_000)
  })

  it('laesst die ruhende Seite stehen', () => {
    expect(restMs(UHR, 'Gegner', T0 + 10_000, 0)).toBe(290_000)
  })

  it('rechnet mit dem Versatz zur Serverzeit', () => {
    // Clientuhr geht 60 s nach.
    const empfangen = T0 - 60_000
    const versatz = uhrVersatz(UHR, empfangen)
    expect(versatz).toBe(60_000)
    expect(restMs(UHR, 'Spieler', empfangen, versatz)).toBe(295_000)
  })

  it('haelt die Uhr an, solange amZugSeit in der Zukunft liegt', () => {
    const spaeter = { ...UHR, amZugSeit: '2026-11-07T10:00:07.000Z' }
    expect(restMs(spaeter, 'Spieler', T0, 0)).toBe(300_000)
  })

  it('wird nie negativ', () => {
    expect(restMs(UHR, 'Spieler', T0 + 600_000, 0)).toBe(0)
  })

  it('steht ganz ohne laufende Seite (Partie beendet)', () => {
    expect(restMs({ ...UHR, laeuftFuer: null }, 'Spieler', T0 + 50_000, 0)).toBe(300_000)
  })
})

describe('uhrZustand', () => {
  it('hebt die aktive Uhr hervor und faerbt unter 20 s rot', () => {
    expect(uhrZustand(60_000, true)).toBe('aktiv')
    expect(uhrZustand(60_000, false)).toBe('ruht')
    expect(uhrZustand(19_999, true)).toBe('knapp')
    expect(uhrZustand(20_000, true)).toBe('aktiv')
  })
})
