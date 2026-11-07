import { Seite } from '../components/Seite'
import { t } from '../i18n/de'

/** Platzhalter der Startseite (`/`). Den Hub nach SCHACH-SPEC 10.4 baut S3b. */
export function Start() {
  return (
    <Seite>
      <h1 className="wortmarke">{t('app.name')}</h1>
      <p className="text-muted">{t('start.untertitel')}</p>
    </Seite>
  )
}
