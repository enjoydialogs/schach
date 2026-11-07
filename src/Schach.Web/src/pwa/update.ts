import { useSyncExternalStore } from 'react'
import type { RegisterSWOptions } from 'vite-plugin-pwa/types'

// Update-Hinweis (SCHACH-SPEC 10.13): Service Worker mit `registerType: 'prompt'`. Ein neuer SW
// wartet, bis der Nutzer „Neu laden“ tippt. `registration.update()` laeuft bei Sichtbarkeit,
// hoechstens stuendlich. Modul-Speicher, damit die Registrierung unabhaengig von der ersten Route
// laeuft (auch ein Kaltstart direkt in /partie/:id registriert) und `UpdateHinweis` nur liest.

export const STUNDE_MS = 60 * 60 * 1000

/** Ist die naechste Pruefung faellig? `letzte` ist der Zeitpunkt der letzten Pruefung (ms). */
export function updateFaellig(letzte: number | null, jetzt: number): boolean {
  return letzte === null || jetzt - letzte >= STUNDE_MS
}

export type RegisterSW = (optionen: RegisterSWOptions) => (neuLaden?: boolean) => Promise<void>

let neueVersion = false
let aktualisiere: (() => Promise<void>) | null = null
const hoerer = new Set<() => void>()

function setze(wert: boolean): void {
  neueVersion = wert
  for (const h of hoerer) h()
}

/**
 * Registriert den Service Worker ueber `registerSW` (aus `virtual:pwa-register`, siehe
 * registrieren.ts) und haengt die stuendliche Pruefung an `visibilitychange`.
 */
export function starteUpdates(
  registerSW: RegisterSW,
  { jetzt = () => Date.now(), dokument = globalThis.document }: { jetzt?: () => number; dokument?: Document } = {},
): void {
  const update = registerSW({
    immediate: true,
    onNeedRefresh: () => setze(true),
    onRegisteredSW: (_url, registrierung) => {
      if (registrierung === undefined) return
      // Die Registrierung selbst hat gerade nach einer neuen Version gesehen.
      let letzte: number | null = jetzt()
      dokument.addEventListener('visibilitychange', () => {
        if (dokument.visibilityState !== 'visible') return
        if (!updateFaellig(letzte, jetzt())) return
        if (globalThis.navigator?.onLine === false) return
        letzte = jetzt()
        registrierung.update().catch(() => {
          // offline oder Server weg: naechste Sichtbarkeit nach einer Stunde
        })
      })
    },
  })
  aktualisiere = () => update(true)
}

/** „Neu laden“: der wartende SW uebernimmt, die Seite laedt neu. */
export function neuLaden(): void {
  void aktualisiere?.()
}

function abonniere(h: () => void): () => void {
  hoerer.add(h)
  return () => hoerer.delete(h)
}

export function useNeueVersion(): boolean {
  return useSyncExternalStore(abonniere, () => neueVersion)
}

/** Nur fuer Tests. */
export function setzeUpdateZurueck(): void {
  aktualisiere = null
  setze(false)
}
