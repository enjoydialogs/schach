// Rechtstext, nicht rechtlich geprüft.
//
// Datenschutzerklärung nach SCHACH-SPEC 12.1, Aufbau aus GladiusEtPilum
// `Views/Legal/Datenschutz.de.cshtml`. Jeder Abschnitt beschreibt, was die Spec festlegt; ändert sich
// der Code, ändert sich dieser Text mit:
// - §2: App-Logs ohne IP und ohne E-Mail (14.3). IPs nur im Arbeitsspeicher der Limiter
//   `magic-link-je-ip` und `gast-partie-je-ip` (11.2, 11.4). Proxy-Log höchstens 14 Tage.
// - §3: Gast-Partien werden beim Ende gelöscht, offen nach 24 h ohne Zug (Sweep C, 8.9).
// - §4: Tabellen `Anmeldung`, `AnmeldungVersuch`, `Spieler`, `Partie`, `Zug`, `Spruch`, `Bilanz`
//   (8.12); Sweep D; Konto löschen und Export (9.7, 12.4). S2a muss das gegen den Code prüfen.
// - §5: Lettermint mit abgeschaltetem Öffnungs- und Klick-Tracking (9.6); Reply-To support@.
// - §7: Kontakt nur per E-Mail, kein Formular, kein Aidbase (Betreiber, PR #45).
// - §6: die Datenfelder aus 6.3 Punkt 4, wörtlich in Worten; nie FEN, nie Spieler-Identität.
//   Ein neues Feld im Prompt heißt ein neuer Punkt in der Liste hier.
// - §8: genau die Speicher aus 12.3 (`SpeicherSchluessel`). Ein neuer Schlüssel, ein neues Cookie
//   oder eine fremde Ressource heißt eine neue Zeile hier und womöglich ein Banner nach § 25 TDDDG.
// - §9: Hostinger (Rechenzentrum Deutschland), Backup nach Hetzner Object Storage in Falkenstein,
//   30 Tage (14.1; Standort vom Betreiber bestätigt, PR #45).
// Im Zweifel beschreibt dieser Text mehr, als geschieht, nie weniger.

import { t } from '../i18n/de'
import { Betreiber } from './Betreiber'
import { SUPPORT_EMAIL } from './kontakt'
import { Rechtstext } from './Rechtstext'

const SPEICHER: { name: string; art: string; zweck: string; dauer: string }[] = [
  {
    name: 'schach_sitzung',
    art: 'Cookie (HttpOnly, Secure, SameSite Lax)',
    zweck: 'Hält Sie nach der Anmeldung angemeldet.',
    dauer: '180 Tage, verlängert sich bei Nutzung; bis zum Abmelden',
  },
  {
    name: '.AspNetCore.Antiforgery.…',
    art: 'Cookie',
    zweck: 'Schützt vor untergeschobenen Anfragen (Cross-Site Request Forgery), auch beim Spiel als Gast.',
    dauer: 'Bis zum Schließen des Browsers',
  },
  {
    name: 'Service-Worker-Precache',
    art: 'Cache Storage',
    zweck: 'Programm der App, Schrift, Figurenbilder und Rechtstexte, damit die App auch ohne Verbindung startet.',
    dauer: 'Bis zur nächsten Version der App oder bis Sie die Websitedaten löschen',
  },
  {
    name: 'schach-partien-v1',
    art: 'Cache Storage',
    zweck: 'Ihre beendeten Partien, damit Sie sie ohne Verbindung nachspielen können.',
    dauer: 'Bis zum Abmelden oder zur Löschung des Kontos',
  },
  {
    name: 'gastPartieId',
    art: 'localStorage',
    zweck: 'Kennung Ihrer laufenden Gast-Partie, damit Sie sie fortsetzen können.',
    dauer: 'Bis die Partie endet oder Sie sich anmelden',
  },
  {
    name: 'einstellungen',
    art: 'localStorage',
    zweck: 'Ihre Einstellungen: erlaubte Züge anzeigen, vor ungedeckten Figuren warnen, Töne und Vibration.',
    dauer: 'Bis Sie die Websitedaten löschen',
  },
  {
    name: 'installHinweis',
    art: 'localStorage',
    zweck: 'Zeitpunkt, zu dem Sie den Hinweis zum Installieren der App auf später verschoben haben.',
    dauer: 'Bis Sie die Websitedaten löschen',
  },
  {
    name: 'anmeldungId',
    art: 'sessionStorage',
    zweck: 'Verknüpft bei der Anmeldung per Code Ihre Eingabe mit Ihrer Anfrage.',
    dauer: 'Bis zum Schließen des Tabs',
  },
]

export function Datenschutz() {
  return (
    <Rechtstext titel={t('recht.datenschutzTitel')} stand={t('recht.datenschutzStand')}>
      <p>
        Schach ist ein Lernspiel für den Browser. Wir verarbeiten nur die Daten, die wir brauchen, um
        Ihnen Partien gegen unsere Gegner und auf Wunsch ein Spielerkonto bereitzustellen und das Spiel
        sicher zu betreiben. Wir setzen keine Analyse- oder Tracking-Werkzeuge ein, zeigen keine Werbung
        und binden keine Inhalte von Drittanbietern ein: keine externen Schriftarten, keine fremden
        Skripte, keine Social-Media-Plugins.
      </p>

      <h2>1. Verantwortlicher</h2>
      <p>
        Verantwortlich für die Verarbeitung Ihrer personenbezogenen Daten im Sinne der
        Datenschutz-Grundverordnung (DSGVO) ist:
      </p>
      <Betreiber />
      <p>Mit allen Fragen zum Datenschutz wenden Sie sich bitte an diese E-Mail-Adresse.</p>

      <h2>2. Aufruf der Website</h2>
      <p>
        Wenn Sie die Website aufrufen, übermittelt Ihr Browser technisch bedingt Daten an unseren
        Server: Ihre IP-Adresse, Datum und Uhrzeit, die aufgerufene Adresse, die Kennung Ihres Browsers
        (User-Agent) und gegebenenfalls die zuvor besuchte Seite (Referrer). Wir verarbeiten diese
        Daten, um die Website auszuliefern, sie gegen Angriffe zu schützen und Fehler zu finden.
      </p>
      <p>
        Diese Angaben speichert der vorgeschaltete Webserver (Reverse-Proxy) in einem Protokoll, das
        nach spätestens 14 Tagen gelöscht wird. Länger bewahren wir einzelne Einträge nur auf, solange
        sie zur Aufklärung eines konkreten Sicherheitsvorfalls nötig sind. Die App selbst speichert
        Ihre IP-Adresse nicht; ihre Protokolle enthalten weder IP-Adressen noch E-Mail-Adressen. Um
        das Spiel vor automatisierten Massenanfragen zu schützen, zählt der Server neue Gast-Partien
        und Anmelde-Anfragen je IP-Adresse; dafür hält er die IP-Adresse kurzzeitig im
        Arbeitsspeicher, ohne sie zu speichern oder einer Partie zuzuordnen.
      </p>
      <p>
        Rechtsgrundlage ist Art. 6 Abs. 1 lit. f DSGVO. Unser berechtigtes Interesse ist ein sicherer
        und störungsfreier Betrieb der Website.
      </p>

      <h2>3. Als Gast spielen</h2>
      <p>
        Sie können ohne Konto spielen. Dafür fragen wir keine Angaben zu Ihrer Person ab. Die Partie
        (Züge, Bedenkzeit, Sprüche des Gegners) liegt nur während des Spiels auf unserem Server. Sie
        wird gelöscht, sobald die Partie endet, spätestens aber 24 Stunden nach dem letzten Zug. Damit
        Sie eine unterbrochene Partie fortsetzen können, merkt sich Ihr Browser ihre Kennung
        (Abschnitt 8). Gast-Partien zählen nicht für eine Wertung.
      </p>
      <p>
        Rechtsgrundlage ist Art. 6 Abs. 1 lit. b DSGVO: Wir brauchen diese Daten, um Ihnen die Partie
        bereitzustellen.
      </p>

      <h2>4. Spielerkonto und Anmeldung per Link</h2>
      <p>
        Mit einem Spielerkonto werden Ihre Partien gespeichert und zählen für Ihre Elo-Zahl. Ein
        Passwort gibt es nicht. Sie geben Ihre E-Mail-Adresse ein, und wir schicken Ihnen einen Link
        und einen sechsstelligen Code. Für diese Anfrage speichern wir Ihre E-Mail-Adresse sowie Link
        und Code nur als kryptografischen Hashwert. Link und Code gelten 15 Minuten und nur einmal.
        Die Anfrage wird gelöscht, sobald Sie sie bestätigen oder eine neue stellen, spätestens aber
        nach 24 Stunden, in der Regel schon nach einer Stunde.
      </p>
      <p>
        Um zu verhindern, dass jemand an Ihre Adresse massenhaft Mails auslöst, zählen wir die Anfragen
        je Adresse. Dafür speichern wir nur einen Hashwert der Adresse mit dem Zeitpunkt der Anfrage,
        nie die Adresse selbst, und löschen ihn nach 24 Stunden.
      </p>
      <p>
        Bestätigen Sie die Anmeldung zum ersten Mal, legen wir Ihr Konto an. Dazu gehören Ihre
        E-Mail-Adresse, der Zeitpunkt der Anlage und der letzten Anmeldung, Ihre Elo-Zahl und ein
        Sicherheitsmerkmal, mit dem Sie sich auf allen Geräten abmelden können. Zu Ihrem Konto
        speichern wir Ihre Partien mit allen Zügen, Ergebnis, Bedenkzeit, den Sprüchen der Gegner und
        der Änderung Ihrer Elo-Zahl sowie Ihre Bilanz je Gegner. Andere Personen sehen nichts davon.
      </p>
      <p>
        Im Profil können Sie Ihre Daten jederzeit als JSON-Datei herunterladen: Ihr Konto, Ihre
        Elo-Zahl, Ihre Bilanz je Gegner und alle Partien mit Zügen (als PGN) und Sprüchen. Ihre
        Kontodaten speichern wir, bis Sie Ihr Konto löschen; auch das können Sie dort selbst tun. Dabei
        löschen wir sofort und vollständig alles, was zu Ihrem Konto gehört, auch offene
        Anmelde-Anfragen und die Zähler zu Ihrer Adresse. Nur im Protokoll des Webservers
        (Abschnitt 2) und in den Datensicherungen (Abschnitt 9) bleiben Daten bis zu deren Löschfristen
        erhalten.
      </p>
      <p>
        Rechtsgrundlage ist Art. 6 Abs. 1 lit. b DSGVO. Ohne E-Mail-Adresse können wir kein Konto
        einrichten. Der Schutz vor massenhaften Mails stützt sich auf Art. 6 Abs. 1 lit. f DSGVO;
        unser berechtigtes Interesse ist, dass unser Dienst nicht zum Belästigen Dritter missbraucht
        wird.
      </p>

      <h2>5. E-Mails</h2>
      <p>
        Wir schreiben Ihnen nur, wenn Sie sich anmelden: eine Mail mit Link und Code. Newsletter oder
        Werbung versenden wir nicht. Antworten Sie auf diese Mail, geht Ihre Antwort an{' '}
        <a href={`mailto:${SUPPORT_EMAIL}`}>{SUPPORT_EMAIL}</a>; für sie gilt Abschnitt 7.
      </p>
      <p>
        Für den Versand nutzen wir Lettermint der Lettermint B.V., Willemsvaart 16 B, 8019 AB Zwolle,
        Niederlande. Lettermint verarbeitet dabei Ihre E-Mail-Adresse, den Inhalt der Nachricht und
        technische Versanddaten wie Zeitpunkt und Zustellstatus in unserem Auftrag (Art. 28 DSGVO).
        Lettermint verarbeitet und speichert diese Daten nur in der Europäischen Union. Die Öffnungs-
        und Klickverfolgung ist für unsere E-Mails abgeschaltet: Sie enthalten kein Zählpixel, und ihr
        Link führt direkt zu uns.
      </p>
      <p>Rechtsgrundlage ist Art. 6 Abs. 1 lit. b DSGVO.</p>

      <h2>6. Sprüche und Sprachmodell</h2>
      <p>
        Die Gegner kommentieren die Partie mit kurzen Sprüchen. Diese Sprüche erzeugt ein Sprachmodell
        der Mistral AI, 15 rue des Halles, 75001 Paris, Frankreich, in unserem Auftrag
        (Art. 28 DSGVO). Wir rufen es über den Endpunkt in der Europäischen Union auf:{' '}
        <code>api.eu.mistral.ai</code>. Die Anfrage stellt unser Server, nicht Ihr Browser; Mistral
        erfährt daher weder Ihre IP-Adresse noch Ihre Browserdaten. Das gilt für Gäste und
        Kontoinhaber gleichermaßen.
      </p>
      <p>Wir senden Mistral eine Anweisung mit der Persönlichkeit des Gegners und diese Spieldaten:</p>
      <ul>
        <li>das Ereignis, das der Spruch kommentiert (etwa Schach, Figurengewinn oder Partieende),</li>
        <li>die Zugnummer und die Phase der Partie,</li>
        <li>mit welcher Farbe Sie spielen und ob mit Bedenkzeit,</li>
        <li>Ihren letzten Zug und die Antwort des Gegners in Schachnotation,</li>
        <li>ob Schach geboten wurde,</li>
        <li>die Änderung des Materials und die Materialbilanz,</li>
        <li>eine Einschätzung der Stellung,</li>
        <li>Merkmale des Gegnerzugs,</li>
        <li>bei Partieende oder abgelehntem Remis den Grund,</li>
        <li>bis zu drei der letzten Sprüche des Gegners.</li>
      </ul>
      <p>
        <strong>Nie gesendet</strong> werden die vollständige Stellung (FEN), Ihr Name, Ihre
        E-Mail-Adresse, Ihre Elo-Zahl und die Kennung Ihres Kontos.
      </p>
      <p>
        Wir nutzen bei Mistral einen eigenen Arbeitsbereich nur für dieses Spiel, in dem die
        Datenweitergabe abgeschaltet ist. Ist Mistral nicht erreichbar oder zu langsam, zeigt das Spiel
        einen vorbereiteten Spruch. Die Sprüche speichern wir mit der Partie (Abschnitte 3 und 4).
      </p>
      <p>Rechtsgrundlage ist Art. 6 Abs. 1 lit. b DSGVO: Die Sprüche sind Teil des Spiels.</p>

      <h2>7. Kontakt</h2>
      <p>
        Sie erreichen uns per E-Mail; ein Kontaktformular gibt es nicht. Wenn Sie uns schreiben,
        verarbeiten wir Ihre E-Mail-Adresse, Ihre Nachricht und die Angaben, die Sie selbst machen.
      </p>
      <p>
        Wir nutzen diese Daten nur, um Ihre Anfrage zu beantworten, und löschen sie, sobald sie
        erledigt ist und keine gesetzliche Aufbewahrungspflicht entgegensteht. Rechtsgrundlage ist
        Art. 6 Abs. 1 lit. b DSGVO, wenn Ihre Anfrage Ihr Spielerkonto betrifft, sonst Art. 6 Abs. 1
        lit. f DSGVO; unser berechtigtes Interesse ist, Anfragen beantworten zu können.
      </p>

      <h2>8. Cookies und Browser-Speicher</h2>
      <p>
        Wir legen auf Ihrem Gerät nur ab, was für den Betrieb unbedingt erforderlich ist: zwei Cookies
        und einige Einträge im Speicher Ihres Browsers (localStorage, sessionStorage und Cache
        Storage). Dafür ist nach § 25 Abs. 2 Nr. 2 Telekommunikation-Digitale-Dienste-Datenschutz-Gesetz
        (TDDDG) keine Einwilligung nötig, deshalb gibt es auch kein Cookie-Banner. Tracking- oder
        Werbe-Cookies verwenden wir nicht. Die Einträge im Browser-Speicher verlassen Ihr Gerät nicht,
        mit Ausnahme der Kennung einer Gast-Partie, die der Browser beim Fortsetzen an uns sendet.
      </p>
      <div className="recht__tabelle-rahmen">
        <table>
          <thead>
            <tr>
              <th>Name</th>
              <th>Art</th>
              <th>Zweck</th>
              <th>Gültigkeit</th>
            </tr>
          </thead>
          <tbody>
            {SPEICHER.map((s) => (
              <tr key={s.name}>
                <td>
                  <code>{s.name}</code>
                </td>
                <td>{s.art}</td>
                <td>{s.zweck}</td>
                <td>{s.dauer}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <p>
        Rechtsgrundlage für die damit verbundene Verarbeitung ist Art. 6 Abs. 1 lit. b DSGVO (Spiel und
        Anmeldung) und Art. 6 Abs. 1 lit. f DSGVO (Schutz vor untergeschobenen Anfragen).
      </p>

      <h2>9. Hosting und Datensicherung</h2>
      <p>
        Die Website und ihre Datenbank laufen auf einem Server der Hostinger International Ltd.,
        61 Lordou Vironos Street, 6023 Larnaca, Zypern, in einem Rechenzentrum in Deutschland. Die
        Datenbank sichern wir täglich in einen Speicher der Hetzner Online GmbH in Falkenstein
        (Deutschland). Jede
        Sicherung wird nach spätestens 30 Tagen gelöscht; bis dahin kann sie noch Daten eines
        inzwischen gelöschten Kontos enthalten. Hostinger und Hetzner verarbeiten die Daten
        ausschließlich in unserem Auftrag (Art. 28 DSGVO).
      </p>

      <h2>10. Weitergabe</h2>
      <p>
        Außer den hier genannten Dienstleistern erhält niemand Ihre Daten, es sei denn, wir sind
        gesetzlich dazu verpflichtet. Wir verkaufen keine Daten.
      </p>

      <h2>11. Ihre Rechte</h2>
      <p>Sie haben das Recht,</p>
      <ul>
        <li>Auskunft über die zu Ihnen gespeicherten Daten zu erhalten (Art. 15 DSGVO),</li>
        <li>unrichtige Daten berichtigen zu lassen (Art. 16 DSGVO),</li>
        <li>Ihre Daten löschen zu lassen (Art. 17 DSGVO),</li>
        <li>die Verarbeitung einschränken zu lassen (Art. 18 DSGVO) und</li>
        <li>
          die Daten, die Sie uns bereitgestellt haben, in einem gängigen, maschinenlesbaren Format zu
          erhalten (Art. 20 DSGVO).
        </li>
      </ul>
      <p>
        Auskunft und Datenkopie (Art. 15 und 20) sowie die Löschung Ihres Kontos (Art. 17) erledigen
        Sie selbst im Profil: „Daten exportieren“ und „Konto löschen“. Für alles Übrige, oder wenn Sie
        das lieber möchten, schreiben Sie uns an die oben genannte E-Mail-Adresse.
      </p>
      <div className="recht__widerspruch">
        <p>
          <strong>Widerspruchsrecht (Art. 21 DSGVO)</strong>
        </p>
        <p>
          Soweit wir Daten auf Grundlage unseres berechtigten Interesses verarbeiten (Art. 6 Abs. 1
          lit. f DSGVO, hier das Protokoll des Webservers, die Zählung von Anfragen je IP-Adresse und
          je E-Mail-Adresse, der Schutz vor untergeschobenen Anfragen und Anfragen ohne Kontobezug),
          können Sie dieser Verarbeitung aus Gründen, die sich aus Ihrer besonderen Situation ergeben,
          jederzeit widersprechen. Wir verarbeiten die Daten dann nicht mehr, es sei denn, wir können
          zwingende schutzwürdige Gründe nachweisen, die Ihre Interessen überwiegen, oder die
          Verarbeitung dient der Geltendmachung, Ausübung oder Verteidigung von Rechtsansprüchen.
        </p>
      </div>
      <p>
        Außerdem können Sie sich bei einer Datenschutz-Aufsichtsbehörde beschweren (Art. 77 DSGVO),
        insbesondere in dem EU-Mitgliedstaat Ihres Aufenthaltsorts, Ihres Arbeitsplatzes oder des Orts
        des mutmaßlichen Verstoßes.
      </p>

      <h2>12. Keine automatisierten Entscheidungen</h2>
      <p>
        Wir treffen keine automatisierten Entscheidungen im Sinne von Art. 22 DSGVO und erstellen keine
        Profile. Ihre Elo-Zahl beschreibt nur Ihre Spielstärke im Spiel und hat keine Folgen außerhalb
        davon.
      </p>

      <h2>13. Änderungen</h2>
      <p>
        Ändert sich das Spiel oder die Art, wie wir Daten verarbeiten, passen wir diese Erklärung an. Es
        gilt die hier veröffentlichte Fassung.
      </p>
    </Rechtstext>
  )
}
