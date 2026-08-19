---
status: ready
type: konzept
project_kind: brownfield
estimated_scope: medium
rules_dir: .agents/rules
last_updated: 2026-08-19T20:20:00+02:00
open_questions: []
---

# Konzept: `get_feature_context` (Composite One-Shot-Exploration)

## Ziel (Was)
Ein neues MCP-Tool `get_feature_context`, das für ein beliebiges C#-Symbol (Typ, Methode, Property, `Datei.cs:Zeile` oder `DocCommentId`) in einem einzigen residenten Aufruf fünf wesentliche Dimensionen bündelt:
1. **Symbol-Deklaration & Metadaten:** Was ist das für ein Typ/Member, Sichtbarkeit, Datei und Zeilenspanne.
2. **Metriken & Budget:** Cyclomatic / Cognitive Complexity, LOC, Parameter, AI-Context-Footprint und Schwellwert-Abgleich gegen `rules.json`.
3. **Direkte Aufrufer (Callers):** Direkte Aufrufstellen in der Solution (depth=1).
4. **Test-Abdeckung:** Testklassen, Testmethoden (`[Fact]`, `[Theory]`, `[Test]`) und Testkategorien (Unit, Component, Integration), die das Symbol abdecken.
5. **Offene Linter-Violations:** Offene Verstöße auf der Zieldatei mit Kennzeichnung, ob sie direkt innerhalb der Zeilenspanne des Ziel-Symbols liegen.

Das Tool liefert einen kompakten Markdown-Report sowie ein typisiertes `StructuredContent`-Objekt (`FeatureContextPayload`).

## Warum / Kontext
Beim Modifizieren von Code oder Vorbereiten eines Refactorings benötigt ein Coding-Agent stets dasselbe Informations-Bündel. Bisher muss der Agent hierfür 4–5 separate MCP-Tools hintereinander aufrufen (`find_symbol`/`get_class_structure` ➔ `metrics_lookup` ➔ `find_references` ➔ `get_violations`). Dies verursacht 4–5 Hin- und Rückrunden (Roundtrips), verbraucht signifikant mehr Token und fragmentiert den Kontext.

Mit `get_feature_context` wird dieser Prozess auf einen einzigen residenten In-Memory-Call reduziert (< 50ms Ausführungszeit, maximale Token- und Zeiteinsparung).

## Scope

### Muss-Haben
- **MCP-Tool `get_feature_context`** registriert in `AnalysisToolRegistrations.cs` im Namespace `AiNetLinter.Mcp.Tools.FeatureContext`.
- **Flexible Symbol-Auflösung** über `FindReferencesTool.ResolveSymbolAsync` (Name, qualifizierter Name, `Datei.cs:Zeile`, `Datei.cs:Zeile:Spalte`, `DocCommentId`).
- **5-Sektions-Aggregation (Composite-Muster)**:
  - Sektion 1: Symbol & Deklaration (Kind, Accessibility, Pfad, Zeilen, Container-Typ).
  - Sektion 2: Metriken & Budget (CC, CogC, LOC, Parameter, AI-Footprint aus `MetricsLookupScanner`).
  - Sektion 3: Direkte Aufrufer via `DiffImpactAnalyzer.FindCallSiteEntriesAsync`.
  - Sektion 4: Test-Abdeckung über neuen residenten `TestCoverageScanner` (Testdateien, Testmethoden, Test-Kategorien, Zuordnungsgrund).
  - Sektion 5: Offene Linter-Violations der Datei via `LinterEngine` / `GetViolationsScanner` mit Kennzeichnung von Symbol-Matches.
- **Modulare Steuerungs-Flags**:
  - `symbol` (string, required): Symbol-Bezeichner.
  - `includeCallers` (bool, default: `true`): Ob Aufrufer gelistet werden.
  - `includeTests` (bool, default: `true`): Ob zugehörige Tests gelistet werden.
  - `includeMetrics` (bool, default: `true`): Ob Metriken ermittelt werden.
  - `includeViolations` (bool, default: `true`): Ob offene Violations ausgegeben werden.
  - `maxCallers` (int, default: `10`, cap: `50`): Maximale Anzahl Aufrufer.
  - `maxTests` (int, default: `10`, cap: `50`): Maximale Anzahl gelisteter Testdateien/-methoden.
- **StructuredContent-Support**: Typisiertes JSON-Objekt `FeatureContextPayload` für programmatische Auswertung.
- **Fehlerbehandlung**: Saubere `McpToolResults.SymbolNotFound`-, `AmbiguousSymbol`- und `CompilationError`-Rückgaben.
- **Gemeinsamer Test-Discovery-Core**: `TestCoverageScanner` in `AiNetLinter.Core` oder `AiNetLinter.Mcp.Tools.FeatureContext`, der auch von Task 02 (`get_test_context`) wiederverwendet werden kann.
- **Unit- & FastTests**: Umfassende Testsuite in `AiNetLinter.FastTests`.
- **Dokumentation**: Aktualisierung von `Docs/configuration.md`, `Docs/integration.md`, `Docs/ROADMAP.md` und `README.md`.

### Nice-to-Have
*(Leer – alle Punkte vor `status: ready` entschieden)*

### Non-Goals (bewusst NICHT Teil davon)
- **Keine rekursive/transitive AST-Tiefenexploration:** Transitive Caller-Bäume gehören zu `get_call_tree` (depth > 1); `get_feature_context` konzentriert sich bewusst auf unmittelbare Direktanrufer (depth = 1) für maximale Geschwindigkeit und Kompaktheit.
- **Kein Inline-Code-Viewer:** Das Anzeigen des vollständigen Quellcodes bleibt Aufgabe von `get_symbol_body`.
- **Keine automatische Code-Modifikation / Fix-Generierung:** Das Tool ist ein reines Analyse- & Kontextwerkzeug.

## Zielplattformen / Technischer Rahmen
- **.NET 10 / C# 13:** Roslyn-basierter residenter In-Memory Server (`McpCodeGraphServer`).
- **Composite-Architektur:** Direkte Wiederverwendung residenter Services ohne Code-Duplikate:
  - `FindReferencesTool.ResolveSymbolAsync`
  - `MetricsLookupScanner.ScanSymbol`
  - `DiffImpactAnalyzer.FindCallSiteEntriesAsync`
  - `GetViolationsScanner` / `LinterEngine`
  - Neuer wiederverwendbarer `TestCoverageScanner`

## Verworfene Alternativen
- **Client-seitige Verkettung (5 separate Tool-Calls):** Verworfen wegen hohem Token-Overhead und 4–5 Roundtrip-Latenzen.
- **Vollständiger Source-Code-Abdruck im Kontext:** Verworfen, da bei großen Typen/Methoden das Token-Budget überlastet würde.

## Wo im Projekt
- `src/AiNetLinter/Mcp/Tools/FeatureContext/`
  - `GetFeatureContextTool.cs` (MCP Tool-Klasse)
  - `FeatureContextScanner.cs` (Aggregations-Logik)
  - `FeatureContextModels.cs` (Records für StructuredContent & DTOs)
  - `FeatureContextFormatter.cs` (Markdown-Formatierung)
- `src/AiNetLinter/Core/TestCoverageScanner.cs` (Test-Discovery-Komponente für Roslyn-Syntax/Semantik)
- `src/AiNetLinter/Mcp/AnalysisToolRegistrations.cs` (Tool-Registrierung)
- `src/AiNetLinter.FastTests/Mcp/GetFeatureContextToolTests.cs` (Unit- & Component-Tests)

## Entdeckte Mängel/Redundanzen
- **TestCoverageIndex speichert aktuell nur Typnamen als Strings:**
  - **Gefunden:** `src/AiNetLinter/Core/TestCoverageIndex.cs` und `TestCoverageResolver.cs` speichern nur `HashSet<string>`.
  - **Entscheidung:** Einführung von `TestCoverageScanner`, der Testklassen, Testmethoden (`[Fact]`, `[Theory]`, `[Test]`) und `@covers`/`typeof`-Referenzen auf Symbole auflöst. Wird direkt für `get_feature_context` und Task 02 genutzt.

## Wie (grober Ansatz)
1. **Symbol-Auflösung:** Ermittlung des Ziel-`ISymbol` über `FindReferencesTool.ResolveSymbolAsync`.
2. **Parallele/Sequentielle Teil-Scans:**
   - Deklaration & Metadaten extrahieren.
   - Wenn `includeMetrics == true`: `MetricsLookupScanner.ScanSymbol(...)` aufrufen.
   - Wenn `includeCallers == true`: `DiffImpactAnalyzer.FindCallSiteEntriesAsync(symbol, solution)` aufrufen.
   - Wenn `includeTests == true`: `TestCoverageScanner.FindTestsForSymbolAsync(symbol, solution, ct)` aufrufen.
   - Wenn `includeViolations == true`: `LinterEngine`-Violations der Zieldatei abrufen und auf Symbol-Matches filtern.
3. **Formatierung & Payload:**
   - Markdown-Report formatieren.
   - `FeatureContextPayload` erzeugen und als `StructuredContent` übergeben.

## Definition of Done / Erfolgskriterien
1. `get_feature_context` löst Typen, Methoden und Properties über alle Standard-Identifikatoren (Name, `Datei.cs:Zeile`, `DocCommentId`) auf.
2. Liefert alle 5 Sektionen (Deklaration, Metriken mit Schwellwert-Check, Callers, Tests, Violations) in einem einzigen Call.
3. Alle Teilbereiche sind über boolesche Flags steuerbar (`includeCallers`, `includeTests`, `includeMetrics`, `includeViolations`).
4. `StructuredContent` liefert ein valides, typisiertes `FeatureContextPayload`-Objekt.
5. Bei unbekanntem oder mehrdeutigem Symbol wird ein sauberes `McpToolResults.SymbolNotFound` bzw. `AmbiguousSymbol` geliefert.
6. 15+ Tests in `AiNetLinter.FastTests` belegen alle Sektionen, Filter-Flags und Randfälle.
7. Alle bestehenden Tests laufen grün (`dotnet test src/AiNetLinter.FastTests --filter Category!=Stress` und `dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress`).
8. Doku in `Docs/configuration.md`, `Docs/integration.md`, `Docs/ROADMAP.md` und `README.md` ist synchronisiert.

## Offene Punkte
- Keine. Bereit für Umsetzung.
