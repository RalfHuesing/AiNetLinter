---
task: magic-values-in-mcp
type: codemap
maintained_by: planer, coder, kritiker
last_updated: 2026-08-14T22:15:00+02:00
---

# CodeMap: magic-values-in-mcp

Task-scoped Landkarte — existiert nur für diesen Task, wird mit
`<task-dir>` gelöscht, kein projektweites Artefakt. Enthält **nur**, was
für diesen Task relevant ist — kein Anspruch auf vollständige Projektabdeckung.

**Pointer-Prinzip:** Jeder Eintrag ist Ort + **ein Satz**, was dort ist
und wozu es für diesen Task relevant ist. Verhaltensbehauptungen veralten,
Ortsangaben kaum. Wer mehr wissen muss, liest die Datei selbst nach.

## Karte

- **`src/AiNetLinter/Mcp/AnalysisToolRegistrations.cs`** — zentrale Registrierungs-Stelle für analyse-orientierte MCP-Tools (`get_violations`, `safeguard`, `search_pattern`, `metrics_tree`, `pattern_detect`); der neue `find_magic_values`-Eintrag wird hier mit `McpServerTool.Create(...)` analog zu `AddPatternDetect` ergänzt, inkl. `McpCallLog`-Wrapper.

- **`src/AiNetLinter/Mcp/McpTruncation.cs`** — wiederverwendbarer Trunkierungs-Helper (`TruncateLines` für Trefferlisten, `TruncateFileList` für Datei-Listen-Fallback); für `find_magic_values` ist `TruncateLines` mit Meta-Zeile `[N Treffer gesamt, M gezeigt — Pattern verfeinern oder maxResults erhöhen]` der Standard-Pfad.

- **`src/AiNetLinter/Mcp/McpToolResults.cs`** — `CallToolResult`-Bau-Helper; relevant für `Text(string)` (kein StructuredContent) und `Text<T>(string, payload)` (additives `StructuredContent`); explizit dokumentiert: `payload` MUSS zu JSON-Objekt serialisieren, kein Top-Level-Array — direkter Bezug für `{ MagicValues = list }`-Wrapper.

- **`src/AiNetLinter/Mcp/Tools/PatternDetect/PatternCatalog.cs`** — `PatternDefinition`-Katalog für `pattern_detect`; Klassen-Doc-Kommentar nennt „magic-numbers" als Pattern ohne existierende Erkennung — dieser Hinweis muss nach Fertigstellung von `find_magic_values` so angepasst werden, dass die Lücke nicht mehr als offen ausgewiesen wird (Konzept §„Wo im Projekt", §„DoD" Punkt 12).

- **`src/AiNetLinter/Mcp/Tools/SymbolGraph/GetImpactTool.cs`** — Vorlage für `changedOnly`-Logik via Git-Diff; bestehender `gitRef: string?`-Parameter wird semantisch durch `changedOnly: bool` ersetzt, der Mechanismus (leerer `gitRef` = uncommittete Änderungen) bleibt 1:1; siehe `ExecuteGitRefBranchAsync` + Aufruf von `DiffImpactAnalyzer.AnalyzeEntriesAsync`.

- **`src/AiNetLinter/Mcp/Tools/Analysis/GetViolationsTool.cs`** + **`GetViolationsScanner.cs`** — Vorlage für Scanner-Pattern (`XxxTool` als dünner Dispatch ohne eigene Logik, `XxxScanner` mit `XxxScannerParameters`-Record und `XxxResult`-Record) sowie für `McpTruncation`-Nutzung; speziell `McpTruncation.TruncateLines` in `FormatReport` ist 1:1 das Muster für `find_magic_values`.

- **`src/AiNetLinter/Mcp/Tools/PatternDetect/PatternDetectTool.cs`** — Vorlage für `StructuredContent` mit verschachteltem Objekt statt nacktem Array (`JsonSerializer.SerializeToElement(result.Payload, McpJsonOptions.Default)`); Vorlage auch für `ResolvePatterns`-Pattern-Validierung mit `INVALID_ARGUMENT`-Recoverable-Error bei unbekannten Werten (passt 1:1 auf `categoryFilter`-Validierung).

- **`src/AiNetLinter/Mcp/Tools/Analysis/SearchPatternTool.cs`** + **`SearchPatternScanner.cs`** — Vorlage für reine On-Demand-Audit-Tools mit `Task.Run`-Wrapper um den CPU-/IO-bound Scan (Lock-Freiheit auf `McpCodeGraphServer`); außerdem Muster für Pflicht-Parameter-Validierung (leeres Pattern → `INVALID_ARGUMENT`-Recoverable).

- **`src/AiNetLinter/Suppression/SuppressionScanner.cs`** + **`SuppressionEvaluator.cs`** — **NICHT** wiederverwendet für `find_magic_values`: bestehende Semantik unterdrückt eine Regel für die **gesamte Datei**, sobald irgendwo ein `// ainetlinter-disable`-Kommentar steht; für Magic-Value-Funde (dutzende pro Datei) wäre das nutzlos — Konzept-Vorgabe ist knotenbasierte Auswertung via `SyntaxTrivia` am jeweiligen `LiteralExpressionSyntax` (Leading + Trailing). Nur als Referenz, wie `// ainetlinter-disable <Rule>`-Parsing funktioniert (siehe `SuppressionCommentParser`).

- **`src/AiNetLinter/Maps/HotspotMapBuilder.cs`** + **`src/AiNetLinter/Mcp/Tools/FileStructure/GetHotspotsScanner.cs`** — **Bestands-Fund** für die Heuristik „duplizierte `private const`-Felder": `private const double WarnThreshold = 0.80;` ist identisch in `HotspotMapBuilder.cs:23` und `GetHotspotsScanner.cs:27` definiert, inkl. identischem Interpolations-String `">80% des Limits"`; `find_magic_values` muss genau dieses Muster als `constant_candidates`-Fall melden und Hochstufung in eine gemeinsame Konstanten-Klasse empfehlen.

- **`src/AiNetLinter/Core/DiffImpactAnalyzer.cs`** — Git-Diff-Mechanik für `changedOnly`: `RunGitDiff(repoRoot, gitSinceRef)` mit `gitSinceRef = null/""` = uncommittete Änderungen, `ParseGitDiffHunks` liefert `Dictionary<string, List<int>>` (Datei → geänderte Zeilen); `find_magic_values` braucht nur die Datei-Set-Semantik (welche Dateien geändert), nicht die Symbol-Resolution, also eine schlanke Eigen-Variante — `DiffImpactAnalyzer.ParseGitDiffHunks` darf wiederverwendet werden, der Rest ist Overhead.

- **`src/AiNetLinter.IntegrationTests/Mcp/Tools/SearchPatternToolTests.cs`** — Vorlage für Integration-Tests des neuen MCP-Tools: Muster `SOLUTION_NOT_LOADED`-Test, ReadOnly-Fixture-Test, `SymbolGraphCatalogFixture`-Injektion, Compile-Error-Warnhinweis-Test; Pfad-Name für neuen Test: `FindMagicValuesToolTests.cs` in `src/AiNetLinter.IntegrationTests/Mcp/Tools/` mit `[Trait("Category", "Integration")]`.

- **`src/AiNetLinter.IntegrationTests/Mcp/Tools/SymbolGraphCatalogFixture.cs`** + **`src/AiNetLinter.FastTests/Fixtures/McpInMemoryTestContext.cs`** — Test-Fixtures für die zwei Test-Layer; FastTests-Pendant für reine Scanner-Unit-Tests ist `McpInMemoryTestContext` (Roslyn-In-Memory), IntegrationTests-Fixture ist `SymbolGraphCatalogFixture` (ReadOnly-Lösung mit Greeter.cs et al.).

- **`src/AiNetLinter.FastTests/Mcp/Tools/PatternDetectScannerTests.cs`** + **`PatternDetectToolTests.cs`** — FastTests-Vorlage für reine Scanner-/Tool-Unit-Tests im Komponenten-Test-Layer; passt 1:1 auf `FindMagicValuesScannerTests.cs` mit `[Trait("Category", "Component")]` für Scanner-Tests und `[Trait("Category", "Unit")]` für reine Helper-Tests (z. B. Klassifizierungs-Tabellen, Heuristik-Reinheit).

- **`src/AiNetLinter/Mcp/Tools/MagicValues/MagicValuesCategories.cs`** — `MagicValueCategory`-Enum mit 7 Werten (ConfigCandidates/ConstantCandidates/EnumCandidates/NameofCandidates/LocalizationCandidates/StandardCandidates/SecurityCandidates) + `ToStringValue()`-Helper (snake_case-Stable-Strings für JSON-RPC und `categoryFilter`-Validierung); neu in step-001.

- **`src/AiNetLinter/Mcp/Tools/MagicValues/MagicValuesClassifier.cs`** — `MagicValuesClassifier.Classify()`-Hauptmethode (Trivial-/Attribut-/Index-/Loop-/GetHashCode-Filter + String-Heuristiken: Connection-String, URL, Windows-Pfad, Format-String, Header-Identifier); plus `MagicValueClassification`/`MagicValueClassifierOptions` Records; neu in step-001.

- **`src/AiNetLinter/Mcp/Tools/MagicValues/MagicValuesNumberClassifier.cs`** — Number-spezifische Sub-Heuristiken (`ClassifyNumber` für HTTP-Statuscode/Timeout-Parameter/Schwellenwert-Konstante, plus `TryResolveParameterName` via `SemanticModel`); aus `MagicValuesClassifier` extrahiert, damit die Hauptklasse unter `MaxLineCount: 500` bleibt; neu in step-001.

- **`src/AiNetLinter/Mcp/Tools/MagicValues/FindMagicValuesScanner.cs`** — `FindMagicValuesScanner.ScanAsync` (Top-Level-Loop, Aggregation, Trunkierung via `McpTruncation`, `StructuredContent`-Payload-Bau) + `MagicValueSyntaxWalker : CSharpSyntaxWalker` (nimmt `MagicValueWalkerContext` als Parameter-Record, nicht 7 einzelne Ctor-Args); plus die Result-/Payload-/Entry-/Summary-Records; neu in step-001, `VisitInterpolatedStringExpression` für statische `InterpolatedStringText`-Segmente in `$"..."` in step-002 nachgezogen (Konzept §"Muss-Haben" Beispiel 2: in-string magic values).

- **`src/AiNetLinter/Mcp/Tools/MagicValues/FindMagicValuesTool.cs`** — Dispatcher-Implementierung des 19. MCP-Tools `find_magic_values` (Loading/NotLoaded/Validation/Malfunction-Pfade + `Task.Run`-Wrapper um den Scanner); nutzt `FindMagicValuesScanner`, `MagicValuesClassifier`, `McpTruncation` und `McpToolResults.Text<T>` (Objekt-Wrapper statt Top-Level-Array); neu in step-001.

- **`src/AiNetLinter.FastTests/Mcp/Tools/FindMagicValuesScannerTests.cs`** — Filter/Aggregation-Pipeline-Tests (Trivial/Index/Attribut/GetHashCode/ignoreNumbers, minOccurrences, valueType/categoryFilter/scopeFilter, maxResults, StructuredContent-Shape, EPIC-2-Platzhalter); neu in step-001.

- **`src/AiNetLinter.FastTests/Mcp/Tools/FindMagicValuesScannerHeuristicTests.cs`** — Heuristik-Detail-Tests (URL/Pfad/Format-String/HTTP-Statuscode/Schwellenwert/Connection-String); aus der Haupt-Test-Klasse extrahiert, um `MaxLineCount: 500` einzuhalten; neu in step-001.

- **`src/AiNetLinter.FastTests/Mcp/Tools/FindMagicValuesScannerMalfunctionTests.cs`** — `FaultingSolutionFixture`-Malfunction-Test (IsMalfunction=true + Context); aus der Haupt-Test-Klasse extrahiert; neu in step-001.

- **`src/AiNetLinter.FastTests/Mcp/Tools/FindMagicValuesTestHelpers.cs`** — Geteilte `RunAsync`-Helpers (3 Overloads) + `ScanAsyncParams`-Parameter-Object; `ainetlinter-disable MaxMethodParameterCount` auf den 7-Param-Overloads (Aufrufstellen-Komfort vs. AI-ContextFootprint); neu in step-001.

- **`src/AiNetLinter.IntegrationTests/Mcp/Tools/FindMagicValuesToolTests.cs`** — Integration-Tests gegen `SymbolGraphCatalogFixture` (SOLUTION_NOT_LOADED/Loading, Parameter-Validierung mit `INVALID_ARGUMENT`, Clamping, `StructuredContent`-Shape, Tool-Registrierung in `tools/list`); neu in step-001.

- **`Docs/agent-api.md`** — Tool-Tabelle Abschnitt „Die 18 Tools" (jetzt „Die 19 Tools", Tabellenzeilen-Eintrag für `find_magic_values`); Structured-Output-Abschnitt um einen `find_magic_values`-Eintrag mit JSON-Schema + Suppression-Sonderfall-Hinweis (pro-Fundstelle statt dateiweit) als explizite „bewusste Ausnahme"-Notiz ergänzt, weil das von der sonst projektweiten Suppression-Semantik abweicht. **Status 2026-08-14: aktualisiert (18→19, Tabellen-Eintrag, JSON-Schema, Sonderfall-Hinweis).**

- **`Docs/ROADMAP.md`** — Eintrag in Epic 19 „AI-Developer Experience (AI-DX) & Tooling" hinzugefügt: `find_magic_values` MCP-Tool (On-Demand-Magic-Value-Audit), Verweis auf `tasks/magic-values-in-mcp/konzept.md` und `step-001/step-plan.md`, Datum 2026-08-14. **Status 2026-08-14: aktualisiert.**

- **`src/AiNetLinter/Suppression/SuppressionCommentParser.cs`** — Parser für `// ainetlinter-disable <Rule>`-Kommentare; relevant nur als Referenz für die exakte Syntax-Erkennung, weil `find_magic_values` Magic-Value-Suppression direkt am `SyntaxTrivia` auswertet statt diesen Parser aufzurufen (Performance-Grund, Konzept §„Wie" Punkt 3).

- **`src/AiNetLinter/Mcp/IsErrorPolicy.md`** + **`src/AiNetLinter/Mcp/McpToolResults.cs` (Methoden `Recoverable`/`Error`/`SolutionNotLoaded`/`Loading`)** — verbindliche `IsError`-Semantik: Pflicht-Argumente / unbekannte Enum-Werte → `InvalidArgument` (recoverable, IsError=false); Server lädt noch → `Loading`; keine Solution → `SolutionNotLoaded` (IsError=true). `find_magic_values` hält sich strikt an diese Tabelle. **Status 2026-08-14: Audit-Tabelle ergänzt um `find_magic_values`-Zeile (18→19 Tools-Überschrift, Eintrag mit `isError=true` für `SOLUTION_NOT_LOADED`+`ANALYSIS_FAILED`, `isError=false` für `INVALID_ARGUMENT`+leere Treffermenge).**

- **`src/AiNetLinter/Mcp/IsErrorPolicy.md`** *(redundant zu oben, bewusst zweimal — getrennt von „echter Malfunction"-Regel)* — defensive `try/catch` um den Scan liefert `CompilationError` (= `WORKSPACE_DIAGNOSTIC`, IsError=true, Retry-once-Hinweis) bei unerwarteten Roslyn-/Laufzeit-Fehlern, nicht `INVALID_ARGUMENT` — siehe Policy-Tabelle.
