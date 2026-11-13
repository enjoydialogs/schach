import { fireEvent, screen, waitFor, within } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { API_BASIS } from '../api/client'
import type { NeuePartieAnfrage } from '../api/types'
import { mitFakeServer, zeigeApp } from '../components/testApp'

// Neue Partie (SCHACH-SPEC 4.5, 10.5, 10.6).

const t = mitFakeServer()

async function waehle(name: string) {
  fireEvent.click(await screen.findByRole('button', { name: new RegExp(`^${name}`) }))
  return screen.getByRole('dialog', { name: `Partie gegen ${name}` })
}

describe('Neue Partie', () => {
  it('listet 8 Gegner nach Elo mit Stufe, Vorstellung und Bilanz', async () => {
    const p = t.fake.starte({ gegnerId: 'finn', farbe: 'Weiss', bedenkzeit: 'OhneUhr' })
    t.fake.gibAuf(p.id, { halbzug: p.halbzug })
    zeigeApp('/neu')

    await screen.findByText('Ilse')
    const zeilen = screen.getAllByRole('button').filter((b) => b.classList.contains('gegnerzeile'))
    expect(zeilen.map((z) => z.querySelector('.zeile__titel')?.textContent)).toEqual([
      'Ilse',
      'Finn',
      'Oscar',
      'Nele',
      'Theo',
      'Sementa',
      'Konrad',
      'Maren',
    ])
    expect(zeilen[2]).toHaveTextContent('Anfänger · 800')
    expect(zeilen[2]).toHaveTextContent('Ich erklär dir hinterher gern, warum ich so gezogen hab.')
    expect(within(zeilen[1]!).getByLabelText('0 Siege, 0 Remis, 1 Niederlagen')).toHaveTextContent('S 0 · R 0 · N 1')
  })

  it('zeigt das Besiegt-Haekchen nur nach einem Sieg, der zaehlt', async () => {
    t.fake.vorgaben = { startFen: '6k1/5ppp/8/8/8/8/8/R5K1 w - - 0 1' }
    const p = t.fake.starte({ gegnerId: 'ilse', farbe: 'Weiss', bedenkzeit: 'OhneUhr' })
    t.fake.ziehe(p.id, { halbzug: 0, zug: 'a1a8' })
    t.fake.vorgaben = {}
    zeigeApp('/neu')

    const ilse = await screen.findByRole('button', { name: /^Ilse/ })
    expect(within(ilse).getByTestId('besiegt')).toBeInTheDocument()
    expect(within(screen.getByRole('button', { name: /^Finn/ })).queryByTestId('besiegt')).toBeNull()
  })

  it('zeigt Gaesten keine Bilanz', async () => {
    t.fake.gast = true
    zeigeApp('/neu')
    await screen.findByText('Ilse')
    expect(screen.queryByText(/^S \d/)).toBeNull()
  })

  it('startet mit Zufall und Ohne Uhr als Standard und ersetzt die Route', async () => {
    let anfrage: NeuePartieAnfrage | null = null
    t.server.events.on('request:start', async ({ request }) => {
      if (request.method === 'POST' && new URL(request.url).pathname === `${API_BASIS}/partien`) {
        anfrage = (await request.clone().json()) as NeuePartieAnfrage
      }
    })
    const router = zeigeApp('/neu')
    const sheet = await waehle('Oscar')

    expect(within(sheet).getByRole('radio', { name: 'Zufall' })).toBeChecked()
    expect(within(sheet).getByRole('radio', { name: /Ohne Uhr/ })).toBeChecked()
    expect(within(sheet).getByText('Kein Zeitlimit, mit Zurücknehmen und Tipp')).toBeInTheDocument()
    expect(within(sheet).getByText('15 Min. pro Spieler, +10 s nach jedem Zug')).toBeInTheDocument()
    expect(within(sheet).getByText('10 Min. pro Spieler, +5 s nach jedem Zug')).toBeInTheDocument()
    expect(within(sheet).getByText('5 Min. pro Spieler, +3 s nach jedem Zug')).toBeInTheDocument()
    expect(within(sheet).getByText('Jeder Zug max. 90 s, Rest verfällt')).toBeInTheDocument()
    expect(within(sheet).getByText('Mit Uhr: kein Zurücknehmen, kein Tipp.')).toBeInTheDocument()

    fireEvent.click(within(sheet).getByRole('button', { name: 'Partie starten' }))
    await waitFor(() => expect(router.state.location.pathname).toMatch(/^\/partie\/fake-/))
    expect(anfrage).toEqual({ gegnerId: 'oscar', farbe: 'Zufall', bedenkzeit: 'OhneUhr' })
    expect(router.state.historyAction).toBe('REPLACE')
    expect(localStorage.getItem('gastPartieId')).toBeNull()
    t.server.events.removeAllListeners()
  })

  it('nimmt Farbe und Bedenkzeit aus dem Sheet', async () => {
    const router = zeigeApp('/neu')
    const sheet = await waehle('Theo')
    fireEvent.click(within(sheet).getByRole('radio', { name: 'Schwarz' }))
    fireEvent.click(within(sheet).getByRole('radio', { name: '5+3' }))
    fireEvent.click(within(sheet).getByRole('button', { name: 'Partie starten' }))

    await waitFor(() => expect(router.state.location.pathname).toMatch(/^\/partie\//))
    const id = router.state.location.pathname.split('/').at(-1)!
    expect(t.fake.lies(id)).toMatchObject({ spielerFarbe: 'Schwarz', bedenkzeit: 'Fischer5Plus3' })
  })

  it('speichert als Gast die gastPartieId', async () => {
    t.fake.gast = true
    const router = zeigeApp('/neu')
    fireEvent.click(within(await waehle('Ilse')).getByRole('button', { name: 'Partie starten' }))
    await waitFor(() => expect(router.state.location.pathname).toMatch(/^\/partie\//))
    expect(localStorage.getItem('gastPartieId')).toBe(router.state.location.pathname.split('/').at(-1))
  })

  it('zeigt bei 409 OffenePartie den Dialog; Aufgeben startet die neue Partie', async () => {
    const offen = t.fake.starte({ gegnerId: 'maren', farbe: 'Weiss', bedenkzeit: 'OhneUhr' })
    const router = zeigeApp('/neu')
    fireEvent.click(within(await waehle('Nele')).getByRole('button', { name: 'Partie starten' }))

    const dialog = await screen.findByRole('dialog', { name: 'Offene Partie gegen Maren' })
    fireEvent.click(within(dialog).getByRole('button', { name: 'Aufgeben und neu starten' }))

    await waitFor(() => expect(router.state.location.pathname).toMatch(/^\/partie\//))
    expect(router.state.location.pathname).not.toBe(`/partie/${offen.id}`)
    expect(t.fake.lies(offen.id).status).toBe('Beendet')
    expect(t.fake.offene().gegner.key).toBe('nele')
  })

  it('setzt bei 409 per Fortsetzen die offene Partie fort', async () => {
    const offen = t.fake.starte({ gegnerId: 'maren', farbe: 'Weiss', bedenkzeit: 'OhneUhr' })
    const router = zeigeApp('/neu')
    fireEvent.click(within(await waehle('Nele')).getByRole('button', { name: 'Partie starten' }))
    fireEvent.click(within(await screen.findByRole('dialog', { name: 'Offene Partie gegen Maren' })).getByRole('button', { name: 'Fortsetzen' }))
    await waitFor(() => expect(router.state.location.pathname).toBe(`/partie/${offen.id}`))
  })

  it('fragt einen Gast mit laufender Gast-Partie vor dem Start', async () => {
    t.fake.gast = true
    const offen = t.fake.starte({ gegnerId: 'finn', farbe: 'Weiss', bedenkzeit: 'OhneUhr' })
    localStorage.setItem('gastPartieId', offen.id)
    zeigeApp('/neu')
    fireEvent.click(within(await waehle('Ilse')).getByRole('button', { name: 'Partie starten' }))
    const dialog = await screen.findByRole('dialog', { name: 'Offene Partie gegen Finn' })
    expect(dialog).toHaveTextContent('Die Gastpartie geht verloren.')
  })

  it('meldet 429 und 503 GeradeVielLos', async () => {
    zeigeApp('/neu')
    const sheet = await waehle('Ilse')

    t.server.use(http.post(`${API_BASIS}/partien`, () => HttpResponse.json({ fehler: 'TageslimitErreicht', zustand: null, offenePartieId: null }, { status: 429 }), { once: true }))
    fireEvent.click(within(sheet).getByRole('button', { name: 'Partie starten' }))
    expect(await within(sheet).findByText('Zu viele neue Partien. Bitte warte kurz.')).toBeInTheDocument()

    t.server.use(http.post(`${API_BASIS}/partien`, () => HttpResponse.json({ fehler: 'GeradeVielLos', zustand: null, offenePartieId: null }, { status: 503 }), { once: true }))
    fireEvent.click(within(sheet).getByRole('button', { name: 'Partie starten' }))
    expect(
      await within(sheet).findByText('Gerade ist viel los. Mit Konto geht es sofort weiter, sonst in ein paar Minuten.'),
    ).toBeInTheDocument()
  })
})
