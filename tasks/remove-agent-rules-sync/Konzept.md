---
status: draft
execution_mode: autonomous
open_questions: []
---

# Restlose Entfernung des Agent-Rules-Sync (AiNetLinter.mdc Generator)

## Freigabe- und Umsetzungsvertrag

Dieses Konzept beschreibt die vollständige, restlose Entfernung des Agent-Rules-Synchronisations-Mechanismus (`AgentRulesGenerator`, `SyncAgentRulesCommand`, CLI-Flags `-sar`/`--sync-agent-rules`, `-saro`/`--sync-agent-rules-only`, `-arp`/`--agent-rules-path`) sowie der daraus erzeugten Datei `.agents/rules/AiNetLinter.mdc`.

`execution_mode: autonomous` erlaubt dem späteren Orchestrator nach erfolgter Freigabe, alle beschriebenen Slices seriell und ohne Zwischenfragen bis zum Release-Gate umzusetzen.

Der Status bleibt bewusst `draft`, bis der Nutzer das Konzept ausdrücklich freigibt. Es gibt keine offenen fachlichen Nutzerentscheidungen. Der Orchestrator startet nicht automatisch.

### Verbindlicher harter Schnitt

- **Keine Legacy-Reste:** Der Code für die Generierung von MDC-Dateien wird vollständig gelöscht. Es verbleiben keine Deprecation-Warnungen, verwaiste Hilfsmethoden, Feature-Flags oder Auskommentierungen.
- **CLI-Schnitt:** Die Optionen `--sync-agent-rules` (`-sar`), `--sync-agent-rules-only` (`-saro`) und `--agent-rules-path` (`-arp`) werden restlos aus der CLI-Definition, dem Parsing und dem Dispatching entfernt. Unbekannte alte Optionen führen zu regulären CLI-Fehlern.
- **Regel-Zuständigkeit:** Die handgepflegte Datei `.agents/rules/AiNetLinterRichtlinien.mdc` ist die alleinige Quelle für agentenbezogene Architektur-, Stil- und Workflow-Vorgaben im Repository. Die automatisch erzeugte Datei `.agents/rules/AiNetLinter.mdc` wird gelöscht und künftig nicht mehr generiert.
- **Dokumentation:** Alle Erwähnungen des MDC-Sync in `AGENTS.md`, `Docs/cli.md`, `Docs/configuration.md`, `Docs/integration.md` sowie in XML-Dokumentationskommentaren von C#-Klassen werden bereinigt. Die Dokumentation beschreibt ausschließlich den gültigen Zustand nach dem Schnitt, ohne historische Migrationshinweise.

---

## Ziel & Problemstellung

### Problem

1. **Context-Waste & Attention Dilution:** Die automatisch generierte Datei `.agents/rules/AiNetLinter.mdc` wird bei jeder C#-Interaktion mit `alwaysApply: true` ungefragt in das Context-Fenster geladen (~88 Zeilen, ~1.500–2.000 Token). Sie überflutet den Agenten mit detaillierten Schwellwerten (inkl. irrelevanter CSS-, Razor- und JavaScript-Limits) und schwächt die Aufmerksamkeit für wesentliche Architekturregeln.
2. **Kognitiver Fehlschluss von Metrik-Tabellen im Prompt:** LLMs können während des Codierens weder Codezeilen zählen noch kognitive Komplexität noch transitive Abhängigkeitsgraphen (`AIContextFootprint ≤ 2500`) im Kopf berechnen. Die Annahme, ein Agent könne Code anhand einer statischen Schwellenwerttabelle vorab linter-konform generieren, trifft in der Praxis nicht zu.
3. **Kategoriefehler in der Linter-Architektur:** Ein Linter hat die Aufgabe, Quellcode statisch zu analysieren und deterministische Diagnosen via CLI, MCP, SARIF und JSON zu liefern. Das automatische Generieren von LLM-System-Prompts aus maschinellen Konfigurationsdateien (`ainetlinter-rules.json`) erzeugt minderwertige Prompts voller Zahlen und ohne echte Handlungsanweisungen („Was tun bei Verstoß?“).
4. **Moderne MCP-Architektur macht das Feature obsolet:** Im modernen MCP-Workflow (`get_violations`, `safeguard`, `get_feature_context`) fragt der Agent Diagnosen gezielt nach Änderungen ab. Die Stärke liegt im Feedback-Loop („Schreiben → Linter/MCP meldet konkrete Zeile & Regel → Agent refaktoriert“), nicht im Auswendiglernen von Grenzwerten.

### Ziel

Vollständige Entfernung des Generators, aller CLI-Bindings, zugehöriger Tests und Dokumentationsstellen, um AiNetLinter schlanker, wartungsärmer und fokussierter auf seine Kernkompetenz (statische Roslyn-Codeanalyse und MCP-Dienste) zu machen.

---

## Betroffene Bereiche und Scope

### 1. Zu löschende Dateien (Dateiebene)

- `.agents/rules/AiNetLinter.mdc`
- `src/AiNetLinter/Generators/AgentRulesGenerator.cs`
- `src/AiNetLinter/Commands/SyncAgentRulesCommand.cs`
- `src/AiNetLinter.IntegrationTests/Commands/SyncAgentRulesFileIntegrationTests.cs`
- `src/AiNetLinter.FastTests/Commands/SyncAgentRulesPolicyTests.cs`

### 2. Zu modifizierende Produktions-Dateien

- `src/AiNetLinter/Cli/CliOptionFactory.cs`:
  - Entfernen von `CreateSyncAgentRulesOption()`, `CreateSyncAgentRulesOnlyOption()`, `CreateAgentRulesPathOption()`.
- `src/AiNetLinter/Cli/CliRunner.cs` (bzw. `Program.cs`):
  - Entfernen der Optionen aus dem RootCommand.
  - Entfernen des Fast-Paths für `--sync-agent-rules-only`.
  - Entfernen des nachgelagerten Aufrufs von `SyncAgentRulesCommand.Run()` bei gesetztem `--sync-agent-rules`.
- `src/AiNetLinter/Cli/LinterArgs.cs`:
  - Entfernen der Properties `SyncAgentRules`, `SyncAgentRulesOnly`, `AgentRulesPath`.

### 3. Zu modifizierende Test-Dateien

- `src/AiNetLinter.FastTests/Cli/ProgramParsingTests.cs`:
  - Entfernen von Testfällen, die `--sync-agent-rules-only`, `--agent-rules-path` oder `-sar` parsen.
- `src/AiNetLinter.IntegrationTests/Configuration/DeveloperExperienceTests.cs`:
  - Entfernen der Testmethoden, die `AgentRulesGenerator` oder MDC-Synchronisation prüfen.
- `src/AiNetLinter.IntegrationTests/Cli/CliFixtureIntegrationTests.cs`:
  - Bereinigen von Tests, die `expectedMdcPath` als Nebeneffekt prüfen.

### 4. XML-Dokumentationskommentare im C#-Code bereinigen

Ca. 15 Stellen verweisen in XML-Docstrings auf `AiNetLinter.mdc` (z. B. `/// (siehe <c>AiNetLinter.mdc</c>)`). Diese werden neutralisiert und verweisen künftig auf die jeweilige Linter-Regel bzw. `ainetlinter-rules.json`:
- `src/AiNetLinter/Mcp/McpServerOptionsFactory.cs`
- `src/AiNetLinter/Mcp/Registration/SymbolGraphToolRegistrations.cs`
- `src/AiNetLinter/Mcp/Tools/SymbolGraph/GetTypeHierarchyFormatter.cs`
- `src/AiNetLinter/Mcp/Tools/SymbolGraph/SymbolIdentifierResolver.cs`
- `src/AiNetLinter/Mcp/Tools/Safeguard/SafeguardTool.cs`
- `src/AiNetLinter/Mcp/Tools/Safeguard/SafeguardModels.cs`
- `src/AiNetLinter/Mcp/Tools/PatternDetect/PatternDetectScanner.cs`
- `src/AiNetLinter/Mcp/Server/McpCodeGraphServerOptions.cs`
- `src/AiNetLinter/Mcp/Tools/MagicValues/FindMagicValuesScannerRecords.cs`
- `src/AiNetLinter/Mcp/Tools/MagicValues/FindMagicValuesScannerWalker.cs`
- `src/AiNetLinter/Mcp/Tools/MagicValues/MagicValuesStringHeuristics.cs`
- `src/AiNetLinter/Mcp/Tools/MagicValues/MagicValuesNumberClassifier.cs`
- `src/AiNetLinter/Mcp/Tools/Analysis/GetViolationsScanner.cs`
- `src/AiNetLinter/Mcp/Tools/DependencyGraph/DependencyGraphModels.cs`
- `src/AiNetLinter/Core/Checkers/CheckerContext.cs`
- `src/AiNetLinter.FastTests/Mcp/McpCodeGraphServerConstructorTests.cs`
- `src/AiNetLinter.FastTests/Mcp/Tools/FindMagicValues/FindMagicValuesScannerMalfunctionTests.cs`
- `src/AiNetLinter.FastTests/Mcp/Tools/FindMagicValues/FindMagicValuesTestHelpers.cs`

### 5. Dokumentation & Richtlinien

- `AGENTS.md`:
  - Abschnitt 3 („Dokumentations- & Regel-Synchronisation“): Den Aufruf `dotnet run --project src/AiNetLinter -- --sync-agent-rules-only` sowie Referenzen auf `AiNetLinter.mdc` entfernen.
  - Abschnitt 1 („Schlüsselkomponenten“): Verweis auf `AiNetLinter.mdc` bereinigen.
- `Docs/configuration.md`:
  - Erwähnungen von `-sar`, `-saro`, `-arp` und den MDC-Sync-Abschnitt entfernen.
- `Docs/cli.md`:
  - CLI-Tabellen und Optionen für Agent-Rules-Sync entfernen.
- `Docs/integration.md`:
  - Verweise auf `--sync-agent-rules` entfernen.
- `.agents/rules/AiNetLinterRichtlinien.mdc`:
  - Ergänzen um einen prägnanten Abschnitt für C#-Coding-Heuristiken (z. B. `sealed` für konkrete Klassen, `#nullable enable`, flache Methoden, kein `async void`, kein `.Result`, keine leeren `catch`-Blöcke), damit Agenten beim Schreiben ohne Context-Overhead die bewährten Standards einhalten.

---

## Muss- und Akzeptanzkriterien

### Muss-Kriterien

1. **Kein verbleibender Generator-Code:** Weder `AgentRulesGenerator` noch `SyncAgentRulesCommand` noch deren Klassen/Methoden existieren im Source-Tree.
2. **Saubere CLI:** Der Befehl `AiNetLinter.exe` akzeptiert weder `-sar`, `--sync-agent-rules`, `-saro`, `--sync-agent-rules-only` noch `-arp`, `--agent-rules-path`.
3. **MDC-Datei entfernt:** Die Datei `.agents/rules/AiNetLinter.mdc` existiert nicht mehr im Dateisystem.
4. **Keine verwaisten Referenzen:** Weder im Code noch in der Dokumentation verweisen aktive Texte auf den gelöschten Generator oder `AiNetLinter.mdc`.
5. **Kompilierbar ohne Warnungen:** `dotnet build` baut alle vier Projekte fehler- und warnungsfrei (`TreatWarningsAsErrors = true`).
6. **Grüne Testsuite:** Alle Unit- und Integrationstests (ohne Stress-Kategorie) laufen erfolgreich durch:
   ```bash
   dotnet test src/AiNetLinter.FastTests --filter Category!=Stress
   dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress
   ```

### Non-Goals

- Keine Änderungen an der Regellogik der Analyzer oder an `ainetlinter-rules.json`.
- Keine Änderungen an der Funktionsweise der MCP-Tools (außer Bereinigung von Docstring-Referenzen).
- Keine Einführung alternativer Prompt-Generatoren.

---

## Umsetzungsplan in logischen Slices (für den Orchestrator)

### Slice 1: CLI-Optionen, Argumente und Command-Dispatcher bereinigen
- In `src/AiNetLinter/Cli/CliOptionFactory.cs` die Methoden `CreateSyncAgentRulesOption`, `CreateSyncAgentRulesOnlyOption` und `CreateAgentRulesPathOption` entfernen.
- In `src/AiNetLinter/Cli/LinterArgs.cs` die entsprechenden Properties entfernen.
- In `src/AiNetLinter/Program.cs` / `CliRunner.cs` die RootCommand-Optionen und die Verzweigung auf `SyncAgentRulesCommand` entfernen.

### Slice 2: Generator- & Command-Klassen sowie Tests löschen / bereinigen
- `src/AiNetLinter/Generators/AgentRulesGenerator.cs` löschen.
- `src/AiNetLinter/Commands/SyncAgentRulesCommand.cs` löschen.
- `src/AiNetLinter.IntegrationTests/Commands/SyncAgentRulesFileIntegrationTests.cs` löschen.
- `src/AiNetLinter.FastTests/Commands/SyncAgentRulesPolicyTests.cs` löschen.
- `src/AiNetLinter.FastTests/Cli/ProgramParsingTests.cs`, `src/AiNetLinter.IntegrationTests/Configuration/DeveloperExperienceTests.cs` und `CliFixtureIntegrationTests.cs` bereinigen.
- Zwischen-Build und schnelle FastTests verifizieren.

### Slice 3: Docstrings & Datei `.agents/rules/AiNetLinter.mdc` entfernen
- `.agents/rules/AiNetLinter.mdc` löschen.
- Alle ca. 15 Stellen in C#-Docstrings anpassen (`/// (siehe <c>AiNetLinter.mdc</c>)` $\to$ neutrale Nennung der Regel).

### Slice 4: Dokumentation und `AiNetLinterRichtlinien.mdc` synchronisieren
- `AGENTS.md` von `AiNetLinter.mdc`- und Sync-Referenzen bereinigen.
- `Docs/configuration.md`, `Docs/cli.md` und `Docs/integration.md` aktualisieren.
- In `.agents/rules/AiNetLinterRichtlinien.mdc` die C#-Kernheuristiken (sealed, nullable, kein async void, keine leeren catch) schlank verankern.

### Slice 5: Vollständige Verifikation & Release-Gate
- `dotnet build`
- `dotnet test src/AiNetLinter.FastTests --filter Category!=Stress`
- `dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress`
- Repo-weites `git grep -i "AgentRulesGenerator"`, `git grep -i "sync-agent-rules"`, `git grep "AiNetLinter.mdc"` zur Absicherung, dass keine toten Überreste existieren.

---

## Verifikation und Release-Gate

Das Release-Gate gilt als bestanden, wenn:
1. `dotnet build` alle vier Projekte ohne Fehler und ohne Warnungen kompiliert.
2. Beide Test-Suites (FastTests & IntegrationTests, Nicht-Stress) vollständig grün sind.
3. Git-Diff ausschließlich Löschungen und Bereinigungen des besprochenen Scopes zeigt.
