import { readFileSync } from 'node:fs'
import { resolve } from 'node:path'
import { manifest } from './manifest'

// index.html (Phase 4 #41, Lighthouse): ohne <link rel="icon"> fragt der Browser /favicon.ico an
// und bekommt 404, was als Konsolenfehler zaehlt. Das Icon kommt aus dem Manifest-Satz.

const html = readFileSync(resolve(__dirname, '../../index.html'), 'utf8')

describe('index.html', () => {
  it('verlinkt ein Icon aus dem Manifest-Satz', () => {
    const href = html.match(/<link rel="icon"[^>]*href="([^"]+)"/)?.[1]
    expect(manifest.icons.map((i) => i.src)).toContain(href)
  })
})
