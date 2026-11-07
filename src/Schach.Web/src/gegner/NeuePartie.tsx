import { useCallback, useEffect, useState, type FormEvent } from 'react'
import { useNavigate } from 'react-router'
import { api, ApiFehler } from '../api/client'
import type { Bedenkzeit, FarbeWahl, GegnerDto, NeuePartieAnfrage, ZustandDto } from '../api/types'
import { BottomSheet } from '../components/BottomSheet'
import { Fehlerkarte } from '../components/Fehlerkarte'
import { GegnerZeile } from '../components/GegnerZeile'
import { Ladeanzeige } from '../components/Ladeanzeige'
import { useOnline } from '../components/online'
import { gibPartieAuf, ladeOffenePartie } from '../components/offenePartie'
import { OffenePartieDialog } from '../components/OffenePartieDialog'
import { Seite } from '../components/Seite'
import { gastPartieId } from '../components/SpeicherSchluessel'
import { useSitzung } from '../components/sitzung'
import { de, t } from '../i18n/de'
import { leseEinstellungen } from '../partie/einstellungen'
import { audioKontext } from '../partie/toene'
import { fuelle } from '../partie/text'

const FARBEN: { wert: FarbeWahl; text: string }[] = [
  { wert: 'Weiss', text: de.neu.weiss },
  { wert: 'Schwarz', text: de.neu.schwarz },
  { wert: 'Zufall', text: de.neu.zufall },
]

// Jede Kurzform bekommt einen Untertitel: „15+10“ setzt Schachwissen voraus.
const BEDENKZEITEN: { wert: Bedenkzeit; untertitel: string }[] = [
  { wert: 'OhneUhr', untertitel: de.neu.ohneUhrUntertitel },
  { wert: 'Fischer15Plus10', untertitel: de.neu.fischer15Plus10Untertitel },
  { wert: 'Fischer10Plus5', untertitel: de.neu.fischer10Plus5Untertitel },
  { wert: 'Fischer5Plus3', untertitel: de.neu.fischer5Plus3Untertitel },
  { wert: 'JeZug90s', untertitel: de.neu.jeZugUntertitel },
]

/** Fehlertext fuer `POST /partien` (10.6). */
function startFehler(e: unknown): string {
  if (e instanceof ApiFehler && e.status === 429) return de.neu.zuViele
  if (e instanceof ApiFehler && e.fehler === 'GeradeVielLos') return de.neu.geradeVielLos
  return de.allgemein.fehler
}

/** Neue Partie (`/neu`, SCHACH-SPEC 10.6): 8 Gegnerzeilen, Tippen oeffnet das Bottom-Sheet. */
export function NeuePartie() {
  const navigate = useNavigate()
  const { status } = useSitzung()
  const online = useOnline()
  const gast = status === 'gast'
  const [gegner, setGegner] = useState<GegnerDto[] | null>(null)
  const [ladeFehler, setLadeFehler] = useState(false)
  const [gewaehlt, setGewaehlt] = useState<GegnerDto | null>(null)
  const [farbe, setFarbe] = useState<FarbeWahl>('Zufall')
  const [bedenkzeit, setBedenkzeit] = useState<Bedenkzeit>('OhneUhr')
  const [beschaeftigt, setBeschaeftigt] = useState(false)
  const [fehler, setFehler] = useState<string | null>(null)
  const [offen, setOffen] = useState<ZustandDto | null>(null)

  const lade = useCallback(() => {
    api.get<GegnerDto[]>('/gegner').then(
      (liste) => setGegner([...liste].sort((a, b) => a.elo - b.elo)),
      () => setLadeFehler(true),
    )
  }, [])
  useEffect(lade, [lade])

  const waehle = (g: GegnerDto) => {
    setGewaehlt(g)
    setFarbe('Zufall')
    setBedenkzeit('OhneUhr')
    setFehler(null)
  }

  const starte = async (): Promise<void> => {
    if (gewaehlt === null) return
    const anfrage: NeuePartieAnfrage = { gegnerId: gewaehlt.key, farbe, bedenkzeit }
    const z = await api.post<ZustandDto>('/partien', anfrage)
    if (gast) gastPartieId.setze(z.id)
    // State: der Partie-Bildschirm liest den ersten Spruch vor (6.9).
    navigate(`/partie/${z.id}`, { replace: true, state: { neuGestartet: true } })
  }

  const absenden = async (e: FormEvent) => {
    e.preventDefault()
    // Der Absenden-Tipp ist die Geste, die den AudioContext fuer den ersten Spruch freigibt.
    if (leseEinstellungen().spruecheVorlesen) audioKontext()
    setBeschaeftigt(true)
    setFehler(null)
    try {
      // Gaeste bekommen vom Server kein 409; ihre laufende Partie kennt nur der Client (10.5).
      const gastOffen = gast ? await ladeOffenePartie(true) : null
      if (gastOffen !== null) {
        setOffen(gastOffen)
        setBeschaeftigt(false)
        return
      }
      await starte()
    } catch (fehlerAntwort) {
      if (fehlerAntwort instanceof ApiFehler && fehlerAntwort.fehler === 'OffenePartie') {
        const id = fehlerAntwort.antwort?.offenePartieId
        const z =
          fehlerAntwort.antwort?.zustand ?? (id ? await api.get<ZustandDto>(`/partien/${id}`).catch(() => null) : null)
        if (z !== null) setOffen(z)
        else setFehler(de.allgemein.fehler)
      } else {
        setFehler(startFehler(fehlerAntwort))
      }
      setBeschaeftigt(false)
    }
  }

  const aufgebenUndStarten = async () => {
    if (offen === null) return
    setBeschaeftigt(true)
    setFehler(null)
    try {
      await gibPartieAuf(offen)
      await starte()
    } catch (e) {
      setOffen(null)
      setFehler(startFehler(e))
      setBeschaeftigt(false)
    }
  }

  return (
    <Seite titel={t('neu.titel')}>
      {ladeFehler ? (
        <Fehlerkarte
          onErneut={() => {
            setLadeFehler(false)
            lade()
          }}
        />
      ) : gegner === null ? (
        <Ladeanzeige skelette={8} />
      ) : (
        <ul className="liste">
          {gegner.map((g) => (
            <li key={g.key}>
              <GegnerZeile gegner={g} gast={gast} onWaehlen={() => waehle(g)} />
            </li>
          ))}
        </ul>
      )}

      {gewaehlt !== null && offen === null && (
        <BottomSheet
          titel={fuelle(de.neu.sheetTitel, { name: gewaehlt.name })}
          onSchliessen={() => !beschaeftigt && setGewaehlt(null)}
        >
          <form onSubmit={(e) => void absenden(e)}>
            <fieldset className="gruppe">
              <legend>{t('neu.farbe')}</legend>
              <div className="segmente">
                {FARBEN.map((f) => (
                  <label key={f.wert} className="segment">
                    <input
                      type="radio"
                      name="farbe"
                      value={f.wert}
                      checked={farbe === f.wert}
                      onChange={() => setFarbe(f.wert)}
                    />
                    {f.text}
                  </label>
                ))}
              </div>
            </fieldset>
            <fieldset className="gruppe">
              <legend>{t('neu.bedenkzeit')}</legend>
              {BEDENKZEITEN.map((b) => (
                <label key={b.wert} className="radiozeile">
                  <input
                    type="radio"
                    name="bedenkzeit"
                    value={b.wert}
                    checked={bedenkzeit === b.wert}
                    onChange={() => setBedenkzeit(b.wert)}
                    aria-labelledby={`bedenkzeit-${b.wert}`}
                    aria-describedby={`bedenkzeit-${b.wert}-untertitel`}
                  />
                  <span className="radiozeile__punkt" aria-hidden="true" />
                  <span className="radiozeile__text">
                    <span id={`bedenkzeit-${b.wert}`}>{de.bedenkzeit[b.wert]}</span>
                    <span id={`bedenkzeit-${b.wert}-untertitel`} className="zeile__klein">
                      {b.untertitel}
                    </span>
                  </span>
                </label>
              ))}
              <p className="zeile__klein">{t('neu.mitUhrHinweis')}</p>
            </fieldset>
            {fehler !== null && (
              <p className="fehlertext" role="alert">
                {fehler}
              </p>
            )}
            {!online && <p className="text-muted">{t('offline.spielenBrauchtInternet')}</p>}
            <button type="submit" className="knopf knopf--breit" disabled={beschaeftigt || !online}>
              {t('neu.starten')}
            </button>
          </form>
        </BottomSheet>
      )}

      {offen !== null && (
        <OffenePartieDialog
          partie={offen}
          gast={gast}
          beschaeftigt={beschaeftigt}
          fehler={null}
          onFortsetzen={() => navigate(`/partie/${offen.id}`, { replace: true })}
          onAufgeben={() => void aufgebenUndStarten()}
          onAbbrechen={() => setOffen(null)}
        />
      )}
    </Seite>
  )
}
