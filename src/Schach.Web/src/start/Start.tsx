import { useEffect, useState } from 'react'
import { ChevronRight } from 'lucide-react'
import { Link, useNavigate } from 'react-router'
import type { ZustandDto } from '../api/types'
import { Avatar } from '../components/Avatar'
import { Ladeanzeige } from '../components/Ladeanzeige'
import { useOnline } from '../components/online'
import { gibPartieAuf, ladeOffenePartie } from '../components/offenePartie'
import { OffenePartieDialog } from '../components/OffenePartieDialog'
import { InstallHinweis } from '../components/pwa'
import { Seite } from '../components/Seite'
import { ladeSitzung, useSitzung } from '../components/sitzung'
import { de, t } from '../i18n/de'
import { fuelle } from '../partie/text'

/**
 * Start (`/`), der Hub (SCHACH-SPEC 10.1, 10.4): Wortmarke, Elo-Chip oder „Anmelden“, Hauptkarte
 * mit offener Partie, Zeilen „Partien“ und „Profil“ (Spieler) oder Gast-Hinweis (10.11).
 */
export function Start() {
  const navigate = useNavigate()
  const sitzung = useSitzung()
  const online = useOnline()
  // Sitzung und offene Partie laden parallel; Skelett, bis beide da sind.
  const [offen, setOffen] = useState<{ spieler: ZustandDto | null; gast: ZustandDto | null } | null>(null)
  const [frage, setFrage] = useState(false)
  const [beschaeftigt, setBeschaeftigt] = useState(false)
  const [fehler, setFehler] = useState<string | null>(null)

  useEffect(() => {
    let aktiv = true
    // Elo-Chip aktuell halten, z. B. nach einer gerade beendeten Partie.
    void ladeSitzung()
    void Promise.all([ladeOffenePartie(false), ladeOffenePartie(true)]).then(([spieler, gast]) => {
      if (aktiv) setOffen({ spieler, gast })
    })
    return () => {
      aktiv = false
    }
  }, [])

  const gast = sitzung.status === 'gast'
  const spieler = sitzung.status === 'spieler' ? sitzung.spieler : null
  const laedt = sitzung.status === 'laedt' || offen === null
  const partie = offen === null ? null : gast ? offen.gast : (offen.spieler ?? (sitzung.status === 'unbekannt' ? offen.gast : null))

  const aufgeben = async () => {
    if (partie === null) return
    setBeschaeftigt(true)
    setFehler(null)
    try {
      await gibPartieAuf(partie)
      navigate('/neu')
    } catch {
      setFehler(t('allgemein.fehler'))
      setBeschaeftigt(false)
    }
  }

  return (
    <Seite>
      <header className="startkopf">
        <h1 className="wortmarke">{t('app.name')}</h1>
        {spieler !== null && (
          <Link className="chip" to="/profil">
            {fuelle(de.hub.eloChip, { elo: spieler.elo })}
          </Link>
        )}
        {gast && (
          <Link className="chip" to="/anmelden">
            {t('hub.anmelden')}
          </Link>
        )}
      </header>

      {laedt ? (
        <Ladeanzeige skelette={2} gross />
      ) : (
        <>
          <section className="karte">
            {partie !== null && (
              <div className="zeile zeile--flach">
                <Avatar gegner={partie.gegner} />
                <span className="zeile__mitte">
                  <span className="karte__titel">{fuelle(de.hub.offenePartie, { name: partie.gegner.name })}</span>
                  <span className="zeile__klein">{de.bedenkzeit[partie.bedenkzeit]}</span>
                </span>
              </div>
            )}
            {partie === null && <p className="text-muted">{t('start.untertitel')}</p>}
            <div className="karte__knoepfe">
              {partie !== null && (
                <button
                  type="button"
                  className="knopf"
                  disabled={!online}
                  onClick={() => navigate(`/partie/${partie.id}`)}
                >
                  {t('hub.fortsetzen')}
                </button>
              )}
              {partie === null && online ? (
                <Link className="knopf" to="/neu">
                  {t('hub.neuePartie')}
                </Link>
              ) : (
                <button
                  type="button"
                  className={`knopf${partie !== null ? ' knopf--outline' : ''}`}
                  disabled={!online}
                  onClick={() => setFrage(true)}
                >
                  {t('hub.neuePartie')}
                </button>
              )}
            </div>
            {!online && <p className="text-muted">{t('offline.spielenBrauchtInternet')}</p>}
          </section>

          {spieler !== null || sitzung.status === 'unbekannt' ? (
            <ul className="liste">
              <li>
                <Link className="zeile" to="/partien">
                  <span className="zeile__mitte zeile__titel">{t('hub.partien')}</span>
                  <ChevronRight className="zeile__pfeil" aria-hidden="true" />
                </Link>
              </li>
              <li>
                <Link className="zeile" to="/profil">
                  <span className="zeile__mitte zeile__titel">{t('hub.profil')}</span>
                  <ChevronRight className="zeile__pfeil" aria-hidden="true" />
                </Link>
              </li>
            </ul>
          ) : (
            <section className="karte karte--gast">
              <p className="karte__text">{t('hub.gastText')}</p>
              <Link to="/anmelden">{t('hub.gastLink')}</Link>
            </section>
          )}

          <InstallHinweis />
        </>
      )}

      {frage && partie !== null && (
        <OffenePartieDialog
          partie={partie}
          gast={gast}
          beschaeftigt={beschaeftigt}
          fehler={fehler}
          onFortsetzen={() => navigate(`/partie/${partie.id}`)}
          onAufgeben={() => void aufgeben()}
          onAbbrechen={() => {
            setFrage(false)
            setFehler(null)
          }}
        />
      )}
    </Seite>
  )
}
