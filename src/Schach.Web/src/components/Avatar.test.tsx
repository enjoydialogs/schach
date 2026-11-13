import { render, screen } from '@testing-library/react'
import { FAKE_GEGNER } from '../fake/fakeServer'
import { Avatar } from './Avatar'
import { GESICHTER } from './gesichter/katalog'

const KATALOG_KEYS = ['ilse', 'finn', 'oscar', 'nele', 'theo', 'sementa', 'konrad', 'maren']

function zeige(props: Parameters<typeof Avatar>[0]) {
  const { container } = render(<Avatar {...props} />)
  const avatar = container.querySelector('.avatar')
  if (avatar === null) throw new Error('kein Avatar gerendert')
  return avatar
}

describe('Avatar', () => {
  it.each(KATALOG_KEYS)('zeigt fuer %s ein Gesicht statt des Buchstabens', (key) => {
    const avatar = zeige({ gegner: { key, name: 'Xaver', stufe: 'Meister' } })
    expect(avatar.querySelector('.avatar__gesicht svg')).not.toBeNull()
    expect(avatar).toHaveClass('avatar--gesicht')
    expect(avatar).not.toHaveClass('avatar--meister')
    expect(avatar.textContent).toBe('')
  })

  it('zeigt bei unbekanntem Key den Anfangsbuchstaben in der Stufenfarbe', () => {
    const avatar = zeige({ gegner: { key: 'unbekannt', name: 'Xaver', stufe: 'Meister' } })
    expect(avatar.querySelector('svg')).toBeNull()
    expect(avatar).toHaveClass('avatar--meister')
    expect(avatar.textContent).toBe('X')
  })

  it('liefert fuer geerbte Objekt-Eigenschaften kein Gesicht', () => {
    const avatar = zeige({ gegner: { key: 'toString', name: 'Xaver', stufe: 'Anfaenger' } })
    expect(avatar.textContent).toBe('X')
  })

  it('setzt die Groessenklassen', () => {
    expect(zeige({ gegner: { key: 'ilse', name: 'Ilse', stufe: 'Anfaenger' } }).className).toBe('avatar avatar--gesicht')
    expect(zeige({ gegner: { key: 'ilse', name: 'Ilse', stufe: 'Anfaenger' }, groesse: 48 })).toHaveClass('avatar--48')
    expect(zeige({ gegner: { key: 'ilse', name: 'Ilse', stufe: 'Anfaenger' }, groesse: 96 })).toHaveClass('avatar--96')
  })

  it('zeigt das Besiegt-Haekchen neben dem Gesicht, nicht darin', () => {
    const avatar = zeige({ gegner: { key: 'finn', name: 'Finn', stufe: 'Anfaenger' }, besiegt: true })
    const haken = screen.getByTestId('besiegt')
    expect(haken.parentElement).toBe(avatar)
  })

  it('hat fuer jeden Gegner des Katalogs ein Gesicht', () => {
    expect(Object.keys(GESICHTER).sort()).toEqual([...KATALOG_KEYS].sort())
    for (const gegner of FAKE_GEGNER) expect(GESICHTER).toHaveProperty(gegner.key)
  })
})
