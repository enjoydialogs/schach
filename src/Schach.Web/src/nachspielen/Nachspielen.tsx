import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { ChevronFirst, ChevronLast, ChevronLeft, ChevronRight, Ellipsis, type LucideIcon } from 'lucide-react'
import { Link, Navigate, useNavigate, useParams } from 'react-router'
import { api, ApiFehler } from '../api/client'
import type { ZustandDto } from '../api/types'
import { Brett } from '../brett/Brett'
import { Avatar } from '../components/Avatar'
import { BottomSheet } from '../components/BottomSheet'
import { teileOderLadeHerunter } from '../components/datei'
import { Fehlerkarte } from '../components/Fehlerkarte'
import { Ladeanzeige } from '../components/Ladeanzeige'
import { mitCache, partienCache } from '../components/partienCache'
import { Seite } from '../components/Seite'
import { de, t } from '../i18n/de'
import { schachFeld } from '../partie/anzeige'
import { erklaerung, grundLabel, sichtAuf } from '../partie/erklaerung'
import { Spruchblase, Statuszeile } from '../partie/teile'
import { fuelle } from '../partie/text'
import { pgnDateiname, spruchAn, stellungen, zugText } from './zugfolge'
import '../partie/partie.css'
import './nachspielen.css'

type Lade = { art: 'laedt' } | { art: 'bereit'; zustand: ZustandDto } | { art: 'fehler'; offline: boolean; nichtGefunden: boolean }

const ERGEBNIS = { sieg: de.partien.sieg, remis: de.partien.remis, niederlage: de.partien.niederlage }

const KNOEPFE: { art: 'anfang' | 'zurueck' | 'vor' | 'ende'; text: string; Icon: LucideIcon }[] = [
  { art: 'anfang', text: de.nachspielen.knopfAnfang, Icon: ChevronFirst },
  { art: 'zurueck', text: de.nachspielen.knopfZurueck, Icon: ChevronLeft },
  { art: 'vor', text: de.nachspielen.knopfVor, Icon: ChevronRight },
  { art: 'ende', text: de.nachspielen.knopfEnde, Icon: ChevronLast },
]

/** Nachspielen (`/nachspielen/:id`, SCHACH-SPEC 10.8): nur Beendet; Offen → `/partie/:id`. */
export function Nachspielen() {
  const { id = '' } = useParams()
  const [lade, setLade] = useState<Lade>({ art: 'laedt' })

  const hole = useCallback(() => {
    mitCache(
      () => api.get<ZustandDto>(`/partien/${encodeURIComponent(id)}`),
      partienCache.merkePartie,
      () => partienCache.partie(id),
    ).then(
      ({ wert }) => setLade({ art: 'bereit', zustand: wert }),
      (e: unknown) =>
        setLade({
          art: 'fehler',
          offline: !(e instanceof ApiFehler),
          nichtGefunden: e instanceof ApiFehler && (e.status === 404 || e.status === 403),
        }),
    )
  }, [id])
  useEffect(hole, [hole])

  if (lade.art === 'bereit' && lade.zustand.status === 'Offen') return <Navigate to={`/partie/${id}`} replace />
  if (lade.art === 'bereit') return <Ansicht key={id} zustand={lade.zustand} />
  return (
    <Seite titel={t('partie.knopfNachspielen')}>
      {lade.art === 'laedt' ? (
        <Ladeanzeige />
      ) : lade.offline ? (
        <div className="leer">
          <p>{t('nachspielen.offline')}</p>
          <Link className="knopf knopf--outline" to="/partien">
            {t('nachspielen.zurListe')}
          </Link>
        </div>
      ) : lade.nichtGefunden ? (
        <div className="leer">
          <p>{t('partie.nichtGefunden')}</p>
          <Link className="knopf knopf--outline" to="/partien">
            {t('nachspielen.zurListe')}
          </Link>
        </div>
      ) : (
        <Fehlerkarte
          onErneut={() => {
            setLade({ art: 'laedt' })
            hole()
          }}
        />
      )}
    </Seite>
  )
}

/** Layout `Fest` wie die Partie: Kopf, Spruch, Brett ohne Eingabe, Status, Zugleiste, Knoepfe. */
function Ansicht({ zustand: z }: { zustand: ZustandDto }) {
  const navigate = useNavigate()
  const liste = useMemo(() => stellungen(z.zugfolge), [z.zugfolge])
  const letzter = liste.length - 1
  const [halbzug, setHalbzug] = useState(0)
  const [menue, setMenue] = useState(false)
  const leiste = useRef<HTMLOListElement>(null)

  const stellung = liste[halbzug]!
  const sicht = z.ergebnis !== null ? sichtAuf(z.ergebnis, z.spielerFarbe) : null
  const spruch = spruchAn(z.sprueche, halbzug)
  const schach = useMemo(() => schachFeld(stellung.fen), [stellung.fen])

  // Aktiven Chip sichtbar halten (jsdom kennt scrollIntoView nicht).
  useEffect(() => {
    const chip = leiste.current?.querySelector<HTMLElement>('[aria-current="true"]')
    chip?.scrollIntoView?.({ block: 'nearest', inline: 'center' })
  }, [halbzug])

  let status: string
  if (halbzug === 0) status = de.nachspielen.grundstellung
  else if (halbzug === letzter && z.grund !== null && sicht !== null) {
    const satz = erklaerung(z.grund, sicht, z.gegner.name)
    status = satz === null ? grundLabel(z.grund) : fuelle(de.nachspielen.ende, { grund: grundLabel(z.grund), satz })
  } else status = zugText(z.zugfolge[halbzug - 1]!)

  const springe = (art: (typeof KNOEPFE)[number]['art']) => {
    if (art === 'anfang') setHalbzug(0)
    else if (art === 'zurueck') setHalbzug((h) => Math.max(0, h - 1))
    else if (art === 'vor') setHalbzug((h) => Math.min(letzter, h + 1))
    else setHalbzug(letzter)
  }

  const teile = () => {
    setMenue(false)
    if (z.pgn !== null) void teileOderLadeHerunter(z.pgn, pgnDateiname(z), 'application/x-chess-pgn')
  }

  return (
    <div className="partie">
      <header className="partie__kopf">
        <Avatar gegner={z.gegner} />
        <h1 className="partie__gegner nachspielen__titel">
          {fuelle(de.nachspielen.kopf, { name: z.gegner.name, ergebnis: sicht !== null ? ERGEBNIS[sicht] : '' })}
        </h1>
        <button type="button" className="symbolknopf" aria-label={t('partie.menue')} onClick={() => setMenue(true)}>
          <Ellipsis aria-hidden="true" />
        </button>
      </header>
      <Spruchblase text={spruch?.text ?? null} />
      <div className="partie__brett">
        <Brett
          fen={stellung.fen}
          orientierung={z.spielerFarbe}
          erlaubteZuege={[]}
          eingabeAktiv={false}
          zeigeZiele={false}
          letzterZug={stellung.letzterZug}
          schach={schach}
          onZug={() => {}}
        />
      </div>
      <Statuszeile art={halbzug === letzter && halbzug > 0 ? 'hinweis' : 'leer'} text={status} />
      <ol className="zugleiste" ref={leiste} aria-label={t('nachspielen.zuege')}>
        {z.zugfolge.slice(0, letzter).map((zug) => (
          <li key={zug.halbzug}>
            <button
              type="button"
              className="zugchip"
              aria-current={zug.halbzug === halbzug ? 'true' : undefined}
              onClick={() => setHalbzug(zug.halbzug)}
            >
              {zug.seite === 'Weiss' ? zugText(zug) : zugText(zug).replace(/^\d+\. … /, '')}
            </button>
          </li>
        ))}
      </ol>
      <nav className="partie__knoepfe">
        {KNOEPFE.map(({ art, text, Icon }) => (
          <button
            key={art}
            type="button"
            className="leistenknopf"
            disabled={art === 'anfang' || art === 'zurueck' ? halbzug === 0 : halbzug === letzter}
            onClick={() => springe(art)}
          >
            <Icon aria-hidden="true" size={22} />
            <span>{text}</span>
          </button>
        ))}
      </nav>

      {menue && (
        <BottomSheet titel={t('menue.titel')} onSchliessen={() => setMenue(false)}>
          <button type="button" className="sheet__zeile" onClick={teile} disabled={z.pgn === null}>
            {t('nachspielen.pgnTeilen')}
          </button>
          <button type="button" className="sheet__zeile" onClick={() => navigate('/partien')}>
            {t('nachspielen.zurListe')}
          </button>
          <button type="button" className="sheet__zeile" onClick={() => navigate('/')}>
            {t('allgemein.zurStartseite')}
          </button>
        </BottomSheet>
      )}
    </div>
  )
}
