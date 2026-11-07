import { useSyncExternalStore } from 'react'

// Speicher fuer den `Hinweis` (Toast, SCHACH-SPEC 10.14): Modul-Speicher, damit eine Seite den
// Hinweis setzen und sofort weiternavigieren kann.

const DAUER_MS = 4_000

let text: string | null = null
let timer: ReturnType<typeof setTimeout> | null = null
const hoerer = new Set<() => void>()

function melde(): void {
  for (const h of hoerer) h()
}

export function zeigeHinweis(neu: string): void {
  text = neu
  if (timer !== null) clearTimeout(timer)
  timer = setTimeout(() => {
    text = null
    timer = null
    melde()
  }, DAUER_MS)
  melde()
}

/** Nur fuer Tests. */
export function setzeHinweisZurueck(): void {
  if (timer !== null) clearTimeout(timer)
  timer = null
  text = null
  melde()
}

function abonniere(h: () => void): () => void {
  hoerer.add(h)
  return () => hoerer.delete(h)
}

export function useHinweisText(): string | null {
  return useSyncExternalStore(abonniere, () => text)
}
