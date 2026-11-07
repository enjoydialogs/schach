import { render, screen } from '@testing-library/react'
import { createMemoryRouter, RouterProvider } from 'react-router'
import { routes } from './router'

function oeffne(pfad: string) {
  render(<RouterProvider router={createMemoryRouter(routes, { initialEntries: [pfad] })} />)
}

describe('router', () => {
  it('zeigt auf / die Startseite mit Fusszeile', () => {
    oeffne('/')

    expect(screen.getByRole('heading', { name: 'Schach' })).toBeInTheDocument()
    expect(screen.getByRole('contentinfo')).toHaveTextContent('Impressum · Datenschutz · Quellcode')
  })

  it('zeigt fuer unbekannte Pfade "Nicht gefunden" statt umzuleiten', () => {
    oeffne('/gibtsnicht')

    expect(screen.getByRole('heading', { name: 'Diese Seite gibt es nicht.' })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Zur Startseite' })).toHaveAttribute('href', '/')
  })
})
