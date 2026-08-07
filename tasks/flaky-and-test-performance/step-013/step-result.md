---
status: done
type: step-result
task: flaky-and-test-performance
step: 013
epic: EPIC-02
step_type: batch
coded_by: coder
coded_by_model: claude-sonnet-5
coded_by_model_knowledge_cutoff: 2026-01
coded_at: 2026-08-07T16:15:00+02:00
code_commit_hash: 0d5cee2c293d19488acf196bf276ffbc483e4380
status_after: done
blocker_category: n/a
---

# Result Step 013: Category-Traits für Mcp/Tools/ nachziehen + TD-007

## Zusammenfassung

Alle 17 Testklassen in `src/AiNetLinter.Tests/Mcp/Tools/` haben jetzt
`[Trait("Category", "Unit")]` auf Klassen-Ebene — 15× Standard-Insert
(CRLF), 2× XML-Doc-Variante (CRLF: `SafeguardScannerTests`,
`SafeguardToolTests`), 2× XML-Doc-Variante mit byte-genauem
Python-Insert für LF-only-Dateien (`GetServerHealthToolTests`,
`ReloadConfigToolTests`). Helper ohne Facts
(`DiRegistrationMiniFixtureWorkspace`, String-Literal-Klassen in
`SafeguardScannerTests`) blieben unangetastet. TD-007 (Hilfsdateien
aus step-012) ist noch offen für den Doku-Commit dieses Steps.

## Geänderte Dateien

- item-01: `src/AiNetLinter.Tests/Mcp/Tools/CallGraphTraversalTests.cs` — Trait vor Klasse
- item-02: `.../DiRegistrationHeuristicsTests.cs` — Trait vor Klasse; Helper `DiRegistrationMiniFixtureWorkspace` unangetastet
- item-03: `.../FindReferencesToolTests.cs` — Trait vor Klasse
- item-04: `.../FindSymbolScannerTests.cs` — Trait vor Klasse
- item-05: `.../FindSymbolToolTests.cs` — Trait vor dual-Fixture-Klasse
- item-06: `.../GetFileSkeletonToolTests.cs` — Trait vor Klasse
- item-07: `.../GetHotspotsToolTests.cs` — Trait vor Klasse
- item-08: `.../GetImpactToolTests.cs` — Trait vor Klasse
- item-09: `.../GetIndexScopeToolTests.cs` — Trait vor Klasse
- item-10: `.../GetServerHealthToolTests.cs` — XML-Doc-Variante, LF-only byte-genau via Python-Skript
- item-11: `.../GetSymbolBodyToolTests.cs` — Trait vor Klasse
- item-12: `.../GetTypeHierarchyToolTests.cs` — Trait vor Klasse
- item-13: `.../GetViolationsToolTests.cs` — Trait vor Klasse
- item-14: `.../ReloadConfigToolTests.cs` — XML-Doc-Variante, LF-only byte-genau via Python-Skript
- item-15: `.../SafeguardScannerTests.cs` — XML-Doc-Variante; String-Literal-Klassen (Greeter/A/B/C/D/Giant) unangetastet
- item-16: `.../SafeguardToolTests.cs` — XML-Doc-Variante
- item-17: `.../SearchPatternToolTests.cs` — Trait vor Klasse
- item-18: TD-007 — `tasks/flaky-and-test-performance/step-012/_insert_trait_skeleton.py` + `_code_commit_msg.txt` werden im Doku-Commit gelöscht

## Commit

- **Code-Commit-Hash:** `0d5cee2c293d19488acf196bf276ffbc483e4380`
- **Message:**
  ```
  test: Mcp/Tools-Tests Kategorie-taggen [flaky-and-test-performance]

  Alle 17 Testklassen in src/AiNetLinter.Tests/Mcp/Tools/ erhalten
  [Trait("Category", "Unit")] auf Klassenebene (in-process
  SymbolGraphCatalogFixture/BaselineCatalogFixture bzw. Mini-Fixture,
  kein Subprozess). Helper ohne Facts (DiRegistrationMiniFixtureWorkspace,
  String-Literal-Klassen in SafeguardScannerTests) bleiben ungetaggt.
  EOL/BOM unveraendert (2 LF-only-Dateien byte-genau via Python-Skript).

  Refs: tasks/flaky-and-test-performance/step-013
  ```
- **Branch:** main
- **Push:** nein (lokal)
- **Doku-Commit:** separater, zweiter Commit (Hash steht nicht hier drin)

## Build-/Test-Output

```
dotnet build                                → grün (0 Warnungen, 0 Fehler)
dotnet run … --config rules.json --path .   → OK
dotnet test --filter "Category=Unit"        → grün (1130 Tests, 0 Fehler)
dotnet test --filter "Category=Integration" → grün (113 Tests, 0 Fehler)
dotnet test (Voll)                          → 1. Lauf: 1 Fehler (bekannter EPIC-06-Flaky
                                                McpServerCommandLoadingStateTests…, siehe unten)
                                               2. Lauf: grün (1325 Tests, 0 Fehler)
```

Der 1. Voll-Lauf zeigte genau 1 Fehlschlag:
`AiNetLinter.Tests.Commands.McpServerCommandLoadingStateTests.LoadState_LoadFuncCompletesSynchronouslyWithCatalog_ReportsLoadedImmediately`
(`Assert.Equal() Failure: Expected: Loaded, Actual: Loading`) — exakt
der in „Bekannte Ausnahmen" des Step-Plans benannte EPIC-06-Flaky, in
`Commands/`, außerhalb des step-013-Scopes (`Mcp/Tools/`), von diesem
Step nicht berührt. Klassifikation nach Schritt 4a: kein
Infrastruktur-Blocker, sondern bekannter, dokumentierter Flaky —
1 Wiederholung genügte für Grün (2/3 Versuchsbudget ungenutzt).

## Abweichungen vom Plan

Keine — Plan 1:1 umgesetzt. Alle 18 Items exakt wie in `step-plan.md`
beschrieben (Zeilen, Varianten, EOL-Behandlung stimmten 1:1 mit dem
vorgefundenen Code überein).

## Beobachtungen

- Der Voll-Testlauf ist mit dem bekannten EPIC-06-Flaky nicht 100%ig
  deterministisch grün (1 von 2 Läufen in diesem Step rot) — bestätigt
  den in `codemap.md` und Step-Plan bereits dokumentierten Zustand,
  keine neue Erkenntnis, nur zusätzlicher empirischer Beleg für den
  Kritiker/Planer.
- Sonst nichts Scope-Fremdes aufgefallen; die 15 Standard-Insert-Dateien
  waren tatsächlich strukturell identisch zueinander (gleiches Muster
  `namespace …;\n\npublic sealed class …`), was die Batch-Homogenität
  aus dem Plan bestätigt.

## Bekannte Unschärfen

Keine — EOL/BOM/Trait-Count wurden für alle 17 Dateien byte-genau via
PowerShell verifiziert (siehe Tabelle unten), Fact-Zahlen wurden nicht
erneut einzeln nachgezählt (Plan-Angabe 146 attr-`[Fact]` als korrekt
übernommen, da Unit-Filter-Delta exakt +146 = 984→1130 stimmt).

## EOL/BOM-Verifikation (17 Dateien, vorher/nachher)

| Datei | CR vorher/nachher | LF vorher/nachher | BOM | TraitCount |
|---|---|---|---|---|
| CallGraphTraversalTests.cs | 57/58 | 57/58 | nein | 1 |
| DiRegistrationHeuristicsTests.cs | 171/172 | 171/172 | nein | 1 |
| FindReferencesToolTests.cs | 229/230 | 229/230 | nein | 1 |
| FindSymbolScannerTests.cs | 84/85 | 84/85 | nein | 1 |
| FindSymbolToolTests.cs | 179/180 | 179/180 | nein | 1 |
| GetFileSkeletonToolTests.cs | 97/98 | 97/98 | nein | 1 |
| GetHotspotsToolTests.cs | 124/125 | 124/125 | nein | 1 |
| GetImpactToolTests.cs | 192/193 | 192/193 | nein | 1 |
| GetIndexScopeToolTests.cs | 132/133 | 132/133 | nein | 1 |
| GetServerHealthToolTests.cs (LF-only) | 0/0 | 130/131 | nein | 1 |
| GetSymbolBodyToolTests.cs | 110/111 | 110/111 | nein | 1 |
| GetTypeHierarchyToolTests.cs | 181/182 | 181/182 | nein | 1 |
| GetViolationsToolTests.cs | 212/213 | 212/213 | nein | 1 |
| ReloadConfigToolTests.cs (LF-only) | 0/0 | 162/163 | nein | 1 |
| SafeguardScannerTests.cs | 480/481 | 480/481 | nein | 1 |
| SafeguardToolTests.cs | 181/182 | 181/182 | nein | 1 |
| SearchPatternToolTests.cs | 180/181 | 180/181 | nein | 1 |

Filter-Delta: Unit 984 → 1130 (+146), Integration 113 (±0), Total
1325 (±0) — exakt wie im Plan prognostiziert.
