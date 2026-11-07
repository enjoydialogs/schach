import { getResponse, isPassthroughResponse } from 'msw'
import { FakeServer } from './fakeServer'
import { fakeHandlers } from './handlers'

// Startet den Fake im Browser. Die MSW-Handler laufen ueber `getResponse` direkt in der Seite statt
// ueber einen Service Worker: das geht auch ohne sicheren Kontext (`--host` ueber LAN-IP) und
// kommt dem spaeteren PWA-Service-Worker auf `/` nicht in die Quere. Nur ueber dynamischen Import
// erreichbar, wenn `import.meta.env.DEV` oder der Bau-Modus `prototyp` gilt.

function sitzungsSpeicher(): Storage | null {
  try {
    return sessionStorage
  } catch {
    return null
  }
}

export const fakeServer = new FakeServer({ speicher: sitzungsSpeicher() })

let installiert = false

export function starteFake(): Promise<void> {
  if (installiert) return Promise.resolve()
  installiert = true
  const handlers = fakeHandlers(fakeServer)
  const netz = globalThis.fetch.bind(globalThis)

  globalThis.fetch = async (eingabe: RequestInfo | URL, init?: RequestInit): Promise<Response> => {
    const anfrage =
      eingabe instanceof Request ? new Request(eingabe, init) : new Request(new URL(String(eingabe), location.href), init)
    const url = new URL(anfrage.url)
    if (url.origin === location.origin && url.pathname.startsWith('/api/')) {
      const antwort = await getResponse(handlers, anfrage)
      if (antwort !== undefined && !isPassthroughResponse(antwort)) return antwort
    }
    return netz(eingabe, init)
  }
  return Promise.resolve()
}
