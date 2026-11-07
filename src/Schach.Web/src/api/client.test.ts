import { afterEach, vi } from 'vitest'
import { api, ApiFehler, CSRF_HEADER, setCsrfToken } from './client'

function antworte(status: number, body: unknown, headers: Record<string, string> = {}) {
  const fetchMock = vi.fn().mockResolvedValue(
    new Response(body === undefined ? null : JSON.stringify(body), { status, headers }),
  )
  vi.stubGlobal('fetch', fetchMock)
  return fetchMock
}

afterEach(() => {
  vi.unstubAllGlobals()
  setCsrfToken(null)
})

describe('api client', () => {
  it('ruft /api/v1 mit Cookie der gleichen Herkunft auf', async () => {
    const fetchMock = antworte(200, { angemeldet: false })

    const sitzung = await api.get<{ angemeldet: boolean }>('/sitzung')

    expect(sitzung.angemeldet).toBe(false)
    const [url, init] = fetchMock.mock.calls[0] as [string, RequestInit]
    expect(url).toBe('/api/v1/sitzung')
    expect(init.credentials).toBe('same-origin')
    expect((init.headers as Record<string, string>)[CSRF_HEADER]).toBeUndefined()
  })

  it('schickt das CSRF-Token bei Nicht-GET', async () => {
    const fetchMock = antworte(204, undefined)
    setCsrfToken('abc')

    await api.post('/auth/abmelden', {})

    const [, init] = fetchMock.mock.calls[0] as [string, RequestInit]
    expect((init.headers as Record<string, string>)[CSRF_HEADER]).toBe('abc')
  })

  it('liefert bei 409 Code und Zustand', async () => {
    antworte(409, { fehler: 'HalbzugVeraltet', zustand: { id: 'p1' }, offenePartieId: null })

    const fehler = await api.post('/partien/p1/zuege', { halbzug: 3, zug: 'e2e4' }).catch((e: unknown) => e)

    expect(fehler).toBeInstanceOf(ApiFehler)
    expect((fehler as ApiFehler).fehler).toBe('HalbzugVeraltet')
    expect((fehler as ApiFehler).antwort?.zustand?.id).toBe('p1')
  })

  it('liest Retry-After bei 429', async () => {
    antworte(429, { fehler: 'TageslimitErreicht', zustand: null, offenePartieId: null }, { 'Retry-After': '2' })

    const fehler = (await api.post('/partien', {}).catch((e: unknown) => e)) as ApiFehler

    expect(fehler.status).toBe(429)
    expect(fehler.retryAfterSekunden).toBe(2)
  })
})
