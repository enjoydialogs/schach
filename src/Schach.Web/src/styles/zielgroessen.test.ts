import { readFileSync } from 'node:fs'
import { resolve } from 'node:path'

// Zielgroessen (SCHACH-SPEC 15.3 Prototyp-Abnahme Punkt 1, Phase 4 #41): jedes Tippziel ist
// mindestens 48 x 48 px hoch. Geprueft wird die Hoehe der Regel, die das Tippziel traegt; die Breite
// ergibt sich aus Inhalt, Polsterung oder `min-width` (Knopf) und steht in docs/abnahme/mvp.md.

const dateien = [
  '../styles/base.css',
  '../components/bausteine.css',
  '../partie/partie.css',
  '../nachspielen/nachspielen.css',
  '../pwa/pwa.css',
].map((d) => readFileSync(resolve(__dirname, d), 'utf8').replace(/\/\*[\s\S]*?\*\//g, ''))

/** Pixelwert von `height` oder `min-height` im ersten Block, dessen Selektorliste `selektor` enthaelt. */
function hoehe(selektor: string): number {
  for (const css of dateien) {
    for (const [, kopf, rumpf] of css.matchAll(/([^{}]+)\{([^{}]*)\}/g)) {
      const selektoren = kopf!.split(',').map((s) => s.trim())
      if (!selektoren.includes(selektor)) continue
      const treffer = rumpf!.match(/(?:^|[;\s])(?:min-)?height:\s*(\d+)px/)
      if (treffer) return Number(treffer[1])
    }
  }
  throw new Error(`Keine Hoehe fuer ${selektor}`)
}

describe('Zielgroessen', () => {
  it.each([
    '.knopf',
    '.symbolknopf',
    '.segment',
    '.schalter',
    '.sheet__zeile',
    '.palettewahl__wahl',
    '.leistenknopf',
    '.zugchip',
    '.update__knopf',
  ])('%s ist mindestens 48 px hoch', (selektor) => {
    expect(hoehe(selektor)).toBeGreaterThanOrEqual(48)
  })
})
