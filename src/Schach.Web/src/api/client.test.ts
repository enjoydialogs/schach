import { http, HttpResponse } from 'msw'
import { setupServer } from 'msw/node'
import { afterAll, afterEach, beforeAll } from 'vitest'
import { api, API_BASIS, ApiFehler, CSRF_HEADER, setCsrfToken, setzeCsrfLader } from './client'

// Client-Vertrag gegen das Verhalten von Schach.Api (S3e): CSRF wie AutoValidateAntiforgeryToken,
// Fehlerform 8.5, Limiter-429 ohne Body nur mit Retry-After (SchachRateLimits.OnRejected).

const server = setupServer()
const ursprungsLader = async () => {
  const s = await api.get<{ csrfToken: string }>('/sitzung')
  setCsrfToken(s.csrfToken)
}

beforeAll(() => server.listen({ onUnhandledRequest: 'error' }))
afterEach(() => {
  server.resetHandlers()
  setCsrfToken(null)
  setzeCsrfLader(ursprungsLader)
})
afterAll(() => server.close())

/** `GET /sitzung` liefert der Reihe nach diese Tokens; zaehlt die Aufrufe. */
function sitzung(...tokens: string[]) {
  const stand = { aufrufe: 0 }
  server.use(
    http.get(`${API_BASIS}/sitzung`, () => {
      const token = tokens[Math.min(stand.aufrufe, tokens.length - 1)]
      stand.aufrufe++
      return HttpResponse.json({ angemeldet: false, spieler: null, csrfToken: token })
    }),
  )
  return stand
}

/** POST, der wie der Antiforgery-Filter ohne passendes Token 400 ProblemDetails liefert. */
function geschuetzt(pfad: string, gueltig: string, antwort: () => Response) {
  const gesehen: (string | null)[] = []
  server.use(
    http.post(`${API_BASIS}${pfad}`, ({ request }) => {
      const token = request.headers.get(CSRF_HEADER)
      gesehen.push(token)
      if (token !== gueltig) return HttpResponse.json({ title: 'Bad Request', status: 400 }, { status: 400 })
      return antwort()
    }),
  )
  return gesehen
}

describe('api client', () => {
  it('ruft /api/v1 mit Cookie der gleichen Herkunft auf, GET ohne CSRF', async () => {
    let header: string | null = 'nicht gelesen'
    let credentials: RequestCredentials | undefined
    server.use(
      http.get(`${API_BASIS}/gegner`, ({ request }) => {
        header = request.headers.get(CSRF_HEADER)
        credentials = request.credentials
        return HttpResponse.json([])
      }),
    )

    expect(await api.get('/gegner')).toEqual([])
    expect(header).toBeNull()
    expect(credentials).toBe('same-origin')
  })

  it('schickt das CSRF-Token bei Nicht-GET', async () => {
    setCsrfToken('abc')
    const gesehen = geschuetzt('/auth/abmelden', 'abc', () => new HttpResponse(null, { status: 204 }))

    await api.post('/auth/abmelden', {})

    expect(gesehen).toEqual(['abc'])
  })

  it('holt das Token vor dem ersten Nicht-GET (Direktaufruf von /partie/:id)', async () => {
    const s = sitzung('t1')
    const gesehen = geschuetzt('/partien/p1/zuege', 't1', () => HttpResponse.json({ id: 'p1' }))

    const z = await api.post<{ id: string }>('/partien/p1/zuege', { halbzug: 0, zug: 'e2e4' })

    expect(z.id).toBe('p1')
    expect(s.aufrufe).toBe(1)
    expect(gesehen).toEqual(['t1'])
  })

  it('erneuert ein abgelehntes Token einmal und wiederholt die Anfrage', async () => {
    setCsrfToken('alt')
    const s = sitzung('neu')
    const gesehen = geschuetzt('/partien', 'neu', () => HttpResponse.json({ id: 'p2' }, { status: 201 }))

    const z = await api.post<{ id: string }>('/partien', { gegnerId: 'oscar', farbe: 'Zufall', bedenkzeit: 'OhneUhr' })

    expect(z.id).toBe('p2')
    expect(s.aufrufe).toBe(1)
    expect(gesehen).toEqual(['alt', 'neu'])
  })

  it('wiederholt nur einmal und nie bei 400 mit Fehlercode', async () => {
    setCsrfToken('t')
    sitzung('t')
    let aufrufe = 0
    server.use(
      http.post(`${API_BASIS}/partien/p1/zuege`, () => {
        aufrufe++
        return HttpResponse.json({ fehler: 'ZugUnzulaessig', zustand: null, offenePartieId: null }, { status: 400 })
      }),
    )

    const fehler = (await api.post('/partien/p1/zuege', { halbzug: 0, zug: 'e2e5' }).catch((e: unknown) => e)) as ApiFehler

    expect(fehler.fehler).toBe('ZugUnzulaessig')
    expect(aufrufe).toBe(1)
  })

  it('liefert bei 409 Code und Zustand', async () => {
    setCsrfToken('t')
    server.use(
      http.post(`${API_BASIS}/partien/p1/zuege`, () =>
        HttpResponse.json({ fehler: 'HalbzugVeraltet', zustand: { id: 'p1' }, offenePartieId: null }, { status: 409 }),
      ),
    )

    const fehler = await api.post('/partien/p1/zuege', { halbzug: 3, zug: 'e2e4' }).catch((e: unknown) => e)

    expect(fehler).toBeInstanceOf(ApiFehler)
    expect((fehler as ApiFehler).fehler).toBe('HalbzugVeraltet')
    expect((fehler as ApiFehler).antwort?.zustand?.id).toBe('p1')
  })

  it('liest Retry-After bei 429 auch ohne Body (Rate-Limiter)', async () => {
    setCsrfToken('t')
    server.use(
      http.post(`${API_BASIS}/partien/p1/zuege`, () => new HttpResponse(null, { status: 429, headers: { 'Retry-After': '2' } })),
    )

    const fehler = (await api.post('/partien/p1/zuege', {}).catch((e: unknown) => e)) as ApiFehler

    expect(fehler.status).toBe(429)
    expect(fehler.fehler).toBeNull()
    expect(fehler.retryAfterSekunden).toBe(2)
  })

  it('liefert bei 204 undefined (GET /partien/offen ohne offene Partie)', async () => {
    server.use(http.get(`${API_BASIS}/partien/offen`, () => new HttpResponse(null, { status: 204 })))

    expect(await api.get('/partien/offen')).toBeUndefined()
  })
})
