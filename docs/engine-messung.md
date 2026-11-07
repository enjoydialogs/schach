# Engine-Messung und Eichung

Werkzeuge für VPS-Messung Teil 2 und Eichung (SCHACH-SPEC 5.4, 15.4 Kriterium 5, 16). Sie liegen in `engine/tools/` und sprechen den Engine-Dienst nur über HTTP an (`engine/API.md`).

| Werkzeug | Zweck | Wo |
|---|---|---|
| `messung.py` | Last wie im Spiel: p50/p95 je Endpunkt, 503- und Fehlerquoten, RSS, Erststart | VPS, gegen den laufenden Engine-Container |
| `eichung.py` | (a) Stärke je Gegner, (b) Stil gegen neutral, (c) Sichtbarkeit; Werte für `Engine:Eichung` | Entwicklungsrechner, Engine nativ in einer venv |
| `stil_referenz.py` | **Referenz** des Stil-Algorithmus 5.3, Nachbau von `Schach.Domain.Stil.Stilgewichtung` | nur für die Eichung |
| `engine_http.py` | HTTP-Client ohne Fremdpakete | von beiden genutzt |

Die Tests laufen mit dem Fake-Modell in der CI-Stufe `engine` (`pytest` aus `engine/`, `testpaths` enthält `tools`).

## Referenz statt .NET

Im Spiel wählt `Schach.Domain` den Zug (`Stilgewichtung.Waehle`, `Zugmerkmale`, `Partiezufall`). `eichung.py` spielt Tausende Partien ohne den .NET-Server und nutzt dafür `stil_referenz.py`.

- Der Nachbau folgt dem C#-Code Zeile für Zeile: Sortierung, Band `p ≥ r · p_max` (höchstens 8), Remis-Vermeidung, `w` nur im Band, begrenzt auf [0,5; 2,0], Ziehen aus `(q / p_max)^(1/τ)` mit SplitMix64 aus (Seed, Halbzug).
- `test_stil_referenz.py` prüft die Gleichheit gegen Werte aus dotnet (`engine/tools/testdaten/stil_dotnet.json`: 5 PRNG-Werte, 208 Zugmerkmale, 36 Zugwahlen).
- Ändert sich der C#-Code, die Testdaten neu erzeugen und `stil_referenz.py` nachziehen.
- Eigener Hook: `--waehler modul:fabrik`. Die Fabrik bekommt `Stilparameter` und liefert `waehler(board, policy, seed, halbzug) -> (uci, merkmale)`.

## VPS-Messung Teil 2 (`messung.py`)

### Was gemessen wird

- `N` parallele Partien (Standard 10). Der Gegner zieht über `POST /move` (Maia, Elo der Leiter reihum, `oppo_elo` 800 wie ein Gast).
- Der Spieler denkt 1 bis 3 s und fragt mit `--tipp-rate` (Standard 0,15) einen Tipp über `POST /analyse` (800 ms, MultiPV 1).
- 503 auf `/move` wird wie im .NET-Client nach 0,5 s wiederholt. „Gegnerzug gesamt“ summiert Versuche und Pausen. Das ist der Engine-Anteil für Kriterium 5.
- Phasen: `maia` (ohne Tipps) und `gemischt` (mit Tipps). Der Vergleich zeigt die Stockfish-Kosten neben Maia.
- Kriterium 5: Gegnerzug p95 ≤ 1 000 ms, Tipp p95 ≤ 1 500 ms. `--streng` gibt Exit 1, wenn eines verfehlt wird.

### Ablauf auf dem VPS

Der Engine-Container hat keinen Port nach außen. Gemessen wird aus einem Wegwerf-Container im selben Docker-Netz. Netz- und Containernamen auf der Box nachsehen, nicht ins Repo schreiben.

1. `engine/tools` auf die Box kopieren, z. B. nach `~/schach-tools`.
2. Netz des Engine-Containers finden: `docker inspect <engine-container> --format '{{json .NetworkSettings.Networks}}'` (das Compose-Netz `internal`).
3. Cgroup-Pfad für RSS finden (cgroup v2): `docker inspect <engine-container> --format '{{.Id}}'`, dann `ls /sys/fs/cgroup/system.slice/ | grep <id>`.
4. `chess` vorab installieren. Das Netz `internal` hat keine Route nach draußen, darum im Standardnetz:

   ```sh
   docker run --rm -v ~/schach-tools/site:/site python:3.12-slim pip install -q --target /site chess==1.11.2
   ```

5. Messung starten, höchstens 5 Minuten (zwei Phasen à 120 s):

   ```sh
   mkdir -p ~/schach-messung
   docker run --rm --network <internal-netz> -e PYTHONPATH=/site \
     -v ~/schach-tools:/tools:ro -v ~/schach-tools/site:/site:ro \
     -v ~/schach-messung:/out -v /sys/fs/cgroup:/host-cgroup:ro \
     python:3.12-slim sh -c "cd /tools && python messung.py \
       --url http://engine:8081 --partien 10 --dauer 120 --phasen maia,gemischt \
       --rss-befehl 'cat /host-cgroup/system.slice/docker-<id>.scope/memory.current' \
       --json /out/m.json --markdown /out/m.md"
   ```

6. Ohne cgroup-Zugriff: RSS in einer zweiten SSH-Sitzung sammeln, `while sleep 2; do docker stats --no-stream --format '{{.MemUsage}}' <engine-container>; done`.
7. Erststart inklusive Hugging-Face-Download (nur mit leerem Volume `engine-models`, also vor dem ersten Start oder nach bewusstem Löschen): den Wegwerf-Container mit `--erststart` starten, dann den Engine-Container starten. Die Zeit bis `/healthz` 200 steht oben im Bericht.
8. Ergebnis unten eintragen und in #15 kommentieren. Danach `MAIA_QUEUE_MAX`, `SF_POOL_SIZE`, die Limits aus 11.2 und `SF_ARCH` setzen.

### Ergebnis VPS-Messung Teil 2

Stand: gemessen am 2026-11-07 gegen den Engine-Container in Produktion. 10 parallele Partien, je Phase 120 s, Tipp-Rate 0,15, Tipp 800 ms. Gemessen wird nur der Engine-Dienst per HTTP, ohne die .NET-Schicht. Details in #15.

| Phase | Gegnerzug p50 ms | Gegnerzug p95 ms | Tipp p50 ms | Tipp p95 ms | 503-Quote `/move` | 503-Quote `/analyse` | RSS max MiB |
|---|---|---|---|---|---|---|---|
| `maia` | 27,8 | 54,2 | – | – | 0,000 | – | 469,2 |
| `gemischt` | 30,0 | 67,7 | 807,0 | 987,7 | 0,000 | 0,000 | 469,2 |

| Wert | Ergebnis |
|---|---|
| Erststart bis `/healthz` 200 (mit Download) | offen: Volume `engine-models` war schon gefüllt. Warmstart etwa 4,5 s bis „maia ready“. Beim nächsten bewussten Neuaufbau des Volumes mit `--erststart` nachholen. |
| `SF_ARCH` | `x86-64-avx2` |
| Kriterium 5 erfüllt | ja (Gegnerzug p95 67,7 ms ≤ 1 000 ms, Tipp p95 987,7 ms ≤ 1 500 ms, RSS 469 MiB von 2 GiB, Limits gesetzt) |

## Eichung (`eichung.py`)

### Engine nativ starten (Entwicklungsrechner)

Nicht in Docker unter Windows. Der Dienst läuft in der venv aus `engine/README.md`.

1. venv mit Modell: `pip install --index-url https://download.pytorch.org/whl/cpu torch==2.15.0`, dann `pip install -e '.[model,test]'`.
2. Stockfish 19 und Fairy-Stockfish 14 für das eigene Betriebssystem von den Release-Seiten in `engine/README.md` holen.
3. Umgebung setzen und starten:

   ```sh
   ENGINE_MODELS_DIR=./models STOCKFISH_PATH=/pfad/stockfish FAIRY_STOCKFISH_PATH=/pfad/fairy-stockfish \
   FAIRY_POOL_SIZE=4 MAIA_QUEUE_MAX=64 \
   python -m uvicorn schach_engine.app:app --host 127.0.0.1 --port 8081 --no-access-log
   ```

4. Warten, bis `http://127.0.0.1:8081/healthz` 200 meldet (der erste Start lädt Maia3-5M).

`FAIRY_POOL_SIZE` mindestens so groß wie `--parallel` setzen, sonst gibt es `pool_busy`.

### (a) Stärke

```sh
cd engine/tools
python eichung.py staerke --url http://127.0.0.1:8081 --gegner alle --partien 100 \
  --spiele eichung/staerke.jsonl --ausgabe eichung/staerke.json
```

- Neutrale Gewichte, Maia gegen Anker. Standard-Anker: Fairy-Stockfish über `/move` mit `fairy_skill` auf Ziel−200, Ziel, Ziel+200 (mindestens 500). Fairy rechnet im Dienst 100 ms je Zug.
- Alternativ ein Anker direkt per UCI: `--anker uci --anker-pfad /pfad/stockfish --anker-nodes 20000` oder `--anker-movetime 100`. Stockfish kann `UCI_Elo` erst ab 1320.
- Elo-Schätzung: Maximum Likelihood über alle Partien gegen alle Anker. Remis zählen halb. Dazu ein 95-%-Intervall.
- Suche je Gegner: Temperaturen (`--temperaturen`, äußere Schleife), darin SelfElo-Offsets (`--offsets`, Standard `0,-100,100,…,±400`). Der erste Versuch im Band ±100 gewinnt. So bleibt der Wert so nah wie möglich an der Identität.
- `--partien` gilt je Anker und Versuch. 100 × 3 Anker ergeben ±40 Elo Intervall.
- Ausgabe: Block `Engine:Eichung` je Gegner-Key, Liste `nicht_im_band`, Liste `tau_abweichung` und alle Versuche.
- Trifft 600 oder 800 nicht ins Band, spielt die ganze Spielstärke Anfänger (Ilse und Finn) mit `Engine: "Fairy"`, `SelfElo` = Gegner-Elo als `fairy_skill` (5.1, 5.4 a).
- τ gilt nur je Spielstärke (5.3 Schritt 2). Bekommen Gegner derselben Spielstärke verschiedene τ, steht die Spielstärke in `tau_abweichung`. Dann eine gemeinsame τ wählen und mit `--temperaturen <τ>` für beide nachmessen.
- `--anker uci` prüft vor dem Spielen, ob alle Anker im `UCI_Elo`-Bereich der Engine liegen (Stockfish ab 1320), und bricht sonst ab.

### (b) Stil gegen neutral und (c) Sichtbarkeit

```sh
python eichung.py stil --url http://127.0.0.1:8081 --gegner alle --partien 400 \
  --eichung eichung/staerke.json --spiele eichung/stil.jsonl --ausgabe eichung/stil.json
```

- Je Gegner spielen gestylte Gewichte gegen neutrale, gleiche SelfElo und Temperatur aus (a), Farben abwechselnd.
- Gegner mit `Engine: "Fairy"` werden übersprungen; der Fairy-Pfad hat keinen Stil.
- Bestanden bei 45 bis 55 % für die gestylte Seite. Bei 400 Partien ist das 95-%-Intervall etwa ±5 %.
- Nicht bestanden: erneut mit `--gewicht-max 1.5` oder `--band-faktor 0.5` (5.4 b). Die Elo wird nie verschoben.
- Sichtbarkeit (c) aus denselben Partien: Raten aller Merkmale je Zug für beide Seiten.
  - Aggressiv: (Schlag + Schach) je Zug mindestens 25 % über neutral.
  - Vorsichtig: riskante Züge je Zug mindestens 25 % unter neutral.
- Verspielt prüft `Ungewöhnlich = 1,5` (4.2). Besteht Nele, gilt 1,5, sonst 1,0.

### Fortsetzen und Auswerten

- Jede fertige Partie steht als Zeile in der `--spiele`-Datei. Die ID enthält Gegner, SelfElo, Temperatur, Anker und Nummer. Der Seed kommt aus der ID.
- Derselbe Aufruf mit derselben Datei spielt nur fehlende Partien. Abbruch mit Strg+C ist sicher; halbe Partien gehen verloren, eine halb geschriebene letzte Zeile wird beim nächsten Start übersprungen.
- `python eichung.py auswerten --spiele eichung/staerke.jsonl` fasst eine Datei neu zusammen.
- Partieende: Regeln von python-chess mit `claim_draw` (Wiederholung, 50 Züge), sonst Remis nach `--max-halbzuege` (Standard 300).

### Ergebnis Eichung

Stand: noch nicht geeicht. Bis dahin gilt der Platzhalter `Engine:Eichung` = Identität.

| Gegner | Elo | SelfElo | τ | Engine | gemessen | 95-%-Intervall | Partien |
|---|---|---|---|---|---|---|---|
| ilse | 600 | | | | | | |
| finn | 800 | | | | | | |
| oscar | 1000 | | | | | | |
| nele | 1200 | | | | | | |
| theo | 1400 | | | | | | |
| sementa | 1600 | | | | | | |
| konrad | 1900 | | | | | | |
| maren | 2200 | | | | | | |

| Gegner | Persönlichkeit | gestylt % | Intervall | bestanden | Sichtbarkeit | Ausweg |
|---|---|---|---|---|---|---|
| ilse | Gemütlich | | | | – | |
| finn | Aggressiv | | | | | |
| oscar | Lehrerhaft | | | | – | |
| nele | Verspielt | | | | – | |
| theo | Vorsichtig | | | | | |
| sementa | Eitel | | | | – | |
| konrad | Lehrerhaft | | | | – | |
| maren | Aggressiv | | | | | |

## Grenzen

- Die Anker-Elo von Fairy-Stockfish und Stockfish ist Engine-Elo, keine Lichess-Elo. Die Eichung misst „Stärke gegen diese Anker“. Abweichungen zur Spielerwahrnehmung zeigt erst der Betrieb.
- `/move` hat keine Rechenzeit-Option (Vertrag eingefroren). Fairy läuft im Dienst mit fest 100 ms; eigene `nodes`/`movetime` nur mit `--anker uci`.
- `oppo_elo` ist in (a) die Anker-Elo, in (b) die SelfElo des Gegners.
