---
status: done
type: step-result
task: 03_get-impact-zum-diff-kontext-erweitern
step: 001
epic: EPIC-1
step_type: single
coded_by: coder
coded_by_model: stealth/ox-alpha
coded_by_model_knowledge_cutoff: unbekannt
coded_at: 2026-08-22T00:00:00+02:00
code_commit_hash: 232aec64
status_after: done
blocker_category: n/a
---

# Result Step 001: Traversierungs-Korrektur (EnqueueChildren) & Sufficiency-Hint-Parität

## Zusammenfassung

`CallGraphTraversal.EnqueueChildrenAsync` enqueued jetzt je Referenzlocation
das einschließende Aufrufer-Member (`GetEnclosingSymbol().NormalizeToOwningMember()`)
statt der referenzierten Definition — `depth > 1` liefert damit echte
mehrstufige Aufruferketten mit korrekter `Depth`/`ReachedFromSymbolId`.
Die Enclosing-Auflösung liegt als gemeinsamer Helper
(`ResolveEnclosingMemberAsync`, internal) vor und wird vom Tree-Pfad
mitgenutzt (DRY wie im Plan vorgesehen). `get_impact` hängt im Symbol-Branch
bei vollständigen Ergebnissen den Sufficiency-Hint exakt so an wie
`find_references`. Weil die Datei durch den Fix das MaxLineCount-Limit (500)
riss (erst das Integration-Gate/Dogfood zeigte es), ist der Tree-Pfad
unverändert in die neue Klasse `CallGraphTreeBuilder` ausgelagert.

## Geänderte Dateien

- `src/AiNetLinter/Mcp/Tools/SymbolGraph/CallGraphTraversal.cs` —
  Enqueue-Fix (async), gemeinsamer Helper `ResolveEnclosingMemberAsync`
  (jetzt internal), `MarkSeenAndEnqueue`-Parameter korrekt benannt;
  Tree-Pfad herausgelöst (s. u.), Klasse wieder 253 Zeilen.
- `src/AiNetLinter/Mcp/Tools/SymbolGraph/CallGraphTreeBuilder.cs` (neu) —
  unveränderter Umzug des Caller-Tree-Pfads (`BuildTreeAsync`, Konstanten,
  Gruppierungs-/Formatierhelfer) aus `CallGraphTraversal`; nutzt
  `ResolveEnclosingMemberAsync`/`FormatSymbolName` über Klassengrenze.
- `src/AiNetLinter/Mcp/Tools/SymbolGraph/GetImpactTool.cs` —
  Hint-Parität: `IsComplete(traversal)` → `McpSufficiencyHints.Append(body)`.
- `src/AiNetLinter/Mcp/Tools/SymbolGraph/CallGraphTraversalState.cs`,
  `src/AiNetLinter/Mcp/Tools/CallTree/GetCallTreeTool.cs`,
  `src/AiNetLinter/Mcp/Tools/CallTree/CallTreeMermaidRenderer.cs` — reine
  Folge-Anpassungen des Splits (Aufrufe/crefs auf `CallGraphTreeBuilder`).
- `src/AiNetLinter.FastTests/Mcp/Tools/CallTree/CallGraphTraversalTests.cs` —
  Review-Kommentare je Bestands-Test (s. u.), Stärkung des Depth2-Tests,
  neu: `ExpandAsync_Depth2_RealCallerChain_ResolvesBothLevels`;
  BuildTree*-Aufrufe auf `CallGraphTreeBuilder` umgestellt.
- `src/AiNetLinter.FastTests/Mcp/Tools/SymbolGraph/FindReferencesToolTests.cs`
  — neu: `ExecuteAsync_Depth2_RealCallerChain_ReturnsBothLevels` (Tool-Level).
- `src/AiNetLinter.FastTests/Mcp/Tools/SymbolGraph/GetImpactToolTests.cs` —
  neu: Symbol-Branch-Kettentest + zwei Hint-Paritätstests
  (`...CompleteResult_AppendsSufficiencyHint` /
  `...TruncatedResult_OmitsSufficiencyHint`).

## Dokumentierte Review-Entscheidung je Bestands-Depth2-Test (Audit F)

| Test | Entscheidung | Begründung |
|:---|:---|:---|
| `ExpandAsync_Depth1_FormatsCallSiteFromCaller` | **bestätigt**, unverändert | Ebene 1 kommt direkt aus `FindReferencesAsync` des Startknotens — vom Enqueue-Defekt nie betroffen. |
| `ExpandAsync_Depth2_FormatsWithDepthMarker` | **gestärkt** (Name bewusst beibehalten) | Alte Assertion war nur `"Caller.cs"` im Text und kodifizierte weder Alt- noch Neuverhalten. Auf der Default-Fixture ruft niemand Run/RunTwice/RunThrice → jetzt explizit: alle Einträge `Depth == 1` (echter Kettenabschluss). Positive Kettenabdeckung liegt im neuen Ketten-Test; kein mechanisches Grünzwingen. |
| `ExpandAsync_DepthAboveCap_ClampsToThree` | **bestätigt**, unverändert | Clamp-Mechanik unabhängig vom Enqueue-Fix. |
| `ExpandAsync_NodeLimit_ReportsNodeTruncationSeparately` | **bestätigt**, nach Fix erneut verifiziert | Knoten-Cap bricht vor dem Besuch enqueuer Kinder ab; VisitedNodeCount/TruncatedByNodeLimit unverändert. |

## Commit

- **Code-Commit-Hash:** `232aec64`
- **Message:**
  ```
  fix: echte Aufruferketten bei depth>1 und Hint-Paritaet [03_get-impact-zum-diff-kontext-erweitern]

  EnqueueChildrenAsync enqueued bisher reference.Definition, ... (Body gekürzt)

  Refs: tasks/mcp-server-weiterentwicklung/03_get-impact-zum-diff-kontext-erweitern/step-001
  ```
- **Branch:** main
- **Push:** nein (lokal)
- **Doku-Commit:** separater, zweiter Commit (Hash steht nicht hier
  drin — Selbstbezug, siehe `git log`).

## Build-/Test-Output

```
dotnet build                                                          → grün (0 Warnungen, 0 Fehler)
dotnet test src/AiNetLinter.FastTests --filter Category!=Stress       → grün (1585 Tests, 0 Fehler)
dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress → grün (345 Tests, 0 Fehler)
```

Schnelliteration während der Entwicklung: `Category=Unit` 1097/1097,
`Category=Component` 488/488 grün.

## Abweichungen vom Plan

1. **Datei-Split `CallGraphTraversal` → `CallGraphTreeBuilder` (nicht im
   Plan):** Nach dem Enqueue-Fix überschritt `CallGraphTraversal.cs` das
   Zero-Warning-Limit `MaxLineCount` (500) — sichtbar erst am vollständigen
   Gate: die drei `CliRepositoryDogfoodTests` linten das eigene Repo und
   schlugen mit genau dieser Violation fehl. Umsetzung nach Vorgabe:
   Tree-Pfad (`BuildTreeAsync`, `MaxCallTree*`-Konstanten,
   Gruppierungs-/Formatierhelfer) 1:1 in neue Klasse
   `CallGraphTreeBuilder` (gleiches Verzeichnis/Namespace) ausgelagert;
   Verhalten unverändert; Folge-Anpassungen nur Aufrufstellen/XML-crefs
   (`GetCallTreeTool`, `CallTreeMermaidRenderer`, `CallGraphTraversalState`,
   Traversal-Tests). Der DRY-Helper `ResolveEnclosingMemberAsync` blieb als
   internal-Helper in `CallGraphTraversal` und wird cross-class genutzt —
   die im Plan skizzierte Konsolidierung ist damit umgesetzt.
2. **Ketten-Szenario dreimal identisch in drei Testdateien:** Der Plan sieht
   je Datei ein eigenes Ketten-Scenario vor (Datei 3/4/5) — umgesetzt wie
   geplant als Inline-Ad-hoc-Scenario via `CreateScenario(ProjectSpec)`, also
   bewusste (kleine) Quell-Duplikation in Tests statt neuer Fixture-
   Infrastruktur, wie in den Plan-Notes vorgegeben.
3. Sonst Plan 1:1 umgesetzt (keine Record-/Vertragsänderungen, Git-Branch,
   `GetImpactInput`, Docs unberührt).

## Beobachtungen

- `McpInMemoryTestContext.CreateScenario(...)` liefert ein
  `RoslynTestSolution` ohne `CreateServer()` — für Tool-Level-Tests braucht
  es zusätzlich den `McpInMemoryTestContext`-Wrapper
  (`new McpInMemoryTestContext(CreateScenario(...))`). Ein direktes
  `CreateServer()` auf dem Scenario-Ergebnis würde die Ergonomie verbessern;
  nicht behoben (außerhalb des Scopes, rein kosmetisch).
- Der Depth-Marker des Formatters (`"- transitiver Aufrufer"`) erscheint ab
  `effectiveDepth > 1` für ALLE Einträge inklusive Ebene 1 — der gestärkte
  Depth2-Test prüft die Ebenen deshalb über die strukturierten Einträge
  (`result.CallSites`), nicht über den Textmarker.
- `GetImpactToolTests.cs` trägt kein `#nullable enable` am Dateianfang
  (Bestand; Build/Linter beschweren sich nicht) — habe ich im Zuge meiner
  Testergänzungen bewusst nicht nachgezogen, um unrelated churn zu vermeiden.

## Bekannte Unschärfen

- `ExecuteAsync_Depth3_MultiProjectFixture_ReturnsStructuredEntriesWithOriginAndDepth`
  (Interface-Fixture) bleibt grün, aber die `Depth > 1`-Einträge entstehen
  jetzt über Enclosing-Aufrufer statt — wie vorher — über enqueute
  Override-/Interface-Definitionen. Das ist genau die beabsichtigte
  Verhaltenskorrektur (Audit B); der Bestandstest unterscheidet die beiden
  Ursachen nicht. Wer das isoliert belegen will, braucht ein Interface-Chain-
  Scenario mit expliziten Herkunfts-Assertions (EPIC-7-Doku nennt die
  Verhaltenskorrektur ohnehin).
- Roslyn-Kosten: pro Location ein `SemanticModel`-Zugriff (Workspace-gecacht),
  durch `MaxRecursionNodes = 200` gedeckelt — wie in den Plan-Notes
  einkalkuliert, nicht separat gemessen.
