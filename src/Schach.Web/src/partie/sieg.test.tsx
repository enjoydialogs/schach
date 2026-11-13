import { fireEvent, render, screen, within } from '@testing-library/react'
import { setupServer } from 'msw/node'
import { MemoryRouter } from 'react-router'
import { afterAll, afterEach, beforeAll, beforeEach, vi } from 'vitest'
import type { ZustandDto } from '../api/types'
import { setzeSitzungZurueck } from '../components/sitzung'
import { FakeServer } from '../fake/fakeServer'
import { fakeHandlers } from '../fake/handlers'
import { Partie } from './Partie'
import { VIBRATION_ENDE, VIBRATION_SIEG } from './toene'

// Sieg-Moment im Partieende-Blatt (SCHACH-SPEC 10.3): Pokal-Band, Konfetti und Feuerwerk nur beim frischen Sieg,
// Elo-Abzeichen, eigene Sieg-Vibration. Niederlage bleibt ruhig.

// Weiss setzt mit Ta1-a8 matt (dieselbe Stellung wie der Prototyp „Matt in 1“).
const MATT_IN_EINS = '6k1/5ppp/8/8/8/8/8/R5K1 w - - 0 1'

const server = setupServer()
let fake: FakeServer
const vibrate = vi.fn()

beforeAll(() => {
  server.listen({ onUnhandledFrame: 'error' })
  vi.spyOn(Element.prototype, 'getBoundingClientRect').mockReturnValue(new DOMRect(0, 0, 48, 48))
  Object.defineProperty(navigator, 'vibrate', { value: vibrate, configurable: true })
})
afterAll(() => {
  server.close()
  vi.restoreAllMocks()
})
beforeEach(() => {
  fake = new FakeServer({ jetzt: () => Date.now() - 10_000, zufall: () => 0.25 })
  server.resetHandlers(...fakeHandlers(fake))
  setzeSitzungZurueck()
  vibrate.mockClear()
})
afterEach(() => localStorage.clear())

function starte(startFen?: string): ZustandDto {
  fake.gast = false
  fake.vorgaben = { startFen }
  return fake.starte({ gegnerId: 'ilse', farbe: 'Weiss', bedenkzeit: 'OhneUhr' })
}

async function zeige(z: ZustandDto) {
  render(
    <MemoryRouter>
      <Partie id={z.id} />
    </MemoryRouter>,
  )
  await screen.findByText('Ilse')
}

const feld = (name: string) => document.querySelector<HTMLElement>(`[data-square="${name}"]`)!
const konfetti = (blatt: HTMLElement) => blatt.querySelectorAll('.sieg__konfetti')

describe('Sieg-Moment', () => {
  it('pulst die Sieg-Vibration auf jedem Feuerwerks-Knall (SCHACH-SPEC 10.3)', () => {
    expect(VIBRATION_SIEG).toEqual([40, 550, 30, 250, 30, 250, 60])
  })

  it('feiert einen frischen Sieg mit Pokal, Konfetti, Feuerwerk, Elo-Abzeichen und Sieg-Vibration', async () => {
    const start = starte(MATT_IN_EINS)
    await zeige(start)

    fireEvent.click(feld('a1'))
    fireEvent.click(feld('a8'))

    const blatt = await screen.findByRole('dialog', { name: 'Du hast gewonnen' })
    expect(blatt).toHaveTextContent('Matt')
    expect(within(blatt).getByTestId('sieg-band')).toHaveClass('sieg--feiern')
    expect(konfetti(blatt).length).toBeGreaterThan(0)
    // Feuerwerk ueber dem Brett, nicht im Blatt
    const feuerwerk = screen.getByTestId('feuerwerk')
    expect(feuerwerk.closest('.partie__brett')).not.toBeNull()
    expect(feuerwerk.querySelectorAll('.feuerwerk__funke').length).toBeGreaterThan(0)

    const z = fake.lies(start.id)
    const plus = `+${z.eloNachher! - z.eloVorher!}`
    expect(blatt.querySelector('.sieg__plus')).toHaveTextContent(plus)
    expect(blatt).toHaveTextContent(`Elo ${z.eloNachher} ${plus}`)
    expect(vibrate).toHaveBeenLastCalledWith(VIBRATION_SIEG)
  })

  it('zeigt beim Oeffnen eines schon gewonnenen Spiels den Pokal, aber kein Konfetti, kein Feuerwerk und keine Vibration', async () => {
    const z = starte(MATT_IN_EINS)
    fake.ziehe(z.id, { halbzug: z.halbzug, zug: 'a1a8' })
    await zeige(z)

    const blatt = await screen.findByRole('dialog', { name: 'Du hast gewonnen' })
    const band = within(blatt).getByTestId('sieg-band')
    expect(band).not.toHaveClass('sieg--feiern')
    expect(konfetti(blatt)).toHaveLength(0)
    expect(screen.queryByTestId('feuerwerk')).toBeNull()
    expect(blatt.querySelector('.sieg__plus')).not.toBeNull()
    expect(vibrate).not.toHaveBeenCalled()
  })

  it('bleibt bei einer Niederlage ruhig: kein Pokal, kein Abzeichen, normale Ende-Vibration', async () => {
    await zeige(starte())

    fireEvent.click(screen.getByRole('button', { name: 'Aufgeben' }))
    const dialog = screen.getByRole('dialog', { name: 'Partie aufgeben?' })
    fireEvent.click(within(dialog).getByRole('button', { name: 'Aufgeben' }))

    const blatt = await screen.findByRole('dialog', { name: 'Du hast verloren' })
    expect(within(blatt).queryByTestId('sieg-band')).toBeNull()
    expect(screen.queryByTestId('feuerwerk')).toBeNull()
    expect(blatt.querySelector('.sieg__plus')).toBeNull()
    expect(blatt).toHaveTextContent('Elo 776 (−24)')
    expect(vibrate).toHaveBeenLastCalledWith(VIBRATION_ENDE)
  })
})

