// Toene und Haptik (SCHACH-SPEC 10.3): WebAudio-Synthese ohne Dateien fuer Zug, Schlag und
// Partieende; `navigator.vibrate(15)` beim eigenen Zug, `[30, 40, 30]` am Ende. Ein Schalter.

export type TonArt = 'zug' | 'schlag' | 'ende'

export const VIBRATION_ZUG = 15
export const VIBRATION_ENDE = [30, 40, 30]

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
