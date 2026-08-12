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
Die produktive Testsuite ist auf drei Zielprojekte verteilt (`src/AiNetLinter.FastTests`,
`src/AiNetLinter.IntegrationTests`, das aktuell noch leere `src/AiNetLinter.TestKit`), innerhalb
derer die Tests in `Unit`/`Component` (FastTests) bzw. `Integration`/`Dogfood`/`Performance`/`Stress`
(IntegrationTests) kategorisiert sind. Agenten sollen Testkategorien während der Entwicklung gezielt
auswählen:

1. **Schnelle Iteration (während der Entwicklung)**:
   Verwende gefilterte Läufe für schnelles Feedback:
   ```bash
   dotnet test src/AiNetLinter.FastTests --filter Category=Unit
   ```

2. **Abschluss-Verifikation (vor Task-Beendigung)**:
   Vor dem Beenden eines Tasks MUSS ein vollständiger Testlauf über beide Zielprojekte grün
   durchgeführt werden — das schließt `Unit`/`Component` und `Integration`/`Dogfood`/`Performance`
   ein, NICHT `Stress` (siehe Punkt 4):
   ```bash
   dotnet test src/AiNetLinter.FastTests --filter Category!=Stress
   dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress
   ```

3. **Build prüfen**:
   ```bash
   dotnet build
   ```
   Baut weiterhin alle fünf Projekte der Solution inklusive des quarantänierten Legacy-Projekts
   (siehe Punkt 6).

4. **`Stress`-Kategorie (nur gezielt/manuell, nie automatisch)**:
   Tests, die absichtlich hohe parallele Last erzeugen (z. B. `McpTestClientParallelTests` mit 16 gleichzeitigen Server-Subprozessen, ~150s) sind `[Trait("Category", "Stress")]` getaggt. Sie laufen NICHT im normalen Volllauf (Punkt 2) und NICHT im Unit-Slice (Punkt 1) mit, sondern nur auf explizite Anforderung:
   ```bash
   dotnet test src/AiNetLinter.IntegrationTests --filter Category=Stress
   ```
   Neue absichtlich lastintensive/parallele Tests (nicht einfach nur "langsam", sondern gezielt Last/Nebenläufigkeit prüfend) gehören ebenfalls in diese Kategorie, nicht in `Integration`.

5. **Test-Ergebnisse & Logging**:
   Das Ergebnis wird in `TestResults/latest.trx` geloggt (Details & Diagnose-Workflow siehe `.agents/rules/AiNetLinterRichtlinien.mdc` §3).

6. **Legacy-Projekt `AiNetLinter.Tests` (quarantäniert)**:
   `AiNetLinter.Tests` bleibt Teil der Solution und baubar (Punkt 3), ist aber **nicht mehr Teil
   des normalen Gates** (Punkt 1/2). Bei Änderung an noch nicht migriertem Produktcode (siehe
   `tasks/speedup-tests/test-migration-ledger.md`, Status `pending`) gezielt nur den betroffenen
   engsten Legacy-Filter aus dem Ledger ausführen, kein solutionweiter Legacy-Lauf:
   ```bash
   dotnet test src/AiNetLinter.Tests --filter FullyQualifiedName~<BetroffeneTestklasse>
   ```

> [!IMPORTANT]
> Beende einen Task erst, wenn sowohl `dotnet test src/AiNetLinter.FastTests --filter
> Category!=Stress` als auch `dotnet test src/AiNetLinter.IntegrationTests --filter
> Category!=Stress` grün durchgelaufen sind!

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
