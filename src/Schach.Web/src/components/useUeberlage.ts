import { useEffect, useRef } from 'react'

const FOKUSSIERBAR =
  'button:not([disabled]), [href], input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])'

/** Offene Ueberlagen, die oberste zuletzt. Nur sie reagiert auf Tasten. */
const stapel: object[] = []

/**
 * Verhalten einer modalen Ueberlage (Dialog, Bottom-Sheet, Umwandlung): Escape schliesst wie das
 * Tippen daneben, der Fokus springt hinein und bleibt drin, beim Schliessen kehrt er zum Ausloeser
 * zurueck, der Hintergrund scrollt nicht mit. Die Flaeche braucht `tabIndex={-1}`.
 */
export function useUeberlage<T extends HTMLElement>(onSchliessen: () => void) {
  const ref = useRef<T>(null)
  const schliessen = useRef(onSchliessen)

  useEffect(() => {
    schliessen.current = onSchliessen
  })

  useEffect(() => {
    const flaeche = ref.current
    const eintrag = {}
    stapel.push(eintrag)
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
      stapel.splice(stapel.indexOf(eintrag), 1)
      document.body.style.overflow = ueberlauf
      if (vorher?.isConnected) vorher.focus({ preventScroll: true })
    }
  }, [])

  return ref
}
