import { useCallback, useEffect, useMemo, useState } from 'react'
import { Lightbulb, List, RotateCcw, X } from 'lucide-react'
import { Link, useNavigate, useParams } from 'react-router'
import { api } from '../api/client'
import type { GegnerDto } from '../api/types'
import { Brett } from '../brett/Brett'
import { BottomSheet, Schalter } from '../components/BottomSheet'
import { Fehlerkarte } from '../components/Fehlerkarte'
import { PaletteWahl } from '../components/PaletteWahl'
import { de, t } from '../i18n/de'
import { schachFeld } from '../partie/anzeige'
import { useEinstellungen } from '../partie/einstellungen'
import { EigeneZeile, Kopfzeile, Spruchblase, Statuszeile, type StatusArt } from '../partie/teile'
import { fuelle } from '../partie/text'
import { spieleTon } from '../partie/toene'
import { erlaubteZuege, gegnerZieht, loesung, naechsterTipp, nimmZurueck, spielerZieht, starte, type Stand } from './ablauf'
import { findeUebung, type Uebung } from './katalog'
import { mitStimme } from './stimmen'
import '../partie/partie.css'
import './uebung.css'

/** Wartezeit, bevor der Gegner zieht, damit der Spieler die Erklaerung lesen kann. */
export const GEGNER_PAUSE_MS = 900
/** So lange bleibt ein anderer Zug stehen, bevor er zurueckgenommen wird. */
export const FALSCH_PAUSE_MS = 1400

/** Zufaelliger Gegner aus `GET /gegner`, einmal je Aufruf der Seite. */
function useZufallsGegner(): { gegner: GegnerDto | null; fehler: boolean; erneut: () => void } {
  const [gegner, setGegner] = useState<GegnerDto | null>(null)
  const [fehler, setFehler] = useState(false)
  const lade = useCallback(() => {
    api.get<GegnerDto[]>('/gegner').then(
      (liste) => {
        if (liste.length === 0) setFehler(true)
        else setGegner(liste[Math.floor(Math.random() * liste.length)]!)
      },
      () => setFehler(true),
    )
  }, [])
  useEffect(lade, [lade])
  const erneut = () => {
    setFehler(false)
    lade()
  }
  return { gegner, fehler, erneut }
}

/** Route `/uebungen/:key` (SCHACH-SPEC 10.16). */
export function UebungSeite() {
  const { key = '' } = useParams()
  const uebung = findeUebung(key)
  const { gegner, fehler, erneut } = useZufallsGegner()

  if (uebung === undefined) {
    return (
      <div className="partie partie--leer">
        <p>{t('uebungen.nichtGefunden')}</p>
        <Link className="knopf" to="/uebungen">
          {t('uebungen.knopfListe')}
        </Link>
      </div>
    )
  }
  if (fehler) {
    return (
      <div className="partie partie--leer">
        <Fehlerkarte onErneut={erneut} />
      </div>
    )
  }
  if (gegner === null) return <p className="partie partie--leer text-muted">{t('uebungen.laedt')}</p>
  return <UebungSpiel key={uebung.key} uebung={uebung} gegner={gegner} />
}

type Offen = 'keins' | 'menue'

/** Uebung im Layout `Fest`: Kopf mit Gegner und Spruchblase, Brett, Statuszeile, Knopfleiste. Die Texte sprechen im Ton des Gegners (`stimmen.ts`). */
export function UebungSpiel({ uebung: neutral, gegner }: { uebung: Uebung; gegner: GegnerDto }) {
  // Alle Texte im Ton der Persoenlichkeit des Gegners; ab hier nur noch diese Fassung verwenden.
  const uebung = useMemo(() => mitStimme(neutral, gegner.persoenlichkeit), [neutral, gegner.persoenlichkeit])
  const navigate = useNavigate()
  const [einstellungen, setzeEinstellung] = useEinstellungen()
  const [stand, setStand] = useState<Stand>(() => starte(uebung))
  const [offen, setOffen] = useState<Offen>('keins')
  const [endeZu, setEndeZu] = useState(false)

  const ton = useCallback(
    (art: 'zug' | 'sieg') => {
      if (einstellungen.toeneUndVibration) spieleTon(art, einstellungen.toeneLautstaerke)
    },
    [einstellungen.toeneUndVibration, einstellungen.toeneLautstaerke],
  )

  // Gegnerzug und Ruecknahme laufen mit Pause, damit der Spieler mitlesen kann.
  useEffect(() => {
    if (stand.phase === 'gegner') {
      const zeit = setTimeout(() => {
        setStand((s) => gegnerZieht(s, uebung))
        ton('zug')
      }, GEGNER_PAUSE_MS)
      return () => clearTimeout(zeit)
    }
    if (stand.phase === 'falsch') {
      const zeit = setTimeout(() => setStand((s) => nimmZurueck(s)), FALSCH_PAUSE_MS)
      return () => clearTimeout(zeit)
    }
    if (stand.phase === 'fertig') ton('sieg')
    return undefined
  }, [stand.phase, uebung, ton])

  const amZug = stand.phase === 'amZug'
  const zuege = useMemo(() => (amZug ? erlaubteZuege(stand.fen) : []), [amZug, stand.fen])
  const schach = useMemo(() => schachFeld(stand.fen), [stand.fen])
  const tipp = amZug && stand.tipp > 0 ? loesung(stand, uebung) : null
  const schritt = uebung.schritte[stand.schritt]

  const spruch =
    stand.schritt === 0 && stand.text === uebung.einleitung
      ? fuelle(de.uebungen.begruessung, { name: gegner.name, text: uebung.einleitung })
      : (stand.text ?? de.uebungen.falsch)

  let status: { art: StatusArt; text: string } = { art: 'leer', text: '' }
  if (stand.phase === 'falsch') status = { art: 'warnung', text: de.uebungen.statusFalsch }
  else if (stand.phase === 'gegner') status = { art: 'denkt', text: fuelle(de.uebungen.gegnerZieht, { name: gegner.name }) }
  else if (tipp !== null && stand.tipp === 1) status = { art: 'tipp', text: de.tipp.stufe1[tipp.figur] }
  else if (tipp !== null) status = { art: 'tipp', text: fuelle(de.tipp.stufe2, { figur: de.tipp.figur[tipp.figur], von: tipp.von, nach: tipp.nach }) }
  else if (amZug && schritt !== undefined) status = { art: 'hinweis', text: schritt.aufgabe }

  const neuStarten = () => {
    setOffen('keins')
    setEndeZu(false)
    setStand(starte(uebung))
  }

  return (
    <div className="partie uebung">
      <Kopfzeile
        gegner={gegner}
        amZug={stand.phase === 'gegner'}
        uhr={null}
        spruch={<Spruchblase text={spruch} />}
        onMenue={() => setOffen('menue')}
      />
      <div className="partie__brett">
        <Brett
          fen={stand.fen}
          orientierung={uebung.farbe}
          erlaubteZuege={zuege}
          eingabeAktiv={amZug}
          zeigeZiele={einstellungen.erlaubteZuegeAnzeigen}
          letzterZug={stand.letzterZug}
          schach={schach}
          tippFeld={tipp?.von ?? null}
          tippPfeil={tipp !== null && stand.tipp === 2 ? { von: tipp.von, nach: tipp.nach } : null}
          onZug={(uci) => {
            setStand((s) => spielerZieht(s, uebung, uci))
            ton('zug')
          }}
        />
      </div>
      <Statuszeile art={status.art} text={status.text} />
      <EigeneZeile
        farbe={uebung.farbe}
        amZug={amZug}
        pille={fuelle(de.uebungen.schritt, {
          nr: Math.min(stand.schritt + 1, uebung.schritte.length),
          von: uebung.schritte.length,
        })}
        uhr={null}
      />
      <nav className="partie__knoepfe">
        <button
          type="button"
          className="leistenknopf leistenknopf--leise"
          aria-disabled={amZug ? undefined : true}
          onClick={() => setStand((s) => naechsterTipp(s))}
        >
          <Lightbulb aria-hidden="true" size={22} />
          <span>{t('uebungen.knopfTipp')}</span>
        </button>
        <button type="button" className="leistenknopf leistenknopf--leise" onClick={neuStarten}>
          <RotateCcw aria-hidden="true" size={22} />
          <span>{t('uebungen.knopfNeu')}</span>
        </button>
        <button type="button" className="leistenknopf leistenknopf--leise" onClick={() => navigate('/uebungen')}>
          <List aria-hidden="true" size={22} />
          <span>{t('uebungen.knopfListe')}</span>
        </button>
      </nav>

      {offen === 'menue' && (
        <BottomSheet
          titel={t('menue.titel')}
          onSchliessen={() => setOffen('keins')}
          kopf={
            <button type="button" className="symbolknopf" aria-label={t('menue.schliessen')} onClick={() => setOffen('keins')}>
              <X aria-hidden="true" />
            </button>
          }
        >
          <Schalter
            text={t('menue.erlaubteZuegeAnzeigen')}
            an={einstellungen.erlaubteZuegeAnzeigen}
            onWechsel={(an) => setzeEinstellung('erlaubteZuegeAnzeigen', an)}
          />
          <Schalter
            text={t('menue.toeneUndVibration')}
            an={einstellungen.toeneUndVibration}
            onWechsel={(an) => setzeEinstellung('toeneUndVibration', an)}
          />
          <PaletteWahl />
          <button type="button" className="sheet__zeile" onClick={() => navigate('/uebungen')}>
            {t('uebungen.verlassen')}
          </button>
        </BottomSheet>
      )}

      {stand.phase === 'fertig' && !endeZu && offen === 'keins' && (
        <BottomSheet titel={t('uebungen.fertigTitel')} onSchliessen={() => setEndeZu(true)}>
          <p className="ende__satz">{uebung.abschluss}</p>
          <div className="ende__knoepfe">
            <button type="button" className="knopf" onClick={() => navigate('/uebungen')}>
              {t('uebungen.andereUebung')}
            </button>
            <button type="button" className="knopf knopf--outline" onClick={neuStarten}>
              {t('uebungen.nochmal')}
            </button>
          </div>
          <Link className="ende__start" to="/">
            {t('partieende.zurStartseite')}
          </Link>
        </BottomSheet>
      )}
    </div>
  )
}
