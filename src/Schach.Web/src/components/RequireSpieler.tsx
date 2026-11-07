import type { ReactNode } from 'react'
import { Navigate } from 'react-router'
import { Ladeanzeige } from './Ladeanzeige'
import { Seite } from './Seite'
import { useSitzung } from './sitzung'

/**
 * Guard fuer Spieler-Routen (SCHACH-SPEC 10.1): `gast` → `/anmelden`; `unbekannt` (Netzfehler)
 * rendert das Kind, damit ein Spieler offline gecachte Partien erreicht.
 */
export function RequireSpieler({ children }: { children: ReactNode }) {
  const { status } = useSitzung()
  if (status === 'laedt') {
    return (
      <Seite>
        <Ladeanzeige />
      </Seite>
    )
  }
  if (status === 'gast') return <Navigate to="/anmelden" replace />
  return children
}
