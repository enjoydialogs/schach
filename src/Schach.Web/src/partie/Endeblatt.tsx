import { X } from 'lucide-react'
import { Link } from 'react-router'
import type { SpruchDto, ZustandDto } from '../api/types'
import { t } from '../i18n/de'
import { BottomSheet } from '../components/BottomSheet'
import { eloZeile, erklaerung, grundLabel, sichtAuf, titel } from './erklaerung'

/**
 * Partieende-Blatt (SCHACH-SPEC 10.3): Titel, Grund, fester Erklaersatz, letzter Spruch, Elo-Zeile
 * oder Gast-Hinweis, „Neue Partie“ und „Nachspielen“ (nur Spieler), „Zur Startseite“.
 * Ohne Abdunkeln und schliessbar, die Stellung bleibt sichtbar.
 */
export function Endeblatt({
  zustand,
  gast,
  spruch,
  onSchliessen,
  onNeuePartie,
  onNachspielen,
}: {
  zustand: ZustandDto
  gast: boolean
  spruch: SpruchDto | null
  onSchliessen: () => void
  onNeuePartie: () => void
  onNachspielen: () => void
}) {
  if (zustand.ergebnis === null || zustand.grund === null) return null
  const sicht = sichtAuf(zustand.ergebnis, zustand.spielerFarbe)
  const satz = erklaerung(zustand.grund, sicht, zustand.gegner.name)
  const elo = eloZeile({ ...zustand, gast })

  return (
    <BottomSheet
      titel={titel(sicht)}
      abdunkeln={false}
      onSchliessen={onSchliessen}
      kopf={
        <button type="button" className="symbolknopf" aria-label={t('partieende.schliessen')} onClick={onSchliessen}>
          <X aria-hidden="true" />
        </button>
      }
    >
      <p className="ende__grund">{grundLabel(zustand.grund)}</p>
      {satz !== null && <p className="ende__satz">{satz}</p>}
      {spruch !== null && (
        <p className="ende__spruch">
          <strong>{zustand.gegner.name}:</strong> „{spruch.text}“
        </p>
      )}
      {elo !== null ? (
        <p className="ende__elo">{elo}</p>
      ) : (
        <p className="ende__elo">
          <Link to="/anmelden">{t('partieende.gastHinweis')}</Link>
        </p>
      )}
      <div className="ende__knoepfe">
        <button type="button" className="knopf" onClick={onNeuePartie}>
          {t('partie.knopfNeuePartie')}
        </button>
        {!gast && (
          <button type="button" className="knopf knopf--outline" onClick={onNachspielen}>
            {t('partie.knopfNachspielen')}
          </button>
        )}
      </div>
      <Link className="ende__start" to="/">
        {t('partieende.zurStartseite')}
      </Link>
    </BottomSheet>
  )
}
