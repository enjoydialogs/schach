import { ApiFehler } from '../api/client'
import type { ZustandDto } from '../api/types'
import { leerePartienCache, mitCache, PARTIEN_CACHE, partienCache } from '../components/partienCache'
import { mitFakeCaches } from './fakeCaches'

// Cache `schach-partien-v1` (SCHACH-SPEC 10.13): nur beendete Partien, Netz zuerst, Kopie nur bei
// Netzfehlern, geleert bei Abmelden und Konto loeschen.

const cache = mitFakeCaches()
const zustand = (status: string) => ({ id: 'p1', status }) as unknown as ZustandDto

describe('partienCache', () => {
  it('merkt beendete Partien und laesst offene weg', async () => {
    await partienCache.merkePartie(zustand('Laeuft'))
    expect(await partienCache.partie('p1')).toBeNull()

    await partienCache.merkePartie(zustand('Beendet'))
    expect(await partienCache.partie('p1')).toMatchObject({ id: 'p1', status: 'Beendet' })
  })

  it('wird bei Abmelden oder Konto loeschen ganz geleert', async () => {
    await partienCache.merkePartie(zustand('Beendet'))
    await leerePartienCache()
    expect(await cache.caches.has(PARTIEN_CACHE)).toBe(false)
    expect(await partienCache.partie('p1')).toBeNull()
  })
})

describe('mitCache', () => {
  const kopie = () => Promise.resolve('kopie')
  const merke = () => Promise.resolve()

  it('nimmt das Netz, solange es antwortet', async () => {
    expect(await mitCache(() => Promise.resolve('netz'), merke, kopie)).toEqual({ wert: 'netz', ausCache: false })
  })

  it('nimmt bei Netzfehler die Kopie', async () => {
    expect(await mitCache(() => Promise.reject(new TypeError('offline')), merke, kopie)).toEqual({
      wert: 'kopie',
      ausCache: true,
    })
  })

  it('reicht Serverantworten (ApiFehler) durch statt der Kopie', async () => {
    const fehler = new ApiFehler(404, null, null)
    await expect(mitCache(() => Promise.reject(fehler), merke, kopie)).rejects.toBe(fehler)
  })

  it('reicht den Netzfehler durch, wenn keine Kopie da ist', async () => {
    const fehler = new TypeError('offline')
    await expect(mitCache(() => Promise.reject(fehler), merke, () => Promise.resolve(null))).rejects.toBe(fehler)
  })
})
