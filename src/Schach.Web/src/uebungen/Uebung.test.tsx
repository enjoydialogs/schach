import { fireEvent, screen, waitFor, within } from '@testing-library/react'
import { mitFakeServer, zeigeApp } from '../components/testApp'
import { UEBUNGEN } from './katalog'

// Uebungen (SCHACH-SPEC 10.16): Hub-Zeile fuer alle, Liste, gefuehrte Uebung im Browser.

const t = mitFakeServer()

function feld(name: string): HTMLElement {
  const element = document.querySelector<HTMLElement>(`[data-square="${name}"]`)
  if (element === null) throw new Error(`Feld ${name} fehlt`)
  return element
}

describe('Uebungen', () => {
  it('Spieler sehen die Kacheln Partien, Übungen, Profil in dieser Reihenfolge', async () => {
    const router = zeigeApp('/')
    const kacheln = await screen.findByRole('navigation', { name: 'Schach' })
    expect(within(kacheln).getAllByRole('link').map((l) => l.textContent)).toEqual(['Partien', 'Übungen', 'Profil'])
    fireEvent.click(await screen.findByRole('link', { name: 'Übungen' }))
    await waitFor(() => expect(router.state.location.pathname).toBe('/uebungen'))
  })

  it('Gaeste sehen die Zeile Übungen auch', async () => {
    t.fake.gast = true
    zeigeApp('/')
    const kacheln = await screen.findByRole('navigation', { name: 'Schach' })
    expect(within(kacheln).getAllByRole('link').map((l) => l.getAttribute('href'))).toEqual(['/uebungen'])
    expect(screen.queryByRole('link', { name: /Profil/ })).toBeNull()
  })

  it('die Liste zeigt alle Uebungen nach Bereich', async () => {
    zeigeApp('/uebungen')
    expect(await screen.findByRole('heading', { name: 'Übungen' })).toBeInTheDocument()
    for (const u of UEBUNGEN) {
      expect(screen.getByRole('link', { name: new RegExp(u.titel) })).toHaveAttribute('href', `/uebungen/${u.key}`)
    }
    const matt = screen.getByRole('region', { name: 'Matt setzen' })
    expect(within(matt).getByRole('link', { name: /Grundreihenmatt/ })).toBeInTheDocument()
  })

  it('fuehrt durch eine Uebung: Begruessung, anderer Zug mit Hinweis und Ruecknahme, Loesung', async () => {
    zeigeApp('/uebungen/grundreihenmatt')
    expect(await screen.findByText(/^Hallo, ich bin \S+\. Schau dir meinen König/)).toBeInTheDocument()
    expect(screen.getByTestId('statuszeile')).toHaveTextContent('Setz mich matt')
    expect(screen.getByText('Schritt 1 von 1')).toBeInTheDocument()

    // Anderer erlaubter Zug: Hinweis, dann zurueckgenommen.
    fireEvent.click(feld('d1'))
    fireEvent.click(feld('d4'))
    expect(await screen.findByText(/Schau auf die achte Reihe/)).toBeInTheDocument()
    expect(screen.getByTestId('statuszeile')).toHaveTextContent('Nicht ganz')
    await waitFor(() => expect(screen.getByTestId('statuszeile')).toHaveTextContent('Setz mich matt'), { timeout: 3000 })

    // Tipp in zwei Stufen.
    fireEvent.click(screen.getByRole('button', { name: 'Tipp' }))
    expect(screen.getByTestId('statuszeile')).toHaveTextContent('Zieh mit dem Turm')
    fireEvent.click(screen.getByRole('button', { name: 'Tipp' }))
    expect(screen.getByTestId('statuszeile')).toHaveTextContent('Turm d1 nach d8')

    fireEvent.click(feld('d1'))
    fireEvent.click(feld('d8'))
    const ende = await screen.findByRole('dialog', { name: 'Übung geschafft!' })
    expect(ende).toHaveTextContent('Luftloch')
    expect(screen.getByText(/^Matt!/)).toBeInTheDocument()
  })

  it('Schwarz-Uebung: Brett von Schwarz, der Gegner zieht zuerst', async () => {
    zeigeApp('/uebungen/schaefermatt-abwehren')
    await screen.findByText(/^Hallo, ich bin/)
    expect(screen.getByTestId('eigene-farbe')).toHaveTextContent('Schwarz')
    await waitFor(() => expect(screen.getByTestId('statuszeile')).toHaveTextContent('Bauer e7 nach e5'), { timeout: 3000 })
  })

  it('unbekannte Uebung: Hinweis und Weg zur Liste', async () => {
    zeigeApp('/uebungen/gibtsnicht')
    expect(await screen.findByText('Diese Übung gibt es nicht.')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Übungen' })).toHaveAttribute('href', '/uebungen')
  })
})
