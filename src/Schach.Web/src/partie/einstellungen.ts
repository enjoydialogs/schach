import { useCallback, useState } from 'react'

// Drei Schalter der Lernhilfen (SCHACH-SPEC 7.4, 12.3). localStorage-Schluessel `einstellungen`;
// fehlender Wert oder Parse-Fehler gilt als `true`.

/**
 * localStorage-Schluessel ohne Praefix (SCHACH-SPEC 12.3). S3a braucht nur `einstellungen`;
 * weitere Schluessel (gastPartieId, installHinweis) haengen spaetere Sessions an.
 */
export const SpeicherSchluessel = {
  einstellungen: 'einstellungen',
} as const

export interface Einstellungen {
  erlaubteZuegeAnzeigen: boolean
  vorUngedecktenWarnen: boolean
  toeneUndVibration: boolean
}

export type EinstellungSchluessel = keyof Einstellungen

const SCHLUESSEL: EinstellungSchluessel[] = ['erlaubteZuegeAnzeigen', 'vorUngedecktenWarnen', 'toeneUndVibration']

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
    // Parse-Fehler: alle Schalter an
  }
  const ergebnis = {} as Einstellungen
  for (const schluessel of SCHLUESSEL) ergebnis[schluessel] = gelesen[schluessel] !== false
  return ergebnis
}

export function schreibeEinstellungen(einstellungen: Einstellungen): void {
  try {
    speicher()?.setItem(SpeicherSchluessel.einstellungen, JSON.stringify(einstellungen))
  } catch {
    // Speicher voll oder gesperrt: die Wahl gilt nur fuer diese Sitzung
  }
}

/** Einstellungen als State; jede Aenderung wird sofort gespeichert. */
export function useEinstellungen(): [Einstellungen, (schluessel: EinstellungSchluessel, wert: boolean) => void] {
  const [einstellungen, setEinstellungen] = useState(leseEinstellungen)
  const setze = useCallback((schluessel: EinstellungSchluessel, wert: boolean) => {
    setEinstellungen((alt) => {
      const neu = { ...alt, [schluessel]: wert }
      schreibeEinstellungen(neu)
      return neu
    })
  }, [])
  return [einstellungen, setze]
}
