# 01 – Konstruktor-Handoffs aus MCP-Ausgaben

**Status:** reproduzierbarer Defekt. **Schweregrad:** mittel bis hoch. **Betroffene Nutzung:** Dead-Code-Review und allgemeine Symbolnavigation.

## Evidenz

- SqlToAi: `verify({targetPath:"C:\\Daten\\Entwicklung\\Ralf\\SqlToAi\\SqlToAi.slnx",scope:"solution"})` gab Konstruktor-IDs wie `h:ciL3`, `h:ciL5` und `h:ciLD` aus. `find_references` mit diesen IDs ergab `SYMBOL_NOT_FOUND`. Auch neu erzeugte Konstruktor-Handoffs aus `get_file_skeleton` schlugen fehl. Typ-Handoffs funktionierten. [Protokoll](SqlToAi.md)
- KnowHowToAI: `find_references({targetPath:"C:\\Daten\\Entwicklung\\Ralf\\KnowHowToAI\\KnowHowToAI.slnx",symbolIdentifier:"h:ciNc",scopeType:"all"})` ergab für den `DashboardQuery`-Konstruktor `SYMBOL_NOT_FOUND`; die Typnamensuche fand dagegen Typreferenzen. [Protokoll](KnowHowToAI.md)
- Der Fehlertext zeigt in diesen Fällen eine Konstruktor-Doc-ID mit `#ctor` und Parameter-Suffix. Das spricht für ein Problem bei Identitätsbildung oder -auflösung; die Ursache ist noch nicht isoliert.

## Auswirkung

Der dokumentierte Zero-Transformation-Handoff scheitert gerade bei Kandidaten, die man überprüfen soll. Ausweichsuchen per Typname ändern die Symbol-Ebene und können Konstruktor- und Typreferenzen vermischen. Das erzeugt zusätzliche Aufrufe und begünstigt falsche Schlussfolgerungen.

## Grundlage für ein mögliches Umsetzungskonzept

- Reproduktion mit einem parameterlosen Record-Konstruktor und einem Konstruktor mit Parametern getrennt festhalten.
- Erzeugte Handoff-ID unverändert durch `find_references`, `get_symbol_body` und `get_feature_context` schicken; kanonisches Symbol und Fehlermeldung vergleichen.
- Nach einer Korrektur müssen die drei Tools dieselbe Konstruktor-Identität auflösen; ein nicht vorhandenes Symbol muss weiterhin eindeutig als Fehler markiert werden.

**Offen:** Betrifft der Defekt nur `#ctor`-IDs oder auch andere Symbolarten mit komplexen Signaturen? Keine Codeänderung in diesem Dokument beauftragt.
