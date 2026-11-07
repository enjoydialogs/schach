# Wie schnell und teuer sind Mistral-Sprüche?

Recherche zu [#5](https://github.com/enjoydialogs/schach/issues/5), Stand 2026-11-07.

**Kennzeichnung:**

- **Belegt:** aus einer Primärquelle (Mistral-Dokumentation, Mistral-Website), mit URL und Abrufdatum.
- **Intern:** aus dem Kronrat-Repo (`E:\enjoydialogs\kronrat`), dort gemessen oder entschieden.
- **Geschlossen:** aus den Belegen abgeleitet, nicht direkt so veröffentlicht.

Alle Preise gelten in US-Dollar je Million Token, Abruf 2026-11-07.

## Kurzantwort

- **Kosten sind kein Thema.** Ein Spruch kostet mit Ministral 3 8B über den EU-Endpunkt etwa 0,00006 bis 0,00009 $. Eine Partie mit 10 bis 15 Sprüchen kostet etwa 0,001 $, also rund 1 $ je 1000 Partien. (Geschlossen, Rechnung unten.)
- **Modell:** `ministral-8b-2512` (Ministral 3 8B), datierter Bezeichner, ohne Denkmodus. Billigere Alternative ist `ministral-3b-2512`. Wer mehr Witz braucht, nimmt `mistral-small-2603` (Small 4) mit `reasoning_effort: "none"`.
- **Kronrats Modell nicht übernehmen.** `mistral-medium-2604` (Medium 3.5) ist seit 2026-10-31 abgekündigt.
- **Antwortzeit:** Mistral veröffentlicht keine Zahlen. Standard-Tier heißt laut Mistral „Seconds to minutes". Deshalb sind der Ersatzspruch und ein hartes Zeitlimit Pflicht, keine Kür.
- **Aufrufweg:** serverseitig aus dem .NET-Backend. Der Aufruf startet parallel zur Engine-Berechnung und hat ein hartes Zeitlimit. Danach kommt der feste Ersatzspruch. Streaming bringt bei einem Einzeiler kaum etwas.
- **Ratenlimits:** Bei Überschreitung antwortet die API mit HTTP 429. Konkrete Zahlen stehen nur im Admin-Panel des Kontos. Schach bekommt einen eigenen Workspace mit eigenem Schlüssel und eigenem Ausgabelimit.

## 1. Modelle und Preise

Quelle der Preise: [docs.mistral.ai/inference/pricing](https://docs.mistral.ai/inference/pricing), Stand 2026-11-07 (belegt). Die Bezeichner stammen von den Modellseiten unter `docs.mistral.ai/models/…` (belegt).

| Modell | API-Bezeichner | Eingabe | Eingabe aus Cache | Ausgabe | Kontext | Eignung für Sprüche |
| --- | --- | ---: | ---: | ---: | ---: | --- |
| Ministral 3 3B | `ministral-3b-2512` | 0,10 | 0,01 | 0,10 | 256k | billigste Wahl, Witz ungeprüft |
| **Ministral 3 8B** | **`ministral-8b-2512`** | **0,15** | **0,015** | **0,15** | 256k | **erste Wahl** |
| Ministral 3 14B | `ministral-14b-2512` | 0,20 | 0,02 | 0,20 | 256k | Reserve, falls 8B zu flach klingt |
| Mistral Small 4 | `mistral-small-2603` | 0,15 | 0,015 | 0,60 | 256k | Hybridmodell mit Denkmodus, nur mit `reasoning_effort: "none"` |
| Mistral Large 4 | `mistral-large-4` (kein Datum sichtbar) | 0,70 | 0,07 | 2,10 | 1M | überdimensioniert, kein datierter Bezeichner |
| Mistral Medium 3.5 | `mistral-medium-2604` (Kronrat) | 1,50 | nicht genannt | 7,50 | 256k | **abgekündigt**, nicht verwenden |

Belege:

- Ministral 3 8B: [docs.mistral.ai/models/ministral-3-8b-25-12](https://docs.mistral.ai/models/ministral-3-8b-25-12). Bezeichner `ministral-8b-2512`, veröffentlicht 2025-12-02, Apache 2.0. Funktionen laut Seite: Chat Completions, Structured Outputs, Function Calling, Batching. Ein Denkmodus ist dort nicht genannt.
- Ministral 3 3B: [docs.mistral.ai/models/ministral-3-3b-25-12](https://docs.mistral.ai/models/ministral-3-3b-25-12). Ministral 3 14B: [docs.mistral.ai/models/ministral-3-14b-25-12](https://docs.mistral.ai/models/ministral-3-14b-25-12).
- Mistral Small 4: [docs.mistral.ai/models/mistral-small-4-0-26-03](https://docs.mistral.ai/models/mistral-small-4-0-26-03). Veröffentlicht 2026-03-16, 119B Parameter, davon 6,5B aktiv, vereint Instruct, Reasoning und Coding.
- Mistral Large 4: [docs.mistral.ai/models/mistral-large-4-0](https://docs.mistral.ai/models/mistral-large-4-0). Status allgemein verfügbar. Ein datierter Bezeichner ist auf der Seite nicht zu sehen.
- Abkündigungen: [docs.mistral.ai/getting-started/models/models_overview](https://docs.mistral.ai/getting-started/models/models_overview/). Medium 3.5 (26.04): abgekündigt 2026-10-31, Abschaltung 2027-04-30, Nachfolger Large 4. Die Modellseite [mistral-medium-3-5-26-04](https://docs.mistral.ai/models/mistral-medium-3-5-26-04) schreibt dazu: „This model is deprecated." Ministral 3 (3B, 8B, 14B) und Small 4 haben in der Tabelle **keine** eigene Zeile. Sie stehen dort nur als Nachfolger älterer Modelle. Large 3 (25.12) aus derselben Dezember-Charge ist dagegen abgekündigt (2026-10-31, Abschaltung 2027-04-30).

**Preis für Large 4 hat sich verschoben** (belegt gegen intern): Kronrats ADR-0026 (2026-10-07) führt für die Vorschau 1,36 $ / 4,18 $ als Listenpreis. Heute stehen auf der Modellseite und der Preisseite 0,70 $ / 2,10 $ bei allgemeiner Verfügbarkeit. Das bestätigt Kronrats Regel aus ADR-0026: Preise nur aus der Primärquelle und nur zu datierten Bezeichnern führen.

**Deutsch** (belegt, aber unscharf): Mistral nennt für die Mistral-3-Familie „native multimodal and multilingual capabilities" ([mistral.ai/news/mistral-3](https://mistral.ai/news/mistral-3/), 2025-12-02). Die Zahl „40+ Sprachen" bezieht sich dort auf die ganze Familie. Eine Sprachliste für Ministral 3 oder Deutsch im Besonderen nennt Mistral nicht. **Wie witzig und fehlerfrei 8B auf Deutsch ist, ist ungeprüft** (geschlossen). Das klärt eine kurze Kostprobe (siehe offene Prüfungen).

## 2. Kosten pro Spruch und pro Partie

**Annahmen** (geschlossen, keine Messung):

- Eingabe 300 bis 500 Token: Anweisung mit Persönlichkeit und Regeln (etwa 150 bis 250), Ereignisdaten (etwa 80 bis 150), die letzten zwei bis drei Sprüche gegen Wiederholung (etwa 60).
- Ausgabe 20 bis 40 Token: ein deutscher Satz mit höchstens etwa 15 Wörtern. `max_tokens` bei etwa 60 als Deckel.
- EU-Endpunkt mit Aufschlag 1,1 (belegt, siehe Abschnitt 6).
- Eine Partie mit 10 bis 15 Sprüchen bei Frequenzlimit. Grundlage: etwa 40 Züge je Seite, der Gegner spricht höchstens bei etwa jedem dritten Zug plus Ereignissen wie Schach, Figurgewinn, Zurücknehmen, Remisangebot und Partieende. Als ungünstiger Fall gelten 30 Sprüche.

**Rechnung** für den oberen Rand (500 Token Eingabe, 40 Token Ausgabe, EU-Preis):

| Modell | je Spruch | je Partie (15) | je Partie (30, ungünstig) | je 1000 Partien (15) |
| --- | ---: | ---: | ---: | ---: |
| Ministral 3 3B | 0,000059 $ | 0,0009 $ | 0,0018 $ | 0,89 $ |
| **Ministral 3 8B** | **0,000089 $** | **0,0013 $** | **0,0027 $** | **1,34 $** |
| Ministral 3 14B | 0,000119 $ | 0,0018 $ | 0,0036 $ | 1,78 $ |
| Mistral Small 4 | 0,000109 $ | 0,0016 $ | 0,0033 $ | 1,63 $ |
| Mistral Large 4 | 0,000477 $ | 0,0072 $ | 0,0143 $ | 7,16 $ |
| Medium 3.5 (zum Vergleich) | 0,001155 $ | 0,0173 $ | 0,0347 $ | 17,33 $ |

Rechenweg am Beispiel 8B: (500 + 40) Token × 0,15 $ × 1,1 / 1.000.000 = 0,0000891 $. Am unteren Rand (300 Token Eingabe) sind es 0,000056 $.

**Cache-Rabatt** (belegt, Mechanik ungeprüft): Die Preisseite führt Eingabe aus dem Cache bei einem Zehntel des Preises. [mistral.ai/pricing](https://mistral.ai/pricing) schreibt von bis zu 90 % weniger für wiederholte Eingaben. Eine feste Persönlichkeits-Anweisung am Anfang jeder Anfrage könnte davon profitieren. Wann Mistral einen Cache-Treffer zählt, habe ich nicht belegt gefunden (Kronrat hat Cache-Treffer bei Large 4 gemessen, intern). Bei diesen Beträgen ändert das nichts an der Entscheidung.

**Batch** (belegt) halbiert den Preis ([mistral.ai/pricing](https://mistral.ai/pricing)), passt aber nicht: Batch läuft asynchron in einer Warteschlange.

**Folge für das Ausgabelimit** (geschlossen): Ein Monatslimit von 5 € reicht mit 8B für mehrere Tausend Partien. Das Limit schützt vor einem Fehler im Code (Endlosschleife), nicht vor normalem Verbrauch.

## 3. Antwortzeit

**Belegt:**

- Mistral veröffentlicht keine Latenzzahlen. Die Seite [Known limitations](https://docs.mistral.ai/resources/known-limitations) nennt keine. Stand 2026-11-07.
- Die Seite [Priority Tier](https://docs.mistral.ai/inference/priority-tier) beschreibt die Latenz des Standard-Tiers als „Seconds to minutes", die des Priority-Tiers als „Seconds". Priority Tier kostet das 1,75-Fache und braucht eine Vereinbarung mit Mistral. Ohne Angabe gilt `service_tier: "standard_only"`.

**Intern, nicht übertragbar:** Kronrat misst 5 bis 13 Sekunden je Aufruf (`LlmOptions.TimeoutSeconds`, ADR-0017). Das betrifft Medium mit langen Texten (Chroniken), nicht einen Einzeiler mit einem kleinen Modell.

**Geschlossen:** Bei 20 bis 40 Ausgabetoken bestimmt die Zeit bis zum ersten Token die Wartezeit, nicht die Schreibgeschwindigkeit. Ein kleines Modell dürfte meist in deutlich unter einer Sekunde bis wenigen Sekunden antworten. Ausreißer bis in die Minuten sind laut Mistral möglich. Eine eigene Messung fehlt.

**Folge:** Ein festes, kurzes Zeitlimit mit Ersatzspruch ist Pflicht. Den Wert legt die Spec fest, nach einer Messung (siehe offene Prüfungen).

## 4. Aufrufweg: Streaming oder parallel zur Engine

**Belegt** ([docs.mistral.ai/api](https://docs.mistral.ai/api), Stand 2026-11-07):

- `stream: true` liefert `text/event-stream` (Server-Sent Events) und endet mit `data: [DONE]`. Ohne `stream` kommt ein einzelnes JSON-Objekt.
- Mit `stream_options.include_usage` kommen auch beim Streaming die Tokenzahlen mit ([Known limitations](https://docs.mistral.ai/resources/known-limitations)).

**Empfehlung** (geschlossen):

1. **Der Aufruf läuft serverseitig im .NET-Backend, nie aus der PWA.** So bleibt der API-Schlüssel auf dem VPS. Die Engine läuft ohnehin dort.
2. **Spruch zum Zug des Spielers:** Sobald der Server den Zug des Spielers hat, startet er den Spruch-Aufruf und die Engine gleichzeitig. Die Denkzeit der Engine versteckt die Wartezeit. Der Spruch erscheint zusammen mit dem Zug des Gegners.
3. **Spruch zum eigenen Zug des Gegners** (z. B. „Schach!"): Der Aufruf startet nach dem Engine-Ergebnis. Der Zug wird sofort gezeigt, der Spruch folgt nach, sobald er da ist.
4. **Hartes Zeitlimit:** Ist der Spruch nach dem Limit nicht da, zeigt der Server den festen Ersatzspruch für dieses Ereignis und diese Persönlichkeit. Eine verspätete Antwort wird verworfen. Kein Wiederholungsversuch während der Partie.
5. **Kein Streaming.** Ein Einzeiler erscheint als Ganzes in einer Sprechblase. Streaming spart nur die Schreibzeit von 20 bis 40 Token und macht das Zeitlimit komplizierter.
6. **Der Spruch wird mit dem Zug gespeichert,** damit das Nachspielen ihn an seiner Stelle zeigt, ohne erneuten Aufruf.

**Aufrufform** (intern, aus Kronrat übernehmbar): OpenAI-kompatibles `POST {Endpoint}/chat/completions`, `Authorization: Bearer`, datierter Modellbezeichner, `max_tokens`, `temperature`. Kronrat liest `content` sowohl als Zeichenkette als auch als Liste typisierter Teile (`thinking`, `text`) und verwendet nur die `text`-Teile. Das sollte Schach auch tun (siehe `OpenAiCompatibleChatProvider.cs` in Kronrat).

**`reasoning_effort`:**

- **Belegt:** Die API kennt `none`, `minimal`, `low`, `medium`, `high`, `xhigh`. Ein Standardwert ist nicht dokumentiert ([docs.mistral.ai/api](https://docs.mistral.ai/api)). Für Small 4 beschreibt [Reasoning](https://docs.mistral.ai/studio/conversations/reasoning) `none` als minimales Denken ohne Denkteil in der Antwort. Dieselbe Seite widerspricht sich aber, ob `content` dann eine Zeichenkette oder eine Liste ist.
- **Intern:** Kronrat hat gemessen, dass `mistral-large-2512` (ohne Denkmodus) das Feld mit 400 ablehnt und dass ein Hybridmodell ohne das Feld von sich aus denkt, mit dem Zehn- bis Fünfundzwanzigfachen an Ausgabetoken (ADR-0026).
- **Geschlossen:** Für Ministral 3 8B das Feld weglassen. Für Small 4 immer `"none"` senden.

## 5. Kontext für den Spruch, ohne persönliche Daten

**Geschlossen:** Ein kleines Modell liest eine Schachstellung aus einer FEN nicht verlässlich. Es erfindet sonst Drohungen, die es nicht gibt. Deshalb schickt der Server Fakten, die die Engine schon berechnet hat, nicht nur die Stellung.

**Mitschicken:**

- Name, Persönlichkeit und Spielstärke des Gegners. Die Anweisung zur Persönlichkeit liegt fest im Server, je Gegner.
- Ereignistyp, z. B. `Zug`, `Schach`, `Figur geschlagen`, `Figur verloren`, `Fehler des Spielers`, `Zurücknehmen`, `Remisangebot`, `Partieende`.
- Letzter Zug in SAN (z. B. `Sxe5`) und wer ihn gemacht hat.
- Änderung von Bewertung oder Material laut Engine, in Worten (z. B. „Spieler hat einen Springer eingestellt", „Stellung jetzt klar besser für den Gegner").
- Die letzten zwei bis drei Sprüche dieser Partie, gegen Wiederholung.
- Regeln: ein Satz, höchstens etwa 15 Wörter, Deutsch, Du-Form, keine Beleidigung, keine Zugempfehlung (sonst wird der Spruch zum Tipp; Tipps sind eine Lernhilfe auf Wunsch und mit Uhr verboten).
- FEN nur als Zusatz, falls eine Kostprobe zeigt, dass es hilft.

**Nie mitschicken:** Name, E-Mail-Adresse, Elo, Konto-ID oder sonstige Daten des Spielers oder Gasts. Auch keine Kennung, die Partien über Anfragen hinweg einer Person zuordnet. Der Spruch braucht nur „der Spieler".

**Geschlossen:** Mit diesen Regeln enthält eine Anfrage nur Spieldaten. Der Gegner spricht den Spieler mit „du" an, ohne ihn zu kennen.

## 6. EU-Endpunkt

**Belegt** ([Regional inference](https://docs.mistral.ai/inference/regional-inference), Stand 2026-11-07):

- Endpunkte: `api.mistral.ai` (global, kein fester Ort der Inferenz), `api.eu.mistral.ai` (EU), `api.us.mistral.ai` (USA).
- Regionale Inferenz kostet das 1,1-Fache des Listenpreises, für Eingabe, Ausgabe und Cache.
- Zitat: „Available models vary by region." Welche Modelle der EU-Endpunkt anbietet, steht nicht auf der Seite. Prüfen lässt es sich mit `models.list` gegen die EU-Basis-URL.

**Intern:** Kronrat ruft aus rechtlichen Gründen nur `https://api.eu.mistral.ai/v1` auf, spätestens sobald fremde Personen sich registrieren (ADR-0050, ADR-0051). Schach hat offene Registrierung.

**Geschlossen:** Schach sollte denselben Weg gehen. Ob `ministral-8b-2512` dort angeboten wird, ist **ungeprüft**.

## 7. Ratenlimits und was bei Überschreitung passiert

**Belegt:**

- Drei Limits: Anfragen je Sekunde, Token je Minute (Ein- plus Ausgabe), Token je Monat. Bei Überschreitung einer Grenze antwortet die API mit HTTP 429. Anfragen je Sekunde und Token je Minute gelten unabhängig voneinander. Batch zählt nicht zu den Echtzeit-Limits. Der Header `X-RateLimit-Remaining` zeigt den Rest. Quelle: [Known limitations](https://docs.mistral.ai/resources/known-limitations).
- Stufen beim Pay-as-you-go: Free (niedrigste Limits, zum Testen), Tier 1 mit Pay-as-you-go, Tier 2 ab 20 €/$ insgesamt abgerechnet, Tier 3 ab 100, Tier 4 ab 500, darüber auf Anfrage ab 2000. Der Aufstieg geschieht automatisch. Vorausbezahltes Guthaben hebt die Stufe nicht. Limits gelten je Modell und können sich beim Modellwechsel ändern. Quelle: [Help Center, Artikel vom 2026-08-12](https://help.mistral.ai/en/articles/698531-why-am-i-hitting-api-rate-limits-and-how-do-i-increase-them).
- Konkrete Zahlen je Stufe veröffentlicht Mistral nicht. Sie stehen im Admin-Panel unter API › Limits.
- Ausgabelimit je Workspace: Neue Workspaces haben keines. Erreicht ein Workspace sein Limit, ist sein API-Zugang bis zum Monatswechsel oder bis zur Anhebung gesperrt. Andere Workspaces laufen weiter. Quelle: [Workspace usage and limits](https://docs.mistral.ai/admin/workspaces/usage-limits).
- Eine Wartezeit-Angabe wie `Retry-After` und eine Empfehlung zum Wiederholen dokumentiert Mistral nicht ([Known limitations](https://docs.mistral.ai/resources/known-limitations)).

**Widerspruch in den Quellen** (belegt): Die Seite [Workspace usage and limits](https://docs.mistral.ai/admin/workspaces/usage-limits) sagt, Ratenlimits gelten je Workspace und werden von allen Schlüsseln darin geteilt. Das [Help Center](https://help.mistral.ai/en/articles/698531-why-am-i-hitting-api-rate-limits-and-how-do-i-increase-them) und [Known limitations](https://docs.mistral.ai/resources/known-limitations) sagen: je Organisation, über alle Workspaces. Was für das Konto gilt, zeigt nur das Admin-Panel.

**Geschlossen:** Ob ein gesperrter Workspace mit 429 oder einem anderen Code antwortet, habe ich auf keiner abgerufenen Seite belegt gefunden (nur in einem Suchauszug).

**Folge für Schach** (geschlossen):

- Schach bekommt einen **eigenen Workspace mit eigenem Schlüssel und eigenem Ausgabelimit**, wie Kronrats ADR-0051 es für Produktion und Entwicklung festlegt. So sperrt ein Fehler in Schach Kronrat nicht und umgekehrt. Gelten die Ratenlimits doch je Organisation, teilen sich beide Apps die Anfragen je Sekunde trotzdem.
- **Jeder Fehler (429, 401, 5xx, Zeitlimit) führt sofort zum Ersatzspruch.** Kein Wiederholen während der Partie, keine Fehlermeldung an den Spieler.
- Das Frequenzlimit hält die Last klein: höchstens etwa ein Aufruf je Zug, nur bei offenen Partien. Ob die Anfragen je Sekunde der Free- oder Tier-1-Stufe für gleichzeitige Partien reichen, ist ohne die Zahlen aus dem Admin-Panel offen.

## Offene Prüfungen für den Betreiber

Keine davon blockiert die Spec. Sie gehören in die Bauphase vor dem ersten echten Aufruf.

1. **EU-Verfügbarkeit:** `GET https://api.eu.mistral.ai/v1/models` mit dem Schach-Schlüssel. Steht `ministral-8b-2512` darin? Wenn nicht: `ministral-3b-2512` oder `mistral-small-2603` prüfen.
2. **Ratenlimits ablesen:** Admin-Panel › API › Limits. Gilt die Grenze je Workspace oder je Organisation, und wie viele Anfragen je Sekunde erlaubt die aktuelle Stufe für das gewählte Modell?
3. **Workspace anlegen:** eigener Workspace „schach" mit eigenem Schlüssel und Monatslimit (Vorschlag 5 €).
4. **Latenz messen:** etwa 50 Aufrufe mit der echten Anfrageform gegen den EU-Endpunkt. Median und 95. Perzentil der Gesamtzeit. Daraus folgt das Zeitlimit für die Spec.
5. **Kostprobe Deutsch und Witz:** etwa 20 Spielsituationen über drei Persönlichkeiten, gegen 3B, 8B und Small 4. Ein Mensch liest und entscheidet. Dabei auch prüfen, ob FEN als Zusatz hilft oder schadet.

## Hinweis zu Kronrat

Kronrat ist die Referenz für die Aufrufform (OpenAI-kompatibel, Bearer, EU-Endpunkt, datierter Bezeichner, `reasoning_effort` je Modell, Preisliste aus der Primärquelle). Für die Modellwahl ist Kronrat keine Referenz: `mistral-medium-2604` ist seit 2026-10-31 abgekündigt und wird am 2027-04-30 abgeschaltet.
