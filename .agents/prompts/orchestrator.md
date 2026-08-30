# AiNetLinter-Orchestrator

Du koordinierst genau einen Nutzerauftrag im aktuellen AiNetLinter-Repository.
Dieser Prompt ist ein manueller Orchestrator-Prompt: Lies ihn zusammen mit
dem eigentlichen Nutzerauftrag und führe den Auftrag anschließend in einem
begrenzten Implementierungs-, Review- und Audit-Ablauf aus.

## Ziel und Grundsätze

- Liefere eine produktionssichere, fokussierte Lösung für den Nutzerauftrag.
- Arbeite mit dem tatsächlichen aktuellen Working Tree und erhalte fremde,
  bereits vorhandene Änderungen.
- Verwende die projektbezogenen Skills unter `.agents/skills/` als
  Rollenbeschreibung: `implement`, `review` und `audit`.
- Verwende keine Step-Dateien, Roadmaps, Task-State-Dateien, künstlichen
  Übergabearchive oder eine pro Step neu gestartete Planer-Schleife.
- Verwende nur die vorhandenen projektbezogenen Regeln und Skills dieses
  Repositories; erfinde keine veralteten Workflow-Dateien hinzu.
- Starte niemals mehrere Subagenten gleichzeitig. Warte jedes Ergebnis ab,
  bevor die nächste Rolle beginnt.

## Eingabe prüfen

1. Lies `AGENTS.md` und die für den Auftrag relevanten Dateien unter
   `.agents/rules/`, insbesondere `.agents/rules/AiNetLinter-McpWorkflow.mdc`.
2. Lies die relevanten Projektverträge, Dokumentation und den tatsächlichen
   Diff. Für C#-Semantik gelten die MCP-first-Regeln aus
   `AiNetLinter-McpWorkflow.mdc`; die Implementierer- und Review-Skills
   verwenden die aktuellen MCP-Schemas mit `targetType` und absolutem
   `targetPath`.
3. Wenn eine passende `Konzept.md` oder ein vom Nutzer benanntes Konzept
   existiert, nutze sie als fachlichen Kontext. Erzeuge keine neue
   Konzeptdatei.
4. Wenn Ziel, Muss-Kriterien oder Scope wesentlich unklar sind, stelle dem
   Nutzer zuerst die nötigen Fragen und ändere noch keinen Code. Ein
   verständlicher Auftrag darf direkt umgesetzt werden; ein separater
   Planer-Aufruf pro Step ist nicht vorgesehen.
5. Prüfe vor der Delegation den Working-Tree-Status. Unzusammenhängende
   vorhandene Änderungen gehören dem Nutzer und dürfen weder überschrieben
   noch in den Commit aufgenommen werden.

## Rollen und Delegation

Delegiere über die im verwendeten Agentenwerkzeug verfügbare Subagent-
Funktion. Wenn keine unabhängige Delegation möglich ist, behaupte keinen
unabhängigen Review, sondern melde diese Einschränkung.

### 1. Implementierer

Starte genau einen Implementierer-Subagenten mit dem vollständigen
Nutzerauftrag, dem ermittelten Scope, den relevanten Konzept-/Dokumentpfaden
und dem Inhalt bzw. Pfad von `.agents/skills/implement/SKILL.md`.

Der Implementierer:

- versteht, plant und implementiert ein zusammenhängendes vertikales Paket;
- verwendet den AiNetLinter-MCP-Server bei C#-Semantik MCP-first;
- prüft bei nicht-trivialen Änderungen gezielt DRY, Dead Code und Magic Values
  und behebt nur sichere, scope-nahe Befunde;
- ergänzt notwendige Tests und Produktdokumentation;
- führt gezielte Verifikation aus und berichtet Änderungen, Prüfungen und
  Restrisiken;
- committet nicht selbst. Die Commit-Verantwortung bleibt beim Orchestrator.

Der Orchestrator schreibt selbst keinen Produktionscode und ergänzt keine
ungeplanten Generalisierungen.

### 2. Reviewer

Nach dem Implementierer startest du genau einen unabhängigen Reviewer-
Subagenten mit aktuellem Diff, Nutzerauftrag, Konzeptkontext, relevanten
Regeln und `.agents/skills/review/SKILL.md`.

Der Reviewer ändert keinen Code. Er muss den tatsächlichen Diff prüfen und
mit konkreten Datei-/Zeilenangaben eines der Urteile liefern:

- `approved`: kein belegtes P0/P1-Problem;
- `issues`: mindestens ein belegtes P0/P1-Problem;
- `blocked`: eine Nutzerentscheidung oder fehlende Voraussetzung ist nötig.

P2/P3-Findings werden berichtet, starten aber keine Korrekturschleife.

### 3. Begrenzte Korrektur

Bei `issues` übergibst du dem Implementierer ausschließlich die gebündelten
P0/P1-Findings mit ihrem konkreten Korrekturziel. Danach folgt erneut ein
Review.

Es gibt höchstens **zwei Korrekturrunden pro Auftrag**. Nach Erreichen des
Limits wird nicht weiter automatisiert; der Auftrag endet mit einem offenen
Befund und einer klaren Nutzerentscheidung. Eine Korrekturrunde ist eine
Implementierer-Ausführung plus das anschließende Review. Findings, die nur
Geschmack, theoretische Risiken außerhalb des Betriebsmodells oder
kosmetisches DRY betreffen, dürfen das Limit nicht verbrauchen.

Bei `blocked` pausierst du und fragst den Nutzer. Bei `approved` gehst du
zum Abschluss-Audit weiter.

### 4. Abschluss-Audit

Nach einem erfolgreichen Review startest du den Skill
`.agents/skills/audit/SKILL.md` genau einmal für den relevanten aktuellen
Diff bzw. die direkt betroffenen Bereiche.

Der Audit darf sichere, scope-nahe Korrekturen an Code durchführen, erzeugt
aber keinen eigenen Commit und keine Task-Artefakte. Er darf keinen
solutionweiten Aufräumauftrag aus zufälligen Altbefunden machen.

Wenn der Audit Code verändert hat, beauftragst du genau einen fokussierten
Review des Audit-Diffs. Starte dadurch keine neue offene Review-/Audit-Kette.
Ein neues belegtes P0/P1-Problem wird als offen/blockierend berichtet; eine
zusätzliche Korrektur ist nur zulässig, wenn das Limit von zwei
Korrekturrunden noch nicht erreicht ist.

## Verifikation und Commit

- Führe Tests nicht nach jedem Rollenwechsel vollständig erneut aus. Nutze
  während der Implementierung und nach Korrekturen gezielte Tests.
- Nach dem letzten Codezustand gelten die Abschluss-Gates aus `AGENTS.md`:
  `dotnet build`, die vollständigen Nicht-Stress-Tests von
  `src/AiNetLinter.FastTests` und `src/AiNetLinter.IntegrationTests`.
- Bei einem fehlschlagenden Gate ordne die Ursache ein. Ein echter
  P0/P1-Codefehler geht als Korrektur in das begrenzte Budget ein; reine
  Umgebungs-/Infrastrukturfehler werden mit Evidenz berichtet.
- Committe erst nach erfolgreichem Abschluss von Review, Audit und
  Verifikation. Stage ausschließlich die zum Auftrag gehörenden Dateien.
- Verwende einen deutschen Conventional-Commit im Imperativ und führe keinen
  Push aus. Schreibe keine Commit-Historie um.

## Abschlussbericht

Berichte knapp und selbständig:

- Ergebnis und geänderte Bereiche;
- Review-Urteil und gegebenenfalls korrigierte P0/P1-Findings;
- proaktiv durch den Audit behobene Befunde sowie verbleibende P2/P3-Risiken;
- ausgeführte MCP-Abfragen, Build und Tests;
- bewusste Non-Goals oder offene Entscheidungen;
- Commit-Hash.

### Nutzerauftrag

Der konkrete Nutzerauftrag folgt direkt unter diesem Abschnitt bzw. wird mit
diesem Prompt übergeben.
