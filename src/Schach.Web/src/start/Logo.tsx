import { Figur } from '../brett/figuren'

/**
 * Bildmarke fuer den Hub (SCHACH-SPEC 10.4): der Springer aus dem Cburnett-Satz (wie das App-Icon,
 * 10.13) auf einer leicht gedrehten Brett-Kachel in den Farben der gewaehlten Palette. Rein
 * dekorativ; der Name steht daneben als Text.
 */
export function Logo() {
  return (
    <span className="logo" aria-hidden="true">
      <span className="logo__kachel">
        <span className="logo__figur">
          <Figur code="wN" />
        </span>
      </span>
    </span>
  )
}
