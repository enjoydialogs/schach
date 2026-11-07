import { http, HttpResponse, passthrough, type HttpResponseResolver } from 'msw'
import { API_BASIS } from '../api/client'
import type { FehlerAntwort, HalbzugAnfrage, NeuePartieAnfrage, ZugAnfrage } from '../api/types'
import { FAKE_ID_PRAEFIX, FakeFehler, type FakeServer } from './fakeServer'

// MSW-Handler fuer den Fake-Server (Browser: fake/starten.ts, Tests: msw/node). Partie-Endpunkte
// antworten nur fuer Fake-Ids (`fake-…`); alles andere geht unveraendert ans Netz.

type Parameter = { id: string }

function antworte(arbeit: () => unknown, status = 200) {
  try {
    return HttpResponse.json(arbeit() as object, { status })
  } catch (e) {
    if (!(e instanceof FakeFehler)) throw e
    if (e.fehler === null) return new HttpResponse(null, { status: e.status })
    const body: FehlerAntwort = { fehler: e.fehler, zustand: e.zustand, offenePartieId: null }
    return HttpResponse.json(body, { status: e.status })
  }
}

export function fakeHandlers(server: FakeServer) {
  const partie =
    (arbeit: (id: string, body: unknown) => unknown): HttpResponseResolver<Parameter> =>
    async ({ params, request }) => {
      const id = params.id
      if (!id.startsWith(FAKE_ID_PRAEFIX)) return passthrough()
      const body: unknown = request.method === 'GET' ? undefined : await request.json()
      return antworte(() => arbeit(id, body))
    }

  return [
    http.get(`${API_BASIS}/sitzung`, () => antworte(() => server.sitzung())),
    http.post(`${API_BASIS}/partien`, async ({ request }) => {
      const anfrage = (await request.json()) as NeuePartieAnfrage
      return antworte(() => server.starte(anfrage), 201)
    }),
    http.get<Parameter>(`${API_BASIS}/partien/:id`, partie((id) => server.lies(id))),
    http.post<Parameter>(`${API_BASIS}/partien/:id/zuege`, partie((id, b) => server.ziehe(id, b as ZugAnfrage))),
    http.post<Parameter>(
      `${API_BASIS}/partien/:id/zuruecknehmen`,
      partie((id, b) => server.nimmZurueck(id, b as HalbzugAnfrage)),
    ),
    http.post<Parameter>(`${API_BASIS}/partien/:id/aufgeben`, partie((id, b) => server.gibAuf(id, b as HalbzugAnfrage))),
    http.post<Parameter>(
      `${API_BASIS}/partien/:id/remis-angebot`,
      partie((id, b) => server.bieteRemis(id, b as HalbzugAnfrage)),
    ),
    http.post<Parameter>(`${API_BASIS}/partien/:id/tipp`, partie((id, b) => server.tipp(id, b as HalbzugAnfrage))),
  ]
}
