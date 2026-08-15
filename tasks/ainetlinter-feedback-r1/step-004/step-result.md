---
status: completed
type: step-result
task: ainetlinter-feedback-r1
step: "004"
title: "Teil B: Code-Snippet in get_violations direkt mitgeben"
epic: EPIC-04
coded_by: coder
coded_by_model: gemini-3.7-flash
coded_by_model_knowledge_cutoff: 2026-01
coded_at: 2026-08-15T19:26:00+02:00
related_to:
  - tasks/ainetlinter-feedback-r1/step-004/step-plan.md
---

# Step 004: Teil B — Code-Snippet in get_violations direkt mitgeben — Ergebnis

## Was wurde geändert

1. **`RuleViolation` Model:**
   - Eigenschaft `public string? Snippet { get; init; }` ergänzt.
2. **`GetViolationsScanner`:**
   - `GetViolationsScannerParameters` um `int ContextLines = 0` und `bool IncludeSnippet = false` erweitert.
   - Methode `ExtractSnippetAsync` hinzugefügt: extrahiert Quellcode-Zeilen `[Line - contextLines, Line + contextLines]` (geklemmt auf max. 15 Zeilen und 0..5 Context-Zeilen).
   - `BuildViolationsTextAsync` reichert `RuleViolation` mit dem extrahierten Snippet an, wenn `includeSnippet: true`.
   - `AppendSection` rendert C#-Codeblöcke unter Tabellenzeilen, falls Snippets vorhanden sind.
3. **`ViolationMarkdownFormatter`:**
   - In `AppendFileGroup` Codeblock-Rendering für vorhandene `Snippet`-Daten integriert.
4. **`GetViolationsTool` & `AnalysisToolRegistrations`:**
   - `GetViolationsTool.ExecuteAsync` um Überladung und Parameter `contextLines` und `includeSnippet` erweitert.
   - MCP-Tool-Registrierung um optionale Parameter `contextLines` (Default 0) und `includeSnippet` (Default false) sowie aktualisierte Beschreibung erweitert.
5. **Tests:**
   - `GetViolationsToolTests.cs` erweitert um Tests für `includeSnippet: true`, `contextLines: 2` und `includeSnippet: false`.
   - `ViolationMarkdownFormatterTests.cs` erweitert um Test für Snippet-Rendering.

## Verifikation

- `dotnet test src/AiNetLinter.FastTests --filter FullyQualifiedName~GetViolationsToolTests`: 16/16 bestanden.
- `dotnet test src/AiNetLinter.FastTests --filter FullyQualifiedName~ViolationMarkdownFormatterTests`: 31/31 bestanden.
- `dotnet test src/AiNetLinter.FastTests --filter Category!=Stress`: 1335/1335 bestanden.
