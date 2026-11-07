import { useEffect, useState } from 'react'
import { t } from '../i18n/de'
import './bausteine.css'

const SPINNER_NACH_MS = 300

/**
 * `Ladeanzeige` (SCHACH-SPEC 10.14): Spinner erst nach 300 ms, damit schnelle Antworten nicht
 * flackern; fuer Listen Skelette in Zeilenhoehe.
 */
export function Ladeanzeige({ skelette = 0, gross = false }: { skelette?: number; gross?: boolean }) {
  const [sichtbar, setSichtbar] = useState(false)
  useEffect(() => {
    if (skelette > 0) return
    const timer = setTimeout(() => setSichtbar(true), SPINNER_NACH_MS)
    return () => clearTimeout(timer)
  }, [skelette])

  if (skelette > 0) {
    return (
      <div aria-busy="true" aria-label={t('laden.laedt')} role="status">
        {Array.from({ length: skelette }, (_, i) => (
          <div key={i} className={`skelett${gross && i === 0 ? ' skelett--gross' : ''}`} />
        ))}
      </div>
    )
  }
  return (
    <div aria-busy="true" aria-label={t('laden.laedt')} role="status">
      {sichtbar && <span className="spinner" />}
    </div>
  )
}
