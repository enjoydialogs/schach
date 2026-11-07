import { screen } from '@testing-library/react'
import { mitFakeServer, zeigeApp } from './components/testApp'

// Start laedt seit S3b Sitzung und offene Partie: die Routen laufen gegen den Fake-Server.
mitFakeServer()

describe('router', () => {
  it('zeigt auf / die Startseite mit Fusszeile', async () => {
    zeigeApp('/')

    expect(screen.getByRole('heading', { name: 'Schach' })).toBeInTheDocument()
    expect(screen.getByRole('contentinfo')).toHaveTextContent('Impressum · Datenschutz · Quellcode')
    await screen.findByRole('link', { name: 'Elo 800' })
  })

  it('zeigt fuer unbekannte Pfade "Nicht gefunden" statt umzuleiten', () => {
    const router = zeigeApp('/gibtsnicht')

    expect(screen.getByRole('heading', { name: 'Diese Seite gibt es nicht.' })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Zur Startseite' })).toHaveAttribute('href', '/')
    expect(router.state.location.pathname).toBe('/gibtsnicht')
  })
})
