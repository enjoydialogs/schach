import { Check } from 'lucide-react'
import type { Stufe } from '../api/types'
import { gesichtFuer } from './gesichter/katalog'
import './bausteine.css'

const AVATAR_KLASSE: Record<Stufe, string> = {
  Anfaenger: 'avatar--anfaenger',
  Hobbyspieler: 'avatar--hobbyspieler',
  Vereinsspieler: 'avatar--vereinsspieler',
  Turnierspieler: 'avatar--turnierspieler',
  Meister: 'avatar--meister',
}

const GROESSEN_KLASSE: Record<40 | 48 | 96, string> = {
  40: '',
  48: ' avatar--48',
  96: ' avatar--96',
}

/**
 * Comic-Gesicht des Gegners im Kreis (SCHACH-SPEC 4.4), nachgeschlagen ueber den Gegner-Key.
 * Unbekannter Key: Anfangsbuchstabe im Kreis, Farbe je Spielstaerke. 40 px Standard, 48 px in der
 * Gegnerwahl, 96 px fuer Kopf mit Sprechblase. Besiegt: Haekchen unten rechts.
 */
export function Avatar({
  gegner,
  groesse = 40,
  besiegt = false,
}: {
  gegner: { key: string; name: string; stufe: Stufe }
  groesse?: 40 | 48 | 96
  besiegt?: boolean
}) {
  const gesicht = gesichtFuer(gegner.key)
  const art = gesicht === undefined ? AVATAR_KLASSE[gegner.stufe] : 'avatar--gesicht'
  return (
    <span className={`avatar ${art}${GROESSEN_KLASSE[groesse]}`} aria-hidden="true" data-gegner={gegner.key}>
      {gesicht === undefined ? gegner.name.charAt(0) : <span className="avatar__gesicht">{gesicht}</span>}
      {besiegt && (
        <span className="avatar__besiegt" data-testid="besiegt">
          <Check size={groesse === 96 ? 20 : 12} strokeWidth={3} />
        </span>
      )}
    </span>
  )
}
