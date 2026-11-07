import { linkToken, nurZiffern, restSekunden } from './linkCode'

describe('linkToken', () => {
  it('liest das Token aus dem Fragment', () => {
    expect(linkToken('#t=abc-_123')).toBe('abc-_123')
  })

  it('unterscheidet kein Fragment von einem unvollstaendigen Link', () => {
    expect(linkToken('')).toBeNull()
    expect(linkToken('#')).toBeNull()
    expect(linkToken('#t=')).toBe('')
    expect(linkToken('#x=1')).toBe('')
  })
})

describe('nurZiffern', () => {
  it('entfernt Leerzeichen und Fremdzeichen', () => {
    expect(nurZiffern('123 456')).toBe('123456')
    expect(nurZiffern(' 12-34 56 ')).toBe('123456')
  })
})

describe('restSekunden', () => {
  it('zaehlt 60 s herunter und bleibt bei 0', () => {
    expect(restSekunden(0, 0)).toBe(60)
    expect(restSekunden(0, 59_500)).toBe(1)
    expect(restSekunden(0, 60_000)).toBe(0)
    expect(restSekunden(0, 90_000)).toBe(0)
  })
})
