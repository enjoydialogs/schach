# AVV-Übersicht

Index der Auftragsverarbeitungsverträge (DSGVO Art. 28), eine Zeile je Auftragsverarbeiter
(SCHACH-SPEC 12.6). Format nach Kronrat `docs/legal/avv-uebersicht.md`. Der Betreiber füllt die
Zeilen.

**Dieser Index beweist die Ablage, nicht ihre rechtliche Wirkung.** Er ist nicht rechtlich geprüft.
Keine Zeile sagt „Art. 28 erfüllt“.

## Regeln

- **Kopien** liegen unter `docs/legal/avv/<anbieter>/`. Von Bestellmails und Rechnungen steht nur der
  Verweis im Repository, nie das Dokument.
- **Nachgeprüft wird vierteljährlich** und bei jeder Mitteilung eines Anbieters; Anbieter ändern ihre
  Dokumente an Ort und Stelle.
- **Ein neuer Auftragsverarbeiter bekommt seine Zeile, bevor er an eine echte Person sendet.** Die
  Datenschutzerklärung (`src/Schach.Web/src/recht/Datenschutz.tsx`) nennt dieselben Anbieter.
- **Die Mistral-Zeile ist erst vollständig mit Mistrals schriftlicher Bestätigung**, dass die
  kommerziellen Bedingungen und das DPA für dieses Konto gelten. Ihr Datum steht in der Spalte
  „Nachweis des Zustandekommens“.
- **Mistral-Workspace:** eigener Workspace „schach“ mit eigenem Schlüssel, Ausgabelimit und
  abgeschalteter Datenweitergabe (SCHACH-SPEC 6.3, 11.1, 12.6). Ein Bildschirmfoto der Einstellung
  liegt unter `docs/legal/avv/mistral/`.
- **Keine Stand-Marke der Rechtstexte hier.** Der Stand der Datenschutzerklärung steht auf der Seite
  selbst (`src/Schach.Web/src/i18n/de.ts`, `recht.datenschutzStand`).

## Format

- Spalten und die Werte der Spalte „Schlüssel“ werden nicht umbenannt, umgestellt oder entfernt.
- Eine Tatsache je Zelle. Eine leere Zelle heißt „nicht abgelegt“.
- „Kopie“ und „SHA-256“ meinen die datierte Kopie des DPA. Weitere Dokumente (kommerzielle
  Bedingungen, Anhänge, Bildschirmfotos) liegen im selben Ordner `docs/legal/avv/<anbieter>/`, ohne
  eigene Spalte.
- Datumsangaben als `JJJJ-MM-TT`.
- SHA-256 als 64 Hexadezimalzeichen in Kleinschreibung, über die Datei in der Spalte „Kopie“.
- Pfade relativ zur Wurzel des Repositorys, ohne Link-Syntax.
- „Endpunkt oder Ort“: für einen Dienst, den die Anwendung über das Netz ruft, nur der Host (etwa
  `api.example.eu`), ohne Schema, Pfad oder Schlüssel; sonst der Ort (Land, Rechenzentrum).
- Nie Servernamen, IPs, Subnetze oder Panel-URLs in diese Tabelle.

## Index

| Schlüssel | Rolle | Anbieter | Rechtsperson (Kontoinhaber) | DPA-URL | DPA-Stand | Kopie | Abrufdatum | SHA-256 | Unterauftragsverarbeiter-Liste | Stand der Liste | Nachweis des Zustandekommens | Endpunkt oder Ort | Offene Frage |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| `hostinger` | Hosting und Datenbank | Hostinger | | | | | | | | | | Deutschland | |
| `lettermint` | Mailversand (Anmelde-Mail) | Lettermint | | | | | | | | | | | |
| `mistral` | Sprachmodell (Sprüche) | Mistral | | | | | | | | | | `api.eu.mistral.ai` | |
| `sicherungsziel` | Externes Sicherungsziel (Postgres-Backup) | Hetzner (Object Storage) | | | | | | | | | | | |
