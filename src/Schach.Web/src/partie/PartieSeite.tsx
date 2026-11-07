import { useEffect, useState } from 'react'
import { useParams } from 'react-router'
import { t } from '../i18n/de'
import { Partie } from './Partie'

// Route /partie/:id. Fake-Ids (`fake-…`, nur Entwicklung und Bau-Modus `prototyp`) starten zuerst
// den MSW-Fake, damit auch ein Neuladen gegen ihn laeuft. Im Produktionsbau faellt der Zweig weg.
const FAKE_MOEGLICH = import.meta.env.DEV || import.meta.env.MODE === 'prototyp'

export function PartieSeite() {
  const { id = '' } = useParams()
  const fake = FAKE_MOEGLICH && id.startsWith('fake-')
  const [bereit, setBereit] = useState(!fake)
  const [fehler, setFehler] = useState(false)

  useEffect(() => {
    if (!FAKE_MOEGLICH || !fake) return
    let aktiv = true
    import('../fake/starten')
      .then((m) => m.starteFake())
      .then(() => aktiv && setBereit(true))
      .catch(() => aktiv && setFehler(true))
    return () => {
      aktiv = false
    }
  }, [fake])

  if (fehler) return <p className="partie partie--leer">{t('prototyp.startFehler')}</p>
  if (!bereit) return <p className="partie partie--leer text-muted">{t('partie.laedt')}</p>
  return <Partie key={id} id={id} neuePartiePfad={fake ? '/prototyp' : '/neu'} />
}
