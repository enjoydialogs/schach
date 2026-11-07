import { t } from '../i18n/de'
import { useOnline } from './online'
import './bausteine.css'

/** Offline-Banner auf jeder `Seite` (SCHACH-SPEC 10.13). */
export function OfflineBanner() {
  const online = useOnline()
  if (online) return null
  return (
    <p className="banner" role="status">
      {t('offline.banner')}
    </p>
  )
}
