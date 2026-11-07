// Erzeugt die PWA-Icons (SCHACH-SPEC 10.13) ohne Fremdpakete und ohne Fremddienst:
// Springer-Silhouette aus dem Cburnett-Satz (BSD-3-Clause, src/brett/figuren.tsx) auf #F6F4EE.
//
//   node scripts/icons.mjs
//
// schreibt public/icons/icon-192.png, icon-512.png und icon-512-maskable.png. Die PNGs sind
// eingecheckt; der Bau ruft das Skript nicht auf. Nie nach public/assets/ schreiben (SpaHosting
// gaebe dort ein Jahr `immutable`).

import { mkdirSync, writeFileSync } from 'node:fs'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'
import { crc32, deflateSync } from 'node:zlib'

const HINTERGRUND = [0xf6, 0xf4, 0xee]
const FIGUR = [0x1e, 0x23, 0x29] // --text aus styles/palette.css

// Koerper des Springers im 45er-Raster, wie in figuren.tsx (transform translate(0,0.3)).
const PFADE = [
  'M 22,10 C 32.5,11 38.5,18 38,39 L 15,39 C 15,30 25,32.5 23,18',
  'M 24,18 C 24.38,20.91 18.45,25.37 16,27 C 13,29 13.18,31.34 11,31 C 9.958,30.06 12.41,27.96 11,28 C 10,28 11.19,29.23 10,30 C 9,30 5.997,31 6,26 C 6,24 12,14 12,14 C 12,14 13.89,12.1 14,10.5 C 13.27,9.506 13.5,8.5 13.5,7.5 C 14.5,6.5 16.5,10 16.5,10 L 18.5,10 C 18.5,10 19.28,8.008 21,7 C 22,7 22,10 22,10',
]
const VERSATZ_Y = 0.3
const RAND = 0.75 // halbe Strichbreite 1.5 der Cburnett-Figuren
const AUGE = { x: 9, y: 25.5 + VERSATZ_Y, r: 1.1 }

/** Pfad (nur M, L, C, absolut) in einen geschlossenen Polygonzug. */
function polygon(d) {
  const zahlen = d.match(/[MLC]|-?\d*\.?\d+/g)
  const punkte = []
  let i = 0
  let befehl = ''
  let x = 0
  let y = 0
  const lies = () => Number(zahlen[i++])
  while (i < zahlen.length) {
    if (/[MLC]/.test(zahlen[i])) befehl = zahlen[i++]
    if (befehl === 'M' || befehl === 'L') {
      x = lies()
      y = lies() + VERSATZ_Y
      punkte.push([x, y])
    } else if (befehl === 'C') {
      const [x1, y1, x2, y2, x3, y3] = [lies(), lies() + VERSATZ_Y, lies(), lies() + VERSATZ_Y, lies(), lies() + VERSATZ_Y]
      for (let s = 1; s <= 24; s++) {
        const t = s / 24
        const u = 1 - t
        punkte.push([
          u * u * u * x + 3 * u * u * t * x1 + 3 * u * t * t * x2 + t * t * t * x3,
          u * u * u * y + 3 * u * u * t * y1 + 3 * u * t * t * y2 + t * t * t * y3,
        ])
      }
      x = x3
      y = y3
    }
  }
  return punkte
}

const POLYGONE = PFADE.map(polygon)
const KANTEN = POLYGONE.flatMap((p) => p.map((a, k) => [a, p[(k + 1) % p.length]]))

function innen(px, py) {
  // Nicht-Null-Regel ueber beide Polygone (Vereinigung).
  for (const p of POLYGONE) {
    let w = 0
    for (let k = 0; k < p.length; k++) {
      const [ax, ay] = p[k]
      const [bx, by] = p[(k + 1) % p.length]
      if (ay <= py) {
        if (by > py && (bx - ax) * (py - ay) - (px - ax) * (by - ay) > 0) w++
      } else if (by <= py && (bx - ax) * (py - ay) - (px - ax) * (by - ay) < 0) w--
    }
    if (w !== 0) return true
  }
  return false
}

function nahAmRand(px, py) {
  for (const [[ax, ay], [bx, by]] of KANTEN) {
    const dx = bx - ax
    const dy = by - ay
    const l = dx * dx + dy * dy
    const t = l === 0 ? 0 : Math.max(0, Math.min(1, ((px - ax) * dx + (py - ay) * dy) / l))
    const ex = ax + t * dx - px
    const ey = ay + t * dy - py
    if (ex * ex + ey * ey <= RAND * RAND) return true
  }
  return false
}

function figurAn(px, py) {
  if ((px - AUGE.x) ** 2 + (py - AUGE.y) ** 2 <= AUGE.r ** 2) return false
  return innen(px, py) || nahAmRand(px, py)
}

// Begrenzungsrahmen der Figur inklusive Strich.
const alle = POLYGONE.flat()
const RAHMEN = {
  x0: Math.min(...alle.map((p) => p[0])) - RAND,
  x1: Math.max(...alle.map((p) => p[0])) + RAND,
  y0: Math.min(...alle.map((p) => p[1])) - RAND,
  y1: Math.max(...alle.map((p) => p[1])) + RAND,
}

/** `anteil`: Seitenlaenge der Figur (laengere Seite) im Verhaeltnis zur Icon-Kante. */
function zeichne(groesse, anteil) {
  const breite = RAHMEN.x1 - RAHMEN.x0
  const hoehe = RAHMEN.y1 - RAHMEN.y0
  const skala = (groesse * anteil) / Math.max(breite, hoehe)
  const ox = (groesse - breite * skala) / 2 - RAHMEN.x0 * skala
  const oy = (groesse - hoehe * skala) / 2 - RAHMEN.y0 * skala
  const N = 4 // 4x4 Abtastungen je Pixel
  const zeilen = Buffer.alloc(groesse * (groesse * 3 + 1))
  for (let y = 0; y < groesse; y++) {
    const z = y * (groesse * 3 + 1)
    zeilen[z] = 0 // Filter: keiner
    for (let x = 0; x < groesse; x++) {
      let treffer = 0
      for (let sy = 0; sy < N; sy++) {
        for (let sx = 0; sx < N; sx++) {
          const fx = (x + (sx + 0.5) / N - ox) / skala
          const fy = (y + (sy + 0.5) / N - oy) / skala
          if (fx >= RAHMEN.x0 && fx <= RAHMEN.x1 && fy >= RAHMEN.y0 && fy <= RAHMEN.y1 && figurAn(fx, fy)) treffer++
        }
      }
      const a = treffer / (N * N)
      for (let c = 0; c < 3; c++) zeilen[z + 1 + x * 3 + c] = Math.round(HINTERGRUND[c] * (1 - a) + FIGUR[c] * a)
    }
  }
  return png(groesse, zeilen)
}

function block(typ, daten) {
  const laenge = Buffer.alloc(4)
  laenge.writeUInt32BE(daten.length)
  const kopf = Buffer.concat([Buffer.from(typ, 'ascii'), daten])
  const pruef = Buffer.alloc(4)
  pruef.writeUInt32BE(crc32(kopf))
  return Buffer.concat([laenge, kopf, pruef])
}

function png(groesse, zeilen) {
  const ihdr = Buffer.alloc(13)
  ihdr.writeUInt32BE(groesse, 0)
  ihdr.writeUInt32BE(groesse, 4)
  ihdr[8] = 8 // Bittiefe
  ihdr[9] = 2 // RGB
  return Buffer.concat([
    Buffer.from([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]),
    block('IHDR', ihdr),
    block('IDAT', deflateSync(zeilen, { level: 9 })),
    block('IEND', Buffer.alloc(0)),
  ])
}

const ziel = join(dirname(fileURLToPath(import.meta.url)), '..', 'public', 'icons')
mkdirSync(ziel, { recursive: true })
// "any": Figur fuellt 72 % der Kante. "maskable": Sicherheitszone ist der Kreis mit 40 % Radius
// (W3C Manifest), die Figur bleibt mit 54 % Kante samt Diagonale darin.
writeFileSync(join(ziel, 'icon-192.png'), zeichne(192, 0.72))
writeFileSync(join(ziel, 'icon-512.png'), zeichne(512, 0.72))
writeFileSync(join(ziel, 'icon-512-maskable.png'), zeichne(512, 0.54))
console.log(`Icons geschrieben nach ${ziel}`)
