import { manifest } from './manifest'

// Manifest (SCHACH-SPEC 10.13).

describe('Manifest', () => {
  it('traegt Name, Sprache, Bereich, Anzeige und Farben laut Spec', () => {
    expect(manifest).toMatchObject({
      name: 'Schach',
      short_name: 'Schach',
      lang: 'de',
      id: '/',
      start_url: '/',
      scope: '/',
      display: 'standalone',
      orientation: 'portrait',
      background_color: '#F4F2FB',
      theme_color: '#F4F2FB',
    })
  })

  it('hat Icons 192 und 512 plus 512 maskable, nie unter /assets/', () => {
    const icons = manifest.icons.map((i) => `${i.sizes} ${i.purpose}`)
    expect(icons).toEqual(['192x192 any', '512x512 any', '512x512 maskable'])
    for (const icon of manifest.icons) {
      expect(icon.type).toBe('image/png')
      // /assets/ bekommt ein Jahr immutable (SpaHosting.cs); Icons liegen unter fester URL.
      expect(icon.src).toMatch(/^\/icons\//)
    }
  })
})
