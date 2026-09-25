# Konzept: Metriken für Primary Constructors korrekt zuordnen

## Befund

Im Read-only-Audit mit AiNetLinter MCP 1.0.216 wurde in der SAN-Solution der
Primary Constructor von `XrmTermineSendTerminanfrageCommandHandler` über eine
frische Symbol-ID korrekt aufgelöst. `get_feature_context` meldete für ihn
jedoch `Method LOC: 320`, Cyclomatic Complexity 24 und Cognitive Complexity 24
über die Zeilen 12–374. Diese Spanne und die LOC entsprechen dem umschließenden
Typ. Derselbe Typ wurde separat korrekt mit `Type LOC: 320` ausgewiesen. Ein
regulärer Konstruktor derselben Solution lieferte dagegen Metriken seines
eigenen Bodys (41 LOC, Complexity 1/0).

Der ID-Handoff ist hierbei nicht defekt: `get_file_skeleton` →
`get_class_structure` → `get_feature_context` und `get_symbol_body` führten
zum richtigen Konstruktor und Projekt. Offen ist die Metrikzuordnung für
Primary Constructors ohne separaten Konstruktor-Body.

## Ziel

Metriken eines Konstruktors beschreiben ausschließlich Code, der diesem
Konstruktor semantisch zugeordnet werden kann. Member, Methoden und die
gesamte Typdeklaration dürfen nicht als Konstruktor-LOC oder
Konstruktor-Komplexität erscheinen. Wo eine Kennzahl für einen Primary
Constructor nicht sinnvoll bestimmbar ist, soll das Ergebnis dies eindeutig
ausweisen statt die Typmetrik als Methodenmetrik zu präsentieren.

Die Lösung muss für beliebige C#-Codebasen gelten. Das SAN-Beispiel dient nur
als reproduzierbare Evidenz, nicht als projektspezifische Sonderregel.

## Vorgehen und Akzeptanzkriterien

1. Mit einem minimalen C#-Beispiel einen roten Regressionstest für einen
   Primary Constructor schreiben, dessen Typ komplexe Methoden enthält.
   `get_feature_context` darf die Methoden- und Typmetriken nicht dem
   Konstruktor zuschreiben.
2. Einen regulären Konstruktor als Kontrolle prüfen: Dessen eigene
   Body-Metriken bleiben erhalten. Auch ein Primary Constructor in einem
   `record` und einer `class` soll eindeutig behandelt werden.
3. Die Roslyn-Syntaxspanne und die Metrikquelle für Konstruktoren prüfen und
   die Zuordnung allgemein korrigieren. Für bodylose Konstruktoren eine
   konsistente, explizite Darstellung festlegen; keine implizite Übernahme
   der gesamten Typmetrik.
4. Prüfen, ob aus den falschen Konstruktor-Metriken Violations oder
   irreführende Priorisierungen entstehen, und den Regressionsschutz auf
   betroffene Ausgaben ausweiten.
5. Die vorhandenen ID-Handoff-Tests weiterhin bestehen lassen: Eine
   Metrikkorrektur darf Symbolidentität, Projektbindung und Referenzen nicht
   verändern.

## Abgrenzung

Dieser Task betrifft die Metrikberechnung und -darstellung für Primary
Constructors. Neue MCP-Features, projektspezifische Regeln und eine erneute
Überarbeitung der bereits funktionierenden Symbol-IDs gehören nicht dazu.
