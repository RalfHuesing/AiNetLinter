---
title: "metrics_lookup — One-Shot-Metriken & AI-Context-Footprint"
status: completed
type: konzept
project_kind: brownfield
estimated_scope: small
rules_dir: .agents/rules
last_updated: 2026-08-19
open_questions: []
---

# Konzept: `metrics_lookup` (One-Shot-Metriken & AI-Context-Footprint)

## Ziel (Was)

Ein schnelles, leichtgewichtiges MCP-Tool `metrics_lookup`, das für ein gezieltes C#-Symbol (Methode, Konstruktor, Property, Klasse, Record, Struct, Interface, Enum) alle relevanten Metriken, Netto-Zeilenangaben, Komplexitätswerte und Schwellwert-Vergleiche mit der aktiven `rules.json` in einem einzigen Aufruf zurückliefert (sowohl als lesbares Markdown als auch als typisiertes `StructuredContent`-JSON).

## Warum / Kontext

Wenn ein KI-Agent ein bestimmtes Symbol analysiert (z. B. vor einem Refactoring oder vor dem Hinzufügen von Methodenlogik), benötigt er sofortige Klarheit über:
- Komplexität (Zyklomatische & Kognitive Komplexität),
- Zeilenanzahl (Netto-Codezeilen ohne Kommentare/Leerzeilen),
- Parameter-Anzahl & Schwellwert-Reserven (droht ein Limit-Bruch?),
- AI-Context-Footprint & Member-Statistiken bei Klassen/Typen.

Bisher musste der Agent entweder die Datei ganz einlesen (Token-intensiv) oder `metrics_tree` bemühen (Baum-Traversal über Dateipfade, umständlich für Einzelsymbole). `metrics_lookup` schließt diese Lücke als punktgenaues Symbol-Analysewerkzeug ohne Token-Ballast.

## Scope

### Muss-Haben

1. **Tool-Name & Registrierung:**
   - Name: `metrics_lookup`
   - Registrierung in `AnalysisToolRegistrations.cs` (`AddMetricsLookup`).
2. **Parameter:**
   - `symbolIdentifier` (string, required): Format wie bei `find_references` / `get_symbol_body` / `get_impact` (`M:Namespace.Klasse.Methode`, `Datei.cs:Zeile:Spalte`, `Datei.cs:Zeile`, `Namespace.Klasse.Methode` oder `Klasse.Methode`).
   - `ct` (`CancellationToken`, default): Kooperativer Abbruch.
3. **Symbol-Auflösung:**
   - Wiederverwendung der erprobten `FindReferencesTool.ResolveSymbolAsync`-Pipeline (inkl. `SymbolIdentifierResolver`).
4. **Generalisierung von `ComplexityCalculator`:**
   - Überladungen `GetCyclomaticComplexity(SyntaxNode node)` und `GetCognitiveComplexity(SyntaxNode node)` in `ComplexityCalculator` ergänzen, damit auch Konstruktoren (`ConstructorDeclarationSyntax`), Properties/Accessoren (`AccessorDeclarationSyntax`, `PropertyDeclarationSyntax`) und Lambdas ohne Code-Duplikation einheitlich berechnet werden.
5. **Unterstützte Symbol-Arten & Metriken:**
   - **Methoden / Konstruktoren / Operatoren (`IMethodSymbol`):**
     - Netto-Codezeilen (`CodeLines`) via `MethodLineCounter`.
     - Start-/Endzeile und Quellpfad (`FilePath`, `StartLine`, `EndLine`).
     - Zyklomatische Komplexität (`CyclomaticComplexity`).
     - Kognitive Komplexität (`CognitiveComplexity`).
     - Parameter-Anzahl: `TotalParameters` und `EffectiveParameters` (unter Beachtung von `Config.Metrics.MethodParameterCountIgnoreTypeNames` / `Prefixes`).
     - Schwellwert-Vergleiche: `MaxMethodLineCount`, `MaxCyclomaticComplexity`, `MaxCognitiveComplexity`, `MaxMethodParameterCount`.
   - **Properties / Indexer / Events (`IPropertySymbol`, `IEventSymbol`):**
     - Netto-Codezeilen des Members.
     - Komplexität der Accessoren (z. B. Getter/Setter-Komplexität, Max-Komplexität).
     - Start-/Endzeile und Quellpfad.
   - **Typen (Klasse, Record, Struct, Interface, Enum via `INamedTypeSymbol`):**
     - Netto-Codezeilen des Typs (`CodeLines`).
     - `AIContextFootprint` & Top-Abhängigkeiten via `AIContextFootprintCalculator.CalculateDetailed`.
     - Member-Statistiken: `PublicMemberCount`, `TotalMemberCount`, `MethodCount`, `PropertyCount`.
     - Schwellwert-Vergleiche: `MaxLineCount`, `MaxAIContextFootprint`.
6. **Schwellwert-Vergleich & Status-Auswertung:**
   - Dreistufiger Status pro Metrik:
     - `OK`: Eingehalten.
     - `WARN`: Near-Miss / Warnung (falls Schwellwert knapp erreicht).
     - `VIOLATION`: Schwellwert überschritten.
   - Angabe des konfigurierten Grenzwerts (`Limit`) und der Regel-ID.
7. **Rückgabe & Formatierung:**
   - Protokollgerechtes `CallToolResult` via `McpToolResults.Text(markdown, payload)`.
   - Markdown: Übersichtliche Tabelle / Key-Value-Darstellung mit `[OK]`, `[WARN]`, `[VIOLATION]`.
   - `StructuredContent`: Stark typisiertes JSON-Objekt (`MetricsLookupResultDto`) über `McpJsonOptions.Default`.
8. **Fehlerbehandlung:**
   - Konsistent mit `IsErrorPolicy.md` (`SymbolNotFound`, `AmbiguousSymbol`, `InvalidArgument` -> `IsError = false` mit Hilfe-Hinweis; `SolutionNotLoaded` -> `IsError = true`).
9. **Tests:**
   - Umfassende Unit- & Component-Tests in `AiNetLinter.FastTests` für alle Symbol-Arten, Überladungen, Parameter-Filter und Fehlerfälle.

### Nice-to-Have (aufgelöst vor `status: ready`)

- *Keine verbleibenden Punkte.*

### Non-Goals (bewusst NICHT Teil davon)

- Keine rekursive Ordneranalyse (dafür existiert `metrics_tree`).
- Kein Batch-Lookup mehrerer Symbole in einem Call.
- Keine Code-Modifikation (reines Analyse-Werkzeug).

## Zielplattformen / Technischer Rahmen

- **.NET 10 / C# 13**, Roslyn Adhoc- / MSBuild-Workspace.
- **ModelContextProtocol.Protocol**: `CallToolResult` mit `TextContentBlock` und `StructuredContent`.
- **Zero New Dependencies**: Reine Nutzung der internen Roslyn- und Metrik-Klassen.

## Verworfene Alternativen

- **Metriken in `get_symbol_body` integrieren:** Verworfen, da `get_symbol_body` für Quelltext-Abruf gedacht ist und schlank bleiben soll.
- **`metrics_tree` für Einzelsymbole zweckentfremden:** Verworfen, da `metrics_tree` Verzeichnishierarchien traversiert.

## Wo im Projekt

- [AnalysisToolRegistrations.cs](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/AnalysisToolRegistrations.cs): `AddMetricsLookup` Registrierung.
- [FindReferencesTool.cs](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Tools/SymbolGraph/FindReferencesTool.cs): Symbol-Resolution `ResolveSymbolAsync`.
- [ComplexityCalculator.cs](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Metrics/ComplexityCalculator.cs): Generalisierung für `SyntaxNode`.
- [AIContextFootprintCalculator.cs](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Metrics/AIContextFootprintCalculator.cs): `CalculateDetailed`.
- [MethodLineCounter.cs](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Metrics/MethodLineCounter.cs): `GetCodeLineCount`.
- `src/AiNetLinter/Mcp/Tools/MetricsLookup/`:
  - `MetricsLookupTool.cs`: MCP-Einstiegspunkt & Exception-Handling.
  - `MetricsLookupScanner.cs`: Metrik-Berechnung & Schwellwert-Abgleich.
  - `MetricsLookupModels.cs`: DTO-Records für `StructuredContent`.
  - `MetricsLookupFormatter.cs`: Markdown-Generierung.
- `src/AiNetLinter.FastTests/Mcp/Tools/MetricsLookup/`: FastTests für alle Szenarien.

## Entdeckte Mängel/Redundanzen

- **`ComplexityCalculator` beschränkt auf `MethodDeclarationSyntax`**
  - **Gefunden:** [ComplexityCalculator.cs:15-30](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Metrics/ComplexityCalculator.cs#L15-L30)
  - **Bezug:** DRY & Clean Code.
  - **Vorschlag:** `GetCyclomaticComplexity(SyntaxNode)` und `GetCognitiveComplexity(SyntaxNode)` einführen.
  - **Entscheidung:** Angenommen -> Teil von Muss-Haben 4.

## Wie (grober Ansatz)

1. **`ComplexityCalculator` anpassen:**
   - Öffentliche Methoden `GetCyclomaticComplexity(SyntaxNode node)` und `GetCognitiveComplexity(SyntaxNode node)` hinzufügen.
   - Vorhandene Methoden `Get*(MethodDeclarationSyntax)` leiten delegierend weiter (100% abwärtskompatibel).
2. **DTO-Modelle erstellen (`MetricsLookupModels.cs`):**
   - `MetricsLookupResultDto` mit discriminated union / Polymorphie oder gemeinsamen Feldern (`SymbolKind`, `SymbolName`, `FilePath`, `LineSpan`, `MethodMetrics`, `TypeMetrics`, `PropertyMetrics`, `ThresholdChecks`).
   - `ThresholdCheckDto(string Metric, int Value, int Limit, string Status, string RuleId)`.
3. **Scanner erstellen (`MetricsLookupScanner.cs`):**
   - Löst `symbolIdentifier` mit `FindReferencesTool.ResolveSymbolAsync` auf.
   - Extrahiert AST-Knoten (`DeclaringSyntaxReferences`).
   - Führt Metrik-Berechnungen durch (Zeilen, CC, CogC, Parameter, AIContextFootprint).
   - Vergleicht mit `Config.Metrics` und erzeugt `ThresholdCheckDto`-Einträge.
4. **Formatter erstellen (`MetricsLookupFormatter.cs`):**
   - Baut lesbare Markdown-Ausgabe mit klaren Status-Badges (`[OK]`, `[WARN]`, `[VIOLATION]`).
5. **Tool & Registrierung anbinden:**
   - `MetricsLookupTool.ExecuteAsync` aufrufen in `AnalysisToolRegistrations.cs`.
   - Tool-Description formulieren.
6. **Tests schreiben:**
   - `AiNetLinter.FastTests` Tests für Methoden, Konstruktoren, Properties, Records, Klassen, Interfaces und Fehlerfälle.

## Definition of Done / Erfolgskriterien

1. `metrics_lookup` löst Symbole per qualifiziertem Namen, `Datei.cs:Zeile:Spalte`, `Datei.cs:Zeile` und `DocCommentId` auf.
2. Liefert bei Methoden/Konstruktoren präzise LOC, CC, CogC, Parameter-Zahlen und Schwellwert-Status.
3. Liefert bei Typen (Klassen/Records/Interfaces/Enums) LOC, Member-Counts, AIContextFootprint und Schwellwert-Status.
4. Liefert bei Properties/Accessoren LOC, Accessor-Komplexitäten und Status.
5. `StructuredContent` ist ein valides JSON-Objekt mit klaren Feldtypen (kein Top-Level Array).
6. Einhaltung aller Vorgaben aus `IsErrorPolicy.md`.
7. `AiNetLinter.FastTests` und `AiNetLinter.IntegrationTests` sind vollständig grün.
8. Zero Compiler-Warnings (`TreatWarningsAsErrors`).
9. Keine Dead-Code-, Magic-Values- oder DRY-Verstöße im neuen Code.
