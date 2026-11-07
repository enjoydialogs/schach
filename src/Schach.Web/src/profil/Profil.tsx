import { useEffect, useState } from 'react'
import { useNavigate } from 'react-router'
import { api } from '../api/client'
import type { AbmeldenAnfrage, GegnerDto, KontoExportDto } from '../api/types'
import { Avatar } from '../components/Avatar'
import { Schalter } from '../components/BottomSheet'
import { isoDatum, ladeHerunter } from '../components/datei'
import { Dialog } from '../components/Dialog'
import { Bilanz } from '../components/GegnerZeile'
import { zeigeHinweis } from '../components/hinweisSpeicher'
import { Ladeanzeige } from '../components/Ladeanzeige'
import { leerePartienCache } from '../components/partienCache'
import { Seite } from '../components/Seite'
import { gastPartieId } from '../components/SpeicherSchluessel'
import { ladeSitzung, useSitzung } from '../components/sitzung'
import { de, t } from '../i18n/de'
import { useEinstellungen } from '../partie/einstellungen'
import './profil.css'

/**
 * Profil (`/profil`, SCHACH-SPEC 10.9): E-Mail, Elo, Bilanz je Gegner, die drei Schalter,
 * Export, Abmelden, Konto loeschen. „Spieler seit …“ fehlt: `SpielerDto` traegt kein Anlagedatum.
 */
export function Profil() {
  const navigate = useNavigate()
  const { spieler } = useSitzung()
  const [einstellungen, setzeEinstellung] = useEinstellungen()
  const [gegner, setGegner] = useState<GegnerDto[] | null>(null)
  const [loeschenFrage, setLoeschenFrage] = useState(false)
  const [beschaeftigt, setBeschaeftigt] = useState(false)
  const [fehler, setFehler] = useState<string | null>(null)

  useEffect(() => {
    void ladeSitzung()
    api.get<GegnerDto[]>('/gegner').then(
      (liste) => setGegner([...liste].sort((a, b) => a.elo - b.elo)),
      () => setGegner([]),
    )
  }, [])

  /** Nach Abmelden oder Loeschen: Cache leeren, Sitzung neu, nach `/`, Toast (10.9). */
  const danach = async (hinweis: string) => {
    await leerePartienCache()
    gastPartieId.setze(null)
    await ladeSitzung()
    navigate('/', { replace: true })
    zeigeHinweis(hinweis)
  }

  const aktion = async (arbeit: () => Promise<unknown>, hinweis: string) => {
    setBeschaeftigt(true)
    setFehler(null)
    try {
      await arbeit()
      await danach(hinweis)
    } catch {
      setFehler(de.allgemein.fehler)
      setBeschaeftigt(false)
      setLoeschenFrage(false)
    }
  }

  const exportiere = async () => {
    setFehler(null)
    try {
      const daten = await api.get<KontoExportDto>('/konto/export')
      ladeHerunter(JSON.stringify(daten, null, 2), `schach-export-${isoDatum(Date.now())}.json`, 'application/json')
    } catch {
      setFehler(de.allgemein.fehler)
    }
  }

  return (
    <Seite titel={t('profil.titel')}>
      <section className="karte profil__konto">
        <span className="profil__email">{spieler?.email}</span>
        <span className="profil__elo-label">{t('profil.elo')}</span>
        <span className="profil__elo" data-testid="profil-elo">
          {spieler?.elo}
        </span>
      </section>

      <h2 className="abschnitt">{t('profil.bilanz')}</h2>
      {gegner === null ? (
        <Ladeanzeige skelette={3} />
      ) : (
        <ul className="liste" aria-label={t('profil.bilanz')}>
          {gegner.map((g) => (
            <li key={g.key} className="zeile">
              <Avatar gegner={g} besiegt={g.bilanz !== null && g.bilanz.besiegtAm !== null} />
              <span className="zeile__mitte">
                <span className="zeile__titel">{g.name}</span>
                <span className="zeile__klein">{de.stufe[g.stufe]}</span>
              </span>
              {g.bilanz !== null && <Bilanz bilanz={g.bilanz} />}
            </li>
          ))}
        </ul>
      )}

      <h2 className="abschnitt">{t('profil.einstellungen')}</h2>
      <div className="karte profil__schalter">
        <Schalter
          text={t('menue.erlaubteZuegeAnzeigen')}
          an={einstellungen.erlaubteZuegeAnzeigen}
          onWechsel={(an) => setzeEinstellung('erlaubteZuegeAnzeigen', an)}
        />
        <Schalter
          text={t('menue.vorUngedecktenWarnen')}
          an={einstellungen.vorUngedecktenWarnen}
          onWechsel={(an) => setzeEinstellung('vorUngedecktenWarnen', an)}
        />
        <Schalter
          text={t('menue.toeneUndVibration')}
          an={einstellungen.toeneUndVibration}
          onWechsel={(an) => setzeEinstellung('toeneUndVibration', an)}
        />
      </div>

      <div className="profil__aktionen">
        {fehler !== null && (
          <p className="fehlertext" role="alert">
            {fehler}
          </p>
        )}
        <button type="button" className="knopf knopf--outline knopf--breit" onClick={() => void exportiere()}>
          {t('profil.export')}
        </button>
        <button
          type="button"
          className="knopf knopf--outline knopf--breit"
          disabled={beschaeftigt}
          onClick={() => void aktion(() => api.post('/auth/abmelden', {} satisfies AbmeldenAnfrage), de.profil.abgemeldet)}
        >
          {t('profil.abmelden')}
        </button>
        <button
          type="button"
          className="knopf knopf--text knopf--gefahr-text knopf--breit"
          disabled={beschaeftigt}
          onClick={() => setLoeschenFrage(true)}
        >
          {t('profil.loeschen')}
        </button>
      </div>

      {loeschenFrage && (
        <Dialog
          titel={t('profil.loeschenTitel')}
          onSchliessen={() => !beschaeftigt && setLoeschenFrage(false)}
          knoepfe={
            <>
              <button type="button" className="knopf knopf--outline" onClick={() => setLoeschenFrage(false)} disabled={beschaeftigt}>
                {t('dialog.abbrechen')}
              </button>
              <button
                type="button"
                className="knopf knopf--gefahr"
                disabled={beschaeftigt}
                onClick={() => void aktion(() => api.del('/konto'), de.profil.geloescht)}
              >
                {t('profil.loeschenKnopf')}
              </button>
            </>
          }
        >
          {t('profil.loeschenText')}
        </Dialog>
      )}
    </Seite>
  )
}
