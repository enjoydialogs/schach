// Rechtstext, nicht rechtlich geprüft.
//
// Rahmen der Rechtstexte (nach GladiusEtPilum `_LegalLayout.cshtml`): eine lesbare Spalte unter der
// Überschrift, optional mit Stand-Datum. Layout `Seite` (SCHACH-SPEC 10.1). Kein lazy(): die Texte
// müssen offline lesbar sein und kommen in den Precache (10.12, 10.13).

import type { ReactNode } from 'react'
import { Seite } from '../components/Seite'
import './recht.css'

export function Rechtstext({
  titel,
  stand,
  children,
}: {
  titel: string
  stand?: string
  children: ReactNode
}) {
  return (
    <Seite>
      <article className="recht">
        <h1 className="wortmarke">{titel}</h1>
        {stand !== undefined && <p className="recht__stand">{stand}</p>}
        {children}
      </article>
    </Seite>
  )
}
