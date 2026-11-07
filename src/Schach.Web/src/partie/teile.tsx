import { Ellipsis, Flag, Handshake, Lightbulb, Plus, Rewind, Undo2, type LucideIcon } from 'lucide-react'
import type { Farbe, GegnerDto, Stufe, UhrDto, UhrSeite } from '../api/types'
import { de, t } from '../i18n/de'
import type { Knopf, KnopfArt } from './knoepfe'
import { fuelle } from './text'
import { formatiereUhr, restMs, uhrZustand } from './uhr'

// Zeilen des Partie-Bildschirms (SCHACH-SPEC 10.3): Kopfzeile, Spruchblase, Statuszeile,
// eigene Zeile, Knopfleiste.

const AVATAR_KLASSE: Record<Stufe, string> = {
  Anfaenger: 'avatar--anfaenger',
  Hobbyspieler: 'avatar--hobbyspieler',
  Vereinsspieler: 'avatar--vereinsspieler',
  Turnierspieler: 'avatar--turnierspieler',
  Meister: 'avatar--meister',
}

/** Anfangsbuchstabe im Kreis, Farbe je Spielstaerke (4.4). */
export function Avatar({ gegner }: { gegner: GegnerDto }) {
  return (
    <span className={`avatar ${AVATAR_KLASSE[gegner.stufe]}`} aria-hidden="true">
      {gegner.name.charAt(0)}
    </span>
  )
}

export function Uhr({
  uhr,
  seite,
  jetztClient,
  versatzMs,
  aktiv,
  label,
}: {
  uhr: UhrDto
  seite: UhrSeite
  jetztClient: number
  versatzMs: number
  /** Hervorgehoben: wer sichtbar am Zug ist (der Gegner auch waehrend seiner Denkzeit). */
  aktiv: boolean
  label: string
}) {
  const rest = restMs(uhr, seite, jetztClient, versatzMs)
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

export function Kopfzeile({
  gegner,
  amZug,
  uhr,
  onMenue,
}: {
  gegner: GegnerDto
  amZug: boolean
  uhr: React.ReactNode
  onMenue: () => void
}) {
  return (
    <header className="partie__kopf">
      <Avatar gegner={gegner} />
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
    </header>
  )
}

export function Spruchblase({ text }: { text: string | null }) {
  return (
    <div className="partie__spruch" aria-live="polite">
      {text !== null && <p className="spruchblase">{text}</p>}
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

const KNOPF: Record<KnopfArt, { text: string; Icon: LucideIcon; gefahr?: boolean }> = {
  zuruecknehmen: { text: de.partie.knopfZuruecknehmen, Icon: Undo2 },
  tipp: { text: de.partie.knopfTipp, Icon: Lightbulb },
  remis: { text: de.partie.knopfRemis, Icon: Handshake },
  aufgeben: { text: de.partie.knopfAufgeben, Icon: Flag, gefahr: true },
  nachspielen: { text: de.partie.knopfNachspielen, Icon: Rewind },
  neuePartie: { text: de.partie.knopfNeuePartie, Icon: Plus },
}

export function Knopfleiste({ knoepfe, onKnopf }: { knoepfe: Knopf[]; onKnopf: (art: KnopfArt) => void }) {
  return (
    <nav className="partie__knoepfe">
      {knoepfe.map(({ art, aktiv }) => {
        const { text, Icon, gefahr } = KNOPF[art]
        return (
          <button
            key={art}
            type="button"
            className={`leistenknopf${gefahr ? ' leistenknopf--gefahr' : ''}`}
            disabled={!aktiv}
            onClick={() => onKnopf(art)}
          >
            <Icon aria-hidden="true" size={22} />
            <span>{text}</span>
          </button>
        )
      })}
    </nav>
  )
}
