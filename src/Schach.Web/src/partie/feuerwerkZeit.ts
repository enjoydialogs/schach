// Feuerwerk beim Sieg (SCHACH-SPEC 10.3): eine Zeitleiste fuer Bild (Feuerwerk.tsx) und Ton
// (toene.ts), damit jeder Knall genau mit seiner Explosion zusammenfaellt.

/** Warten, bis das Partieende-Blatt hochgeglitten ist (`--dauer-mittel`). */
export const FEUERWERK_VORLAUF_MS = 240
/** Aufstieg der Rakete bis zur Explosion. */
export const FEUERWERK_AUFSTIEG_MS = 350
/** Funken fliegen aus und verglimmen. */
export const FEUERWERK_FUNKEN_MS = 900

export interface Rakete {
  /** Explosionspunkt in % der Brettbreite und -hoehe (oben, das Blatt deckt das untere Drittel). */
  x: number
  y: number
  /** Start relativ zum Vorlauf. */
  startMs: number
  /** Zwei Farben 1 bis 5 (`--feuerwerk-N`), abwechselnd auf die Funken verteilt. */
  farben: [number, number]
}

export const RAKETEN: Rakete[] = [
  { x: 28, y: 34, startMs: 0, farben: [1, 2] },
  { x: 72, y: 24, startMs: 280, farben: [3, 4] },
  { x: 50, y: 14, startMs: 560, farben: [5, 2] },
]

/** Zeitpunkt der Explosion ab Blatt-Start in Sekunden (fuer den Knall). */
export function explosionS(r: Rakete): number {
  return (FEUERWERK_VORLAUF_MS + r.startMs + FEUERWERK_AUFSTIEG_MS) / 1000
}
