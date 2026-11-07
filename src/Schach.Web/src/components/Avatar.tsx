import { Check } from 'lucide-react'
import type { Stufe } from '../api/types'
import './bausteine.css'

const AVATAR_KLASSE: Record<Stufe, string> = {
  Anfaenger: 'avatar--anfaenger',
  Hobbyspieler: 'avatar--hobbyspieler',
  Vereinsspieler: 'avatar--vereinsspieler',
  Turnierspieler: 'avatar--turnierspieler',
  Meister: 'avatar--meister',
}

/**
 * Anfangsbuchstabe im Kreis, Farbe je Spielstaerke (SCHACH-SPEC 4.4). 48 px in der Gegnerwahl,
 * 40 px im Partie-Kopf. Besiegt: Haekchen unten rechts.
 */
export function Avatar({
  gegner,
  groesse = 40,
  besiegt = false,
}: {
  gegner: { name: string; stufe: Stufe }
  groesse?: 40 | 48
  besiegt?: boolean
}) {
  return (
    <span className={`avatar ${AVATAR_KLASSE[gegner.stufe]}${groesse === 48 ? ' avatar--48' : ''}`} aria-hidden="true">
      {gegner.name.charAt(0)}
      {besiegt && (
        <span className="avatar__besiegt" data-testid="besiegt">
          <Check size={12} strokeWidth={3} />
        </span>
      )}
    </span>
  )
}
