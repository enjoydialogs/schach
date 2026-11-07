import type { ZustandDto } from '../api/types'
import { de, t } from '../i18n/de'
import { fuelle } from '../partie/text'
import { Dialog } from './Dialog'

/** Dialog „Fortsetzen oder aufgeben?“ (SCHACH-SPEC 10.5), auf Start und Neu. */
export function OffenePartieDialog({
  partie,
  gast,
  beschaeftigt,
  fehler,
  onFortsetzen,
  onAufgeben,
  onAbbrechen,
}: {
  partie: ZustandDto
  gast: boolean
  beschaeftigt: boolean
  fehler: string | null
  onFortsetzen: () => void
  onAufgeben: () => void
  onAbbrechen: () => void
}) {
  return (
    <Dialog
      titel={fuelle(de.offenePartie.titel, { name: partie.gegner.name })}
      onSchliessen={onAbbrechen}
      senkrecht
      knoepfe={
        <>
          <button type="button" className="knopf" onClick={onFortsetzen} disabled={beschaeftigt}>
            {t('offenePartie.fortsetzen')}
          </button>
          <button type="button" className="knopf knopf--gefahr" onClick={onAufgeben} disabled={beschaeftigt}>
            {t('offenePartie.aufgebenUndNeu')}
          </button>
          <button type="button" className="knopf knopf--text" onClick={onAbbrechen}>
            {t('offenePartie.abbrechen')}
          </button>
        </>
      }
    >
      <p>
        {t('offenePartie.text')} {gast ? t('offenePartie.gast') : t('offenePartie.spieler')}
      </p>
      {fehler !== null && <p className="fehlertext">{fehler}</p>}
    </Dialog>
  )
}
