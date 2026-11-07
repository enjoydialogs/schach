import { useState, type FormEvent } from 'react'
import { useNavigate, useSearchParams } from 'react-router'
import { api } from '../api/client'
import type { Bedenkzeit, FarbeWahl, NeuePartieAnfrage, ZustandDto } from '../api/types'
import { Seite } from '../components/Seite'
import { t, type TextSchluessel } from '../i18n/de'
import { FAKE_GEGNER } from './fakeServer'
import { fakeServer, starteFake } from './starten'
import './prototyp.css'

// Dev-Route /prototyp (S3a): startet eine Partie gegen den Fake-Server. Voreinstellungen auch per
// Query, z. B. /prototyp?bedenkzeit=Fischer5Plus3&uhr=knapp fuer die Pixel-7-Abnahme.

const STELLUNGEN = {
  start: { text: 'prototyp.stellungStart', fen: undefined },
  umwandlung: { text: 'prototyp.stellungUmwandlung', fen: '4k3/P7/8/8/8/8/7p/4K3 w - - 0 1' },
  ungedeckt: { text: 'prototyp.stellungUngedeckt', fen: 'r3k3/ppp2ppp/8/8/1b6/2N5/P4PPP/R5K1 w q - 0 1' },
  matt: { text: 'prototyp.stellungMatt', fen: '6k1/5ppp/8/8/8/8/8/R5K1 w - - 0 1' },
} satisfies Record<string, { text: TextSchluessel; fen: string | undefined }>
type Stellung = keyof typeof STELLUNGEN

const BEDENKZEITEN: { wert: Bedenkzeit; text: TextSchluessel }[] = [
  { wert: 'OhneUhr', text: 'prototyp.bedenkzeitOhneUhr' },
  { wert: 'Fischer15Plus10', text: 'prototyp.bedenkzeit15Plus10' },
  { wert: 'Fischer10Plus5', text: 'prototyp.bedenkzeit10Plus5' },
  { wert: 'Fischer5Plus3', text: 'prototyp.bedenkzeit5Plus3' },
  { wert: 'JeZug90s', text: 'prototyp.bedenkzeit90s' },
]

function aus<T extends string>(wert: string | null, erlaubt: readonly T[], standard: T): T {
  return erlaubt.includes(wert as T) ? (wert as T) : standard
}

export function Prototyp() {
  const navigate = useNavigate()
  const [query] = useSearchParams()
  const [gegnerId, setGegnerId] = useState(
    aus(query.get('gegner'), FAKE_GEGNER.map((g) => g.key), 'ilse'),
  )
  const [farbe, setFarbe] = useState<FarbeWahl>(aus<FarbeWahl>(query.get('farbe'), ['Weiss', 'Schwarz', 'Zufall'], 'Weiss'))
  const [bedenkzeit, setBedenkzeit] = useState<Bedenkzeit>(
    aus(query.get('bedenkzeit'), BEDENKZEITEN.map((b) => b.wert), 'OhneUhr'),
  )
  const [gast, setGast] = useState(query.get('rolle') === 'gast')
  const [stellung, setStellung] = useState<Stellung>(
    aus(query.get('stellung'), Object.keys(STELLUNGEN) as Stellung[], 'start'),
  )
  const [uhrKnapp, setUhrKnapp] = useState(query.get('uhr') === 'knapp')
  const [fehler, setFehler] = useState(false)
  const [laeuft, setLaeuft] = useState(false)

  const starte = async (e?: FormEvent) => {
    e?.preventDefault()
    setLaeuft(true)
    setFehler(false)
    try {
      await starteFake()
      fakeServer.gast = gast
      // Seit S3b gilt „eine offene Partie je Spieler“ (409): der Prototyp raeumt sie vorher ab.
      fakeServer.gibOffeneAuf()
      fakeServer.vorgaben = {
        startFen: STELLUNGEN[stellung].fen,
        spielerStartMs: uhrKnapp && bedenkzeit !== 'OhneUhr' ? 25_000 : undefined,
      }
      // Die Uebungsstellungen sind fuer Weiss gebaut.
      const anfrage: NeuePartieAnfrage = { gegnerId, farbe: stellung === 'start' ? farbe : 'Weiss', bedenkzeit }
      const zustand = await api.post<ZustandDto>('/partien', anfrage)
      navigate(`/partie/${zustand.id}`)
    } catch {
      setFehler(true)
      setLaeuft(false)
    }
  }

  return (
    <Seite>
      <form className="prototyp" onSubmit={(e) => void starte(e)}>
        <h1 className="wortmarke">{t('prototyp.titel')}</h1>
        <p className="text-muted">{t('prototyp.hinweis')}</p>

        <label>
          {t('prototyp.gegner')}
          <select value={gegnerId} onChange={(e) => setGegnerId(e.target.value)}>
            {FAKE_GEGNER.map((g) => (
              <option key={g.key} value={g.key}>
                {g.name} · {g.elo}
              </option>
            ))}
          </select>
        </label>

        <label>
          {t('prototyp.farbe')}
          <select value={farbe} onChange={(e) => setFarbe(e.target.value as FarbeWahl)}>
            <option value="Weiss">{t('prototyp.farbeWeiss')}</option>
            <option value="Schwarz">{t('prototyp.farbeSchwarz')}</option>
            <option value="Zufall">{t('prototyp.farbeZufall')}</option>
          </select>
        </label>

        <label>
          {t('prototyp.bedenkzeit')}
          <select value={bedenkzeit} onChange={(e) => setBedenkzeit(e.target.value as Bedenkzeit)}>
            {BEDENKZEITEN.map((b) => (
              <option key={b.wert} value={b.wert}>
                {t(b.text)}
              </option>
            ))}
          </select>
        </label>

        <label>
          {t('prototyp.rolle')}
          <select value={gast ? 'gast' : 'spieler'} onChange={(e) => setGast(e.target.value === 'gast')}>
            <option value="spieler">{t('prototyp.rolleSpieler')}</option>
            <option value="gast">{t('prototyp.rolleGast')}</option>
          </select>
        </label>

        <label>
          {t('prototyp.stellung')}
          <select value={stellung} onChange={(e) => setStellung(e.target.value as Stellung)}>
            {(Object.keys(STELLUNGEN) as Stellung[]).map((s) => (
              <option key={s} value={s}>
                {t(STELLUNGEN[s].text)}
              </option>
            ))}
          </select>
        </label>

        <label className="prototyp__haken">
          <input type="checkbox" checked={uhrKnapp} onChange={(e) => setUhrKnapp(e.target.checked)} />
          {t('prototyp.uhrKnapp')}
        </label>

        {fehler && <p className="prototyp__fehler">{t('prototyp.startFehler')}</p>}

        <button className="knopf" type="submit" disabled={laeuft}>
          {t('prototyp.starten')}
        </button>
      </form>
    </Seite>
  )
}
