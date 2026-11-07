import { useId, type ReactNode } from 'react'
import './bausteine.css'

/** Dialog mittig mit Titel, Text und Knopfreihe (SCHACH-SPEC 10.14). Tippen daneben schliesst. */
export function Dialog({
  titel,
  children,
  knoepfe,
  onSchliessen,
  senkrecht = false,
}: {
  titel: string
  children: ReactNode
  knoepfe: ReactNode
  onSchliessen: () => void
  /** Knoepfe untereinander, z. B. bei drei Wahlmoeglichkeiten (10.5). */
  senkrecht?: boolean
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
        <div className={`dialog__knoepfe${senkrecht ? ' dialog__knoepfe--senkrecht' : ''}`}>{knoepfe}</div>
      </div>
    </div>
  )
}
