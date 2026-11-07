import { ApiFehler } from '../api/client'
import type { GegnerDto, PartienListeDto, ZustandDto } from '../api/types'

// Cache `schach-partien-v1` fuer beendete Partien (SCHACH-SPEC 10.13): Partienliste und Nachspielen
// per `put`/`match`. Ihn verwaltet die App, nicht der Service Worker (der cacht `/api/` nie).
// Ohne Cache Storage (alte Browser, Tests, unsicherer Kontext) ist jeder Aufruf ein No-op.

export const PARTIEN_CACHE = 'schach-partien-v1'

const SCHLUESSEL = {
  liste: '/schach-partien/liste',
  gegner: '/schach-partien/gegner',
  partie: (id: string) => `/schach-partien/partie/${encodeURIComponent(id)}`,
}

async function oeffne(): Promise<Cache | null> {
  try {
    if (!('caches' in globalThis)) return null
    return await caches.open(PARTIEN_CACHE)
  } catch {
    return null
  }
}

async function lege(schluessel: string, wert: unknown): Promise<void> {
  const cache = await oeffne()
  try {
    await cache?.put(schluessel, new Response(JSON.stringify(wert), { headers: { 'Content-Type': 'application/json' } }))
  } catch {
    // Speicher voll: dann eben ohne Offline-Kopie
  }
}

async function hole<T>(schluessel: string): Promise<T | null> {
  const cache = await oeffne()
  try {
    const antwort = await cache?.match(schluessel)
    return antwort ? ((await antwort.json()) as T) : null
  } catch {
    return null
  }
}

export const partienCache = {
  merkeListe: (liste: PartienListeDto) => lege(SCHLUESSEL.liste, liste),
  liste: () => hole<PartienListeDto>(SCHLUESSEL.liste),
  merkeGegner: (gegner: GegnerDto[]) => lege(SCHLUESSEL.gegner, gegner),
  gegner: () => hole<GegnerDto[]>(SCHLUESSEL.gegner),
  /** Nur beendete Partien; offene aendern sich noch. */
  merkePartie: (z: ZustandDto) => (z.status === 'Beendet' ? lege(SCHLUESSEL.partie(z.id), z) : Promise.resolve()),
  partie: (id: string) => hole<ZustandDto>(SCHLUESSEL.partie(id)),
}

/** Bei Abmelden und Konto loeschen (10.9, 10.13). */
export async function leerePartienCache(): Promise<void> {
  try {
    if ('caches' in globalThis) await caches.delete(PARTIEN_CACHE)
  } catch {
    // nichts zu tun
  }
}

/**
 * Erst das Netz, bei Erfolg in den Cache; bei einem Netzfehler (kein `ApiFehler`) die Kopie aus dem
 * Cache. Ohne Kopie wird der Netzfehler weitergereicht. `offline` meldet, ob die Kopie kam.
 */
export async function mitCache<T>(
  netz: () => Promise<T>,
  merke: (wert: T) => Promise<void>,
  ausCache: () => Promise<T | null>,
): Promise<{ wert: T; ausCache: boolean }> {
  try {
    const wert = await netz()
    void merke(wert)
    return { wert, ausCache: false }
  } catch (e) {
    if (e instanceof ApiFehler) throw e
    const kopie = await ausCache()
    if (kopie === null) throw e
    return { wert: kopie, ausCache: true }
  }
}
