import { act, renderHook } from '@testing-library/react'
import { describe, expect, it, vi } from 'vitest'
import type { SpruchDto } from '../api/types'
import { naechsteAnsage, useVorlesen, Vorleser, type VorleserUmgebung } from './vorlesen'

interface FakeQuelle {
  buffer: unknown
  gestartet: boolean
  gestoppt: boolean
  onended: (() => void) | null
  connect: () => void
  start: () => void
  stop: () => void
}

function fakeUmgebung(status = 200) {
  const quellen: FakeQuelle[] = []
  const urls: string[] = []
  const offen: Array<() => void> = []
  const ctx = {
    destination: {},
    decodeAudioData: vi.fn(async (b: ArrayBuffer) => ({ laenge: b.byteLength })),
    createBufferSource: () => {
      const q: FakeQuelle = {
        buffer: null,
        gestartet: false,
        gestoppt: false,
        onended: null,
        connect: () => {},
        start() {
          this.gestartet = true
        },
        stop() {
          this.gestoppt = true
        },
      }
      quellen.push(q)
      return q
    },
  }
  let haengen = false
  const umgebung: VorleserUmgebung = {
    kontext: () => ctx as unknown as AudioContext,
    holen: (url) => {
      urls.push(url)
      const antwort = new Response(status === 204 ? null : new Uint8Array([1, 2, 3]), { status })
      if (!haengen) return Promise.resolve(antwort)
      return new Promise((fertig) => offen.push(() => fertig(antwort)))
    },
  }
  return {
    umgebung,
    quellen,
    urls,
    ctx,
    haengeAb: () => {
      haengen = true
    },
    loese: () => offen.splice(0).forEach((f) => f()),
  }
}

const spruch = (nachHalbzug: number, stimme: string | null = `tok${nachHalbzug}`): SpruchDto => ({
  nachHalbzug,
  ereignis: 'Zug',
  text: `Spruch ${nachHalbzug}`,
  stimme,
})

describe('Vorleser', () => {
  it('holt den Ton ueber das Token und spielt ihn ueber den AudioContext', async () => {
    const f = fakeUmgebung()
    await new Vorleser(f.umgebung).spiele('a_b-c')

    expect(f.urls).toEqual(['/api/v1/sprachausgabe/a_b-c'])
    expect(f.quellen).toHaveLength(1)
    expect(f.quellen[0]!.gestartet).toBe(true)
  })

  it('bleibt bei 204 still', async () => {
    const f = fakeUmgebung(204)
    await new Vorleser(f.umgebung).spiele('x')

    expect(f.ctx.decodeAudioData).not.toHaveBeenCalled()
    expect(f.quellen).toHaveLength(0)
  })

  it('bleibt bei Netzfehler still', async () => {
    const f = fakeUmgebung()
    f.umgebung.holen = () => Promise.reject(new TypeError('offline'))

    await expect(new Vorleser(f.umgebung).spiele('x')).resolves.toBeUndefined()
  })

  it('stoppt den laufenden Spruch, wenn ein neuer kommt', async () => {
    const f = fakeUmgebung()
    const v = new Vorleser(f.umgebung)
    await v.spiele('eins')
    await v.spiele('zwei')

    expect(f.quellen.map((q) => [q.gestartet, q.gestoppt])).toEqual([
      [true, true],
      [true, false],
    ])
  })

  it('spielt einen verspaeteten Ton nicht mehr, wenn inzwischen gestoppt wurde', async () => {
    const f = fakeUmgebung()
    f.haengeAb()
    const v = new Vorleser(f.umgebung)
    const laeuft = v.spiele('alt')
    v.stoppe()
    f.loese()
    await laeuft

    expect(f.quellen).toHaveLength(0)
  })
})

describe('naechsteAnsage', () => {
  it('liest den beim Laden sichtbaren Spruch nicht vor, den naechsten schon', () => {
    let s = naechsteAnsage(null, spruch(0), true)
    expect(s.token).toBeNull()

    s = naechsteAnsage(s.bekannt, spruch(0), true)
    expect(s.token).toBeNull()

    s = naechsteAnsage(s.bekannt, spruch(4), true)
    expect(s.token).toBe('tok4')
  })

  it('schweigt bei ausgeschaltetem Schalter und ohne Stimme, merkt sich den Spruch aber', () => {
    let s = naechsteAnsage(null, null, true)
    s = naechsteAnsage(s.bekannt, spruch(2), false)
    expect(s.token).toBeNull()
    s = naechsteAnsage(s.bekannt, spruch(2), true)
    expect(s.token).toBeNull()

    s = naechsteAnsage(s.bekannt, spruch(4, null), true)
    expect(s.token).toBeNull()
  })
})

describe('useVorlesen', () => {
  function attrappe() {
    return { spiele: vi.fn(async () => {}), stoppe: vi.fn() } as unknown as Vorleser & {
      spiele: ReturnType<typeof vi.fn>
      stoppe: ReturnType<typeof vi.fn>
    }
  }

  it('spielt nur neu ankommende Sprueche und wiederholt per Tipp', () => {
    const v = attrappe()
    const { rerender, result } = renderHook(({ s, geladen }) => useVorlesen(s, geladen, true, v), {
      initialProps: { s: null as SpruchDto | null, geladen: false },
    })
    rerender({ s: spruch(0), geladen: true })
    expect(v.spiele).not.toHaveBeenCalled()

    rerender({ s: spruch(2), geladen: true })
    expect(v.spiele).toHaveBeenCalledWith('tok2')

    act(() => result.current())
    expect(v.spiele).toHaveBeenCalledTimes(2)
  })

  it('stoppt beim Verlassen und wenn der Tab verschwindet', () => {
    const v = attrappe()
    const { unmount } = renderHook(() => useVorlesen(spruch(0), true, true, v))

    vi.spyOn(document, 'visibilityState', 'get').mockReturnValue('hidden')
    document.dispatchEvent(new Event('visibilitychange'))
    expect(v.stoppe).toHaveBeenCalledTimes(1)

    unmount()
    expect(v.stoppe).toHaveBeenCalledTimes(2)
    vi.restoreAllMocks()
  })

  it('stoppt, wenn der Schalter ausgeht', () => {
    const v = attrappe()
    const { rerender } = renderHook(({ aktiv }) => useVorlesen(spruch(0), true, aktiv, v), { initialProps: { aktiv: true } })

    rerender({ aktiv: false })

    expect(v.stoppe).toHaveBeenCalled()
  })
})
