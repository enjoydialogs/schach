import { useId, type CSSProperties, type ReactNode } from 'react'
import { useUeberlage } from './useUeberlage'
import './bausteine.css'

/** Bottom-Sheet mit Titel (SCHACH-SPEC 10.14). Tippen daneben oder Escape schliesst. */
export function BottomSheet({
  titel,
  children,
  onSchliessen,
  abdunkeln = true,
  kopf,
}: {
  titel: string
  children: ReactNode
  onSchliessen: () => void
  /** Ohne Abdunkeln bleibt die Stellung voll sichtbar (Partieende). */
  abdunkeln?: boolean
  kopf?: ReactNode
}) {
  const titelId = useId()
  const flaeche = useUeberlage<HTMLDivElement>(onSchliessen)
  return (
    <div className={`ueberlage ueberlage--unten${abdunkeln ? '' : ' ueberlage--durchsichtig'}`} onClick={onSchliessen}>
      <div
        ref={flaeche}
        tabIndex={-1}
        className="sheet"
        role="dialog"
        aria-modal="true"
        aria-labelledby={titelId}
        onClick={(e) => e.stopPropagation()}
      >
        <div className="sheet__kopf">
          <h2 id={titelId} className="sheet__titel">
            {titel}
          </h2>
          {kopf}
        </div>
        {children}
      </div>
    </div>
  )
}

/** Schalterzeile 48 px (Partie-Menue und Profil). */
export function Schalter({
  text,
  an,
  onWechsel,
}: {
  text: string
  an: boolean
  onWechsel: (an: boolean) => void
}) {
  return (
    <label className="schalter">
      <span>{text}</span>
      <input type="checkbox" role="switch" checked={an} onChange={(e) => onWechsel(e.target.checked)} />
    </label>
  )
}

const REGLER_TASTEN = new Set(['ArrowLeft', 'ArrowRight', 'ArrowUp', 'ArrowDown', 'Home', 'End', 'PageUp', 'PageDown'])

/**
 * Lautstaerke-Regler 48 px (Partie-Menue und Profil): 10 bis 100 % in Zehnerschritten. `wert` und
 * `onWechsel` rechnen in 0,1 bis 1. Aus ist der Schalter darueber, nicht 0 %. `onLoslassen` laeuft
 * nach dem Ziehen oder einer Pfeiltaste, etwa fuer einen Probeton.
 */
export function Regler({
  text,
  wert,
  aktiv = true,
  onWechsel,
  onLoslassen,
}: {
  text: string
  wert: number
  aktiv?: boolean
  onWechsel: (wert: number) => void
  onLoslassen?: () => void
}) {
  const prozent = Math.round(wert * 100)
  return (
    <label className="regler">
      <span className="regler__text">{text}</span>
      <input
        type="range"
        min={10}
        max={100}
        step={10}
        value={prozent}
        disabled={!aktiv}
        aria-valuetext={`${prozent} %`}
        style={{ '--regler-anteil': `${((prozent - 10) / 90) * 100}%` } as CSSProperties}
        onChange={(e) => onWechsel(Number(e.target.value) / 100)}
        onPointerUp={onLoslassen}
        onKeyUp={(e) => {
          if (REGLER_TASTEN.has(e.key)) onLoslassen?.()
        }}
      />
      <span className="regler__wert" aria-hidden="true">
        {prozent} %
      </span>
    </label>
  )
}
