import { useCallback, useEffect, useState } from 'react'
import { Link } from 'react-router'
import { api } from '../api/client'
import type { GegnerDto, PartieZeileDto, PartienListeDto } from '../api/types'
import { Avatar } from '../components/Avatar'
import { kurzDatum } from '../components/datei'
import { Fehlerkarte } from '../components/Fehlerkarte'
import { Ladeanzeige } from '../components/Ladeanzeige'
import { mitCache, partienCache } from '../components/partienCache'
import { Seite } from '../components/Seite'
import { de, t } from '../i18n/de'
import { eloAenderung, sichtAuf, type Sicht } from '../partie/erklaerung'
import { fuelle } from '../partie/text'

const SEITE = 50

const ERGEBNIS: Record<Sicht, string> = {
  sieg: de.partien.sieg,
  remis: de.partien.remis,
  niederlage: de.partien.niederlage,
}

/** Zeile 2: „7. Nov. · 5+3 · Elo +12“ oder „… · zählt nicht“ (10.7). */
function zweiteZeile(p: PartieZeileDto): string {
  const elo =
    p.zaehltFuerElo && p.eloVorher !== null && p.eloNachher !== null
      ? fuelle(de.partien.eloAenderung, { aenderung: eloAenderung(p.eloVorher, p.eloNachher) })
      : de.partien.zaehltNicht
  return [kurzDatum(p.beendetAm), de.bedenkzeit[p.bedenkzeit], elo].join(' · ')
}

function Zeile({ partie, gegner }: { partie: PartieZeileDto; gegner: GegnerDto | undefined }) {
  const sicht = sichtAuf(partie.ergebnis, partie.spielerFarbe)
  const name = gegner?.name ?? partie.gegnerKey
  return (
    <Link className="zeile partiezeile" to={`/nachspielen/${partie.id}`}>
      {gegner !== undefined && <Avatar gegner={gegner} />}
      <span className="zeile__mitte">
        <span className="partiezeile__oben">
          <span className="zeile__titel">{name}</span>
          <span className="partiezeile__ergebnis">
            <span className={`ergebnispunkt ergebnispunkt--${sicht}`} aria-hidden="true" />
            <span>{ERGEBNIS[sicht]}</span>
          </span>
        </span>
        <span className="zeile__klein">{zweiteZeile(partie)}</span>
      </span>
    </Link>
  )
}

/** Partienliste (`/partien`, SCHACH-SPEC 10.7). Offline aus dem Cache `schach-partien-v1`. */
export function Partienliste() {
  const [gegner, setGegner] = useState<Map<string, GegnerDto>>(new Map())
  const [partien, setPartien] = useState<PartieZeileDto[] | null>(null)
  const [weitere, setWeitere] = useState(false)
  const [fehler, setFehler] = useState(false)
  const [laedtMehr, setLaedtMehr] = useState(false)

  const lade = useCallback(() => {
    void Promise.all([
      mitCache(() => api.get<GegnerDto[]>('/gegner'), partienCache.merkeGegner, partienCache.gegner).catch(() => null),
      mitCache(() => api.get<PartienListeDto>(`/partien?limit=${SEITE}`), partienCache.merkeListe, partienCache.liste),
    ]).then(
      ([g, liste]) => {
        setFehler(false)
        if (g !== null) setGegner(new Map(g.wert.map((x) => [x.key, x])))
        setPartien(liste.wert.partien)
        setWeitere(liste.wert.weitere && !liste.ausCache)
      },
      () => setFehler(true),
    )
  }, [])
  useEffect(lade, [lade])

  const mehr = async () => {
    const letzte = partien?.at(-1)
    if (letzte === undefined) return
    setLaedtMehr(true)
    try {
      const liste = await api.get<PartienListeDto>(`/partien?limit=${SEITE}&vorId=${encodeURIComponent(letzte.id)}`)
      setPartien((alt) => [...(alt ?? []), ...liste.partien])
      setWeitere(liste.weitere)
    } catch {
      setFehler(true)
    } finally {
      setLaedtMehr(false)
    }
  }

  return (
    <Seite titel={t('partien.titel')}>
      {fehler && partien === null ? (
        <Fehlerkarte
          onErneut={() => {
            setFehler(false)
            lade()
          }}
        />
      ) : partien === null ? (
        <Ladeanzeige skelette={6} />
      ) : partien.length === 0 ? (
        <div className="leer">
          <p>{t('partien.leer')}</p>
          <Link className="knopf" to="/neu">
            {t('hub.neuePartie')}
          </Link>
        </div>
      ) : (
        <>
          <ul className="liste">
            {partien.map((p) => (
              <li key={p.id}>
                <Zeile partie={p} gegner={gegner.get(p.gegnerKey)} />
              </li>
            ))}
          </ul>
          {fehler && <p className="fehlertext">{t('allgemein.fehler')}</p>}
          {weitere && (
            <button type="button" className="knopf knopf--outline knopf--breit" disabled={laedtMehr} onClick={() => void mehr()}>
              {t('partien.mehrLaden')}
            </button>
          )}
        </>
      )}
    </Seite>
  )
}
