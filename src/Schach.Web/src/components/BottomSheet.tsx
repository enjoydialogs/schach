import { useId, type ReactNode } from 'react'
import './bausteine.css'

/** Bottom-Sheet mit Titel (SCHACH-SPEC 10.14). Tippen daneben schliesst. */
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
  return (
    <div className={`ueberlage ueberlage--unten${abdunkeln ? '' : ' ueberlage--durchsichtig'}`} onClick={onSchliessen}>
      <div
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
