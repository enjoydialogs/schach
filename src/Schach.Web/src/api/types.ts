// Wire-Typen der API, gespiegelt aus src/Schach.Api/Contracts (eingefroren ab Phase 0).
// Append-Register (SCHACH-SPEC 15.1 Regel 5): nur anhaengen. Zeitpunkte sind ISO-8601-Strings mit Z.

export type Bedenkzeit = 'OhneUhr' | 'Fischer15Plus10' | 'Fischer10Plus5' | 'Fischer5Plus3' | 'JeZug90s'
export type FarbeWahl = 'Weiss' | 'Schwarz' | 'Zufall'
export type Farbe = 'Weiss' | 'Schwarz'
export type UhrSeite = 'Spieler' | 'Gegner'
export type PartieStatus = 'Offen' | 'Beendet'
export type Ergebnis = 'WeissGewinnt' | 'SchwarzGewinnt' | 'Remis'
export type Grund =
  | 'Matt'
  | 'Aufgabe'
  | 'Zeit'
  | 'Patt'
  | 'Wiederholung'
  | 'FuenfzigZuege'
  | 'Material'
  | 'ZeitOhneMattmaterial'
  | 'Vereinbarung'
export type SpruchEreignis =
  | 'Partiebeginn'
  | 'Sieg'
  | 'Niederlage'
  | 'Remis'
  | 'RemisAbgelehnt'
  | 'Zuruecknahme'
  | 'FigurGewonnen'
  | 'FigurVerloren'
  | 'SchachGegeben'
  | 'SchachErhalten'
  | 'StellungBesser'
  | 'StellungSchlechter'
  | 'Tausch'
  | 'Zug'
export type Stufe = 'Anfaenger' | 'Hobbyspieler' | 'Vereinsspieler' | 'Turnierspieler' | 'Meister'
export type Persoenlichkeit = 'Aggressiv' | 'Vorsichtig' | 'Lehrerhaft' | 'Verspielt' | 'Eitel' | 'Gemuetlich'
export type Figur = 'Koenig' | 'Dame' | 'Turm' | 'Laeufer' | 'Springer' | 'Bauer'
export type FehlerCode =
  | 'NichtAmZug'
  | 'HalbzugVeraltet'
  | 'PartieBeendet'
  | 'UhrAbgelaufen'
  | 'OffenePartie'
  | 'ZuruecknehmenNichtErlaubt'
  | 'RemisAngebotGesperrt'
  | 'TippNichtErlaubt'
  | 'TippNichtVerfuegbar'
  | 'ZugUnzulaessig'
  | 'TageslimitErreicht'
  | 'GeradeVielLos'
  | 'EmailUngueltig'
  | 'MailNichtGesendet'
  | 'MailKontingentErschoepft'
  | 'AnmeldungUngueltig'

export interface BilanzDto {
  siege: number
  remis: number
  niederlagen: number
  besiegtAm: string | null
}

export interface GegnerDto {
  key: string
  name: string
  elo: number
  stufe: Stufe
  persoenlichkeit: Persoenlichkeit
  vorstellung: string
  bilanz: BilanzDto | null
}

export interface SpruchDto {
  nachHalbzug: number
  ereignis: SpruchEreignis
  text: string
  /** Signiertes Token fuer `GET /sprachausgabe/{token}` (6.9); `null` = stumm. */
  stimme: string | null
}

export interface ZugDto {
  halbzug: number
  seite: Farbe
  uci: string
  san: string
  gespieltAm: string
}

export interface GegnerZugDto extends ZugDto {
  sichtbarAb: string
  spruch: SpruchDto | null
}

export interface UhrDto {
  spielerRestMs: number
  gegnerRestMs: number
  laeuftFuer: UhrSeite | null
  amZugSeit: string | null
  serverZeit: string
}

export interface ZustandDto {
  id: string
  status: PartieStatus
  spielerFarbe: Farbe
  gegner: GegnerDto
  bedenkzeit: Bedenkzeit
  halbzug: number
  fen: string
  zugfolge: ZugDto[]
  erlaubteZuege: string[]
  amZug: boolean
  gegnerZugAusstehend: boolean
  gegnerZug: GegnerZugDto | null
  uhr: UhrDto | null
  zaehltFuerElo: boolean
  remisAngebotMoeglichAbZug: number
  sprueche: SpruchDto[]
  ergebnis: Ergebnis | null
  grund: Grund | null
  eloVorher: number | null
  eloNachher: number | null
  pgn: string | null
}

export interface NeuePartieAnfrage {
  gegnerId: string
  farbe: FarbeWahl
  bedenkzeit: Bedenkzeit
}

export interface ZugAnfrage {
  halbzug: number
  zug: string
}

export interface HalbzugAnfrage {
  halbzug: number
}

export interface TippDto {
  uci: string
  von: string
  nach: string
  figur: Figur
}

export interface TippAntwort {
  tipp: TippDto
  zustand: ZustandDto
}

export interface PartieZeileDto {
  id: string
  gegnerKey: string
  spielerFarbe: Farbe
  bedenkzeit: Bedenkzeit
  ergebnis: Ergebnis
  grund: Grund
  beendetAm: string
  zaehltFuerElo: boolean
  eloVorher: number | null
  eloNachher: number | null
}

export interface PartienListeDto {
  partien: PartieZeileDto[]
  weitere: boolean
}

export interface FehlerAntwort {
  fehler: FehlerCode
  zustand: ZustandDto | null
  offenePartieId: string | null
}

export interface SpielerDto {
  id: string
  email: string
  elo: number
  /** Anlage des Kontos, ISO-8601 UTC („Spieler seit …“, 10.9). */
  erstelltAm: string
}

export interface SitzungAntwort {
  angemeldet: boolean
  spieler: SpielerDto | null
  csrfToken: string
}

export interface AnmeldungAnfrage {
  email: string
}

export interface AnmeldungAntwort {
  anmeldungId: string
}

export interface BestaetigenAnfrage {
  token?: string
  anmeldungId?: string
  code?: string
}

export interface BestaetigenAntwort {
  spieler: SpielerDto
  neu: boolean
}

export interface AbmeldenAnfrage {
  ueberall?: boolean
}

// S3b: Konto-Export (SCHACH-SPEC 9.3, 9.7), gespiegelt aus KontoContracts.cs.

export interface KontoExportKonto {
  id: string
  email: string
  erstelltAm: string
  zuletztAngemeldetAm: string | null
}

export interface KontoExportBilanz {
  gegnerKey: string
  siege: number
  remis: number
  niederlagen: number
  besiegtAm: string | null
}

export interface KontoExportPartie {
  id: string
  gegnerKey: string
  spielerFarbe: Farbe
  bedenkzeit: Bedenkzeit
  status: PartieStatus
  ergebnis: Ergebnis | null
  grund: Grund | null
  erstelltAm: string
  beendetAm: string | null
  zaehltFuerElo: boolean
  eloVorher: number | null
  eloNachher: number | null
  pgn: string | null
  sprueche: SpruchDto[]
}

export interface KontoExportDto {
  konto: KontoExportKonto
  elo: number
  bilanz: KontoExportBilanz[]
  partien: KontoExportPartie[]
}
