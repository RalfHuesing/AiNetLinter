---
status: done
type: step-result
task: codegraph-mcp
step: 007
epic: EPIC-03
step_type: single
coded_by: coder
coded_by_model: claude-sonnet-5
coded_by_model_knowledge_cutoff: 2026-01
coded_at: 2026-07-31T16:00:00Z
code_commit_hash: e90b52c
status_after: done (pending audit)
blocker_category: n/a
---

# Result Step 007: get_type_hierarchy Tool (Basisklassen/abgeleitete Klassen/Interface-Implementierer via SymbolFinder)

## Zusammenfassung

Alle acht im Plan beschriebenen Dateien umgesetzt. `McpServerOptionsFactory`
ist jetzt ein duenner Dispatch, der nur noch
`SymbolGraphToolRegistrations.Register(tools, mcpState)` und
`FileStructureToolRegistrations.Register(tools, mcpState)` aufruft (Datei 1-3,
reine Verschiebung der vier bestehenden `tools.Add(...)`-Bloecke +
Neuregistrierung von `get_type_hierarchy`, keine Verhaltensaenderung an den
vier bestehenden Tools). `GetTypeHierarchyTool.ExecuteAsync` loest den
Typ-Identifikator ueber `FindReferencesTool.ResolveSymbolAsync` auf, prueft
`INamedTypeSymbol`, meldet sonst `INVALID_ARGUMENT`, und delegiert an
`GetTypeHierarchyFormatter.BuildHierarchyTextAsync`. Der Formatter laeuft die
`BaseType`-Kette (inkl. `System.Object`, bewusst nicht gefiltert),
`AllInterfaces` sowie — je nach `TypeKind` — `SymbolFinder.FindImplementationsAsync`
(Interface) bzw. `SymbolFinder.FindDerivedClassesAsync` (Klasse) ab und
formatiert jede Sektion ueber das bestehende `FindSymbolTool.FormatSymbolLocations`.
Kein neuer Fehlercode, kein neuer Identifikator-Parser.

## Geänderte Dateien

- `src/AiNetLinter/Mcp/McpServerOptionsFactory.cs` — `BuildToolCollection` auf zwei Registrar-Aufrufe reduziert, keine `tools.Add(...)`-Bloecke mehr inline.
- `src/AiNetLinter/Mcp/SymbolGraphToolRegistrations.cs` (neu) — `find_symbol`/`find_references`/`get_impact` (1:1 verschoben) + `get_type_hierarchy` (neu).
- `src/AiNetLinter/Mcp/FileStructureToolRegistrations.cs` (neu) — `get_file_skeleton` (1:1 verschoben).
- `src/AiNetLinter/Mcp/Tools/GetTypeHierarchyTool.cs` (neu) — `ExecuteAsync`.
- `src/AiNetLinter/Mcp/Tools/GetTypeHierarchyFormatter.cs` (neu) — `BuildHierarchyTextAsync` + private Traversierungs-/Formatierungs-Helfer.
- `tests/Fixtures/SymbolGraphMini/src/SymbolGraphMini/Hierarchy.cs` (neu) — Typ-Hierarchie-Fixture (Namen abweichend vom Plan, siehe „Abweichungen vom Plan").
- `src/AiNetLinter.Tests/Mcp/Tools/GetTypeHierarchyToolTests.cs` (neu) — sechs Tests gemaess Plan-Testliste.
- `src/AiNetLinter.Tests/Commands/McpServerCommandTests.cs` — Test umbenannt zu `RunAsync_ValidFixture_ServerRespondsWithFiveTools`, Assertion auf fuenf Tools inkl. `get_type_hierarchy` erweitert; neuer E2E-Test `RunAsync_ValidFixture_GetTypeHierarchyReturnsBaseGreetingHierarchy`.

## Commit

- **Code-Commit-Hash:** `e90b52c`
- **Message:**
  ```
  feat(mcp): add get_type_hierarchy tool [codegraph-mcp]

  Add the fifth and final EPIC-03 symbol-graph tool. It resolves a type
  identifier via FindReferencesTool.ResolveSymbolAsync, then reports base
  type chain, implemented interfaces, and derived classes / implementing
  types (SymbolFinder.FindDerivedClassesAsync / FindImplementationsAsync).
  Split McpServerOptionsFactory into SymbolGraphToolRegistrations and
  FileStructureToolRegistrations to keep its own AIContextFootprint under
  the 2500-line limit, proactively as recommended in the step-006 review.

  Refs: tasks/codegraph-mcp/step-007
  ```
- **Branch:** main
- **Push:** nein (lokal)
- **Doku-Commit:** separater, zweiter Commit (Hash steht nicht hier drin, siehe Orchestrator-Rueckmeldung).

## Build-/Test-Output

```
dotnet build AiNetLinter.slnx → gruen, 0 Warnungen
dotnet test AiNetLinter.slnx  → gruen (1063 Tests, 0 Fehler)
ainetlinter --config rules.json --path . → OK, 0 Violations
--footprint McpServerOptionsFactory        → 2437 (Limit 2500)
--footprint SymbolGraphToolRegistrations   → 2455 (Limit 2500)
--footprint FileStructureToolRegistrations → 2422 (Limit 2500)
--footprint GetTypeHierarchyTool           → 2423 (Limit 2500)
```

## Dogfooding

Gebautes `AiNetLinter.exe` per `StdioClientTransport` (identisches
Verbindungsmuster wie `McpServerCommandTests`) als `--mcp-server --path
C:\Daten\Entwicklung\Ralf\AiNetLinter` gestartet (echtes Repo-Root, keine
Fixture) und `get_type_hierarchy` per MCP-Client mit `typeIdentifier =
"ILintConsole"` aufgerufen (reales Interface aus `src/AiNetLinter/Output/ILintConsole.cs`,
per vorherigem `grep -rn ": ILintConsole"` als vier-Implementierer-Kandidat
identifiziert). Client-Code lag in einem Scratch-Projekt
(`ModelContextProtocol`-Client-Package via `ProjectReference` auf
`AiNetLinter.csproj`, nicht Teil des Repos, nicht committet).

Ergebnis: `IsError` leer/falsy, Antwort kam sofort. Text:

```
Basisklassen:
Keine Basisklasse.

Implementierte Interfaces:
Keine Interfaces.

Implementierende Typen:
src/AiNetLinter/Evals/EvalAssembler.cs:91 - Klasse: AiNetLinter.Evals.EvalAssembler.StringLintConsole
src/AiNetLinter/Output/LinterConsole.cs:7 - Klasse: AiNetLinter.Output.LinterConsole
src/AiNetLinter.Tests/Maps/TestLintConsole.cs:8 - Klasse: AiNetLinter.Tests.Maps.TestLintConsole
src/AiNetLinter.Tests/Output/TestLintConsole.cs:8 - Klasse: AiNetLinter.Tests.Output.TestLintConsole
```

Plausibel: `ILintConsole` ist ein reines Interface (korrekt "Keine
Basisklasse."/"Keine Interfaces." fuer die eigenen Basis-Sektionen), die
vier gemeldeten Implementierer decken sich exakt mit einem unabhaengigen
`grep -rn ": ILintConsole"` ueber das Repo (`EvalAssembler.StringLintConsole`,
`Output.LinterConsole`, zwei `TestLintConsole`-Klassen in
`AiNetLinter.Tests`). Da die MCP-Solution `AiNetLinter.slnx` beide Projekte
(`AiNetLinter` + `AiNetLinter.Tests`) umfasst, ist die Test-Implementierer
korrekt mit aufgelistet — keine Auffaelligkeit.

## Abweichungen vom Plan

- **Fixture-Namen (`Hierarchy.cs`):** Der Plan-Codeblock schlug
  `IGreeter`/`BaseGreeter`/`SpecialGreeter` mit Methode `Greet(string)` vor.
  Beim Testlauf (Schritt 4) brach das genau dadurch drei bereits
  bestehende, ausserhalb des Scopes liegende Tests
  (`FindReferencesToolTests.ResolveSymbolAsync_QualifiedName_ReturnsSingleMatch`,
  `FindReferencesToolTests.ExecuteAsync_ValidQualifiedName_ReturnsCallSiteInCaller`,
  `GetImpactToolTests.ExecuteAsync_SymbolIdentifierGiven_DelegatesToResolveSymbolAndReturnsCallSites`,
  `McpServerCommandTests.RunAsync_ValidFixture_FindReferencesReturnsCallSite`):
  diese rufen `FindReferencesTool.ResolveSymbolAsync`/`find_references` mit
  dem Identifikator `"Greeter.Greet"` auf und erwarten genau **ein**
  Ergebnis (`Greeter.Greet` aus der bestehenden `Greeter.cs`). Die
  Namensaufloesung in `ResolveByNameAsync` matcht per
  `ToDisplayString().EndsWith(identifier)` — sowohl `"IGreeter.Greet"` als
  auch `"BaseGreeter.Greet"` enden ebenfalls auf `"Greeter.Greet"` und
  wurden dadurch zusaetzliche Kandidaten, was die Aufloesung von
  eindeutig auf mehrdeutig (`AMBIGUOUS_SYMBOL`) kippte. Der Plan hatte nur
  die Zeilennummern-Kollision mit `Greeter.cs` antizipiert (deshalb die
  separate Fixture-Datei), nicht diese Namens-Suffix-Kollision. Fix: alle
  drei Typen in `Hierarchy.cs` von `*Greeter`/`IGreeter` auf
  `*Greeting`/`IGreeting` umbenannt (`IGreeting`, `BaseGreeting`,
  `SpecialGreeting`, Methode weiterhin `Greet(string)`) — Struktur
  (Interface + Basisklasse + abgeleitete Klasse, alle drei
  Hierarchie-Richtungen) bleibt identisch zum Plan, nur die Bezeichner
  vermeiden die Suffix-Kollision. Alle eigenen Tests sowie die
  Testnamen/Assertions in `McpServerCommandTests` entsprechend angepasst
  (`RunAsync_ValidFixture_GetTypeHierarchyReturnsBaseGreetingHierarchy`
  statt `...BaseGreeterHierarchy`). Keine Aenderung an den vier
  betroffenen, aus dem Scope liegenden Bestandstests noetig — nach der
  Umbenennung liefen alle 1063 Tests gruen.

## Beobachtungen

- Keine weiteren Beobachtungen ausserhalb des Plans. Die
  Footprint-Reduktion durch die Registrar-Aufteilung wirkte wie im
  JIT-Kontext beschrieben: `McpServerOptionsFactory` fiel von 2480 (Stand
  step-006) auf 2437, obwohl gleichzeitig ein fuenftes Tool registriert
  wurde — die eigentliche Datei ist durch die Aufteilung kleiner
  geworden.

## Bekannte Unschärfen

- Wie im Plan unter „Bekannte Ausnahmen" dokumentiert: kein
  `search_pattern`-Fallback fuer nicht-C#-Typidentifikatoren (EPIC-05,
  nicht Teil dieses Steps); `System.Object` bewusst nicht aus der
  Basisklassen-Kette gefiltert (Design-Entscheidung, kein Bug).
- `SymbolGraphToolRegistrations` liegt mit 2455 am naechsten am
  2500-Limit der vier gepruesten Klassen (45 Zeilen Puffer) — ein
  sechstes Symbolgraph-Tool wuerde dort vermutlich das Limit reissen und
  eine weitere Aufteilung noetig machen. Kein eigener Tech-Debt-Eintrag
  angelegt (bleibt dem Kritiker vorbehalten).
