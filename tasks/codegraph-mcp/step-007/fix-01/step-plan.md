---
status: done (pending audit)
type: step-plan
task: codegraph-mcp
step: 007/fix-01
title: "Fix: externe Basisklassen/Interfaces verschwinden in get_type_hierarchy"
epic: EPIC-03
estimated_risk: low
step_type: single
items: []
created_by: planer
created_by_model: claude-sonnet-5
created_by_model_knowledge_cutoff: 2026-01
created_at: 2026-07-31T19:00:00Z
related_to: [step-007/step-review.md]
---

# Step 007/fix-01: Fix: externe Basisklassen/Interfaces verschwinden in get_type_hierarchy

## Bezug

- **Task:** `codegraph-mcp`
- **Epic:** `EPIC-03` — Fix-Step zu `step-007` (Review-Verdict `issues`),
  keine Epic-Änderung.
- **Konzept-Referenz:** `konzept.md` Tool-Tabelle Zeile `get_type_hierarchy
  | ... | Basisklassen, abgeleitete Klassen, Interface-Implementierer |
  ...` — siehe Finding 1 unten für den Konzept-Treue-Bezug.

## Scope (alleiniger Auftrag dieses Fix-Steps)

Ausschließlich **Finding 1** aus `tasks/codegraph-mcp/step-007/step-review.md`:

> `src/AiNetLinter/Mcp/Tools/GetTypeHierarchyFormatter.cs:29-52`
> (`FormatBaseTypes`/`FormatInterfaces`, über die Wiederverwendung von
> `FindSymbolTool.FormatSymbolLocations`) — **[MAJOR]** Basisklassen und
> implementierte Interfaces, die außerhalb der analysierten Solution
> deklariert sind (jede BCL-/NuGet-Bibliotheksklasse/-Interface, nicht nur
> `System.Object`), werden durch den `location.IsInSource`-Filter in
> `FormatSymbolLocations` stillschweigend aus der Ausgabe entfernt.
> Ergebnis: falsche Meldung „Keine Basisklasse."/„Keine Interfaces." für so
> gut wie jeden real existierenden Typ mit externer Basisklasse/externem
> Interface.

Kein anderer Teil von step-007 wird angefasst. Der Kritiker hat
Plan-Erfüllung, Rules-Konformität, Build/Test-Status und alle übrigen
Aspekte bereits abgehakt (nur Logische Korrektheit + Konzept-Treue wurden
wegen Finding 1 nicht abgehakt).

## Aktueller Projektzustand (JIT-Kontext)

Gelesen: `src/AiNetLinter/Mcp/Tools/GetTypeHierarchyFormatter.cs` und
`src/AiNetLinter/Mcp/Tools/FindSymbolTool.cs` vollständig.

- `FindSymbolTool.FormatSymbolLocations(symbol, outputRoot)`
  (`FindSymbolTool.cs:81-91`) iteriert **nur** über
  `symbol.Locations.Where(l => l.IsInSource)` — das ist für ihren
  ursprünglichen Zweck (Fundstellen von `find_symbol`/`find_references`,
  wo ein Symbol ohne Quell-Location schlicht kein sinnvolles Suchergebnis
  ist) korrekt. Sie wird von `FindReferencesTool` für die
  Ambiguitäts-Fehlerliste wiederverwendet — dieser Anwendungsfall ist von
  diesem Bug **nicht** betroffen und **darf sich nicht ändern**.
- `GetTypeHierarchyFormatter.FormatBaseTypes`
  (`GetTypeHierarchyFormatter.cs:42-54`) und `.FormatInterfaces`
  (`GetTypeHierarchyFormatter.cs:56-59`) zweckentfremden dieselbe Methode
  für einen andersartigen Anwendungsfall: hier ist ein Symbol **ohne**
  Quell-Location (BCL/NuGet-Typ) der **Normalfall**, keine Ausnahme — die
  Basisklassen-/Interface-Sektion soll laut Plan/`konzept.md` gerade
  diese Typen sichtbar machen (der Plan dokumentiert das explizit für
  `System.Object`, das Problem betrifft aber jeden externen Typ gleichermaßen).
- Die Implementierer-/Ableitungs-Sektion
  (`FormatSubtypesSectionAsync`, `GetTypeHierarchyFormatter.cs:61-76`) ist
  **nicht** betroffen: `SymbolFinder.FindImplementationsAsync`/
  `FindDerivedClassesAsync` liefern ohnehin nur Typen aus der analysierten
  Solution (immer mit Quell-Location) — dort entsteht kein
  Informationsverlust durch `FormatSymbolLocations`. Diese Sektion bleibt
  unverändert.
- Bestehende Fixture `tests/Fixtures/SymbolGraphMini/src/SymbolGraphMini/Hierarchy.cs`
  (`IGreeting`/`BaseGreeting`/`SpecialGreeting`) deckt bereits implizit den
  Bug ab, ohne ihn zu testen: `BaseGreeting`s Basisklasse ist `object`
  (BCL, keine Quell-Location) — kein bestehender Test prüft aber, was in
  der Basisklassen-Sektion dafür steht. Für den externen-Interface-Fall
  fehlt in der Fixture ein Typ, der ein BCL-Interface implementiert (alle
  bisherigen Interfaces in der Fixture sind `IGreeting`, selbst in der
  Solution deklariert).

## Intention

Minimal-invasiver Fix: `GetTypeHierarchyFormatter` bekommt eine eigene,
kleine Formatierungsfunktion für Basisklassen-/Interface-Referenzen, die
bei vorhandener Quell-Location `FindSymbolTool.FormatSymbolLocations`
unverändert weiterverwendet (identisches Format wie bisher für
solution-interne Typen), bei fehlender Quell-Location aber eine
sinnvolle Fallback-Zeile ausgibt statt den Typ kommentarlos verschwinden
zu lassen. `FindSymbolTool.FormatSymbolLocations` selbst bleibt
unverändert (weiterhin korrekt für `find_symbol`/`find_references`).

## Konkrete Änderungen

### Datei 1: `src/AiNetLinter/Mcp/Tools/GetTypeHierarchyFormatter.cs`

- **Was:**
  - `FormatBaseTypes` (aktuell Zeile 42-54) und `FormatInterfaces`
    (aktuell Zeile 56-59) rufen statt
    `FindSymbolTool.FormatSymbolLocations(symbol, outputRoot)` neu
    `FormatHierarchyTypeReference(symbol, outputRoot)` auf (neue private
    Methode in derselben Datei).
  - Neue private Methode:
    ```csharp
    /// <summary>
    /// Formatiert einen Basistyp/ein Interface fuer die Basisklassen-/Interface-Sektionen. Anders als
    /// <see cref="FindSymbolTool.FormatSymbolLocations"/> (gedacht fuer lokale Symbol-Fundstellen,
    /// daher auf <c>IsInSource</c> gefiltert) verwirft dies Typen ohne Quell-Location nicht: BCL-/NuGet-
    /// Basistypen und -Interfaces (z. B. <c>object</c>, <c>IDisposable</c>, <c>CSharpSyntaxWalker</c>)
    /// sind hier der Normalfall, kein Sonderfall, und muessen sichtbar bleiben statt spurlos zu
    /// verschwinden (siehe step-007/fix-01, Review-Finding 1).
    /// </summary>
    private static IEnumerable<string> FormatHierarchyTypeReference(INamedTypeSymbol symbol, string outputRoot)
    {
        var sourceLines = FindSymbolTool.FormatSymbolLocations(symbol, outputRoot).ToList();
        if (sourceLines.Count > 0)
        {
            return sourceLines;
        }

        var kindLabel = symbol.TypeKind == TypeKind.Interface ? "Interface" : "Klasse";
        return new[] { $"{kindLabel}: {symbol.ToDisplayString()} (extern, keine Datei im Repo)" };
    }
    ```
  - `FormatSubtypesSectionAsync` (Implementierer/abgeleitete Klassen)
    bleibt **unverändert** — nutzt weiterhin
    `FindSymbolTool.FormatSymbolLocations` direkt, da dort kein
    Informationsverlust auftritt (siehe JIT-Kontext).
- **Warum:** Behebt Finding 1 minimal-invasiv, ohne
  `FindSymbolTool.FormatSymbolLocations` (und damit `find_symbol`/
  `find_references`) anzufassen. Nutzt bestehende Formatierung für den
  Solution-internen Fall unverändert weiter (kein doppeltes Format für
  denselben Fall).

### Datei 2: `src/AiNetLinter/Mcp/Tools/FindSymbolTool.cs`

- **Was:** Keine Änderung. Explizit geprüft und verworfen, siehe JIT-Kontext:
  `FormatSymbolLocations` bleibt für `find_symbol`/`find_references`
  unverändert korrekt (dort ist ein Symbol ohne Quell-Location kein
  sinnvolles Suchergebnis).
- **Warum:** Dokumentiert die bewusste Nicht-Änderung, damit ein
  Reviewer nicht nach einer übersehenen Anpassung sucht.

### Datei 3: `tests/Fixtures/SymbolGraphMini/src/SymbolGraphMini/Hierarchy.cs`

- **Was:** Eine neue Klasse ergänzen, die ein **nicht** in der Solution
  deklariertes BCL-Interface implementiert (deckt den externen-Interface-
  Fall ab, den die bestehende Fixture nicht abbildet):
  ```csharp
  public sealed class DisposableGreeting : IDisposable
  {
      public void Dispose()
      {
      }
  }
  ```
  Rein additiv, keine Änderung an `IGreeting`/`BaseGreeting`/
  `SpecialGreeting` — keine Kollisionsgefahr mit bestehenden,
  line-sensitiven Tests (analog zur Begründung für die separate Datei in
  step-007).
- **Warum:** Ohne einen Fixture-Typ mit echtem externen Interface lässt
  sich der externe-Interface-Zweig des Fixes nicht durch einen Unit-Test
  abdecken. Der externe-Basisklassen-Fall (`object`) ist bereits über
  `BaseGreeting` vorhanden und braucht keine neue Fixture-Klasse.

### Datei 4: `src/AiNetLinter.Tests/Mcp/Tools/GetTypeHierarchyToolTests.cs`

- **Was:** Zwei neue `[Fact]`-Tests ergänzen (bestehende sechs Tests
  bleiben unverändert):
  ```csharp
  [Fact]
  public async Task ExecuteAsync_ClassWithImplicitObjectBase_ReturnsExternalBaseTypeInsteadOfEmptyMessage()
  {
      using var fixture = new SymbolGraphMiniFixtureWorkspace();
      var catalog = await SourceFileCatalog.LoadAsync(fixture.RootPath);
      var state = new McpCodeGraphServer(catalog);

      var result = await GetTypeHierarchyTool.ExecuteAsync(state, "BaseGreeting", CancellationToken.None);

      Assert.NotEqual(true, result.IsError);
      var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
      Assert.Contains("Object", textContent.Text, StringComparison.Ordinal);
      Assert.DoesNotContain("Keine Basisklasse.", textContent.Text, StringComparison.Ordinal);
  }

  [Fact]
  public async Task ExecuteAsync_TypeWithExternalInterface_ReturnsExternalInterfaceInsteadOfEmptyMessage()
  {
      using var fixture = new SymbolGraphMiniFixtureWorkspace();
      var catalog = await SourceFileCatalog.LoadAsync(fixture.RootPath);
      var state = new McpCodeGraphServer(catalog);

      var result = await GetTypeHierarchyTool.ExecuteAsync(state, "DisposableGreeting", CancellationToken.None);

      Assert.NotEqual(true, result.IsError);
      var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
      Assert.Contains("IDisposable", textContent.Text, StringComparison.Ordinal);
      Assert.DoesNotContain("Keine Interfaces.", textContent.Text, StringComparison.Ordinal);
  }
  ```
- **Warum:** Deckt genau die vom Kritiker gefundene Lücke ab — bislang
  prüfte kein Test einen Typ mit externer (BCL-/Bibliotheks-)Basisklasse
  oder externem Interface. `BaseGreeting`/`object` und
  `DisposableGreeting`/`IDisposable` sind die kleinstmöglichen Fälle, die
  beide Zweige des Fixes (Basisklassen-Sektion, Interface-Sektion)
  unabhängig voneinander abdecken.

## Tests

- [ ] `GetTypeHierarchyToolTests.ExecuteAsync_ClassWithImplicitObjectBase_ReturnsExternalBaseTypeInsteadOfEmptyMessage` (neu)
- [ ] `GetTypeHierarchyToolTests.ExecuteAsync_TypeWithExternalInterface_ReturnsExternalInterfaceInsteadOfEmptyMessage` (neu)
- [ ] Alle sechs bestehenden `GetTypeHierarchyToolTests`-Fälle bleiben grün (unverändert)
- [ ] Volle Testsuite (`dotnet test AiNetLinter.slnx`) grün

## Definition of Done

- [ ] Datei 1 (`GetTypeHierarchyFormatter.cs`) wie beschrieben geändert
- [ ] Datei 3 (Fixture `Hierarchy.cs`) um `DisposableGreeting` ergänzt
- [ ] Datei 4 (`GetTypeHierarchyToolTests.cs`) um die zwei neuen Tests ergänzt
- [ ] `dotnet build AiNetLinter.slnx` grün, 0 Warnungen
- [ ] `dotnet test AiNetLinter.slnx` grün
- [ ] `ainetlinter --config rules.json --path ./src/` → 0 Violations
- [ ] Selbst-Lint-Footprint-Kontrolle für `GetTypeHierarchyFormatter`
      (Zeilenzuwachs durch die neue private Methode; sollte angesichts
      des geringen Umfangs deutlich unter 2500 bleiben, aber dokumentieren)
- [ ] Erneutes Dogfooding gegen die reale `AiNetLinter.slnx` mit
      mindestens einem der drei vom Kritiker genannten Repo-Typen
      (`PerformanceProfiler`, `SourceFileCatalog`, `SkeletonSyntaxWalker`)
      — Nachweis, dass die Basisklassen-/Interface-Sektion jetzt eine
      externe Basisklasse/ein externes Interface anzeigt statt „Keine
      Basisklasse."/„Keine Interfaces.". In `step-result.md` unter
      „Dogfooding" dokumentieren.
- [ ] Commit auf aktuellem Branch (Conventional Commit, z. B.
      `fix(mcp): show external base types/interfaces in get_type_hierarchy [codegraph-mcp]`)
- [ ] `step-007/fix-01/step-result.md` geschrieben
- [ ] `status` in `step-plan.md` von `in_progress` auf
      `done (pending audit)` gesetzt

## Rules-Refs

- `.agents/rules/AiNetLinter.mdc` — `AIContextFootprint`-Grenzwert
  (weiterhin < 2500 für `GetTypeHierarchyFormatter` nach der Ergänzung),
  `#nullable enable` (bereits vorhanden, bleibt erhalten).
- `.agents/rules/AiNetLinterRichtlinien.mdc` — Result-Pattern statt
  Exceptions (unverändert, dieser Fix wirft keine neuen Exceptions),
  Build/Test-Pflicht, Commit-Vorschlag-Pflicht.

## Bekannte Ausnahmen

- Keine.

## Notes

- **Scope-Disziplin:** Dieser Fix-Step ändert ausschließlich das in
  Finding 1 beschriebene Verhalten. Die „Sonstige Beobachtungen"-Notiz im
  Review (fehlende Testabdeckung für externe Basisklassen/Interfaces) ist
  laut Review selbst bereits durch den Fix zu Finding 1 mitabgedeckt —
  entsprechend in Datei 4 oben mit erledigt, kein separater Punkt.
  `tech-debt.md`-Einträge (`TD-004`/`TD-005`, Footprint-Update) sind
  explizit nicht Teil dieses Fix-Scopes.
- `roadmap.md` wird in diesem Fix-Modus **nicht** angefasst (siehe
  `skills/planer/SKILL.md` §Fix-Modus).
