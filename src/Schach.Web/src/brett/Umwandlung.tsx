import { useUeberlage } from '../components/useUeberlage'
import { t } from '../i18n/de'
import type { Umwandlungsfigur } from './auswahl'
import { usePalette } from '../components/palette'
import { FIGUR_JE_SATZ } from './figurensatz'

const AUSWAHL: { figur: Umwandlungsfigur; code: string; name: string }[] = [
  { figur: 'q', code: 'Q', name: t('brett.umwandlungDame') },
  { figur: 'r', code: 'R', name: t('brett.umwandlungTurm') },
  { figur: 'b', code: 'B', name: t('brett.umwandlungLaeufer') },
  { figur: 'n', code: 'N', name: t('brett.umwandlungSpringer') },
]

/** Umwandlungs-Overlay: 4 Kacheln a 88 px (D, T, L, S). Tippen daneben bricht ab (SCHACH-SPEC 10.3). */
export function Umwandlung({
  farbe,
  onWahl,
  onAbbruch,
}: {
  farbe: 'w' | 'b'
  onWahl: (figur: Umwandlungsfigur) => void
  onAbbruch: () => void
}) {
  const flaeche = useUeberlage<HTMLDivElement>(onAbbruch)
  const Figur = FIGUR_JE_SATZ[usePalette().figuren]
  return (
    <div className="umwandlung" data-testid="umwandlung-hintergrund" onClick={onAbbruch}>
      <div
        ref={flaeche}
        tabIndex={-1}
        className="umwandlung__kacheln"
        role="dialog"
        aria-label={t('brett.umwandlungTitel')}
        onClick={(e) => e.stopPropagation()}
      >
        {AUSWAHL.map(({ figur, code, name }) => (
          <button key={figur} type="button" className="umwandlung__kachel" aria-label={name} onClick={() => onWahl(figur)}>
            <Figur code={farbe + code} />
          </button>
        ))}
      </div>
    </div>
  )
}
