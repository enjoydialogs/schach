import type { ReactNode } from 'react'
import { Fusszeile } from './Fusszeile'
import { Hinweis } from './Hinweis'
import { Kopfzeile } from './Kopfzeile'
import { OfflineBanner } from './OfflineBanner'
import { UpdateHinweis } from './pwa'

/**
 * Layout "Seite" (SCHACH-SPEC 10.1): scrollt, 16 px Rand, Kopf- und Fusszeile. Mit `titel` traegt
 * die Seite eine Kopfzeile mit Zurueck-Pfeil; die Startseite bringt ihre Wortmarke selbst mit.
 * Offline-Banner (10.13), Update-Hinweis (Naht fuer S3d) und Toast haengen hier.
 */
export function Seite({ children, titel, aktion }: { children: ReactNode; titel?: string; aktion?: ReactNode }) {
  return (
    <div className="seite">
      {titel !== undefined && <Kopfzeile titel={titel} aktion={aktion} />}
      <OfflineBanner />
      <UpdateHinweis />
      <main className="seite__inhalt">{children}</main>
      <Fusszeile />
      <Hinweis />
    </div>
  )
}
