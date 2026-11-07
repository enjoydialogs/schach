import { fireEvent, screen, waitFor, within } from '@testing-library/react'
import { mitFakeServer, zeigeApp } from '../components/testApp'

// Start-Hub (SCHACH-SPEC 10.1, 10.4, 10.5, 10.11).

const t = mitFakeServer()

describe('Start', () => {
  it('zeigt Spielern Elo-Chip, Neue Partie, Partien und Profil', async () => {
    const router = zeigeApp('/')

    const chip = await screen.findByRole('link', { name: 'Elo 800' })
    expect(chip).toHaveAttribute('href', '/profil')
    expect(screen.getByRole('link', { name: 'Neue Partie' })).toHaveAttribute('href', '/neu')
    expect(screen.queryByText(/Offene Partie/)).toBeNull()

    fireEvent.click(screen.getByRole('link', { name: /Partien/ }))
    await waitFor(() => expect(router.state.location.pathname).toBe('/partien'))
  })

  it('erreicht jede Seite mit einem Tipp und kommt per Zurueck zurueck', async () => {
    const router = zeigeApp('/')
    fireEvent.click(await screen.findByRole('link', { name: /Profil/ }))
    await screen.findByRole('heading', { name: 'Profil' })

    fireEvent.click(screen.getByRole('button', { name: 'Zurück' }))
    await waitFor(() => expect(router.state.location.pathname).toBe('/'))
  })

  it('fuehrt per Zurueck ohne Verlauf zur Startseite', async () => {
    const router = zeigeApp('/neu')
    fireEvent.click(await screen.findByRole('button', { name: 'Zurück' }))
    await waitFor(() => expect(router.state.location.pathname).toBe('/'))
  })

  it('zeigt Gaesten Anmelden und die Gast-Hinweis-Karte', async () => {
    t.fake.gast = true
    zeigeApp('/')

    expect(await screen.findByRole('link', { name: 'Anmelden' })).toHaveAttribute('href', '/anmelden')
    expect(
      screen.getByText('Du spielst als Gast. Partien werden nicht gespeichert und zählen nicht für die Elo.'),
    ).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Anmelden oder registrieren' })).toHaveAttribute('href', '/anmelden')
    expect(screen.queryByRole('link', { name: /Profil/ })).toBeNull()
  })

  it('zeigt die offene Partie mit Fortsetzen und fragt bei Neue Partie nach', async () => {
    const offen = t.fake.starte({ gegnerId: 'oscar', farbe: 'Weiss', bedenkzeit: 'Fischer5Plus3' })
    const router = zeigeApp('/')

    expect(await screen.findByText('Offene Partie gegen Oscar')).toBeInTheDocument()
    expect(screen.getByText('5+3')).toBeInTheDocument()

    fireEvent.click(screen.getByRole('button', { name: 'Neue Partie' }))
    const dialog = screen.getByRole('dialog', { name: 'Offene Partie gegen Oscar' })
    expect(dialog).toHaveTextContent('Du kannst nur eine Partie gleichzeitig spielen. Aufgeben zählt als Niederlage.')
    fireEvent.click(within(dialog).getByRole('button', { name: 'Abbrechen' }))
    expect(screen.queryByRole('dialog')).toBeNull()

    fireEvent.click(screen.getByRole('button', { name: 'Fortsetzen' }))
    await waitFor(() => expect(router.state.location.pathname).toBe(`/partie/${offen.id}`))
  })

  it('gibt die offene Partie auf und fuehrt zu Neue Partie', async () => {
    const offen = t.fake.starte({ gegnerId: 'oscar', farbe: 'Weiss', bedenkzeit: 'OhneUhr' })
    const router = zeigeApp('/')

    fireEvent.click(await screen.findByRole('button', { name: 'Neue Partie' }))
    fireEvent.click(screen.getByRole('button', { name: 'Aufgeben und neu starten' }))

    await waitFor(() => expect(router.state.location.pathname).toBe('/neu'))
    expect(t.fake.lies(offen.id)).toMatchObject({ status: 'Beendet', grund: 'Aufgabe' })
  })

  it('findet die laufende Gast-Partie ueber gastPartieId und vergisst eine beendete', async () => {
    t.fake.gast = true
    const offen = t.fake.starte({ gegnerId: 'finn', farbe: 'Weiss', bedenkzeit: 'OhneUhr' })
    localStorage.setItem('gastPartieId', offen.id)
    zeigeApp('/')
    expect(await screen.findByText('Offene Partie gegen Finn')).toBeInTheDocument()
    fireEvent.click(screen.getByRole('button', { name: 'Neue Partie' }))
    expect(screen.getByRole('dialog')).toHaveTextContent('Die Gastpartie geht verloren.')
  })

  it('loescht gastPartieId, wenn die Gast-Partie beendet ist', async () => {
    t.fake.gast = true
    const p = t.fake.starte({ gegnerId: 'finn', farbe: 'Weiss', bedenkzeit: 'OhneUhr' })
    t.fake.gibAuf(p.id, { halbzug: p.halbzug })
    localStorage.setItem('gastPartieId', p.id)
    zeigeApp('/')

    expect(await screen.findByRole('link', { name: 'Neue Partie' })).toBeInTheDocument()
    expect(localStorage.getItem('gastPartieId')).toBeNull()
  })

  it('sperrt offline die Spielknoepfe mit „Spielen braucht Internet“', async () => {
    const online = vi.spyOn(navigator, 'onLine', 'get').mockReturnValue(false)
    try {
      zeigeApp('/')
      expect(
        await screen.findByText('Keine Verbindung. Spielen braucht Internet, Nachspielen geht auch offline.'),
      ).toBeInTheDocument()
      expect(await screen.findByText('Spielen braucht Internet')).toBeInTheDocument()
      expect(screen.getByRole('button', { name: 'Neue Partie' })).toBeDisabled()
    } finally {
      online.mockRestore()
    }
  })
})
