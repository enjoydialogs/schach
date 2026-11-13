import { readFileSync } from 'node:fs'
import { resolve } from 'node:path'
import { ladePaletteNeu, lesePalette, PALETTEN, setzePalette, STANDARD_PALETTE } from './palette'

// Grundfarbe (SCHACH-SPEC 10.3): Speicher, Anwenden, und jede Palette in palette.css haelt die
// Kontrastregel (alle Paare >= 4,5:1) und passt zur Tabelle in palette.ts. Abgeleitete Flaechen
// (Glas, Tints) rechnet der Test aus ihrer color-mix-Formel im CSS nach, je Palette.

const css = readFileSync(resolve(__dirname, '../styles/palette.css'), 'utf8')

/** Rohwerte eines Blocks; `:root` allein ist die Standardpalette. */
function block(id: string): Record<string, string> {
  const selektor = id === STANDARD_PALETTE ? ':root {' : `:root[data-palette='${id}'] {`
  const start = css.indexOf(selektor)
  if (start < 0) throw new Error(`Block fuer ${id} fehlt`)
  const inhalt = css.slice(start + selektor.length, css.indexOf('\n}', start)).replace(/\/\*[\s\S]*?\*\//g, '')
  const werte: Record<string, string> = {}
  for (const [, name, wert] of inhalt.matchAll(/(--[\w-]+):\s*([^;]+);/g)) {
    if (name !== undefined && wert !== undefined) werte[name] = wert.trim().toLowerCase()
  }
  return werte
}

/**
 * Farbe einer Variable in einer Palette (Standard plus Ueberschreibungen) als Hex. Loest `var(--x)`
 * und `color-mix(in srgb, <farbe> N%, transparent)` auf; die Mischung liegt ueber `hinten`
 * (Standard: `--bg` der Palette). Andere Werte werfen.
 */
function palette(id: string): (name: string, hinten?: string) => string {
  const werte = { ...block(STANDARD_PALETTE), ...block(id) }
  const loese = (wert: string, hinten: string | undefined): string => {
    if (/^#[0-9a-f]{6}$/.test(wert)) return wert
    const verweis = wert.match(/^var\((--[\w-]+)\)$/)
    if (verweis) return farbe(verweis[1]!, hinten)
    const mix = wert.match(/^color-mix\(in srgb, (.+) (\d+)%, transparent\)$/)
    if (mix) return mische(loese(mix[1]!, hinten), Number(mix[2]) / 100, hinten ?? farbe('--bg'))
    throw new Error(`Wert nicht aufloesbar: ${wert}`)
  }
  const farbe = (name: string, hinten?: string): string => {
    const wert = werte[name]
    if (wert === undefined) throw new Error(`${name} fehlt in ${id}`)
    return loese(wert, hinten)
  }
  return farbe
}

function rgb(hex: string): [number, number, number] {
  return [parseInt(hex.slice(1, 3), 16), parseInt(hex.slice(3, 5), 16), parseInt(hex.slice(5, 7), 16)]
}

function leuchtdichte(hex: string): number {
  const linear = (v: number) => {
    const c = v / 255
    return c <= 0.03928 ? c / 12.92 : ((c + 0.055) / 1.055) ** 2.4
  }
  const [r, g, b] = rgb(hex)
  return 0.2126 * linear(r) + 0.7152 * linear(g) + 0.0722 * linear(b)
}

function kontrast(a: string, b: string): number {
  const [x, y] = [leuchtdichte(a), leuchtdichte(b)]
  return (Math.max(x, y) + 0.05) / (Math.min(x, y) + 0.05)
}

/** `vorne` mit Deckkraft `anteil` ueber `hinten` (wie color-mix mit transparent). */
function mische(vorne: string, anteil: number, hinten: string): string {
  const h = rgb(hinten)
  return '#' + rgb(vorne).map((v, i) => Math.round(v * anteil + h[i]! * (1 - anteil)).toString(16).padStart(2, '0')).join('')
}

describe('Palette', () => {
  beforeEach(() => {
    localStorage.clear()
    ladePaletteNeu()
  })

  it('startet ohne gespeicherten Wert mit Lila', () => {
    expect(lesePalette()).toBe('lila')
    expect(document.documentElement.dataset.palette).toBe('lila')
  })

  it('faellt bei unbekanntem Wert auf Lila zurueck', () => {
    localStorage.setItem('palette', 'regenbogen')
    expect(lesePalette()).toBe('lila')
  })

  it('speichert die Wahl und setzt data-palette und theme-color', () => {
    const meta = document.createElement('meta')
    meta.name = 'theme-color'
    document.head.append(meta)

    setzePalette('pink')

    expect(localStorage.getItem('palette')).toBe('pink')
    expect(document.documentElement.dataset.palette).toBe('pink')
    expect(meta.content).toBe('#fcf1f6')
    meta.remove()
  })

  it.each(PALETTEN.map((p) => p.id))('%s stimmt mit palette.css ueberein', (id) => {
    const werte = palette(id)
    const p = PALETTEN.find((q) => q.id === id)!
    expect(werte('--bg')).toBe(p.grund)
    expect(werte('--board-dark')).toBe(p.brett)
    expect(werte('--accent')).toBe(p.akzent)
  })

  it.each(PALETTEN.map((p) => p.id))('%s haelt 4,5:1 fuer Text, Knoepfe und Avatare', (id) => {
    const w = palette(id)
    const weiss = '#ffffff'
    const glas = w('--glass-surface')
    const paare: [string, string, string][] = [
      ['Text auf Grund', w('--text'), w('--bg')],
      ['Nebentext auf Grund', w('--text-muted'), w('--bg')],
      ['Nebentext auf Glas', w('--text-muted'), glas],
      ['Nebentext auf hellem Feld', w('--text-muted'), w('--board-light')],
      ['Knopftext auf Akzent', w('--on-accent'), w('--accent')],
      ['Akzent auf Grund', w('--accent'), w('--bg')],
      ['Akzent auf Akzent-Tint', w('--accent'), w('--accent-tint')],
      ['Avatar Anfaenger', w('--avatar-anfaenger-text'), w('--avatar-anfaenger')],
      ['Avatar Hobbyspieler', w('--avatar-hobbyspieler-text'), w('--avatar-hobbyspieler')],
      ['Avatar Vereinsspieler', weiss, w('--avatar-vereinsspieler')],
      ['Avatar Turnierspieler', weiss, w('--avatar-turnierspieler')],
      ['Avatar Meister', weiss, w('--avatar-meister')],
      // Phase 4 (#41): Paare, die bisher fehlten
      ['Akzent auf Hover-Tint ueber Grund', w('--accent'), w('--glass-tint')],
      ['Gefahr auf Grund', w('--danger'), w('--bg')],
      ['Gefahr auf Glas', w('--danger'), glas],
      ['Knopftext auf Gefahr', w('--on-danger'), w('--danger')],
      ['Warntext auf Warn-Tint', w('--warning-text'), w('--warning-tint')],
      ['Warntext auf Grund', w('--warning-text'), w('--bg')],
      ['Gesperrt auf Gesperrt-Flaeche', w('--text-muted'), w('--disabled-bg', glas)],
      ['Knopftext auf dunklem Glas', w('--on-accent'), w('--glass-dark')],
      ['Nebentext auf erhobenem Glas (Sheet, Dialog)', w('--text-muted'), w('--glass-surface-strong')],
      ['Text in der Spruchblase', w('--text'), w('--blase-flaeche')],
    ]
    // Nur Nacht: Koordinaten und Schalterknauf. Die hellen Paletten setzen Koordinaten in der
    // Gegenfarbe des Feldes (unter 4,5:1) und den Knauf mit Schatten ab; offen fuer spaeter.
    if (id === 'nacht') {
      paare.push(
        ['Koordinate auf hellem Feld', w('--notation-auf-hell', w('--board-light')), w('--board-light')],
        ['Koordinate auf dunklem Feld', w('--notation-auf-dunkel', w('--board-dark')), w('--board-dark')],
      )
      // Bedienelement (WCAG 1.4.11): Knauf hebt sich mit 3:1 von der Schiene ab.
      expect(kontrast(w('--knauf'), w('--disabled-bg', w('--surface')))).toBeGreaterThanOrEqual(3)
      expect(kontrast(w('--knauf-an'), w('--accent'))).toBeGreaterThanOrEqual(3)
    }
    for (const [name, vorne, hinten] of paare) {
      expect({ name, kontrast: kontrast(vorne, hinten) >= 4.5 }).toEqual({ name, kontrast: true })
    }
  })

  it.each(PALETTEN.map((p) => p.id))('%s: Tastatur-Cursor hebt sich mit 3:1 von beiden Feldfarben ab', (id) => {
    // --overlay-cursor: aussen --text, innen --bg (WCAG 1.4.11, Bedienelement).
    const w = palette(id)
    for (const feld of ['--board-light', '--board-dark']) {
      expect({ feld, kontrast: kontrast(w('--text'), w(feld)) >= 3 }).toEqual({ feld, kontrast: true })
    }
  })
})
