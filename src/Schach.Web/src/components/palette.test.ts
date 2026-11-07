import { readFileSync } from 'node:fs'
import { resolve } from 'node:path'
import { ladePaletteNeu, lesePalette, PALETTEN, setzePalette, STANDARD_PALETTE } from './palette'

// Grundfarbe (SCHACH-SPEC 10.3): Speicher, Anwenden, und jede Palette in palette.css haelt die
// Kontrastregel (alle Paare >= 4,5:1) und passt zur Tabelle in palette.ts.

const css = readFileSync(resolve(__dirname, '../styles/palette.css'), 'utf8')

/** Variablen eines Blocks; `:root` allein ist die Standardpalette. */
function block(id: string): Record<string, string> {
  const selektor = id === STANDARD_PALETTE ? ':root {' : `:root[data-palette='${id}'] {`
  const start = css.indexOf(selektor)
  if (start < 0) throw new Error(`Block fuer ${id} fehlt`)
  const inhalt = css.slice(start + selektor.length, css.indexOf('\n}', start))
  const werte: Record<string, string> = {}
  for (const [, name, wert] of inhalt.matchAll(/(--[\w-]+):\s*(#[0-9a-f]{6})\s*;/gi)) {
    if (name !== undefined && wert !== undefined) werte[name] = wert.toLowerCase()
  }
  return werte
}

/** Hex-Werte einer Palette (Standard plus Ueberschreibungen); fehlende Variable wirft. */
function palette(id: string): (name: string) => string {
  const werte = { ...block(STANDARD_PALETTE), ...block(id) }
  return (name) => {
    const wert = werte[name]
    if (wert === undefined) throw new Error(`${name} fehlt in ${id}`)
    return wert
  }
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

/** Glas: Weiss mit Deckkraft 0,72 ueber dem Grund. */
function glas(grund: string): string {
  return mische('#ffffff', 0.72, grund)
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
    const paare: [string, string, string][] = [
      ['Text auf Grund', w('--text'), w('--bg')],
      ['Nebentext auf Grund', w('--text-muted'), w('--bg')],
      ['Nebentext auf Glas', w('--text-muted'), glas(w('--bg'))],
      ['Nebentext auf hellem Feld', w('--text-muted'), w('--board-light')],
      ['Knopftext auf Akzent', weiss, w('--accent')],
      ['Akzent auf Grund', w('--accent'), w('--bg')],
      ['Akzent auf Akzent-Tint', w('--accent'), w('--accent-tint')],
      ['Avatar Anfaenger', w('--text'), w('--avatar-anfaenger')],
      ['Avatar Hobbyspieler', w('--text'), w('--avatar-hobbyspieler')],
      ['Avatar Vereinsspieler', weiss, w('--avatar-vereinsspieler')],
      ['Avatar Turnierspieler', weiss, w('--avatar-turnierspieler')],
      ['Avatar Meister', weiss, w('--avatar-meister')],
      // Phase 4 (#41): Paare, die bisher fehlten
      ['Akzent auf Hover-Tint (12 % ueber Grund)', w('--accent'), mische(w('--accent'), 0.12, w('--bg'))],
      ['Gefahr auf Grund', w('--danger'), w('--bg')],
      ['Gefahr auf Glas', w('--danger'), glas(w('--bg'))],
      ['Knopftext auf Gefahr', weiss, w('--danger')],
      ['Warntext auf Warn-Tint', w('--warning-text'), w('--warning-tint')],
      ['Warntext auf Grund', w('--warning-text'), w('--bg')],
      ['Gesperrt auf Gesperrt-Flaeche', w('--text-muted'), mische(w('--text'), 0.07, glas(w('--bg')))],
      ['Weiss auf dunklem Glas', weiss, mische(w('--text'), 0.86, w('--bg'))],
    ]
    for (const [name, vorne, hinten] of paare) {
      expect({ name, kontrast: kontrast(vorne, hinten) >= 4.5 }).toEqual({ name, kontrast: true })
    }
  })
})
