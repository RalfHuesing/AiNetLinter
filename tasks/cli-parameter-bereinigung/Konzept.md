---
status: ready
type: konzept
project_kind: brownfield
estimated_scope: medium
priority: P1
agent_role: .agents/Agent-Scaffolding/prompts/dev/sparring.md
rules_dir: .agents/rules
last_updated: 2026-08-26
open_questions: []
---

# Radikale Bereinigung ungenutzter CLI-Parameter & Altlasten

## 1. Executive Summary & 360-Grad-Bewertung

### Kernaussage
**Die ersatzlose und restlose Löschung aller 14 vorgeschlagenen CLI-Parameter ist zu 100 % architekturgerecht, sinnvoll und dringend geboten.**

AiNetLinter hat sich klar in zwei scharfe Verantwortungsbereiche aufgeteilt:
1. **CLI / `.exe`:** Deterministische, blitzschnelle Regelvalidierung und Linting in CI/Unit-Tests (inkl. Baseline-Ratchet, Wave-Migration und Roslyn Auto-Fixer) sowie Synchronisation der Agent-Regeln.
2. **MCP-Server:** Interaktive, symbolgraph- und semantikbasierte Code-Exploration für AI-Agenten (`get_impact`, `metrics_lookup`, `get_feature_context`, `get_file_skeleton`, `safeguard`, `get_violations`, etc.).

Die 14 Kandidaten stammen aus einer frühen Phase vor der Etablierung des MCP-Servers, in der versucht wurde, interaktive Abfragen (Footprint, Playbook, Debt-Report, Diff-Impact) und Ad-hoc-Filterungen (Glob-Projekte, Namespaces) über Konsolen-Optionen abzubilden. Seit dem Ausbau des MCP-Servers und deklarativer `ProjectOverrides` in `rules.json` sind diese CLI-Parameter toter Ballast.

---

## 2. Was wir NICHT tun (Non-Goals & Invarianten)

> [!IMPORTANT]
> **Die MCP-Server-Funktionalität wird NICHT geändert.**
> Sämtliche MCP-Tools (`get_impact`, `get_file_skeleton`, `metrics_lookup`, `get_feature_context`, `get_violations`, `get_namespace_tree`, `safeguard`, etc.), ihre Namen, Eingabeparameter, JSON-Schemas, Tool-Annotations und Antwortverträge (Text & `StructuredContent`) bleiben **nach außen 100 % unverändert**.
> Die Bereinigung betrifft ausschließlich die CLI-Exposition (`.exe`) und deren internen toten Code.

---

## 3. Schärfungs- & Akzeptanzkriterium: Zero-Finding-Garantie

Nach Abschluss der Bereinigung MUSS eine systemweite Suche (`rg`) nach allen 14 CLI-Parametern im gesamten Repository (C#-Code, Unit- und Integrationstests, Dokumentation unter `Docs/`, `README.md`, `.agents/rules/`) **absolut null Treffer (0 Findings)** liefern:

- `--footprint`
- `--git-since`
- `--playbook`
- `--impact`
- `--debt-report`
- `--check`
- `--project`
- `--exclude-project`
- `--namespace`
- `--exclude-namespace`
- `--exclude-tests`
- `--tests-only`
- `--public-only`
- `--ignore-suppressions`

---

## 4. 360-Grad-Detailanalyse der 14 Kandidaten

| # | Parameter | Ursprünglicher Zweck | Warum heute obsolet? | MCP-Äquivalent | Betroffene Komponenten / Klassen |
|:---|:---|:---|:---|:---|:---|
| 1 | `--footprint <Klasse>` | Ad-hoc-Abfrage der transitiven Zeilen & Top-3 Abhängigkeiten einer Klasse auf stdout | Reine CLI-Abfrage. Die *Regel* `MaxAIContextFootprint` läuft im Audit automatisch; symbolgenaue Abfragen macht der Agent per MCP. | `metrics_lookup`, `get_feature_context` | `FootprintCommand.cs` (löschen). `AIContextFootprintCalculator.cs` bleibt für Engine/MCP erhalten. |
| 2 | `--git-since <ref>` | Audit-Filterung auf Dateien aus `git diff <ref>` | Brittle Git-Subprozess-Kopplung im CLI-Audit (bricht bei Shallow Clones in CI). Für inkrementelle CI gibt es `--baseline` / `--only-changed`. | `get_impact(gitRef)` | `GitChangedFilesResolver.cs` (löschen), `AuditCommand.cs`. |
| 3 | `--playbook` / `-pb` | Generierung eines statischen Markdown-Playbooks (`.md`/`.mdc`) mit Suppression-Statistiken | Statische 500-Zeilen-Playbooks liest kein moderner LLM-Agent. Agenten nutzen live MCP-Tools. | `ainetlinter://overview`, `get_index_scope`, `pattern_detect` | `RepoPlaybookGenerator.cs`, `PlaybookSyntaxWalker.cs`, `PlaybookTypes.cs`, `PlaybookCheckCommand.cs` (alle löschen). |
| 4 | `--impact` / `-im` | Textausgabe betroffener Call-Sites bei Signaturänderungen ab Git-Ref | Das MCP-Tool `get_impact` ist um Welten mächtiger (unterstützt `callers`, `change-context`, strukturierte JSON-Antworten, Test-Zuordnungen). | `get_impact` (Symbol & Git-Diff) | `ImpactCommand.cs` (löschen). `DiffImpactAnalyzer.cs` bleibt als Kern für MCP `GetImpactTool` erhalten. |
| 5 | `--debt-report` | Text-Report über `// ainetlinter-disable all`-Kommentare nach Ordnern | CLI-Use-Case ist Regeldurchsetzung (Exit 1), kein Reporting. Für Tech-Debt-Analysen nutzt der Agent MCP. | `get_violations`, `safeguard`, `get_hotspots` | `DebtReportCommand.cs`, `DebtReportBuilder.cs` (löschen). |
| 6 | `--check` | Drift-Prüfung für Playbook & SyncAgentRules; Dry-Run für `--fix` | Verliert mit `--playbook` seinen Hauptzweck. `--sync-agent-rules-only` ist bereits idempotent (schreibt nur bei Diff); `--fix` wird direkt durch Git-Diff kontrolliert. | N/A | `PlaybookCheckCommand.cs` (löschen), `SyncAgentRulesCommand.cs`, `LinterAutoFixer.cs` (`FixOptions.Check` entfernen). |
| 7 | `--project` | Glob-Include für Projektnamen im Batch-Lauf | Projekt-Ausnahmen gehören deklarativ und versioniert in `rules.json` unter `"ProjectOverrides"`. | `scopeFilter` in `get_violations` | `SourceFileCatalog.cs`, `CliOptions.cs`. |
| 8 | `--exclude-project` | Glob-Exclude für Projektnamen | Gehört deklarativ in `rules.json` `"ProjectOverrides"`. | `scopeFilter` in `get_violations` | `SourceFileCatalog.cs`, `CliOptions.cs`. |
| 9 | `--namespace` | Glob-Include für C#-Namespaces | Erzwingt `NamespaceFilter`-Checks auf jedem AST-Knoten im SyntaxWalker. Architekturregeln gehören in `rules.json`. | `get_namespace_tree`, `get_violations` | `NamespaceFilter.cs` (löschen), `LinterAnalyzer.cs`. |
| 10 | `--exclude-namespace` | Glob-Exclude für Namespaces | Siehe `--namespace`. | `get_namespace_tree` | `NamespaceFilter.cs` (löschen), `LinterAnalyzer.cs`. |
| 11 | `--exclude-tests` | Filtert automatisch erkannte Testprojekte aus dem Audit | Testprojekte werden über `rules.json` `TestSentinel` & `ProjectOverrides` sauber gesteuert. | `scopeFilter` in `get_violations` | `SourceFileCatalog.cs`. |
| 12 | `--tests-only` | Analysiert ausschließlich Testprojekte | Überflüssiger Shortcut; widerspricht dem Voll-Audit-Gedanken der CLI. | `scopeFilter` in `get_violations` | `SourceFileCatalog.cs`. |
| 13 | `--public-only` | Blendet private Member in Map-Skeletten aus | Verwaister Überrest des bereits entfernten `--map skeleton`-Befehls (aus Roadmap 2026-08-11). MCP `get_file_skeleton` nutzt dies nicht. | N/A | `SkeletonMapBuilder.cs`, `SkeletonSyntaxWalker.cs`. |
| 14 | `--ignore-suppressions` | Ignoriert Suppressions für bestimmte Sprachen (all, cs, razor, etc.) | In CI/Tests sind konfigurierte Suppressions gewollt. Schleift `IgnoreSuppressionsFilter` durch 7 Scanner-Klassen. | N/A | `IgnoreSuppressionsFilter.cs` (löschen), `SuppressionEvaluator.cs`, `WebFileSeparationChecker.cs`, etc. |

---

## 5. Architektur- & Qualitätsgewinn

### A. Code-Reduktion (Massiver Ballast-Abbau)
- **11 C#-Quelldateien restlos entfernbar:**
  1. `src/AiNetLinter/Commands/FootprintCommand.cs`
  2. `src/AiNetLinter/Commands/ImpactCommand.cs`
  3. `src/AiNetLinter/Commands/DebtReportCommand.cs`
  4. `src/AiNetLinter/Commands/PlaybookCheckCommand.cs`
  5. `src/AiNetLinter/Generators/RepoPlaybookGenerator.cs`
  6. `src/AiNetLinter/Generators/PlaybookSyntaxWalker.cs`
  7. `src/AiNetLinter/Generators/PlaybookTypes.cs`
  8. `src/AiNetLinter/Output/DebtReportBuilder.cs`
  9. `src/AiNetLinter/Scope/GitChangedFilesResolver.cs`
  10. `src/AiNetLinter/Core/NamespaceFilter.cs`
  11. `src/AiNetLinter/Suppression/IgnoreSuppressionsFilter.cs`
- **7 Test-Dateien restlos entfernbar:**
  1. `src/AiNetLinter.FastTests/Core/PlaybookGeneratorRound2Tests.cs`
  2. `src/AiNetLinter.FastTests/Core/NamespaceFilterTests.cs`
  3. `src/AiNetLinter.FastTests/Maps/Skeleton/SkeletonMapFilterTests.cs`
  4. `src/AiNetLinter.FastTests/Output/DebtReportBuilderHeaderTests.cs`
  5. `src/AiNetLinter.FastTests/Suppression/IgnoreSuppressionsFilterTests.cs`
  6. `src/AiNetLinter.IntegrationTests/Core/PlaybookGeneratorRound2FileTests.cs`
  7. `src/AiNetLinter.IntegrationTests/Output/DebtReportBuilderTests.cs`

### B. Performance-Optimierung im AST-Walker
- In `LinterAnalyzer.cs` entfallen `IsNamespaceAllowed()`-Abfragen bei `VisitUsingDirective`, `VisitClassDeclaration` etc.
- In `SourceFileCatalog.cs` entfallen Glob-Pattern-Matching-Schleifen pro Projekt.

### C. Schärfung der CLI (`Program.cs` & `AuditCommand.cs`)
Die Dispatch-Logik in `Program.cs` schrumpft auf das Wesentliche zusammen:
- Standalone: `--docs`, `--list-rules`, `--describe-rule`, `--search-rules`
- Sync-Fast-Path: `--sync-agent-rules-only`
- Maintenance: `--add-disable-all`, `--remove-disable-all`
- Audit: Standard-Audit mit oder ohne `--baseline` / `--wave-ready` / `--fix` / `--verbose` / `--no-cache`
- MCP: `--mcp-server` (und Daemon-Host-Parameter)

---

## 6. Was bleibt in der CLI erhalten?

Nach der Bereinigung verfügt die CLI über ein konsistentes, hochfokussiertes Set an Flags:

1. **Audit & Engine:**
   - `--config`, `-c`: Pfad zur `rules.json`
   - `--path`, `-p`: Pfad zur Solution (.sln / .slnx) oder Verzeichnis
   - `--verbose`, `-v`: Detaillierte Protokollausgaben
   - `--no-cache`: Cache deaktivieren
   - `--cache-ttl`: Cache-Lebensdauer
   - `--fix`: Roslyn-basierter Auto-Fixer für einfache Verstöße
2. **Wellen- & Baseline-Migration:**
   - `--create-baseline`: Baseline-JSON mit Checksummen erzeugen
   - `--baseline`: Inkrementelle Prüfung gegen Baseline
   - `--only-changed`: Nur Verstöße in geänderten Dateien
   - `--add-disable-all`: Bulk-Suppression in betroffenen Dateien
   - `--remove-disable-all`: Bulk-Suppression entfernen
   - `--wave-ready`: Audit nur für Dateien ohne Disable-all
3. **Agent-Regeln & Dokumentation:**
   - `--sync-agent-rules`, `-sar`: Sync während Audit
   - `--sync-agent-rules-only`, `-saro`: Schneller Sync ohne Audit (in `AGENTS.md` verankert)
   - `--agent-rules-path`, `-arp`: Benutzerdefinierter Ausgabepfad
   - `--docs`, `-d`: Eingebettete Dokumentation
   - `--list-rules`, `--describe-rule`, `--search-rules`: Regel-Katalog
4. **MCP-Server & Daemon-Infrastruktur:**
   - `--mcp-server`, `--parent-pid`, `--mcp-project-ttl-minutes`, `--mcp-max-projects`, `--daemon-start`, `--mcp-daemon-idle-exit-minutes`

---

## 7. Phasenplan zur restlosen Umsetzung

1. **Phase 1: Code-Entfernung (Engine, Analyzer, Generators, Commands)**
   - Löschen der 11 obsoleten Quelldateien und 7 Testdateien.
   - Bereinigung von `LinterAnalyzer.cs`, `SourceFileCatalog.cs`, `SkeletonSyntaxWalker.cs`, `SkeletonMapBuilder.cs`.
   - Bereinigung von `SuppressionEvaluator.cs`, `SuppressionScanner.cs`, `DisableAllDetector.cs`, `WebFileSeparationChecker.cs`, `WebSuppressionDetector.cs`.
2. **Phase 2: CLI-Optionen & Binding-Bereinigung**
   - Entfernen der 14 Optionen aus `CliOptions.cs`, `CliOptionFactory.cs`, `CliCommandBuilder.cs`, `LinterArgs.cs`.
   - Verschlankung von `Program.cs`, `AuditCommand.cs`, `SyncAgentRulesCommand.cs`.
3. **Phase 3: Test- & Dokumentations-Aktualisierung**
   - Anpassung der verbleibenden Unit- & Integrations-Tests.
   - Bereinigung aller Erwähnungen der 14 Optionen in `Docs/configuration.md`, `Docs/agent-api.md`, `Docs/ROADMAP.md`, `README.md`.
   - Sync der Agent-Regeln: `dotnet run --project src/AiNetLinter -- --sync-agent-rules-only`.
4. **Phase 4: Gate-Verifikation & Zero-Findings-Audit**
   - Vollständiger ripgrep-Scan: 0 Findings für alle 14 Parameter.
   - `dotnet build` (Zero Warnings mit `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`).
   - `dotnet test src/AiNetLinter.FastTests --filter Category!=Stress`
   - `dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress`
