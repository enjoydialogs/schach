# schach-engine

Engine-Dienst für Schach (SCHACH-SPEC 5.2): Maia-3 für menschliche Kandidatenzüge, Stockfish 19 für Tipp und Remis-Bewertung, Fairy-Stockfish als Ausweg. Der Dienst spricht nur HTTP. Vertrag: [`API.md`](API.md).

## Lizenz

- Dieses Verzeichnis steht unter **AGPL-3.0-or-later** ([`LICENSE`](LICENSE)). Der Rest des Repos steht unter MIT.
- `src/schach_engine/maia3/` enthält **geänderten Code aus [CSSLab/maia3](https://github.com/CSSLab/maia3)** (AGPL-3.0, Commit `1e13597c42d4858b7cfd7cfdae01e297263364b2`). Übernommen sind das Netz (`models.py`), Eingabe und Zugvokabular (`tokens.py`) sowie Laden und Abfragen (`netz.py`). Jede Datei nennt im Kopf ihre Herkunft und die Änderungen. Kurz: nur Inferenz für Maia3-5M, kein UCI, kein Ziehen, Policy über alle erlaubten Züge, ohne numpy.
- Der Dienst ist über das Netz erreichbar. Nach AGPL § 13 steht sein Quellcode hier im öffentlichen Repo.

## Gewichte

- Die Maia-3-Gewichte (`maia3-5m.pt`, ca. 21 MB, Hugging Face [`UofTCSSLab/Maia3-5M`](https://huggingface.co/UofTCSSLab/Maia3-5M)) liegen **nie im Repo und nie im Image**. Sie werden **nicht weiterverbreitet**.
- Der Container lädt sie beim ersten Start nach `ENGINE_MODELS_DIR` (`/models`, Volume `engine-models`). Revision und sha256 sind in `src/schach_engine/modell_download.py` gepinnt. Passt die Prüfsumme nicht, lädt der Dienst nicht und `/healthz` bleibt 503.
- Für die Gewichte ist keine Lizenz angegeben. Sie ist bei CSSLab angefragt (CSSLab/maia3 Issue #8, SCHACH-SPEC 5.5).

## Binaries im Image

| Engine | Version | Quelle | Lizenz |
|---|---|---|---|
| Stockfish | 19 (`sf_19`), Universal-Binary `stockfish-linux-x86-64-universal` | https://github.com/official-stockfish/Stockfish/releases/tag/sf_19 | GPL-3.0 |
| Fairy-Stockfish | 14 (`fairy_sf_14`), Build je `SF_ARCH` | https://github.com/fairy-stockfish/Fairy-Stockfish/releases/tag/fairy_sf_14 | GPL-3.0 |

- URL und sha256 stehen in [`docker/hole_binaries.py`](docker/hole_binaries.py). Der Build bricht bei falscher Prüfsumme ab.
- Build-ARG `SF_ARCH` (Standard `x86-64-avx2`, auch `x86-64-avx512`). Stockfish 19 erkennt die CPU zur Laufzeit selbst; `SF_ARCH` wählt das Fairy-Stockfish-Build (beide Werte: bmi2).
- Beide laufen als eigene Prozesse über UCI und werden nicht gelinkt. Die Quellen liegen unter den genannten Tags.

## Betrieb

| Env | Vorgabe | Bedeutung |
|---|---|---|
| `MAIA_TORCH_THREADS` | `1` | PyTorch-Threads für Maia |
| `MAIA_QUEUE_MAX` | `32` | wartende Maia-Anfragen; Frist 2 s, danach 503 `queue_full` |
| `SF_POOL_SIZE` | `2` | Stockfish-Prozesse, je `Threads=1`, `Hash=32`; Wartegrenze 3 s, danach 503 `pool_busy` |
| `SF_MAX_MOVETIME_MS` | `800` | Deckel für `/analyse`; größere `movetime_ms` werden gekappt |
| `FAIRY_POOL_SIZE` | `1` | Fairy-Stockfish-Prozesse, lazy gestartet, 100 ms je Zug |
| `ENGINE_ROLES` | `maia,stockfish,fairy` | abgeschaltete Rolle antwortet 503 `role_disabled` |
| `SCHACH_ENGINE_FAKE_MODEL` | | `1`: alle Kerne antworten aus der Fixture (pytest, ohne Torch und Binaries) |
| `ENGINE_MODELS_DIR` | `/models` | Ziel des Modell-Downloads |
| `STOCKFISH_PATH`, `FAIRY_STOCKFISH_PATH` | `/usr/local/bin/…` | setzt das Image |

- `/healthz` meldet 200 erst, wenn Maia geladen ist und alle Stockfish-Prozesse `readyok` gemeldet haben (nur für Rollen in `ENGINE_ROLES`). Scheitert der Start (z. B. Hugging Face nicht erreichbar), versucht der Dienst es mit wachsender Pause (5 s bis 60 s) erneut.
- Ein abgestürzter Stockfish-Prozess wird verworfen und beim nächsten Zugriff neu gestartet; die laufende Anfrage bekommt 503 `pool_busy`.

## Entwickeln und testen

```sh
python -m venv .venv
.venv/bin/python -m pip install -e '.[test]'
SCHACH_ENGINE_FAKE_MODEL=1 .venv/bin/python -m pytest
```

Modelltest mit dem echten Maia3-5M (lädt ca. 21 MB, braucht PyTorch CPU):

```sh
.venv/bin/python -m pip install --index-url https://download.pytorch.org/whl/cpu torch==2.15.0
.venv/bin/python -m pip install -e '.[model,test]'
SCHACH_ENGINE_MODEL_TESTS=1 SCHACH_ENGINE_MODEL_CACHE=/pfad/zu/models .venv/bin/python -m pytest -m model
```
