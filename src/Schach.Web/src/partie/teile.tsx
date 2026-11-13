import { useEffect, useState } from 'react'
import { Ellipsis, Flag, Handshake, Lightbulb, Plus, Rewind, Undo2, type LucideIcon } from 'lucide-react'
import type { Farbe, GegnerDto, UhrDto, UhrSeite } from '../api/types'
import { Avatar } from '../components/Avatar'
import { de, t } from '../i18n/de'
import type { Knopf, KnopfArt } from './knoepfe'
import { fuelle } from './text'
import { formatiereUhr, restMs, uhrZustand } from './uhr'

// Zeilen des Partie-Bildschirms (SCHACH-SPEC 10.3): Kopf (Avatar neben Name und Spruchblase),
// Statuszeile, eigene Zeile, Knopfleiste.

export function Uhr({
  uhr,
  seite,
  versatzMs,
  aktiv,
  label,
}: {
  uhr: UhrDto
  seite: UhrSeite
  versatzMs: number
  /** Hervorgehoben: wer sichtbar am Zug ist (der Gegner auch waehrend seiner Denkzeit). */
  aktiv: boolean
  label: string
}) {
  // Nur die laufende Uhr tickt, und nur sie rendert neu.
  const laeuft = uhr.laeuftFuer === seite
  const [jetzt, setJetzt] = useState(() => Date.now())
  useEffect(() => {
    if (!laeuft) return
    const takt = setInterval(() => setJetzt(Date.now()), 250)
    return () => clearInterval(takt)
  }, [laeuft])
  const rest = restMs(uhr, seite, jetzt, versatzMs)
  const zustand = uhrZustand(rest, aktiv)
  return (
    <span className={`uhr uhr--${zustand}`} role="timer" aria-label={label}>
      {formatiereUhr(rest)}
    </span>
  )
}

export function AmZugPunkt({ sichtbar }: { sichtbar: boolean }) {
  return <span className={`am-zug${sichtbar ? '' : ' am-zug--aus'}`} aria-label={sichtbar ? t('partie.amZug') : undefined} />
}

/** Kopf: Avatar links ueber zwei Zeilen, rechts oben Name, Uhr und Menue, darunter die Spruchblase. */
export function Kopfzeile({
  gegner,
  amZug,
  uhr,
  spruch,
  onMenue,
}: {
  gegner: GegnerDto
  amZug: boolean
  uhr: React.ReactNode
  /** Die Spruchblase; sie zeigt mit ihrer Spitze auf den Avatar. */
  spruch: React.ReactNode
  onMenue: () => void
}) {
  return (
    <header className="partie__kopf partie__kopf--blase">
      <div className="partie__gesicht">
        <Avatar gegner={gegner} groesse={96} />
      </div>
      <div className="partie__kopfzeile">
        <div className="partie__gegner">
          <span className="partie__name">
            {gegner.name}
            <AmZugPunkt sichtbar={amZug} />
          </span>
          <span className="partie__elo">{fuelle(de.partie.elo, { elo: gegner.elo })}</span>
        </div>
        {uhr}
        <button type="button" className="symbolknopf" aria-label={t('partie.menue')} onClick={onMenue}>
          <Ellipsis aria-hidden="true" />
        </button>
      </div>
      {spruch}
    </header>
  )
}

/** Mit `onTipp` ist die Blase ein Knopf, der den Spruch erneut vorliest (6.9). */
export function Spruchblase({ text, onTipp }: { text: string | null; onTipp?: () => void }) {
  return (
    <div className="partie__spruch" aria-live="polite">
      {text !== null &&
        (onTipp ? (
          <button type="button" className="spruchblase spruchblase--knopf" aria-label={fuelle(de.partie.spruchVorlesen, { text })} onClick={onTipp}>
            <span className="spruchblase__text">{text}</span>
          </button>
        ) : (
          <p className="spruchblase">
            <span className="spruchblase__text">{text}</span>
          </p>
        ))}
    </div>
  )
}

export type StatusArt = 'offline' | 'hinweis' | 'tipp' | 'warnung' | 'schach' | 'denkt' | 'leer'

export function Statuszeile({ art, text }: { art: StatusArt; text: string }) {
  return (
    <div className={`partie__status status--${art}`} role="status" aria-live="polite" data-testid="statuszeile">
      {text}
    </div>
  )
}

export function EigeneZeile({
  farbe,
  amZug,
  pille,
  uhr,
}: {
  farbe: Farbe
  amZug: boolean
  pille: string | null
  uhr: React.ReactNode
}) {
  return (
    <div className="partie__eigene">
      <span className="partie__name">
        {t('partie.du')}
        <span
          className={`farbpunkt farbpunkt--${farbe === 'Weiss' ? 'weiss' : 'schwarz'}`}
          role="img"
          aria-label={farbe === 'Weiss' ? t('partie.farbeWeiss') : t('partie.farbeSchwarz')}
        />
        <AmZugPunkt sichtbar={amZug} />
      </span>
      {pille !== null && <span className="pille">{pille}</span>}
      <span className="partie__fueller" />
      {uhr}
    </div>
  )
}

// Gewichtung (10.3): Im Spiel ist das Brett der Blickfang, alle Knoepfe sind leise. Nach dem Ende
// ist „Neue Partie“ der eine gefuellte Hauptknopf. Tipp wird nie primaer: er nimmt die Elo-Wertung.
const KNOPF: Record<KnopfArt, { text: string; Icon: LucideIcon; gefahr?: boolean; primaer?: boolean }> = {
  zuruecknehmen: { text: de.partie.knopfZuruecknehmen, Icon: Undo2 },
  tipp: { text: de.partie.knopfTipp, Icon: Lightbulb },
  remis: { text: de.partie.knopfRemis, Icon: Handshake },
  aufgeben: { text: de.partie.knopfAufgeben, Icon: Flag, gefahr: true },
  nachspielen: { text: de.partie.knopfNachspielen, Icon: Rewind },
  neuePartie: { text: de.partie.knopfNeuePartie, Icon: Plus, primaer: true },
}

/** Gesperrte Knoepfe bleiben tippbar (`aria-disabled`) und melden `onKnopf`; der Aufrufer zeigt den Grund. */
export function Knopfleiste({ knoepfe, onKnopf }: { knoepfe: Knopf[]; onKnopf: (knopf: Knopf) => void }) {
  return (
    <nav className="partie__knoepfe">
      {knoepfe.map((knopf) => {
        const { art, aktiv } = knopf
        const { text, Icon, gefahr, primaer } = KNOPF[art]
        return (
          <button
            key={art}
            type="button"
            className={`leistenknopf ${primaer ? 'leistenknopf--primaer' : 'leistenknopf--leise'}${gefahr ? ' leistenknopf--gefahr' : ''}`}
            aria-disabled={aktiv ? undefined : true}
            onClick={() => onKnopf(knopf)}
          >
            <Icon aria-hidden="true" size={22} />
            <span>{text}</span>
          </button>
        )
      })}
    </nav>
  )
}
