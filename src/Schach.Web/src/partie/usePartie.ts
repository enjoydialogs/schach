import { useCallback, useEffect, useRef, useState } from 'react'
import { api, ApiFehler } from '../api/client'
import type { SitzungAntwort, TippAntwort, ZustandDto } from '../api/types'
import { de } from '../i18n/de'
import { nachZug, sichtbareStellung, type Anzeige } from './anzeige'
import { spieleTon, vibriere, VIBRATION_ENDE, VIBRATION_ZUG } from './toene'
import type { TippStand } from './tipp'
import { restMs, uhrVersatz } from './uhr'

// Zustand und Serverabgleich des Partie-Bildschirms (SCHACH-SPEC 8.1, 8.5, 8.6, 10.3).
// Der Server ist autoritativ; der Client zeigt optimistisch und uebernimmt jede Antwort,
// auch den Zustand aus einem 409.

const POLL_MS = 3_000
const HINWEIS_MS = 3_000

export type LadeStand = 'laedt' | 'bereit' | 'nichtGefunden' | 'fehler'

interface Optimistisch {
  fen: string
  von: string
  nach: string
}

export interface PartieSteuerung {
  lade: LadeStand
  zustand: ZustandDto | null
  anzeige: Anzeige | null
  /** `true` Gast, `false` Spieler, `null` unbekannt (Sitzung nicht lesbar). */
  gast: boolean | null
  offline: boolean
  hinweis: string | null
  beschaeftigt: boolean
  /** 429 auf `/zuege`: der Client wiederholt nach `Retry-After`. */
  limitWartet: boolean
  tippStand: TippStand | null
  setTippStand: (stand: TippStand | null) => void
  versatzMs: number
  ziehe: (uci: string) => void
  nimmZurueck: () => void
  gibAuf: () => void
  bieteRemis: () => void
  holeTipp: () => void
}

export function usePartie(id: string, toeneAn: boolean): PartieSteuerung {
  const [lade, setLade] = useState<LadeStand>('laedt')
  const [zustand, setZustand] = useState<ZustandDto | null>(null)
  const [gast, setGast] = useState<boolean | null>(null)
  const [optimistisch, setOptimistisch] = useState<Optimistisch | null>(null)
  const [versatzMs, setVersatzMs] = useState(0)
  const [offline, setOffline] = useState(() => typeof navigator !== 'undefined' && navigator.onLine === false)
  const [hinweis, setHinweis] = useState<string | null>(null)
  const [beschaeftigt, setBeschaeftigt] = useState(false)
  const [limitWartet, setLimitWartet] = useState(false)
  const [tippStand, setTippStand] = useState<TippStand | null>(null)

  // Stellung nach dem eigenen Zug, bis der Gegnerzug sichtbar wird (Schluessel: dessen Halbzug).
  const [vorGegnerzug, setVorGegnerzug] = useState<{ halbzug: number; fen: string } | null>(null)
  const optimistischRef = useRef<Optimistisch | null>(null)
  const zustandRef = useRef<ZustandDto | null>(null)
  const toeneRef = useRef(toeneAn)
  useEffect(() => {
    toeneRef.current = toeneAn
  }, [toeneAn])

  const uebernimm = useCallback((z: ZustandDto) => {
    const opt = optimistischRef.current
    if (opt !== null && z.gegnerZug !== null) setVorGegnerzug({ halbzug: z.gegnerZug.halbzug, fen: opt.fen })
    optimistischRef.current = null
    setOptimistisch(null)
    zustandRef.current = z
    setZustand(z)
    if (z.uhr !== null) setVersatzMs(uhrVersatz(z.uhr, Date.now()))
    setLade('bereit')
  }, [])

  const verwerfeOptimistisch = useCallback(() => {
    optimistischRef.current = null
    setOptimistisch(null)
  }, [])

  const hinweisTimer = useRef<ReturnType<typeof setTimeout> | null>(null)
  const zeigeHinweis = useCallback((text: string) => {
    setHinweis(text)
    if (hinweisTimer.current !== null) clearTimeout(hinweisTimer.current)
    hinweisTimer.current = setTimeout(() => setHinweis(null), HINWEIS_MS)
  }, [])

  const ladeZustand = useCallback(
    () =>
      api.get<ZustandDto>(`/partien/${id}`).then(
        (z) => {
          uebernimm(z)
          setOffline(false)
        },
        (e: unknown) => {
          const vomServer = e instanceof ApiFehler
          setOffline(!vomServer)
          if (zustandRef.current !== null) return
          setLade(vomServer && e.status === 404 ? 'nichtGefunden' : 'fehler')
        },
      ),
    [id, uebernimm],
  )

  // Erstes Laden, Sitzung, Wiederaufnahme nach App-Wechsel und nach Verbindungsverlust.
  useEffect(() => {
    void ladeZustand()
    api
      .get<SitzungAntwort>('/sitzung')
      .then((s) => setGast(!s.angemeldet))
      .catch(() => setGast(null))
    const sichtbar = () => {
      if (document.visibilityState === 'visible') void ladeZustand()
    }
    const online = () => {
      setOffline(false)
      void ladeZustand()
    }
    const weg = () => setOffline(true)
    document.addEventListener('visibilitychange', sichtbar)
    window.addEventListener('online', online)
    window.addEventListener('offline', weg)
    return () => {
      document.removeEventListener('visibilitychange', sichtbar)
      window.removeEventListener('online', online)
      window.removeEventListener('offline', weg)
    }
  }, [ladeZustand])

  /** POST mit einheitlicher Fehlerbehandlung (8.5). 429 auf `/zuege` wird idempotent wiederholt. */
  const sende = useCallback(
    <T,>(pfad: string, body: unknown, verarbeite: (antwort: T) => void, fehlerText: string) => {
      setBeschaeftigt(true)
      const versuche = (): void => {
        api.post<T>(pfad, body).then(
          (antwort) => {
            setOffline(false)
            setLimitWartet(false)
            setBeschaeftigt(false)
            verarbeite(antwort)
          },
          (e: unknown) => {
            if (e instanceof ApiFehler && e.status === 429 && pfad.endsWith('/zuege')) {
              setOffline(false)
              setLimitWartet(true)
              setTimeout(versuche, (e.retryAfterSekunden ?? 2) * 1000)
              return
            }
            setLimitWartet(false)
            setBeschaeftigt(false)
            if (!(e instanceof ApiFehler)) {
              setOffline(true)
              verwerfeOptimistisch()
              return
            }
            setOffline(false)
            const z = e.antwort?.zustand
            if (z) uebernimm(z)
            else verwerfeOptimistisch()
            zeigeHinweis(e.fehler === 'TippNichtVerfuegbar' ? de.status.tippNichtVerfuegbar : fehlerText)
          },
        )
      }
      versuche()
    },
    [uebernimm, verwerfeOptimistisch, zeigeHinweis],
  )

  const ziehe = useCallback(
    (uci: string) => {
      const z = zustandRef.current
      if (z === null) return
      const ergebnis = nachZug(z.fen, uci)
      if (ergebnis === null) {
        zeigeHinweis(de.status.zugNichtMoeglich)
        return
      }
      const opt = { fen: ergebnis.fen, von: uci.slice(0, 2), nach: uci.slice(2, 4) }
      optimistischRef.current = opt
      setOptimistisch(opt)
      if (toeneRef.current) {
        spieleTon(ergebnis.schlag ? 'schlag' : 'zug')
        vibriere(VIBRATION_ZUG)
      }
      sende<ZustandDto>(`/partien/${id}/zuege`, { halbzug: z.halbzug, zug: uci }, uebernimm, de.status.zugNichtMoeglich)
    },
    [id, sende, uebernimm, zeigeHinweis],
  )

  const halbzugAktion = useCallback(
    (pfad: string) => {
      const z = zustandRef.current
      if (z === null) return
      sende<ZustandDto>(`/partien/${id}/${pfad}`, { halbzug: z.halbzug }, uebernimm, de.status.aktionNichtMoeglich)
    },
    [id, sende, uebernimm],
  )

  const holeTipp = useCallback(() => {
    const z = zustandRef.current
    if (z === null) return
    sende<TippAntwort>(
      `/partien/${id}/tipp`,
      { halbzug: z.halbzug },
      (antwort) => {
        uebernimm(antwort.zustand)
        setTippStand({ halbzug: antwort.zustand.halbzug, stufe: 1, tipp: antwort.tipp })
      },
      de.status.tippNichtVerfuegbar,
    )
  }, [id, sende, uebernimm])

  // Kein Dauertakt fuer den ganzen Bildschirm (das Brett soll beim Ziehen nicht neu rendern): die
  // Zeit springt nur, wenn der Gegnerzug sichtbar wird. Die Uhren ticken in `Uhr` selbst.
  const [jetztClient, setJetztClient] = useState(() => Date.now())
  const sichtbarAb = zustand?.gegnerZug?.sichtbarAb ?? null
  useEffect(() => {
    if (sichtbarAb === null) return
    const warte = Math.max(0, Date.parse(sichtbarAb) - versatzMs - Date.now())
    const zeitgeber = setTimeout(() => setJetztClient(Date.now()), warte + 20)
    return () => clearTimeout(zeitgeber)
  }, [sichtbarAb, versatzMs])
  const jetztServer = jetztClient + versatzMs

  let anzeige: Anzeige | null = null
  if (zustand !== null) {
    if (optimistisch !== null) {
      anzeige = {
        fen: optimistisch.fen,
        letzterZug: { von: optimistisch.von, nach: optimistisch.nach },
        halbzug: zustand.zugfolge.length + 1,
        gegnerDenkt: true,
      }
    } else {
      const vor = vorGegnerzug
      const fenVor = vor !== null && vor.halbzug === zustand.gegnerZug?.halbzug ? vor.fen : null
      anzeige = sichtbareStellung(zustand, jetztServer, fenVor)
    }
  }

  // Polling bei ausstehendem Gegnerzug, solange der Tab sichtbar ist (8.1).
  const ausstehend = zustand?.status === 'Offen' && zustand.gegnerZugAusstehend
  useEffect(() => {
    if (!ausstehend) return
    const takt = setInterval(() => {
      if (document.visibilityState === 'visible') void ladeZustand()
    }, POLL_MS)
    return () => clearInterval(takt)
  }, [ausstehend, ladeZustand])

  // Ein GET beim lokal berechneten Uhrablauf (8.1).
  const ablaufGemeldet = useRef<number | null>(null)
  useEffect(() => {
    const uhr = zustand?.uhr
    if (zustand === null || zustand.status !== 'Offen' || !uhr || uhr.laeuftFuer === null) return
    const rest = restMs(uhr, uhr.laeuftFuer, Date.now(), versatzMs)
    // Vor amZugSeit steht die Uhr; der Rest zaehlt erst ab dann.
    const start = uhr.amZugSeit === null ? 0 : Math.max(0, Date.parse(uhr.amZugSeit) - versatzMs - Date.now())
    const halbzug = zustand.halbzug
    const zeitgeber = setTimeout(() => {
      if (ablaufGemeldet.current === halbzug) return
      ablaufGemeldet.current = halbzug
      void ladeZustand()
    }, start + rest + 50)
    return () => clearTimeout(zeitgeber)
  }, [zustand, versatzMs, ladeZustand])

  // Toene fuer sichtbar gewordene Gegnerzuege und das Partieende.
  const tonHalbzug = useRef<number | null>(null)
  const warOffen = useRef(false)
  const sichtbarerHalbzug = anzeige?.halbzug ?? null
  const endeSichtbar = zustand?.status === 'Beendet' && anzeige !== null && !anzeige.gegnerDenkt
  useEffect(() => {
    if (zustand === null || sichtbarerHalbzug === null) return
    const vorher = tonHalbzug.current
    tonHalbzug.current = sichtbarerHalbzug
    if (vorher === null || sichtbarerHalbzug <= vorher || !toeneRef.current) return
    const zug = zustand.zugfolge[sichtbarerHalbzug - 1]
    if (zug !== undefined && zug.seite !== zustand.spielerFarbe) spieleTon(zug.san.includes('x') ? 'schlag' : 'zug')
  }, [sichtbarerHalbzug, zustand])
  useEffect(() => {
    if (zustand?.status === 'Offen') warOffen.current = true
    // Nur beim Uebergang Offen → Beendet, nicht beim Oeffnen einer schon beendeten Partie.
    if (!endeSichtbar || !warOffen.current) return
    warOffen.current = false
    if (toeneRef.current) {
      spieleTon('ende')
      vibriere(VIBRATION_ENDE)
    }
  }, [endeSichtbar, zustand])

  return {
    lade,
    zustand,
    anzeige,
    gast,
    offline,
    hinweis,
    beschaeftigt,
    limitWartet,
    tippStand,
    setTippStand,
    versatzMs,
    ziehe,
    nimmZurueck: () => halbzugAktion('zuruecknehmen'),
    gibAuf: () => halbzugAktion('aufgeben'),
    bieteRemis: () => halbzugAktion('remis-angebot'),
    holeTipp,
  }
}
