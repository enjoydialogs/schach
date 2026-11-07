import { http, HttpResponse, passthrough, type HttpResponseResolver } from 'msw'
import { API_BASIS } from '../api/client'
import type {
  AnmeldungAnfrage,
  BestaetigenAnfrage,
  FehlerAntwort,
  HalbzugAnfrage,
  NeuePartieAnfrage,
  ZugAnfrage,
} from '../api/types'
import { FAKE_ID_PRAEFIX, FakeFehler, type FakeServer } from './fakeServer'

// MSW-Handler fuer den Fake-Server (Browser: fake/starten.ts, Tests: msw/node). Partie-Endpunkte
// antworten nur fuer Fake-Ids (`fake-…`); alles andere geht unveraendert ans Netz.

type Parameter = { id: string }

/** Wie `antworte`, aber ohne Body (204). */
function ohneInhalt(arbeit: () => void) {
  return antworte(() => {
    arbeit()
    return null
  }, 204)
}

function antworte(arbeit: () => unknown, status = 200) {
  try {
    const body = arbeit()
    if (body === null) return new HttpResponse(null, { status })
    return HttpResponse.json(body as object, { status })
  } catch (e) {
    if (!(e instanceof FakeFehler)) throw e
    if (e.fehler === null) return new HttpResponse(null, { status: e.status })
    const body: FehlerAntwort = { fehler: e.fehler, zustand: e.zustand, offenePartieId: e.offenePartieId }
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
    // S3b: vor `/partien/:id`, sonst faengt der Partie-Handler "offen" als Id ab.
    http.get(`${API_BASIS}/partien/offen`, () => antworte(() => server.offene())),
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

    // S3b: Gegner, Partienliste, Konto und Sitzung (SCHACH-SPEC 8.3, 9.3).
    http.get(`${API_BASIS}/gegner`, () => antworte(() => server.gegnerListe())),
    http.get(`${API_BASIS}/partien`, ({ request }) => {
      const query = new URL(request.url).searchParams
      const limit = Math.min(Math.max(Number(query.get('limit') ?? 50) || 50, 1), 50)
      return antworte(() => server.partienListe(limit, query.get('vorId')))
    }),
    http.post(`${API_BASIS}/auth/anmeldung`, async ({ request }) => {
      const anfrage = (await request.json()) as AnmeldungAnfrage
      return antworte(() => {
        const antwort = server.meldeAn(anfrage.email)
        const a = server.letzteAnmeldung
        // Statt einer Mail: Link und Code in der Konsole des Dev-Servers bzw. Browsers.
        if (a !== null) console.info(`[Fake-Mail an ${a.email}] Link: /anmelden#t=${a.token} · Code: ${a.code}`)
        return antwort
      }, 202)
    }),
    http.post(`${API_BASIS}/auth/anmeldung/bestaetigen`, async ({ request }) => {
      const anfrage = (await request.json()) as BestaetigenAnfrage
      return antworte(() => server.bestaetige(anfrage))
    }),
    http.post(`${API_BASIS}/auth/abmelden`, () => ohneInhalt(() => server.meldeAb())),
    http.delete(`${API_BASIS}/konto`, () => ohneInhalt(() => server.loescheKonto())),
    http.get(`${API_BASIS}/konto/export`, () => antworte(() => server.export())),
  ]
}
