import { fireEvent, screen, waitFor, within } from '@testing-library/react'
import { mitFakeServer, zeigeApp } from '../components/testApp'
import type { FakeServer } from '../fake/fakeServer'

// Partienliste (SCHACH-SPEC 10.7).

const t = mitFakeServer()

function spieleUndGibAuf(fake: FakeServer, gegnerId: string, bedenkzeit: 'OhneUhr' | 'Fischer5Plus3' = 'OhneUhr') {
  const p = fake.starte({ gegnerId, farbe: 'Weiss', bedenkzeit })
  return fake.gibAuf(p.id, { halbzug: p.halbzug })
}

describe('Partienliste', () => {
  it('zeigt leer den Hinweis und „Neue Partie“', async () => {
    zeigeApp('/partien')
    expect(await screen.findByText('Noch keine beendete Partie.')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Neue Partie' })).toHaveAttribute('href', '/neu')
  })

  it('listet beendete Partien neueste zuerst mit Ergebnis, Bedenkzeit und Elo', async () => {
    spieleUndGibAuf(t.fake, 'ilse')
    const z = spieleUndGibAuf(t.fake, 'oscar', 'Fischer5Plus3')
    const router = zeigeApp('/partien')

    const zeilen = await screen.findAllByRole('link', { name: /Niederlage/ })
    expect(zeilen).toHaveLength(2)
    expect(zeilen[0]).toHaveTextContent('Oscar')
    expect(zeilen[0]).toHaveTextContent(/\d+\. \w+\.? · 5\+3 · Elo −\d+/)
    expect(zeilen[1]).toHaveTextContent('Ilse')
    expect(zeilen[1]).toHaveTextContent('Ohne Uhr')
    expect(within(zeilen[0]!).getByText('Niederlage').previousSibling).toHaveClass('ergebnispunkt--niederlage')

    fireEvent.click(zeilen[0]!)
    await waitFor(() => expect(router.state.location.pathname).toBe(`/nachspielen/${z.id}`))
  })

  it('zeigt „zählt nicht“ fuer Partien ohne Elo-Wirkung', async () => {
    const p = t.fake.starte({ gegnerId: 'nele', farbe: 'Weiss', bedenkzeit: 'OhneUhr' })
    t.fake.tipp(p.id, { halbzug: 0 })
    t.fake.gibAuf(p.id, { halbzug: 0 })
    zeigeApp('/partien')
    expect(await screen.findByRole('link', { name: /Nele/ })).toHaveTextContent('zählt nicht')
  })

  it('laedt per „Mehr laden“ die naechsten 50', async () => {
    for (let i = 0; i < 52; i++) spieleUndGibAuf(t.fake, 'finn')
    zeigeApp('/partien')

    await waitFor(() => expect(screen.getAllByRole('link', { name: /Finn/ })).toHaveLength(50))
    fireEvent.click(screen.getByRole('button', { name: 'Mehr laden' }))
    await waitFor(() => expect(screen.getAllByRole('link', { name: /Finn/ })).toHaveLength(52))
    expect(screen.queryByRole('button', { name: 'Mehr laden' })).toBeNull()
  })

  it('schickt Gaeste zur Anmeldung', async () => {
    t.fake.gast = true
    const router = zeigeApp('/partien')
    await waitFor(() => expect(router.state.location.pathname).toBe('/anmelden'))
  })
})
