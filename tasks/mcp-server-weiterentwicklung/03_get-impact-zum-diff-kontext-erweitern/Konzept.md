---
status: ready
type: konzept
project_kind: brownfield
estimated_scope: large
priority: P2
agent_role: .agents/Agent-Scaffolding/dev-loop/planning/orchestrator.md
rules_dir: .agents/rules
last_updated: 2026-08-21
open_questions: []
herkunft: "1:1 uebernommen aus tasks/mcp-agenten-effizienz/05_get-impact-zum-diff-kontext-erweitern.md (Konsolidierung 2026-08-21)"
---

# `get_impact` zum deterministischen Diff-Kontext erweitern

## Ziel

Der bestehende Git-Diff-Modus von `get_impact` erhält einen optionalen Detailgrad `change-context`. Ein Aufruf liefert dann die geänderten C#-Symbole, ihre Call-Sites, statisch zugeordnete Tests und direkt betroffene Linter-Violations. Dafür wird **kein neues MCP-Tool** registriert.

## Warum / Kontext

`DiffImpactAnalyzer` berechnet bereits Diff-Hunks und geänderte öffentliche/interne Roslyn-Symbole, verwirft diese Zwischenstruktur aber zugunsten der Call-Sites. Für einen Diff mit mehreren Symbolen muss ein Agent anschließend pro Symbol `get_test_context` und gegebenenfalls `get_feature_context`/`get_violations` aufrufen. Das vermehrt Round-Trips und wiederholt Kontext.

Die Erweiterung ist mit dem aktuellen Stack technisch möglich:

- Git-Diff und Symbolermittlung: `Core/DiffImpactAnalyzer.cs`,
- Referenzen: `FindCallSiteEntriesAsync` und die strukturierte Ausgabe der Hybridsuche-Initiative,
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
- Call-Sites verwenden das strukturierte Ergebnis der transitive-Ausgaben-Aufgabe.
- **Traversierungs-Korrektur in `CallGraphTraversal.ExpandAsync`:** BFS-Kindknoten enqueuen den tatsächlichen einschließenden Aufrufer (`callerSymbol` via `SemanticModel.GetEnclosingSymbol().NormalizeToOwningMember()`) statt nur `reference.Definition`. Damit liefert `depth > 1` auch für reguläre Methoden echte mehrstufige Aufruferketten (`A -> B -> C`).
- **Sufficiency-Hint Parität:** `GetImpactTool` (Symbol-Branch) hängt im Erfolgsfall bei vollständigen Ergebnissen konsistent `McpSufficiencyHints.Append` an (identisch zu `FindReferencesTool`).
- Tests werden für alle gezeigten geänderten Symbole in einem gebatchten Solution-Scan zugeordnet; kein vollständiger Testprojekt-Scan pro Symbol.
- Violations werden einmal solutionweit berechnet und danach auf geänderte Hunks bzw. Symbolspannen gefiltert.
- Antwort enthält explizite Vollständigkeitsmetadaten für Symbol-, Call-Site- und Test-Caps.
- Textantwort ist eine kompakte Zusammenfassung; detaillierte Einträge stehen im `structuredContent`.
- Bestehender `callers`-Modus bleibt abwärtskompatibel.
- Deduplizierte `dotnet test`-Filterbefehle pro betroffenem Testprojekt (aus dem ehemaligen
  Nice-to-have hochgestuft — das Antwortbeispiel enthält `recommendedTestCommands` bereits
  als vertraglichen Bestandteil; ohne Umsetzung wäre das Beispiel falsch).
- `changedFiles` mit kompakten Hunk-Ranges statt Liste jeder einzelnen geänderten Zeile
  (aus dem ehemaligen Nice-to-have hochgestuft — das Antwortbeispiel definiert genau
  dieses Format; Nice-to-haves gibt es in diesem Task nicht, siehe Audit-Abschnitt).

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

---

# Audit zweiter Pass (2026-08-21): Funde und Präzisierungen

Verifiziert gegen `CallGraphTraversal.cs`, `GetImpactTool.cs`, `DiffImpactAnalyzer.cs`
(Skeleton + Schlüsselstellen) und `TestCoverageScanner.cs`. Die Kernbehauptungen des
Konzepts halten alle; zusätzlich wurden Randfälle und ein Kompatibilitätsthema gefunden.

## A. Verifizierte Kernbehauptungen

1. **Traversierungs-Bug bestätigt, schärfer als beschrieben:** `EnqueueChildren`
   (`CallGraphTraversal.cs:126-134`) enqueued `reference.Definition`. Für
   `FindReferencesAsync(current)` ist `Definition` aber meist `current` selbst — das ist
   bereits in `_seen`, wird also gar nicht enqueued. `depth > 1` expandiert heute faktisch
   nur über Override-/Interface-Definitionen, nicht über Aufruferketten. Der vorgeschlagene
   Fix (`GetEnclosingSymbol().NormalizeToOwningMember()` pro Referenzlocation) ist richtig.
2. **Scope-Behauptung bestätigt:** `IsPublicOrInternal`
   (`DiffImpactAnalyzer.cs:301`, genutzt in `GetValidChangedSymbol:280`) filtert den
   heutigen Git-Modus auf public/internal — der breitere `change-context`-Scanner ist
   tatsächlich neu.
3. **Sufficiency-Hint-Lücke bestätigt:** `GetImpactTool.ExecuteSymbolBranchAsync`
   hängt heute keinen Sufficiency-Hint an (`GetImpactTool.cs:47-65`), während
   `FindReferencesTool` dies tut.
4. **Git-einmal-pro-Call gilt heute schon** (`RunGitDiff` einmal in
   `AnalyzeEntriesAsync`) — die Performance-Regel zielt auf die anderen beiden
   N-mal-Muster, die real existieren (siehe C).

## B. Kompatibilitätsthema: Der Traversierungs-Fix ändert Bestandsverhalten

`ExpandAsync` wird von **zwei** Tools genutzt (`find_references`,
`GetImpactTool` Symbol-Branch). Der Fix verändert deren `depth > 1`-Ausgabe von
"faktisch leer/Override-only" zu "echte Aufruferketten". Das ist die Intention, aber:

- Bestehende Tests (`CallGraphTraversalTests.ExpandAsync_Depth2_*`) können das alte,
  defekte Verhalten als Erwartung kodieren — sie sind als Verhaltenstests zu prüfen und
  ggf. bewusst umzustellen, nicht mechanisch grün zu zwingen (Symptom-Fixing-Verbot).
- Die Änderung ist in `Docs/agent-api.md` als Verhaltenskorrektur (nicht nur additive
  Erweiterung) auszuweisen.

## C. Performance-Fundament ist größer als angedeutet

Zwei existierende N-mal-Muster müssen für `change-context` aktiv konsolidiert werden:

1. `FindAllCallSiteEntriesAsync` ruft `FindCallSiteEntriesAsync` **pro geändertem Symbol**
   auf — jeder ein voller Solution-weiter `SymbolFinder.FindReferencesAsync`-Lauf.
2. `TestCoverageScanner.FindTestsForSymbolAsync` ist per-Symbol-API und scannt bei jedem
   Aufruf alle Testprojekte. Die geforderte Batch-Zuordnung erfordert echte Refactoring-
   Arbeit: Testprojekte einmal parsen/semantisch auswerten und gegen **alle** gekappten
   Symbole matchen.

Das ist machbar, aber der größte Einzelblock des Tasks — `estimated_scope: large` ist
korrekt.

## D. Randfälle und Präzisierungen

1. **Gelöschte Dateien erscheinen nicht:** `ParseGitDiffHunks` wertet nur `+++ b/`-Zeilen
   aus; gelöschte Dateien (`+++ /dev/null`) liefern keine Hunks. Gelöschte Symbole können
   folglich nie in `changedSymbols` auftreten (ihr Deklarationsknoten existiert nicht mehr
   im Snapshot). Inhärente Grenze — muss in `Docs/agent-api.md` dokumentiert stehen,
   nicht stillschweigend fehlen.
2. **Umbenannte Dateien:** Mit Git-Rename-Detection landen Hunks unter dem neuen Pfad —
   akzeptabel; ohne Detection entstehen Löschung+Neuanlage mit denselben Grenzen wie D.1.
3. **`depth` im `change-context`:** Der Git-Branch ignoriert `depth` heute
   (`GetImpactTool.cs:19-20`). Entscheidung: **`depth` bleibt im gesamten Git-Branch
   wirkungslos** (auch in `change-context`); die Call-Site-Tiefe ergibt sich aus dem
   Traversal-Ergebnis der strukturierten Ausgaben-Aufgabe. In den Vertragstext aufnehmen.
4. **Stabile IDs für lokale Funktionen:** Lokale Funktionen haben keine
   DocumentationCommentId; hier greift der Fallback (voll qualifizierter Display-String).
   Vertrag formulieren als "DocCommentId oder deterministischer Fallback".
5. **Linter-Kosten:** "Linter genau einmal" bedeutet einen vollständigen Solution-Lint pro
   `change-context`-Aufruf. Auf großen Solutions spürbar; bewusst akzeptiert, da die
   Violation-Filterung sonst nicht diffbezogen bliebe. Antwortbudgets decken die Größe,
   nicht die Laufzeit.
6. **Parameter-Record-Wachstum:** `GetImpactInput` hat bereits 4 Parameter
   (`MaxMethodParameterCount: 4`). Neue Optionen (`detailLevel`, `maxChangedSymbols`,
   `maxTestsPerSymbol`) kommen additiv mit Default-Werten in den Record; die
   Linter-Regeln sind bei der Delegat-Signatur zu prüfen.
7. **`BuildAggregateWarningAsync` mit `CancellationToken.None`** im Git-Branch
   (`GetImpactTool.cs:87`) — beim Umbau an den echten `ct` anbinden.

## E. Nice-to-have-Regel (Nutzerentscheidung 2026-08-21)

Dieser Task kennt keine Nice-to-haves. Beide ehemaligen Punkte sind nach Muss-Have
hochgestuft (Begründung dort), weil das Antwortbeispiel sie bereits als vertraglichen
Bestandteil definiert. Alles, was nicht Muss ist, steht in Non-Goals.

## F. Ergänzte DoD-/Test-Punkte

- Test: Gelöschte Datei im Diff → taucht nicht in `changedSymbols` auf; Antwort bleibt
  valide (dokumentierte Grenze, kein Fehlerfall).
- Test: Bestehende `ExpandAsync_Depth2`-Tests wurden bewusst reviewed und entweder als
  korrekt bestätigt oder als Kodifikation des Defekts umgestellt (Entscheidung dokumentieren).
- Vertragstext: `depth` ist im gesamten Git-Branch wirkungslos (inkl. `change-context`).
- Vertragstext: Stabile ID = DocCommentId oder deterministischer Fallback (lokale Funktionen).
- `GetImpactInput` wächst nur additiv mit Default-Werten; Linter-Regeln bleiben eingehalten.


