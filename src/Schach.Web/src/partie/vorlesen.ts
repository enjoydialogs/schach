import { useCallback, useEffect, useRef } from 'react'
import { API_BASIS } from '../api/client'
import type { SpruchDto } from '../api/types'
import { audioKontext } from './toene'

// Sprueche vorlesen (SCHACH-SPEC 6.9, 10.3): Ton per `GET /sprachausgabe/{token}`, Wiedergabe ueber
// den gemeinsamen AudioContext aus toene.ts, ueber einen eigenen Gain fuer die Lautstaerke. Jeder
// Fehler und jede 204 ist Stille.

export interface VorleserUmgebung {
  kontext: () => AudioContext | null
  holen: (url: string) => Promise<Response>
}

const standard: VorleserUmgebung = {
  kontext: audioKontext,
  holen: (url) => fetch(url, { credentials: 'same-origin' }),
}

/** Spielt hoechstens einen Spruch; ein neuer Spruch oder `stoppe()` beendet den laufenden. */
export class Vorleser {
  private quelle: AudioBufferSourceNode | null = null
  private regler: GainNode | null = null
  private lautstaerke = 1
  private lauf = 0
  private readonly umgebung: VorleserUmgebung

  constructor(umgebung: VorleserUmgebung = standard) {
    this.umgebung = umgebung
  }

  /** 0 bis 1; wirkt sofort, auch auf einen laufenden Spruch. */
  setzeLautstaerke(wert: number): void {
    this.lautstaerke = Math.min(1, Math.max(0, wert))
    if (this.regler !== null) this.regler.gain.value = this.lautstaerke
  }

  async spiele(token: string): Promise<void> {
    this.stoppe()
    const lauf = this.lauf
    const ctx = this.umgebung.kontext()
    if (ctx === null) return
    try {
      const antwort = await this.umgebung.holen(`${API_BASIS}/sprachausgabe/${encodeURIComponent(token)}`)
      if (antwort.status !== 200 || lauf !== this.lauf) return
      const puffer = await ctx.decodeAudioData(await antwort.arrayBuffer())
      if (lauf !== this.lauf) return
      const quelle = ctx.createBufferSource()
      quelle.buffer = puffer
      const regler = ctx.createGain()
      regler.gain.value = this.lautstaerke
      quelle.connect(regler).connect(ctx.destination)
      quelle.onended = () => {
        if (this.quelle !== quelle) return
        this.quelle = null
        this.regler = null
      }
      this.quelle = quelle
      this.regler = regler
      quelle.start()
    } catch {
      // Stille, nie eine Fehlermeldung
    }
  }

  stoppe(): void {
    this.lauf++
    const quelle = this.quelle
    this.quelle = null
    this.regler = null
    try {
      quelle?.stop()
    } catch {
      // schon beendet
    }
  }
}

/**
 * Erkennt einen Spruch ueber Antworten hinweg. Nicht ueber das Token: Data Protection verschluesselt
 * mit Zufall, jede Antwort traegt fuer denselben Spruch ein neues.
 */
export function spruchSchluessel(spruch: SpruchDto): string {
  return `${spruch.nachHalbzug}:${spruch.ereignis}:${spruch.text}`
}

/**
 * Welcher Spruch jetzt vorgelesen wird, oder `null`. Der beim Laden sichtbare Spruch gilt als
 * bekannt und wird nicht vorgelesen, nur neu ankommende; Ausnahme `neuGestartet`: die Partie kommt
 * frisch aus „Neue Partie“, ihr erster Spruch ist neu. `bekannt` wird fortgeschrieben.
 */
export function naechsteAnsage(
  bekannt: Set<string> | null,
  spruch: SpruchDto | null,
  aktiv: boolean,
  neuGestartet = false,
): { bekannt: Set<string>; token: string | null } {
  const schluessel = spruch === null ? null : spruchSchluessel(spruch)
  if (bekannt === null && !neuGestartet) {
    return { bekannt: new Set(schluessel === null ? [] : [schluessel]), token: null }
  }
  bekannt ??= new Set()
  if (spruch === null || schluessel === null || bekannt.has(schluessel)) return { bekannt, token: null }
  bekannt.add(schluessel)
  return { bekannt, token: aktiv && spruch.stimme !== null ? spruch.stimme : null }
}

/**
 * Liest neu sichtbare Sprueche vor. `geladen` ist falsch, solange kein Zustand da ist. Stoppt bei
 * neuem Spruch, beim Verlassen und wenn der Tab in den Hintergrund geht. `wiederholen` spielt den
 * sichtbaren Spruch erneut (Tipp auf die Spruchblase). `lautstaerke` 0 bis 1 wirkt sofort.
 */
export function useVorlesen(
  spruch: SpruchDto | null,
  geladen: boolean,
  aktiv: boolean,
  neuGestartet = false,
  vorleser?: Vorleser,
  lautstaerke = 1,
) {
  const eigener = useRef<Vorleser | null>(null)
  eigener.current ??= vorleser ?? new Vorleser()
  const bekannt = useRef<Set<string> | null>(null)

  // Vor dem Vorlese-Effekt, damit schon der erste Spruch die gewaehlte Lautstaerke hat.
  useEffect(() => {
    eigener.current!.setzeLautstaerke(lautstaerke)
  }, [lautstaerke])

  useEffect(() => {
    if (!geladen) return
    const { bekannt: neu, token } = naechsteAnsage(bekannt.current, spruch, aktiv, neuGestartet)
    bekannt.current = neu
    if (token !== null) void eigener.current!.spiele(token)
  }, [geladen, spruch, aktiv, neuGestartet])

  useEffect(() => {
    if (!aktiv) eigener.current!.stoppe()
  }, [aktiv])

  useEffect(() => {
    const v = eigener.current!
    const sichtbarkeit = () => {
      if (document.visibilityState === 'hidden') v.stoppe()
    }
    document.addEventListener('visibilitychange', sichtbarkeit)
    return () => {
      document.removeEventListener('visibilitychange', sichtbarkeit)
      v.stoppe()
    }
  }, [])

  return useCallback(() => {
    if (aktiv && spruch?.stimme) void eigener.current!.spiele(spruch.stimme)
  }, [aktiv, spruch])
}
