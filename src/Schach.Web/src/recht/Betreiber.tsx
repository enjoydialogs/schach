// Rechtstext, nicht rechtlich geprüft.
//
// Der Betreiber, einmal festgehalten (aus GladiusEtPilum `Views/Legal/_Operator.cshtml`, 1:1).
// Das Impressum nennt ihn als Anbieter (§ 5 DDG), die Datenschutzerklärung als Verantwortlichen
// (Art. 13 Abs. 1 lit. a DSGVO); zwei Kopien einer Anschrift wären zwei Gelegenheiten zu veralten.
// Privatperson unter dem Namen "enjoy dialogs": voller Name und ladungsfähige Anschrift sind Pflicht.

import { KONTAKT_EMAIL, KONTAKTFORMULAR_URL } from './kontakt'

export function Betreiber() {
  return (
    <>
      <address>
        Christian Metz
        <br />
        enjoy dialogs
        <br />
        Albert-Einstein-Str. 6
        <br />
        87437 Kempten (Allgäu)
        <br />
        Deutschland
      </address>
      <p>
        E-Mail: <a href={`mailto:${KONTAKT_EMAIL}`}>{KONTAKT_EMAIL}</a>
        <br />
        Kontaktformular:{' '}
        <a href={KONTAKTFORMULAR_URL} target="_blank" rel="noopener noreferrer">
          Nachricht schreiben
        </a>
      </p>
    </>
  )
}
