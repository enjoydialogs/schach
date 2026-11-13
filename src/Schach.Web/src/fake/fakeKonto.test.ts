import { FakeFehler, FakeServer } from './fakeServer'

// S3b: Konto, Gegnerliste, Partienliste und offene Partie im Fake (SCHACH-SPEC 8.3, 9.3).

function server(start = Date.parse('2026-11-07T10:00:00.000Z')) {
  let jetzt = start
  const fake = new FakeServer({ jetzt: () => jetzt, zufall: () => 0.5 - 1e-9 })
  return { fake, spaeter: (ms: number) => (jetzt += ms) }
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

describe('FakeServer: offene Partie', () => {
  it('meldet einem Spieler mit offener Partie 409 OffenePartie mit Id', () => {
    const { fake } = server()
    const offen = fake.starte({ gegnerId: 'oscar', farbe: 'Weiss', bedenkzeit: 'OhneUhr' })
    const e = fehlerVon(() => fake.starte({ gegnerId: 'ilse', farbe: 'Weiss', bedenkzeit: 'OhneUhr' }))
    expect(e).toMatchObject({ status: 409, fehler: 'OffenePartie', offenePartieId: offen.id })
    expect(fake.offene().id).toBe(offen.id)
  })

  it('laesst Gaeste mehrere Partien starten und hat fuer sie keine offene Partie', () => {
    const { fake } = server()
    fake.gast = true
    fake.starte({ gegnerId: 'oscar', farbe: 'Weiss', bedenkzeit: 'OhneUhr' })
    expect(() => fake.starte({ gegnerId: 'ilse', farbe: 'Weiss', bedenkzeit: 'OhneUhr' })).not.toThrow()
    expect(fehlerVon(() => fake.offene()).status).toBe(401)
  })

  it('meldet ohne offene Partie 404', () => {
    const { fake } = server()
    expect(fehlerVon(() => fake.offene()).status).toBe(404)
  })
})

describe('FakeServer: Gegner und Partienliste', () => {
  it('liefert 8 Gegner mit Bilanz fuer Spieler und ohne fuer Gaeste', () => {
    const { fake } = server()
    const p = fake.starte({ gegnerId: 'oscar', farbe: 'Weiss', bedenkzeit: 'OhneUhr' })
    fake.gibAuf(p.id, { halbzug: p.halbzug })

    const liste = fake.gegnerListe()
    expect(liste.map((g) => g.elo)).toEqual([600, 700, 800, 900, 1000, 1100, 1200, 1300])
    expect(liste.find((g) => g.key === 'oscar')?.bilanz).toEqual({ siege: 0, remis: 0, niederlagen: 1, besiegtAm: null })
    expect(liste.find((g) => g.key === 'ilse')?.bilanz).toEqual({ siege: 0, remis: 0, niederlagen: 0, besiegtAm: null })

    fake.gast = true
    expect(fake.gegnerListe().every((g) => g.bilanz === null)).toBe(true)
  })

  it('listet beendete Partien neueste zuerst mit Keyset-Blaettern', () => {
    const { fake, spaeter } = server()
    const ids: string[] = []
    for (let i = 0; i < 3; i++) {
      const p = fake.starte({ gegnerId: 'ilse', farbe: 'Weiss', bedenkzeit: 'Fischer5Plus3' })
      fake.gibAuf(p.id, { halbzug: p.halbzug })
      ids.push(p.id)
      spaeter(60_000)
    }
    const erste = fake.partienListe(2, null)
    expect(erste.partien.map((p) => p.id)).toEqual([ids[2], ids[1]])
    expect(erste.weitere).toBe(true)
    expect(erste.partien[0]).toMatchObject({
      gegnerKey: 'ilse',
      bedenkzeit: 'Fischer5Plus3',
      ergebnis: 'SchwarzGewinnt',
      grund: 'Aufgabe',
      zaehltFuerElo: true,
    })
    const zweite = fake.partienListe(2, ids[1]!)
    expect(zweite.partien.map((p) => p.id)).toEqual([ids[0]])
    expect(zweite.weitere).toBe(false)
  })
})

describe('FakeServer: Anmeldung', () => {
  it('meldet per Link-Token an, nur einmal', () => {
    const { fake } = server()
    fake.gast = true
    const { anmeldungId } = fake.meldeAn('neu@example.invalid')
    expect(anmeldungId).toBe(fake.letzteAnmeldung!.anmeldungId)

    const antwort = fake.bestaetige({ token: fake.letzteAnmeldung!.token })
    expect(antwort.spieler.email).toBe('neu@example.invalid')
    expect(antwort.neu).toBe(true)
    expect(fake.sitzung().angemeldet).toBe(true)

    expect(fehlerVon(() => fake.bestaetige({ token: fake.letzteAnmeldung!.token }))).toMatchObject({
      status: 422,
      fehler: 'AnmeldungUngueltig',
    })
  })

  it('meldet per Code an und loescht nach 5 Fehlversuchen', () => {
    const { fake } = server()
    fake.gast = true
    const { anmeldungId } = fake.meldeAn('a@example.invalid')
    const code = fake.letzteAnmeldung!.code
    expect(code).toMatch(/^\d{6}$/)
    for (let i = 0; i < 5; i++) {
      expect(fehlerVon(() => fake.bestaetige({ anmeldungId, code: '000000' === code ? '111111' : '000000' })).status).toBe(422)
    }
    expect(fehlerVon(() => fake.bestaetige({ anmeldungId, code })).status).toBe(422)

    const zweite = fake.meldeAn('a@example.invalid')
    expect(fake.bestaetige({ anmeldungId: zweite.anmeldungId, code: fake.letzteAnmeldung!.code }).spieler.email).toBe(
      'a@example.invalid',
    )
  })

  it('lehnt nach 15 Minuten ab und prueft die Adresse', () => {
    const { fake, spaeter } = server()
    fake.gast = true
    fake.meldeAn('a@example.invalid')
    spaeter(15 * 60_000 + 1)
    expect(fehlerVon(() => fake.bestaetige({ token: fake.letzteAnmeldung!.token })).status).toBe(422)
    expect(fehlerVon(() => fake.meldeAn('keine-adresse'))).toMatchObject({ status: 400, fehler: 'EmailUngueltig' })
  })

  it('meldet ab, exportiert und loescht das Konto', () => {
    const { fake } = server()
    const p = fake.starte({ gegnerId: 'finn', farbe: 'Weiss', bedenkzeit: 'OhneUhr' })
    fake.gibAuf(p.id, { halbzug: p.halbzug })

    const daten = fake.export()
    expect(daten.konto.email).toBe('spieler@example.invalid')
    expect(daten.partien).toHaveLength(1)
    expect(daten.partien[0]?.pgn).toContain('[Result "0-1"]')
    expect(daten.bilanz.find((b) => b.gegnerKey === 'finn')?.niederlagen).toBe(1)

    fake.meldeAb()
    expect(fake.sitzung().angemeldet).toBe(false)
    expect(fehlerVon(() => fake.export()).status).toBe(401)

    fake.meldeAn('spieler@example.invalid')
    expect(fake.bestaetige({ token: fake.letzteAnmeldung!.token }).neu).toBe(false)
    fake.loescheKonto()
    expect(fake.sitzung().angemeldet).toBe(false)
    fake.meldeAn('spieler@example.invalid')
    expect(fake.bestaetige({ token: fake.letzteAnmeldung!.token }).neu).toBe(true)
    expect(fake.partienListe(50, null).partien).toHaveLength(0)
  })
})
