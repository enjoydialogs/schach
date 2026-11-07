import { api, ApiFehler } from '../api/client'
import type { HalbzugAnfrage, ZustandDto } from '../api/types'
import { gastPartieId } from './SpeicherSchluessel'

/**
 * Laedt die offene Partie: Spieler ueber `GET /partien/offen`, Gast ueber `gastPartieId`
 * (SCHACH-SPEC 10.4). Eine beendete oder unbekannte Gast-Partie loescht den Schluessel.
 * `null`: keine offene Partie (oder nicht lesbar).
 */
export async function ladeOffenePartie(gast: boolean): Promise<ZustandDto | null> {
  if (!gast) {
    // 204 ohne offene Partie hat keinen Body; Gaeste bekommen 401.
    return api.get<ZustandDto | undefined>('/partien/offen').then(
      (z) => z ?? null,
      () => null,
    )
  }
  const id = gastPartieId.lies()
  if (id === null) return null
  try {
    const z = await api.get<ZustandDto>(`/partien/${id}`)
    if (z.status === 'Offen') return z
    gastPartieId.setze(null)
    return null
  } catch (e) {
    if (e instanceof ApiFehler && e.status === 404) gastPartieId.setze(null)
    return null
  }
}

/**
 * Gibt die offene Partie auf. Ein veralteter Halbzug (zweiter Tab) wird einmal mit dem
 * mitgeschickten Zustand wiederholt; ist die Partie schon beendet, gilt das als erledigt.
 */
export async function gibPartieAuf(z: ZustandDto): Promise<void> {
  const versuche = async (halbzug: number, nochmal: boolean): Promise<void> => {
    try {
      await api.post<ZustandDto>(`/partien/${z.id}/aufgeben`, { halbzug } satisfies HalbzugAnfrage)
    } catch (e) {
      if (!(e instanceof ApiFehler)) throw e
      if (e.fehler === 'PartieBeendet' || e.status === 404) return
      const neu = e.antwort?.zustand
      if (nochmal && e.fehler === 'HalbzugVeraltet' && neu) return versuche(neu.halbzug, false)
      throw e
    }
  }
  await versuche(z.halbzug, true)
  if (gastPartieId.lies() === z.id) gastPartieId.setze(null)
}
