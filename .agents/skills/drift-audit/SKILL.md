---
name: drift-audit
description: Prüfe vor dem Abschluss größerer AiNetLinter-Änderungen gezielt auf relevante Code-Duplikation und Refactoring-Drift mit dem projektspezifischen find_duplicates-MCP-Tool.
---

# Drift-Audit in AiNetLinter

## Zweck

Dieser Skill ist ein optionaler, fokussierter Duplicate-only-Check. Er soll
relevante Duplikation im geänderten Bereich sichtbar machen, nicht jede
strukturelle Ähnlichkeit in einen Refactoring-Auftrag verwandeln. Für den
umfassenden Abschlusscheck einschließlich Dead Code und Magic Values ist der
Skill `audit` zuständig.

Nicht bei jedem Commit oder kleinen Fix ausführen. Sinnvoll ist er einmal vor
dem Abschluss eines größeren Features, eines größeren Refactorings oder eines
fachlich zusammenhängenden Pakets, wenn mehrere ähnliche Pfade berührt wurden.

## Verbindliche Projektregeln

- Lies `AGENTS.md` und die relevanten Dateien unter `.agents/rules/`.
- Lies `.agents/rules/AiNetLinter-McpWorkflow.mdc` vor der MCP-Abfrage.
- Für den zielgebundenen `find_duplicates`-Aufruf das aktuelle Toolschema,
  `targetType=project` und den absoluten `targetPath` verwenden. Nicht auf
  veraltete Parameterannahmen aus diesem Skill vertrauen.
- `find_duplicates` ist die primäre Quelle für diesen Audit. Semantische
  Fragen zu Aufrufern oder Symbolen anschließend mit `find_references` oder
  einem passenden MCP-Tool prüfen.

## Audit

1. Beginne mit dem durch den aktuellen Diff betroffenen Produktions- und
   Testbereich. Ein solutionweiter Scan ist nur bei einem großen Abschluss-
   audit oder ausdrücklichem Auftrag angemessen.
2. Prüfe zuerst `exact`-Treffer. Vergleiche Signaturen, Aufrufer, Ownership,
   Fehlersemantik und fachliche Verantwortung, bevor du eine Konsolidierung
   empfiehlst.
3. Behandle `near`- und `structural`-Treffer ausschließlich als Kandidaten.
   Unterschiedliche Eingaben, Verträge oder bewusst getrennte Lebenszyklen
   sind ausreichende Gründe für legitime Ähnlichkeit.
4. Verwende einen Refactoring-Drift-Check für einen konkreten bestehenden
   Helper nur dann, wenn der erste Audit einen plausiblen Nachbau zeigt.
   Führe keinen breiten Suchlauf ohne konkrete Hypothese aus.

Verwende für den Aufruf ausschließlich die aktuelle MCP-Tooldefinition und
erfinde keine konkreten Toolparameter aus diesem Dokument.

## Triage

- **Fix jetzt:** kleine, risikoarme Konsolidierung mit identischem Vertrag und
  klarer gemeinsamer Ownership.
- **Berichten:** Konsolidierung braucht Architekturentscheidung, erweitert den
  Scope, verändert Fehler-/Lebenszeitsemantik oder ist nicht eindeutig besser.
- **Verwerfen:** legitime Ähnlichkeit mit kurzer Begründung.

Ein Audit-Fund erzeugt keinen eigenen Task, keinen Step und keine automatische
Korrekturschleife. Der Skill ändert keinen Code. Wenn der Nutzer keinen
separaten Auftrag erteilt, bleibt es bei einer knappen Empfehlung im
Abschlussbericht.

## Ergebnis

Berichte nur entscheidungsrelevante Treffer: Bereich, konkrete Fundstellen,
Ähnlichkeitstyp, fachliche Bewertung, Risiko und Empfehlung. Keine ausführliche
Clusterabschrift und keine globale Tech-Debt-Datei nur wegen dieses Audits.

Der Audit ist nicht das automatische Lint-Gate und ersetzt keine gezielte
MCP-Impact-/Violations-Prüfung. Er blockiert den Abschluss nur, wenn der
Nutzer dies ausdrücklich als Qualitätskriterium festgelegt hat oder die
Duplikation ein konkretes P0/P1-Produktionsrisiko erzeugt.
