import type { ReactNode } from 'react'
import { Fusszeile } from './Fusszeile'

/** Layout "Seite" (SCHACH-SPEC 10.1): scrollt, 16 px Rand, Fusszeile. Kopfzeile, Offline-Banner
 *  und Update-Hinweis haengen spaetere Sessions hier an (S3b, S3d). */
export function Seite({ children }: { children: ReactNode }) {
  return (
    <div className="seite">
      <main className="seite__inhalt">{children}</main>
      <Fusszeile />
    </div>
  )
}
