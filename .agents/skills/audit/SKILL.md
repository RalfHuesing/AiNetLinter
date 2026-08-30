---
name: audit
description: Führe am Ende größerer AiNetLinter-Aufgaben einen gezielten MCP-Audit auf DRY, Refactoring-Drift, Dead Code und Magic Values durch und behebe sichere, scope-nahe Befunde proaktiv.
---

# Qualitäts-Audit in AiNetLinter

## Zweck

Dieser Skill ist ein unabhängiger Abschlusscheck für größere Features,
Refactorings und zusammenhängende Codepakete. Er sucht mit dem AiNetLinter-
MCP-Server nach Code-Duplikation, Refactoring-Drift, totem Code und Magic Values und behebt
eindeutige, risikoarme Befunde direkt im aktuellen Arbeitsstand.

Der Audit wird nicht bei jedem kleinen Fix und nicht als endlose
Korrekturschleife ausgeführt. Ein Orchestrator kann ihn nach Implementierung
und Review einmal am Ende eines Tasks aufrufen.

## Verbindliche Projektregeln

- Lies `AGENTS.md` und die relevanten Dateien unter `.agents/rules/`.
- Lies `.agents/rules/AiNetLinter-McpWorkflow.mdc` vor jeder semantischen
  C#-Analyse.
- Verwende für `find_duplicates`, `find_dead_code` und `find_magic_values` die
  aktuellen MCP-Toolschemas mit `targetType=project` und dem absoluten
  `targetPath`. `rg` ergänzt nur konkrete Textarbeit und ersetzt die
  semantischen MCP-Abfragen nicht.
- Halte Architekturverbote, Testregeln, Warnungsfreiheit und
  Dokumentationspflichten ein. Ändere keine externen Source-Repositories oder
  untersuchten Assemblies.

## Scope

Leite den primären Auditbereich aus dem aktuellen Diff und den direkt
betroffenen Symbolen ab. Prüfe zuerst geänderte Produktions- und Testpfade;
beziehe unmittelbare Aufrufer und gemeinsam genutzte Helper nur bei konkreter
Relevanz ein.

Ein solutionweiter Audit ist nur bei einem ausdrücklich beauftragten
Gesamtcheck oder beim Abschluss eines entsprechend großen Tasks angemessen.
Unabhängige Altbefunde dürfen nicht nur deshalb in Arbeit verwandelt werden,
weil sie im globalen Scan sichtbar werden.

## Audit-Reihenfolge

1. **DRY und Refactoring-Drift:** Rufe `find_duplicates` auf. Prüfe exakte
   Treffer zuerst; `near`- und strukturelle Treffer sind nur Kandidaten.
   Vergleiche Signaturen, Aufrufer, Ownership, Fehlersemantik und fachliche
   Verantwortung. Verwende `mode="refactoring-drift"` nur bei einer konkreten
   Hypothese, dass ein bestehender Helper in einem betroffenen Pfad nachgebaut
   wurde; führe keinen breiten Drift-Suchlauf ohne solchen Hinweis aus.
2. **Dead Code:** Rufe `find_dead_code` auf. Bestätige einen Fund mit
   `find_references` oder einem passenden MCP-Impact-Tool. Berücksichtige
   öffentliche bzw. interne Verträge, Reflection, Serialisierung,
   `InternalsVisibleTo` und Testnutzung, bevor Code entfernt wird.
3. **Magic Values:** Rufe `find_magic_values` auf. Konzentriere dich auf
   wiederholte oder fachlich identische Werte im Auditbereich. Diagnosecodes,
   bewusst einmalige Testdaten und unterschiedliche Wire-Verträge werden
   nicht pauschal zentralisiert.
4. Nach jeder Änderung prüfe die betroffenen Aufrufer und die direkte
   Invariante erneut. Führe gezielte Tests aus; vor dem finalen Hand-off gelten
   die Build-/Test-Gates aus `AGENTS.md`.

## Proaktive Korrekturen

Ein Befund darf automatisch behoben werden, wenn alle folgenden Bedingungen
erfüllt sind:

- Er liegt im aktuellen Task- oder unmittelbar betroffenen Codebereich.
- Die Ursache und die fachliche gemeinsame Verantwortung sind eindeutig.
- Die Korrektur ist klein, verhaltensneutral und ohne öffentliche
  Vertragsänderung möglich.
- Es gibt keinen konkurrierenden Architekturweg und keine unklare indirekte
  Nutzung.

Typische sichere Korrekturen sind ein exakter privater Helper-Klon, eindeutig
unreferenzierter privater Code nach bestätigter Referenzprüfung oder ein
wiederholter fachlicher Wert, der in eine bestehende passende Konstante bzw.
Konfiguration gehört.

Nicht automatisch beheben: `near`-/`structural`-Kandidaten ohne bestätigte
gemeinsame Semantik, öffentliche oder intern getestete APIs mit unsicherer
indirekter Nutzung, breite Umbenennungen, neue Abstraktionsschichten und
projektweite Aufräumarbeiten außerhalb des Task-Scope. Diese Befunde werden
mit Fundstelle, Evidenz, Risiko und Empfehlung berichtet.

## Abschluss und Stoppregel

Führe nach den sicheren Korrekturen einen gebündelten Nachcheck der betroffenen
Bereiche durch. Starte keine neue vollständige Audit-Kette nur wegen eines
neu sichtbaren P2-/P3-Befunds. Sobald ein Fund Architekturentscheidungen,
größeren Scope oder ein anderes Betriebsmodell erfordert, stoppt der Audit an
dieser Stelle und berichtet ihn zur Nutzerentscheidung.

Der Audit ändert keine Task-/Step-Dateien und erzeugt keinen eigenen Task oder
Commit. Wenn er vom Orchestrator aufgerufen wird, bleiben Codeänderungen im
aktuellen Arbeitsstand; der aufrufende Workflow entscheidet über Commit und
weitere Verifikation.

## Ergebnis

Berichte zuerst, was proaktiv behoben wurde. Danach folgen verbleibende
entscheidungsrelevante Befunde mit Kategorie, Fundstellen, Evidenz, Risiko und
Empfehlung sowie die tatsächlich ausgeführten MCP-Abfragen und Tests.
