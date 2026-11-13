import { Chess } from 'chess.js'
import { describe, expect, it } from 'vitest'
import { erlaubteZuege, gegnerZieht, loesung, naechsterTipp, nimmZurueck, spielerZieht, starte, type Stand } from './ablauf'
import { BEREICHE, findeUebung, UEBUNGEN, type Uebung } from './katalog'

/** Spielt eine Uebung mit dem ersten Loesungszug je Schritt bis zum Ende. */
function spieleDurch(uebung: Uebung, wahl: (zuege: readonly string[]) => string = (z) => z[0]!): Stand {
  let stand = starte(uebung)
  for (let runde = 0; runde < 50 && stand.phase !== 'fertig'; runde++) {
    if (stand.phase === 'gegner') stand = gegnerZieht(stand, uebung)
    else stand = spielerZieht(stand, uebung, wahl(uebung.schritte[stand.schritt]!.zuege))
  }
  return stand
}

const farbeAmZug = (fen: string) => (new Chess(fen).turn() === 'w' ? 'Weiss' : 'Schwarz')

describe('Uebungs-Katalog', () => {
  it('hat eindeutige Keys und nur bekannte Bereiche', () => {
    const keys = UEBUNGEN.map((u) => u.key)
    expect(new Set(keys).size).toBe(keys.length)
    for (const u of UEBUNGEN) expect(BEREICHE).toContain(u.bereich)
    for (const b of BEREICHE) expect(UEBUNGEN.some((u) => u.bereich === b)).toBe(true)
  })

  it.each(UEBUNGEN.map((u) => [u.key, u] as const))('%s: jede Loesung ist erlaubt und der Spieler hat die richtige Farbe', (_, uebung) => {
    // Alle Zweige: jede Loesung jedes Schritts, Antworten des Gegners muessen dann ebenfalls passen.
    const anzahl = Math.max(...uebung.schritte.map((s) => s.zuege.length))
    for (let i = 0; i < anzahl; i++) {
      let stand = starte(uebung)
      for (let runde = 0; runde < 50 && stand.phase !== 'fertig'; runde++) {
        if (stand.phase === 'gegner') {
          const danach = gegnerZieht(stand, uebung)
          expect(danach, `Gegnerzug ${stand.ausstehend} in ${stand.fen}`).not.toBe(stand)
          stand = danach
        } else {
          expect(farbeAmZug(stand.fen)).toBe(uebung.farbe)
          const zuege = uebung.schritte[stand.schritt]!.zuege
          const uci = zuege[Math.min(i, zuege.length - 1)]!
          expect(erlaubteZuege(stand.fen), `Zug ${uci} in ${stand.fen}`).toContain(uci)
          stand = spielerZieht(stand, uebung, uci)
        }
      }
      expect(stand.phase).toBe('fertig')
      expect(stand.schritt).toBe(uebung.schritte.length)
    }
  })

  it('en passant: der Loesungszug ist ein en-passant-Schlag', () => {
    const u = findeUebung('en-passant')!
    const stand = gegnerZieht(starte(u), u)
    const zug = new Chess(stand.fen).move({ from: 'e5', to: 'd6' })
    expect(zug.isEnPassant()).toBe(true)
  })

  it('Rochade: Weiss und Schwarz rochieren', () => {
    const fen = spieleDurch(findeUebung('rochade')!).fen
    const brett = new Chess(fen)
    expect(brett.get('g1')?.type).toBe('k')
    expect(brett.get('f1')?.type).toBe('r')
    expect(brett.get('c8')?.type).toBe('k')
    expect(brett.get('d8')?.type).toBe('r')
  })

  it('Umwandlung: am Ende steht eine weisse Dame auf e8', () => {
    const brett = new Chess(spieleDurch(findeUebung('umwandlung')!).fen)
    expect(brett.get('e8')).toEqual({ type: 'q', color: 'w' })
  })

  it.each(['grundreihenmatt', 'treppenmatt'])('%s endet im Matt', (key) => {
    expect(new Chess(spieleDurch(findeUebung(key)!).fen).isCheckmate()).toBe(true)
  })

  it('Springergabel: erst Schach, dann ist die Dame weg', () => {
    const u = findeUebung('springergabel')!
    const nachGabel = spielerZieht(starte(u), u, 'd5e7')
    expect(new Chess(nachGabel.fen).inCheck()).toBe(true)
    expect(new Chess(spieleDurch(u).fen).findPiece({ type: 'q', color: 'b' })).toEqual([])
  })

  it('Schaefermatt abwehren: der Spieler ist Schwarz und f7 bleibt stehen, in beiden Zweigen', () => {
    const u = findeUebung('schaefermatt-abwehren')!
    expect(u.farbe).toBe('Schwarz')
    for (const zweig of ['g7g6', 'd8e7']) {
      const fen = spieleDurch(u, (z) => (z.includes(zweig) ? zweig : z[0]!)).fen
      const brett = new Chess(fen)
      expect(brett.get('f7')).toEqual({ type: 'p', color: 'b' })
      expect(brett.isCheckmate()).toBe(false)
    }
  })
})

describe('Uebungs-Ablauf', () => {
  const u = findeUebung('treppenmatt')!

  it('ein anderer erlaubter Zug wird gezeigt, mit Hinweis, und dann zurueckgenommen', () => {
    const start = starte(u)
    const falsch = spielerZieht(start, u, 'a2a3')
    expect(falsch.phase).toBe('falsch')
    expect(falsch.fen).not.toBe(start.fen)
    expect(falsch.text).toBe(u.schritte[0]!.hinweis)
    const zurueck = nimmZurueck(falsch)
    expect(zurueck.phase).toBe('amZug')
    expect(zurueck.fen).toBe(start.fen)
    expect(zurueck.schritt).toBe(0)
  })

  it('ein unmoeglicher Zug aendert nichts', () => {
    const start = starte(u)
    expect(spielerZieht(start, u, 'a2h8')).toBe(start)
  })

  it('richtiger Zug: Erklaerung, dann Antwort des Gegners', () => {
    const nach = spielerZieht(starte(u), u, 'b2b6')
    expect(nach.phase).toBe('gegner')
    expect(nach.text).toBe(u.schritte[0]!.erklaerung)
    expect(nach.ausstehend).toBe('e7d7')
    const antwort = gegnerZieht(nach, u)
    expect(antwort.phase).toBe('amZug')
    expect(antwort.letzterZug).toEqual({ von: 'e7', nach: 'd7' })
  })

  it('Tipp: erst Stufe 1, dann 2; nach dem Zug wieder aus', () => {
    const start = starte(u)
    const eins = naechsterTipp(start)
    expect(eins.tipp).toBe(1)
    expect(naechsterTipp(eins).tipp).toBe(2)
    expect(loesung(start, u)).toEqual({ von: 'b2', nach: 'b6', figur: 'Turm' })
    expect(spielerZieht(naechsterTipp(eins), u, 'b2b6').tipp).toBe(0)
  })

  it('mit Vorzug beginnt der Gegner', () => {
    const ep = findeUebung('en-passant')!
    const start = starte(ep)
    expect(start.phase).toBe('gegner')
    expect(naechsterTipp(start)).toBe(start)
    expect(gegnerZieht(start, ep).phase).toBe('amZug')
  })

  it('Umwandlung in eine andere Figur zaehlt als anderer Zug', () => {
    const um = findeUebung('umwandlung')!
    expect(spielerZieht(starte(um), um, 'e7e8n').phase).toBe('falsch')
  })
})
