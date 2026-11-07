// Toene und Haptik (SCHACH-SPEC 10.3): WebAudio-Synthese ohne Dateien fuer Zug, Schlag und
// Partieende; `navigator.vibrate(15)` beim eigenen Zug, `[30, 40, 30]` am Ende. Ein Schalter.

export type TonArt = 'zug' | 'schlag' | 'ende'

export const VIBRATION_ZUG = 15
export const VIBRATION_ENDE = [30, 40, 30]

let kontext: AudioContext | null = null

function audio(): AudioContext | null {
  try {
    kontext ??= new AudioContext()
    // Vor der ersten Geste startet der Kontext angehalten; resume() holt ihn spaeter zurueck.
    if (kontext.state === 'suspended') void kontext.resume()
    return kontext
  } catch {
    return null
  }
}

function klang(ctx: AudioContext, frequenz: number, beiS: number, dauerS: number, lautstaerke: number, form: OscillatorType) {
  const start = ctx.currentTime + beiS
  const osc = ctx.createOscillator()
  const huelle = ctx.createGain()
  osc.type = form
  osc.frequency.setValueAtTime(frequenz, start)
  huelle.gain.setValueAtTime(0.0001, start)
  huelle.gain.exponentialRampToValueAtTime(lautstaerke, start + 0.005)
  huelle.gain.exponentialRampToValueAtTime(0.0001, start + dauerS)
  osc.connect(huelle).connect(ctx.destination)
  osc.start(start)
  osc.stop(start + dauerS + 0.02)
}

/** Spielt einen kurzen synthetischen Ton. Ohne WebAudio still. */
export function spieleTon(art: TonArt): void {
  const ctx = audio()
  if (ctx === null) return
  if (art === 'zug') {
    klang(ctx, 520, 0, 0.07, 0.25, 'triangle')
  } else if (art === 'schlag') {
    klang(ctx, 300, 0, 0.1, 0.35, 'square')
    klang(ctx, 180, 0.03, 0.12, 0.25, 'triangle')
  } else {
    klang(ctx, 523, 0, 0.18, 0.25, 'sine')
    klang(ctx, 659, 0.16, 0.18, 0.25, 'sine')
    klang(ctx, 784, 0.32, 0.3, 0.25, 'sine')
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
