---
status: done
type: step-result
task: codegraph-mcp
step: 004
epic: EPIC-03
step_type: single
coded_by: coder
coded_by_model: claude-sonnet-5
coded_by_model_knowledge_cutoff: 2026-01
coded_at: 2026-07-31T12:30:00Z
code_commit_hash: a9e91ed
status_after: done (pending audit)
blocker_category: n/a
---

# Result Step 004: find_references Tool (Symbol- und Positions-Aufloesung + Aufrufstellen)

## Zusammenfassung

Alle zehn im Plan beschriebenen Dateien umgesetzt: `find_references` loest
einen Symbol-Identifikator (`Datei:Zeile:Spalte` oder qualifizierter/
teil-qualifizierter Name) zu genau einem Roslyn-`ISymbol` auf und liefert
dessen Aufrufstellen ueber die bereits bestehende
`DiffImpactAnalyzer.FindCallSitesAsync` — kein Neubau der Referenzsuche.
Zwei neue Fehlercodes (`SYMBOL_NOT_FOUND`, `AMBIGUOUS_SYMBOL`) plus
zugehoerige `McpToolResults`-Bausteine, Registrierung als zweites Tool in
`McpServerOptionsFactory`, neue `SymbolGraphMini`-Fixture (drei Klassen,
siehe Abweichungen) mit isoliertem `SymbolGraphMiniFixtureWorkspace`, und
Anpassung des bestehenden E2E-Tests an zwei registrierte Tools plus ein
neuer E2E-Test fuer `find_references`.

Ein echter, waehrend der Umsetzung entdeckter Defekt musste zusaetzlich
behoben werden, um die Definition-of-Done (Selbst-Lint 0 Violations) zu
erfuellen — siehe „Abweichungen vom Plan".

## Geänderte Dateien

- `src/AiNetLinter/Core/DiffImpactAnalyzer.cs` — `FindDocumentByPath`/`FindCallSitesAsync` `private` → `internal`, Xml-Doc ergaenzt.
- `src/AiNetLinter/Mcp/Tools/FindSymbolTool.cs` — `FormatSymbolLocations` `private` → `internal`, Xml-Doc ergaenzt.
- `src/AiNetLinter/Output/LinterErrorCodes.cs` — `SymbolNotFound`/`AmbiguousSymbol` ergaenzt.
- `src/AiNetLinter/Mcp/McpToolResults.cs` — `SymbolNotFound(identifier)`/`AmbiguousSymbol(identifier, candidateLines)` ergaenzt.
- `src/AiNetLinter/Mcp/Tools/FindReferencesTool.cs` (neu) — `ExecuteAsync`/`ResolveSymbolAsync`/`ResolveByPositionAsync`/`ResolveByNameAsync`.
- `src/AiNetLinter/Mcp/Tools/SymbolIdentifierResolver.cs` (neu, nicht im Plan) — `ResolveSymbolAtToken`/`TryParsePosition`/`StripParameterList`, siehe Abweichungen.
- `src/AiNetLinter/Mcp/McpServerOptionsFactory.cs` — zweiter `tools.Add(...)`-Aufruf fuer `find_references`.
- `tests/Fixtures/SymbolGraphMini/` (neu) — `.slnx`, `.csproj`, `Greeter.cs`, `Caller.cs`, `OtherCaller.cs` (dritte Klasse, siehe Abweichungen).
- `src/AiNetLinter.Tests/Fixtures/SymbolGraphMiniFixtureWorkspace.cs` (neu) — 1:1-Analogon zu `BaselineMiniFixtureWorkspace`.
- `src/AiNetLinter.Tests/Mcp/Tools/FindReferencesToolTests.cs` (neu) — sechs Tests gemaess Plan-Testliste.
- `src/AiNetLinter.Tests/Commands/McpServerCommandTests.cs` — Test umbenannt/angepasst (`RunAsync_ValidFixture_ServerRespondsWithBothTools`), neuer E2E-Test `RunAsync_ValidFixture_FindReferencesReturnsCallSite`.

## Commit

- **Code-Commit-Hash:** `a9e91ed`
- **Message:**
  ```
  feat(mcp): add find_references tool for call-site lookup [codegraph-mcp]

  Resolves a symbol identifier (file:line:column or qualified name) to a
  Roslyn ISymbol and reuses DiffImpactAnalyzer.FindCallSitesAsync to list
  call sites. Adds SYMBOL_NOT_FOUND/AMBIGUOUS_SYMBOL error codes, a new
  SymbolGraphMini fixture with a real cross-file call site, and extracts
  small parsing helpers into SymbolIdentifierResolver to keep
  FindReferencesTool under the AIContextFootprint limit.

  Refs: tasks/codegraph-mcp/step-004
  ```
- **Branch:** main
- **Push:** nein (lokal)
- **Doku-Commit:** separater, zweiter Commit (Hash steht nicht hier drin).

## Build-/Test-Output

```
dotnet build AiNetLinter.slnx → gruen, 0 Warnungen
dotnet test AiNetLinter.slnx  → gruen (1043 Tests, 0 Fehler)
ainetlinter --config rules.json --path . → OK, 0 Violations
```

## Abweichungen vom Plan

1. **Neue Datei `SymbolIdentifierResolver.cs` (nicht im Plan, echter
   Fix noetig fuer DoD):** Nach Erst-Implementierung von
   `FindReferencesTool.cs` exakt nach Code-Skizze meldete der
   Pflicht-Selbst-Lint (DoD) `AIContextFootprint` fuer
   `FindReferencesTool` selbst: `2515 > 2500` (Top-Deps:
   `MetricsConfig`, `GlobalConfigOverride`, `MetricsConfigOverride`,
   alle 350-400 Zeilen). Ursache: `ExecuteAsync(McpCodeGraphServer
   state, ...)` traegt `McpCodeGraphServer` als Parametertyp in der
   Signatur — dieselbe Abhaengigkeit hat auch `FindSymbolTool.ExecuteAsync`
   bereits (`McpCodeGraphServer` → `SourceFileCatalog` → Config-Klassen),
   dort aber unter dem Limit. `FindReferencesTool.cs` ist mit den vier
   zusaetzlichen privaten Hilfsmethoden (`TryParsePosition`,
   `ResolveSymbolAtToken`, `StripParameterList`, `ResolveByPositionAsync`/
   `ResolveByNameAsync`) rund 45 Zeilen laenger als `FindSymbolTool.cs` —
   die **eigene Dateilaenge** zaehlt vollstaendig zum Footprint der
   Klasse (nicht nur Signaturen), daher reichten 15 Zeilen ueber dem
   Limit fuer den Ausschlag. TD-004 im Plan hatte exakt diese Kategorie
   Footprint-Risiko schon fuer `McpServerOptionsFactory` durchdacht (mit
   dokumentierter Ausweich-Option: kleinere, benannte private Methoden
   statt neuer Datei) — hier traf es aber `FindReferencesTool` selbst,
   nicht `McpServerOptionsFactory` (das blieb wie erwartet unauffaellig).
   Fix: die drei reinen Parsing-/Token-Aufloesungs-Helfer
   (`TryParsePosition`, `StripParameterList`, `ResolveSymbolAtToken` —
   keine davon in einer `FindReferencesTool`-Membersignatur referenziert,
   nur in Methodenkoerpern aufgerufen) in eine eigene Datei
   `SymbolIdentifierResolver.cs` ausgelagert. Das reduziert
   `FindReferencesTool.cs` um genau die noetigen Zeilen (Selbst-Lint
   danach: `OK`, 0 Violations) ohne Verhaltensaenderung — reine
   Datei-Organisation, keine neue Abstraktionsebene (kein Interface, kein
   DI), analog zur im Plan selbst skizzierten "kleinere, benannte
   Methoden"-Ausweichoption, nur auf Datei- statt Methodenebene
   angewendet, weil die Methoden bereits klein und fokussiert waren.
2. **`SymbolGraphMini`-Fixture mit drei statt zwei Klassen:** Plan
   skizzierte nur `Greeter`/`Caller`. Fuer den Testfall
   `ResolveSymbolAsync_AmbiguousSimpleName_ReturnsAmbiguousSymbolError`
   sah der Plan selbst vor: "sonst gezielt einen zweiten Typ mit
   gleichnamiger Methode in SymbolGraphMini ergaenzen" — `OtherCaller.cs`
   mit einer zweiten `Run()`-Methode ergaenzt, damit der Identifikator
   `"Run"` echt zwei Kandidaten liefert statt die Mehrdeutigkeit nur zu
   unterstellen.
3. **Test-Identifikator `<GreeterPath>:5:19` statt `:3:19`:** Der Plan
   nannte als Beispiel-Position `3:19` fuer die `Greet`-Deklaration; in
   der tatsaechlich geschriebenen Fixture-Datei (mit Leerzeile zwischen
   `namespace`- und `class`-Zeile, wie im Plan-Codeblock selbst gezeigt)
   liegt `Greet` auf Zeile 5, nicht 3 (Spalte 19 stimmte). Testwert an
   die tatsaechliche Datei angepasst statt den Plan-Wert blind zu
   uebernehmen.

## Beobachtungen

- Keine weiteren, ueber das oben Dokumentierte hinausgehenden
  Beobachtungen. Der naechste EPIC-03-Step (`get_impact` o.ae.) sollte
  wie im Plan vermerkt `FindReferencesTool.ResolveSymbolAsync`
  wiederverwenden, falls er direkte Symbol-Identifikator-Eingabe
  braucht.

## Bekannte Unschärfen

- Wie im Plan unter „Bekannte Ausnahmen" dokumentiert: Die
  Positions-Parse-Heuristik und `ResolveSymbolAtToken` decken nicht
  jeden denkbaren Roslyn-Sonderfall ab (z. B. Cursor exakt auf einem
  Operator-Token). Kein Blocker fuer diesen Step, unveraendert gegenueber
  Plan-Einschaetzung.
