import { useCallback, useState } from 'react'
import { SpeicherSchluessel } from '../components/SpeicherSchluessel'

// Vier Schalter: Lernhilfen (SCHACH-SPEC 7.4), Toene und Sprueche vorlesen (6.9, 12.3), dazu je ein
// Lautstaerke-Regler fuer Toene und Sprueche. localStorage-Schluessel `einstellungen`; fehlender
// Schalter oder Parse-Fehler gilt als `true`, fehlende oder ungueltige Lautstaerke als 1 (voll).

export { SpeicherSchluessel }

export interface Einstellungen {
  erlaubteZuegeAnzeigen: boolean
  vorUngedecktenWarnen: boolean
  toeneUndVibration: boolean
  spruecheVorlesen: boolean
  /** 0,1 bis 1. Aus ist der Schalter `toeneUndVibration`, nicht Lautstaerke 0. */
  toeneLautstaerke: number
  /** 0,1 bis 1. Aus ist der Schalter `spruecheVorlesen`, nicht Lautstaerke 0. */
  spruecheLautstaerke: number
}

export type EinstellungSchluessel = keyof Einstellungen

type SchalterSchluessel = { [K in EinstellungSchluessel]: Einstellungen[K] extends boolean ? K : never }[EinstellungSchluessel]
type LautstaerkeSchluessel = { [K in EinstellungSchluessel]: Einstellungen[K] extends number ? K : never }[EinstellungSchluessel]

const SCHALTER: SchalterSchluessel[] = ['erlaubteZuegeAnzeigen', 'vorUngedecktenWarnen', 'toeneUndVibration', 'spruecheVorlesen']
const LAUTSTAERKEN: LautstaerkeSchluessel[] = ['toeneLautstaerke', 'spruecheLautstaerke']

export const LAUTSTAERKE_MIN = 0.1
export const LAUTSTAERKE_MAX = 1

/** Gueltige Zahl auf 0,1 bis 1 begrenzt, sonst voll. */
export function lautstaerke(wert: unknown): number {
  if (typeof wert !== 'number' || !Number.isFinite(wert)) return LAUTSTAERKE_MAX
  return Math.min(LAUTSTAERKE_MAX, Math.max(LAUTSTAERKE_MIN, wert))
}

function speicher(): Storage | null {
  try {
    return globalThis.localStorage ?? null
  } catch {
    return null
  }
}

export function leseEinstellungen(): Einstellungen {
  let gelesen: Record<string, unknown> = {}
  try {
    const roh = speicher()?.getItem(SpeicherSchluessel.einstellungen)
    const wert: unknown = roh ? JSON.parse(roh) : {}
    if (wert !== null && typeof wert === 'object') gelesen = wert as Record<string, unknown>
  } catch {
    // Parse-Fehler: alle Schalter an, volle Lautstaerke
  }
  const ergebnis = {} as Einstellungen
  for (const schluessel of SCHALTER) ergebnis[schluessel] = gelesen[schluessel] !== false
  for (const schluessel of LAUTSTAERKEN) ergebnis[schluessel] = lautstaerke(gelesen[schluessel])
  return ergebnis
}

export function schreibeEinstellungen(einstellungen: Einstellungen): void {
  try {
    speicher()?.setItem(SpeicherSchluessel.einstellungen, JSON.stringify(einstellungen))
  } catch {
    // Speicher voll oder gesperrt: die Wahl gilt nur fuer diese Sitzung
  }
}

export type SetzeEinstellung = <K extends EinstellungSchluessel>(schluessel: K, wert: Einstellungen[K]) => void

/** Einstellungen als State; jede Aenderung wird sofort gespeichert. */
export function useEinstellungen(): [Einstellungen, SetzeEinstellung] {
  const [einstellungen, setEinstellungen] = useState(leseEinstellungen)
  const setze = useCallback<SetzeEinstellung>((schluessel, wert) => {
    setEinstellungen((alt) => {
      const neu = { ...alt, [schluessel]: wert }
      schreibeEinstellungen(neu)
      return neu
    })
  }, [])
  return [einstellungen, setze]
}
