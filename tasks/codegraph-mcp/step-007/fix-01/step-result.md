---
status: done
type: step-result
task: codegraph-mcp
step: 007/fix-01
epic: EPIC-03
step_type: single
coded_by: coder
coded_by_model: claude-sonnet-5
coded_by_model_knowledge_cutoff: 2026-01
coded_at: 2026-07-31T16:10:00Z
code_commit_hash: 22e8410
status_after: done (pending audit)
blocker_category: n/a
---

# Result Step 007/fix-01: Fix: externe Basisklassen/Interfaces verschwinden in get_type_hierarchy

## Zusammenfassung

Finding 1 aus `step-007/step-review.md` behoben: `GetTypeHierarchyFormatter`
ruft fuer die Basisklassen-/Interface-Sektionen jetzt eine eigene, neue
private Methode `FormatHierarchyTypeReference` auf statt direkt
`FindSymbolTool.FormatSymbolLocations`. Bei vorhandener Quell-Location bleibt
das Format identisch (Delegation an `FormatSymbolLocations`); ohne
Quell-Location (BCL/NuGet-Typ) wird jetzt eine Fallback-Zeile
`"<Klasse|Interface>: <DisplayString> (extern, keine Datei im Repo)"`
ausgegeben statt den Typ stillschweigend zu verwerfen.
`FindSymbolTool.FormatSymbolLocations` selbst unveraendert (weiterhin
korrekt fuer `find_symbol`/`find_references`/`FindReferencesTool`'s
Ambiguitaets-Fehlerliste). `FormatSubtypesSectionAsync` unveraendert (dort
entsteht laut JIT-Kontext kein Informationsverlust).

## Geänderte Dateien

- `src/AiNetLinter/Mcp/Tools/GetTypeHierarchyFormatter.cs` — `FormatBaseTypes`/`FormatInterfaces` rufen neu `FormatHierarchyTypeReference` (neu, privat) statt direkt `FindSymbolTool.FormatSymbolLocations` auf; Fallback-Zeile fuer Symbole ohne Quell-Location.
- `tests/Fixtures/SymbolGraphMini/src/SymbolGraphMini/Hierarchy.cs` — neue Klasse `DisposableGreeting : IDisposable` (rein additiv, deckt den externen-Interface-Fall ab).
- `src/AiNetLinter.Tests/Mcp/Tools/GetTypeHierarchyToolTests.cs` — zwei neue Tests (`ExecuteAsync_ClassWithImplicitObjectBase_ReturnsExternalBaseTypeInsteadOfEmptyMessage`, `ExecuteAsync_TypeWithExternalInterface_ReturnsExternalInterfaceInsteadOfEmptyMessage`); bestehende sechs Tests unveraendert.

## Commit

- **Code-Commit-Hash:** `22e8410`
- **Message:**
  ```
  fix(mcp): show external base types/interfaces in get_type_hierarchy [codegraph-mcp]

  GetTypeHierarchyFormatter reused FindSymbolTool.FormatSymbolLocations
  (filtered on IsInSource) for the base type/interface sections, silently
  dropping every BCL/NuGet base class or interface. Adds a dedicated
  FormatHierarchyTypeReference that falls back to a labelled "extern,
  keine Datei im Repo" line when no source location exists, instead of
  discarding the symbol. FindSymbolTool.FormatSymbolLocations itself is
  unchanged (still correct for find_symbol/find_references).

  Adds DisposableGreeting : IDisposable to the SymbolGraphMini fixture
  and two tests covering the external-base-class (object) and
  external-interface (IDisposable) cases.

  Refs: tasks/codegraph-mcp/step-007/fix-01
  ```
- **Branch:** main
- **Push:** nein (lokal)
- **Doku-Commit:** separater, zweiter Commit (Hash steht nicht hier drin, siehe Orchestrator-Rueckmeldung).

## Build-/Test-Output

```
dotnet build AiNetLinter.slnx → gruen, 0 Warnungen
dotnet test AiNetLinter.slnx  → gruen (1065 Tests, 0 Fehler)
ainetlinter --config rules.json --path . → OK, 0 Violations
--footprint GetTypeHierarchyFormatter → 105 Zeilen transitiv (Limit 2500)
```

## Dogfooding

Gebautes `AiNetLinter.exe` per `StdioClientTransport` (identisches
Verbindungsmuster wie `McpServerCommandTests`/step-007-Dogfooding) als
`--mcp-server --path C:\Daten\Entwicklung\Ralf\AiNetLinter` gestartet
(echtes Repo-Root, keine Fixture). Client-Code lag in einem
Scratch-Projekt im Session-Scratchpad (`ModelContextProtocol`
2.0.0-Package, nicht Teil des Repos, nicht committet, nach dem Lauf
geloescht).

**1) `typeIdentifier = "PerformanceProfiler"`** (einer der drei vom
Kritiker genannten Kandidaten):

```
IsError:
Basisklassen:
Klasse: object (extern, keine Datei im Repo)

Implementierte Interfaces:
src/AiNetLinter/Diagnostics/IPerformanceProfiler.cs:5 - Interface: AiNetLinter.Diagnostics.IPerformanceProfiler

Abgeleitete Klassen:
Keine abgeleiteten Typen.
```

Basisklasse `object` (BCL, kein Repo-File) erscheint jetzt korrekt statt
„Keine Basisklasse.". `IPerformanceProfiler` (solution-intern) ist
weiterhin unveraendert mit Datei:Zeile formatiert — bestaetigt, dass der
solution-interne Formatpfad unangetastet blieb.

**2) `typeIdentifier = "SkeletonSyntaxWalker"`** (zusaetzlicher Nachweis,
deckt eine mehrstufige externe Basisklassen-Kette ab):

```
IsError:
Basisklassen:
Klasse: Microsoft.CodeAnalysis.CSharp.CSharpSyntaxWalker (extern, keine Datei im Repo)
Klasse: Microsoft.CodeAnalysis.CSharp.CSharpSyntaxVisitor (extern, keine Datei im Repo)
Klasse: object (extern, keine Datei im Repo)

Implementierte Interfaces:
Keine Interfaces.

Abgeleitete Klassen:
Keine abgeleiteten Typen.
```

Zeigt die komplette Basisklassen-Kette eines externen NuGet-Typs
(`Microsoft.CodeAnalysis.CSharp` — `CSharpSyntaxWalker` →
`CSharpSyntaxVisitor` → `object`), genau der von Finding 1 beschriebene
Fall („jede BCL-/NuGet-Bibliotheksklasse, nicht nur `System.Object`").
Vorher waeren alle drei Zeilen durch den `IsInSource`-Filter verworfen
worden und die Sektion haette faelschlich „Keine Basisklasse." gemeldet.

## Abweichungen vom Plan

- **Testassertion `ExecuteAsync_ClassWithImplicitObjectBase_...`:** Der
  Plan-Codeblock erwartete `Assert.Contains("Object", ...)` (Grossbuchstabe).
  `INamedTypeSymbol.ToDisplayString()` liefert fuer `System.Object` das
  C#-Schluesselwort `object` (Kleinbuchstabe), nicht `Object` — der erste
  Testlauf schlug entsprechend fehl (`Sub-string not found`, tatsaechlicher
  Text: `"Klasse: object (extern, keine Datei im Repo)"`). Assertion auf
  `Assert.Contains("object", ...)` korrigiert (weiterhin
  `StringComparison.Ordinal`). Kein Verhaltensunterschied im
  Produktionscode, nur eine Falschannahme im Plan-Testcode ueber die
  Gross-/Kleinschreibung von Roslyns `ToDisplayString()`-Ausgabe fuer
  eingebaute Typen.
- Ansonsten Plan 1:1 umgesetzt (Datei 1-4 exakt wie in
  „Konkrete Änderungen" spezifiziert; `FindSymbolTool.cs` bewusst nicht
  angefasst).

## Beobachtungen

- Keine neuen Beobachtungen ausserhalb des Plans. Die im Plan unter
  „Notes" bereits antizipierte Deckung der „Sonstige
  Beobachtungen"-Notiz aus dem Review (fehlende Testabdeckung fuer externe
  Basisklassen/Interfaces) ist durch die zwei neuen Tests erledigt.

## Bekannte Unschärfen

- Das Fallback-Label unterscheidet nur zwischen `Klasse` und `Interface`
  (`symbol.TypeKind == TypeKind.Interface`), analog zu
  `FindSymbolTool.DescribeKind`. Fuer Structs/Enums als Basistyp (in C#
  praktisch nie moeglich fuer `BaseType`/`AllInterfaces` einer Klasse)
  wuerde faelschlich „Klasse" ausgegeben — kein real erreichbarer Fall,
  daher nicht gesondert behandelt, wie schon in `FindSymbolTool` selbst.
