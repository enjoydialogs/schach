import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { createMemoryRouter, RouterProvider } from 'react-router'
import { routes } from '../router'

function oeffne(pfad: string) {
  render(<RouterProvider router={createMemoryRouter(routes, { initialEntries: [pfad] })} />)
}

const DATENSCHUTZ_ABSCHNITTE = [
  '1. Verantwortlicher',
  '2. Aufruf der Website',
  '3. Als Gast spielen',
  '4. Spielerkonto und Anmeldung per Link',
  '5. E-Mails',
  '6. Sprüche und Sprachmodell',
  '7. Kontakt',
  '8. Cookies und Browser-Speicher',
  '9. Hosting und Datensicherung',
  '10. Weitergabe',
  '11. Ihre Rechte',
  '12. Keine automatisierten Entscheidungen',
  '13. Änderungen',
]

describe('Rechtstexte', () => {
  it('zeigt auf /impressum das Impressum mit Anbieter und Anschrift', () => {
    oeffne('/impressum')

    expect(screen.getByRole('heading', { level: 1, name: 'Impressum' })).toBeInTheDocument()
    expect(screen.getByText('Angaben gemäß § 5 Digitale-Dienste-Gesetz (DDG)')).toBeInTheDocument()
    expect(screen.getByRole('heading', { level: 2, name: 'Anbieter' })).toBeInTheDocument()
    expect(screen.getByText(/Albert-Einstein-Str\. 6/)).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'info@enjoy-dialogs.de' })).toHaveAttribute(
      'href',
      'mailto:info@enjoy-dialogs.de',
    )
  })

  it.each(['/impressum', '/datenschutz'])('bietet auf %s nur E-Mail-Kontakt, kein Formular und nichts Fremdes', (pfad) => {
    const { container } = render(
      <RouterProvider router={createMemoryRouter(routes, { initialEntries: [pfad] })} />,
    )

    expect(container.querySelector('script, iframe, form')).toBeNull()
    expect(document.body).not.toHaveTextContent(/aidbase|Kontaktformular:/i)
    const fremd = [...container.querySelectorAll('a[href^="http"]')]
      .map((a) => a.getAttribute('href') ?? '')
      .filter((href) => !href.startsWith('https://github.com/enjoydialogs/schach'))
    expect(fremd).toEqual([])
  })

  it('nennt die Reply-To-Adresse der Anmelde-Mail', () => {
    oeffne('/datenschutz')

    expect(screen.getByRole('link', { name: 'support@enjoy-dialogs.de' })).toHaveAttribute(
      'href',
      'mailto:support@enjoy-dialogs.de',
    )
  })

  it('zeigt auf /datenschutz alle 13 Abschnitte in Reihenfolge', () => {
    oeffne('/datenschutz')

    expect(screen.getByRole('heading', { level: 1, name: 'Datenschutzerklärung' })).toBeInTheDocument()
    const abschnitte = screen.getAllByRole('heading', { level: 2 }).map((h) => h.textContent)
    expect(abschnitte).toEqual(DATENSCHUTZ_ABSCHNITTE)
    expect(screen.getByText(/Albert-Einstein-Str\. 6/)).toBeInTheDocument()
  })

  it('nennt Lettermint, den Mistral-EU-Endpunkt und Hostinger', () => {
    oeffne('/datenschutz')

    expect(screen.getAllByText(/Lettermint B\.V\./).length).toBeGreaterThan(0)
    expect(screen.getByText('api.eu.mistral.ai')).toBeInTheDocument()
    expect(screen.getAllByText(/Hostinger International Ltd\./).length).toBeGreaterThan(0)
    expect(screen.getByText(/Hetzner Online GmbH in Falkenstein/)).toBeInTheDocument()
  })

  it('listet jeden Browser-Speicher aus Spec 12.3 in der Tabelle', () => {
    oeffne('/datenschutz')

    const tabelle = screen.getByRole('table')
    for (const name of [
      'schach_sitzung',
      '.AspNetCore.Antiforgery.…',
      'schach-partien-v1',
      'gastPartieId',
      'einstellungen',
      'installHinweis',
      'anmeldungId',
    ]) {
      expect(within(tabelle).getByText(name)).toBeInTheDocument()
    }
    expect(within(tabelle).getByText('Service-Worker-Precache')).toBeInTheDocument()
  })

  it.each(['/impressum', '/datenschutz'])('zeigt den Prüfvermerk der Quelldateien nie auf %s', (pfad) => {
    oeffne(pfad)
    expect(document.body).not.toHaveTextContent(/nicht rechtlich geprüft/i)
  })

  it('erreicht beide Seiten über die Fusszeile', async () => {
    const user = userEvent.setup()
    oeffne('/')

    await user.click(within(screen.getByRole('contentinfo')).getByRole('link', { name: 'Impressum' }))
    expect(screen.getByRole('heading', { level: 1, name: 'Impressum' })).toBeInTheDocument()

    await user.click(within(screen.getByRole('contentinfo')).getByRole('link', { name: 'Datenschutz' }))
    expect(screen.getByRole('heading', { level: 1, name: 'Datenschutzerklärung' })).toBeInTheDocument()
  })
})
