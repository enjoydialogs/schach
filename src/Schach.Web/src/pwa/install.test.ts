import { fangeInstallPrompt, istStandalone, SPAETER_MS, zeigeInstallKarte, type InstallLage } from './install'

// Regel der Install-Karte (SCHACH-SPEC 10.13).

const JETZT = Date.parse('2026-11-07T12:00:00Z')
const passt: InstallLage = { promptGefangen: true, standalone: false, beendetePartie: true, spaeterAm: null, jetzt: JETZT }
const vor = (ms: number) => new Date(JETZT - ms).toISOString()

describe('zeigeInstallKarte', () => {
  it('zeigt die Karte, wenn alles passt', () => {
    expect(zeigeInstallKarte(passt)).toBe(true)
  })

  it('braucht ein gefangenes beforeinstallprompt', () => {
    expect(zeigeInstallKarte({ ...passt, promptGefangen: false })).toBe(false)
  })

  it('zeigt nie im Standalone', () => {
    expect(zeigeInstallKarte({ ...passt, standalone: true })).toBe(false)
  })

  it('braucht mindestens eine beendete Partie', () => {
    expect(zeigeInstallKarte({ ...passt, beendetePartie: false })).toBe(false)
  })

  it('schweigt 14 Tage nach „Später“, danach wieder', () => {
    expect(zeigeInstallKarte({ ...passt, spaeterAm: vor(0) })).toBe(false)
    expect(zeigeInstallKarte({ ...passt, spaeterAm: vor(SPAETER_MS - 1) })).toBe(false)
    expect(zeigeInstallKarte({ ...passt, spaeterAm: vor(SPAETER_MS) })).toBe(true)
  })

  it('wertet einen kaputten Zeitpunkt als nie verschoben', () => {
    expect(zeigeInstallKarte({ ...passt, spaeterAm: 'kaputt' })).toBe(true)
  })
})

describe('istStandalone', () => {
  it('erkennt display-mode standalone und iOS navigator.standalone', () => {
    const fenster = (standalone: boolean, ios = false) =>
      ({ matchMedia: () => ({ matches: standalone }), navigator: { standalone: ios } }) as unknown as Window
    expect(istStandalone(fenster(true))).toBe(true)
    expect(istStandalone(fenster(false, true))).toBe(true)
    expect(istStandalone(fenster(false))).toBe(false)
  })
})

describe('fangeInstallPrompt', () => {
  it('unterdrueckt die Browser-Infoleiste', () => {
    const fenster = new EventTarget() as unknown as Window
    fangeInstallPrompt(fenster)
    const e = new Event('beforeinstallprompt', { cancelable: true })
    fenster.dispatchEvent(e)
    expect(e.defaultPrevented).toBe(true)
  })
})
