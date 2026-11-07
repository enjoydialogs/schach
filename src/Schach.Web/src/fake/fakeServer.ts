import { Chess, type Color, type Move } from 'chess.js'
import type {
  Bedenkzeit,
  Ergebnis,
  Farbe,
  FehlerCode,
  Figur,
  GegnerDto,
  GegnerZugDto,
  Grund,
  HalbzugAnfrage,
  NeuePartieAnfrage,
  SitzungAntwort,
  SpruchDto,
  SpruchEreignis,
  TippAntwort,
  TippDto,
  ZugAnfrage,
  ZugDto,
  ZustandDto,
} from '../api/types'

// Fake-Server fuer den Brett-Prototyp (SCHACH-SPEC 15.3, S3a). Erfuellt den Zustand-DTO-Vertrag
// (8.3 bis 8.6) mit chess.js statt Gera.Chess. Der Gegner zieht zufaellig, Sprueche und Tipp sind
// Platzhalter. Nur fuer Entwicklung und die Pixel-7-Abnahme; nie im Produktionsbau.

export const FAKE_ID_PRAEFIX = 'fake-'

export const FAKE_GEGNER: GegnerDto[] = [
  gegner('ilse', 'Ilse', 600, 'Anfaenger', 'Gemuetlich', 'Keine Eile. Wir spielen in aller Ruhe, ja?'),
  gegner('finn', 'Finn', 800, 'Anfaenger', 'Aggressiv', 'Ich greif einfach immer an!'),
  gegner('oscar', 'Oscar', 1000, 'Hobbyspieler', 'Lehrerhaft', 'Ich erklär dir hinterher gern, warum ich so gezogen hab.'),
  gegner('nele', 'Nele', 1200, 'Hobbyspieler', 'Verspielt', 'Mal gucken, was passiert, wenn ich das probiere.'),
  gegner('theo', 'Theo', 1400, 'Vereinsspieler', 'Vorsichtig', 'Sicher ist sicher. Ich decke erst mal alles ab.'),
  gegner('sementa', 'Sementa', 1600, 'Vereinsspieler', 'Eitel', 'Elegant, oder? So sind meine Züge immer.'),
  gegner('konrad', 'Konrad', 1900, 'Turnierspieler', 'Lehrerhaft', 'Erst die Grundsätze, dann der Angriff.'),
  gegner('maren', 'Maren', 2200, 'Meister', 'Aggressiv', 'Ich opfere gern eine Figur für den Angriff.'),
]

function gegner(
  key: string,
  name: string,
  elo: number,
  stufe: GegnerDto['stufe'],
  persoenlichkeit: GegnerDto['persoenlichkeit'],
  vorstellung: string,
): GegnerDto {
  return { key, name, elo, stufe, persoenlichkeit, vorstellung, bilanz: null }
}

const FAKE_SPRUECHE: Record<SpruchEreignis, string> = {
  Partiebeginn: 'Na dann, auf eine schöne Partie!',
  Sieg: 'Diesmal hat es für mich gereicht.',
  Niederlage: 'Gut gespielt, da war nichts mehr zu machen.',
  Remis: 'Ein Remis, damit kann ich leben.',
  RemisAbgelehnt: 'Remis? Dafür ist es mir noch zu früh.',
  Zuruecknahme: 'Na gut, noch einmal von vorn.',
  FigurGewonnen: 'Die nehme ich gern mit.',
  FigurVerloren: 'Ach, die war mir lieb.',
  SchachGegeben: 'Schach! Mal sehen, wohin der König geht.',
  SchachErhalten: 'Oh, Schach. Da muss ich aufpassen.',
  StellungBesser: 'Das sieht gut aus für mich.',
  StellungSchlechter: 'Hm, das wird knifflig.',
  Tausch: 'Tauschen wir eben.',
  Zug: 'Mal sehen, was du dazu sagst.',
}

const UHR: Record<Bedenkzeit, { startMs: number; plusMs: number; jeZug: boolean } | null> = {
  OhneUhr: null,
  Fischer15Plus10: { startMs: 900_000, plusMs: 10_000, jeZug: false },
  Fischer10Plus5: { startMs: 600_000, plusMs: 5_000, jeZug: false },
  Fischer5Plus3: { startMs: 300_000, plusMs: 3_000, jeZug: false },
  JeZug90s: { startMs: 90_000, plusMs: 0, jeZug: true },
}

const FIGUR: Record<string, Figur> = { k: 'Koenig', q: 'Dame', r: 'Turm', b: 'Laeufer', n: 'Springer', p: 'Bauer' }
const WERT: Record<string, number> = { p: 1, n: 3, b: 3, r: 5, q: 9, k: 0 }

/** Optionen fuer die naechste Partie, gesetzt von `/prototyp`. Nicht Teil des API-Vertrags. */
export interface FakeVorgaben {
  startFen?: string
  /** Eigene Startzeit in ms, z. B. 25 000 fuer den Test „unter 20 s rot“. */
  spielerStartMs?: number
}

interface FakePartie {
  id: string
  gegner: GegnerDto
  spielerFarbe: Farbe
  bedenkzeit: Bedenkzeit
  startFen: string
  gast: boolean
  zuege: ZugDto[]
  sprueche: SpruchDto[]
  status: 'Offen' | 'Beendet'
  ergebnis: Ergebnis | null
  grund: Grund | null
  zaehltFuerElo: boolean
  remisAbZug: number
  spielerRestMs: number
  gegnerRestMs: number
  amZugSeit: number | null
  letzterGegnerZug: GegnerZugDto | null
  tipp: { halbzug: number; tipp: TippDto } | null
  eloVorher: number | null
  eloNachher: number | null
}

interface FakeDaten {
  gast: boolean
  elo: number
  partien: Record<string, FakePartie>
}

export class FakeFehler extends Error {
  readonly status: number
  readonly fehler: FehlerCode | null
  readonly zustand: ZustandDto | null

  constructor(status: number, fehler: FehlerCode | null, zustand: ZustandDto | null = null) {
    super(fehler ?? `HTTP ${status}`)
    this.status = status
    this.fehler = fehler
    this.zustand = zustand
  }
}

export interface FakeServerOptionen {
  jetzt?: () => number
  zufall?: () => number
  /** Speichert die Partien ueber ein Neuladen hinweg (sessionStorage im Browser). */
  speicher?: Storage | null
}

const SPEICHER_SCHLUESSEL = 'schach-fake'
const iso = (ms: number) => new Date(ms).toISOString()
const farbeVon = (f: Farbe): Color => (f === 'Weiss' ? 'w' : 'b')
const uciVon = (zug: Pick<Move, 'from' | 'to' | 'promotion'>) => zug.from + zug.to + (zug.promotion ?? '')

export class FakeServer {
  private readonly jetzt: () => number
  private readonly zufall: () => number
  private readonly speicher: Storage | null
  private daten: FakeDaten
  vorgaben: FakeVorgaben = {}

  constructor(optionen: FakeServerOptionen = {}) {
    this.jetzt = optionen.jetzt ?? Date.now
    this.zufall = optionen.zufall ?? Math.random
    this.speicher = optionen.speicher ?? null
    this.daten = this.lade()
  }

  get gast(): boolean {
    return this.daten.gast
  }

  set gast(wert: boolean) {
    this.daten.gast = wert
    this.sichere()
  }

  kennt(id: string): boolean {
    return id in this.daten.partien
  }

  sitzung(): SitzungAntwort {
    return {
      angemeldet: !this.daten.gast,
      spieler: this.daten.gast ? null : { id: 'fake-spieler', email: 'spieler@example.invalid', elo: this.daten.elo },
      csrfToken: 'fake-csrf',
    }
  }

  starte(anfrage: NeuePartieAnfrage): ZustandDto {
    const g = FAKE_GEGNER.find((x) => x.key === anfrage.gegnerId) ?? FAKE_GEGNER[0]!
    const spielerFarbe: Farbe =
      anfrage.farbe === 'Zufall' ? (this.zufall() < 0.5 ? 'Weiss' : 'Schwarz') : anfrage.farbe
    const uhr = UHR[anfrage.bedenkzeit]
    const t = this.jetzt()
    const p: FakePartie = {
      id: FAKE_ID_PRAEFIX + Math.floor(this.zufall() * 2 ** 48).toString(16).padStart(12, '0'),
      gegner: g,
      spielerFarbe,
      bedenkzeit: anfrage.bedenkzeit,
      startFen: this.vorgaben.startFen ?? new Chess().fen(),
      gast: this.daten.gast,
      zuege: [],
      sprueche: [{ nachHalbzug: 0, ereignis: 'Partiebeginn', text: FAKE_SPRUECHE.Partiebeginn }],
      status: 'Offen',
      ergebnis: null,
      grund: null,
      zaehltFuerElo: !this.daten.gast,
      remisAbZug: 1,
      spielerRestMs: this.vorgaben.spielerStartMs ?? uhr?.startMs ?? 0,
      gegnerRestMs: uhr?.startMs ?? 0,
      amZugSeit: uhr ? t : null,
      letzterGegnerZug: null,
      tipp: null,
      eloVorher: null,
      eloNachher: null,
    }
    this.daten.partien[p.id] = p
    if (this.brett(p).turn() !== farbeVon(spielerFarbe)) this.gegnerZieht(p, t)
    this.sichere()
    return this.zustand(p)
  }

  lies(id: string): ZustandDto {
    const p = this.hole(id)
    this.pruefeUhr(p)
    this.sichere()
    return this.zustand(p)
  }

  ziehe(id: string, anfrage: ZugAnfrage): ZustandDto {
    const p = this.hole(id)
    if (this.pruefeUhr(p)) this.fehler(409, 'UhrAbgelaufen', p)
    if (p.status === 'Beendet') this.fehler(409, 'PartieBeendet', p)
    // Gleicher Zug an bereits gespielter Position: 200 mit aktuellem Zustand (8.8).
    const schon = p.zuege[anfrage.halbzug]
    if (anfrage.halbzug < p.zuege.length && schon?.uci === anfrage.zug && schon.seite === p.spielerFarbe) {
      return this.zustand(p)
    }
    if (anfrage.halbzug !== p.zuege.length) this.fehler(409, 'HalbzugVeraltet', p)
    const brett = this.brett(p)
    if (brett.turn() !== farbeVon(p.spielerFarbe)) this.fehler(409, 'NichtAmZug', p)
    const zug = brett.moves({ verbose: true }).find((m) => uciVon(m) === anfrage.zug)
    if (!zug) this.fehler(400, 'ZugUnzulaessig', p)

    const t = this.jetzt()
    const uhr = UHR[p.bedenkzeit]
    if (uhr && p.amZugSeit !== null) {
      p.spielerRestMs -= Math.max(0, t - p.amZugSeit)
      if (p.spielerRestMs <= 0) {
        p.spielerRestMs = 0
        this.beendeNachZeit(p)
        this.sichere()
        this.fehler(409, 'UhrAbgelaufen', p)
      }
      p.spielerRestMs = uhr.jeZug ? uhr.startMs : p.spielerRestMs + uhr.plusMs
    }
    brett.move(zug)
    p.zuege.push(this.zugDto(p, p.spielerFarbe, zug, t))
    p.letzterGegnerZug = null
    if (!this.pruefeEnde(p, brett)) this.gegnerZieht(p, t)
    this.sichere()
    return this.zustand(p)
  }

  nimmZurueck(id: string, anfrage: HalbzugAnfrage): ZustandDto {
    const p = this.offen(id, anfrage)
    if (p.bedenkzeit !== 'OhneUhr' || !p.zuege.some((z) => z.seite === p.spielerFarbe)) {
      this.fehler(409, 'ZuruecknehmenNichtErlaubt', p)
    }
    // Letzten Spielerzug und die Gegnerantwort entfernen (3.3).
    while (p.zuege.length > 0) {
      const weg = p.zuege.pop()!
      if (weg.seite === p.spielerFarbe) break
    }
    p.sprueche = p.sprueche.filter((s) => s.nachHalbzug <= p.zuege.length)
    p.sprueche.push({ nachHalbzug: p.zuege.length, ereignis: 'Zuruecknahme', text: FAKE_SPRUECHE.Zuruecknahme })
    p.letzterGegnerZug = null
    p.tipp = null
    p.zaehltFuerElo = false
    this.sichere()
    return this.zustand(p)
  }

  gibAuf(id: string, anfrage: HalbzugAnfrage): ZustandDto {
    const p = this.offen(id, anfrage)
    this.beende(p, p.spielerFarbe === 'Weiss' ? 'SchwarzGewinnt' : 'WeissGewinnt', 'Aufgabe')
    this.sichere()
    return this.zustand(p)
  }

  bieteRemis(id: string, anfrage: HalbzugAnfrage): ZustandDto {
    const p = this.offen(id, anfrage)
    const brett = this.brett(p)
    const zugnummer = brett.moveNumber()
    if (zugnummer < p.remisAbZug) this.fehler(409, 'RemisAngebotGesperrt', p)
    // Vor Zug 20 immer abgelehnt; danach nimmt der Fake an, wenn er nicht mehr als 1 Bauer vorn liegt.
    if (zugnummer < 20 || this.materialAusSichtDesGegners(p, brett) > 1) {
      p.remisAbZug = zugnummer + 5
      p.sprueche.push({ nachHalbzug: p.zuege.length, ereignis: 'RemisAbgelehnt', text: FAKE_SPRUECHE.RemisAbgelehnt })
    } else {
      this.beende(p, 'Remis', 'Vereinbarung')
    }
    this.sichere()
    return this.zustand(p)
  }

  tipp(id: string, anfrage: HalbzugAnfrage): TippAntwort {
    const p = this.offen(id, anfrage)
    if (p.bedenkzeit !== 'OhneUhr') this.fehler(409, 'TippNichtErlaubt', p)
    const brett = this.brett(p)
    if (brett.turn() !== farbeVon(p.spielerFarbe)) this.fehler(409, 'NichtAmZug', p)
    if (p.tipp?.halbzug !== p.zuege.length) {
      // Platzhalter fuer Stockfish: wertvollster Schlag, sonst ein zufaelliger Zug.
      const zuege = brett.moves({ verbose: true })
      const schlaege = zuege.filter((m) => m.captured).sort((a, b) => WERT[b.captured!]! - WERT[a.captured!]!)
      const zug = schlaege[0] ?? zuege[Math.floor(this.zufall() * zuege.length)]!
      p.tipp = { halbzug: p.zuege.length, tipp: { uci: uciVon(zug), von: zug.from, nach: zug.to, figur: FIGUR[zug.piece]! } }
    }
    p.zaehltFuerElo = false
    this.sichere()
    return { tipp: p.tipp.tipp, zustand: this.zustand(p) }
  }

  // --- intern ---

  private offen(id: string, anfrage: HalbzugAnfrage): FakePartie {
    const p = this.hole(id)
    if (this.pruefeUhr(p)) this.fehler(409, 'UhrAbgelaufen', p)
    if (p.status === 'Beendet') this.fehler(409, 'PartieBeendet', p)
    if (anfrage.halbzug !== p.zuege.length) this.fehler(409, 'HalbzugVeraltet', p)
    return p
  }

  private hole(id: string): FakePartie {
    const p = this.daten.partien[id]
    if (!p) throw new FakeFehler(404, null)
    return p
  }

  private fehler(status: number, code: FehlerCode, p: FakePartie): never {
    throw new FakeFehler(status, code, this.zustand(p))
  }

  private brett(p: FakePartie): Chess {
    const brett = new Chess(p.startFen)
    for (const zug of p.zuege) {
      brett.move({ from: zug.uci.slice(0, 2), to: zug.uci.slice(2, 4), promotion: zug.uci[4] })
    }
    return brett
  }

  private zugDto(p: FakePartie, seite: Farbe, zug: Move, t: number): ZugDto {
    return { halbzug: p.zuege.length + 1, seite, uci: uciVon(zug), san: zug.san, gespieltAm: iso(t) }
  }

  /** Zufaelliger Gegnerzug mit Denkzeit D in [1; 3] s, mit Uhr hoechstens 10 % des Rests (8.6). */
  private gegnerZieht(p: FakePartie, empfangenAm: number): void {
    const brett = this.brett(p)
    const zuege = brett.moves({ verbose: true })
    const zug = zuege[Math.floor(this.zufall() * zuege.length)]
    if (!zug) return
    const uhr = UHR[p.bedenkzeit]
    let denkzeit = 1_000 + Math.floor(this.zufall() * 2_000)
    if (uhr) {
      denkzeit = Math.min(denkzeit, Math.floor(p.gegnerRestMs * 0.1))
      p.gegnerRestMs = uhr.jeZug ? uhr.startMs : p.gegnerRestMs - denkzeit + uhr.plusMs
    }
    const gespieltAm = empfangenAm + denkzeit
    brett.move(zug)
    const dto = this.zugDto(p, p.spielerFarbe === 'Weiss' ? 'Schwarz' : 'Weiss', zug, gespieltAm)
    p.zuege.push(dto)

    let spruch: SpruchDto | null = null
    const ereignis: SpruchEreignis | null = zug.captured
      ? 'FigurGewonnen'
      : zug.san.includes('+')
        ? 'SchachGegeben'
        : dto.halbzug % 6 === 0
          ? 'Zug'
          : null
    if (ereignis) {
      spruch = { nachHalbzug: dto.halbzug, ereignis, text: FAKE_SPRUECHE[ereignis] }
      p.sprueche.push(spruch)
    }
    p.letzterGegnerZug = { ...dto, sichtbarAb: iso(gespieltAm), spruch }
    if (uhr) p.amZugSeit = gespieltAm
    this.pruefeEnde(p, brett)
  }

  /** Ende nach jedem Halbzug in der Reihenfolge Matt, Patt, Material, Wiederholung, 50 Zuege (3.4). */
  private pruefeEnde(p: FakePartie, brett: Chess): boolean {
    if (brett.isCheckmate()) {
      this.beende(p, brett.turn() === 'w' ? 'SchwarzGewinnt' : 'WeissGewinnt', 'Matt')
    } else if (brett.isStalemate()) {
      this.beende(p, 'Remis', 'Patt')
    } else if (brett.isInsufficientMaterial()) {
      this.beende(p, 'Remis', 'Material')
    } else if (brett.isThreefoldRepetition()) {
      this.beende(p, 'Remis', 'Wiederholung')
    } else if (brett.isDrawByFiftyMoves()) {
      this.beende(p, 'Remis', 'FuenfzigZuege')
    }
    return p.status === 'Beendet'
  }

  /** Lazy-Zeitablauf (8.7). Nur der Spieler kann ueberziehen; der Fake-Gegner zieht sofort. */
  private pruefeUhr(p: FakePartie): boolean {
    if (p.status !== 'Offen' || !UHR[p.bedenkzeit] || p.amZugSeit === null) return false
    if (this.brett(p).turn() !== farbeVon(p.spielerFarbe)) return false
    if (this.jetzt() - p.amZugSeit < p.spielerRestMs) return false
    p.spielerRestMs = 0
    this.beendeNachZeit(p)
    return true
  }

  private beendeNachZeit(p: FakePartie): void {
    const gegnerFarbe = p.spielerFarbe === 'Weiss' ? 'b' : 'w'
    // Remis, wenn der Gegner nur K oder K plus genau eine Leichtfigur hat (3.4.6).
    const steine = this.brett(p)
      .board()
      .flat()
      .filter((s) => s !== null && s.color === gegnerFarbe && s.type !== 'k')
    const ohneMattmaterial = steine.length === 0 || (steine.length === 1 && ['n', 'b'].includes(steine[0]!.type))
    if (ohneMattmaterial) this.beende(p, 'Remis', 'ZeitOhneMattmaterial')
    else this.beende(p, p.spielerFarbe === 'Weiss' ? 'SchwarzGewinnt' : 'WeissGewinnt', 'Zeit')
  }

  private beende(p: FakePartie, ergebnis: Ergebnis, grund: Grund): void {
    p.status = 'Beendet'
    p.ergebnis = ergebnis
    p.grund = grund
    p.amZugSeit = null
    const spielerGewinnt =
      (ergebnis === 'WeissGewinnt' && p.spielerFarbe === 'Weiss') ||
      (ergebnis === 'SchwarzGewinnt' && p.spielerFarbe === 'Schwarz')
    const ereignis: SpruchEreignis = ergebnis === 'Remis' ? 'Remis' : spielerGewinnt ? 'Niederlage' : 'Sieg'
    p.sprueche.push({ nachHalbzug: p.zuege.length, ereignis, text: FAKE_SPRUECHE[ereignis] })
    if (p.gast || !p.zaehltFuerElo) return
    // Elo nach 3.7: K = 32, Untergrenze 100.
    const s = ergebnis === 'Remis' ? 0.5 : spielerGewinnt ? 1 : 0
    const e = 1 / (1 + 10 ** ((p.gegner.elo - this.daten.elo) / 400))
    const delta = Math.sign(32 * (s - e)) * Math.round(Math.abs(32 * (s - e)))
    p.eloVorher = this.daten.elo
    p.eloNachher = Math.max(100, this.daten.elo + delta)
    this.daten.elo = p.eloNachher
  }

  private materialAusSichtDesGegners(p: FakePartie, brett: Chess): number {
    const gegnerFarbe = p.spielerFarbe === 'Weiss' ? 'b' : 'w'
    let bilanz = 0
    for (const stein of brett.board().flat()) {
      if (stein) bilanz += (stein.color === gegnerFarbe ? 1 : -1) * WERT[stein.type]!
    }
    return bilanz
  }

  private zustand(p: FakePartie): ZustandDto {
    const brett = this.brett(p)
    const amZug = p.status === 'Offen' && brett.turn() === farbeVon(p.spielerFarbe)
    const letzter = p.zuege.at(-1)
    const gegnerZug =
      p.letzterGegnerZug !== null && letzter !== undefined && p.letzterGegnerZug.halbzug === letzter.halbzug
        ? p.letzterGegnerZug
        : null
    const uhr = UHR[p.bedenkzeit]
      ? {
          spielerRestMs: p.spielerRestMs,
          gegnerRestMs: p.gegnerRestMs,
          laeuftFuer: p.status === 'Offen' ? (amZug ? ('Spieler' as const) : ('Gegner' as const)) : null,
          amZugSeit: p.status === 'Offen' && p.amZugSeit !== null ? iso(p.amZugSeit) : null,
          serverZeit: iso(this.jetzt()),
        }
      : null
    return {
      id: p.id,
      status: p.status,
      spielerFarbe: p.spielerFarbe,
      gegner: p.gegner,
      bedenkzeit: p.bedenkzeit,
      halbzug: p.zuege.length,
      fen: brett.fen(),
      zugfolge: [...p.zuege],
      erlaubteZuege: amZug ? brett.moves({ verbose: true }).map(uciVon) : [],
      amZug,
      gegnerZugAusstehend: false,
      gegnerZug,
      uhr,
      zaehltFuerElo: p.zaehltFuerElo,
      remisAngebotMoeglichAbZug: p.remisAbZug,
      sprueche: [...p.sprueche],
      ergebnis: p.ergebnis,
      grund: p.grund,
      eloVorher: p.eloVorher,
      eloNachher: p.eloNachher,
      pgn: p.status === 'Beendet' ? this.pgn(p, brett) : null,
    }
  }

  private pgn(p: FakePartie, brett: Chess): string {
    const ergebnis = { WeissGewinnt: '1-0', SchwarzGewinnt: '0-1', Remis: '1/2-1/2' }[p.ergebnis!]
    brett.setHeader('Result', ergebnis)
    brett.setHeader('Termination', p.grund!)
    return brett.pgn()
  }

  private lade(): FakeDaten {
    try {
      const roh = this.speicher?.getItem(SPEICHER_SCHLUESSEL)
      if (roh) return JSON.parse(roh) as FakeDaten
    } catch {
      // kaputter Stand: neu anfangen
    }
    return { gast: false, elo: 800, partien: {} }
  }

  private sichere(): void {
    try {
      this.speicher?.setItem(SPEICHER_SCHLUESSEL, JSON.stringify(this.daten))
    } catch {
      // Speicher voll: der Fake lebt dann nur im Speicher der Seite
    }
  }
}
