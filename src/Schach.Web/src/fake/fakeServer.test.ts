import { FakeFehler, FakeServer } from './fakeServer'

function server(start = Date.parse('2026-11-07T10:00:00.000Z')) {
  let jetzt = start
  // Fester Zufall: Farbe Weiss, Denkzeit knapp 2 s, erster erlaubter Zug in der Mitte der Liste.
  const fake = new FakeServer({ jetzt: () => jetzt, zufall: () => 0.5 - 1e-9 })
  return { fake, spaeter: (ms: number) => (jetzt += ms), jetzt: () => jetzt }
}

function fehlerVon(arbeit: () => unknown): FakeFehler {
  try {
    arbeit()
  } catch (e) {
    if (e instanceof FakeFehler) return e
    throw e
  }
  throw new Error('kein Fehler')
}

describe('FakeServer', () => {
  it('startet eine Partie mit Partiebeginn-Spruch und erlaubten Zuegen', () => {
    const { fake } = server()
    const z = fake.starte({ gegnerId: 'ilse', farbe: 'Weiss', bedenkzeit: 'OhneUhr' })
    expect(z.id).toMatch(/^fake-/)
    expect(z.status).toBe('Offen')
    expect(z.amZug).toBe(true)
    expect(z.erlaubteZuege).toHaveLength(20)
    expect(z.sprueche[0]).toMatchObject({ nachHalbzug: 0, ereignis: 'Partiebeginn' })
    expect(z.uhr).toBeNull()
    expect(z.zaehltFuerElo).toBe(true)
  })

  it('antwortet auf einen Zug mit Gegnerzug, sichtbar nach der Denkzeit', () => {
    const { fake, jetzt } = server()
    const start = fake.starte({ gegnerId: 'ilse', farbe: 'Weiss', bedenkzeit: 'OhneUhr' })
    const z = fake.ziehe(start.id, { halbzug: 0, zug: 'e2e4' })
    expect(z.halbzug).toBe(2)
    expect(z.zugfolge[0]).toMatchObject({ halbzug: 1, seite: 'Weiss', uci: 'e2e4', san: 'e4' })
    expect(z.gegnerZug?.halbzug).toBe(2)
    const denkzeit = Date.parse(z.gegnerZug!.sichtbarAb) - jetzt()
    expect(denkzeit).toBeGreaterThanOrEqual(1_000)
    expect(denkzeit).toBeLessThanOrEqual(3_000)
    expect(z.amZug).toBe(true)
  })

  it('laesst als Schwarz den Gegner zuerst ziehen', () => {
    const { fake } = server()
    const z = fake.starte({ gegnerId: 'finn', farbe: 'Schwarz', bedenkzeit: 'OhneUhr' })
    expect(z.halbzug).toBe(1)
    expect(z.gegnerZug?.seite).toBe('Weiss')
    expect(z.amZug).toBe(true)
  })

  it('ist idempotent und meldet veraltete Halbzuege und illegale Zuege', () => {
    const { fake } = server()
    const { id } = fake.starte({ gegnerId: 'ilse', farbe: 'Weiss', bedenkzeit: 'OhneUhr' })
    const z = fake.ziehe(id, { halbzug: 0, zug: 'e2e4' })
    expect(fake.ziehe(id, { halbzug: 0, zug: 'e2e4' })).toEqual(z)
    expect(fehlerVon(() => fake.ziehe(id, { halbzug: 0, zug: 'd2d4' }))).toMatchObject({
      status: 409,
      fehler: 'HalbzugVeraltet',
    })
    const illegal = fehlerVon(() => fake.ziehe(id, { halbzug: 2, zug: 'e1e3' }))
    expect(illegal).toMatchObject({ status: 400, fehler: 'ZugUnzulaessig' })
    expect(illegal.zustand?.halbzug).toBe(2)
  })

  it('nimmt Spielerzug und Gegnerantwort zurueck und zaehlt dann nicht mehr', () => {
    const { fake } = server()
    const { id } = fake.starte({ gegnerId: 'ilse', farbe: 'Weiss', bedenkzeit: 'OhneUhr' })
    fake.ziehe(id, { halbzug: 0, zug: 'e2e4' })
    const z = fake.nimmZurueck(id, { halbzug: 2 })
    expect(z.halbzug).toBe(0)
    expect(z.zaehltFuerElo).toBe(false)
    expect(z.sprueche.at(-1)?.ereignis).toBe('Zuruecknahme')
  })

  it('verweigert Zuruecknehmen und Tipp mit Bedenkzeit', () => {
    const { fake } = server()
    const { id } = fake.starte({ gegnerId: 'ilse', farbe: 'Weiss', bedenkzeit: 'Fischer5Plus3' })
    fake.ziehe(id, { halbzug: 0, zug: 'e2e4' })
    expect(fehlerVon(() => fake.nimmZurueck(id, { halbzug: 2 })).fehler).toBe('ZuruecknehmenNichtErlaubt')
    expect(fehlerVon(() => fake.tipp(id, { halbzug: 2 })).fehler).toBe('TippNichtErlaubt')
  })

  it('gibt einen Tipp, haelt ihn je Halbzug fest und nimmt die Partie aus der Elo', () => {
    const { fake } = server()
    const { id } = fake.starte({ gegnerId: 'ilse', farbe: 'Weiss', bedenkzeit: 'OhneUhr' })
    const antwort = fake.tipp(id, { halbzug: 0 })
    expect(antwort.zustand.erlaubteZuege).toContain(antwort.tipp.uci)
    expect(antwort.tipp.von + antwort.tipp.nach).toBe(antwort.tipp.uci.slice(0, 4))
    expect(antwort.zustand.zaehltFuerElo).toBe(false)
    expect(fake.tipp(id, { halbzug: 0 }).tipp).toEqual(antwort.tipp)
  })

  it('beendet nach Aufgabe mit Elo-Buchung', () => {
    const { fake } = server()
    const { id } = fake.starte({ gegnerId: 'ilse', farbe: 'Weiss', bedenkzeit: 'OhneUhr' })
    const z = fake.gibAuf(id, { halbzug: 0 })
    expect(z).toMatchObject({ status: 'Beendet', ergebnis: 'SchwarzGewinnt', grund: 'Aufgabe', amZug: false })
    expect(z.eloVorher).toBe(800)
    // Erwartung gegen 600: 0,76; delta = round(32 * -0,76) = -24.
    expect(z.eloNachher).toBe(776)
    expect(z.pgn).toContain('0-1')
    expect(fehlerVon(() => fake.ziehe(id, { halbzug: 0, zug: 'e2e4' })).fehler).toBe('PartieBeendet')
  })

  it('bucht fuer Gaeste keine Elo', () => {
    const { fake } = server()
    fake.gast = true
    expect(fake.sitzung().angemeldet).toBe(false)
    const { id, zaehltFuerElo } = fake.starte({ gegnerId: 'ilse', farbe: 'Weiss', bedenkzeit: 'OhneUhr' })
    expect(zaehltFuerElo).toBe(false)
    expect(fake.gibAuf(id, { halbzug: 0 }).eloNachher).toBeNull()
  })

  it('lehnt Remis vor Zug 20 ab und sperrt fuer 5 Zuege', () => {
    const { fake } = server()
    const { id } = fake.starte({ gegnerId: 'ilse', farbe: 'Weiss', bedenkzeit: 'OhneUhr' })
    const z = fake.bieteRemis(id, { halbzug: 0 })
    expect(z.status).toBe('Offen')
    expect(z.remisAngebotMoeglichAbZug).toBe(6)
    expect(z.sprueche.at(-1)?.ereignis).toBe('RemisAbgelehnt')
    expect(fehlerVon(() => fake.bieteRemis(id, { halbzug: 0 })).fehler).toBe('RemisAngebotGesperrt')
  })

  it('erkennt Matt nach dem eigenen Zug', () => {
    const { fake } = server()
    fake.vorgaben = { startFen: '6k1/5ppp/8/8/8/8/8/R5K1 w - - 0 1' }
    const { id } = fake.starte({ gegnerId: 'ilse', farbe: 'Weiss', bedenkzeit: 'OhneUhr' })
    const z = fake.ziehe(id, { halbzug: 0, zug: 'a1a8' })
    expect(z).toMatchObject({ status: 'Beendet', ergebnis: 'WeissGewinnt', grund: 'Matt', gegnerZug: null })
  })

  it('fuehrt die Uhr mit Inkrement und beendet nach Zeitablauf', () => {
    const { fake, spaeter } = server()
    const { id } = fake.starte({ gegnerId: 'ilse', farbe: 'Weiss', bedenkzeit: 'Fischer5Plus3' })
    spaeter(10_000)
    const z = fake.ziehe(id, { halbzug: 0, zug: 'e2e4' })
    expect(z.uhr).toMatchObject({ spielerRestMs: 293_000, laeuftFuer: 'Spieler' })
    // Gegner: 300 s - 1,999 s Denkzeit + 3 s.
    expect(z.uhr?.gegnerRestMs).toBe(301_001)
    spaeter(400_000)
    const ende = fake.lies(id)
    expect(ende).toMatchObject({ status: 'Beendet', grund: 'Zeit', ergebnis: 'SchwarzGewinnt' })
    expect(ende.uhr?.laeuftFuer).toBeNull()
  })
})
