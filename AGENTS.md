# AiNetLinter – Agent Instructions & Development Rules

Willkommen beim **AiNetLinter**-Projekt! Dieses Dokument dient KI-Agenten (Antigravity, Cursor, Windsurf, Roo, etc.) als primäre Orientierung und Handlungsanleitung für Entwicklung, Refactoring und Wartung in diesem Repository.

---

## 1. Projekt-Überblick & Architektur

**AiNetLinter** ist eine Roslyn-basierte C#/.NET 10 Statische-Code-Analyse- & Linter-Engine zur Durchsetzung von Architekturregeln, Clean-Code-Standards und Konventionen.

### Schlüsselkomponenten:
- **Engine & Core CLI**: `src/AiNetLinter/`
  - `Cli/`: Argument-Parsing und CLI-Optionen System (System.CommandLine basiert).
  - `Generators/`: SyntaxWalker, Agent-Rules Sync und Skeleton Map.
  - `Rules/`: Roslyn-basierte Regel-Implementierungen.
  - `Diagnostics/`: Performance-Profiler und Messungen.
- **Fast Tests**: `src/AiNetLinter.FastTests/` (xUnit, Unit- und Component-Tests, rein in-memory / Roslyn Adhoc-Workspaces, < 10s Laufzeit).
- **Integration Tests**: `src/AiNetLinter.IntegrationTests/` (xUnit, Datei-I/O-, CLI-, Dogfood-, Performance- und Stress-Tests).
- **TestKit**: `src/AiNetLinter.TestKit/` (Wiederverwendbare Test-Infrastruktur, Fixtures, InMemory-Lösungen und Assertions).
- **Konfiguration**: `rules.json` definiert das aktive Regelwerk und Parameter.
- **Agent-Regeln (`.agents/rules/`)**: primäre Quelle für Coding-/Architektur-/Verhaltensregeln — `AiNetLinter.mdc` (auto-generiert aus `rules.json`, Linter-Metriken), `AiNetLinterRichtlinien.mdc` (Architektur, Workflow, Kommentar- und Verhaltensregeln, manuell gepflegt) und `AiNetLinter-McpWorkflow.mdc` (MCP-Entscheidungshilfe und Entwicklungszyklus für AiNetLinter). Details siehe Abschnitt 6.
- **Dokumentation**: `Docs/` enthält Systemdokumentation, CLI-Referenzen und Anleitungen.

> [!IMPORTANT]
> Dieses Repository registriert sich selbst als **MCP-Server `ainetlinter`** — für C#-Symbol-/Violation-Abfragen (`get_feature_context`, `find_symbol`, `find_references`, `get_impact`, `get_violations`, `safeguard`, `get_hotspots`, …) bevorzugt die semantischen MCP-Tools einsetzen; für Text- und Nicht-C#-Suchen bleiben `rg`/`grep` sinnvoll. Siehe `.agents/rules/AiNetLinter-McpWorkflow.mdc` und `Docs/integration.md` Abschnitt „Tool-vs-`rg`-Empfehlung für Agent-Loops".

### AiNetLinter-MCP: Initialisierung

Der MCP-Server wird ohne projektbezogene `--path`- oder `--config`-Argumente
registriert:

```json
{
  "mcpServers": {
    "ainetlinter": {
      "command": "ainetlinter",
      "args": ["--mcp-server"]
    }
  }
}
```

Jeder zielgebundene Tool- oder Resource-Aufruf verwendet ausschließlich den
absoluten `targetPath` einer vorhandenen `.sln`/`.slnx` (Source) oder `.dll`/`.exe`
(dekompilierte Assembly). Die Herkunft wird deterministisch aus der Endung
abgeleitet. Die jeweils aktuellen Tool-Argumente werden über `tools/list`
veröffentlicht; zusätzliche Argumente werden als `invalid_argument` abgelehnt.
Die einzige zielgebundene Ausnahme im MCP-Modus
ist `get_server_health` als globale Health-Abfrage; `report_observability_feedback`
bleibt ungebunden. Eine optionale `ainetlinter-rules.json` liegt direkt neben
der adressierten Source-Solution.

`--path` und `--config` bleiben dem Batch-Modus vorbehalten. Die Registry-Defaults
betragen 45 Minuten Idle-TTL und höchstens 4 residente Projekt-Keys; sie können
im MCP-Modus mit `--mcp-project-ttl-minutes` und `--mcp-max-projects` angepasst
werden.

---

## 2. Entwicklungs- & Test-Workflow

### Verifikation & Test-Kategorien
Die produktive Testsuite ist auf `src/AiNetLinter.FastTests` (`Unit`/`Component`) und `src/AiNetLinter.IntegrationTests` (`Integration`/`Dogfood`/`Performance`/`Stress`) aufgeteilt. Agenten sollen Testkategorien während der Entwicklung gezielt auswählen:

1. **Schnelle Iteration (während der Entwicklung)**:
   Verwende gefilterte Läufe für schnelles Feedback:
   ```bash
   dotnet test src/AiNetLinter.FastTests --filter Category=Unit
   ```

2. **Abschluss-Verifikation bei Codeänderungen**:
   Bei Änderungen an Produktions- oder Testcode MUSS vor dem Beenden eines Tasks ein vollständiger Testlauf über beide Zielprojekte grün durchgeführt werden — das schließt `Unit`/`Component` und `Integration`/`Dogfood`/`Performance` ein, NICHT `Stress` (siehe Punkt 4):
   ```bash
   dotnet test src/AiNetLinter.FastTests --filter Category!=Stress
   dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress
   ```

3. **Build prüfen bei Codeänderungen**:
   ```bash
   dotnet build
   ```
   Baut alle vier Projekte der Solution fehler- und warnungsfrei (`TreatWarningsAsErrors = true`).

4. **`Stress`-Kategorie (nur gezielt/manuell, nie automatisch)**:
   Tests, die absichtlich hohe parallele Last erzeugen (z. B. `McpTestClientParallelTests` mit 16 gleichzeitigen Server-Subprozessen, ~150s) sind `[Trait("Category", "Stress")]` getaggt. Sie laufen NICHT im normalen Volllauf (Punkt 2) und NICHT im Unit-Slice (Punkt 1) mit, sondern nur auf explizite Anforderung:
   ```bash
   dotnet test src/AiNetLinter.IntegrationTests --filter Category=Stress
   ```
   Neue absichtlich lastintensive/parallele Tests (nicht einfach nur "langsam", sondern gezielt Last/Nebenläufigkeit prüfend) gehören ebenfalls in diese Kategorie, nicht in `Integration`.

5. **Test-Ergebnisse & Logging**:
   Testläufe können mit `--logger "trx;LogFileName=<Name>.trx"` diagnostiziert werden (Details siehe `.agents/rules/AiNetLinterRichtlinien.mdc` §3).

 > [!IMPORTANT]
 > Für reine Dokumentations-, Markdown- oder Agenteninfrastrukturänderungen sind Build und Tests nicht erforderlich. Dafür genügen relevante Referenzprüfungen, `git diff --check` und eine sachliche Diff-Prüfung. Bei Produktions- oder Testcodeänderungen bleiben die vollständigen Nicht-Stress-Gates verbindlich.

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
- Änderungen an versionierten Dateien werden automatisch committed; ausschließlich eigene auftragsbezogene Änderungen dürfen in den Commit gelangen. Details zu Baseline-, Staging- und Parallel-Agenten-Schutz stehen in `.agents/rules/AiNetLinterRichtlinien.mdc` §4.

---

## 5. Task-Orchestration

- Bei größeren oder unklaren Vorhaben zuerst den `concept-planner`-Skill mit einem konkreten Task-Verzeichnis verwenden. Ein umsetzungsbereites Konzept trägt `status: ready`, `execution_mode: autonomous` und `open_questions: []`; danach den Orchestrator-Skill nur bei ausdrücklichem Wunsch einsetzen.
- Für zusammenhängende Features, Refactorings und andere mehrstufige Aufgaben: `.agents/skills/project-orchestrator/SKILL.md` sowie die benötigten Rollen unter `.agents/roles/` verwenden.
- Der Orchestrator wird nur mit dem Task-Verzeichnis oder dessen `Konzept.md` aufgerufen und arbeitet autonom über alle Slices bis zur Task-Abschlussbedingung. Ein Zwischenabschluss nach einem einzelnen Slice ist nicht zulässig.
- Innerhalb eines Tasks gilt strikt serielle Abarbeitung: höchstens ein delegierter Agent gleichzeitig; keine parallelen Rollen, Builds, Tests oder MCP-Prüfungen. Die nächste Rolle startet erst nach vollständiger Beendigung der vorherigen.
- Vor Abschluss eines größeren orchestrierten Tasks: die `auditor`-Rolle einmal für DRY-, Refactoring-Drift-, Dead-Code- und Magic-Value-Prüfungen über die passenden MCP-Tools einsetzen.

---

## 6. Code-Style, Architektur & Agenten-Verhalten

Sämtliche Coding-Konventionen, Architektur-Leitplanken, Qualitätsdrift-Prävention und Agenten-Verhaltensregeln (Sparring, Antwortstil) liegen ausschließlich in `.agents/rules/`, primär `.agents/rules/AiNetLinterRichtlinien.mdc`. Diese Datei hier bleibt bewusst ein schlanker Einstiegspunkt — Inhaltliches bitte dort pflegen, nicht hier duplizieren.
