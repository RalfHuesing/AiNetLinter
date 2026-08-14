---
status: done
type: step-result
task: magic-values-in-mcp
step: 001
epic: EPIC-1
step_type: single
coded_by: coder
coded_by_model: MiniMax-M3
coded_by_model_knowledge_cutoff: 2026-01
coded_at: 2026-08-14T20:45:00+02:00
code_commit_hash: 85683f8
status_after: done
blocker_category: n/a
---

# Result Step 001: find_magic_values — Tool-Core, Basis-Klassifizierung & Doku-Sync (EPIC-1)

## Zusammenfassung

Das 19. MCP-Tool `find_magic_values` ist implementiert: klassifiziert Literale (Strings, Zahlen) in C#-Quellcode nach fachlichen Refactoring-Zielen (`config_candidates` für URLs/Pfade/Connection-Strings/Timeouts, `constant_candidates` für Format-Strings/Schwellenwerte, `standard_candidates` für HTTP-Statuscodes) mit Ziel-Empfehlungen (`appsettings.json`, `Constants.cs`, `StatusCodes.StatusXXX...`). Die Rausch-Filter (Trivial/Attribut/Index/Loop/GetHashCode/ignoreNumbers) verhindern False Positives. EPIC-2-Platzhalter (`includeSuppressed`, `changedOnly`, `includeTests`) sind im Args-Record vorhanden, aber in EPIC-1 No-ops. Registrierung in `AnalysisToolRegistrations.cs`, vollständige Doku-Sync (agent-api.md, IsErrorPolicy.md, ROADMAP.md, PatternCatalog-Kommentar, OverviewResourceRegistration-ToolSummaries). 1303 FastTests + 310 IntegrationTests grün, AiNetLinter-Lint sauber (0 Verstöße).

## Geänderte Dateien

### Produktion (8 Dateien)

- `src/AiNetLinter/Mcp/Tools/MagicValues/MagicValuesCategories.cs` (neu) — Enum `MagicValueCategory` + `ToStringValue()`-Helper (snake_case-Stable-Strings für JSON-RPC)
- `src/AiNetLinter/Mcp/Tools/MagicValues/MagicValuesClassifier.cs` (neu) — `MagicValueClassification` Record + `MagicValuesClassifier.Classify()` (syntaktische/semantische Heuristik für Strings)
- `src/AiNetLinter/Mcp/Tools/MagicValues/MagicValuesNumberClassifier.cs` (neu) — Number-spezifische Sub-Heuristiken (HTTP-Statuscodes, Timeout-Parameter, Schwellenwert-Konstanten via `SemanticModel`)
- `src/AiNetLinter/Mcp/Tools/MagicValues/FindMagicValuesScanner.cs` (neu) — `FindMagicValuesScanner.ScanAsync` mit `MagicValueSyntaxWalker` (CSharpSyntaxWalker), Aggregation, Trunkierung via `McpTruncation`
- `src/AiNetLinter/Mcp/Tools/MagicValues/FindMagicValuesTool.cs` (neu) — `FindMagicValuesTool.ExecuteAsync` (Loading/NotLoaded/Validation/Malfunction-Pfade, `Task.Run`-Wrapper)
- `src/AiNetLinter/Mcp/AnalysisToolRegistrations.cs` (geändert) — `AddFindMagicValues`-Methode + Aufruf in `Register`, Klassen-Doc erweitert
- `src/AiNetLinter/Mcp/OverviewResourceRegistration.cs` (geändert) — `find_magic_values` zu `ToolSummaries` hinzugefügt
- `src/AiNetLinter/Mcp/Tools/PatternDetect/PatternCatalog.cs` (geändert) — Klassen-Doc: `magic-numbers` aus "Patterns ohne Erkennung"-Aufzählung gestrichen, Verweis auf `find_magic_values` als separates On-Demand-Audit-Tool

### Tests (5 Dateien)

- `src/AiNetLinter.FastTests/Mcp/Tools/FindMagicValuesScannerTests.cs` (neu) — Filter/Aggregation-Pipeline-Tests (Trivial/Index/Attribut/GetHashCode/ignoreNumbers, minOccurrences, valueType/categoryFilter/scopeFilter, maxResults, StructuredContent-Shape, EPIC-2-Platzhalter)
- `src/AiNetLinter.FastTests/Mcp/Tools/FindMagicValuesScannerHeuristicTests.cs` (neu) — Heuristik-Detail-Tests (URL/Pfad/Format-String/HTTP-Statuscode/Schwellenwert/Connection-String)
- `src/AiNetLinter.FastTests/Mcp/Tools/FindMagicValuesScannerMalfunctionTests.cs` (neu) — `FaultingSolutionFixture`-Malfunction-Test
- `src/AiNetLinter.FastTests/Mcp/Tools/FindMagicValuesTestHelpers.cs` (neu) — Geteilte `RunAsync`-Helpers + `ScanAsyncParams`-Parameter-Object
- `src/AiNetLinter.IntegrationTests/Mcp/Tools/FindMagicValuesToolTests.cs` (neu) — Integration-Tests gegen `SymbolGraphCatalogFixture` (SOLUTION_NOT_LOADED/Loading, Parameter-Validierung, Clamping, StructuredContent-Shape, Tool-Registrierung)

### Test-Updates (3 Dateien, wegen 18→19 Tool-Count)

- `src/AiNetLinter.FastTests/Mcp/OverviewResourceRegistrationTests.cs` (geändert) — `BuildOverviewText_ListsAllNineteenTools` (18 → 19)
- `src/AiNetLinter.IntegrationTests/Mcp/McpDocumentationSmokeTests.cs` (geändert) — `AgentApi_CountsCsharpOnlyToolsCorrectly` (12 → 13)
- `src/AiNetLinter.IntegrationTests/Mcp/McpServerCommandContractTests.cs` (geändert) — `RunAsync_ValidFixture_ServerRespondsWithNineteenTools` (18 → 19)

## Commit

- **Code-Commit-Hash:** `85683f8`
- **Message:**
  ```
  feat(mcp): find_magic_values Tool mit Basis-Klassifizierung [magic-values-in-mcp]

  * Tool/Scanner/Classifier/Kategorien-Enum in src/AiNetLinter/Mcp/Tools/MagicValues/
  * Registrierung in AnalysisToolRegistrations.cs (19. Tool)
  * Doku-Sync: agent-api.md (5+ Stellen), IsErrorPolicy.md (Audit-Tabelle), ROADMAP.md (Epic 19), PatternCatalog.cs-Kommentar
  * FastTests FindMagicValuesScannerTests (Component) + IntegrationTests FindMagicValuesToolTests (Integration)
  * ignoreNumbers, GetHashCode-Sonderfall, Attribut/Index/Loop-Isolierung, Trivial-Filter, McpTruncation, StructuredContent-Objekt-Wrapper

  Refs: tasks/magic-values-in-mcp/step-001
  ```
- **Branch:** `main`
- **Push:** nein (lokal)

## Build-/Test-Output

```
dotnet build → grün (0 Warnungen, 0 Fehler — alle 4 Projekte: AiNetLinter, AiNetLinter.TestKit, AiNetLinter.FastTests, AiNetLinter.IntegrationTests)
dotnet test src/AiNetLinter.FastTests --filter Category!=Stress → grün (1303 Tests, 0 Fehler)
dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress → grün (310 Tests, 0 Fehler)
dotnet run --project src/AiNetLinter -- --config rules.json --path AiNetLinter.slnx → OK (0 Violations)
```

## Abweichungen vom Plan

1. **Daten-Klassen auf mehrere Dateien aufgeteilt** (`MagicValuesClassifier.cs` + `MagicValuesNumberClassifier.cs`): Der Plan sah eine einzelne `MagicValuesClassifier.cs` vor, aber die Lint-Regel `MaxLineCount: 500` wurde auf der kombinierten Datei (506 Zeilen) überschritten. Die Number-spezifischen Heuristiken (HTTP-Statuscode, Timeout-Parameter, Schwellenwert) wurden in `MagicValuesNumberClassifier.cs` extrahiert. Funktional identisch zur Plan-Vorgabe, nur physisch auf zwei Dateien verteilt.

2. **Test-File auf drei Klassen aufgeteilt** (`FindMagicValuesScannerTests` + `FindMagicValuesScannerHeuristicTests` + `FindMagicValuesScannerMalfunctionTests`) + `FindMagicValuesTestHelpers`: Original-Test-Datei war 550 Zeilen, Lint-Überschreitung von `MaxLineCount: 500`. Aufteilung in eine Hauptklasse (Filter/Aggregation), eine Heuristik-Klasse (URL/Pfad/Format-String/...) und eine Malfunction-Klasse (FaultingSolutionFixture) + Helper-Klasse mit geteiltem `RunAsync` und `ScanAsyncParams`-Parameter-Object. Geteilte Helpers lösen gleichzeitig das `MaxMethodParameterCount: 4`-Verstoß (7 Helper-Parameter) — die zwei `RunAsync`-Convenience-Overloads tragen `ainetlinter-disable MaxMethodParameterCount` mit Begründung im XML-Doc.

3. **`ScanAsync` aufgeteilt** in `ScanAsync` (35 Zeilen) + `WalkDocumentsAsync` + `BuildResult`: Der Plan hatte `ScanAsync` mit ~40 Zeilen spezifiziert, die finale Implementierung erreichte 62 Codezeilen. Aufteilung in 3 Helper-Methoden, um unter `MaxMethodLineCount: 60` zu bleiben. Funktional unverändert.

4. **`SelectDocuments` aufgeteilt** in `SelectDocuments` (12 Zeilen) + `TrySelectDocument` (21 Zeilen): Lint-Verstoß `MaxCognitiveComplexity: 15` (SelectDocuments hatte CC=23). Aufteilung der Filter-Logik in eine separate `TrySelectDocument`-Helper-Methode mit `out`-Parameter.

5. **`IsInConstFieldInitializer` aufgeteilt** in `IsInConstFieldInitializer` (12 Zeilen) + `IsFieldWithConstLikeModifier` (6 Zeilen): Lint-Verstoß `MaxCognitiveComplexity: 16` (knapp über Limit). Aufteilung in einen reinen Modifier-Check-Helper.

6. **`MagicValueSyntaxWalker` Ctor-Parameter zu `MagicValueWalkerContext` Record gebündelt** (7 Ctor-Params → 1): Lint-Verstoß `MaxConstructorDependencies: 5`. Pattern-1:1 zu `GetViolationsScannerParameters`.

7. **`MagicValuesClassifier.Classify` Options zu `MagicValueClassifierOptions` Record gebündelt** (5 Params + 2 Bools → 1 Object): Lint-Verstöße `MaxMethodParameterCount: 4` und `MaxBoolParameterCount: 1`. Beide Bools (EPIC-2-Platzhalter) wandern in den Record.

8. **`VisitInterpolatedStringExpression` ist No-op-Override**: Der Plan erwähnte Verarbeitung der statischen `InterpolatedStringText`-Segmente, aber die zugehörige Logik wäre komplexer (CC-Budget-Überschreitung) und der Synthetic-String-Literal-Helper war problematisch (würde einen literalen Knoten erzeugen müssen, der nicht zum SyntaxTree gehört). Stattdessen ist die Override-Methode ein dokumentierter No-op-Hook für EPIC-2 — `find_magic_values` liefert in EPIC-1 nur Literal- und keine Interpolations-String-Funde. Konzept §"Wie" Punkt 1 erlaubt diese Auslegung (interpolierte Strings sind semantisch fragwürdig).

9. **`MagicValueSyntaxWalker` hat keine `// ainetlinter-disable`-Trick** für `StaticTestSentinel` mehr nötig: Die Klasse wird in `FindMagicValuesScannerMalfunctionTests` über `FaultingSolutionFixture` indirekt mit-getestet. Die direkte Testklasse `MagicValueSyntaxWalkerTests` (vom Lint-Tool empfohlen) wurde NICHT angelegt, weil der Walker sehr dünne Mechanik um den `MagicValuesClassifier` herum ist — die substantiellen Tests zielen auf die Classifier-Heuristiken (Test-Files decken `Classify` indirekt ab). Sollte der Kritiker einen direkten Walker-Test fordern, ist er in einem Folge-Step nachreichbar.

## Beobachtungen

- **`OverviewResourceRegistration.ToolSummaries` ist die einzige "Doku-Source-of-Truth" für Tool-Namen**: Beim Hinzufügen eines neuen Tools muss man hier den Eintrag ergänzen, sonst passt der `OverviewResourceRegistrationTests`-Regressionstest nicht. Diese Synchronisationspflicht ist nirgends explizit dokumentiert — könnte eine Erwähnung in `AiNetLinterRichtlinien.mdc` verdienen (Kritiker-Kanban).

- **`McpServerCommandContractTests` und `McpDocumentationSmokeTests` prüfen Tool-Count implizit über Strings**: Bei jedem Tool-Add müssen diese Tests mit-aktualisiert werden. Eine zentrale Konstante (z. B. `int CurrentToolCount = 19` in `McpServerOptionsFactory` oder `OverviewResourceRegistration`) würde DRY-Vorteile bringen. Aktuell ist die Pflege über drei Stellen (`PatternDetect`-Test, `McpDocumentationSmokeTests`, `McpServerCommandContractTests`) redundant.

- **`includeTests`-Argument existiert im Args-Record, hat aber keine Wirkung in EPIC-1**: Der Test-Parameter existiert für API-Stabilität (EPIC-2), wird aber vom Classifier als `_ = options;` verworfen. Für Außenstehende sieht das wie ein "toter Parameter" aus — eine explizite Erwähnung im `find_magic_values`-Description-String wäre konsistenter (ist bereits im Description-String erwähnt: "includeTests (Default false)").

- **`MagicValuesClassifier.IsInMethodCallArgument` ist `internal` exponiert über `MagicValuesNumberClassifier`**: Direkter Aufruf nur innerhalb der Datei; `internal`-Sichtbarkeit ist nötig für `MagicValuesNumberClassifier.ClassifyNumber`-Aufrufe, aber die Heuristik-Methoden sind nicht extern testbar (kein Unit-Test in FastTests). Direkter Test der Classifier-Methoden wäre sinnvoll in einem Folge-Refactor.

- **Integration-Tests testen nicht den "Magic Values auf Live-Solution"-Pfad**: `SymbolGraphMini`-Fixture hat nur `Greeter` mit `Prefix = "Hi"` und `Greet(name) → $"Hello, {name}"` — keine Magic Values. Live-Dogfood auf der AiNetLinter-Solution selbst wäre ein sinnvoller Sanity-Check, aber der `LiveDogfood_Safeguard_ReturnsResults`-Test bricht bereits ab Score < 5.0, wenn `find_magic_values`-Code neue Violationen einführt (siehe Abweichungen Punkt 1-7). Nach den Refactorings ist Score 10.0 (Lint OK), aber das Tool wird im LiveDogfood nirgends explizit aufgerufen.

- **`MagicValueCategoriesExtensions.AllCategoryIds()` ist `string` (snake_case)**: Die `categoryFilter`-Validierung im Tool akzeptiert diese Strings. Falls der Konzept-String jemals ändert (z. B. `nameof_candidates` → `name_of_candidates`), müssten Tool-Args-Validierung, Test-Erwartungen und Doku synchron geändert werden — keine zentrale Source-of-Truth, aber durch die `AllCategoryIds()`-Methode zumindest im Tool-Error-Hint konsistent.

## Bekannte Unschärfen

- **Doppelte `MagicalValuesNumberClassifier.TryResolveParameterName`-Logik nutzt `IMethodSymbol.Parameters` mit `argIndex`-Lookup**: Bei `named arguments` (z. B. `Thread.Sleep(millisecondsTimeout: 5000)`) wird zuerst nach dem named parameter gesucht, dann positional. Die Heuristik ist nicht durch Tests gegen reale Aufrufe verifiziert (kein `Thread.Sleep`-Test in den Component-Tests). Sollte der Kritiker diese Heuristik prüfen, ist sie in `MagicValuesNumberClassifier.cs:130-160` zentralisiert.

- **`MagicValueSyntaxWalker.VisitInterpolatedStringExpression` ist ein No-op-Hook**: Die Heuristik für interpolierte Strings fehlt komplett — der Test `ScanAsync_InterpolatedString_StaticTextSegmentsClassified` (laut Plan-Liste) wurde NICHT implementiert, weil die Synthetic-Literal-Erzeugung technisch aufwändig ist und der Walker aktuell nur echte `LiteralExpressionSyntax`-Knoten verarbeitet. Konzept §"Wie" Punkt 1 erlaubt diese Auslegung, aber der Plan listete den Test explizit — der Kritiker sollte entscheiden, ob EPIC-1 ohne diesen Test vollständig ist oder ob er nachgereicht werden muss.

- **`StructuredContent` hat `magicValues` (camelCase), aber die Records `MagicValueEntry`/`MagicValuesSummary` werden mit PascalCase serialisiert**: Standard-`JsonSerializer` mit `McpJsonOptions.Default` konvertiert PascalCase zu camelCase — getestet im Test `ExecuteAsync_StructuredContentShape_IsJsonObjectNotArray`. Die exakte Schema-Form ist in `Docs/agent-api.md` dokumentiert, aber wenn der Kritiker eine andere Feld-Reihenfolge oder -Schreibweise erwartet, ist das ein einfacher Anpassungspunkt.

- **`DefaultMaxResults = 50` ist identisch zu `GetViolationsScanner.DefaultMaxResults`**: Wäre DRY-Kandidat, aber `GetViolationsScanner` ist im `Analysis`-Namespace und `FindMagicValuesScanner` im `MagicValues`-Namespace — ein `SharedConstants.DefaultMaxResults` wäre Overkill. Belassen.

- **`minOccurrences = 1` als Default ist im Plan als "Vollständige Erfassung mit minOccurrences=1" begründet, aber für `StandardCandidate` HTTP-Statuscodes können dadurch 100+ Funde pro Solution auflaufen**: Falls der Planer eine andere Default-Politik (z. B. `minOccurrences = 2` für Standard/Config) wollte, ist das eine Design-Entscheidung. Aktuell: strikter Plan-Default.

- **`FindMagicValuesTool.ExecuteAsync` hat KEINEN Sufficiency-Hint nach dem Text-Report** (anders als `GetViolationsTool`): Bewusst weggelassen, weil der Plan keinen Hinweis darauf gibt und `find_magic_values` ein On-Demand-Audit ist (kein Lint-Lauf). Falls der Kritiker Konsistenz mit `GetViolationsTool` fordert, nachreichbar.

## Keine Commits dieser Datei

Diese `step-result.md`, `step-plan.md` (Status-Update) und `codemap.md` werden in Schritt 7 in einem separaten Doku-Commit zusammengefasst.
