import { render } from '@testing-library/react'
import { setupServer } from 'msw/node'
import { createMemoryRouter, RouterProvider } from 'react-router'
import { afterAll, afterEach, beforeAll, beforeEach, vi } from 'vitest'
import { FakeServer } from '../fake/fakeServer'
import { fakeHandlers } from '../fake/handlers'
import { routes } from '../router'
import { setzeHinweisZurueck } from './hinweisSpeicher'
import { setzeSitzungZurueck } from './sitzung'

// Testhilfe fuer Bildschirmtests (nur Vitest): die ganze App ueber die echten Routen gegen den
// Fake-Server per msw/node, derselbe Vertrag wie im Browser.

export function mitFakeServer() {
  const server = setupServer()
  const stand = { fake: new FakeServer() }

  beforeAll(() => {
    server.listen({ onUnhandledFrame: 'error' })
    // jsdom hat kein Layout; react-chessboard misst Felder (Nachspielen).
    vi.spyOn(Element.prototype, 'getBoundingClientRect').mockReturnValue(new DOMRect(0, 0, 48, 48))
    vi.spyOn(console, 'info').mockImplementation(() => {})
  })
  afterAll(() => {
    server.close()
    vi.restoreAllMocks()
  })
  beforeEach(() => {
    // Uhr 10 s zurueck: Gegnerzuege sind sofort sichtbar.
    stand.fake = new FakeServer({ jetzt: () => Date.now() - 10_000, zufall: () => 0.25 })
    server.resetHandlers(...fakeHandlers(stand.fake))
    setzeSitzungZurueck()
    setzeHinweisZurueck()
  })
  afterEach(() => {
    localStorage.clear()
    sessionStorage.clear()
  })

  return {
    server,
    get fake() {
      return stand.fake
    },
  }
}

/** Rendert die App mit Speicher-Router ab `pfad`; liefert den Router fuer Pfad-Pruefungen. */
export function zeigeApp(pfad: string) {
  const router = createMemoryRouter(routes, { initialEntries: [pfad] })
  render(<RouterProvider router={router} />)
  return router
}
