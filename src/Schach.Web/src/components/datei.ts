// Dateien teilen oder herunterladen (PGN in Nachspielen, JSON-Export im Profil).

const ZEITZONE = 'Europe/Berlin'

/** `2026-11-07` in deutscher Zeit, fuer Dateinamen. */
export function isoDatum(zeitpunkt: string | number | Date): string {
  return new Intl.DateTimeFormat('en-CA', { timeZone: ZEITZONE, year: 'numeric', month: '2-digit', day: '2-digit' }).format(
    new Date(zeitpunkt),
  )
}

/** `7. Nov.` in deutscher Zeit (Partienliste, 10.7). */
export function kurzDatum(zeitpunkt: string | number | Date): string {
  return new Intl.DateTimeFormat('de-DE', { timeZone: ZEITZONE, day: 'numeric', month: 'short' }).format(new Date(zeitpunkt))
}

/** `7. November 2026` in deutscher Zeit („Spieler seit …“, 10.9). */
export function langDatum(zeitpunkt: string | number | Date): string {
  return new Intl.DateTimeFormat('de-DE', { timeZone: ZEITZONE, day: 'numeric', month: 'long', year: 'numeric' }).format(
    new Date(zeitpunkt),
  )
}

/** Laedt Text als Datei herunter (Blob-URL, kein Fremd-Host). */
export function ladeHerunter(inhalt: string, dateiname: string, typ: string): void {
  const url = URL.createObjectURL(new Blob([inhalt], { type: typ }))
  const a = document.createElement('a')
  a.href = url
  a.download = dateiname
  a.rel = 'noopener'
  document.body.append(a)
  a.click()
  a.remove()
  setTimeout(() => URL.revokeObjectURL(url), 0)
}

/**
 * Teilt eine Datei per Web Share, sonst Download (SCHACH-SPEC 10.8). Geht offline, weil der Inhalt
 * schon im Speicher liegt. Bricht der Nutzer das Teilen ab, passiert nichts weiter.
 */
export async function teileOderLadeHerunter(inhalt: string, dateiname: string, typ: string): Promise<'geteilt' | 'geladen'> {
  const datei = typeof File === 'function' ? new File([inhalt], dateiname, { type: typ }) : null
  const nav = globalThis.navigator as Navigator | undefined
  if (datei !== null && nav?.canShare?.({ files: [datei] }) === true && typeof nav.share === 'function') {
    try {
      await nav.share({ files: [datei], title: dateiname })
      return 'geteilt'
    } catch (e) {
      if (e instanceof DOMException && e.name === 'AbortError') return 'geteilt'
      // Teilen fehlgeschlagen (z. B. Typ nicht erlaubt): Download als Ausweg
    }
  }
  ladeHerunter(inhalt, dateiname, typ)
  return 'geladen'
}
