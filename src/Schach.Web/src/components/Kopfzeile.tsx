import type { ReactNode } from 'react'
import { ArrowLeft } from 'lucide-react'
import { t } from '../i18n/de'
import { useZurueck } from './zurueck'
import './bausteine.css'

/** Kopfzeile 56 px: Zurueck-Pfeil 48x48, Titel, optional eine Aktion (10.1). */
export function Kopfzeile({ titel, aktion }: { titel: string; aktion?: ReactNode }) {
  const zurueck = useZurueck()
  return (
    <header className="kopfzeile">
      <button type="button" className="symbolknopf" aria-label={t('kopfzeile.zurueck')} onClick={zurueck}>
        <ArrowLeft aria-hidden="true" />
      </button>
      <h1 className="kopfzeile__titel">{titel}</h1>
      {aktion !== undefined && <div className="kopfzeile__aktion">{aktion}</div>}
    </header>
  )
}
