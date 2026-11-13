// Toene und Haptik (SCHACH-SPEC 10.3): WebAudio-Synthese ohne Dateien fuer Zug, Schlag und
// Partieende; `navigator.vibrate(15)` beim eigenen Zug, `[30, 40, 30]` am Ende. Beim Sieg ein
// heller Lauf und dazu das Feuerwerk (Pfeifen, Knall, Knistern), die Vibration pulst mit jedem Knall.
// Ein Schalter.

import { explosionS, FEUERWERK_AUFSTIEG_MS, RAKETEN } from './feuerwerkZeit'

export type TonArt = 'zug' | 'schlag' | 'ende' | 'sieg'

export const VIBRATION_ZUG = 15
export const VIBRATION_ENDE = [30, 40, 30]
/** Kurzer Puls zum Lauf, dann je ein Puls auf jedem Feuerwerks-Knall (feuerwerkZeit.ts). */
export const VIBRATION_SIEG = siegVibration()

function siegVibration(): number[] {
  const muster = [40]
  let jetzt = 40
  RAKETEN.forEach((r, i) => {
    const knall = Math.round(explosionS(r) * 1000)
    const puls = i === RAKETEN.length - 1 ? 60 : 30
    muster.push(knall - jetzt, puls)
    jetzt = knall + puls
  })
  return muster
}

let kontext: AudioContext | null = null

/**
 * Der eine AudioContext der App (Toene und Sprachausgabe, SCHACH-SPEC 6.9). Beim Aufruf aus einer
 * Geste (Zug-Tipp) gibt der Browser ihn frei.
 */
export function audioKontext(): AudioContext | null {
  try {
    kontext ??= new AudioContext()
    // Vor der ersten Geste startet der Kontext angehalten; resume() holt ihn spaeter zurueck.
    if (kontext.state === 'suspended') void kontext.resume()
    return kontext
  } catch {
    return null
  }
}

function klang(ctx: AudioContext, ziel: AudioNode, frequenz: number, beiS: number, dauerS: number, spitze: number, form: OscillatorType) {
  const start = ctx.currentTime + beiS
  const osc = ctx.createOscillator()
  const huelle = ctx.createGain()
  osc.type = form
  osc.frequency.setValueAtTime(frequenz, start)
  huelle.gain.setValueAtTime(0.0001, start)
  huelle.gain.exponentialRampToValueAtTime(spitze, start + 0.005)
  huelle.gain.exponentialRampToValueAtTime(0.0001, start + dauerS)
  osc.connect(huelle).connect(ziel)
  osc.start(start)
  osc.stop(start + dauerS + 0.02)
}

let rauschPuffer: AudioBuffer | null = null

/** Eine Sekunde weisses Rauschen, einmal erzeugt (Knall und Knistern). */
function rauschen(ctx: AudioContext): AudioBuffer {
  if (rauschPuffer === null || rauschPuffer.sampleRate !== ctx.sampleRate) {
    rauschPuffer = ctx.createBuffer(1, ctx.sampleRate, ctx.sampleRate)
    const daten = rauschPuffer.getChannelData(0)
    for (let i = 0; i < daten.length; i++) daten[i] = Math.random() * 2 - 1
  }
  return rauschPuffer
}

/** Gefiltertes Rauschen mit Huelle: tiefer Knall (lowpass) oder feines Knistern (highpass). */
function rauschStoss(ctx: AudioContext, ziel: AudioNode, beiS: number, dauerS: number, spitze: number, typ: BiquadFilterType, frequenz: number, frequenzEnde = frequenz) {
  const start = ctx.currentTime + beiS
  const quelle = ctx.createBufferSource()
  quelle.buffer = rauschen(ctx)
  const filter = ctx.createBiquadFilter()
  filter.type = typ
  filter.frequency.setValueAtTime(frequenz, start)
  filter.frequency.exponentialRampToValueAtTime(frequenzEnde, start + dauerS)
  const huelle = ctx.createGain()
  huelle.gain.setValueAtTime(0.0001, start)
  huelle.gain.exponentialRampToValueAtTime(spitze, start + 0.004)
  huelle.gain.exponentialRampToValueAtTime(0.0001, start + dauerS)
  quelle.connect(filter).connect(huelle).connect(ziel)
  quelle.start(start, Math.random() * 0.5)
  quelle.stop(start + dauerS + 0.02)
}

/** Leises steigendes Pfeifen waehrend des Aufstiegs einer Rakete. */
function pfeifen(ctx: AudioContext, ziel: AudioNode, beiS: number, dauerS: number) {
  const start = ctx.currentTime + beiS
  const osc = ctx.createOscillator()
  const huelle = ctx.createGain()
  osc.type = 'sine'
  osc.frequency.setValueAtTime(700, start)
  osc.frequency.exponentialRampToValueAtTime(1900, start + dauerS)
  huelle.gain.setValueAtTime(0.0001, start)
  huelle.gain.exponentialRampToValueAtTime(0.05, start + 0.05)
  huelle.gain.exponentialRampToValueAtTime(0.0001, start + dauerS)
  osc.connect(huelle).connect(ziel)
  osc.start(start)
  osc.stop(start + dauerS + 0.02)
}

/** Feuerwerk passend zum Bild: je Rakete Pfeifen, Knall und Knistern. */
function feuerwerk(ctx: AudioContext, ziel: AudioNode) {
  const aufstiegS = FEUERWERK_AUFSTIEG_MS / 1000
  for (const r of RAKETEN) {
    const knall = explosionS(r)
    pfeifen(ctx, ziel, knall - aufstiegS, aufstiegS)
    rauschStoss(ctx, ziel, knall, 0.45, 0.5, 'lowpass', 2200, 300)
    for (let i = 0; i < 7; i++) {
      rauschStoss(ctx, ziel, knall + 0.12 + Math.random() * 0.45, 0.025, 0.12, 'highpass', 3500)
    }
  }
}

/** Spielt einen kurzen synthetischen Ton; `lautstaerke` 0 bis 1. Ohne WebAudio still. */
export function spieleTon(art: TonArt, lautstaerke = 1): void {
  if (!(lautstaerke > 0)) return
  const ctx = audioKontext()
  if (ctx === null) return
  try {
    // Die Huelle bleibt fest; die Lautstaerke regelt ein eigener Gain dahinter.
    const regler = ctx.createGain()
    regler.gain.value = Math.min(1, lautstaerke)
    regler.connect(ctx.destination)
    if (art === 'zug') {
      klang(ctx, regler, 520, 0, 0.07, 0.25, 'triangle')
    } else if (art === 'schlag') {
      klang(ctx, regler, 300, 0, 0.1, 0.35, 'square')
      klang(ctx, regler, 180, 0.03, 0.12, 0.25, 'triangle')
    } else if (art === 'sieg') {
      // Aufsteigender Dur-Lauf bis zur Oktave, der Schlusston mit Terz darueber: knapp 0,8 s.
      klang(ctx, regler, 523, 0, 0.14, 0.22, 'triangle')
      klang(ctx, regler, 659, 0.11, 0.14, 0.22, 'triangle')
      klang(ctx, regler, 784, 0.22, 0.14, 0.22, 'triangle')
      klang(ctx, regler, 1047, 0.33, 0.45, 0.24, 'sine')
      klang(ctx, regler, 1319, 0.33, 0.45, 0.12, 'sine')
      feuerwerk(ctx, regler)
    } else {
      klang(ctx, regler, 523, 0, 0.18, 0.25, 'sine')
      klang(ctx, regler, 659, 0.16, 0.18, 0.25, 'sine')
      klang(ctx, regler, 784, 0.32, 0.3, 0.25, 'sine')
    }
  } catch {
    // Ton ist Beiwerk: nie ein Fehler im Zug
  }
}

/** Vibriert, wo das Geraet es kann (Android Chrome). */
export function vibriere(muster: number | number[]): void {
  try {
    navigator.vibrate?.(muster)
  } catch {
    // ohne Vibration
  }
}
