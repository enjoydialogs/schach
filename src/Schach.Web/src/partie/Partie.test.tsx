import { act, fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import { setupServer } from 'msw/node'
import { MemoryRouter } from 'react-router'
import { afterAll, afterEach, beforeAll, beforeEach, vi } from 'vitest'
import type { Bedenkzeit, ZustandDto } from '../api/types'
import { FakeServer } from '../fake/fakeServer'
import { fakeHandlers } from '../fake/handlers'
import { Partie } from './Partie'
import { setzeSitzungZurueck } from '../components/sitzung'

// Partie-Bildschirm gegen den Fake-Server ueber msw/node: derselbe Vertrag wie im Browser.

const server = setupServer()
let fake: FakeServer

beforeAll(() => {
  server.listen({ onUnhandledFrame: 'error' })
  // jsdom hat kein Layout; react-chessboard misst Felder fuer die Zug-Animation, auch noch
  // waehrend des Aufraeumens nach einem Test.
  vi.spyOn(Element.prototype, 'getBoundingClientRect').mockReturnValue(new DOMRect(0, 0, 48, 48))
})
afterAll(() => {
  server.close()
  vi.restoreAllMocks()
})
beforeEach(() => {
  // Uhr des Fakes 10 s zurueck: der Gegnerzug ist sofort sichtbar, die Tests warten nicht.
  fake = new FakeServer({ jetzt: () => Date.now() - 10_000, zufall: () => 0.25 })
  server.resetHandlers(...fakeHandlers(fake))
  setzeSitzungZurueck()
})
afterEach(() => localStorage.clear())

function starte(bedenkzeit: Bedenkzeit = 'OhneUhr', vorgaben: { gast?: boolean; startFen?: string } = {}): ZustandDto {
  fake.gast = vorgaben.gast ?? false
  fake.vorgaben = { startFen: vorgaben.startFen }
  return fake.starte({ gegnerId: 'ilse', farbe: 'Weiss', bedenkzeit })
}

async function zeige(z: ZustandDto) {
  render(
    <MemoryRouter>
      <Partie id={z.id} />
    </MemoryRouter>,
  )
  await screen.findByText('Ilse')
}

const knopf = (name: string) => screen.getByRole('button', { name })
const feld = (name: string) => document.querySelector<HTMLElement>(`[data-square="${name}"]`)!

describe('Partie-Bildschirm', () => {
  it('zeigt ohne Bedenkzeit vier Knoepfe; Zuruecknehmen erst nach dem eigenen Zug', async () => {
    await zeige(starte())

    expect(knopf('Zurücknehmen')).toHaveAttribute('aria-disabled', 'true')
    expect(knopf('Tipp')).not.toHaveAttribute('aria-disabled')
    expect(knopf('Remis anbieten')).not.toHaveAttribute('aria-disabled')
    expect(knopf('Aufgeben')).not.toHaveAttribute('aria-disabled')
    // Im Spiel ist das Brett der Blickfang: alle Knoepfe leise, keiner primaer (10.3).
    expect(document.querySelectorAll('.leistenknopf--leise')).toHaveLength(4)
    expect(document.querySelector('.leistenknopf--primaer')).toBeNull()
    expect(screen.queryAllByRole('timer')).toHaveLength(0)

    fireEvent.click(feld('e2'))
    fireEvent.click(feld('e4'))

    await waitFor(() => expect(knopf('Zurücknehmen')).not.toHaveAttribute('aria-disabled'))
  })

  it('erklaert einen gesperrten Knopf in der Statuszeile, statt zu handeln', async () => {
    await zeige(starte())

    fireEvent.click(knopf('Zurücknehmen'))

    const status = screen.getByTestId('statuszeile')
    expect(status).toHaveTextContent('Zurücknehmen geht nach deinem ersten Zug.')
    expect(status).toHaveClass('status--hinweis')
    expect(screen.queryByRole('dialog')).toBeNull()
  })

  it('zeigt mit 5+3 nur Remis anbieten und Aufgeben und zwei Uhren', async () => {
    await zeige(starte('Fischer5Plus3'))

    expect(screen.queryByRole('button', { name: 'Zurücknehmen' })).toBeNull()
    expect(screen.queryByRole('button', { name: 'Tipp' })).toBeNull()
    expect(knopf('Remis anbieten')).not.toHaveAttribute('aria-disabled')
    expect(knopf('Aufgeben')).not.toHaveAttribute('aria-disabled')
    const uhren = screen.getAllByRole('timer')
    expect(uhren).toHaveLength(2)
    expect(screen.getByRole('timer', { name: 'Deine Uhr' })).toHaveClass('uhr--aktiv')
  })

  it('fragt beim ersten Tipp einmal nach der Elo, zeigt Ring und Text, dann den Pfeil', async () => {
    await zeige(starte())

    fireEvent.click(knopf('Tipp'))
    const frage = screen.getByRole('dialog', { name: 'Partie zählt dann nicht für die Elo' })
    fireEvent.click(within(frage).getByRole('button', { name: 'Tipp zeigen' }))

    const status = await screen.findByText(/^Zieh mit/)
    expect(status).toHaveClass('status--tipp')
    expect(screen.getByText('Zählt nicht für die Elo')).toBeInTheDocument()

    fireEvent.click(knopf('Tipp'))
    expect(await screen.findByText(/^\S+ [a-h][1-8] nach [a-h][1-8]$/)).toBeInTheDocument()
    expect(knopf('Tipp')).toHaveAttribute('aria-disabled', 'true')

    // Nach dem Zug kein Tipp mehr und keine zweite Frage.
    fireEvent.click(feld('e2'))
    fireEvent.click(feld('e4'))
    await waitFor(() => expect(knopf('Tipp')).not.toHaveAttribute('aria-disabled'))
    fireEvent.click(knopf('Tipp'))
    expect(screen.queryByRole('dialog')).toBeNull()
    expect(await screen.findByText(/^Zieh mit/)).toBeInTheDocument()
  })

  it('bestaetigt das Aufgeben und zeigt das Endblatt, das sich schliessen laesst', async () => {
    await zeige(starte())

    fireEvent.click(knopf('Aufgeben'))
    const dialog = screen.getByRole('dialog', { name: 'Partie aufgeben?' })
    expect(dialog).toHaveTextContent('Aufgeben zählt als Niederlage und senkt deine Elo.')
    fireEvent.click(within(dialog).getByRole('button', { name: 'Aufgeben' }))

    const blatt = await screen.findByRole('dialog', { name: 'Du hast verloren' })
    expect(blatt).toHaveTextContent('Aufgabe')
    expect(blatt).toHaveTextContent('Du hast die Partie aufgegeben.')
    expect(blatt).toHaveTextContent('Ilse:')
    expect(blatt).toHaveTextContent('Elo 776 (−24)')
    expect(within(blatt).getByRole('button', { name: 'Neue Partie' })).toBeInTheDocument()
    expect(within(blatt).getByRole('button', { name: 'Nachspielen' })).toBeInTheDocument()
    expect(within(blatt).getByRole('link', { name: 'Zur Startseite' })).toHaveAttribute('href', '/')

    fireEvent.click(within(blatt).getByRole('button', { name: 'Schließen' }))
    expect(screen.queryByRole('dialog')).toBeNull()
    expect(document.querySelectorAll('[data-square]')).toHaveLength(64)
    expect(knopf('Nachspielen')).toHaveClass('leistenknopf--leise')
    // Nach dem Ende ist „Neue Partie“ der eine Hauptknopf (10.3).
    expect(knopf('Neue Partie')).toHaveClass('leistenknopf--primaer')
  })

  it('zeigt Gaesten die Pille, den Gast-Text und kein Nachspielen', async () => {
    await zeige(starte('OhneUhr', { gast: true }))
    await screen.findByText('Gast')

    fireEvent.click(knopf('Tipp'))
    expect(screen.queryByRole('dialog')).toBeNull()
    await screen.findByText(/^Zieh mit/)

    fireEvent.click(knopf('Aufgeben'))
    const dialog = screen.getByRole('dialog', { name: 'Partie aufgeben?' })
    expect(dialog).toHaveTextContent('Die Gastpartie endet als Niederlage und wird nicht gespeichert.')
    fireEvent.click(within(dialog).getByRole('button', { name: 'Aufgeben' }))

    const blatt = await screen.findByRole('dialog', { name: 'Du hast verloren' })
    expect(within(blatt).queryByRole('button', { name: 'Nachspielen' })).toBeNull()
    expect(within(blatt).getByRole('link', { name: /Konto anlegen/ })).toHaveAttribute('href', '/anmelden')
  })

  it('warnt vor einer ungedeckten Figur und schweigt mit ausgeschaltetem Schalter', async () => {
    await zeige(starte('OhneUhr', { startFen: 'r3k3/ppp2ppp/8/8/1b6/2N5/P4PPP/R5K1 w q - 0 1' }))

    expect(screen.getByTestId('statuszeile')).toHaveTextContent('Dein Springer auf c3 ist angegriffen und ungedeckt.')

    fireEvent.click(screen.getByRole('button', { name: 'Menü' }))
    fireEvent.click(screen.getByRole('switch', { name: 'Vor ungedeckten Figuren warnen' }))
    expect(screen.getByTestId('statuszeile')).toBeEmptyDOMElement()
    expect(JSON.parse(localStorage.getItem('einstellungen')!)).toMatchObject({ vorUngedecktenWarnen: false })
  })

  it('setzt einen abgelehnten Zug zurueck und meldet „Zug nicht möglich“', async () => {
    const z = starte()
    await zeige(z)
    // Ein zweiter Tab hat inzwischen gezogen: der Halbzug des Clients ist veraltet.
    act(() => {
      fake.ziehe(z.id, { halbzug: 0, zug: 'd2d4' })
    })

    fireEvent.click(feld('e2'))
    fireEvent.click(feld('e4'))

    expect(await screen.findByText('Zug nicht möglich')).toBeInTheDocument()
    // Der Client uebernimmt den Zustand aus dem 409: d4 steht, e2 ist noch da.
    await waitFor(() => expect(feld('d4').querySelector('svg')).not.toBeNull())
    expect(feld('e2').querySelector('svg')).not.toBeNull()
  })
})
