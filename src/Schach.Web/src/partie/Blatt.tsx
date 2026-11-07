import { useId, type ReactNode } from 'react'

// Dialog und Bottom-Sheet fuer den Partie-Bildschirm (SCHACH-SPEC 10.3). Die gemeinsamen Bausteine
// `Dialog` und `BottomSheet` aus 10.14 legt S3b an; bis dahin leben sie hier.

export function Dialog({
  titel,
  children,
  knoepfe,
  onSchliessen,
}: {
  titel: string
  children: ReactNode
  knoepfe: ReactNode
  onSchliessen: () => void
}) {
  const titelId = useId()
  return (
    <div className="ueberlage ueberlage--mitte" onClick={onSchliessen}>
      <div
        className="dialog"
        role="dialog"
        aria-modal="true"
        aria-labelledby={titelId}
        onClick={(e) => e.stopPropagation()}
      >
        <h2 id={titelId} className="dialog__titel">
          {titel}
        </h2>
        <div className="dialog__text">{children}</div>
        <div className="dialog__knoepfe">{knoepfe}</div>
      </div>
    </div>
  )
}

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

/** Schalterzeile 48 px. */
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
