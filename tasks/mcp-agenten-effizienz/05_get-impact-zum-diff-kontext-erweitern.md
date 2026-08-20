---
status: ready
type: konzept
project_kind: brownfield
estimated_scope: large
priority: P2
agent_role: .agents/Agent-Scaffolding/dev-loop/planning/orchestrator.md
rules_dir: .agents/rules
last_updated: 2026-08-20
open_questions: []
---

# `get_impact` zum deterministischen Diff-Kontext erweitern

## Ziel

Der bestehende Git-Diff-Modus von `get_impact` erhält einen optionalen Detailgrad `change-context`. Ein Aufruf liefert dann die geänderten C#-Symbole, ihre Call-Sites, statisch zugeordnete Tests und direkt betroffene Linter-Violations. Dafür wird **kein neues MCP-Tool** registriert.

## Warum / Kontext

`DiffImpactAnalyzer` berechnet bereits Diff-Hunks und geänderte öffentliche/interne Roslyn-Symbole, verwirft diese Zwischenstruktur aber zugunsten der Call-Sites. Für einen Diff mit mehreren Symbolen muss ein Agent anschließend pro Symbol `get_test_context` und gegebenenfalls `get_feature_context`/`get_violations` aufrufen. Das vermehrt Round-Trips und wiederholt Kontext.

Die Erweiterung ist mit dem aktuellen Stack technisch möglich:

- Git-Diff und Symbolermittlung: `Core/DiffImpactAnalyzer.cs`,
- Referenzen: `FindCallSiteEntriesAsync` und Aufgabe 03,
- statische Test-Zuordnung: `Core/TestCoverageScanner.cs`,
- Violations: `Mcp/Tools/Analysis/GetViolationsScanner.cs`,
- strukturierte Antworten: `McpToolResults.Text<T>`.

## Öffentlicher Vertrag

`get_impact` additiv erweitern:

```text
detailLevel: "callers" | "change-context"   // Default "callers"
maxChangedSymbols: int                       // Default 20, Cap 100
maxTestsPerSymbol: int                       // Default 10, Cap 50
```

- `detailLevel=callers` behält Laufzeit und Ausgabe des bisherigen Git-Modus weitgehend bei.
- `detailLevel=change-context` ist nur im Git-Diff-Modus zulässig. Zusammen mit `symbolIdentifier` liefert der Server `INVALID_ARGUMENT` plus Hinweis auf `get_feature_context`.
- Bestehende Parameter `gitSinceRef`, `depth` und `maxResults` bleiben erhalten.

## Scope

### Must-have

- `DiffImpactAnalyzer` gibt ein strukturiertes Analyseergebnis zurück, ohne Git erneut auszuführen.
- Der bestehende `callers`-Modus behält seinen bisherigen Scope auf öffentliche/interne Methoden und Konstruktoren.
- `change-context` verwendet einen breiteren Diff-Symbolscanner: private/protected/internal/public Methoden und Konstruktoren, Properties/Indexer, Events, Felder, Typdeklarationen und lokale Funktionen. Lokale Variablen und reine Statement-Knoten sind keine eigenständigen Zielsymbole.
- Pro geänderter Zeile wird die innerste passende Deklaration gewählt; dadurch werden nicht gleichzeitig Methode und enthaltender Typ als zwei Änderungen gemeldet. Partielle Typdeklarationen bleiben anhand Datei und Deklarationsspanne unterscheidbar.
- Geänderte Symbole enthalten stabile ID, Accessibility, Kind, Anzeigename, Projekt, Datei und Deklarationszeilen.
- Call-Sites verwenden das strukturierte Ergebnis aus Aufgabe 03.
- **Traversierungs-Korrektur in `CallGraphTraversal.ExpandAsync`:** BFS-Kindknoten enqueuen den tatsächlichen einschließenden Aufrufer (`callerSymbol` via `SemanticModel.GetEnclosingSymbol().NormalizeToOwningMember()`) statt nur `reference.Definition`. Damit liefert `depth > 1` auch für reguläre Methoden echte mehrstufige Aufruferketten (`A -> B -> C`).
- **Sufficiency-Hint Parität:** `GetImpactTool` (Symbol-Branch) hängt im Erfolgsfall bei vollständigen Ergebnissen konsistent `McpSufficiencyHints.Append` an (identisch zu `FindReferencesTool`).
- Tests werden für alle gezeigten geänderten Symbole in einem gebatchten Solution-Scan zugeordnet; kein vollständiger Testprojekt-Scan pro Symbol.
- Violations werden einmal solutionweit berechnet und danach auf geänderte Hunks bzw. Symbolspannen gefiltert.
- Antwort enthält explizite Vollständigkeitsmetadaten für Symbol-, Call-Site- und Test-Caps.
- Textantwort ist eine kompakte Zusammenfassung; detaillierte Einträge stehen im `structuredContent`.
- Bestehender `callers`-Modus bleibt abwärtskompatibel.

### Nice-to-have

- Deduplizierte `dotnet test`-Filterbefehle pro betroffenem Testprojekt.
- `changedFiles` mit kompakten Hunk-Ranges statt Liste jeder einzelnen geänderten Zeile.

### Non-Goals

- Keine natürliche Sprache als Suchquery.
- Keine Embeddings, keine semantische Textähnlichkeit und kein RAG.
- Keine automatische Codeänderung oder Testausführung.
- Keine Metrics-Duplikation aus `get_feature_context`.
- Keine lokalen Variablen, Parameter oder einzelnen Statements als Zielsymbole.
- Keine Garantie echter Test-Coverage.

## Internes Ergebnisobjekt

Mindestens folgende Information erhalten:

```csharp
internal sealed record DiffImpactAnalysis(
    string RepositoryRoot,
    string? SinceRef,
    IReadOnlyList<ChangedFileRange> ChangedFiles,
    IReadOnlyList<ChangedSymbolEntry> ChangedSymbols,
    ReferenceTraversalResult References);
```

`AnalyzeEntriesAsync` darf als kompatibler Wrapper bestehen bleiben, soll intern aber das neue Ergebnisobjekt verwenden. Git darf pro Toolaufruf genau einmal ausgeführt werden.

Der breitere Symbolscope darf den bisherigen `callers`-Modus nicht stillschweigend verändern. Dafür entweder zwei klar benannte Scannerpfade oder einen expliziten Scope-Parameter im internen Analyzer verwenden; kein verstecktes boolesches Flag. Ein Diff an einer privaten Methode muss im `change-context` erscheinen, auch wenn keine externen Call-Sites gefunden werden.

## StructuredContent

```json
{
  "mode": "gitDiff",
  "detailLevel": "change-context",
  "changedFiles": [
    { "filePath": "src/App/OrderService.cs", "ranges": [{ "startLine": 40, "lineCount": 8 }] }
  ],
  "changedSymbols": [
    {
      "documentationCommentId": "M:App.OrderService.PlaceAsync",
      "displayName": "OrderService.PlaceAsync",
      "kind": "Method",
      "accessibility": "Public",
      "projectName": "App",
      "filePath": "src/App/OrderService.cs",
      "startLine": 37,
      "endLine": 61
    }
  ],
  "callSites": [],
  "testAssociations": [
    {
      "symbolId": "M:App.OrderService.PlaceAsync",
      "filePath": "tests/App.Tests/OrderServiceTests.cs",
      "testMethods": ["PlaceAsync_ValidOrder_Persists"],
      "matchReason": "Direct Member Match / Invocation"
    }
  ],
  "violations": [],
  "recommendedTestCommands": [],
  "completeness": {
    "changedSymbolsTotal": 3,
    "changedSymbolsShown": 3,
    "symbolsTruncated": false,
    "callSitesTruncated": false,
    "testsTruncated": false
  }
}
```

JSON-Feldnamen sind additiv und in `Docs/agent-api.md` exakt zu dokumentieren.

## Filterregeln für Violations

Eine Violation ist direkt relevant, wenn mindestens eine Bedingung erfüllt ist:

1. Datei und Zeile liegen in einem geänderten Hunk.
2. Datei und Zeile liegen in der Deklarationsspanne eines gezeigten geänderten Symbols.

Andere Violations derselben Datei werden nicht aufgenommen. Damit bleibt die Antwort diffbezogen und wird nicht zu einem zweiten ungescopten `get_violations`.

## Performance- und Größenregeln

- Testdokumente pro Aufruf höchstens einmal parsen/semantisch auswerten.
- Linter genau einmal ausführen.
- Geänderte Symbole vor teuren Folgeanalysen deterministisch kappen: Projekt, Datei, Startzeile, Symbol-ID.
- Im Text nur Counts und höchstens die bereits gekappten Top-Einträge ausgeben; JSON und Markdown dürfen keine zwei verschieden großen Vollkopien langer Bodies enthalten.
- Keine Source-Bodies in dieser Antwort; dafür bleibt `get_symbol_body` zuständig.

## Tests

- Neutrale Fixture mit mindestens zwei Produktionsprojekten und einem Testprojekt.
- Diff verändert zwei Methoden in zwei Dateien; beide erscheinen als `changedSymbols`.
- Eine davon ist privat und hat keine externen Aufrufstellen; sie erscheint trotzdem im `change-context`.
- Eine Änderung innerhalb einer Methode meldet nur die Methode, nicht zusätzlich den enthaltenden Typ.
- Direkte und transitive Call-Sites stimmen mit `find_references` überein.
- Echte Methoden-Aufruferkette (`MethodA -> MethodB -> MethodC`, nicht nur Interface-Overrides) liefert bei `depth=2` in `find_references` und `get_impact` Aufrufstellen auf Ebene 1 und Ebene 2 mit korrekter `Depth` und `ReachedFromSymbolId`.
- `GetImpactTool` im Symbol-Branch hängt bei vollständigen Ergebnissen den Sufficiency-Hint `(Vollstaendig - keine weiteren Calls noetig)` an.
- Test-Zuordnung enthält mindestens direkte Invocation und Namenskonvention als getrennte Evidenzarten.
- Nur eine Violation innerhalb Hunk/Symbolspanne wird aufgenommen; benachbarte irrelevante Violation derselben Datei nicht.
- `detailLevel=callers` bleibt snapshot-kompatibel.
- `detailLevel=change-context` plus `symbolIdentifier` liefert recoverable `INVALID_ARGUMENT`.
- Caps setzen die passenden Completeness-Felder.
- Instrumentierter Test/Counter weist nach: Git einmal, Testsolution einmal, Linter einmal.

## Definition of Done

- Ein Git-Diff kann mit einem `get_impact(detailLevel="change-context")` vollständig lokalisiert werden.
- `CallGraphTraversal.ExpandAsync` traversiert echte Aufruferketten über `GetEnclosingSymbol()`.
- Kein neues MCP-Tool wurde registriert.
- Keine N-malige Vollsolution-Abtastung pro geändertem Symbol.
- Antwort ist deterministisch, gekappt und vollständigkeitsbewusst.
- Dokumentation nennt die Testdaten korrekt „statische Zuordnung“.
- `dotnet build` sowie beide Nicht-Stress-Testprojekte sind grün.
