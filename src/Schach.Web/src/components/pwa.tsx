import { useEffect, useState } from 'react'
import { t } from '../i18n/de'
import {
  hatBeendetePartie,
  installHinweis,
  installiere,
  istStandalone,
  useInstallPrompt,
  zeigeInstallKarte,
} from '../pwa/install'
import { neuLaden, useNeueVersion } from '../pwa/update'
import '../pwa/pwa.css'

// PWA-Bausteine (SCHACH-SPEC 10.13, S3d). Registrierung, Install-Ereignis und Update-Status leben
// in src/pwa/ (Modul-Speicher, ab App-Start); diese Komponenten lesen nur.

/** Hinweis „Neue Version verfügbar“ plus „Neu laden“, nur in `Seite`-Layouts. */
export function UpdateHinweis() {
  const neu = useNeueVersion()
  if (!neu) return null
  return (
    <div className="update" role="status">
      <span>{t('pwa.neueVersion')}</span>
      <button type="button" className="knopf knopf--text update__knopf" onClick={neuLaden}>
        {t('pwa.neuLaden')}
      </button>
    </div>
  )
}

/** Install-Karte, nur auf Start und nach der Regel aus 10.13. */
export function InstallHinweis({ jetzt = Date.now }: { jetzt?: () => number } = {}) {
  const prompt = useInstallPrompt()
  const [beendetePartie, setBeendetePartie] = useState(false)
  const [spaeterAm, setSpaeterAm] = useState(() => installHinweis.lies())
  const lage = { promptGefangen: prompt !== null, standalone: istStandalone(), spaeterAm, jetzt: jetzt() }
  // Die Partienliste nur fragen, wenn alles andere schon passt.
  const vorab = zeigeInstallKarte({ ...lage, beendetePartie: true })

  useEffect(() => {
    if (!vorab) return
    let aktiv = true
    void hatBeendetePartie().then((ja) => {
      if (aktiv) setBeendetePartie(ja)
    })
    return () => {
      aktiv = false
    }
  }, [vorab])

  if (!zeigeInstallKarte({ ...lage, beendetePartie })) return null
  return (
    <section className="karte" aria-labelledby="install-titel">
      <h2 id="install-titel" className="karte__titel">
        {t('pwa.installTitel')}
      </h2>
      <p className="karte__text">{t('pwa.installText')}</p>
      <div className="karte__knoepfe">
        <button type="button" className="knopf" onClick={() => void installiere()}>
          {t('pwa.installieren')}
        </button>
        <button
          type="button"
          className="knopf knopf--outline"
          onClick={() => {
            const nun = jetzt()
            installHinweis.setze(nun)
            setSpaeterAm(new Date(nun).toISOString())
          }}
        >
          {t('pwa.spaeter')}
        </button>
      </div>
    </section>
  )
}
