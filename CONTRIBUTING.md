# 🤝 Mitmachen bei Schach

Schön, dass du da bist! 🎉 Diese Seite erklärt, wie ein Beitrag vom Issue bis zum Merge kommt.

Mit deiner Teilnahme stimmst du dem [Verhaltenskodex](CODE_OF_CONDUCT.md) zu. 💜

---

## 🗣️ Sprache und Begriffe

- 🇩🇪 Issues, PRs, Spec und Spieltexte sind auf Deutsch.
- 📖 Begriffe nach [`GLOSSARY.md`](GLOSSARY.md). Schreib *Gegner*, nicht *Bot* oder *KI*. Schreib *Partie*, nicht *Match*. Die `_Avoid_`-Zeilen im Glossar sagen, was du meiden sollst.
- 💻 Bezeichner in Code, API und Dateinamen bleiben wie in der Spec angegeben.

---

## 🧭 Ablauf

1. 🔍 **Issue suchen oder anlegen.** Jede Änderung beginnt mit einem Issue. Neue Issues bekommen `needs-triage`.
2. 🌱 **Branch von `main`.** Ein Issue = ein Branch = ein PR.
3. 🛠️ **Umsetzen und testen** (siehe unten).
4. 🔄 **`main` aufnehmen per Merge:**

   ```bash
   git fetch origin && git merge origin/main
   ```

   ⛔ Kein Rebase, kein Force-Push.
5. 📬 **PR öffnen.** Die Vorlage fragt alles ab, was der Betreiber braucht.
6. ✅ **Merge** nur bei grüner CI und `behind_by = 0`. Der Betreiber merged, als Merge-Commit.

Zu groß für einen PR? Kommentiere das Issue und schlag eine Aufteilung vor. ✂️

---

## 🧰 Werkzeuge

| Werkzeug | Version | Quelle |
|---|---|---|
| 🟣 .NET SDK | 10.0.x | [`global.json`](global.json) |
| 🟢 Node | 22 | [`.nvmrc`](.nvmrc) |
| 🐍 Python | ab 3.12 | [`engine/pyproject.toml`](engine/pyproject.toml) |
| 💻 PowerShell | 7.4 | für `ci/local-ci.ps1` |
| 🔑 gitleaks | aktuell | Geheimnis-Scan |
| 📜 shellcheck | optional lokal | prüft `*.sh` |
| 🐳 Docker | mit Compose | nur für Container-Tests und den lokalen Stack |

---

## 🧪 Vor dem PR

Führe die CI lokal aus. Sie läuft nur die Stufen, die deine Änderung betrifft:

```bash
pwsh ci/local-ci.ps1
```

Das prüft unter anderem:

- 🔨 **Build:** `dotnet build -c Release`. Warnungen sind Fehler (`TreatWarningsAsErrors`).
- 🧹 **Format:** `dotnet format --verify-no-changes`. Stil steht in [`.editorconfig`](.editorconfig).
- 🧪 **Tests:** .NET (Microsoft.Testing.Platform), Vitest, pytest.
- 🛡️ **Pakete:** keine bekannten Lücken ab High.
- 🔑 **Geheimnisse:** gitleaks findet nichts.

🐳 Container-Tests (`[ContainerFact]`) laufen nur in der Cloud-CI. Lokal blendest du sie aus:

```bash
dotnet test --solution Schach.slnx --filter-not-trait "Category=Container"
```

### ✍️ C#-Stil

- ✅ `is null` / `is not null` statt `== null` / `!= null`
- ✅ Nullable-Referenztypen und .NET-Analyzer sind an (`latest-recommended`).
- ✅ Zeit über `TimeProvider`, nie `DateTime.Now`. So bleiben Uhr-Tests deterministisch.
- ✅ Paketversionen nur zentral in [`Directory.Packages.props`](Directory.Packages.props) (Central Package Management).

---

## 🧊 Eingefrorene Dateien

Diese Dateien ändern sich nur per eigenem, kleinem Gerüst-PR (SCHACH-SPEC 15.1 Regel 4):

- `Schach.slnx`, `Directory.Packages.props`
- `src/Schach.Application/Abstractions/*`, `src/Schach.Api/Contracts/*`
- `engine/API.md`
- `Dockerfile`, `engine/Dockerfile`, `docker-compose*.yml`
- `.github/workflows/*`, `ci/*`

## 📝 Append-Register

In diesen Dateien nur anhängen, nie umsortieren (Regel 5). So kollidieren parallele PRs nicht:

- `Directory.Packages.props`, `Program.cs` (ein DI-Block je PR)
- `router.tsx`, `i18n/de.ts`, `api/types.ts`
- `THIRD-PARTY-NOTICES.md`, `tests/fen-suite/*.json`, `ControllerAuthorizationTests`

## 🗄️ Migrationen

Jede Schemaänderung ist eine eigene Vorwärts-Migration auf aktuellem `main`. Merge nie einen Model-Snapshot von Hand.

---

## 📦 Neue Abhängigkeiten

1. Version in `Directory.Packages.props` (oder `package.json`, `pyproject.toml`) eintragen.
2. Lizenz prüfen. Sie muss zum Zielordner passen: MIT-Teil oder AGPL-Teil.
3. Eintrag an [`THIRD-PARTY-NOTICES.md`](THIRD-PARTY-NOTICES.md) anhängen.

---

## ⚖️ Lizenz deiner Beiträge

- 📄 Beiträge außerhalb von `engine/` stehen unter [MIT](LICENSE).
- 📄 Beiträge in `engine/` stehen unter [AGPL-3.0-or-later](engine/LICENSE).

---

## 🔒 Öffentliches Repo

Keine Geheimnisse, Server-IPs, Subnetze oder Panel-URLs in Code, Commits, Issues oder Logs. 🙅 Sicherheitslücken meldest du nach [`SECURITY.md`](SECURITY.md), nicht als Issue.

---

Danke, dass du Schach besser machst! ♟️🚀
