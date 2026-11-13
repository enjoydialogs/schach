/*
 * Flache Schachfiguren fuer die dunkle Palette Nacht (SCHACH-SPEC 10.3). Eigene Zeichnung fuer
 * dieses Projekt: runde, kraeftige Formen mit Umriss, heller Glanzstelle, dunklerem Sockel und
 * weichem Schatten darunter (im SVG, kein CSS-Filter, damit das Ziehen fluessig bleibt). Farben
 * stehen fest im SVG, weil der Satz nur in Nacht erscheint.
 */
import type { CSSProperties, ReactNode } from 'react'
import type { FigurProps } from './figuren'

interface Toene {
  koerper: string
  sockel: string
  rand: string
  glanz: string
}

const HELL: Toene = { koerper: '#e6edf1', sockel: '#b9c6ce', rand: '#93a2ac', glanz: '#ffffff' }
const DUNKEL: Toene = { koerper: '#5d707c', sockel: '#46565f', rand: '#222e36', glanz: '#8599a6' }

const UMRISS = { strokeWidth: 4, strokeLinejoin: 'round', strokeLinecap: 'round' } as const

function Svg({ children, svgStyle }: { children: ReactNode; svgStyle?: CSSProperties }) {
  return (
    <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 100 100" width="100%" height="100%" style={svgStyle}>
      {/* Schatten unter der Figur */}
      <ellipse cx="50" cy="89" rx="29" ry="5.5" fill="#000" opacity="0.32" />
      {/* Etwas groesser als gezeichnet, Fusspunkt bleibt auf dem Schatten */}
      <g transform="translate(50 86) scale(1.08) translate(-50 -86)">{children}</g>
    </svg>
  )
}

type Teil = (t: Toene) => ReactNode

/** Fuellung und Umriss eines Formteils. */
const form = (t: Toene, fill: string = t.koerper) => ({ fill, stroke: t.rand, ...UMRISS })

const sockel = (t: Toene, x = 24, breite = 52): ReactNode => (
  <rect x={x} y="71" width={breite} height="14" rx="7" {...form(t, t.sockel)} />
)

const bauer: Teil = (t) => (
  <>
    <path
      d="M50 44 C42 44 38 50 40 56 C33 61 30 67 30 74 H70 C70 67 67 61 60 56 C62 50 58 44 50 44 Z"
      {...form(t)}
    />
    {sockel(t, 27, 46)}
    <circle cx="50" cy="32" r="12" {...form(t)} />
    <circle cx="45.5" cy="27.5" r="3.5" fill={t.glanz} />
  </>
)

const turm: Teil = (t) => (
  <>
    <rect x="33" y="38" width="34" height="36" rx="3" {...form(t)} />
    {sockel(t)}
    <path
      d="M28 24 Q28 20 32 20 H38 V27 H46 V20 H54 V27 H62 V20 H68 Q72 20 72 24 V38 Q72 42 68 42 H32 Q28 42 28 38 Z"
      {...form(t)}
    />
    <rect x="38" y="46" width="5" height="20" rx="2.5" fill={t.glanz} />
  </>
)

const springer: Teil = (t) => (
  <>
    <path
      d="M34 73 C33 62 38 55 45 50 C40 51 33 53 28 50 C24 47 24 41 27 38 L44 24 C45 18 50 14 56 13 L58 20 C68 23 74 35 73 48 C72 58 69 65 70 73 Z"
      {...form(t)}
    />
    <path d="M59 22 C66 28 69 38 68 52" fill="none" stroke={t.sockel} strokeWidth="4" strokeLinecap="round" />
    {sockel(t)}
    <circle cx="48" cy="31" r="3.2" fill={t.rand} />
    <circle cx="31" cy="43" r="1.8" fill={t.rand} />
    <path d="M47 21 C43 23 40 26 37 29" fill="none" stroke={t.glanz} strokeWidth="3.5" strokeLinecap="round" />
  </>
)

const laeufer: Teil = (t) => (
  <>
    <path d="M42 52 H58 C58 60 66 64 68 74 H32 C34 64 42 60 42 52 Z" {...form(t)} />
    {sockel(t)}
    <rect x="37" y="47" width="26" height="9" rx="4.5" {...form(t, t.sockel)} />
    <ellipse cx="50" cy="32" rx="12" ry="15" {...form(t)} />
    <circle cx="50" cy="14" r="5" {...form(t)} />
    <path d="M54 25 L47 34" stroke={t.rand} strokeWidth="3" strokeLinecap="round" />
    <ellipse cx="44.5" cy="31" rx="2.8" ry="5" fill={t.glanz} />
  </>
)

const dame: Teil = (t) => (
  <>
    <path
      d="M32 68 L22 34 L31 50 L37 27 L44 46 L50 22 L56 46 L63 27 L69 50 L78 34 L68 68 Z"
      {...form(t)}
    />
    {[
      [22, 31],
      [37, 25],
      [50, 19],
      [63, 25],
      [78, 31],
    ].map(([cx, cy]) => (
      <circle key={cx} cx={cx} cy={cy} r="5" {...form(t)} />
    ))}
    <rect x="30" y="62" width="40" height="10" rx="5" {...form(t, t.sockel)} />
    {sockel(t)}
    <path d="M37 54 L33 41" stroke={t.glanz} strokeWidth="3.5" strokeLinecap="round" />
  </>
)

const koenig: Teil = (t) => (
  <>
    <path
      d="M46 13 H54 V19 H60 V27 H54 V38 H46 V27 H40 V19 H46 Z"
      {...form(t)}
    />
    <path
      d="M32 70 C22 58 20 44 30 38 C38 34 46 39 50 46 C54 39 62 34 70 38 C80 44 78 58 68 70 Z"
      {...form(t)}
    />
    <rect x="30" y="62" width="40" height="10" rx="5" {...form(t, t.sockel)} />
    {sockel(t)}
    <path d="M30 47 C30 43 33 41 36 41" fill="none" stroke={t.glanz} strokeWidth="3.5" strokeLinecap="round" />
  </>
)

const TEIL = { k: koenig, q: dame, r: turm, b: laeufer, n: springer, p: bauer } as const

/** Einzelne flache Figur, `code` wie in react-chessboard: `wN`, `bQ`. */
export function FlacheFigur({ code, svgStyle }: { code: string } & FigurProps) {
  const teil = TEIL[code[1]!.toLowerCase() as keyof typeof TEIL]
  return <Svg svgStyle={svgStyle}>{teil(code[0] === 'w' ? HELL : DUNKEL)}</Svg>
}
