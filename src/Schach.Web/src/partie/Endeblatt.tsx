import { Trophy, X } from 'lucide-react'
import type { CSSProperties } from 'react'
import { Link } from 'react-router'
import type { SpruchDto, ZustandDto } from '../api/types'
import { de, t } from '../i18n/de'
import { BottomSheet } from '../components/BottomSheet'
import { eloGewinn, eloZeile, erklaerung, grundLabel, sichtAuf, titel } from './erklaerung'
import { fuelle } from './text'
import './sieg.css'

/**
 * Partieende-Blatt (SCHACH-SPEC 10.3): Titel, Grund, fester Erklaersatz, letzter Spruch, Elo-Zeile
 * oder Gast-Hinweis, „Neue Partie“ und „Nachspielen“ (nur Spieler), „Zur Startseite“.
 * Ohne Abdunkeln und schliessbar, die Stellung bleibt sichtbar. Beim Sieg ein Pokal-Band und das
 * Elo-Abzeichen; `feiern` (gerade hier gewonnen) laesst dazu einmal Konfetti aufspringen.
 */
export function Endeblatt({
  zustand,
  gast,
  spruch,
  feiern = false,
  onSchliessen,
  onNeuePartie,
  onNachspielen,
}: {
  zustand: ZustandDto
  gast: boolean
  spruch: SpruchDto | null
  /** Gerade hier beendet (nicht beim Oeffnen einer schon beendeten Partie): Sieg-Konfetti einmal. */
  feiern?: boolean
  onSchliessen: () => void
  onNeuePartie: () => void
  onNachspielen: () => void
}) {
  if (zustand.ergebnis === null || zustand.grund === null) return null
  const sicht = sichtAuf(zustand.ergebnis, zustand.spielerFarbe)
  const satz = erklaerung(zustand.grund, sicht, zustand.gegner.name)
  const elo = eloZeile({ ...zustand, gast })
  const sieg = sicht === 'sieg'
  const gewinn = sieg ? eloGewinn({ ...zustand, gast }) : null

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
      {sieg && <SiegBand feiern={feiern} />}
      <p className="ende__grund">{grundLabel(zustand.grund)}</p>
      {satz !== null && <p className="ende__satz">{satz}</p>}
      {spruch !== null && (
        <p className="ende__spruch">
          <strong>{zustand.gegner.name}:</strong> „{spruch.text}“
        </p>
      )}
      {gewinn !== null ? (
        <p className="ende__elo">
          {fuelle(de.partieende.eloNeu, { elo: gewinn.elo })} <span className="sieg__plus">{gewinn.plus}</span>
        </p>
      ) : elo !== null ? (
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

/** Konfetti-Stuecke: Ziel relativ zum Pokal (px), Drehung, Farbe 0 bis 2, Verzoegerung (ms). */
const KONFETTI: { x: number; y: number; r: number; f: 0 | 1 | 2; v: number }[] = [
  { x: -150, y: -18, r: -200, f: 0, v: 0 },
  { x: -118, y: 26, r: 160, f: 1, v: 40 },
  { x: -92, y: -30, r: -120, f: 2, v: 20 },
  { x: -64, y: 32, r: 240, f: 0, v: 60 },
  { x: -46, y: -34, r: -260, f: 1, v: 10 },
  { x: -24, y: 30, r: 140, f: 2, v: 50 },
  { x: 24, y: -32, r: 200, f: 0, v: 30 },
  { x: 44, y: 32, r: -160, f: 1, v: 0 },
  { x: 66, y: -28, r: 120, f: 2, v: 50 },
  { x: 90, y: 28, r: -240, f: 0, v: 20 },
  { x: 120, y: -22, r: 260, f: 1, v: 60 },
  { x: 150, y: 20, r: -140, f: 2, v: 10 },
]

/**
 * Pokal-Band beim Sieg (Peak-End-Regel): statischer Hervorhebungs-Streifen mit Pokal; mit `feiern`
 * springt der Pokal federnd auf und Konfetti fliegt einmal aus (~800 ms, nur CSS). Bei
 * `prefers-reduced-motion: reduce` bleibt nur der statische Streifen (sieg.css). Rein dekorativ.
 */
function SiegBand({ feiern }: { feiern: boolean }) {
  return (
    <div className={`sieg${feiern ? ' sieg--feiern' : ''}`} data-testid="sieg-band" aria-hidden="true">
      <Trophy className="sieg__pokal" />
      {feiern &&
        KONFETTI.map((k, i) => (
          <span
            key={i}
            className={`sieg__konfetti sieg__konfetti--${k.f}`}
            style={{ '--x': `${k.x}px`, '--y': `${k.y}px`, '--r': `${k.r}deg`, '--v': `${k.v}ms` } as CSSProperties}
          />
        ))}
    </div>
  )
}
