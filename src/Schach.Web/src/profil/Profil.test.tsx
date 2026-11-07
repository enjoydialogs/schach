import { fireEvent, screen, waitFor, within } from '@testing-library/react'
import { langDatum } from '../components/datei'
import { setzePalette } from '../components/palette'
import { mitFakeServer, zeigeApp } from '../components/testApp'

// Profil (SCHACH-SPEC 10.9, 9.7).

const t = mitFakeServer()

describe('Profil', () => {
  it('zeigt E-Mail, Elo und die Bilanz je Gegner', async () => {
    const p = t.fake.starte({ gegnerId: 'theo', farbe: 'Weiss', bedenkzeit: 'OhneUhr' })
    t.fake.gibAuf(p.id, { halbzug: 0 })
    zeigeApp('/profil')

    expect(await screen.findByText('spieler@example.invalid')).toBeInTheDocument()
    const elo = screen.getByTestId('profil-elo')
    expect(elo).toHaveTextContent(/^\d+$/)
    const bilanz = await screen.findByRole('list', { name: 'Bilanz' })
    expect(within(bilanz).getAllByRole('listitem')).toHaveLength(8)
    expect(within(bilanz).getByText('Theo').closest('li')).toHaveTextContent('S 0 · R 0 · N 1')
  })

  it('zeigt „Spieler seit“ mit dem Anlagedatum des Kontos', async () => {
    zeigeApp('/profil')
    const seit = await screen.findByText(/^Spieler seit /)
    expect(seit).toHaveTextContent(/^Spieler seit \d{1,2}\. \S+ \d{4}$/)
    const { erstelltAm } = t.fake.sitzung().spieler!
    expect(seit).toHaveTextContent(`Spieler seit ${langDatum(erstelltAm)}`)
  })

  it('schaltet die drei Einstellungen mit denselben localStorage-Schluesseln', async () => {
    zeigeApp('/profil')
    const schalter = await screen.findByRole('switch', { name: 'Töne und Vibration' })
    expect(screen.getByRole('switch', { name: 'Erlaubte Züge anzeigen' })).toBeChecked()
    expect(screen.getByRole('switch', { name: 'Vor ungedeckten Figuren warnen' })).toBeChecked()
    fireEvent.click(schalter)
    expect(JSON.parse(localStorage.getItem('einstellungen')!)).toMatchObject({ toeneUndVibration: false })
  })

  it('waehlt die Grundfarbe und speichert sie im Geraet', async () => {
    zeigeApp('/profil')
    const gruppe = await screen.findByRole('group', { name: 'Grundfarbe' })
    expect(within(gruppe).getByRole('radio', { name: 'Lila' })).toBeChecked()
    fireEvent.click(within(gruppe).getByRole('radio', { name: 'Pink' }))
    expect(within(gruppe).getByRole('radio', { name: 'Pink' })).toBeChecked()
    expect(localStorage.getItem('palette')).toBe('pink')
    expect(document.documentElement.dataset.palette).toBe('pink')
    setzePalette('lila')
  })

  it('exportiert die Daten als JSON-Datei', async () => {
    const erzeuge = vi.fn<(blob: Blob) => string>(() => 'blob:export')
    Object.assign(URL, { createObjectURL: erzeuge, revokeObjectURL: vi.fn() })
    let dateiname = ''
    const klick = vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(function (this: HTMLAnchorElement) {
      dateiname = this.download
    })
    zeigeApp('/profil')
    fireEvent.click(await screen.findByRole('button', { name: 'Daten exportieren (JSON)' }))

    await waitFor(() => expect(klick).toHaveBeenCalledOnce())
    expect(dateiname).toMatch(/^schach-export-\d{4}-\d{2}-\d{2}\.json$/)
    const inhalt = JSON.parse(await erzeuge.mock.calls[0]![0].text()) as { konto: { email: string } }
    expect(inhalt.konto.email).toBe('spieler@example.invalid')
    klick.mockRestore()
  })

  it('meldet ab, fuehrt zur Startseite und zeigt einen Hinweis', async () => {
    const router = zeigeApp('/profil')
    fireEvent.click(await screen.findByRole('button', { name: 'Abmelden' }))

    await waitFor(() => expect(router.state.location.pathname).toBe('/'))
    expect(await screen.findByText('Du bist abgemeldet.')).toBeInTheDocument()
    expect(await screen.findByRole('link', { name: 'Anmelden' })).toBeInTheDocument()
    expect(t.fake.sitzung().angemeldet).toBe(false)
  })

  it('loescht das Konto erst nach Bestaetigung', async () => {
    const router = zeigeApp('/profil')
    fireEvent.click(await screen.findByRole('button', { name: 'Konto löschen' }))
    const dialog = screen.getByRole('dialog', { name: 'Konto endgültig löschen?' })
    expect(dialog).toHaveTextContent(
      'Alle Partien, deine Elo und deine E-Mail-Adresse werden sofort und unwiderruflich gelöscht.',
    )
    fireEvent.click(within(dialog).getByRole('button', { name: 'Abbrechen' }))
    expect(t.fake.sitzung().angemeldet).toBe(true)

    fireEvent.click(screen.getByRole('button', { name: 'Konto löschen' }))
    fireEvent.click(within(screen.getByRole('dialog')).getByRole('button', { name: 'Endgültig löschen' }))

    await waitFor(() => expect(router.state.location.pathname).toBe('/'))
    expect(await screen.findByText('Dein Konto ist gelöscht.')).toBeInTheDocument()
    expect(t.fake.sitzung().angemeldet).toBe(false)

    // Danach fuehrt /partien zur Anmeldung (10.15 Punkt 8).
    await router.navigate('/partien')
    await waitFor(() => expect(router.state.location.pathname).toBe('/anmelden'))
  })
})
