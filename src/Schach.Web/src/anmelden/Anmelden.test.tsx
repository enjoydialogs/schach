import { act, fireEvent, screen, waitFor, within } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { API_BASIS } from '../api/client'
import { mitFakeServer, zeigeApp } from '../components/testApp'

// Anmelden (SCHACH-SPEC 9.3, 9.5, 10.10).

const t = mitFakeServer()

beforeEach(() => {
  t.fake.gast = true
})

async function sende(adresse = 'neu@example.invalid') {
  fireEvent.change(await screen.findByLabelText('E-Mail-Adresse'), { target: { value: adresse } })
  fireEvent.click(screen.getByRole('button', { name: 'Link senden' }))
}

describe('Anmelden', () => {
  it('zeigt Text, Datenschutz-Link und „Als Gast weiterspielen“', async () => {
    zeigeApp('/anmelden')
    expect(
      await screen.findByText(
        'Wir schicken dir einen Anmeldelink per E-Mail. Kein Passwort nötig. Mit einer neuen Adresse entsteht ein neues Konto.',
      ),
    ).toBeInTheDocument()
    expect(within(screen.getByRole('main')).getByRole('link', { name: 'Datenschutz' })).toHaveAttribute('href', '/datenschutz')
    expect(screen.getByRole('link', { name: 'Als Gast weiterspielen' })).toHaveAttribute('href', '/')
    expect(screen.queryByRole('checkbox')).toBeNull()
    expect(screen.queryByText('Deine laufende Gastpartie endet beim Anmelden.')).toBeNull()
  })

  it('warnt bei laufender Gast-Partie', async () => {
    localStorage.setItem('gastPartieId', 'fake-gast')
    zeigeApp('/anmelden')
    expect(await screen.findByText('Deine laufende Gastpartie endet beim Anmelden.')).toBeInTheDocument()
  })

  it('sendet den Link und meldet per Code an', async () => {
    localStorage.setItem('gastPartieId', 'fake-gast')
    const router = zeigeApp('/anmelden')
    await sende()

    expect(
      await screen.findByText('Mail geschickt. Tippe dort auf ‚Jetzt anmelden‘ oder gib den Code ein'),
    ).toBeInTheDocument()
    expect(sessionStorage.getItem('anmeldungId')).toBe(t.fake.letzteAnmeldung!.anmeldungId)

    const code = t.fake.letzteAnmeldung!.code
    fireEvent.change(screen.getByLabelText('Code aus der Mail'), { target: { value: `${code.slice(0, 3)} ${code.slice(3)}` } })
    fireEvent.click(screen.getByRole('button', { name: 'Anmelden' }))

    await waitFor(() => expect(router.state.location.pathname).toBe('/'))
    expect(await screen.findByRole('link', { name: 'Elo 800' })).toBeInTheDocument()
    expect(sessionStorage.getItem('anmeldungId')).toBeNull()
    expect(localStorage.getItem('gastPartieId')).toBeNull()
  })

  it('meldet einen falschen Code', async () => {
    zeigeApp('/anmelden')
    await sende()
    fireEvent.change(await screen.findByLabelText('Code aus der Mail'), { target: { value: '99999x' } })
    fireEvent.change(screen.getByLabelText('Code aus der Mail'), {
      target: { value: t.fake.letzteAnmeldung!.code === '999999' ? '000000' : '999999' },
    })
    fireEvent.click(screen.getByRole('button', { name: 'Anmelden' }))
    expect(await screen.findByText('Der Code stimmt nicht oder ist abgelaufen.')).toBeInTheDocument()
  })

  it('erlaubt „Erneut senden“ erst nach 60 s und „Andere Adresse“ sofort', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true })
    try {
      zeigeApp('/anmelden')
      await sende()
      const erneut = await screen.findByRole('button', { name: /Erneut senden/ })
      expect(erneut).toBeDisabled()
      expect(erneut).toHaveTextContent('Erneut senden in 60 s')

      await act(() => vi.advanceTimersByTimeAsync(60_000))
      expect(screen.getByRole('button', { name: 'Erneut senden' })).toBeEnabled()
      const erste = t.fake.letzteAnmeldung!.anmeldungId
      fireEvent.click(screen.getByRole('button', { name: 'Erneut senden' }))
      await waitFor(() => expect(t.fake.letzteAnmeldung!.anmeldungId).not.toBe(erste))
      await waitFor(() => expect(sessionStorage.getItem('anmeldungId')).toBe(t.fake.letzteAnmeldung!.anmeldungId))

      fireEvent.click(screen.getByRole('button', { name: 'Andere Adresse' }))
      expect(screen.getByLabelText('E-Mail-Adresse')).toBeInTheDocument()
      expect(sessionStorage.getItem('anmeldungId')).toBeNull()
    } finally {
      vi.useRealTimers()
    }
  })

  it('meldet Formfehler, Versandfehler und Limit', async () => {
    zeigeApp('/anmelden')
    await sende('keine-adresse')
    expect(await screen.findByText('Bitte gib eine gültige E-Mail-Adresse ein.')).toBeInTheDocument()

    t.server.use(
      http.post(`${API_BASIS}/auth/anmeldung`, () =>
        HttpResponse.json({ fehler: 'MailNichtGesendet', zustand: null, offenePartieId: null }, { status: 503 }),
      { once: true }),
    )
    await sende()
    expect(
      await screen.findByText('Die Mail konnte nicht gesendet werden. Bitte versuch es später noch einmal.'),
    ).toBeInTheDocument()

    t.server.use(http.post(`${API_BASIS}/auth/anmeldung`, () => new HttpResponse(null, { status: 429 }), { once: true }))
    await sende()
    expect(await screen.findByText('Zu viele Versuche. Bitte warte kurz.')).toBeInTheDocument()
  })

  it('landet per Link mit Knopf „Jetzt anmelden“ und sendet nie beim Laden', async () => {
    t.fake.meldeAn('link@example.invalid')
    const token = t.fake.letzteAnmeldung!.token
    let bestaetigt = 0
    t.server.events.on('request:start', ({ request }) => {
      if (new URL(request.url).pathname.endsWith('/auth/anmeldung/bestaetigen')) bestaetigt++
    })
    const router = zeigeApp(`/anmelden#t=${token}`)

    const knopf = await screen.findByRole('button', { name: 'Jetzt anmelden' })
    expect(bestaetigt).toBe(0)
    fireEvent.click(knopf)

    await waitFor(() => expect(router.state.location.pathname).toBe('/'))
    expect(router.state.location.hash).toBe('')
    expect(await screen.findByRole('link', { name: 'Elo 800' })).toBeInTheDocument()
    expect(bestaetigt).toBe(1)
    t.server.events.removeAllListeners()
  })

  it('meldet einen verbrauchten Link als ungueltig', async () => {
    t.fake.meldeAn('link@example.invalid')
    const token = t.fake.letzteAnmeldung!.token
    t.fake.bestaetige({ token })
    t.fake.meldeAb()
    zeigeApp(`/anmelden#t=${token}`)

    fireEvent.click(await screen.findByRole('button', { name: 'Jetzt anmelden' }))
    expect(await screen.findByText('Dieser Link ist ungültig oder abgelaufen. Fordere einen neuen an.')).toBeInTheDocument()
    expect(screen.getByLabelText('E-Mail-Adresse')).toBeInTheDocument()
  })

  it('zeigt angemeldet beim Link nur einen Hinweis', async () => {
    t.fake.gast = false
    zeigeApp('/anmelden#t=irgendwas')
    expect(await screen.findByText('Du bist schon angemeldet.')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Jetzt anmelden' })).toBeNull()
  })

  it('meldet einen Link ohne Token als unvollstaendig und bietet den Code an', async () => {
    t.fake.meldeAn('a@example.invalid')
    sessionStorage.setItem('anmeldungId', t.fake.letzteAnmeldung!.anmeldungId)
    zeigeApp('/anmelden#t=')
    expect(await screen.findByText('Link unvollständig, bitte Code eingeben')).toBeInTheDocument()
    expect(screen.getByLabelText('Code aus der Mail')).toBeInTheDocument()
  })

  it('prueft die Sitzung erneut, wenn die App wieder sichtbar wird', async () => {
    const router = zeigeApp('/anmelden')
    await sende()
    await screen.findByLabelText('Code aus der Mail')

    // In einem anderen Tab per Link angemeldet:
    t.fake.bestaetige({ token: t.fake.letzteAnmeldung!.token })
    document.dispatchEvent(new Event('visibilitychange'))
    await waitFor(() => expect(router.state.location.pathname).toBe('/'))
  })
})
