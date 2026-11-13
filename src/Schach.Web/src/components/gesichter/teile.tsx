import type { ReactNode } from 'react'

/*
 * Bausteine fuer die Gegner-Gesichter (SCHACH-SPEC 4.4). Flacher Comic-Stil ohne Konturen:
 * grosser Kopf, wenige kraeftige Formen, damit jedes Gesicht auch bei 40 px erkennbar bleibt.
 * Koordinatensystem 100 x 100; der Kreis-Zuschnitt kommt aus dem CSS (`.avatar__gesicht`).
 */

export const TINTE = '#2a2233'
export const MUNDRAUM = '#7a2235'
export const WANGE = '#ff8f8f'

/** SVG mit eigener Hintergrundflaeche, damit das Gesicht auf hellen und dunklen Seiten traegt. */
export function Rahmen({ hintergrund, children }: { hintergrund: string; children: ReactNode }) {
  return (
    <svg viewBox="0 0 100 100" width="100%" height="100%" focusable="false" aria-hidden="true">
      <rect width="100" height="100" fill={hintergrund} />
      {children}
    </svg>
  )
}

/** Hals, Ohren und Kopf. `kleidung` liegt zwischen Hals und Kopf, damit der Kragen den Hals ueberdeckt. */
export function Kopf({ haut, kleidung }: { haut: string; kleidung: ReactNode }) {
  return (
    <>
      <rect x="42" y="66" width="16" height="18" rx="4" fill={haut} />
      {kleidung}
      <circle cx="24.5" cy="53" r="6" fill={haut} />
      <circle cx="75.5" cy="53" r="6" fill={haut} />
      <ellipse cx="50" cy="50" rx="26" ry="28" fill={haut} />
    </>
  )
}

/** Oberkoerper am unteren Rand. */
export function Rumpf({ farbe }: { farbe: string }) {
  return <path d="M10 100 Q12 80 50 78 Q88 80 90 100 Z" fill={farbe} />
}

export function Nase({ farbe }: { farbe: string }) {
  return <ellipse cx="50" cy="58" rx="4.2" ry="3.6" fill={farbe} />
}

export function Wangen({ deckkraft = 0.55 }: { deckkraft?: number }) {
  return (
    <g fill={WANGE} opacity={deckkraft}>
      <circle cx="33" cy="61" r="5" />
      <circle cx="67" cy="61" r="5" />
    </g>
  )
}

/**
 * Offenes Auge. `lid` (0 bis 1) deckt den oberen Teil mit Hautfarbe ab und zieht eine Lidkante;
 * so entstehen halb geschlossene, muede oder skeptische Augen ohne Clip-Pfade.
 */
export function Auge({
  x,
  y = 50,
  haut,
  rx = 6.5,
  ry = 7,
  blickX = 0,
  blickY = 0,
  lid = 0,
}: {
  x: number
  y?: number
  haut: string
  rx?: number
  ry?: number
  blickX?: number
  blickY?: number
  lid?: number
}) {
  const kante = y - ry + lid * 2 * ry
  const halb = rx * Math.sqrt(Math.max(0, 1 - ((kante - y) / ry) ** 2))
  return (
    <g>
      <ellipse cx={x} cy={y} rx={rx} ry={ry} fill="#ffffff" />
      <circle cx={x + blickX} cy={y + blickY} r={rx * 0.58} fill={TINTE} />
      {lid > 0 && (
        <>
          <rect x={x - rx - 1} y={y - ry - 1.5} width={rx * 2 + 2} height={kante - (y - ry - 1.5)} fill={haut} />
          <line
            x1={x - halb - 0.6}
            y1={kante}
            x2={x + halb + 0.6}
            y2={kante}
            stroke={TINTE}
            strokeWidth="2.6"
            strokeLinecap="round"
          />
        </>
      )}
    </g>
  )
}

/** Geschlossenes, laechelndes Auge (Bogen nach oben). */
export function AugeZu({ x, y = 50 }: { x: number; y?: number }) {
  return (
    <path
      d={`M${x - 6.5} ${y + 1.5} Q${x} ${y - 6} ${x + 6.5} ${y + 1.5}`}
      fill="none"
      stroke={TINTE}
      strokeWidth="3.4"
      strokeLinecap="round"
    />
  )
}

/** Linie mit runden Enden, fuer Brauen und Muender. */
export function Strich({ d, farbe = TINTE, breite = 3.4 }: { d: string; farbe?: string; breite?: number }) {
  return <path d={d} fill="none" stroke={farbe} strokeWidth={breite} strokeLinecap="round" strokeLinejoin="round" />
}
