import type { UhrDto, UhrSeite } from '../api/types'

// Uhranzeige (SCHACH-SPEC 8.6, 10.3). Der Server fuehrt die Uhr; der Client rechnet nur die
// laufende Seite seit `amZugSeit` herunter. `offsetMs` = Serverzeit minus Clientzeit beim Empfang.

/** Ab hier ist die Uhr rot. */
export const KNAPP_MS = 20_000

/** Versatz zwischen Server- und Clientuhr aus `serverZeit`. */
export function uhrVersatz(uhr: UhrDto, empfangenAmClientMs: number): number {
  return Date.parse(uhr.serverZeit) - empfangenAmClientMs
}

/** Rest einer Seite jetzt. Liegt `amZugSeit` in der Zukunft (Gegnerzug noch unsichtbar), steht sie. */
export function restMs(uhr: UhrDto, seite: UhrSeite, jetztClientMs: number, offsetMs: number): number {
  const basis = seite === 'Spieler' ? uhr.spielerRestMs : uhr.gegnerRestMs
  if (uhr.laeuftFuer !== seite || uhr.amZugSeit === null) return Math.max(0, basis)
  const verstrichen = Math.max(0, jetztClientMs + offsetMs - Date.parse(uhr.amZugSeit))
  return Math.max(0, basis - verstrichen)
}

/** `mm:ss`; angefangene Sekunden zaehlen, 00:00 erst bei Ablauf. */
export function formatiereUhr(ms: number): string {
  const sekunden = Math.ceil(Math.max(0, ms) / 1000)
  const mm = Math.floor(sekunden / 60)
  const ss = sekunden % 60
  return `${String(mm).padStart(2, '0')}:${String(ss).padStart(2, '0')}`
}

export type UhrZustand = 'ruht' | 'aktiv' | 'knapp'

/** Unter 20 s rot, sonst aktive Uhr in Akzentfarbe. */
export function uhrZustand(rest: number, aktiv: boolean): UhrZustand {
  if (rest < KNAPP_MS) return 'knapp'
  return aktiv ? 'aktiv' : 'ruht'
}
