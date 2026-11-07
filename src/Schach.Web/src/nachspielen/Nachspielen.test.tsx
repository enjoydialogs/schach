import { fireEvent, screen, waitFor, within } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { API_BASIS } from '../api/client'
import { mitFakeServer, zeigeApp } from '../components/testApp'

// Nachspielen (SCHACH-SPEC 10.8).

const t = mitFakeServer()

/** Partie mit 1. e4, Gegnerantwort, 2. d4, Gegnerantwort, dann Aufgabe (4 Halbzuege). */
function beendetePartie() {
  const p = t.fake.starte({ gegnerId: 'oscar', farbe: 'Weiss', bedenkzeit: 'OhneUhr' })
  t.fake.ziehe(p.id, { halbzug: 0, zug: 'e2e4' })
  t.fake.ziehe(p.id, { halbzug: 2, zug: 'd2d4' })
  return t.fake.gibAuf(p.id, { halbzug: 4 })
}

const status = () => screen.getByTestId('statuszeile')
const knopf = (name: string) => screen.getByRole('button', { name })
const feld = (name: string) => document.querySelector<HTMLElement>(`[data-square="${name}"]`)!

describe('Nachspielen', () => {
  it('startet bei Halbzug 0 mit Kopf, Spruch und Grundstellung', async () => {
    const z = beendetePartie()
    zeigeApp(`/nachspielen/${z.id}`)

    expect(await screen.findByRole('heading', { name: 'Gegen Oscar · Niederlage' })).toBeInTheDocument()
    expect(status()).toHaveTextContent('Grundstellung')
    expect(screen.getByText('Na dann, auf eine schöne Partie!')).toBeInTheDocument()
    expect(knopf('Anfang')).toBeDisabled()
    expect(knopf('Zurück')).toBeDisabled()
    expect(feld('e2').querySelector('svg')).not.toBeNull()
  })

  it('geht mit Vor, Zurueck, Ende und Anfang durch die Partie', async () => {
    const z = beendetePartie()
    zeigeApp(`/nachspielen/${z.id}`)
    await screen.findByText('Grundstellung')

    fireEvent.click(knopf('Vor'))
    expect(status()).toHaveTextContent('1. e4')
    expect(screen.queryByText('Na dann, auf eine schöne Partie!')).toBeNull()
    await waitFor(() => expect(feld('e4').querySelector('svg')).not.toBeNull())

    fireEvent.click(knopf('Vor'))
    expect(status()).toHaveTextContent(/^1\. … /)

    fireEvent.click(knopf('Zurück'))
    expect(status()).toHaveTextContent('1. e4')

    fireEvent.click(knopf('Ende'))
    expect(status()).toHaveTextContent('Aufgabe. Du hast die Partie aufgegeben.')
    expect(knopf('Vor')).toBeDisabled()
    expect(knopf('Ende')).toBeDisabled()
    // Spruch am letzten Halbzug: der Sieg-Spruch des Gegners.
    expect(screen.getByText('Diesmal hat es für mich gereicht.')).toBeInTheDocument()

    fireEvent.click(knopf('Anfang'))
    expect(status()).toHaveTextContent('Grundstellung')
  })

  it('springt per Zug-Chip und markiert den aktiven', async () => {
    const z = beendetePartie()
    zeigeApp(`/nachspielen/${z.id}`)
    const leiste = await screen.findByRole('list', { name: 'Züge' })
    const chips = within(leiste).getAllByRole('button')
    expect(chips).toHaveLength(4)
    expect(chips[0]).toHaveTextContent('1. e4')
    expect(chips[2]).toHaveTextContent('2. d4')

    fireEvent.click(chips[2]!)
    expect(status()).toHaveTextContent('2. d4')
    expect(chips[2]).toHaveAttribute('aria-current', 'true')
  })

  it('leitet eine offene Partie zur Partie um', async () => {
    const p = t.fake.starte({ gegnerId: 'ilse', farbe: 'Weiss', bedenkzeit: 'OhneUhr' })
    const router = zeigeApp(`/nachspielen/${p.id}`)
    await waitFor(() => expect(router.state.location.pathname).toBe(`/partie/${p.id}`))
  })

  it('teilt das PGN per Web Share mit Dateiname', async () => {
    const z = beendetePartie()
    const share = vi.fn().mockResolvedValue(undefined)
    Object.assign(navigator, { canShare: () => true, share })
    try {
      zeigeApp(`/nachspielen/${z.id}`)
      fireEvent.click(await screen.findByRole('button', { name: 'Menü' }))
      fireEvent.click(screen.getByRole('button', { name: 'PGN teilen' }))

      await waitFor(() => expect(share).toHaveBeenCalledOnce())
      const datei = (share.mock.calls[0]![0] as { files: File[] }).files[0]!
      expect(datei.name).toMatch(/^schach-oscar-\d{4}-\d{2}-\d{2}\.pgn$/)
      expect(await datei.text()).toContain('[Result "0-1"]')
    } finally {
      Reflect.deleteProperty(navigator, 'canShare')
      Reflect.deleteProperty(navigator, 'share')
    }
  })

  it('laedt das PGN herunter, wenn Teilen nicht geht', async () => {
    const z = beendetePartie()
    const erzeuge = vi.fn(() => 'blob:pgn')
    Object.assign(URL, { createObjectURL: erzeuge, revokeObjectURL: vi.fn() })
    const klick = vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(function (this: HTMLAnchorElement) {
      expect(this.download).toMatch(/^schach-oscar-.*\.pgn$/)
    })
    zeigeApp(`/nachspielen/${z.id}`)
    fireEvent.click(await screen.findByRole('button', { name: 'Menü' }))
    fireEvent.click(screen.getByRole('button', { name: 'PGN teilen' }))
    await waitFor(() => expect(klick).toHaveBeenCalledOnce())
    expect(erzeuge).toHaveBeenCalledOnce()
    klick.mockRestore()
  })

  it('fuehrt aus dem Menue zur Partienliste', async () => {
    const z = beendetePartie()
    const router = zeigeApp(`/nachspielen/${z.id}`)
    fireEvent.click(await screen.findByRole('button', { name: 'Menü' }))
    fireEvent.click(screen.getByRole('button', { name: 'Zur Partienliste' }))
    await waitFor(() => expect(router.state.location.pathname).toBe('/partien'))
  })

  it('fuehrt aus dem Menue zur Startseite', async () => {
    const z = beendetePartie()
    const router = zeigeApp(`/nachspielen/${z.id}`)
    fireEvent.click(await screen.findByRole('button', { name: 'Menü' }))
    fireEvent.click(screen.getByRole('button', { name: 'Zur Startseite' }))
    await waitFor(() => expect(router.state.location.pathname).toBe('/'))
  })

  it('zeigt ohne Netz und ohne Cache den Offline-Hinweis', async () => {
    t.server.use(http.get(`${API_BASIS}/partien/:id`, () => HttpResponse.error()))
    zeigeApp('/nachspielen/fake-unbekannt')
    expect(
      await screen.findByText(
        'Diese Partie ist offline nicht verfügbar. Offline kannst du nur Partien nachspielen, die du schon einmal geöffnet hast.',
      ),
    ).toBeInTheDocument()
  })
})
