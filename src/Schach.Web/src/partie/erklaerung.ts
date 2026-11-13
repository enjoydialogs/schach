import type { Ergebnis, Farbe, Grund } from '../api/types'
import { de } from '../i18n/de'
import { fuelle } from './text'

// Partieende-Blatt (SCHACH-SPEC 3.6, 3.7, 10.3): Titel, Grund, Erklaersatz, Elo-Zeile.

export type Sicht = 'sieg' | 'niederlage' | 'remis'

/** Sicht des Spielers aus Ergebnis und eigener Farbe. */
export function sichtAuf(ergebnis: Ergebnis, spielerFarbe: Farbe): Sicht {
  if (ergebnis === 'Remis') return 'remis'
  const weissGewinnt = ergebnis === 'WeissGewinnt'
  return weissGewinnt === (spielerFarbe === 'Weiss') ? 'sieg' : 'niederlage'
}

export function titel(sicht: Sicht): string {
  if (sicht === 'sieg') return de.partieende.titelSieg
  if (sicht === 'niederlage') return de.partieende.titelNiederlage
  return de.partieende.titelRemis
}

export function grundLabel(grund: Grund): string {
  return de.partieende.grund[grund]
}

/** Fester Erklaersatz; eine Kombination ohne Eintrag liefert `null` (dann nur das Grund-Label). */
export function erklaerung(grund: Grund, sicht: Sicht, gegnerName: string): string | null {
  const saetze: Partial<Record<Sicht, string>> = de.partieende.erklaerung[grund]
  const satz = saetze[sicht]
  return satz === undefined ? null : fuelle(satz, { name: gegnerName })
}

/** Elo-Aenderung mit Vorzeichen: „+12“, „−8“ (U+2212), „±0“. */
export function eloAenderung(vorher: number, nachher: number): string {
  const delta = nachher - vorher
  if (delta === 0) return '±0'
  return delta > 0 ? `+${delta}` : `−${Math.abs(delta)}`
}

/** Elo-Zeile: „Elo 812 (+12)“, „Elo: zählt nicht“ oder `null` fuer Gaeste (dann der Konto-Hinweis). */
export function eloZeile(z: {
  zaehltFuerElo: boolean
  eloVorher: number | null
  eloNachher: number | null
  gast: boolean
}): string | null {
  if (z.gast) return null
  if (!z.zaehltFuerElo || z.eloVorher === null || z.eloNachher === null) return de.partieende.eloZaehltNicht
  return fuelle(de.partieende.eloZeile, { elo: z.eloNachher, aenderung: eloAenderung(z.eloVorher, z.eloNachher) })
}

/**
 * Elo-Gewinn fuer das Sieg-Abzeichen (10.3): neue Elo und „+12“, nur wenn die Partie gezaehlt hat
 * und die Elo gestiegen ist; sonst `null` (dann die normale Elo-Zeile).
 */
export function eloGewinn(z: {
  zaehltFuerElo: boolean
  eloVorher: number | null
  eloNachher: number | null
  gast: boolean
}): { elo: number; plus: string } | null {
  if (z.gast || !z.zaehltFuerElo || z.eloVorher === null || z.eloNachher === null) return null
  if (z.eloNachher <= z.eloVorher) return null
  return { elo: z.eloNachher, plus: eloAenderung(z.eloVorher, z.eloNachher) }
}
