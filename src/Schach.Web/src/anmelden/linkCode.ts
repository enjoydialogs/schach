// Reine Logik der Anmeldung (SCHACH-SPEC 9.5).

export const ERNEUT_SENDEN_NACH_S = 60

/**
 * Token aus dem Fragment `#t=<token>`. `null`: kein Fragment (normale Seite); `''`: Fragment ohne
 * brauchbares Token (Link unvollstaendig, z. B. von einem Link-Umschreiber gekuerzt).
 */
export function linkToken(hash: string): string | null {
  if (hash === '' || hash === '#') return null
  return new URLSearchParams(hash.slice(1)).get('t')?.trim() ?? ''
}

/** Nur die Ziffern; die Mail zeigt den Code als „123 456“. */
export function nurZiffern(eingabe: string): string {
  return eingabe.replace(/\D/g, '')
}

/** Sekunden bis „Erneut senden“ wieder geht. */
export function restSekunden(gesendetAm: number, jetzt: number): number {
  return Math.max(0, ERNEUT_SENDEN_NACH_S - Math.floor((jetzt - gesendetAm) / 1_000))
}
