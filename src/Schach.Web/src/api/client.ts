import type { FehlerAntwort, FehlerCode } from './types'

/** Praefix aller Endpunkte (ApiJson.RoutePrefix). */
export const API_BASIS = '/api/v1'

/** Header, unter dem das CSRF-Token aus `GET /sitzung` mitgeht (SCHACH-SPEC 9.4). */
export const CSRF_HEADER = 'X-CSRF-TOKEN'

// Das Token kommt aus `GET /sitzung` und lebt nur im Speicher, nie im localStorage (9.1).
let csrfToken: string | null = null

export function setCsrfToken(token: string | null): void {
  csrfToken = token
}

/**
 * Ein Fehler der API. `fehler` ist der Code aus der Antwort (8.5), falls der Server einen schickt;
 * `antwort` der vollstaendige Body, damit der Aufrufer bei 409 den mitgeschickten Zustand
 * uebernimmt. `retryAfterSekunden` aus dem Header `Retry-After` bei 429.
 */
export class ApiFehler extends Error {
  readonly status: number
  readonly fehler: FehlerCode | null
  readonly antwort: FehlerAntwort | null
  readonly retryAfterSekunden: number | null

  constructor(status: number, antwort: FehlerAntwort | null, retryAfterSekunden: number | null) {
    super(antwort?.fehler ?? `HTTP ${status}`)
    this.name = 'ApiFehler'
    this.status = status
    this.fehler = antwort?.fehler ?? null
    this.antwort = antwort
    this.retryAfterSekunden = retryAfterSekunden
  }
}

async function anfrage<T>(methode: string, pfad: string, body?: unknown): Promise<T> {
  const headers: Record<string, string> = { Accept: 'application/json' }
  if (body !== undefined) headers['Content-Type'] = 'application/json'
  if (methode !== 'GET' && csrfToken !== null) headers[CSRF_HEADER] = csrfToken

  const antwort = await fetch(`${API_BASIS}${pfad}`, {
    method: methode,
    headers,
    // Sitzung per HttpOnly-Cookie (9.4), nur gleiche Herkunft.
    credentials: 'same-origin',
    body: body !== undefined ? JSON.stringify(body) : undefined,
  })

  const text = await antwort.text()
  if (!antwort.ok) {
    let fehler: FehlerAntwort | null = null
    try {
      const geparst = JSON.parse(text) as Partial<FehlerAntwort>
      if (typeof geparst.fehler === 'string') fehler = geparst as FehlerAntwort
    } catch {
      // kein JSON (z. B. Proxy-Fehlerseite): nur der Status zaehlt
    }
    const retryAfter = antwort.headers.get('Retry-After')
    throw new ApiFehler(antwort.status, fehler, retryAfter !== null ? Number(retryAfter) : null)
  }
  return (text.length > 0 ? JSON.parse(text) : undefined) as T
}

export const api = {
  get: <T>(pfad: string) => anfrage<T>('GET', pfad),
  post: <T>(pfad: string, body?: unknown) => anfrage<T>('POST', pfad, body),
  del: <T>(pfad: string) => anfrage<T>('DELETE', pfad),
}
