# AiNetLinter – Agent Instructions & Development Rules

Willkommen beim **AiNetLinter**-Projekt! Dieses Dokument dient KI-Agenten (Antigravity, Cursor, Windsurf, Roo, etc.) als primäre Orientierung und Handlungsanleitung für Entwicklung, Refactoring und Wartung in diesem Repository.

---

## 1. Projekt-Überblick & Architektur

**AiNetLinter** ist eine Roslyn-basierte C#/.NET 9 Statische-Code-Analyse- & Linter-Engine zur Durchsetzung von Architekturregeln, Clean-Code-Standards und Konventionen.

### Schlüsselkomponenten:
- **Engine & Core CLI**: `src/AiNetLinter/`
  - `Cli/`: Argument-Parsing und CLI-Optionen System (System.CommandLine basiert).
  - `Generators/`: SyntaxWalker, Agent-Rules Sync, Skeleton Map & Playbook Generierung.
  - `Rules/`: Roslyn-basierte Regel-Implementierungen.
  - `Diagnostics/`: Performance-Profiler und Messungen.
- **Unit & Integration Tests**: `src/AiNetLinter.Tests/` (xUnit, Roslyn Workspace/MSBuild Workspaces).
- **Konfiguration**: `rules.json` definiert das aktive Regelwerk und Parameter.
- **Agent-Regeln (`.agents/rules/`)**: primäre Quelle für Coding-/Architektur-/Verhaltensregeln — `AiNetLinter.mdc` (auto-generiert aus `rules.json`, Linter-Metriken) und `AiNetLinterRichtlinien.mdc` (Architektur, Workflow, Kommentar- und Verhaltensregeln, manuell gepflegt). Details siehe Abschnitt 6.
- **Dokumentation**: `Docs/` enthält Systemdokumentation, CLI-Referenzen und Anleitungen.

> [!IMPORTANT]
> Dieses Repository registriert sich selbst als **MCP-Server `ainetlinter`** (`.mcp.json`) — für C#-Symbol-/Violation-Abfragen (`find_symbol`, `find_references`, `get_impact`, `get_violations`, `safeguard`, `get_hotspots`, …) **vor** `rg`/`grep` verwenden, siehe `.agents/rules/AiNetLinterRichtlinien.mdc` §1 und `Docs/integration.md` Abschnitt „Tool-vs-`rg`-Empfehlung für Agent-Loops".

---

## 2. Entwicklungs- & Test-Workflow

### Verifikation & Test-Kategorien
Da die gesamte Testsuite durch Integrationstests und MCP-Subprozesse zeitintensiv sein kann, sind die Tests in `Unit`, `Integration` und `Stress` kategorisiert. Agenten sollen Testkategorien während der Entwicklung gezielt auswählen:

1. **Schnelle Iteration (während der Entwicklung)**:
   Verwende gefilterte Läufe für schnelles Feedback (z. B. Unit-Tests in ~23-24 Sekunden):
   ```bash
   dotnet test --filter Category=Unit
   ```
   (oder alternativ `dotnet test --filter Category!=Integration`)

2. **Abschluss-Verifikation (vor Task-Beendigung)**:
   Vor dem Beenden eines Tasks MUSS ein vollständiger Testlauf grün durchgeführt werden — das schließt `Unit` und `Integration` ein, NICHT `Stress` (siehe Punkt 4):
   ```bash
   dotnet test --filter Category!=Stress
   ```

3. **Build prüfen**:
   ```bash
   dotnet build
   ```

4. **`Stress`-Kategorie (nur gezielt/manuell, nie automatisch)**:
   Tests, die absichtlich hohe parallele Last erzeugen (z. B. `McpTestClientParallelTests` mit 16 gleichzeitigen Server-Subprozessen, ~150s) sind `[Trait("Category", "Stress")]` statt `Integration` getaggt. Sie laufen NICHT im normalen Volllauf (Punkt 2) und NICHT im Unit-Slice (Punkt 1) mit, sondern nur auf explizite Anforderung:
   ```bash
   dotnet test --filter Category=Stress
   ```
   Neue absichtlich lastintensive/parallele Tests (nicht einfach nur "langsam", sondern gezielt Last/Nebenläufigkeit prüfend) gehören ebenfalls in diese Kategorie, nicht in `Integration`.

5. **Test-Ergebnisse & Logging**:
   Das Ergebnis wird in `TestResults/latest.trx` geloggt (Details & Diagnose-Workflow siehe `.agents/rules/AiNetLinterRichtlinien.mdc` §3).

> [!IMPORTANT]
> Beende einen Task erst, wenn `dotnet test --filter Category!=Stress` (Volllauf ohne Stress-Tests) grün durchgelaufen ist!

---

## 3. Dokumentations- & Regel-Synchronisation

- **Regel- oder CLI-Änderungen**:
  Wenn CLI-Optionen, `rules.json`-Schemata oder Regel-Verhalten geändert werden, MÜSSEN folgende Dokumente aktualisiert werden:
  - `Docs/configuration.md`
  - `Docs/ROADMAP.md` (falls Meilensteine betroffen sind)
- **Agenten-Regeln Sync**:
  Die Agenten-Regeldatei `.agents/rules/AiNetLinter.mdc` wird aus `rules.json` generiert und kann mit folgenden Befehlen synchronisiert werden:
  ```bash
  dotnet run --project src/AiNetLinter -- --sync-agent-rules-only
  ```

---

## 4. Commit- & PR-Konventionen

- Conventional Commits **auf Deutsch**, imperativ (z. B. `feat:`, `fix:`, `docs:`, `chore:`).
- Weitere Format-Pflichten (u. a. Pflicht-`### Commit-Vorschlag`-Block): siehe `.agents/rules/AiNetLinterRichtlinien.mdc` §4.

---

## 5. Dev-Loop & Task-Orchestration

- Für mehrstufige Aufgaben (Audits, Refactorings, Features): siehe `.agents/Agent-Scaffolding/dev-loop/README.md`.
- Vor Abschluss eines Epics oder eines größeren Tasks: `.agents/skills/drift-audit/SKILL.md` einmal ausführen (DRY-Audit über `find_duplicates`). Für einzelne Steps innerhalb eines Tasks ist die Ausführung optional.

---

## 6. Code-Style, Architektur & Agenten-Verhalten

Sämtliche Coding-Konventionen, Architektur-Leitplanken, Qualitätsdrift-Prävention und Agenten-Verhaltensregeln (Sparring, Antwortstil) liegen ausschließlich in `.agents/rules/`, primär `.agents/rules/AiNetLinterRichtlinien.mdc`. Diese Datei hier bleibt bewusst ein schlanker Einstiegspunkt — Inhaltliches bitte dort pflegen, nicht hier duplizieren.
