---
status: done
type: step-result
task: 04_repositoryweite-hybridsuche-und-kontextbudget
step: 001
epic: EPIC-01
step_type: single
coded_by: coder
coded_by_model: GPT-5 (Codex)
coded_by_model_knowledge_cutoff: nicht angegeben
coded_at: 2026-08-21
code_commit_hash: a166eb38
status_after: done
blocker_category: n/a
---

# Result Step 001: Strukturierte repositoryweite Suche mit Legacy-Kompatibilität und Kontextbudget

## Zusammenfassung

Der `search_pattern`-Pfad liefert nun deterministische strukturierte Treffer mit MatchRanges, Kontext, Scope-/Snapshot-Metadaten und expliziter Completeness. Treffer-, Datei-, Kontext- und Antwortbudgets sowie Encoding-, Binary-, Unreadable-, Cancellation- und Regex-Timeout-Zustände werden maschinenlesbar ausgewiesen. Legacy-Text und `GetFilesWithHits` bleiben kompatibel und verwenden weiterhin getrennte, unbudgetierte Legacy-Aufbereitung.

## Geänderte Dateien

- `src/AiNetLinter/Mcp/Tools/Analysis/SearchPatternScanner.cs` — zentraler repositoryweiter Scan mit Scope-, Filter-, Treffer- und Antwortbudgetlogik.
- `src/AiNetLinter/Mcp/Tools/Analysis/SearchPatternScannerRecords.cs` — interne Records für Optionen, Treffer, MatchRanges, Scope und Completeness.
- `src/AiNetLinter/Mcp/Tools/Analysis/SearchPatternScannerCompleteness.cs` — Aggregation und Truncation-Gründe des Scans.
- `src/AiNetLinter/Mcp/Tools/Analysis/SearchPatternLegacyFormatter.cs` — kompatible Legacy-Textdarstellung.
- `src/AiNetLinter/Mcp/Tools/Analysis/SearchPatternLegacyFileHitScanner.cs` — unbudgetierter Legacy-Dateitrefferpfad für `GetFilesWithHits`.
- `src/AiNetLinter/Mcp/Tools/Analysis/SearchPatternTool.cs` und `src/AiNetLinter/Mcp/AnalysisToolRegistrations.cs` — neue optionale Toolargumente und Structured-Content-Ausgabe.
- `src/AiNetLinter/Baseline/FileSystemExclusionHelpers.cs` — sichere Enumeration und generische Search-Ausschlüsse.
- `src/AiNetLinter.FastTests/Mcp/Tools/Analysis/SearchPatternScannerTests.cs` (neu) — Scanner-/Formatter-, Scope-, Budget-, Encoding- und Completeness-Tests.
- `src/AiNetLinter.IntegrationTests/Mcp/Tools/SearchPatternToolTests.cs` sowie MCP-Contract-, E2E- und Raw-Wire-Tests — additive Wire- und Legacy-Vertragsabdeckung.
- `tests/Fixtures/SymbolGraphMini/src/SymbolGraphMini/search-fixture.md` und `tests/Fixtures/SymbolGraphMini/src/SymbolGraphMini/wwwroot/search-fixture.json` — neutrale Such-Fixtures.

## Commit

- **Code-Commit-Hash:** `a166eb38`
- **Message:**
  ```
  feat: Suche strukturieren [04_repositoryweite-hybridsuche-und-kontextbudget]
  ```
- **Branch:** `main`
- **Push:** nein (lokal)
- **Doku-Commit:** separater, zweiter Commit folgt.

## Build-/Test-Output

- `dotnet build` → grün, 0 Warnungen, 0 Fehler.
- `dotnet test src/AiNetLinter.FastTests --filter Category=Unit` → grün, 1073/1073.
- `dotnet test src/AiNetLinter.IntegrationTests --filter FullyQualifiedName~SearchPatternToolTests` → grün, 17/17.
- `dotnet test src/AiNetLinter.IntegrationTests --filter FullyQualifiedName~McpServerCommandContractTests` → grün, 14/14.
- `dotnet test src/AiNetLinter.IntegrationTests --filter FullyQualifiedName~McpServerAllToolsE2ETests` → grün, 29/29.
- `dotnet test src/AiNetLinter.IntegrationTests --filter FullyQualifiedName~McpServerCommandJsonRpcFramingTests` → grün, 7/7.
- `dotnet test src/AiNetLinter.FastTests --filter Category!=Stress` → grün, 1553/1553.
- `dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress` → grün, 336/336.

## Abweichungen vom Plan

Keine fachliche Abweichung. Für die im Vollauf sichtbar gewordene Linter-Regel `EnforceNoSilentCatch` wurden die bewusst abgefangenen Enumeration-Exceptions im Step-001-Helper explizit verworfen; das ändert die Scan-Semantik nicht.

## Beobachtungen

Der erste vollständige Integrationstestlauf meldete zusätzlich temporär die gewachsene, git-ignorierte `temp`-Ablage als `MaxDirectoryChildren`-Violation. Nach Abschluss der Testprozesse war der erneute vollständige Lauf mit 336/336 Tests grün; es bleibt keine Step-001-Testabweichung offen.

## Bekannte Unschärfen

Keine offenen Unschärfen innerhalb dieses Steps. C#-Roslyn-Enrichment, öffentliche Dokumentationsangleichung und optionale Performance-/`rg`-Messung bleiben wie geplant in späteren Epics.
