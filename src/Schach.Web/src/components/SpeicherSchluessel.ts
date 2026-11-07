/**
 * localStorage- und sessionStorage-Schluessel ohne Praefix (SCHACH-SPEC 12.3). Ein neuer Schluessel
 * braucht einen Eintrag in der Datenschutzerklaerung (12.1, recht/Datenschutz.tsx).
 */
export const SpeicherSchluessel = {
  /** localStorage: JSON der drei Schalter (7.4). */
  einstellungen: 'einstellungen',
  /** localStorage: Id der laufenden Gast-Partie. */
  gastPartieId: 'gastPartieId',
  /** localStorage: ISO-Zeitpunkt des letzten „Später“ der Install-Karte (S3d, #37). */
  installHinweis: 'installHinweis',
  /** sessionStorage: Code-Weg der Anmeldung (9.5). */
  anmeldungId: 'anmeldungId',
} as const

function lokal(): Storage | null {
  try {
    return globalThis.localStorage ?? null
  } catch {
    return null
  }
}

function sitzung(): Storage | null {
  try {
    return globalThis.sessionStorage ?? null
  } catch {
    return null
  }
}

function lies(speicher: Storage | null, schluessel: string): string | null {
  try {
    return speicher?.getItem(schluessel) ?? null
  } catch {
    return null
  }
}

function schreibe(speicher: Storage | null, schluessel: string, wert: string | null): void {
  try {
    if (wert === null) speicher?.removeItem(schluessel)
    else speicher?.setItem(schluessel, wert)
  } catch {
    // Speicher voll oder gesperrt: der Wert gilt dann nur bis zum Neuladen nicht
  }
}

/** Id der laufenden Gast-Partie (localStorage). */
export const gastPartieId = {
  lies: () => lies(lokal(), SpeicherSchluessel.gastPartieId),
  setze: (id: string | null) => schreibe(lokal(), SpeicherSchluessel.gastPartieId, id),
}

/** `anmeldungId` fuer den Code-Weg (sessionStorage, 9.5). */
export const anmeldungId = {
  lies: () => lies(sitzung(), SpeicherSchluessel.anmeldungId),
  setze: (id: string | null) => schreibe(sitzung(), SpeicherSchluessel.anmeldungId, id),
}
