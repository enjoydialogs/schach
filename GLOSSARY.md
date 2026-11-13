# Schach

Ein Browserspiel für das Handy, das Schach durch Partien gegen Computergegner mit eigener Persönlichkeit beibringt.

## Sprache

### Spielen

**Spieler**:
Ein Mensch mit Konto, dessen Partien gespeichert werden und der eine Elo hat.
_Avoid_: User, Nutzer, Account

**Gast**:
Ein Mensch ohne Konto, der spielen kann, dessen Partien aber nicht gespeichert werden und nicht in die Elo zählen.
_Avoid_: Anonymer Nutzer, Besucher

**Elo**:
Die Wertungszahl eines Spielers; jeder Gegner hat eine feste Elo passend zu seiner Spielstärke.
_Avoid_: Rating, Wertung, Punkte

**Gegner**:
Eine computergesteuerte Figur mit Namen, Persönlichkeit und Spielstärke, gegen die der Spieler eine Partie spielt.
_Avoid_: KI, Bot, Engine, Computer

**Persönlichkeit**:
Der Charakter eines Gegners (z. B. aggressiv), der bestimmt, wie er redet und welchen Spielstil er bevorzugt.
_Avoid_: Charakter, Stil, Persona

**Spielstärke**:
Wie gut ein Gegner Schach spielt, von Anfänger bis Hobbyspieler; unabhängig von seiner Persönlichkeit.
_Avoid_: Schwierigkeit, Level, Stufe

**Partie**:
Ein einzelnes Schachspiel zwischen dem Spieler und einem Gegner, von der Grundstellung bis zum Ergebnis.
_Avoid_: Spiel, Match, Game

**Offene Partie**:
Die eine Partie eines Spielers, die noch kein Ergebnis hat; jeder Spieler hat höchstens eine.
_Avoid_: laufendes Spiel, aktive Partie

**Bedenkzeit**:
Die optionale Zeitvorgabe einer Partie, beim Start gewählt; ohne sie wird ohne Uhr gespielt.
_Avoid_: Timer, Zeitkontrolle, Uhr (außer im UI-Label „Ohne Uhr“)

**Zugfolge**:
Die geordnete Liste aller Züge einer Partie.
_Avoid_: Historie, Verlauf, Zugreihenfolge

**Spruch**:
Ein Kommentar, den ein Gegner zu einem Zug oder einer Spielsituation abgibt.
_Avoid_: Kommentar, Nachricht, Chat

**Frechheit**:
Wie frech ein Gegner in seinen Sprüchen über die Züge des Spielers reden darf: freundlich, neckend oder vorlaut; nie über die Person.
_Avoid_: im Code „Stufe“ für die Frechheit (`Stufe` ist die Spielstärke), Modus

**Laune**:
Die Stimmung eines einzelnen Spruchs (trocken, lustig, vorlaut, genervt), je Halbzug aus dem Partie-Zufall gewählt.
_Avoid_: Stimmung, Mood

**Ergebnis**:
Der Ausgang einer beendeten Partie: Weiß gewinnt, Schwarz gewinnt oder Remis.
_Avoid_: Ausgang, Resultat, Score

**Grund**:
Warum eine Partie beendet wurde, z. B. Matt, Aufgabe, Zeit oder eine Remisregel.
_Avoid_: Ursache, Termination

**Remis**:
Ein Ergebnis ohne Sieger, automatisch durch eine Remisregel oder durch ein angenommenes Angebot.
_Avoid_: Unentschieden, Draw

**Patt**:
Die Seite am Zug hat keinen erlaubten Zug und steht nicht im Schach; die Partie endet Remis.
_Avoid_: Stalemate

**Mattmaterial**:
Genug Figuren, um noch matt setzen zu können; es fehlt, wenn eine Seite nur den König oder König und eine Leichtfigur hat.
_Avoid_: mating material

**Zurücknehmen**:
Den letzten eigenen Zug samt Gegnerantwort rückgängig machen; nur ohne Bedenkzeit, danach zählt die Partie nicht mehr für die Elo.
_Avoid_: Undo, Takeback

**Bilanz**:
Siege, Remis und Niederlagen eines Spielers gegen einen Gegner; zählt jede beendete Partie.
_Avoid_: Statistik, Head-to-head

### Lernen

**Lernhilfe**:
Eine Hilfe während der Partie, z. B. das Anzeigen erlaubter Züge, eine Warnung vor einer ungedeckten Figur oder ein Tipp auf Wunsch.
_Avoid_: Hint, Coach, Tutorial

**Nachspielen**:
Das Ansehen einer beendeten Partie Zug für Zug.
_Avoid_: Replay, Review, Analyse

**Tipp**:
Eine Lernhilfe, die auf Wunsch den besten Zug zeigt; nur ohne Bedenkzeit, und der erste Tipp nimmt die Partie aus der Elo.
_Avoid_: Hint, Vorschlag

**Übung**:
Eine geführte Situation aus fester Stellung mit vorgegebenen Zügen, die ein zufälliger Gegner erklärt; ohne Uhr, ohne Elo, keine Partie.
_Avoid_: Lektion, Rätsel, Puzzle, Szenario, Training
