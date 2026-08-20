---
status: ready
type: konzept
project_kind: brownfield
estimated_scope: medium
priority: P1
agent_role: .agents/Agent-Scaffolding/dev-loop/planning/orchestrator.md
rules_dir: .agents/rules
last_updated: 2026-08-20
open_questions: []
---

# Transitive Symbolgraph-Ausgaben strukturiert und vollständigkeitsbewusst machen

## Ziel

`find_references` und der symbolbasierte Modus von `get_impact` müssen für `depth = 1` bis zum Hard-Cap dieselbe strukturierte Antwortform liefern. Ein Agent darf transitive Ergebnisse nicht aus formatierten Strings zurückparsen müssen und muss zuverlässig erkennen, ob ein Ergebnis vollständig oder gekappt ist.

## Warum / Kontext

Die aktuelle Implementierung dokumentiert den Strukturverlust direkt im Code:

- `src/AiNetLinter/Mcp/Tools/SymbolGraph/FindReferencesTool.cs`: bei `depth > 1` wird kein `structuredContent` geliefert, weil `CallGraphTraversal` Strings erzeugt.
- `src/AiNetLinter/Mcp/Tools/SymbolGraph/GetImpactTool.cs`: identische Einschränkung.
- `src/AiNetLinter/Mcp/Tools/SymbolGraph/CallGraphTraversal.cs`: `ExpandAndFormatAsync` und `TraversalState` aggregieren formatierte Locations statt eines Ergebnisobjekts.

Textparsing ist unnötig fehleranfällig und verliert Tiefe, Herkunftskante und getrennte Trunkierungsgründe. Das ist ein konkret belegter Qualitätsverlust der API, unabhängig vom verwendeten LLM.

## Scope

### Must-have

- Internes Modell für transitive Referenztraversierung einführen.
- Traversierung sammelt Daten; ein separater Formatter erzeugt den bisherigen Text.
- `find_references` liefert für jede erlaubte Tiefe `structuredContent`.
- Symbolbasiertes `get_impact` liefert für jede erlaubte Tiefe dieselbe Struktur.
- Bestehende Top-Level-Eigenschaft `callSites` beibehalten und nur additive Metadaten ergänzen.
- Vollständigkeit getrennt nach `maxResults`, Knoten-Hard-Cap und Depth-Hard-Cap ausweisen.
- Deterministische Sortierung und Deduplizierung implementieren.
- Bestehende Textausgabe und Sufficiency-Hinweise kompatibel halten.

### Non-Goals

- Keine neue Relevanzbewertung oder LLM-basierte Sortierung.
- Kein unbeschränktes Traversieren.
- Keine Änderung von `get_call_tree`; dessen Baumstruktur ist ein eigener Vertrag.
- Kein Cursor-/Session-State.
- Keine Breaking-Umbenennung bestehender JSON-Felder.

## Datenmodell

Mindestens folgende internen Records anlegen; Namen dürfen den Projektkonventionen angepasst werden:

```csharp
internal sealed record TransitiveCallSiteEntry(
    string FilePath,
    int Line,
    string SymbolName,
    string ProjectName,
    int Depth,
    string ReachedFromSymbolId);

internal sealed record TraversalCompleteness(
    int RequestedDepth,
    int EffectiveDepth,
    int VisitedNodeCount,
    int TotalCallSiteCount,
    int ShownCallSiteCount,
    bool TruncatedByMaxResults,
    bool TruncatedByNodeLimit,
    bool DepthWasClamped);

internal sealed record ReferenceTraversalResult(
    IReadOnlyList<TransitiveCallSiteEntry> CallSites,
    TraversalCompleteness Completeness);
```

`ReachedFromSymbolId` ist die stabile DocumentationCommentId des Symbols, dessen Referenzen in diesem Traversierungsschritt untersucht wurden. Falls für ein Roslyn-Symbol keine ID existiert, einen deterministischen qualifizierten Anzeigenamen verwenden. Keine zufälligen IDs erzeugen.

## Algorithmus

1. BFS und bestehende Hard-Caps beibehalten.
2. Queue-Eintrag muss Symbol plus aktuelle Tiefe enthalten.
3. Für jede ReferenceLocation einen strukturierten Eintrag erzeugen, bevor formatiert wird.
4. Deduplizierschlüssel: normalisierter `FilePath`, `Line`, `SymbolName`, `Depth`, `ReachedFromSymbolId`.
5. Sortierung: `Depth`, dann `FilePath` ordinal-ignore-case, `Line`, `SymbolName` ordinal.
6. Erst nach vollständiger Aggregation auf `maxResults` kappen. `TotalCallSiteCount` bleibt ungekappte Anzahl innerhalb des Traversierungs-Hard-Caps.
7. Formatter aus dem Ergebnisobjekt speisen. Keine zweite Traversierung für Text und JSON.

## Antwortvertrag

Erfolgsantwort mindestens:

```json
{
  "callSites": [
    {
      "filePath": "src/App/OrderService.cs",
      "line": 42,
      "symbolName": "OrderService.PlaceAsync",
      "projectName": "App",
      "depth": 2,
      "reachedFromSymbolId": "M:App.OrderFacade.PlaceAsync"
    }
  ],
  "completeness": {
    "requestedDepth": 2,
    "effectiveDepth": 2,
    "visitedNodeCount": 8,
    "totalCallSiteCount": 14,
    "shownCallSiteCount": 14,
    "truncatedByMaxResults": false,
    "truncatedByNodeLimit": false,
    "depthWasClamped": false
  }
}
```

Bei recoverable Fehlern bleibt die bestehende `isError`-Policy erhalten; es muss kein Erfolgsschema vorgetäuscht werden.

## Tests

- Unitfixture mit mindestens drei Aufrufebenen und zwei Projekten.
- `depth=1` und `depth=3` liefern beide `structuredContent.callSites`.
- Alle Einträge enthalten korrekte Tiefe und Herkunft.
- Text und StructuredContent nennen dieselbe gezeigte Trefferzahl.
- `maxResults=1` setzt nur `truncatedByMaxResults`.
- künstlich erreichter Knoten-Cap setzt `truncatedByNodeLimit`.
- überhöhter Depth-Wert setzt `depthWasClamped` und den effektiven Hard-Cap.
- Wiederholte Aufrufe liefern byte-identische Reihenfolge im `structuredContent`.
- E2E-Raw-Wire-Test bestätigt, dass `structuredContent` ein JSON-Objekt bleibt.

## Definition of Done

- Kein `depth > 1`-Pfad in `find_references` oder symbolbasiertem `get_impact` fällt auf Nur-Text zurück.
- Traversierung und Formatierung sind getrennt.
- Vollständigkeit und jeder Trunkierungsgrund sind strukturiert sichtbar.
- Bestehende Textclients bleiben kompatibel.
- Dokumentation und Tool-Descriptions sind aktualisiert.
- `dotnet build` sowie beide Nicht-Stress-Testprojekte sind grün.

