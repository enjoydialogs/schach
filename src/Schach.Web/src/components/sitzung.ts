import { useEffect, useSyncExternalStore } from 'react'
import { api, setCsrfToken, setzeCsrfLader } from '../api/client'
import type { SitzungAntwort, SpielerDto } from '../api/types'

// Sitzungsstatus der App (SCHACH-SPEC 10.1): `laedt | gast | spieler | unbekannt` (Netzfehler).
// Ein Modul-Speicher statt eines Providers: jede Seite liest ihn per Hook, ohne dass die Routen
// umgebaut werden muessen. Der Client ruft `GET /sitzung` beim Start, nach der Anmeldung und nach
// dem Abmelden (9.3). Das CSRF-Token lebt nur im Speicher (9.1, 9.4).

export type SitzungStatus = 'laedt' | 'gast' | 'spieler' | 'unbekannt'

export interface Sitzung {
  status: SitzungStatus
  spieler: SpielerDto | null
}

const START: Sitzung = { status: 'laedt', spieler: null }

let stand: Sitzung = START
let laufend: Promise<Sitzung> | null = null
let geladen = false
const hoerer = new Set<() => void>()

function setze(neu: Sitzung): void {
  stand = neu
  for (const h of hoerer) h()
}

/** Laedt die Sitzung neu. Waehrend des Ladens bleibt der alte Stand sichtbar (kein Flackern). */
export function ladeSitzung(): Promise<Sitzung> {
  if (laufend !== null) return laufend
  geladen = true
  laufend = api
    .get<SitzungAntwort>('/sitzung')
    .then(
      (s): Sitzung => {
        setCsrfToken(s.csrfToken)
        return s.angemeldet && s.spieler !== null
          ? { status: 'spieler', spieler: s.spieler }
          : { status: 'gast', spieler: null }
      },
      // Netz- oder Serverfehler ist kein Beweis fuer "Gast"; offline erreicht ein Spieler gecachte
      // Partien (RequireSpieler rendert bei `unbekannt` das Kind).
      (): Sitzung => ({ status: 'unbekannt', spieler: stand.spieler }),
    )
    .then((neu) => {
      laufend = null
      setze(neu)
      return neu
    })
  return laufend
}

// S3e: Fehlt dem Client vor einem Nicht-GET das Token oder lehnt der Server es ab, laedt er die
// Sitzung neu; so bleibt der Sitzungsstand (z. B. nach Abmeldung auf einem anderen Geraet) aktuell.
setzeCsrfLader(ladeSitzung)

/** Nur fuer Tests: zurueck auf den Anfang. */
export function setzeSitzungZurueck(): void {
  stand = START
  laufend = null
  geladen = false
  setCsrfToken(null)
  hoerer.forEach((h) => h())
}

function abonniere(h: () => void): () => void {
  hoerer.add(h)
  return () => hoerer.delete(h)
}

/** Aktuelle Sitzung; laedt sie beim ersten Gebrauch. */
export function useSitzung(): Sitzung {
  const sitzung = useSyncExternalStore(abonniere, () => stand)
  useEffect(() => {
    if (!geladen) void ladeSitzung()
  }, [])
  return sitzung
}
