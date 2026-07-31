---
status: done (pending audit)
type: step-plan
task: codegraph-mcp
step: 007
title: "get_type_hierarchy Tool (Basisklassen/abgeleitete Klassen/Interface-Implementierer via SymbolFinder)"
epic: EPIC-03
estimated_risk: medium
step_type: single
items: []
created_by: planer
created_by_model: claude-sonnet-5
created_by_model_knowledge_cutoff: 2026-01
created_at: 2026-07-31T23:30:00Z
related_to: [step-004, step-005, step-006]
---

# Step 007: get_type_hierarchy Tool (Basisklassen/abgeleitete Klassen/Interface-Implementierer via SymbolFinder)

## Bezug

- **Task:** `codegraph-mcp`
- **Epic:** `EPIC-03` aus `roadmap.md` — letztes noch offenes der fünf
  Symbolgraph-Tools (`find_symbol`, `find_references`, `get_impact`,
  `get_file_skeleton` sind bereits `approved`, siehe step-003..006). Nach
  diesem Step (bei `approved`-Review) ist EPIC-03 vollständig.
- **Konzept-Referenz:** `konzept.md` Tool-Tabelle Zeile
  `get_type_hierarchy | Typ-Identifikator | Basisklassen, abgeleitete
  Klassen, Interface-Implementierer | SymbolFinder.FindDerivedClassesAsync/
  FindImplementationsAsync (neu einzubinden)`; Muss-Haben "Explizite
  Scope-Kommunikation" (Zeile 90-97, Tool explizit in der Aufzählung
  genannt) und "Dogfooding pro Tool-Step" (EPIC-03-Notiz in `roadmap.md`).

## Aktueller Projektzustand (JIT-Kontext)

- **Wiederverwendbare Symbol-Auflösung bereits vorhanden:**
  `FindReferencesTool.ResolveSymbolAsync(Solution, string, CancellationToken)`
  (`src/AiNetLinter/Mcp/Tools/FindReferencesTool.cs:51`) löst sowohl
  `Datei:Zeile:Spalte`- als auch (teil-)qualifizierte Namens-Identifikatoren
  zu genau einem `ISymbol` auf (inkl. `SYMBOL_NOT_FOUND`/`AMBIGUOUS_SYMBOL`-
  Fehlerpfaden) — bereits von `GetImpactTool` als Symbol-Branch
  wiederverwendet (`GetImpactTool.ExecuteSymbolBranchAsync`,
  `src/AiNetLinter/Mcp/Tools/GetImpactTool.cs:45`). Dieser Step nutzt
  dieselbe Methode unverändert — **kein neuer Identifikator-Parser**, wie
  vom Auftrag verlangt.
- **`FindSymbolTool.FormatSymbolLocations`** (bereits von
  `FindReferencesTool` für die Ambiguitäts-Fehlerliste wiederverwendet)
  formatiert Datei:Zeile + Kind + Signatur für ein Symbol — wird für die
  Ergebnis-Listen (Basisklassen/abgeleitete Typen/Interfaces) erneut
  wiederverwendet statt neu gebaut.
- **`SymbolFinder.FindDerivedClassesAsync`/`FindImplementationsAsync`**
  sind im Projekt bislang an keiner Stelle eingebunden (verifiziert:
  `SymbolFinder.FindSourceDeclarationsAsync` ist die einzige bisher
  genutzte `SymbolFinder`-API, in `FindSymbolTool`/`FindReferencesTool`).
  Für **Basisklassen** gibt es keine passende `SymbolFinder`-Suchmethode —
  die werden stattdessen durch simples Ablaufen von
  `INamedTypeSymbol.BaseType` (Kette bis `null`/`System.Object`) ermittelt,
  für **implementierte Interfaces** durch `INamedTypeSymbol.AllInterfaces`
  — beides bereits vorhandene Roslyn-Symbol-Eigenschaften, kein API-Aufruf
  nötig.
- **Footprint-Lage (kritisch für diesen Step, siehe `tech-debt.md`
  TD-004/TD-005):** `McpServerOptionsFactory` liegt nach step-006 bei
  2480/2500 (nur 20 Zeilen Puffer), `GetFileSkeletonTool` bei 2428/2500.
  Der Kritiker hat in seinem step-006-Review explizit empfohlen, die im
  step-006-Plan bereits skizzierte Aufteilung von `BuildToolCollection`
  **jetzt, proaktiv** vorzunehmen statt erst reaktiv nach einem
  gerissenen Limit. Beim Lesen von `AIContextFootprintCalculator.cs`
  (`src/AiNetLinter/Metrics/AIContextFootprintCalculator.cs`) zeigt sich,
  **warum** die Zahl bei jedem neuen Tool-Eintrag wächst: der Walker
  summiert für die Ziel-Klasse auch deren **eigene** Syntaxbaum-Zeilen
  (`SumLinesForSymbol` wird auch für `classSymbol` selbst aufgerufen) —
  jeder neue `tools.Add(...)`-Block in `McpServerOptionsFactory.cs`
  erhöht also direkt die eigene Zeilenzahl der Datei und damit den
  Footprint dieser Klasse, unabhängig davon, ob die einzelnen Tool-Klassen
  selbst als Typ referenziert werden. Der Walker folgt Typreferenzen nur
  über deklarierte Member-Signaturen (Felder/Properties/Methoden-Parameter/
  -Rückgabetypen), **nicht** in Methodenkörper hinein — die per Lambda in
  `tools.Add(...)` aufgerufenen `*Tool.ExecuteAsync`-Methoden zählen daher
  **nicht** transitiv zum Footprint von `McpServerOptionsFactory`. Eine
  Aufteilung in **separate statische Klassen** (nicht nur separate private
  Methoden **derselben** Klasse — das würde die eigene Dateizeilenzahl der
  Klasse nicht senken) reduziert den Footprint von
  `McpServerOptionsFactory` also tatsächlich, weil dessen eigene Datei
  dadurch kleiner wird. Dieser Step setzt genau das um (Datei 1 unten),
  **bevor** das fünfte Tool registriert wird — damit das neue Tool nicht
  in eine bereits am Limit stehende Datei geschrieben werden muss.
- **`GetTypeHierarchyTool` selbst** droht laut TD-005-Trend ebenfalls knapp
  zu werden (`GetImpactTool` 2458, `GetFileSkeletonTool` 2428 — beide ohne
  strukturellen Spielraum). Dieser Step wendet daher von Anfang an das in
  TD-005 empfohlene Muster an, das `step-004` bereits nachträglich für
  `FindReferencesTool`/`SymbolIdentifierResolver` etabliert hat: dünner
  Dispatch in der Tool-Klasse selbst, reine Formatierungs-/Traversierungs-
  Helfer in einer separaten Datei ohne `McpCodeGraphServer`-Abhängigkeit.

## Intention

Fünftes und letztes EPIC-03-Tool `get_type_hierarchy`: löst einen
Typ-Identifikator über die bestehende `FindReferencesTool.ResolveSymbolAsync`
zu einem Symbol auf, prüft, dass es ein Typ ist (Klasse/Interface/Struct),
und liefert dessen Basisklassen-Kette, implementierte Interfaces sowie
(je nach Typ-Art) abgeleitete Klassen bzw. implementierende Typen als
Text. Reine Wiederverwendung/Ergänzung bestehender Bausteine (Symbol-
Auflösung, Location-Formatierung, Fehler-Helper) — kein neuer
Identifikator-Parser, keine neue Fehlerklasse. Parallel dazu wird die
Tool-Registrierung proaktiv in zwei Klassen aufgeteilt, um das laut
TD-004 akut gewordene Footprint-Limit nicht zu reißen.

## Konkrete Änderungen

### Datei 1: `src/AiNetLinter/Mcp/McpServerOptionsFactory.cs` (Registrierung aufteilen)

- **Was:** `BuildToolCollection` bleibt als dünner Dispatch bestehen, ruft
  aber statt aller `tools.Add(...)`-Blöcke inline nur noch zwei neue
  interne statische Registrar-Klassen auf:
  `SymbolGraphToolRegistrations.Register(tools, mcpState)` (für
  `find_symbol`, `find_references`, `get_impact`, `get_type_hierarchy` —
  die vier reinen Symbolgraph-Tools) und
  `FileStructureToolRegistrations.Register(tools, mcpState)` (für
  `get_file_skeleton`, Platzhalter für kommende EPIC-04-Tools). Beide
  Klassen in neuen Dateien im selben `Mcp`-Namespace, gleiches
  `tools.Add(McpServerTool.Create(...))`-Muster wie bisher, 1:1 aus
  `BuildToolCollection` übernommen (reine Verschiebung, keine
  Verhaltensänderung an den vier bestehenden Tools).
- **Warum:** Reduziert den `AIContextFootprint` von
  `McpServerOptionsFactory` durch Verkleinerung seiner eigenen Datei
  (siehe Begründung oben, JIT-Kontext) — proaktiv, wie vom Kritiker in
  `step-006/step-review.md` empfohlen, statt reaktiv nach einem
  gerissenen Limit. Reine Umstrukturierung, kein neues Verhalten für die
  vier bereits bestehenden Tools — muss durch die bestehende
  E2E-Tool-Zähl-Assertion (Datei 6) unverändert grün bleiben.

### Datei 2: `src/AiNetLinter/Mcp/SymbolGraphToolRegistrations.cs` (neu)

- **Was:** `internal static class SymbolGraphToolRegistrations` mit
  `internal static void Register(McpServerPrimitiveCollection<McpServerTool> tools, McpCodeGraphServer mcpState)`
  — enthält die drei bestehenden `tools.Add(...)`-Blöcke für
  `find_symbol`/`find_references`/`get_impact` (unverändert aus
  `BuildToolCollection` übernommen) plus den neuen vierten Block für
  `get_type_hierarchy` (Analog-Signatur zu `find_references`: ein
  Pflichtparameter `string typeIdentifier`, `CancellationToken ct = default`).
  Tool-`description` benennt explizit die C#-only-Grenze
  ("Deckt nur .cs-Dateien ab, keine .js/.razor/.xaml/.html/.css-Dateien"),
  analog zu den vier bestehenden Tool-Beschreibungen (Muss-Haben
  "Explizite Scope-Kommunikation").
- **Warum:** Träger der Aufteilung aus Datei 1; hier landet das neue Tool.

### Datei 3: `src/AiNetLinter/Mcp/FileStructureToolRegistrations.cs` (neu)

- **Was:** `internal static class FileStructureToolRegistrations` mit
  derselben `Register(...)`-Signatur, enthält den bestehenden
  `tools.Add(...)`-Block für `get_file_skeleton` (unverändert übernommen).
- **Warum:** Zweite Hälfte der Aufteilung; für spätere EPIC-04-Tools
  (`get_index_scope`, `get_hotspots`, `get_violations`, `search_pattern`)
  vorbereitet — dieser Step ergänzt hier aber **nichts** über die reine
  Verschiebung hinaus (kein Vorausplanen, siehe Auftrag).

### Datei 4: `src/AiNetLinter/Mcp/Tools/GetTypeHierarchyTool.cs` (neu)

- **Was:** `internal static class GetTypeHierarchyTool` mit
  `internal static async Task<CallToolResult> ExecuteAsync(McpCodeGraphServer state, string typeIdentifier, CancellationToken ct)`:
  1. `state.GetCurrentSolution()` — `null` → `McpToolResults.SolutionNotLoaded()`.
  2. `FindReferencesTool.ResolveSymbolAsync(solution, typeIdentifier, ct)` —
     Fehler durchreichen wie bei `GetImpactTool.ExecuteSymbolBranchAsync`.
  3. Ist das aufgelöste Symbol kein `INamedTypeSymbol` (z. B. Methode/
     Property aufgelöst) → `McpToolResults.InvalidArgument(
     $"'{typeIdentifier}' loest zu '{symbol.Kind}' auf, nicht zu einem Typ (Klasse/Interface/Struct).")`
     — **kein** neuer Fehlercode/keine neue `McpToolResults`-Methode, der
     bestehende `INVALID_ARGUMENT`-Code (bereits für Nutzungsfehler wie
     `get_impact`s exklusive Parameter verwendet) passt semantisch.
  4. Bei Erfolg: Delegation an `GetTypeHierarchyFormatter.BuildHierarchyTextAsync`
     (Datei 5) — Dispatch bleibt dünn, damit die eigene Datei kurz und
     der `AIContextFootprint` niedrig bleibt (TD-005).
  5. `McpToolResults.Text(text)`.
- **Warum:** Wiederverwendet Symbol-Auflösung und Fehler-Vokabular
  vollständig; hält sich an das in `GetImpactTool`/`GetFileSkeletonTool`
  etablierte Dispatch-Muster.

### Datei 5: `src/AiNetLinter/Mcp/Tools/GetTypeHierarchyFormatter.cs` (neu)

- **Was:** `internal static class GetTypeHierarchyFormatter` mit reiner
  Traversierungs-/Formatierungslogik, keine `McpCodeGraphServer`-
  Abhängigkeit (analog zu `SymbolIdentifierResolver`, die für
  `FindReferencesTool` denselben Zweck erfüllt):
  - `internal static async Task<string> BuildHierarchyTextAsync(INamedTypeSymbol type, Solution solution, CancellationToken ct)`
    — orchestriert die drei folgenden Abschnitte und fügt sie mit
    Überschriften zu einem Text zusammen (Format analog zu den
    Zeilen-Listen der übrigen Tools: eine Zeile pro Fund via
    `FindSymbolTool.FormatSymbolLocations`).
  - **Basisklassen:** `type.BaseType`-Kette ablaufen bis `null` (inkl.
    `System.Object` — bewusst **nicht** ausschließen, da explizite
    Sichtbarkeit der vollständigen Kette hilfreicher ist als eine
    Sonderregel für einen einzelnen Typ; einfacher, keine
    Sonderfall-Logik). Leere Kette (Interface/Struct ohne Basisklasse)
    → "Keine Basisklasse."
  - **Implementierte Interfaces:** `type.AllInterfaces` (transitiv,
    bereits vorhandene Roslyn-Property) — leer → "Keine Interfaces."
  - **Abgeleitete Klassen / implementierende Typen** (abhängig von
    `type.TypeKind`):
    - `TypeKind.Interface` → `SymbolFinder.FindImplementationsAsync(type, solution, transitive: true, cancellationToken: ct)`
      (liefert `INamedTypeSymbol`-Ergebnisse für den Typ-Overload).
    - `TypeKind.Class` (und sonst) → `SymbolFinder.FindDerivedClassesAsync(type, solution, transitive: true, cancellationToken: ct)`.
    - Leeres Ergebnis → "Keine abgeleiteten Typen." bzw. "Keine
      implementierenden Typen." (je nach Zweig).
  - Jeder Abschnitt nutzt `FindSymbolTool.FormatSymbolLocations(symbol, outputRoot)`
    für die Zeilenformatierung (gleiches Muster wie
    `FindReferencesTool.ResolveByNameAsync`/`FindSymbolTool.FindMatchesAsync`
    für `outputRoot = Path.GetDirectoryName(solution.FilePath) ?? ""`).
- **Warum:** Hält `GetTypeHierarchyTool.cs` selbst kurz (TD-005) und
  bündelt die einzige in diesem Step neu einzubindende `SymbolFinder`-API
  an einer testbaren, von `McpCodeGraphServer` unabhängigen Stelle —
  direkt unit-testbar wie `SymbolIdentifierResolver`/`FindSymbolTool.FindMatchesAsync`.

### Datei 6: `tests/Fixtures/SymbolGraphMini/src/SymbolGraphMini/Hierarchy.cs` (neu)

- **Was:** Neue Fixture-Datei mit einer kleinen, echten Typ-Hierarchie —
  **bewusst neue Datei statt Änderung an `Greeter.cs`**, da
  `FindReferencesToolTests.ResolveSymbolAsync_PositionIdentifier_...`
  (`src/AiNetLinter.Tests/Mcp/Tools/FindReferencesToolTests.cs:74`) eine
  hartcodierte Position (`GreeterPath:5:19`) in `Greeter.cs` prüft — jede
  Zeilenverschiebung dort würde diesen bestehenden, außerhalb des Scopes
  dieses Steps liegenden Test brechen:
  ```csharp
  namespace SymbolGraphMini;

  public interface IGreeter
  {
      string Greet(string name);
  }

  public class BaseGreeter : IGreeter
  {
      public virtual string Greet(string name) => $"Hi, {name}";
  }

  public class SpecialGreeter : BaseGreeter
  {
  }
  ```
  Deckt alle drei Hierarchie-Richtungen ab: `BaseGreeter` hat sowohl eine
  Basisklasse (`object`, über die Kette) als auch ein implementiertes
  Interface (`IGreeter`) und eine abgeleitete Klasse (`SpecialGreeter`);
  `IGreeter` hat einen Implementierer (`BaseGreeter`, transitiv auch
  `SpecialGreeter`); `SpecialGreeter` hat eine Basisklasse (`BaseGreeter`)
  ohne eigene abgeleitete Klassen.
- **Warum:** `Greeter.cs` (flache Klasse ohne Hierarchie) taugt nicht als
  Testgrundlage für dieses Tool; eine neue, additive Fixture-Datei
  vermeidet jede Kollision mit bestehenden, line-sensitiven Tests in
  `FindReferencesToolTests`/`GetFileSkeletonToolTests`.

### Datei 7: `src/AiNetLinter.Tests/Mcp/Tools/GetTypeHierarchyToolTests.cs` (neu)

- **Was:** Unit-Tests analog zum Muster der bestehenden `*ToolTests.cs`
  (siehe Testliste unten).
- **Warum:** Testabdeckung für das neue Tool inkl. Basisklasse/
  Interface/abgeleitete-Klassen/Implementierer-Zweige und Fehlerpfade.

### Datei 8: `src/AiNetLinter.Tests/Commands/McpServerCommandTests.cs`

- **Was:** `RunAsync_ValidFixture_ServerRespondsWithFourTools` →
  `RunAsync_ValidFixture_ServerRespondsWithFiveTools`, Assertion auf 5
  Tools inkl. `get_type_hierarchy` erweitert. Neuer E2E-Subprozess-Test
  `RunAsync_ValidFixture_GetTypeHierarchyReturnsBaseGreeterHierarchy`
  (Muster identisch zu `RunAsync_ValidFixture_GetFileSkeletonReturnsGreeterSignature`),
  ruft `get_type_hierarchy` mit `typeIdentifier = "BaseGreeter"` gegen die
  neue Fixture-Datei (Datei 6) auf und prüft, dass sowohl `IGreeter` als
  auch `SpecialGreeter` im Text vorkommen.
- **Warum:** Bestehender Tool-Zähl-Test muss die Registrierungs-
  Aufteilung (Datei 1-3) und das neue Tool widerspiegeln; E2E-Test
  verifiziert den vollen Subprozess-Pfad wie bei den vier Vorgänger-Tools.

## Tests

- [ ] `GetTypeHierarchyToolTests.ExecuteAsync_NoSolutionLoaded_ReturnsErrorWithSolutionNotLoadedCode`
- [ ] `GetTypeHierarchyToolTests.ExecuteAsync_UnknownTypeIdentifier_ReturnsSymbolNotFoundError`
- [ ] `GetTypeHierarchyToolTests.ExecuteAsync_IdentifierResolvesToMethodNotType_ReturnsInvalidArgumentError`
      (z. B. `typeIdentifier = "BaseGreeter.Greet"`)
- [ ] `GetTypeHierarchyToolTests.ExecuteAsync_ClassWithBaseAndDerived_ReturnsInterfaceAndDerivedClass`
      (`typeIdentifier = "BaseGreeter"` → enthält `IGreeter` und
      `SpecialGreeter` im Text)
- [ ] `GetTypeHierarchyToolTests.ExecuteAsync_InterfaceType_ReturnsImplementingClasses`
      (`typeIdentifier = "IGreeter"` → enthält `BaseGreeter` im Text)
- [ ] `GetTypeHierarchyToolTests.ExecuteAsync_LeafClassWithoutDerivedTypes_ReturnsNoDerivedTypesMessage`
      (`typeIdentifier = "SpecialGreeter"` → enthält `BaseGreeter` als
      Basisklasse, aber "Keine abgeleiteten Typen"/"Keine
      implementierenden Typen" für den eigenen Zweig)
- [ ] `McpServerCommandTests.RunAsync_ValidFixture_ServerRespondsWithFiveTools` (umbenannt/erweitert)
- [ ] `McpServerCommandTests.RunAsync_ValidFixture_GetTypeHierarchyReturnsBaseGreeterHierarchy` (neu)

## Definition of Done

- [ ] Alle „Konkrete Änderungen" umgesetzt (Dateien 1-8)
- [ ] `dotnet build AiNetLinter.slnx` grün, 0 Warnungen
- [ ] `dotnet test AiNetLinter.slnx` grün
- [ ] `ainetlinter --config rules.json --path ./src/` → 0 Violations
- [ ] Selbst-Lint-Footprint-Kontrolle (Pflicht wegen TD-004/TD-005,
      analog zu step-005/step-006): `--footprint McpServerOptionsFactory`,
      `--footprint SymbolGraphToolRegistrations`,
      `--footprint FileStructureToolRegistrations`,
      `--footprint GetTypeHierarchyTool` — alle < 2500 dokumentiert in
      `step-result.md`. Reißt eine der vier Klassen dennoch das Limit:
      weitere Aufteilung (z. B. `GetTypeHierarchyFormatter` bereits als
      Entlastung vorgesehen) statt Rules-Override, dokumentiert als
      Abweichung.
- [ ] Commit auf aktuellem Branch (Conventional Commit,
      `feat(mcp): add get_type_hierarchy tool [codegraph-mcp]` o. ä.)
- [ ] `step-007/step-result.md` geschrieben, inkl. Abschnitt „Dogfooding"
      (Pflicht laut `roadmap.md` EPIC-03-Notiz) — Tool gegen die reale
      `AiNetLinter.slnx` mit einem tatsächlichen Interface/Klassen-Paar
      aufrufen (z. B. `ILintRule`-Implementierer, falls vorhanden, sonst
      ein anderes reales Interface aus `src/AiNetLinter`) und Ergebnis auf
      Plausibilität prüfen.
- [ ] `status` in `step-plan.md` von `in_progress` auf
      `done (pending audit)` gesetzt

## Rules-Refs

- `.agents/rules/AiNetLinter.mdc` — Footprint-Grenzwert
  (`AIContextFootprint` 2500, direkt Auslöser für Datei 1-3 dieses Steps),
  `#nullable enable`, `sealed`/statische Klassen, Methodenlänge,
  Parameterzahl (`Register(tools, mcpState)` bleibt bei 2 Parametern).
- `.agents/rules/AiNetLinterRichtlinien.mdc` — kein DI-Container (Delegate-
  Closure-Registrierung bleibt identisch zum bestehenden Muster über die
  Aufteilung hinweg), Result-Pattern statt Exceptions (Fehlerpfade über
  `McpToolResults`, keine geworfenen Exceptions bei nicht aufgelöstem
  Typ), Build/Test-Pflicht, Commit-Vorschlag-Pflicht.

## Bekannte Ausnahmen

- Kein `search_pattern`-Fallback für nicht-C#-Typidentifikatoren
  (EPIC-05, nicht Teil dieses Steps) — wie bei allen bisherigen
  EPIC-03-Tools.
- `System.Object` wird in der Basisklassen-Kette **nicht** herausgefiltert
  (siehe Datei 5) — bewusste Design-Entscheidung für Einfachheit, kein
  Bug, falls dem Kritiker die vollständige Kette auffällt.

## Notes

- **Reihenfolge-Begründung ggü. step-006:** step-006 hat
  `get_type_hierarchy` bewusst hinter `get_file_skeleton` eingeordnet, weil
  Letzteres eine bereits granular vorhandene Basis (`SkeletonMapBuilder`)
  nur sichtbar machen musste, während `get_type_hierarchy` eine bislang
  ungenutzte `SymbolFinder`-API neu einbindet (höheres Risiko). Dieser
  Step ist entsprechend als `estimated_risk: medium` eingestuft (neue
  API-Fläche + strukturelle Registrierungs-Änderung in Datei 1-3), nicht
  `low`/Batch-fähig.
- **Registrierungs-Aufteilung ist Teil des Scopes, nicht Ausweich-
  Option:** Anders als in step-005/006 (wo die Aufteilung als
  dokumentierte, aber nicht gezogene Ausweich-Stufe im Plan stand), ist
  sie hier fester Bestandteil der „Konkrete Änderungen" — der 20-Zeilen-
  Puffer laut TD-004-Update reicht nicht mehr, um sie erneut nur als
  Eventualität zu behandeln.
- Nach `approved`-Review dieses Steps: Planer aktualisiert `roadmap.md`
  im nächsten Step-Modus-Aufruf, EPIC-03 auf abgehakt setzt (bereits in
  dieser `step-plan.md` als Erwartung vermerkt, formale Abhakung bleibt
  aber dem nächsten Planer-Aufruf nach dem Review vorbehalten, siehe
  `skills/planer/SKILL.md` Schritt 1).
