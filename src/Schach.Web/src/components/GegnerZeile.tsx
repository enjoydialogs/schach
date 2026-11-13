import type { BilanzDto, GegnerDto } from '../api/types'
import { de } from '../i18n/de'
import { fuelle } from '../partie/text'
import { Avatar } from './Avatar'
import './bausteine.css'

/** „Siege 3 · Remis 1 · Niederlagen 2“; Screenreader hoeren „Siege: 3, …“ ohne Pluralfehler. */
export function Bilanz({ bilanz }: { bilanz: BilanzDto }) {
  return (
    <span className="bilanz" aria-label={fuelle(de.gegnerzeile.bilanzLang, { siege: bilanz.siege, remis: bilanz.remis, niederlagen: bilanz.niederlagen })}>
      {fuelle(de.gegnerzeile.bilanz, { siege: bilanz.siege, remis: bilanz.remis, niederlagen: bilanz.niederlagen })}
    </span>
  )
}

/**
 * Gegnerwahl-Zeile (SCHACH-SPEC 4.5): Kreis 48 px, Name, „Stufe · Elo“, Vorstellungssatz, eigene
 * Bilanz (Siege, Remis, Niederlagen) und Besiegt-Haekchen. Gaeste sehen keine Bilanz.
 */
export function GegnerZeile({ gegner, gast, onWaehlen }: { gegner: GegnerDto; gast: boolean; onWaehlen: () => void }) {
  const bilanz = gast ? null : gegner.bilanz
  const besiegt = bilanz !== null && bilanz.besiegtAm !== null
  return (
    <button type="button" className="zeile gegnerzeile" onClick={onWaehlen}>
      <Avatar gegner={gegner} groesse={48} besiegt={besiegt} />
      <span className="zeile__mitte">
        <span className="zeile__titel">
          {gegner.name}
          {besiegt && <span className="sr-only"> ({de.gegnerzeile.besiegt})</span>}
        </span>
        <span className="zeile__klein">
          {fuelle(de.gegnerzeile.stufeElo, { stufe: de.stufe[gegner.stufe], elo: gegner.elo })}
        </span>
        <span className="gegnerzeile__vorstellung">„{gegner.vorstellung}“</span>
        {bilanz !== null && <Bilanz bilanz={bilanz} />}
      </span>
    </button>
  )
}
