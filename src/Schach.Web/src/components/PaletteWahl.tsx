import { useId, type CSSProperties } from 'react'
import { de, t } from '../i18n/de'
import { PALETTEN, setzePalette, usePalette } from './palette'
import './bausteine.css'

/** Auswahl der Grundfarbe (SCHACH-SPEC 10.3), im Partie-Menue und im Profil. Wirkt sofort. */
export function PaletteWahl() {
  const name = useId()
  const aktiv = usePalette()
  return (
    <fieldset className="palettewahl">
      <legend className="palettewahl__titel">{t('palette.titel')}</legend>
      <div className="palettewahl__reihe">
        {PALETTEN.map((p) => (
          <label key={p.id} className="palettewahl__wahl">
            <input
              type="radio"
              name={name}
              value={p.id}
              checked={aktiv.id === p.id}
              onChange={() => setzePalette(p.id)}
            />
            <span
              className="palettewahl__muster"
              aria-hidden="true"
              style={{ '--muster-brett': p.brett, '--muster-akzent': p.akzent } as CSSProperties}
            />
            <span className="palettewahl__name">{de.palette.namen[p.id]}</span>
          </label>
        ))}
      </div>
    </fieldset>
  )
}
