---
status: ready
execution_mode: autonomous
open_questions: []
---

# Agentenwert und Tokenökonomie der AiNetLinter-MCP-Antworten

## Übergabe und Fortsetzung

Bei einer Fortsetzung nach einem Modell- oder Agentenwechsel zuerst
[`Handover.md`](Handover.md) vollständig lesen. Die Datei beschreibt den
aktuellen Git-/Arbeitsbaumstand, offene Slices, Verifikationslücken und die
wiederholt korrigierten Risikostellen. Sie ergänzt dieses Konzept und ersetzt
weder den Umsetzungsvertrag noch die Projektregeln.

## Umsetzungsvertrag

AiNetLinter erhält einen einzigen kompakten, agentengerechten MCP-Vertrag:
kanonische IDs stehen ausschließlich im normalisierten StructuredContent,
fachliche Evidenz wird vor Metadaten geschützt, große Mengen werden semantisch
geordnet und budgetiert, und alle betroffenen Responseformen werden atomar auf
den Zielzustand umgestellt.

### Verbindlicher harter Schnitt

- Es gibt genau ein gültiges Handoff-ID-Format und eine StructuredContent-Form.
- Es gibt keine Parallelform, keinen Feature-Schalter, keinen
  Kompatibilitätsmodus, keine Dual-Read-/Dual-Write-Phase und keine
  Deprecation-Schicht.
- Der Parser akzeptiert ausschließlich das hier definierte Format.
- Entfernte Felder bleiben weder als Alias noch als leerer Platzhalter erhalten.
- Interne Konsumenten, Tests, Guides und Produktdokumentation werden im selben
  Task umgestellt.
- Produktdokumentation beschreibt ausschließlich den danach gültigen Zustand:
  keine Versionsgeschichte, kein Vergleich, keine Umstellungsanleitung und
  keine Beispiele eines anderen Vertrags.

Ein anders geformter Input erhält `INVALID_ARGUMENT` am konkreten Feld. Das ist
bewusst kein Migrationsfall.

## Ziel

Ein programmierender Agent soll bei großen .NET-Solutions möglichst viel
belastbare semantische Erkenntnis pro verbrauchtem Kontext erhalten. Optimiert
wird auf **semantische Dichte**, nicht auf die kleinste Antwort:

- Kurz ist gut, wenn die Agentenfrage damit entschieden ist.
- Groß ist gut, wenn zusätzliche Bytes neue handlungsrelevante Evidenz liefern.
- IDs, Maschinenbuchhaltung, identische Metadaten, redundante Teilbäume und
  folgenlose Statussätze dürfen den Modellkontext nicht dominieren.
- Trunkierung darf konkrete Erkenntnis nicht zugunsten von Zählern oder
  Navigationsfeldern verdrängen.

Die Roslyn-Stärken – Symbolidentität, Call-/Typgraphen, Testzuordnung, Metriken,
Lint und Assembly-Herkunft – sollen dadurch häufiger besser und kontextärmer
nutzbar sein als Textsuche plus vollständiges Dateilesen.

## Belegte Probleme

Der 360°-Audit an ungefähr 180 kLoC zeigt:

1. IDs wiederholen reversibel kodierten Targetpfad, vollständigen SHA-256 und
   DocumentationCommentId in Text und StructuredContent.
2. Treffer/Graphknoten wiederholen Target, Snapshot, Handoff-Markierung und
   Folge-Tool-Listen.
3. Erfolgsantworten tragen einen langen Navigationsfooter und oft einen
   inhaltsgleichen Sufficiency-Satz.
4. Composite-Budgetierung kürzt nach Größe statt Erkenntniswert; Feature- und
   Testkontext können zu Counts ohne konkreten Caller/Test werden.
5. `get_call_tree` serialisiert einen Graph als rekursiven Baum. Diamonds,
   Zyklen, Overrides und Tests vervielfachen Inhalt; ein Auditcall hatte 114 KB.
6. C#-Listen/Graphen haben keinen gemeinsamen Production-/Test-/Generated-
   Scope und kein konsistentes Relevanzranking.
7. Generierte Dokumente können vor editierbaren Quellen stehen; echte
   Partial-Deklarationen erscheinen als getrennte Symbole.
8. Eine Testklassen-Namenskonvention kann alle Methoden der Klasse als Tests
   eines einzelnen Members ausgeben.
9. Leer- und Statusfälle sind lang oder nicht disjunkt; Populationen aus
   Dateien, Roslyn-Dokumenten, Generated und Tests sind unklar benannt.
10. Server-Instructions/Toolbeschreibungen wiederholen Informationen; eine
    Workflowregel nennt ein nicht existentes AiNetLinter-MCP-Tool.
11. Exakter Namespace, Source-Origin, Kindfilter und Assemblysuche liefern
    teilweise nicht die für den nächsten Agentenschritt relevante Form.

Pauschal kleinere Limits lösen das nicht. Auswahl, Reihenfolge, Projektion und
Budgetierung müssen fachlichen Wert kennen.

## Auditabdeckung und bewusste Grenzen

Diese Matrix ist für die autonome Umsetzung verbindlich. Sie verhindert, dass
ein ausführendes Modell den Task auf die IDs reduziert oder unbewertete
Auditbeobachtungen eigenmächtig zu neuen Features ausweitet.

| Auditfinding | Entscheidung | Umsetzung/Nachweis |
|---|---|---|
| A1 – IDs im Markdown und pro Entry | vollständig beheben | Slices 01–07; AK 1–10 |
| A2 – Compositebudget schützt Bytes statt Erkenntnis | vollständig beheben | Slices 15–17; AK 11–17 |
| A3 – Incoming-DAG als duplizierender Baum | vollständig beheben | Slices 13–14; AK 24–27 |
| A4 – Testcode dominiert ohne Scope | vollständig beheben | Slices 08–11; AK 18–23 |
| B1 – Ranking/Completeness leicht falsch lesbar | vollständig beheben | Slices 09, 10, 19 |
| B2 – leere/statusbezogene Antworten unpräzise | vollständig beheben | Slice 18; AK 29–31 |
| B3 – unterschiedliche Zählpopulationen | vollständig beheben | Slice 18; AK 31 |
| B4 – Namespace-, Origin- und DI-Asymmetrien | vollständig beheben | Slices 10 und 19 |
| C1 – Workflow nennt falsches Tool/überverspricht MCP | vollständig beheben | Slices 20–21; AK 34–35 |
| Schema-/Discovery-Kosten im Serveranteil | messen und serverseitig reduzieren | Slice 20; AK 32–33 |
| Client serialisiert StructuredContent zusätzlich | serverseitig durch Entduplizierung abmildern | Rootnormalisierung; Clientänderung bleibt Non-Goal |
| Cursor verlangt zusätzliche Callbeschreibung | keine Serveränderung | clientseitiges Verhalten außerhalb des Repositories |
| `search_pattern` verliert gegen gezieltes `rg` | keine Semantikänderung | Workflow dokumentiert Textsuche als legitime lokale Wahl |
| Eigene Source ist besser als dekompilierte eigene DLL | keine Produktänderung | korrektes Routing in Integration-/Workflowdoku |
| Trunkierung/Completeness als Prinzip | erhalten, Auswahl davor verbessern | alle Budget-Slices |
| Typ als Call-Graph-Seed | kleine UX-Korrektur, kein Typgraph-Feature | Slice 14; AK 27 |
| Dünne Overview-Resource | nicht aufblasen | Detailwissen bleibt in Agent-API-Resource und Toolschema |
| `Release/` als großer File-Tree-Treffer | keinen Verzeichnisnamen hardcodieren | Population/Ausschlüsse erklären; vorhandene `excludePatterns` im Workflow zeigen |

Zusätzlich zum fremden Audit behebt das Konzept die bei der Verifikation
reproduzierten Folgeprobleme: redundanter Navigationsfooter, generierte
Discovery-Treffer, ungruppierte Partial-Symbole, zu breite Member-Testzuordnung
und unpräzise Source-Origin-Locations. Diese Punkte sind keine optionale
Ausweitung, sondern direkte Ursachen derselben Agenten- und Tokeneffizienz-
Probleme.

Die Auditklassen A, B und C werden damit vollständig abgearbeitet. Beobachtete
korrekte Trade-offs bleiben erhalten. Rein clientseitige Kosten werden nicht
durch serverseitigen Zustand oder Scheinautomatismen kompensiert.

## Leitprinzipien

1. `content` ist die knappe Agentenantwort mit Befund und Belegen;
   `structuredContent` ist der maschinenlesbare Handoff-/Statusvertrag. Beide
   stammen aus demselben Domainergebnis, duplizieren aber nicht blind Daten.
2. Erst ranken, dann vollständige semantische Einheiten auswählen, daraus Text
   und StructuredContent rendern, zuletzt kombinierte UTF-8-Bytes prüfen.
3. Reihenfolge: Matchqualität, direkte vor heuristischen Beziehungen,
   editierbare Production, notwendige Generated-Brücken, direkte Tests,
   heuristische Tests, sonstiges Generated/Unknown.
4. Scopefilter laufen vor `maxResults`, Traversal und Bytebudget. Counts sagen
   explizit, welche Population sie messen.
5. `complete`, `empty`, `partial`, `truncated`, Scope und nötiger Folgeschritt
   bleiben ehrlich; vollständiger Erfolg braucht keinen Textfooter.
6. Kanonische Handoffs bleiben ohne Sessionzustand über Eviction/Neustart
   gültig. Kurze Graph-NodeIds gelten nur innerhalb einer Response.

## Scope und Source of Truth

### Produktion

- Ergebnis-, Navigation-, Status- und Wireprojektion unter
  `src/AiNetLinter/Mcp/`.
- `AnalysisSymbolIdentity`, `SymbolIdentifierResolver` und Assembly-Handoffs.
- `find_symbol`, `find_references`, `get_call_tree`,
  `find_implementations`, `get_type_hierarchy`, `dependency_graph`,
  `get_feature_context`, `get_test_context`, `get_file_skeleton`,
  `get_symbol_body`, `get_namespace_tree`, `resolve_type_origin`.
- Leer-/Statusfälle von `get_impact`, `find_magic_values`, `pattern_detect`,
  `reload_config`, `get_index_scope`, `get_file_tree`.
- Toolregistrierungen, Server-Instructions, Resources und Agent-Guides.

### Tests und Fixtures

- FastTests für Parser, Resolver, Projektionen, Scope, Ranking, Generated,
  Partial, Testevidenz, Graph und Budgets.
- IntegrationTests für MCP-Wirevertrag, `tools/list`, Source-/Assembly-
  Handoffs, Eviction/Neustart und Dogfood.
- Kleine synthetische Fixture mit Hot-Symbol, Diamond, Zyklus,
  Virtual/Override, Partial, Generated und gemischten Production-/Testcallern.
- Keine privaten Auditprojektpfade oder gespeicherten Rawausgaben in Tests.

### Fachliche Quellen

- Roslyn `Solution`, `Project`, `Document`, `ISymbol`,
  `SymbolEqualityComparer` und vorhandene Analysemodelle.
- `AiNetLinter.Core.TestDetector` als Testklassifikation.
- Ein gemeinsamer MCP-Dokumentklassifizierer für Generated/Editierbar.
- Navigation-Envelope als Ziel-/Snapshotquelle.
- Handoff-ID ausschließlich in StructuredContent als Folge-Call-Identität.
- `tools/list`, `Docs/agent-api.md` und `Docs/integration.md` als öffentlicher
  Vertrag. Audit-MDs bleiben nur Task-Evidenz.

## Verbindlicher Zielvertrag

### Kanonische Handoff-ID

```text
s:<target-token>:<content-token>:<documentation-comment-id>
a:<target-token>:<content-token>:<documentation-comment-id>
```

- `s` = Source-Solution, `a` = Assembly.
- `target-token`: erste 128 Bit SHA-256 über den normalisierten kanonischen
  absoluten Targetpfad, Base64url ohne Padding, exakt 22 Zeichen.
- `content-token`: erste 128 Bit des Source-Snapshot-/Assembly-Contenthashes,
  Base64url ohne Padding, exakt 22 Zeichen.
- DocumentationCommentId: Roslyn-ID mit `M:`, `T:`, `P:`, `F:`, `E:` oder `!:`.
- Fester Overhead vor der Doc-ID: 48 ASCII-Zeichen.
- Kein Targetpfad und kein vollständiger Hash sind rekonstruierbar.

Neue Dateien/Namespace:

```text
src/AiNetLinter/Mcp/Handoffs/SymbolHandoffIdentifier.cs
src/AiNetLinter/Mcp/Handoffs/SymbolHandoffToken.cs
namespace AiNetLinter.Mcp.Handoffs;
```

```csharp
internal enum SymbolHandoffOrigin { Source, Assembly }

internal readonly record struct SymbolHandoffIdentifier(
    SymbolHandoffOrigin Origin,
    string TargetToken,
    string ContentToken,
    string DocumentationCommentId)
{
    internal const int TokenBytes = 16;
    internal const int EncodedTokenLength = 22;
}
```

`AnalysisSymbolIdentity` behält vollständige Runtime-Daten (`CanonicalPath`,
voller ContentHash, Generation); `FormatHandoff` delegiert an das Wiremodell.
Der Resolver arbeitet strikt:

1. Format prüfen → sonst `INVALID_ARGUMENT`, `$.symbolIdentifier`.
2. Origin und Targettoken prüfen → sonst `TARGET_MISMATCH`.
3. Contenttoken prüfen → sonst `STALE_SNAPSHOT`.
4. Doc-ID per Roslyn auflösen → Null/Mehrdeutigkeit explizit, nie First-Match.

Der Parser akzeptiert nur `s:`/`a:`, zwei valide 22-Zeichen-Tokens und den
gesamten Rest als kanonische Doc-ID. Whitespace, Padding, leere Segmente,
Local-Function-Marker und unbekannte Präfixe sind ungültig. Fehlertexte echoen
lange Eingaben nicht vollständig.

Dependency-freier Encoder:

```csharp
private static string Encode128(ReadOnlySpan<byte> hash) =>
    Convert.ToBase64String(hash[..16])
        .TrimEnd('=')
        .Replace('+', '-')
        .Replace('/', '_');
```

### Normalisierter Navigation-Envelope

Jede zielgebundene StructuredContent-Response besitzt einen Rootknoten:

```json
{
  "navigation": {
    "contractVersion": 1,
    "target": {
      "targetPath": "C:/repo/App.slnx",
      "analysisRoot": "C:/repo",
      "origin": "source"
    },
    "snapshot": {
      "fingerprint": "<full-server-fingerprint>",
      "kind": "source",
      "fresh": true
    },
    "status": {
      "operation": "ok",
      "completeness": "complete",
      "code": null
    },
    "scope": { "requestedType": "all", "includeGenerated": false },
    "next": null,
    "handoff": {
      "acceptedAs": "symbolIdentifier",
      "followUpsByKind": {
        "type": ["get_type_hierarchy", "find_implementations"],
        "member": ["get_symbol_body", "find_references", "get_call_tree"]
      }
    }
  }
}
```

- `contractVersion` ist konstant `1`; Produktcode kennt keine zweite Form.
- Origin liegt in `target`, nicht zusätzlich daneben.
- Tautologische `result.available`-/`capabilities`-Felder entfallen.
- `next=null`, wenn kein Schritt nötig ist.
- Scope/Handoff fehlen nur, wenn sachlich nicht anwendbar.
- Folge-Tools stehen einmal nach `handoffKind`, nicht pro Entry.
- Ein abweichendes Assembly-`originTarget` ist nur am betroffenen Entry erlaubt.

Empfohlenes Modell in `McpNavigationProjection.cs`:

```csharp
internal sealed record McpNavigationPayload(
    int ContractVersion,
    McpNavigationTarget Target,
    McpNavigationSnapshot Snapshot,
    McpNavigationStatus Status,
    McpNavigationScope? Scope,
    McpNavigationNext? Next,
    McpHandoffContract? Handoff);
```

### Symbolentry und Text

```json
{
  "id": "s:...:M:Example.Service.Run",
  "handoffKind": "member",
  "displayName": "Example.Service.Run()",
  "kind": "method",
  "signature": "void Run()",
  "locations": [{
    "project": "Example",
    "path": "src/Example/Service.cs",
    "line": 42,
    "column": 5,
    "scopeType": "production",
    "sourceKind": "editable"
  }]
}
```

Entries enthalten kein Target, Snapshot, `handoff=true` oder identische
Toollisten. Partial-Symbole gruppieren alle Locations. Unterschiedliche
Symbole mit gleichem Namen bleiben getrennt.

Kanonische IDs erscheinen in keiner regulären Textantwort, auch nicht in
Mehrdeutigkeit, Skeleton, Body, Graph oder Assemblytext. Textbeispiel:

```text
method Example.Service.Run() — src/Example/Service.cs:42
```

`McpNavigationText.Format` liefert bei `ok + complete` leer, bei
empty/partial/truncated/recoverable genau eine entscheidungsrelevante Zeile und
höchstens eine Aktion. `WithNavigation` hängt nur nichtleeren Text an.
`McpSufficiencyHints.CompleteDataHint` wird gelöscht.

### Gemeinsamer Scope und Generated-Klassifikation

Neue Typen unter `src/AiNetLinter/Mcp/Scope/`, Namespace
`AiNetLinter.Mcp.Scope`:

```csharp
internal enum McpScopeType { All, Production, Tests }
internal enum McpProjectKind { Production, Tests, Unknown }
internal enum McpSourceKind { Editable, Generated }
internal readonly record struct McpDocumentScope(
    McpProjectKind ProjectKind, McpSourceKind SourceKind);
```

`McpScopeClassifier` ist die einzige MCP-weite Quelle:

- Projekt/Testdatei delegiert an `TestDetector`.
- Generated prüft `/obj/`, `.g.cs`, `.g.i.cs`, `.generated.cs`,
  `.designer.cs`, `<auto-generated`-Header und GeneratedCode-Merkmale.
- Cachekey ist Solution-Snapshot plus `DocumentId`, nie global nur Pfad.
- Unsicher bleibt `Unknown` und ist bei `all` sichtbar.

Parameter aller betroffenen Tools:

```text
scopeType: all | production | tests     Default all
includeGenerated: false | true         Default false
```

`includeGenerated=false` entfernt keine semantisch notwendige Graphkante;
Generated-Brücken bleiben kompakt markiert. Reihenfolge: Domainmenge →
klassifizieren → Scope filtern → gruppieren → ranken → Treffer/Traversal
begrenzen → Bytebudget.

### Stabiles Ranking

```csharp
(matchRank, relationshipRank, projectRank, sourceRank,
 relativePath, line, canonicalDocumentationId)
```

Niedriger ist besser:

- Match: exact 0, qualified exact 1, prefix 2, substring 3, fuzzy/wildcard 4.
- Beziehung: direct 0, explicit mapping 1, strong name 2, type use 3,
  convention 4, unknown 5.
- Projekt: production 0, tests 1, unknown 2.
- Quelle: editable 0, generated 1.

Toolfachlichkeit darf ergänzen, aber Scope/Generated nicht neu definieren.

### Präzise Testevidenz

```csharp
internal enum TestEvidenceKind
{
    DirectInvocation = 0,
    ExplicitMemberCoverage = 1,
    MemberNameMatch = 2,
    DirectTypeUse = 3,
    ExplicitTypeCoverage = 4,
    TypeNamingConvention = 5,
}
```

- Konkrete `methods` nur für Stufen 0–2.
- Reine Typkonvention: Projekt, Datei/Klasse, `totalTestCount`, low confidence,
  aber keine behaupteten Membermethoden.
- StructuredContent enthält `evidenceKind` und `confidence`.
- Testfilter sind nie enger als die Evidenz: Memberfilter nur bei konkreter
  Methode, Klassenfilter bei Typkonvention.

### Call-Graph statt rekursivem Baum

Zielmodels in `Mcp/Tools/CallTree/GetCallTreeModels.cs`:

```csharp
internal sealed record CallGraphPayload(
    string RootNodeId,
    string Direction,
    int RequestedDepth,
    int EffectiveDepth,
    IReadOnlyList<CallGraphNode> Nodes,
    IReadOnlyList<CallGraphEdge> Edges,
    CallGraphCounts Counts,
    McpCompleteness Completeness);

internal sealed record CallGraphNode(
    string NodeId, SymbolLocationEntry Symbol,
    bool IsExternal, bool IsGeneratedBridge);

internal sealed record CallGraphEdge(
    string FromNodeId, string ToNodeId, string DispatchKind,
    IReadOnlyList<CallSiteLocation> CallSites);
```

- Antwortlokale, deterministische IDs `n1`, `n2`, ... nach stabiler Sortierung.
- Knotenschlüssel: Origin + kanonische Doc-ID; für nichtkanonische externe
  Knoten zusätzlich Assemblyidentität.
- Ein Node und eine gerichtete Kante jeweils einmal; mehrere Call-Sites werden
  an der Kante gruppiert.
- Virtual/Interface/Override/Dispatch bleiben unterscheidbar.
- `both` darf Nodes teilen, nie Kantenrichtung verlieren.
- ASCII, Mermaid und StructuredContent verwenden dasselbe Payload.
- Text enthält NodeId, Kurzsignatur, Scope, Location – keine Handoff-ID.
- Typseed ist recoverable ungültig und liefert strukturiert bis zu fünf
  gerankte aufrufbare Methoden desselben Typs.

### Tool-spezifische Bytebudgets

Zentrale Grenzen: Minimum `512`, Maximum `65_536` kombinierte UTF-8-Bytes.

| Tool | Default |
|---|---:|
| `find_symbol` | 16.384 |
| `find_references` | 24.576 |
| `get_call_tree` | 32.768 |
| `find_implementations` | 16.384 |
| `get_type_hierarchy` | 24.576 |
| `dependency_graph` | 24.576 |
| `get_feature_context` | 32.768 |
| `get_test_context` | 24.576 |
| `get_file_skeleton` | 24.576 |
| `get_symbol_body` | 32.768 |

Für diese Tools bedeutet `0` öffentlich nicht „unbegrenzt“. Registration,
Inputmodelle und Implementierung verwenden dieselben Konstanten.

```text
full domain result
 -> stable ranking
 -> typed budget projection
 -> text + structured from same projection
 -> navigation
 -> combined UTF-8 measurement
 -> success or explicit minimum-budget error
```

Die generische JSON-Mutation in `McpToolResults.Composite*` wird gelöscht.
Neue toolnahe Projektoren:

```text
FeatureContextResponseBudget.cs
TestContextResponseBudget.cs
CallGraphResponseBudget.cs
```

Ein gemeinsamer `McpResponseSize` misst nur und trifft keine Fachauswahl.

Feature-Mindestprojektion: Deklaration; direkte Violations/Nullbefund;
Grenzwertüberschreitungen; ein Production-Caller; ein direkter Testkandidat;
Gesamt-/Shown-Counts, Scope und Trunkierungsgrund; danach weitere Evidenz.

Test-Mindestprojektion: bis zu drei höchstwertige konkrete Kandidaten;
Typkonventionskandidaten erst danach.

Passt Envelope plus kleinstes fachliches Element nicht, kommt recoverable
`RESPONSE_BUDGET_TOO_SMALL`, `fieldPath=$.maxResponseBytes` und der berechnete
Mindestwert. Counts-only als scheinbarer Erfolg ist verboten.

### Status-, Leer- und Zählersemantik

`get_impact.impactStatus` ist exakt einer von:

```text
not_git_repository
invalid_ref
clean_worktree
diff_without_callsite_impact
impact_found
```

Leere Magic-/Pattern-Audits: `completeness=empty`, Scope/Ausschlüsse,
`totalCount=0`, `shownCount=0`, eine Textzeile, keine Remediation.

`reload_config` trennt `enabledRuleCheckCount`,
`effectiveMetricThresholdCount`, `snapshotChanged`; Fingerprints nur
strukturiert.

Index-/Dateipopulationen verwenden nur passende benannte Felder:

```text
physicalFileCount, roslynDocumentCount, generatedDocumentCount,
testDocumentCount, excludedCount, shownCount
```

Nicht anwendbare Counts fehlen oder sind `null`.

`resolve_type_origin`: getrennte `targetPath`, `projectName`,
`sourceLocations[]`, `assemblyOrigin`, `outputAssembly`.

Exakter Namespace-Prefix wird selbst Root; direkte Typen sind unabhängig von
`childNamespaces`. Ein Kindfilter bindet `complete` an den Filter; andere
Arten stehen nur unter `alternatives`, nie in Treffern/ShownCount.

### Discovery und Dokumentation

Server-Instructions enthalten nur Targetprinzip, C#-Semantik versus
Textsuche, Status/Scope/Completeness/Next und einen Verweis auf die
Agent-API-Resource; maximal 1.200 UTF-8-Bytes.

Toolbeschreibungen enthalten nur Agentenfrage, überraschende Grenzen/Defaults
und Zielarten. Schemafelder werden nicht nochmals als Fließtext wiederholt.

Geänderte Produktdokumente enthalten nur kanonische ID, normalisierte
Responseform, aktuelle Scope-/Ranking-/Budget-/Graph-/Statussemantik und
aktuelle Beispiele. Verboten sind historische Wirebeispiele,
Versionsvergleiche und Formulierungen wie „früher“, „bisher“, „vormals“,
„weiterhin unterstützt“ oder „deprecated“.

## Muss-Kriterien

1. Exakt das kompakte stateless Handoffformat; kein alternativer Parser.
2. Keine Handoff-ID im Agententext; lange Fehleingaben werden nicht geechot.
3. Rootnormalisierung; Entries ohne gemeinsame Metadaten;
   `contractVersion=1` als einzige Responseform.
4. Kein Erfolgsfooter/Complete-Sufficiency-Satz; knappe andere Statuszeilen.
5. Typisierte Mindestprojektionen für Feature-/Testkontext.
6. Alle Budgettools halten kombinierte Bytes; mehr Budget liefert Fachinhalt.
7. Call-Graph mit eindeutigen Nodes/Edges und lokalen Referenzen.
8. Ein gemeinsamer Scope-/Ranking-/Generated-/Partial-Vertrag.
9. Testkandidaten mit Evidence/Confidence; Typkonvention ohne Memberbehauptung.
10. Disjunkte Status-, Leer-, Origin-, Namespace- und Populationssemantik.
11. Schlanke Discovery und reine Ist-Dokumentation.
12. Ersetzte Parser, DTO-Felder, Formatter, Budgettrimmer, Tests und Beispiele
    werden gelöscht; keine toten Adapter oder auskommentierten Gegenstücke.

## Akzeptanzkriterien

1. IDs haben exakt zwei 22-Zeichen-Base64url-Tokens und 48 Zeichen Overhead.
2. Kein Pfad/voller Hash ist dekodierbar.
3. Handoff funktioniert nach Eviction/Neustart bei gleichem Inhalt.
4. Falsches Target → `TARGET_MISMATCH`; anderer Inhalt → `STALE_SNAPSHOT`.
5. Jede andere Handoffform → `INVALID_ARGUMENT`; kein Alternativparser.
6. Keine reguläre Textantwort der betroffenen Tools enthält eine Handoff-ID.
7. Zehn Entries serialisieren Target, Snapshot und Toolmengen je höchstens 1×.
8. Source/Assembly/Erfolg/Leer/Truncated/Fehler nutzen denselben Envelope v1.
9. Entfernte Entryfelder fehlen vollständig statt `null`/leer zu bleiben.
10. `ok + complete` hat keinen Navigationsfooter/Snapshot/`next:none` im Text.
11. Hot-Featurekontext behält Deklaration, Violation/Nullbefund, einen
    Production-Caller, direkten Test und korrekte Counts.
12. Testkontext behält bis zu drei konkrete Topkandidaten.
13. Kein erfolgreicher Counts-only-Fallback bei vorhandener Evidenz.
14. Undarstellbares Minimum → `RESPONSE_BUDGET_TOO_SMALL` samt Mindestwert.
15. Text-UTF8 + Structured-UTF8 bleibt nach Navigation innerhalb des Budgets.
16. Mehr Budget fügt vollständige fachliche Einheiten hinzu.
17. Generische größenbasierte Composite-JSON-Mutation ist gelöscht.
18. `all` zeigt bei gleicher Matchqualität Production vor Tests.
19. Production-/Testscope sind disjunkt; Invalid → `$.scopeType`.
20. Kein betroffenes Tool besitzt eigene Testprojekt-Heuristik.
21. Generated steht nicht unmarkiert vor Editable; Opt-in zeigt es markiert.
22. Partial erscheint als ein Symbol mit vollständigen Locations.
23. Exact steht in Source-/Assemblysuche vor Substring.
24. Diamond/Zyklus/Virtual/Override/Testgraph hat jeden Node einmal, alle Edges.
25. Graphgröße wächst mit eindeutigen Nodes/Edges statt Pfadkopien.
26. ASCII/Mermaid/Structured verwenden dieselbe Node-/Edge-Menge.
27. Typseed liefert Zielarthinweis plus bis zu fünf relevante Methoden.
28. Klassenkonvention behauptet nicht alle Testmethoden als Membertests.
29. Alle fünf Impactstatuswerte haben isolierte Tests.
30. Leere Magic-/Pattern-Audits enthalten keine Remediation.
31. Reload/Index/File/Origin/Namespace/Kindfilter erfüllen die Zielpayloads.
32. Server-Instructions sind höchstens 1.200 UTF-8-Bytes.
33. Toolbeschreibungssumme ist mindestens 30 % und die vollständig
    serialisierte servereigene `tools/list`-Response mindestens 20 % kleiner
    als die in Slice 20 vor Änderung gemessene Größe, ohne wesentliche
    Wahlinformation oder Schemavalidierung zu verlieren.
34. Workflow nennt nur echte AiNetLinter-Tools und keine pauschale Tokenregel.
35. Produktdokumentation zeigt nur aktuelle Requests/Responses.
36. Produktionscode enthält keine ersetzten Prefixe/Felder/Kompatibilitätswege.
37. Symbolbody, outgoing Graph, Implementierungen, direkte Testzuordnung,
    Lint/Metriken und Assemblysuche behalten relevante Evidenz.

## Fehler, Fallback, Ownership und Lebenszeit

- Handoffs sind nicht sessiongebunden; Snapshotwechsel macht sie stale.
- Graph-NodeIds werden nie als `symbolIdentifier` akzeptiert.
- Kürzung nur an vollständigen semantischen Einheiten, nie mitten in Strings.
- `totalCount` misst die gefilterte Vollmenge, `shownCount` die Projektion.
- Gefiltertes Empty ist keine globale Nichtexistenzaussage.
- `Unknown` bleibt in `all` sichtbar.
- `SymbolHandoffIdentifier`: Wireformat/Parser.
- `AnalysisSymbolIdentity`: Runtime-Target und volle Hashes.
- `SymbolIdentifierResolver`: Target/Snapshot/Roslyn-Auflösung.
- `McpNavigationProjection`: Rootvertrag; `McpNavigationText`: Textstatus.
- `McpScopeClassifier`: Production/Test/Unknown und Editable/Generated.
- Scanner: vollständiges Domainergebnis; Budgetprojektor: fachliche Auswahl;
  Formatter: reine Darstellung; `McpResponseSize`: reine Messung.

## Architektur- und Betriebsannahmen

- Kein neuer DI-Container, Pluginmechanismus oder dynamischer Responsevertrag.
- `targetPath` bleibt Pflicht für zielgebundene Calls.
- 128 Bit Hashraum genügen lokal; Mehrdeutigkeit wird nie First-Match.
- UTF-8-Bytes sind Servereinheit, keine modellunabhängige Tokenzusage.
- Text und StructuredContent können beide Modellkontext verbrauchen.
- `scopeType=all` bleibt Default; Ranking verhindert Testdominanz.
- Stress-Tests nur auf ausdrückliche Anforderung.

## Serieller Umsetzungsplan

Jeder Slice lädt nur diese `Konzept.md`, den genannten Lesescope und direkte
Abhängigkeiten. So bleibt er deutlich unter 256k Kontext. Immer: Arbeitsbaum
prüfen → Red-Test → kleinster Codeumfang → enger Test grün →
`git diff --check`/Diffprüfung → tote ersetzte Strukturen löschen. Erst danach
der nächste Slice. Compilebedingte Kleinstverschiebungen sind erlaubt;
fachliche Slices werden nicht zusammengelegt.

### Slice 01 – Handoff-Wiredomäne

**Lesen:** `AnalysisSymbolIdentity.cs`, `SymbolIdentifierResolver.cs`,
`SymbolIdentifierResolverTests.cs`, direkte Assembly-ID-Tests.

**Bauen:** `Mcp/Handoffs/*`; exaktes Format/Token/Parser; Formatdelegation;
Pfad-Encode/-Decode und andere Prefixkonstanten löschen.

**Rot/Exit:** Source/Assembly deterministisch; 22 Zeichen; kein Pfad;
Invalidpräfix/-token/-docid/local function feldgenau abgelehnt; Domänentests
grün und Produktcode kennt nur `s:`/`a:`.

### Slice 02 – Source-Resolver und Lebenszeit

**Lesen:** Slice-01-Dateien, Source-Registry-/Leasepfad, Resolver-
IntegrationTests.

**Bauen:** Format→Origin→Targettoken→Contenttoken→Roslyn; kurzes Fehlecho;
stateless Eviction/Reload; keine First-Match-Auflösung.

**Rot/Exit:** Eviction, neuer Prozess, Target-Mismatch, stale, null,
mehrdeutig, kein langes Echo; Source-Handofftests grün; keine Aliasmap.

### Slice 03 – Assembly-Resolver

**Lesen:** `Mcp/Assemblies/`, relevante `AnalysisToolCall.cs`-Pfade,
Assembly-Dispatcher-/Handofftests.

**Bauen:** nur `a:`; explizites Assemblytarget/content prüfen; abweichendes
`originTarget` einmal; doppelte Formatter/Parser löschen.

**Rot/Exit:** Eviction/Neustart, geänderte/falsche DLL, exact vor substring,
Cross-Assembly-Follow-up; alle Assembly-Handoffs nutzen dieselbe Wiredomäne.

### Slice 04 – Navigation-Envelope

**Lesen:** `McpNavigationProjection*.cs`, `McpNavigationText.cs`,
`McpToolResults.cs`, Navigation-/Contracttests.

**Bauen:** finales Modell v1; Origin in Target; tautologische Felder weg;
nullable Next, optional Scope/Handoff; alle Producer atomar umstellen.

**Rot/Exit:** exakte Source-/Assembly-JSON-Shape; `next=null`; Felder nur bei
Anwendbarkeit; Rootmetadaten 1×; entfernte Felder fehlen; Tests grün.

### Slice 05 – Textnavigation/Sufficiency

**Lesen:** Navigation/Results, `McpSufficiencyHints.cs`, alle Aufrufer/Tests.

**Bauen:** Erfolg → leer; sonst knapper Einzeiler; `WithNavigation` ohne
Leerzeilen; CompleteDataHint samt toten Aufrufern löschen.

**Rot/Exit:** Erfolg ohne Footer, Empty mit Scope, Truncated mit einer Aktion,
kein ID-Echo, Text/Structured statusgleich; Resulttests grün.

### Slice 06 – IDs aus Symbolsuche/Body/Skeleton-Text

**Lesen:** `FindSymbolTool.cs`, SymbolBody-, FileSkeleton-Tool/Formatter/Tests.

**Bauen:** Kurzsignatur+Location; ID strukturiert; Mehrdeutigkeit ohne sichtbare
ID wählbar; Entry-Metadaten entfernen, `handoffKind` ergänzen.

**Rot/Exit:** kein ID-Präfix im Text; Body/Skeleton vollständig;
Mehrdeutigkeit unterscheidbar; zehn Entries ohne Rootduplikate; Tests grün.

### Slice 07 – IDs aus References/Hierarchy/Implementations

**Lesen:** Source-/Assembly-FindReferences, `GetTypeHierarchy*`,
`FindImplementations*`, Models/Formatter/Tests.

**Bauen:** schlanke Entries; IDs nur strukturiert; Rootmetadaten einmal;
abweichendes Assembly-Origin nur wo nötig.

**Rot/Exit:** Text ohne ID; Structured-Follow-up funktioniert; keine
per-entry Toolliste; Fachsemantik unverändert; Gruppen grün.

### Slice 08 – Scope-/Dokumentklassifizierer

**Lesen:** `TestDetector.cs`/Tests, `GeneratedCodeDetector.cs`, vorhandene
Search-/Hotspots-Scopes als Referenz.

**Bauen:** `Mcp/Scope/*`; TestDetector-Delegation; Generated-Kriterien;
Snapshot+DocumentId-Cache; Scopevalidator.

**Rot/Exit:** Production/Test/Mixed/Unknown, obj/endings/header/attribute,
normale Datei, drei Scopewerte, Snapshotwechsel; neue Scope-FastTests grün.

### Slice 09 – `find_symbol` Scope/Ranking/Partial

**Lesen:** `FindSymbolTool.cs`, `SymbolGraphToolRegistrations.cs`, Tests, Scope.

**Bauen:** Parameter/Defaultbudget; Scope vor maxResults; Ranking; Gruppierung
via SymbolEqualityComparer/OriginalDefinition; Generated zählen/Opt-in;
Kindalternativen getrennt.

**Rot/Exit:** exact, Production-first, disjunkte Scopes, Partial=1 Entry,
Generated-Opt-in, filtergebundenes Complete; Tooltests/tools-list grün.

### Slice 10 – Scope für References/Hierarchy/Implementations

**Lesen:** Slice-07-Gruppen, Registrations, Scope, jeweilige Tests.

**Bauen:** Parameter/Defaults; Filter vor Limit; Scopecounts/Ordnung; lokale
Testheuristiken löschen.

**Rot/Exit:** pro Tool all/production/tests/invalid, Production-first,
Generated, monotones höheres Limit; Produktions-DI-Registrierungen vor
Testsetup-Registrierungen; Tests grün; keine lokale Testdetektion.

### Slice 11 – Dependency-Graph Scope

**Lesen:** `Mcp/Tools/DependencyGraph/*`, Registration, beide Testklassen.

**Bauen:** Scope/Generated/Defaultbudget; Knoten klassifizieren; Ausschlüsse
zählen; notwendige Generated-Brücken erhalten.

**Rot/Exit:** Mixed-Graph, disjunkte Scopes, Generated-Brücke, ganze
Node-/Edge-Kürzung; Tests grün.

### Slice 12 – Testevidenz

**Lesen:** `Core/TestCoverageScanner.cs`, `Mcp/Tools/TestContext/*`, Tests.

**Bauen:** EvidenceKind/Confidence; Typkonvention ohne Methodenliste; stabile
Ordnung; Filterempfehlung evidenzgerecht.

**Rot/Exit:** direct=high/method, membername=medium/method,
type-convention=low/class/count/no methods, 25-Test-Fall, deterministische
Filter; Coverage-/TestContext-Tests grün.

### Slice 13 – Call-Graph-Domäne

**Lesen:** `Mcp/Tools/CallTree/*`, `CallGraphTreeBuilder.cs`,
`CallGraphTraversal.cs`, Graphtests.

**Bauen:** Nodes/Edges statt rekursivem Wirebaum; stabile NodeIds;
CallSites gruppieren; visited Node-/Edge-Keys; DispatchKind.

**Rot/Exit:** Diamond, Zyklus, self-call, Virtual/Override/Interface,
Node einmal, Multi-Callsite-Edge, deterministische IDs; kein rekursives Payload.

### Slice 14 – Call-Graph Renderer/Scope/Budget

**Lesen:** Slice-13-Dateien, Mermaidrenderer, Registration, Tooltests.

**Bauen:** ASCII/Mermaid aus Graphpayload; Text ohne IDs; Scope vor Fan-out;
`CallGraphResponseBudget`; Typseed-Hinweis+Methoden.

**Rot/Exit:** Rendererparität, 32-KiB-Default, höheres Budget erweitert ganze
Einheiten, Type-Seed, Richtungen/Scopes; gesamte CallTree-Suite grün.

### Slice 15 – Featurekontext-Budget

**Lesen:** `Mcp/Tools/FeatureContext/*`, Composite-Wiredateien,
Wiremessung, Feature-/Cap-Tests.

**Bauen:** `FeatureContextResponseBudget`; feste Mindestordnung; Caller über
gemeinsames Ranking; beide Projektionen aus derselben Auswahl; Post-Navigation.

**Rot/Exit:** Hot-Symbol, Production-Caller+direkter Test bleiben, korrekte
Counts, kein Counts-only, Mindestfehler, monotones größeres Budget; Tool ruft
keinen generischen JSON-Trimmer mehr.

### Slice 16 – Testkontext-Budget/Composite-Löschung

**Lesen:** `Mcp/Tools/TestContext/*`, Composite-Wiredateien,
`McpToolResults.cs`, Result-/TestContexttests.

**Bauen:** `TestContextResponseBudget`, drei konkrete Kandidaten schützen;
`McpResponseSize`; generische Apply-/LargestArray-/ID-Key-Mutation samt Tests
vollständig löschen.

**Rot/Exit:** drei Kandidaten, Convention verdrängt Direct nicht, kein
Counts-only, Post-Navigation-Bytes, keine Stringkürzung; kein
`TryTrimLargestSection` im Produktcode.

### Slice 17 – Feste Budgets der übrigen Symboltools

**Lesen:** restliche Budgettabellentools, `McpArgumentValidationFilter.cs`,
Registrations, Budgettests.

**Bauen:** zentrale Defaults/Min/Max; öffentlich kein 0; Listen an Entries,
Body/Skeleton an vollständigen Einheiten; Post-Navigation messen.

**Rot/Exit:** Default, 511, 512, 65536, 65537; kombinierte Bytes; größeres
Budget erweitert Inhalt; Registration=Implementierung; alle Gruppen grün.

### Slice 18 – Status/Leer/Populationen

**Lesen:** GetImpact+Integrationtests, FindMagicValues+Tests,
PatternDetect+Tests, ReloadConfig Models/Tests, IndexScope/FileTree+Tests.

**Bauen:** fünf Impactstatus; Empty ohne Remediation; klare Reloadcounts;
benannte Datei-/Dokumentpopulationen; knappe Texte. Beim File-Tree keine
projektspezifischen Verzeichnisnamen als Defaultausschluss einbauen;
`excludePatterns` und tatsächlich angewandte Ausschlüsse strukturiert zeigen.

**Rot/Exit:** jeder Status isoliert, Emptyshape, Reload changed/unchanged,
Physical/Roslyn/Generated/Test, Text/Structured-Konsistenz; Tests grün.

### Slice 19 – Origin/Namespace/Kindalternativen

**Lesen:** ResolveTypeOrigin Models/Tests, GetNamespaceTree+Tests,
FindSymbol-Kindtests.

**Bauen:** Target/Projekt/SourceLocations/Assembly getrennt; exakter Namespace
als Root; direkte Typen unabhängig von Children; Alternatives separat.

**Rot/Exit:** Source 1/n Locations, Assembly, Leaf-Namespace, Empty-Namespace,
Kind-Nulltreffer+Alternative; Tests/Dogfood 6–7 grün.

### Slice 20 – Instructions/Toolbeschreibungen

**Lesen:** `ServerInstructions.cs`, Registrations, Agent-Guide/Overview,
tools-list-/Instructiontests.

**Bauen:** Vor der Änderung UTF-8-Bytes von Instructions, allen
Toolbeschreibungen, allen Inputschemas und der vollständig serialisierten
servereigenen `tools/list`-Response getrennt erfassen; Instructions auf vier
Aussagen reduzieren; Tooltexte auf Frage/Grenzen/Zielarten reduzieren;
wiederholte Target-/Statusverträge aus Beschreibungen entfernen; Schemas final
abgleichen. Keine Feldvalidierung nur zur Größenreduktion löschen.

**Rot/Exit:** <=1200 Bytes; Beschreibungssumme -30 %; vollständiges
servereigenes tools-list -20 %; kein globales Textduplikat; finale
Parameter/Defaults/Felder in tools-list; Tests grün.

### Slice 21 – Produktdokumentation nur Endzustand

**Lesen:** `Docs/agent-api.md`, `Docs/integration.md`, relevante
`Docs/configuration.md`-Abschnitte, MCP-Workflowregel, eingebettete Guides.

**Bauen:** ausschließlich finale ID/Envelope/Scope/Budget/Graph/Statusbeispiele;
nicht existentes Tool durch neutralen lokalen Datei-/Zeilenleser ersetzen;
keine pauschale MCP-Tokenüberlegenheit; kein Historienabschnitt.

**Exit:** Beispiele stimmen mit tools-list/Contracttests; gezielte Suche nach
historischen Wireformen/Versionsvergleichen in Produktdocs leer;
Markdown-/Resource-Tests grün.

### Slice 22 – Bereinigung, Dogfood, Auditor, Release

**Lesen:** vollständiger eigener Diff, alle Kriterien, Dogfoodszenarien.

**Bauen/Prüfen:** ersetzte Prefixe/Felder/Parser/Formatter/Trimmer/Models und
auskommentierten Code entfernen; `auditor` einmal für DRY, Drift, Dead Code,
Magic Values; Findings beheben; Dogfood; vollständige Gates; nur eigene
Dateien committen.

**Exit:** 37 Kriterien nachgewiesen, kein Auditor-Finding, Release grün,
deutscher imperativer Conventional Commit.

## Verifikation

Response-Tests messen Textbytes, StructuredContent-Bytes, Summe, Total/Shown,
eindeutige IDs, Vorkommen gemeinsamer Metadaten, Graphnodes/-edges und konkrete
versus heuristische Testkandidaten.

### Dogfood auf `AiNetLinter.slnx`

1. `find_symbol("*TestCoverageScanner*")`: Editable Production vor Tests;
   Generated markiert/Opt-in; Text ohne ID.
2. `get_symbol_body`: Bodies/Kurzsignaturen ohne ID-Echo/Erfolgsfooter.
3. `get_feature_context(AnalysisSymbolIdentity)`: konkrete Production- und
   Testevidenz unter Defaultbudget.
4. `get_test_context(FindSymbolTool.FormatEntry)`: nicht alle Klassenmethoden
   als konkrete Membertests.
5. Incoming `get_call_tree(McpNavigationText.Format)`: Production/Test
   getrennt, Symbole einmal, <=32.768 Bytes.
6. Exakter Namespace `AiNetLinter.Mcp.Tools.TestContext`: Root und direkte Typen.
7. `resolve_type_origin(AnalysisSymbolIdentity)`: Target, Projekt, Quelldateien.
8. Source-/Assembly-Handoff nach frischem Serverstart ohne Aliaszustand.
9. `tools/list`: ausschließlich finale Parameter, Defaults, Beschreibungen.

### Release-Gate

```powershell
dotnet build
dotnet test src/AiNetLinter.FastTests --filter Category!=Stress
dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress
git diff --check
```

Danach Diff und alle Kriterien prüfen, keine fremden Änderungen stagen und nur
auftragsbezogene Dateien deutsch-imperativ nach Conventional Commits committen.

## Dokumentationsänderungen

- `Docs/agent-api.md`: ID, Envelope, Entry, Scope, Ranking, Budget, Graph, Status.
- `Docs/integration.md`: Toolwahl, Chaining, Scope-/Budgetbeispiele.
- `Docs/configuration.md`: nur dort tatsächlich verortete MCP-Verträge.
- `.agents/rules/AiNetLinter-McpWorkflow.mdc`: echte Tools und aktuelle
  Scope-/Budget-/Handoff-Regeln.
- Eingebettete Guides/Resources und Smoke-Tests.

`README` bleibt außerhalb. Audit-MDs bleiben Task-Evidenz, nicht Produktdoku.

## Risiken und Gegenmaßnahmen

- **Brechender Vertrag:** repositoryweite Suche, exakte JSON-/tools-list-/E2E-
  Tests und atomare Aktualisierung. Jeder Restfund blockiert Release.
- **128-Bit-Token:** praktisch ausreichend; Target und Content getrennt prüfen,
  nie First-Match.
- **Zu aggressive Filter:** `all` bleibt Default; getrennte Counts;
  Generated-Brücken bleiben markiert erhalten.
- **Projektordrift:** gemeinsam nur Validierung/Messung, Fachauswahl toolnah;
  Invariantentests sichern jedes Minimum.
- **Viele Slices:** mehr serielle Läufe, aber kleine Kontext-/Fehlerflächen.
  Compilebedingte Kleinstverschiebung erlaubt, kein übersprungenes Exit.
- **Clientunterschiede:** Text und StructuredContent serverseitig messen und
  beide entduplizieren; nicht auf clientseitiges Ausblenden verlassen.

## Verworfene Alternativen

- Sessionlokale Kurzaliase: unklare Lebenszeit/Ownership; nur als lokale
  Graph-NodeIds zulässig.
- Nur IDs im Text entfernen: StructuredContent bleibt teuer.
- Budgets pauschal erhöhen: mehr Wiederholung ohne Relevanzgarantie.
- Global 10 KB: kann wertvollen Sourcebody/Caller abschneiden.
- Tests standardmäßig ausblenden: vor Änderungen oft entscheidend.
- Nur DocumentationCommentId: keine Target-/Snapshotbindung.
- Generische JSON-Kürzung: sieht Größe, nicht Agentenwert.

## Non-Goals

- Allgemeiner Ersatz für Textsuche, Glob, Git oder gezieltes Dateilesen.
- Externe MCP-Clients/Promptserialisierung ändern.
- Modellunabhängiges exaktes Tokenbudget versprechen.
- Alle Tools auf denselben Text/Defaultbytewert zwingen.
- Roslyn-, Lint-, Metrik- oder Decompilationfachlichkeit außerhalb der Findings
  neu entwerfen.
- Aliasdatenbank/Sessionzustand einführen.
- Stressläufe automatisch ausführen.
- Auditdateien als Runtime-Fixture/Produktdoku verwenden.
- `README` ändern.

## Offene Entscheidungen

Keine. Der Orchestrator darf lokale Namen und kleine Dateizuschnitte an
bestehende Konventionen anpassen, solange Wireform, Semantik und Exitkriterien
unverändert bleiben.

## Abschlussbedingung

Abgeschlossen ist der Task erst nach allen 12 Muss-Kriterien, 37
Akzeptanzkriterien, 22 Slices, reiner Endzustandsdokumentation, grünen
Nicht-Stress-Gates und einem Auditor-Ergebnis ohne offene auftragsbezogene
Findings. Einzelne Tokenoptimierungen oder ein verbleibender alternativer
Vertragsweg reichen nicht.
