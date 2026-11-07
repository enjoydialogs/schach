// Rechtstext, nicht rechtlich geprüft.
//
// Impressum, 1:1 aus GladiusEtPilum `Views/Legal/Impressum.de.cshtml` (SCHACH-SPEC 12.2).
// Bewusst kurz. § 5 DDG verlangt von einem privaten Anbieter Name, ladungsfähige Anschrift und zwei
// schnelle Kontaktwege (Betreiber.tsx; hier nur E-Mail, ohne das Gladius-Kontaktformular). Was eine Vorlage sonst ergänzt, wäre hier falsch:
// - kein Handelsregister, keine USt-IdNr (enjoy dialogs ist keine eingetragene Firma);
// - kein Link zur OS-Plattform der EU: am 20. Juli 2025 abgeschaltet (VO (EU) 2024/3228);
// - kein Hinweis nach § 36 VSBG (Pflicht erst ab zehn Beschäftigten);
// - kein "Verantwortlich nach § 18 Abs. 2 MStV": keine journalistisch-redaktionellen Inhalte;
// - keine Haftungsausschlüsse (zitieren das aufgehobene TMG, ohne Rechtswirkung).

import { t } from '../i18n/de'
import { Betreiber } from './Betreiber'
import { Rechtstext } from './Rechtstext'

export function Impressum() {
  return (
    <Rechtstext titel={t('recht.impressumTitel')}>
      <p>Angaben gemäß § 5 Digitale-Dienste-Gesetz (DDG)</p>

      <h2>Anbieter</h2>
      <Betreiber />
    </Rechtstext>
  )
}
