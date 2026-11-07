// Testhilfe (nur Vitest): Cache Storage im Speicher, weil jsdom kein `caches` kennt.

class FakeCache {
  private readonly eintraege = new Map<string, string>()

  async put(schluessel: RequestInfo | URL, antwort: Response): Promise<void> {
    this.eintraege.set(String(schluessel), await antwort.text())
  }

  async match(schluessel: RequestInfo | URL): Promise<Response | undefined> {
    const text = this.eintraege.get(String(schluessel))
    return text === undefined ? undefined : new Response(text, { headers: { 'Content-Type': 'application/json' } })
  }

  get groesse(): number {
    return this.eintraege.size
  }
}

export class FakeCaches {
  readonly speicher = new Map<string, FakeCache>()

  async open(name: string): Promise<FakeCache> {
    let cache = this.speicher.get(name)
    if (cache === undefined) {
      cache = new FakeCache()
      this.speicher.set(name, cache)
    }
    return cache
  }

  async delete(name: string): Promise<boolean> {
    return this.speicher.delete(name)
  }

  async has(name: string): Promise<boolean> {
    return this.speicher.has(name)
  }
}

/** Setzt `globalThis.caches` fuer die Dauer der Tests der aufrufenden Datei. */
export function mitFakeCaches(): { readonly caches: FakeCaches } {
  const stand = { caches: new FakeCaches() }
  beforeEach(() => {
    stand.caches = new FakeCaches()
    vi.stubGlobal('caches', stand.caches)
  })
  afterEach(() => {
    vi.unstubAllGlobals()
  })
  return stand
}
