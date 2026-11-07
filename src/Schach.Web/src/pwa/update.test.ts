import type { RegisterSWOptions } from 'vite-plugin-pwa/types'
import { setzeUpdateZurueck, starteUpdates, STUNDE_MS, updateFaellig } from './update'

// Update-Pruefung (SCHACH-SPEC 10.13): bei Sichtbarkeit, hoechstens stuendlich.

describe('updateFaellig', () => {
  it('ist ohne letzte Pruefung und nach einer Stunde faellig, vorher nicht', () => {
    expect(updateFaellig(null, 0)).toBe(true)
    expect(updateFaellig(1_000, 1_000 + STUNDE_MS - 1)).toBe(false)
    expect(updateFaellig(1_000, 1_000 + STUNDE_MS)).toBe(true)
  })
})

describe('starteUpdates', () => {
  afterEach(() => setzeUpdateZurueck())

  function aufbau() {
    let uhr = 0
    const dokument = Object.assign(new EventTarget(), { visibilityState: 'visible' }) as unknown as Document & {
      visibilityState: DocumentVisibilityState
    }
    const registrierung = { update: vi.fn(() => Promise.resolve()) }
    let optionen: RegisterSWOptions = {}
    starteUpdates(
      (o) => {
        optionen = o
        return () => Promise.resolve()
      },
      { jetzt: () => uhr, dokument },
    )
    optionen.onRegisteredSW?.('/sw.js', registrierung as unknown as ServiceWorkerRegistration)
    const zeige = (ms: number, sichtbar = true) => {
      uhr = ms
      ;(dokument as { visibilityState: string }).visibilityState = sichtbar ? 'visible' : 'hidden'
      dokument.dispatchEvent(new Event('visibilitychange'))
    }
    return { registrierung, zeige, optionen }
  }

  it('registriert sofort und mit Prompt-Rueckruf', () => {
    const { optionen } = aufbau()
    expect(optionen.immediate).toBe(true)
    expect(optionen.onNeedRefresh).toBeTypeOf('function')
  })

  it('prueft bei Sichtbarkeit hoechstens stuendlich', () => {
    const { registrierung, zeige } = aufbau()
    zeige(10 * 60 * 1000)
    expect(registrierung.update).not.toHaveBeenCalled()

    zeige(STUNDE_MS)
    expect(registrierung.update).toHaveBeenCalledTimes(1)

    zeige(STUNDE_MS + 30 * 60 * 1000)
    expect(registrierung.update).toHaveBeenCalledTimes(1)

    zeige(3 * STUNDE_MS, false)
    expect(registrierung.update).toHaveBeenCalledTimes(1)

    zeige(3 * STUNDE_MS)
    expect(registrierung.update).toHaveBeenCalledTimes(2)
  })

  it('verbraucht offline keine Pruefung', () => {
    const { registrierung, zeige } = aufbau()
    const online = vi.spyOn(navigator, 'onLine', 'get').mockReturnValue(false)
    zeige(STUNDE_MS)
    expect(registrierung.update).not.toHaveBeenCalled()
    online.mockReturnValue(true)
    zeige(STUNDE_MS + 1)
    expect(registrierung.update).toHaveBeenCalledTimes(1)
    online.mockRestore()
  })
})
