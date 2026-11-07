import { render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router'
import { Fusszeile } from './Fusszeile'
import { kurzSha, REPO_URL } from './quellcode'

function zeige(commitSha?: string) {
  render(
    <MemoryRouter>
      <Fusszeile commitSha={commitSha} />
    </MemoryRouter>,
  )
}

describe('Fusszeile', () => {
  it('zeigt Impressum, Datenschutz und Quellcode', () => {
    zeige('0123456789abcdef')

    expect(screen.getByRole('link', { name: 'Impressum' })).toHaveAttribute('href', '/impressum')
    expect(screen.getByRole('link', { name: 'Datenschutz' })).toHaveAttribute('href', '/datenschutz')
    expect(screen.getByRole('link', { name: 'Quellcode 0123456' })).toHaveAttribute(
      'href',
      `${REPO_URL}/tree/0123456`,
    )
  })

  it('verlinkt ohne Commit-SHA das Repo und zeigt "dev"', () => {
    zeige(undefined)

    expect(screen.getByRole('link', { name: 'Quellcode dev' })).toHaveAttribute('href', REPO_URL)
  })
})

describe('kurzSha', () => {
  it('kuerzt auf 7 Zeichen und verwirft Unsinn', () => {
    expect(kurzSha('ABCDEF0123456')).toBe('abcdef0')
    expect(kurzSha('')).toBeNull()
    expect(kurzSha('kein-sha')).toBeNull()
  })
})
