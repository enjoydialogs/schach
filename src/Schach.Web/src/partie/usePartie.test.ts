import { act, renderHook } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { setupServer } from 'msw/node'
import { afterAll, afterEach, beforeAll, beforeEach, vi } from 'vitest'
import { API_BASIS, CSRF_HEADER } from '../api/client'
import type { ZustandDto } from '../api/types'
import { gastPartieId } from '../components/SpeicherSchluessel'
import { setzeSitzungZurueck } from '../components/sitzung'
import { de } from '../i18n/de'
import { zug, zustand } from './testdaten'
import { usePartie } from './usePartie'

// Client-Vertrag des Partie-Bildschirms gegen Schach.Api (S3e; SCHACH-SPEC 8.1, 8.5, 8.6, 11.4).
// Die Handler bilden das echte Verhalten nach, das der Fake nicht hat: CSRF-Pruefung, Limiter-429
// ohne Body, 409 ohne Zustand (Concurrency), 404 fuer beendete Gast-Partien.

const START = Date.parse('2026-11-07T10:00:00.000Z')
const CSRF = 'csrf-1'
const server = setupServer()

let sichtbarkeit: DocumentVisibilityState = 'visible'
let angemeldet = true
/** Antwort auf `GET /partien/p1`; eine Funktion, damit Tests den Serverstand weiterdrehen. */
let aufGet: () => Response = () => HttpResponse.json(zustand())
let gets = 0

beforeAll(() => {
  server.listen({ onUnhandledFrame: 'error' })
  Object.defineProperty(document, 'visibilityState', { configurable: true, get: () => sichtbarkeit })
})
afterAll(() => server.close())
beforeEach(() => {
  vi.useFakeTimers({ toFake: ['setTimeout', 'clearTimeout', 'setInterval', 'clearInterval', 'Date'] })
  vi.setSystemTime(START)
  sichtbarkeit = 'visible'
  angemeldet = true
  gets = 0
  aufGet = () => HttpResponse.json(zustand())
  setzeSitzungZurueck()
  server.resetHandlers(
    http.get(`${API_BASIS}/sitzung`, () =>
      HttpResponse.json({ angemeldet, spieler: null, csrfToken: CSRF }),
    ),
    http.get(`${API_BASIS}/partien/p1`, () => {
      gets++
      return aufGet()
    }),
  )
})
afterEach(() => {
  vi.useRealTimers()
  localStorage.clear()
})

/** Laesst `ms` Fake-Zeit vergehen und alle Antworten ankommen. */
async function lauf(ms = 0) {
  await act(async () => {
    await vi.advanceTimersByTimeAsync(ms)
  })
}

async function oeffne() {
  const hook = renderHook(() => usePartie('p1', false))
  await lauf()
  await lauf()
  return hook
}

/** POST-Handler mit CSRF-Pruefung wie AutoValidateAntiforgeryToken; merkt sich die Bodys. */
function post(pfad: string, antwort: (aufruf: number) => Response) {
  const bodys: string[] = []
  server.use(
    http.post(`${API_BASIS}/partien/p1/${pfad}`, async ({ request }) => {
      if (request.headers.get(CSRF_HEADER) !== CSRF) {
        return HttpResponse.json({ title: 'Bad Request', status: 400 }, { status: 400 })
      }
      bodys.push(await request.text())
      return antwort(bodys.length)
    }),
  )
  return bodys
}

const nachE4 = (teil: Partial<ZustandDto> = {}) =>
  zustand({
    halbzug: 2,
    fen: 'rnbqkbnr/pppp1ppp/8/4p3/4P3/8/PPPP1PPP/RNBQKBNR w KQkq - 0 2',
    zugfolge: [zug(1, 'e2e4', 'e4'), zug(2, 'e7e5', 'e5')],
    ...teil,
  })

describe('usePartie gegen die echte API', () => {
  it('429 auf /zuege: „denkt nach“, dann nach Retry-After derselbe Body', async () => {
    const bodys = post('zuege', (n) =>
      n === 1
        ? new HttpResponse(null, { status: 429, headers: { 'Retry-After': '2' } })
        : HttpResponse.json(nachE4()),
    )
    const { result } = await oeffne()

    act(() => result.current.ziehe('e2e4'))
    await lauf()

    expect(result.current.limitWartet).toBe(true)
    expect(result.current.beschaeftigt).toBe(true)
    expect(result.current.anzeige?.gegnerDenkt).toBe(true)
    expect(bodys).toHaveLength(1)

    await lauf(1_999)
    expect(bodys).toHaveLength(1)
    await lauf(1)
    await lauf()

    expect(bodys).toEqual([bodys[0], bodys[0]])
    expect(JSON.parse(bodys[0]!)).toEqual({ halbzug: 0, zug: 'e2e4' })
    expect(result.current.limitWartet).toBe(false)
    expect(result.current.zustand?.halbzug).toBe(2)
  })

  it('bricht die Wiederholung beim Verlassen ab', async () => {
    const bodys = post('zuege', () => new HttpResponse(null, { status: 429, headers: { 'Retry-After': '2' } }))
    const { result, unmount } = await oeffne()

    act(() => result.current.ziehe('e2e4'))
    await lauf()
    unmount()
    await lauf(5_000)

    expect(bodys).toHaveLength(1)
  })

  it('409 mit Zustand: uebernimmt ihn immer', async () => {
    post('zuege', () =>
      HttpResponse.json({ fehler: 'HalbzugVeraltet', zustand: nachE4(), offenePartieId: null }, { status: 409 }),
    )
    const { result } = await oeffne()
    const getsVorher = gets

    act(() => result.current.ziehe('d2d4'))
    await lauf()

    expect(result.current.zustand?.halbzug).toBe(2)
    expect(result.current.anzeige?.fen).toBe(nachE4().fen)
    expect(result.current.hinweis).toBe(de.status.zugNichtMoeglich)
    expect(gets).toBe(getsVorher)
  })

  // DomainExceptionHandler: Concurrency-Konflikt und Engine-Ausfall tragen `fehler`, aber keinen Zustand.
  it.each([
    ['409 HalbzugVeraltet (Concurrency-Konflikt)', 409, 'HalbzugVeraltet'],
    ['503 GeradeVielLos (Engine-Ausfall)', 503, 'GeradeVielLos'],
  ])('%s ohne Zustand auf /zuege: laedt neu', async (_, status, fehler) => {
    post('zuege', () => HttpResponse.json({ fehler, zustand: null, offenePartieId: null }, { status }))
    const { result } = await oeffne()
    const getsVorher = gets
    aufGet = () => HttpResponse.json(nachE4())

    act(() => result.current.ziehe('e2e4'))
    await lauf()
    await lauf()

    expect(gets).toBeGreaterThan(getsVorher)
    expect(result.current.zustand?.halbzug).toBe(2)
    expect(result.current.hinweis).toBe(de.status.zugNichtMoeglich)
  })

  it('400 ZugUnzulaessig: setzt zurueck ohne neu zu laden', async () => {
    post('zuege', () =>
      HttpResponse.json({ fehler: 'ZugUnzulaessig', zustand: null, offenePartieId: null }, { status: 400 }),
    )
    const { result } = await oeffne()
    const getsVorher = gets

    act(() => result.current.ziehe('e2e4'))
    await lauf()

    expect(result.current.anzeige?.fen).toBe(zustand().fen)
    expect(result.current.hinweis).toBe(de.status.zugNichtMoeglich)
    expect(gets).toBe(getsVorher)
  })

  it.each([
    ['429 vom Limiter', () => new HttpResponse(null, { status: 429, headers: { 'Retry-After': '2' } })],
    [
      '503 TippNichtVerfuegbar',
      () => HttpResponse.json({ fehler: 'TippNichtVerfuegbar', zustand: null, offenePartieId: null }, { status: 503 }),
    ],
  ])('%s auf /tipp: „gerade nicht verfügbar“, keine Wiederholung', async (_, antwort) => {
    const bodys = post('tipp', antwort)
    const { result } = await oeffne()

    act(() => result.current.holeTipp())
    await lauf()
    await lauf(5_000)

    expect(bodys).toHaveLength(1)
    expect(result.current.limitWartet).toBe(false)
    expect(result.current.tippStand).toBeNull()
  })

  it('zeigt den Tipp-Hinweis sofort', async () => {
    post('tipp', () => new HttpResponse(null, { status: 429, headers: { 'Retry-After': '2' } }))
    const { result } = await oeffne()

    act(() => result.current.holeTipp())
    await lauf()

    expect(result.current.hinweis).toBe(de.status.tippNichtVerfuegbar)
  })

  it('pollt bei gegnerZugAusstehend alle 3 s, nur bei sichtbarem Tab', async () => {
    aufGet = () => HttpResponse.json(zustand({ halbzug: 1, amZug: false, gegnerZugAusstehend: true, erlaubteZuege: [] }))
    await oeffne()
    expect(gets).toBe(1)

    await lauf(3_000)
    expect(gets).toBe(2)

    sichtbarkeit = 'hidden'
    await lauf(9_000)
    expect(gets).toBe(2)

    sichtbarkeit = 'visible'
    aufGet = () => HttpResponse.json(nachE4())
    await lauf(3_000)
    expect(gets).toBe(3)

    // Gegnerzug da: kein Polling mehr.
    await lauf(9_000)
    expect(gets).toBe(3)
  })

  it('laedt bei visibilitychange neu, nicht bei beendeter Partie', async () => {
    await oeffne()
    expect(gets).toBe(1)

    act(() => document.dispatchEvent(new Event('visibilitychange')))
    await lauf()
    expect(gets).toBe(2)

    aufGet = () => HttpResponse.json(zustand({ status: 'Beendet', amZug: false, ergebnis: 'Remis', grund: 'Patt' }))
    act(() => document.dispatchEvent(new Event('visibilitychange')))
    await lauf()
    expect(gets).toBe(3)

    act(() => document.dispatchEvent(new Event('visibilitychange')))
    await lauf()
    expect(gets).toBe(3)
  })

  it('ein GET beim lokal berechneten Uhrablauf, gerechnet mit serverZeit', async () => {
    // Serveruhr 60 s vor der Clientuhr; Spieler hat noch 10 s, seit 2 s am Zug.
    const server0 = START + 60_000
    const offen = zustand({
      bedenkzeit: 'Fischer5Plus3',
      uhr: {
        spielerRestMs: 10_000,
        gegnerRestMs: 300_000,
        laeuftFuer: 'Spieler',
        amZugSeit: new Date(server0 - 2_000).toISOString(),
        serverZeit: new Date(server0).toISOString(),
      },
    })
    aufGet = () => HttpResponse.json(offen)
    const { result } = await oeffne()
    expect(result.current.versatzMs).toBe(60_000)

    await lauf(7_900)
    expect(gets).toBe(1)
    aufGet = () =>
      HttpResponse.json(
        zustand({ ...offen, status: 'Beendet', amZug: false, ergebnis: 'SchwarzGewinnt', grund: 'Zeit', uhr: { ...offen.uhr!, laeuftFuer: null } }),
      )
    await lauf(200)
    expect(gets).toBe(2)
    expect(result.current.zustand?.grund).toBe('Zeit')

    await lauf(30_000)
    expect(gets).toBe(2)
  })

  it('fragt nach Uhrablauf im Poll-Takt nach, solange der Server die Partie offen meldet', async () => {
    const uhr = (jetzt: number) => ({
      spielerRestMs: 0,
      gegnerRestMs: 300_000,
      laeuftFuer: 'Spieler' as const,
      amZugSeit: new Date(jetzt - 5_000).toISOString(),
      serverZeit: new Date(jetzt).toISOString(),
    })
    aufGet = () => HttpResponse.json(zustand({ bedenkzeit: 'Fischer5Plus3', uhr: uhr(Date.now()) }))
    await oeffne()
    await lauf(100)
    expect(gets).toBe(2)

    // Nicht im Dauerfeuer, sondern im Poll-Takt.
    await lauf(1_000)
    expect(gets).toBe(2)
    await lauf(2_000)
    expect(gets).toBe(3)
  })

  it('beendete Gast-Partie (404 nach dem Sweep): kein Weiterspielen am alten Brett', async () => {
    angemeldet = false
    gastPartieId.setze('p1')
    const { result } = await oeffne()
    expect(result.current.gast).toBe(true)

    aufGet = () => new HttpResponse(null, { status: 404 })
    act(() => document.dispatchEvent(new Event('visibilitychange')))
    await lauf()

    expect(result.current.zustand).toBeNull()
    expect(result.current.lade).toBe('nichtGefunden')
    expect(gastPartieId.lies()).toBeNull()
  })

  it('zeigt den Gegnerzug ohne Uhr spaetestens 3,5 s nach Empfang, auch bei nachgehender Geraeteuhr', async () => {
    // Geraeteuhr 60 s nach: sichtbarAb liegt aus Clientsicht eine Minute in der Zukunft.
    const sichtbarAb = new Date(START + 61_000).toISOString()
    post('zuege', () =>
      HttpResponse.json(
        nachE4({ gegnerZug: { ...zug(2, 'e7e5', 'e5'), sichtbarAb, spruch: null } }),
      ),
    )
    const { result } = await oeffne()

    act(() => result.current.ziehe('e2e4'))
    await lauf()
    expect(result.current.anzeige?.gegnerDenkt).toBe(true)

    await lauf(3_600)
    expect(result.current.anzeige?.gegnerDenkt).toBe(false)
    expect(result.current.anzeige?.fen).toBe(nachE4().fen)
  })
})
