---
status: done
type: step-plan
task: markdown-builder
step: 005
epic: EPIC-02
step_type: single
planned_by: planer
planned_by_model: antigravity
planned_at: 2026-08-19
---

# Plan Step 005: EPIC-02 Welle 3 — MetricsLookupFormatter (Prio 9) + TD-001 + Sealed Test Class Fix

## Ziel

Abschluss von EPIC-02 und des gesamten Tasks `markdown-builder`:
1. Migration von `MetricsLookupFormatter.cs` (Prio 9) auf `MarkdownBuilder` und `MarkdownTableBuilder`.
2. Behebung von TD-001 in `ViolationMarkdownFormatter.cs` (Top-Level Heading via `MarkdownBuilder.Heading`).
3. Behebung der Linter-Violation `EnforceSealedClasses` in `MarkdownBuilderTests.cs` (Klasse als `sealed` deklarieren).
4. Verifikation der Byte-Stabilität und Ausführung aller Testsuites und MCP-Quality-Gates.

## Geplante Änderungen

- `src/AiNetLinter/Mcp/Tools/MetricsLookup/MetricsLookupFormatter.cs`:
  - `using AiNetLinter.Output;` hinzufügen.
  - `Format`, `FormatMethodDetails`, `FormatTypeDetails`, `FormatPropertyDetails` auf `MarkdownBuilder` umstellen.
  - Metrik-Tabelle mit `MarkdownTableBuilder` erstellen.
- `src/AiNetLinter/Output/ViolationMarkdownFormatter.cs`:
  - Zeile 40: Top-Level Heading über `new MarkdownBuilder().Heading(1, ...).AppendTo(output)` erzeugen (TD-001).
- `src/AiNetLinter.FastTests/Output/MarkdownBuilderTests.cs`:
  - `public sealed class MarkdownBuilderTests` (Linter-Fix).
  - Zusätzliche Test-Abdeckung für `Heading.AppendTo`.

## DoD

- `dotnet build`: 0 Fehler, 0 Warnungen
- `dotnet test src/AiNetLinter.FastTests --filter Category!=Stress`: grün
- `dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress`: grün
- `get_violations`: 0 Verstöße
- `safeguard`: Score 10.00 / PASS
