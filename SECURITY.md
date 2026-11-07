# 🛡️ Sicherheit

Danke, dass du hilfst, Schach sicher zu halten! 🙏

## ✅ Unterstützte Versionen

| Version | Unterstützt |
|---|---|
| 🟢 `main` und die ausgelieferte Version auf `schach.enjoy-dialogs.de` | ✅ |
| 🔴 ältere Commits und Forks | ❌ |

## 📣 Lücke melden

⛔ **Bitte kein öffentliches Issue.** Schreib stattdessen eine E-Mail an:

📧 **info@enjoy-dialogs.de**, Betreff beginnt mit `[security]`

Hilfreich sind:

1. 📝 Was ist betroffen (Endpunkt, Datei, Komponente)?
2. 🔁 Schritte zum Nachstellen
3. 💥 Mögliche Auswirkung
4. 🩹 Falls vorhanden: ein Vorschlag zur Behebung

## 🔄 Was dann passiert

1. 📬 Du bekommst eine Eingangsbestätigung.
2. 🔍 Wir prüfen und bewerten die Meldung.
3. 🛠️ Die Behebung landet über einen PR auf `main` und wird ausgeliefert.
4. 🏅 Auf Wunsch nennen wir dich im Fix als Finder.

## 🎯 Im Fokus

- 🔐 Anmeldung per Magic Link und Code, Cookies, CSRF
- 🗄️ Zugriff auf Partien und Konten anderer Spieler
- 🚦 Umgehen von Rate-Limits und Budgetgrenzen
- 🔑 Geheimnisse im Repo oder in Images

## 🚫 Nicht im Fokus

- Denial of Service durch reine Last
- Social Engineering
- Lücken in Abhängigkeiten ohne Auswirkung auf Schach (gern trotzdem per Issue oder Renovate-PR)

## 🧰 Was das Projekt selbst tut

- 🔑 gitleaks scannt jeden PR und die Historie
- 🛡️ `dotnet list package --vulnerable` bricht die CI ab High ab
- 🤖 Renovate ist für Abhängigkeits-Updates konfiguriert (`renovate.json`)
- 🏗️ Nur GitHub-gehostete Runner, keine Self-hosted Runner
