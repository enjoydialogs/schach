import { Link } from 'react-router'
import { Seite } from '../components/Seite'
import { t } from '../i18n/de'

/** `*`: eine Seite, nie eine Umleitung (SCHACH-SPEC 10.2, 10.12). */
export function NichtGefunden() {
  return (
    <Seite>
      <h1 className="wortmarke">{t('nichtGefunden.titel')}</h1>
      <Link className="knopf" to="/">
        {t('allgemein.zurStartseite')}
      </Link>
    </Seite>
  )
}
