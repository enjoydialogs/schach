// Nahtstellen fuer die PWA (SCHACH-SPEC 10.13, Session S3d, #37). Service Worker, Manifest,
// Install- und Update-Logik baut S3d; bis dahin rendern beide Bausteine nichts.

/** Hinweis „Neue Version verfügbar“ plus „Neu laden“, nur in `Seite`-Layouts. */
export function UpdateHinweis() {
  return null
}

/** Install-Karte, nur auf Start und nach der Regel aus 10.13. */
export function InstallHinweis() {
  return null
}
