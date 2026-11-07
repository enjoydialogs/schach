# ♟️ Schach

**Schach lernen durch Partien, nicht durch Lektionen.** 🎓

Ein Browserspiel fürs Handy: Du spielst Partien gegen Gegner mit eigener Persönlichkeit, und Lernhilfen begleiten dich dabei. 📱✨

[![CI](https://github.com/enjoydialogs/schach/actions/workflows/ci.yml/badge.svg)](https://github.com/enjoydialogs/schach/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![.NET 10](https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet)](global.json)

> 🚧 **Im Aufbau.** Das Projekt entsteht in Phasen nach [`docs/SCHACH-SPEC.md`](docs/SCHACH-SPEC.md). Was unten steht, ist der geplante Umfang, nicht der aktuelle Stand. Den Stand dokumentiert jeweils der Docs-PR, der eine Phase abschließt.

---

## 🎯 Worum es geht

| | |
|---|---|
| 🎭 **Gegner mit Persönlichkeit** | Jeder Gegner hat Namen, Persönlichkeit und Spielstärke, von Anfänger bis Meister. Er kommentiert Züge mit Sprüchen. |
| 💡 **Lernhilfen** | Erlaubte Züge anzeigen, Warnung vor einer ungedeckten Figur, Tipp auf Wunsch. |
| 📈 **Elo** | Spieler mit Konto bekommen eine Elo. Jeder Gegner hat eine feste Elo passend zu seiner Spielstärke. |
| ⏱️ **Bedenkzeit** | Optional beim Start wählbar. Ohne Bedenkzeit gibt es Zurücknehmen und Tipp. |
| 🔁 **Nachspielen** | Beendete Partien Zug für Zug ansehen. Bereits geladene Partien auch offline. |
| 🔐 **Magic Link** | Anmeldung per E-Mail-Link oder 6-stelligem Code. Kein Passwort. |
| 👤 **Als Gast spielen** | Sofort loslegen, ohne Konto. Gastpartien zählen nicht für die Elo. |
| 📲 **PWA** | Installierbar auf dem Startbildschirm. Referenzgerät: Pixel 7 im Hochformat. |

Begriffe wie *Gegner*, *Partie* oder *Lernhilfe* sind in [`GLOSSARY.md`](GLOSSARY.md) festgelegt. 📖

---

## 🏗️ Architektur

```text
📱 Browser (React-PWA)
      │  HTTP  api/v1
      ▼
🟣 Schach.Api (ASP.NET Core, .NET 10)  ──▶  🐘 PostgreSQL 17
      │  HTTP (engine/API.md)
      ▼
🐍 Engine-Dienst (FastAPI): Maia-3 · Stockfish · Fairy-Stockfish
```

| Ordner | Inhalt |
|---|---|
| 🧠 `src/Schach.Domain` | Regeln, Remis, Elo, Uhr. Legalität über Gera.Chess |
| ⚙️ `src/Schach.Application` | Anwendungsfälle und Abstraktionen |
| 🗄️ `src/Schach.Infrastructure` | EF Core, Postgres, Mail, Identity |
| 🌐 `src/Schach.Api` | HTTP-API, Auth, Rate-Limits, liefert die Web-App aus |
| 🎨 `src/Schach.Web` | React 19, Vite, TypeScript |
| ♞ `engine/` | Engine-Dienst in Python, **AGPL-3.0-or-later** |
| 🧪 `tests/` | .NET-Tests und die gemeinsame FEN-Suite |
| 🚦 `ci/` | Ein CI-Skript für lokal und Cloud |
| 🚀 `deploy/` | Betrieb über Dokploy |

---

## 🚀 Schnellstart

### ✅ Voraussetzungen

- 🟣 .NET SDK laut [`global.json`](global.json) (10.0.x)
- 🟢 Node 22 laut [`.nvmrc`](.nvmrc)
- 🐍 Python 3.12 oder neuer (nur für `engine/`)
- 🐳 Docker mit Compose
- 💻 PowerShell 7.4 (für `ci/local-ci.ps1`)

### ▶️ Lokal starten

1. Postgres und Engine starten:

   ```bash
   docker compose up -d
   ```

2. API starten (lauscht auf `http://localhost:5252`):

   ```bash
   dotnet run --project src/Schach.Api
   ```

3. Web-App im Entwicklungsmodus starten:

   ```bash
   cd src/Schach.Web && npm ci && npm run dev
   ```

4. Öffnen: `http://localhost:5173` 🎉 Vite leitet `/api` an die API auf `:5252` weiter.

🐳 Alles als Container, inklusive `web` auf Port 8080:

```bash
docker compose --profile full up -d
```

> ℹ️ Die Maia-3-Gewichte liegen nie im Repo oder im Image. Für Tests reicht `SCHACH_ENGINE_FAKE_MODEL=1`.

---

## 🧪 Tests

Ein Skript prüft alles, lokal wie in der Cloud:

```bash
pwsh ci/local-ci.ps1
```

| Aufruf | Wirkung |
|---|---|
| `pwsh ci/local-ci.ps1` | nur Stufen mit Änderungen gegenüber `origin/main` |
| `pwsh ci/local-ci.ps1 -All` | alle Stufen |
| `pwsh ci/local-ci.ps1 -Only web,build` | eine Auswahl |

Stufen: 🔑 `secrets` · 📜 `scripts` · 🧹 `format` · 🔨 `build` · 🧪 `test` · 🛡️ `vulns` · 🎨 `web` · ♞ `engine`. Details in [`ci/README.md`](ci/README.md).

🐳 Container-Tests (Testcontainers) laufen nur mit `SCHACH_CONTAINER_TESTS=1`, also in der Cloud-CI. Lokal ohne sie:

```bash
dotnet test --solution Schach.slnx --filter-not-trait "Category=Container"
```

---

## 📚 Dokumentation

| Dokument | Wofür |
|---|---|
| 📘 [`docs/SCHACH-SPEC.md`](docs/SCHACH-SPEC.md) | Die Spezifikation. Bei Widerspruch gilt sie. |
| 📖 [`GLOSSARY.md`](GLOSSARY.md) | Verbindliche Begriffe |
| 🔌 [`engine/API.md`](engine/API.md) | HTTP-Vertrag des Engine-Dienstes |
| 🚦 [`ci/README.md`](ci/README.md) | CI-Stufen und Werkzeuge |
| 🚀 [`deploy/README.md`](deploy/README.md) | Deploy und Rollback |
| 🤝 [`CONTRIBUTING.md`](CONTRIBUTING.md) | So arbeitest du mit |
| 🛡️ [`SECURITY.md`](SECURITY.md) | Sicherheitslücken melden |
| 💬 [`SUPPORT.md`](SUPPORT.md) | Hilfe bekommen |

---

## 🤝 Mitmachen

Beiträge sind willkommen! 🎉 Lies zuerst [`CONTRIBUTING.md`](CONTRIBUTING.md) und den [`CODE_OF_CONDUCT.md`](CODE_OF_CONDUCT.md). Offene Aufgaben für Menschen tragen das Label [`ready-for-human`](https://github.com/enjoydialogs/schach/labels/ready-for-human).

---

## ⚖️ Lizenz

- 📄 Alles außer `engine/`: [MIT](LICENSE)
- 📄 Engine-Dienst in `engine/`: [AGPL-3.0-or-later](engine/LICENSE)
- 📦 Fremdkomponenten und ihre Lizenzen: [`THIRD-PARTY-NOTICES.md`](THIRD-PARTY-NOTICES.md)

---

<p align="center">Gebaut mit ♟️, ☕ und viel 🧠 von <a href="https://github.com/enjoydialogs">enjoydialogs</a></p>
