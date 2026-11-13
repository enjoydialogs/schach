import { Link } from 'react-router'
import { t } from '../i18n/de'
import { kurzSha, quellcodeUrl } from './quellcode'

/** Fusszeile 13 px, Links 48 px hoch tippbar (SCHACH-SPEC 10.1): Impressum · Datenschutz · Quellcode mit Commit-SHA. */
export function Fusszeile({ commitSha = import.meta.env.VITE_COMMIT_SHA }: { commitSha?: string }) {
  const sha = kurzSha(commitSha)
  return (
    <footer className="fusszeile">
      <Link to="/impressum">{t('fusszeile.impressum')}</Link>
      {t('fusszeile.trenner')}
      <Link to="/datenschutz">{t('fusszeile.datenschutz')}</Link>
      {t('fusszeile.trenner')}
      <a href={quellcodeUrl(sha)} rel="noopener">
        {t('fusszeile.quellcode')} {sha ?? 'dev'}
      </a>
    </footer>
  )
}
