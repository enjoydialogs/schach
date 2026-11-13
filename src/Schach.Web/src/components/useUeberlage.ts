import { useEffect, useRef } from 'react'

const FOKUSSIERBAR =
  'button:not([disabled]), [href], input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])'

type Eintrag = {
  id: string
  /** Liegt ein eigener Verlaufseintrag oben, den die Zurueck-Geste verbrauchen kann? */
  imVerlauf: boolean
  schliessen: () => void
}

/** Offene Ueberlagen, die oberste zuletzt. Nur sie reagiert auf Tasten und die Zurueck-Geste. */
const stapel: Eintrag[] = []

/** So viele eigene `history.back()` warten noch auf ihr `popstate`; sie schliessen nichts. */
let eigeneZurueck = 0
/** Verlaufseintraege, die erst nach den ausstehenden eigenen Zurueck-Schritten entstehen duerfen. */
const wartend: (() => void)[] = []
let naechsteId = 0
let hoertZu = false

function marke(): string | undefined {
  const zustand: unknown = history.state
  return typeof zustand === 'object' && zustand !== null && 'ueberlage' in zustand
    ? String(zustand.ueberlage)
    : undefined
}

function zurueckOhneSchliessen() {
  eigeneZurueck++
  history.back()
}

/** Fuehrt `f` aus, sobald kein eigener Zurueck-Schritt mehr unterwegs ist (feste Reihenfolge). */
function nachEigenemZurueck(f: () => void) {
  if (eigeneZurueck === 0) f()
  else wartend.push(f)
}

/**
 * Android-Zurueck-Geste (und Browser-Zurueck): schliesst erst die oberste Ueberlage, statt die Seite
 * zu verlassen. Ein verwaister Eintrag (Ueberlage schloss durch Navigation) wird uebersprungen.
 */
function beiZurueck() {
  if (eigeneZurueck > 0) {
    eigeneZurueck--
    if (eigeneZurueck === 0) for (const f of wartend.splice(0)) f()
    return
  }
  const aktuell = marke()
  const oben = stapel[stapel.length - 1]
  if (oben?.imVerlauf && aktuell !== oben.id) {
    oben.imVerlauf = false
    oben.schliessen()
    return
  }
  if (aktuell !== undefined && !stapel.some((e) => e.id === aktuell)) zurueckOhneSchliessen()
}

/**
 * Verhalten einer modalen Ueberlage (Dialog, Bottom-Sheet, Umwandlung): Escape und die Zurueck-Geste
 * schliessen wie das Tippen daneben, der Fokus springt hinein und bleibt drin, beim Schliessen kehrt
 * er zum Ausloeser zurueck, der Hintergrund scrollt nicht mit. Die Flaeche braucht `tabIndex={-1}`.
 */
export function useUeberlage<T extends HTMLElement>(onSchliessen: () => void) {
  const ref = useRef<T>(null)
  const schliessen = useRef(onSchliessen)

  useEffect(() => {
    schliessen.current = onSchliessen
  })

  useEffect(() => {
    const flaeche = ref.current
    const eintrag: Eintrag = { id: `u${++naechsteId}`, imVerlauf: false, schliessen: () => schliessen.current() }
    stapel.push(eintrag)
    if (!hoertZu) {
      hoertZu = true
      window.addEventListener('popstate', beiZurueck)
    }
    // Eintrag erst nach dem Rendern (StrictMode baut den Effekt sofort ab und wieder auf) und erst,
    // wenn ein eigener Zurueck-Schritt der vorigen Ueberlage angekommen ist.
    let abgebaut = false
    const verzoegert = window.setTimeout(() => {
      nachEigenemZurueck(() => {
        if (abgebaut) return
        const zustand: unknown = history.state
        history.pushState({ ...(typeof zustand === 'object' ? zustand : null), ueberlage: eintrag.id }, '')
        eintrag.imVerlauf = true
      })
    }, 0)
    const vorher = document.activeElement instanceof HTMLElement ? document.activeElement : null
    const ueberlauf = document.body.style.overflow
    document.body.style.overflow = 'hidden'

    if (flaeche && !flaeche.contains(document.activeElement)) flaeche.focus({ preventScroll: true })

    function taste(e: KeyboardEvent) {
      if (stapel[stapel.length - 1] !== eintrag) return
      if (e.key === 'Escape') {
        e.preventDefault()
        schliessen.current()
        return
      }
      if (e.key !== 'Tab' || !flaeche) return
      const ziele = Array.from(flaeche.querySelectorAll<HTMLElement>(FOKUSSIERBAR))
      const erstes = ziele[0]
      const letztes = ziele[ziele.length - 1]
      if (erstes === undefined || letztes === undefined) {
        e.preventDefault()
        return
      }
      const aktiv = document.activeElement
      if (e.shiftKey && (aktiv === erstes || aktiv === flaeche)) {
        e.preventDefault()
        letztes.focus()
      } else if (!e.shiftKey && (aktiv === letztes || !flaeche.contains(aktiv))) {
        e.preventDefault()
        erstes.focus()
      }
    }

    document.addEventListener('keydown', taste)
    return () => {
      document.removeEventListener('keydown', taste)
      abgebaut = true
      window.clearTimeout(verzoegert)
      stapel.splice(stapel.indexOf(eintrag), 1)
      // Per Knopf, Tippen daneben oder Escape geschlossen: den eigenen Eintrag wieder abbauen. Hat eine
      // Navigation ihn schon ueberdeckt, bleibt er liegen und beiZurueck ueberspringt ihn spaeter.
      if (eintrag.imVerlauf && marke() === eintrag.id) zurueckOhneSchliessen()
      document.body.style.overflow = ueberlauf
      if (vorher?.isConnected) vorher.focus({ preventScroll: true })
    }
  }, [])

  return ref
}
