import { act, cleanup, fireEvent, screen, waitFor } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import type { RegisterSWOptions } from 'vite-plugin-pwa/types'
import { API_BASIS } from '../api/client'
import { mitFakeServer, zeigeApp } from '../components/testApp'
import { mitFakeCaches } from './fakeCaches'
import { fangeInstallPrompt, setzeInstallZurueck, SPAETER_MS } from './install'
import { setzeUpdateZurueck, starteUpdates } from './update'

// PWA auf den Bildschirmen (SCHACH-SPEC 10.13, 10.15 Punkte 4 bis 6): Install-Karte, Update-Hinweis,
// Offline-Banner, Partie offline, Offline-Nachspielen aus `schach-partien-v1`.

const t = mitFakeServer()
const cache = mitFakeCaches()

const ELO_CHIP = /^Elo \d+$/
const BANNER ='Keine Verbindung. Spielen braucht Internet, Nachspielen geht auch offline.'

function beendetePartie() {
  const p = t.fake.starte({ gegnerId: 'oscar', farbe: 'Weiss', bedenkzeit: 'OhneUhr' })
  t.fake.ziehe(p.id, { halbzug: 0, zug: 'e2e4' })
  return t.fake.gibAuf(p.id, { halbzug: 2 })
}

function offline() {
  const spion = vi.spyOn(navigator, 'onLine', 'get').mockReturnValue(false)
  act(() => {
    window.dispatchEvent(new Event('offline'))
  })
  return spion
}

beforeAll(() => fangeInstallPrompt())
afterEach(() => {
  setzeInstallZurueck()
  setzeUpdateZurueck()
})

describe('Install-Karte', () => {
  function installEreignis() {
    const e = Object.assign(new Event('beforeinstallprompt', { cancelable: true }), {
      prompt: vi.fn(() => Promise.resolve()),
      userChoice: Promise.resolve({ outcome: 'accepted' as const }),
    })
    act(() => {
      window.dispatchEvent(e)
    })
    return e
  }

  it('erscheint auf Start nach der ersten beendeten Partie und installiert', async () => {
    beendetePartie()
    const e = installEreignis()
    zeigeApp('/')

    expect(await screen.findByRole('heading', { name: 'Schach als App installieren' })).toBeInTheDocument()
    fireEvent.click(screen.getByRole('button', { name: 'Installieren' }))
    expect(e.prompt).toHaveBeenCalledOnce()
    await waitFor(() => expect(screen.queryByRole('heading', { name: 'Schach als App installieren' })).toBeNull())
  })

  it('fehlt ohne beendete Partie', async () => {
    installEreignis()
    zeigeApp('/')
    await screen.findByRole('link', { name: ELO_CHIP })
    await act(() => new Promise((r) => setTimeout(r, 100)))
    expect(screen.queryByRole('heading', { name: 'Schach als App installieren' })).toBeNull()
  })

  it('fehlt fuer Gaeste', async () => {
    beendetePartie()
    t.fake.gast = true
    installEreignis()
    zeigeApp('/')
    await screen.findByRole('link', { name: 'Anmelden' })
    await act(() => new Promise((r) => setTimeout(r, 100)))
    expect(screen.queryByRole('heading', { name: 'Schach als App installieren' })).toBeNull()
  })

  it('fehlt ohne beforeinstallprompt', async () => {
    beendetePartie()
    zeigeApp('/')
    await screen.findByRole('link', { name: ELO_CHIP })
    await act(() => new Promise((r) => setTimeout(r, 100)))
    expect(screen.queryByRole('heading', { name: 'Schach als App installieren' })).toBeNull()
  })

  it('fehlt im Standalone', async () => {
    const mm = vi.fn((q: string) => ({ matches: q === '(display-mode: standalone)' }))
    vi.stubGlobal('matchMedia', mm)
    beendetePartie()
    installEreignis()
    zeigeApp('/')
    await screen.findByRole('link', { name: ELO_CHIP })
    await act(() => new Promise((r) => setTimeout(r, 100)))
    expect(mm).toHaveBeenCalled()
    expect(screen.queryByRole('heading', { name: 'Schach als App installieren' })).toBeNull()
  })

  it('„Später“ merkt den Zeitpunkt und schweigt 14 Tage', async () => {
    beendetePartie()
    installEreignis()
    zeigeApp('/')
    fireEvent.click(await screen.findByRole('button', { name: 'Später' }))

    expect(screen.queryByRole('heading', { name: 'Schach als App installieren' })).toBeNull()
    const gemerkt = localStorage.getItem('installHinweis')
    expect(gemerkt).not.toBeNull()
    expect(Math.abs(Date.parse(gemerkt!) - Date.now())).toBeLessThan(5_000)
  })

  it('kommt nach 14 Tagen wieder', async () => {
    localStorage.setItem('installHinweis', new Date(Date.now() - SPAETER_MS - 1_000).toISOString())
    beendetePartie()
    installEreignis()
    zeigeApp('/')
    expect(await screen.findByRole('heading', { name: 'Schach als App installieren' })).toBeInTheDocument()
  })

  it('bleibt 13 Tage nach „Später“ weg', async () => {
    localStorage.setItem('installHinweis', new Date(Date.now() - 13 * 24 * 3600 * 1000).toISOString())
    beendetePartie()
    installEreignis()
    zeigeApp('/')
    await screen.findByRole('link', { name: ELO_CHIP })
    await act(() => new Promise((r) => setTimeout(r, 100)))
    expect(screen.queryByRole('heading', { name: 'Schach als App installieren' })).toBeNull()
  })
})

describe('Update-Hinweis', () => {
  function neueVersion() {
    const update = vi.fn(() => Promise.resolve())
    let optionen: RegisterSWOptions = {}
    starteUpdates((o) => {
      optionen = o
      return update
    })
    act(() => optionen.onNeedRefresh?.())
    return update
  }

  it('erscheint auf Start und laedt per „Neu laden“', async () => {
    const update = neueVersion()
    zeigeApp('/')
    expect(await screen.findByText('Neue Version verfügbar')).toBeInTheDocument()
    fireEvent.click(screen.getByRole('button', { name: 'Neu laden' }))
    expect(update).toHaveBeenCalledWith(true)
  })

  it('erscheint auf Rechtstexten (jede Seite)', async () => {
    neueVersion()
    zeigeApp('/impressum')
    expect(await screen.findByText('Neue Version verfügbar')).toBeInTheDocument()
  })

  it('fehlt ohne neue Version', async () => {
    zeigeApp('/')
    await screen.findByRole('link', { name: ELO_CHIP })
    expect(screen.queryByText('Neue Version verfügbar')).toBeNull()
  })

  it('erscheint nie in der Partie', async () => {
    neueVersion()
    const p = t.fake.starte({ gegnerId: 'oscar', farbe: 'Weiss', bedenkzeit: 'OhneUhr' })
    zeigeApp(`/partie/${p.id}`)
    await screen.findByText('Oscar')
    expect(screen.queryByText('Neue Version verfügbar')).toBeNull()
  })

  it('erscheint nie beim Nachspielen', async () => {
    neueVersion()
    const z = beendetePartie()
    zeigeApp(`/nachspielen/${z.id}`)
    await screen.findByText('Grundstellung')
    expect(screen.queryByText('Neue Version verfügbar')).toBeNull()
  })
})

describe('Offline', () => {
  it('zeigt das Banner auf jeder Seite und nimmt es mit der Verbindung weg', async () => {
    zeigeApp('/datenschutz')
    await screen.findByRole('heading', { name: 'Datenschutzerklärung' })
    expect(screen.queryByText(BANNER)).toBeNull()

    const spion = offline()
    expect(await screen.findByText(BANNER)).toBeInTheDocument()

    spion.mockReturnValue(true)
    act(() => {
      window.dispatchEvent(new Event('online'))
    })
    await waitFor(() => expect(screen.queryByText(BANNER)).toBeNull())
    spion.mockRestore()
  })

  it('zeigt in der Partie „Keine Verbindung“ und sperrt die Eingabe', async () => {
    const p = t.fake.starte({ gegnerId: 'oscar', farbe: 'Weiss', bedenkzeit: 'OhneUhr' })
    zeigeApp(`/partie/${p.id}`)
    await screen.findByText('Oscar')
    const ziehe = vi.spyOn(t.fake, 'ziehe')

    const spion = offline()
    expect(await screen.findByText('Keine Verbindung')).toBeInTheDocument()
    fireEvent.click(document.querySelector<HTMLElement>('[data-square="e2"]')!)
    fireEvent.click(document.querySelector<HTMLElement>('[data-square="e4"]')!)
    await act(() => new Promise((r) => setTimeout(r, 100)))
    expect(ziehe).not.toHaveBeenCalled()
    spion.mockRestore()
  })

  it('spielt eine einmal geoeffnete beendete Partie offline aus dem Cache nach', async () => {
    const z = beendetePartie()
    zeigeApp(`/nachspielen/${z.id}`)
    await screen.findByText('Grundstellung')
    const gecacht = async () => (await caches.open('schach-partien-v1')).match(`/schach-partien/partie/${z.id}`)
    await waitFor(async () => expect(await gecacht()).toBeDefined())
    cleanup()

    // Flugmodus: jede API-Anfrage scheitert am Netz.
    t.server.use(http.all(`${API_BASIS}/*`, () => HttpResponse.error()))
    const spion = offline()
    zeigeApp(`/nachspielen/${z.id}`)
    expect(await screen.findByText('Grundstellung')).toBeInTheDocument()
    fireEvent.click(screen.getByRole('button', { name: 'Vor' }))
    expect(screen.getByTestId('statuszeile')).toHaveTextContent('1. e4')
    spion.mockRestore()
  })

  it('cacht eine offene Partie nicht', async () => {
    const p = t.fake.starte({ gegnerId: 'oscar', farbe: 'Weiss', bedenkzeit: 'OhneUhr' })
    zeigeApp(`/nachspielen/${p.id}`)
    await act(() => new Promise((r) => setTimeout(r, 50)))
    const c = cache.caches.speicher.get('schach-partien-v1')
    expect(await c?.match(`/schach-partien/partie/${p.id}`)).toBeUndefined()
  })
})
