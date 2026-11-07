import { t } from '../i18n/de'
import './bausteine.css'

/** `Fehlerkarte` (SCHACH-SPEC 10.14): Meldung plus „Erneut versuchen“. */
export function Fehlerkarte({ text = t('allgemein.fehler'), onErneut }: { text?: string; onErneut: () => void }) {
  return (
    <div className="karte" role="alert">
      <p>{text}</p>
      <button type="button" className="knopf knopf--outline" onClick={onErneut}>
        {t('allgemein.erneutVersuchen')}
      </button>
    </div>
  )
}
