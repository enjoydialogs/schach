import { useSyncExternalStore } from 'react'
import { api, ApiFehler } from '../api/client'
import type { PartienListeDto } from '../api/types'
import { partienCache } from '../components/partienCache'
import { SpeicherSchluessel } from '../components/SpeicherSchluessel'

// Install-Karte (SCHACH-SPEC 10.13): nur auf Start, wenn `beforeinstallprompt` gefangen ist, die
// App nicht standalone laeuft, mindestens eine beendete Partie existiert und „Später“ nicht juenger
// als 14 Tage ist (ISO-Zeitpunkt in localStorage `installHinweis`, 12.3).

export const SPAETER_MS = 14 * 24 * 60 * 60 * 1000

/** Das Chromium-Ereignis; nicht in lib.dom. */
export interface BeforeInstallPromptEvent extends Event {
  prompt(): Promise<void>
  readonly userChoice: Promise<{ outcome: 'accepted' | 'dismissed' }>
}

export interface InstallLage {
  promptGefangen: boolean
  standalone: boolean
  beendetePartie: boolean
  /** ISO-Zeitpunkt des letzten „Später“ oder `null`. */
  spaeterAm: string | null
  jetzt: number
}

export function zeigeInstallKarte(lage: InstallLage): boolean {
  if (!lage.promptGefangen || lage.standalone || !lage.beendetePartie) return false
  if (lage.spaeterAm === null) return true
  const damals = Date.parse(lage.spaeterAm)
  // Kaputter Wert gilt als nie verschoben.
  return Number.isNaN(damals) || lage.jetzt - damals >= SPAETER_MS
}

export function istStandalone(fenster: Window = globalThis.window): boolean {
  try {
    if (fenster.matchMedia?.('(display-mode: standalone)').matches) return true
  } catch {
    // kein matchMedia (alte Browser, Tests)
  }
  return (fenster.navigator as Navigator & { standalone?: boolean }).standalone === true
}

let ereignis: BeforeInstallPromptEvent | null = null
const hoerer = new Set<() => void>()

function setze(neu: BeforeInstallPromptEvent | null): void {
  ereignis = neu
  for (const h of hoerer) h()
}

/** Faengt `beforeinstallprompt` ab App-Start (feuert oft, bevor Start gerendert ist). */
export function fangeInstallPrompt(fenster: Window = globalThis.window): void {
  fenster.addEventListener('beforeinstallprompt', (e) => {
    // Kein Mini-Infobar des Browsers; die App fragt selbst auf Start.
    e.preventDefault()
    setze(e as BeforeInstallPromptEvent)
  })
  fenster.addEventListener('appinstalled', () => setze(null))
}

function abonniere(h: () => void): () => void {
  hoerer.add(h)
  return () => hoerer.delete(h)
}

export function useInstallPrompt(): BeforeInstallPromptEvent | null {
  return useSyncExternalStore(abonniere, () => ereignis)
}

/** Zeigt den Browser-Dialog. Das Ereignis taugt nur einmal. */
export async function installiere(): Promise<void> {
  const e = ereignis
  if (e === null) return
  setze(null)
  try {
    await e.prompt()
    await e.userChoice
  } catch {
    // Browser hat abgelehnt; die Karte bleibt weg bis zum naechsten Ereignis
  }
}

function lokal(): Storage | null {
  try {
    return globalThis.localStorage ?? null
  } catch {
    return null
  }
}

/** localStorage `installHinweis`: ISO-Zeitpunkt des letzten „Später“. */
export const installHinweis = {
  lies: (): string | null => {
    try {
      return lokal()?.getItem(SpeicherSchluessel.installHinweis) ?? null
    } catch {
      return null
    }
  },
  setze: (jetzt: number): void => {
    try {
      lokal()?.setItem(SpeicherSchluessel.installHinweis, new Date(jetzt).toISOString())
    } catch {
      // Speicher gesperrt: die Karte kommt dann beim naechsten Start wieder
    }
  },
}

/**
 * Gibt es mindestens eine beendete Partie? Nur Spieler haben eine Partienliste (Gast: 401 → nein).
 * Offline zaehlt die gecachte Liste.
 */
export async function hatBeendetePartie(): Promise<boolean> {
  try {
    const liste = await api.get<PartienListeDto>('/partien?limit=1')
    return liste.partien.length > 0
  } catch (e) {
    if (e instanceof ApiFehler) return false
    const kopie = await partienCache.liste()
    return (kopie?.partien.length ?? 0) > 0
  }
}

/** Nur fuer Tests. */
export function setzeInstallZurueck(): void {
  setze(null)
}
