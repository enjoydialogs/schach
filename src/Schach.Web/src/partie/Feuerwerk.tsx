import type { CSSProperties } from 'react'
import { FEUERWERK_AUFSTIEG_MS, FEUERWERK_FUNKEN_MS, FEUERWERK_VORLAUF_MS, RAKETEN } from './feuerwerkZeit'
import './feuerwerk.css'

const FUNKEN = 20
const WEITE_PX = 88

/**
 * Kurzes buntes Feuerwerk ueber dem Brett beim frischen Sieg (SCHACH-SPEC 10.3): drei Raketen
 * steigen auf und zerplatzen in Funken, zusammen knapp 2 s. Nur CSS, faengt keine Klicks ab,
 * bei `prefers-reduced-motion: reduce` unsichtbar (feuerwerk.css). Rein dekorativ.
 */
export function Feuerwerk() {
  return (
    <div className="feuerwerk" data-testid="feuerwerk" aria-hidden="true">
      {RAKETEN.map((r, i) => {
        const start = FEUERWERK_VORLAUF_MS + r.startMs
        const stil = {
          '--x': `${r.x}%`,
          '--y': `${r.y}%`,
          '--start': `${start}ms`,
          '--knall': `${start + FEUERWERK_AUFSTIEG_MS}ms`,
          '--aufstieg': `${FEUERWERK_AUFSTIEG_MS}ms`,
          '--funken': `${FEUERWERK_FUNKEN_MS}ms`,
          '--farbe': `var(--feuerwerk-${r.farben[0]})`,
        } as CSSProperties
        return (
          <div key={i} className="feuerwerk__rakete" style={stil}>
            <span className="feuerwerk__spur" />
            <span className="feuerwerk__blitz" />
            {Array.from({ length: FUNKEN }, (_, j) => {
              const winkel = (j / FUNKEN) * 2 * Math.PI
              // Jeder zweite Funke fliegt etwas kuerzer: die Explosion wirkt doppelt gefuellt.
              const weite = j % 2 === 0 ? WEITE_PX : WEITE_PX * 0.62
              const funke = {
                '--dx': `${Math.round(Math.cos(winkel) * weite)}px`,
                '--dy': `${Math.round(Math.sin(winkel) * weite)}px`,
                '--farbe': `var(--feuerwerk-${r.farben[j % 2]})`,
              } as CSSProperties
              return <span key={j} className="feuerwerk__funke" style={funke} />
            })}
          </div>
        )
      })}
    </div>
  )
}
