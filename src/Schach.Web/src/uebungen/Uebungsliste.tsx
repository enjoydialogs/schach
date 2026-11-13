import { ChevronRight } from 'lucide-react'
import { Link } from 'react-router'
import { Seite } from '../components/Seite'
import { de, t } from '../i18n/de'
import { BEREICHE, UEBUNGEN } from './katalog'

/** Uebungen (`/uebungen`, SCHACH-SPEC 10.16): alle Uebungen, nach Bereich gruppiert. */
export function Uebungsliste() {
  return (
    <Seite titel={t('uebungen.titel')}>
      <p className="text-muted">{t('uebungen.untertitel')}</p>
      {BEREICHE.map((bereich) => (
        <section key={bereich} aria-labelledby={`bereich-${bereich}`}>
          <h2 id={`bereich-${bereich}`} className="abschnitt">
            {de.uebungen.bereich[bereich]}
          </h2>
          <ul className="liste">
            {UEBUNGEN.filter((u) => u.bereich === bereich).map((u) => (
              <li key={u.key}>
                <Link className="zeile" to={`/uebungen/${u.key}`}>
                  <span className="zeile__mitte">
                    <span className="zeile__titel">{u.titel}</span>
                    <span className="zeile__klein">{u.kurz}</span>
                  </span>
                  <ChevronRight className="zeile__pfeil" aria-hidden="true" />
                </Link>
              </li>
            ))}
          </ul>
        </section>
      ))}
    </Seite>
  )
}
