import { useCallback, useEffect, useRef, useState } from 'react'
import { api, ApiFehler } from '../api/client'
import type { TippAntwort, ZustandDto } from '../api/types'
import { gastPartieId } from '../components/SpeicherSchluessel'
import { useSitzung } from '../components/sitzung'
import { de } from '../i18n/de'
import { nachZug, sichtbareStellung, type Anzeige } from './anzeige'
import { sichtAuf } from './erklaerung'
import { spieleTon, vibriere, VIBRATION_ENDE, VIBRATION_SIEG, VIBRATION_ZUG } from './toene'
import type { TippStand } from './tipp'
import { restMs, uhrVersatz } from './uhr'

// Zustand und Serverabgleich des Partie-Bildschirms (SCHACH-SPEC 8.1, 8.5, 8.6, 10.3).
// Der Server ist autoritativ; der Client zeigt optimistisch und uebernimmt jede Antwort,
// auch den Zustand aus einem 409.

const POLL_MS = 3_000
const HINWEIS_MS = 3_000
/**
 * Hoechstens so lange nach Empfang bleibt ein Gegnerzug unsichtbar. Die Denkzeit D ist hoechstens
 * 3 s (8.6); ohne Uhr fehlt `serverZeit`, eine nachgehende Geraeteuhr verzoegerte ihn sonst beliebig.
 */
const SICHTBAR_MAX_MS = 3_500

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
  /** Gerade hier beendet (Offen → Beendet gesehen): Ende-Ton und Sieg-Konfetti, nicht beim Wieder-Oeffnen. */
  frischBeendet: boolean
  /** `true` Gast, `false` Spieler, `null` unbekannt (Sitzung nicht lesbar). */
  gast: boolean | null
  offline: boolean
  hinweis: string | null
  /** Zeigt einen Hinweis fuer kurze Zeit in der Statuszeile. */
  zeigeHinweis: (text: string) => void
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

export function usePartie(id: string, toeneAn: boolean, toeneLautstaerke = 1): PartieSteuerung {
  const [lade, setLade] = useState<LadeStand>('laedt')
  const [zustand, setZustand] = useState<ZustandDto | null>(null)
  const sitzung = useSitzung()
  const gast = sitzung.status === 'gast' ? true : sitzung.status === 'spieler' ? false : null
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
  // Clientzeit, ab der der Gegnerzug spaetestens sichtbar ist (SICHTBAR_MAX_MS nach Empfang).
  const [sichtbarSpaetestens, setSichtbarSpaetestens] = useState<number | null>(null)
  const toeneRef = useRef(toeneAn)
  useEffect(() => {
    toeneRef.current = toeneAn
  }, [toeneAn])
  const lautstaerkeRef = useRef(toeneLautstaerke)
  useEffect(() => {
    lautstaerkeRef.current = toeneLautstaerke
  }, [toeneLautstaerke])

  const uebernimm = useCallback((z: ZustandDto) => {
    const opt = optimistischRef.current
    if (opt !== null && z.gegnerZug !== null) setVorGegnerzug({ halbzug: z.gegnerZug.halbzug, fen: opt.fen })
    optimistischRef.current = null
    setOptimistisch(null)
    zustandRef.current = z
    setZustand(z)
    const jetzt = Date.now()
    if (z.uhr !== null) setVersatzMs(uhrVersatz(z.uhr, jetzt))
    setSichtbarSpaetestens(z.gegnerZug !== null ? jetzt + SICHTBAR_MAX_MS : null)
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
          const gehalten = zustandRef.current
          const weg = vomServer && e.status === 404
          // Eine Gast-Partie wird beim Beenden geloescht (8.9). Hat der Sweep sie beendet, waehrend
          // der Tab verborgen war, kommt nur noch 404: dann nicht mit dem alten Brett weiterspielen.
          if (gehalten !== null && !(weg && gehalten.status === 'Offen')) return
          if (weg) {
            if (gastPartieId.lies() === id) gastPartieId.setze(null)
            optimistischRef.current = null
            setOptimistisch(null)
            zustandRef.current = null
            setZustand(null)
          }
          setLade(weg ? 'nichtGefunden' : 'fehler')
        },
      ),
    [id, uebernimm],
  )

  /** Neu laden, ausser die Partie ist schon beendet (endgueltig, 8.2; Gast-Partien gibt es dann nicht mehr). */
  const ladeWennOffen = useCallback(() => {
    if (zustandRef.current?.status !== 'Beendet') void ladeZustand()
  }, [ladeZustand])

  // Erstes Laden, Sitzung, Wiederaufnahme nach App-Wechsel und nach Verbindungsverlust.
  useEffect(() => {
    void ladeZustand()
    const sichtbar = () => {
      if (document.visibilityState === 'visible') ladeWennOffen()
    }
    const online = () => {
      setOffline(false)
      ladeWennOffen()
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
  }, [ladeZustand, ladeWennOffen])

  // Wiederholung nach 429; beim Verlassen des Bildschirms abbrechen.
  const wiederholung = useRef<ReturnType<typeof setTimeout> | null>(null)
  useEffect(
    () => () => {
      if (wiederholung.current !== null) clearTimeout(wiederholung.current)
    },
    [],
  )

  /**
   * POST mit einheitlicher Fehlerbehandlung (8.5, 11.4):
   * - 409 mit Zustand: uebernehmen. Ohne Zustand (Concurrency-Konflikt, 5xx, Netz): neu laden,
   *   denn der Zug kann schon gespeichert sein (T1, 8.8).
   * - 429 auf `/zuege`: „<Name> denkt nach …“, nach `Retry-After` derselbe Body (idempotent, 8.8).
   * - 429 und 503 auf `/tipp`: „Tipp gerade nicht verfuegbar“.
   */
  const sende = useCallback(
    <T,>(pfad: string, body: unknown, verarbeite: (antwort: T) => void, fehlerText: string) => {
      setBeschaeftigt(true)
      const istZug = pfad.endsWith('/zuege')
      const istTipp = pfad.endsWith('/tipp')
      const versuche = (): void => {
        wiederholung.current = null
        api.post<T>(pfad, body).then(
          (antwort) => {
            setOffline(false)
            setLimitWartet(false)
            setBeschaeftigt(false)
            verarbeite(antwort)
          },
          (e: unknown) => {
            if (e instanceof ApiFehler && e.status === 429 && istZug) {
              setOffline(false)
              setLimitWartet(true)
              const sekunden =
                e.retryAfterSekunden !== null && Number.isFinite(e.retryAfterSekunden) ? e.retryAfterSekunden : 2
              wiederholung.current = setTimeout(versuche, Math.max(1, sekunden) * 1000)
              return
            }
            setLimitWartet(false)
            setBeschaeftigt(false)
            if (!(e instanceof ApiFehler)) {
              setOffline(true)
              verwerfeOptimistisch()
              // Kam der Zug doch an, holt ihn das GET; sonst bleibt „Keine Verbindung“ stehen.
              if (istZug) void ladeZustand()
              return
            }
            setOffline(false)
            const z = e.antwort?.zustand
            const tippWeg = istTipp && (e.status === 429 || e.status === 503)
            if (z) uebernimm(z)
            else {
              verwerfeOptimistisch()
              if (e.status !== 400 && e.status !== 429 && !tippWeg) void ladeZustand()
            }
            zeigeHinweis(tippWeg || e.fehler === 'TippNichtVerfuegbar' ? de.status.tippNichtVerfuegbar : fehlerText)
          },
        )
      }
      versuche()
    },
    [ladeZustand, uebernimm, verwerfeOptimistisch, zeigeHinweis],
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
        spieleTon(ergebnis.schlag ? 'schlag' : 'zug', lautstaerkeRef.current)
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
    let ziel = Date.parse(sichtbarAb) - versatzMs
    if (sichtbarSpaetestens !== null) ziel = Math.min(ziel, sichtbarSpaetestens)
    const warte = Math.max(0, ziel - Date.now())
    const zeitgeber = setTimeout(() => setJetztClient(Date.now()), warte + 20)
    return () => clearTimeout(zeitgeber)
  }, [sichtbarAb, versatzMs, sichtbarSpaetestens])
  // Nach SICHTBAR_MAX_MS gilt der Gegnerzug als sichtbar, auch wenn die Geraeteuhr nachgeht.
  let jetztServer = jetztClient + versatzMs
  if (sichtbarAb !== null && sichtbarSpaetestens !== null && jetztClient >= sichtbarSpaetestens) {
    jetztServer = Math.max(jetztServer, Date.parse(sichtbarAb))
  }

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

  // Ein GET beim lokal berechneten Uhrablauf (8.1). Meldet der Server die Partie danach noch offen
  // (Uhren nicht ganz gleich, Sweep noch nicht gelaufen), fragt der Client bei sichtbarem Tab im
  // Poll-Takt nach, bis sie endet, statt bei 00:00 stehen zu bleiben.
  const ablaufGemeldet = useRef<number | null>(null)
  useEffect(() => {
    const uhr = zustand?.uhr
    if (zustand === null || zustand.status !== 'Offen' || !uhr || uhr.laeuftFuer === null) return
    const rest = restMs(uhr, uhr.laeuftFuer, Date.now(), versatzMs)
    // Vor amZugSeit steht die Uhr; der Rest zaehlt erst ab dann.
    const start = uhr.amZugSeit === null ? 0 : Math.max(0, Date.parse(uhr.amZugSeit) - versatzMs - Date.now())
    const halbzug = zustand.halbzug
    const nachfrage = ablaufGemeldet.current === halbzug
    const zeitgeber = setTimeout(
      () => {
        if (nachfrage && document.visibilityState !== 'visible') return
        ablaufGemeldet.current = halbzug
        void ladeZustand()
      },
      Math.max(start + rest + 50, nachfrage ? POLL_MS : 0),
    )
    return () => clearTimeout(zeitgeber)
  }, [zustand, versatzMs, ladeZustand])

  // Toene fuer sichtbar gewordene Gegnerzuege und das Partieende.
  const tonHalbzug = useRef<number | null>(null)
  const sichtbarerHalbzug = anzeige?.halbzug ?? null
  const endeSichtbar = zustand?.status === 'Beendet' && anzeige !== null && !anzeige.gegnerDenkt
  // Partie, die hier offen zu sehen war. Waehrend des Renderns gesetzt (React: Zustand aus Props
  // ableiten), damit Ende-Ton und Sieg-Feier (Endeblatt) dasselbe Signal lesen.
  const [offenGesehen, setOffenGesehen] = useState<string | null>(null)
  if (zustand?.status === 'Offen' && offenGesehen !== zustand.id) setOffenGesehen(zustand.id)
  /** Gerade hier beendet (Uebergang Offen → Beendet), nicht beim Oeffnen einer schon beendeten Partie. */
  const frischBeendet = endeSichtbar && zustand !== null && offenGesehen === zustand.id
  const sieg = zustand !== null && zustand.ergebnis !== null && sichtAuf(zustand.ergebnis, zustand.spielerFarbe) === 'sieg'
  useEffect(() => {
    if (zustand === null || sichtbarerHalbzug === null) return
    const vorher = tonHalbzug.current
    tonHalbzug.current = sichtbarerHalbzug
    if (vorher === null || sichtbarerHalbzug <= vorher || !toeneRef.current) return
    const zug = zustand.zugfolge[sichtbarerHalbzug - 1]
    if (zug !== undefined && zug.seite !== zustand.spielerFarbe) spieleTon(zug.san.includes('x') ? 'schlag' : 'zug', lautstaerkeRef.current)
  }, [sichtbarerHalbzug, zustand])
  useEffect(() => {
    if (!frischBeendet || !toeneRef.current) return
    // Sieg: eigener heller Lauf; Niederlage und Remis bleiben beim ruhigen Ende-Ton.
    spieleTon(sieg ? 'sieg' : 'ende', lautstaerkeRef.current)
    vibriere(sieg ? VIBRATION_SIEG : VIBRATION_ENDE)
  }, [frischBeendet, sieg])

  return {
    lade,
    zustand,
    anzeige,
    frischBeendet,
    gast,
    offline,
    hinweis,
    zeigeHinweis,
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
