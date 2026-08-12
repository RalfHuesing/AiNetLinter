---
status: done
type: step-plan
task: speedup-tests
step: 008
corrects: null
title: "Testplattform-Fundament Teil 3 — FilterMini-Fixture (Disk + In-Memory-Spec + Fidelity-Test)"
epic: EPIC-2
estimated_risk: low
step_type: single
items: []
created_by: planer
created_by_model: claude-sonnet-5
created_by_model_knowledge_cutoff: 2026-01
created_at: 2026-08-12
related_to: []
---

# Step 008: Testplattform-Fundament Teil 3 — FilterMini-Fixture (Disk + In-Memory-Spec + Fidelity-Test)

## Bezug

- **Task:** `speedup-tests`
- **Epic:** `EPIC-2` aus `roadmap.md` — letzter offener Baustein: die kalibrierte
  `FilterMini`-Fixture aus Konzept §4 „Fixture-Portfolio statt Einheits-Fixture". Nach step-006
  (`RoslynTestSolutionFactory`/`PreparedSolutionFixture`) und step-007 (`MsBuildFixtureHost`/
  `IsolatedFixtureLease`) ist das der letzte fehlende Punkt, um EPIC-2 vollständig abzuschließen.
- **Konzept-Referenz:** `konzept.md` §4, Zeile 413-414 („neu: `FilterMini` mit mindestens
  Produktions- und Testprojekt, mehreren Namespaces, public/private Membern und Projektbezug;
  ersetzt die echte Solution in der Filtermatrix") sowie Zeile 416-418 („Wo eine Fixture nur
  Quelltextstruktur braucht, wird dieselbe Definition auch durch die In-Memory-Factory
  materialisiert") und Zeile 426-442 („Fidelity-/Paritätstests" — struktureller Formvergleich als
  primärer, Verhaltensparität als sekundärer Teil, Fidelity-Tests wohnen zwingend in
  `AiNetLinter.IntegrationTests`).

## Aktueller Projektzustand (JIT-Kontext)

- Alle sechs bisherigen kanonischen Mini-Solutions unter `tests/Fixtures/*` (`BaselineMini`,
  `BlazorPartialMini`, `CompileErrorMini`, `SingleCompileErrorMini`, `GitImpactMini`,
  `DiRegistrationMini`, `SymbolGraphMini`) sind **Einzelprojekt**-Solutions (ein `.slnx`, ein
  Projekt unter `src/<Name>/`). `FilterMini` ist die erste Fixture mit **zwei** Projekten
  (Produktions- + Testprojekt mit Projektreferenz) — kein Vorbild 1:1 kopierbar, `BaselineMini`
  dient als Vorlage für Datei-/Ordnerkonventionen (`.slnx`-Format, `.csproj`-Minimalform ohne
  explizites `OutputType` für Bibliotheken, `Nullable enable`, `ImplicitUsings enable`,
  `TargetFramework net10.0`).
- `TestProjectDetector.IsTestProject` (`src/AiNetLinter/Core/TestProjectDetector.cs`) erkennt
  Testprojekte primär über Metadatenreferenzen (`xunit`/`nunit`/`testplatform`/`unittesting` im
  Referenz-Display), **subsidiär über Namenssuffix** (`Tests`, `Test`, `IntegrationTests`, `Specs`,
  `Spec`). Für `FilterMini` reicht der Namenssuffix `.Tests` — kein echtes Testframework-Package
  nötig, die Fixture muss nur strukturell, nicht lauffähig als Testprojekt sein.
- `RoslynTestSolutionFactory.CreateSolution(params ProjectSpec[])` (`src/AiNetLinter.TestKit/
  RoslynTestSolutionFactory.cs`) ist bereits der fertige In-Memory-Builder aus step-006:
  `ProjectSpec(Name, Documents, ProjectReferences, ...)` mit `ProjectReferences` als Liste anderer
  `Name`-Werte desselben Aufrufs — passt exakt auf das Produktions-/Testprojekt-Paar von
  `FilterMini`.
- `IsolatedFixtureLease.CopyFixture(root, fixtureFolderName)` + `SourceFileCatalog.LoadAsync(path)`
  (aus step-007) sind der fertige Weg, eine Disk-Fixture einmalig real per MSBuild zu laden.
  `MsBuildFixtureHost` selbst ist **hart auf `"BaselineMini"` verdrahtet**
  (`IsolatedFixtureLease.CopyFixture(root, "BaselineMini")` in `InitializeAsync()`) — für diesen
  Step **nicht** wiederverwenden/generalisieren (nur eine einzelne Fidelity-Testklasse mit ein bis
  zwei Testfällen braucht keine geteilte Assembly-Fixture; das würde `MsBuildFixtureHost` unnötig
  verkomplizieren). Stattdessen lädt die neue Testklasse selbst direkt über `IsolatedFixtureLease`
  + `SourceFileCatalog.LoadAsync` innerhalb ihrer Testmethode(n) — identisches Muster, nur ohne
  Assembly-weites Teilen, weil hier keine mehrfache Wiederverwendung ansteht.
- `codemap.md` listet `FilterMini` noch als „vorgesehener neuer kalibrierter Mehrprojekt-Bestand"
  (Platzhalter, `zuletzt: planning`) — wird durch diesen Step real.
- Die eigentliche Migration von `src/AiNetLinter.Tests/Cli/FilterCliIntegrationTests.cs` (18
  Fälle, 150,1s Laufzeit laut Konzept Zeile 169) ist **nicht** Teil dieses Steps — Konzept §9 Punkt
  3 und die Roadmap ordnen das ausdrücklich EPIC-4 zu. Dieser Step baut nur das Fundament, keinen
  Konsumenten.

## Intention

Nach diesem Step existiert `FilterMini` als kalibrierte Mehrprojekt-Fixture in beiden vom Konzept
geforderten Formen — echte Disk-Solution (für spätere MSBuild-/Integrationstests) und
In-Memory-`ProjectSpec`-Paar (für spätere schnelle Fast-Tests) — aus derselben Quelltext-
Spezifikation, plus ein erster struktureller Fidelity-Test, der beide Welten vergleicht und damit
selbst zum Vertragstest für die neue Fixture wird. Damit ist EPIC-2 vollständig abgeschlossen; die
Migration der Filtermatrix selbst bleibt bewusst EPIC-4 vorbehalten.

## Konkrete Änderungen

### Datei 1: `tests/Fixtures/FilterMini/FilterMini.slnx` (neu)

- **Was:** Neue `.slnx`-Solution-Datei nach dem Muster von `tests/Fixtures/BaselineMini/
  BaselineMini.slnx`, referenziert beide Projekte:
  `src/FilterMini/FilterMini.csproj` und `src/FilterMini.Tests/FilterMini.Tests.csproj`
  (jeweils im `/src/`-Ordner der Solution-Struktur).
- **Warum:** Kanonische Mini-Solution-Struktur, identisch zu den bestehenden sechs Fixtures.

### Datei 2: `tests/Fixtures/FilterMini/src/FilterMini/FilterMini.csproj` (neu)

- **Was:** Minimales SDK-Projekt (`Microsoft.NET.Sdk`), `TargetFramework net10.0`,
  `ImplicitUsings enable`, `Nullable enable`, kein `OutputType` (Default = Library).
- **Warum:** Produktionsprojekt der Fixture, analog `BaselineMini.csproj`, aber als Bibliothek
  statt `Exe` (keine Konsolen-Entry-Point-Notwendigkeit für dieses Szenario).

### Datei 3: `tests/Fixtures/FilterMini/src/FilterMini/Core/Widget.cs` (neu)

- **Was:** `namespace FilterMini.Core;` — `public sealed class Widget` mit public Konstruktor,
  public Property `Name`, public Methode `Describe()` **und** einer privaten Methode (z. B.
  `BuildInternalLabel()`), um Public/Private-Mix im selben Typ zu haben.
- **Warum:** Liefert Namespace 1 der Produktionsseite plus die public/private-Membermischung, die
  Konzept Zeile 413 explizit fordert.

### Datei 4: `tests/Fixtures/FilterMini/src/FilterMini/Utils/Formatter.cs` (neu)

- **Was:** `namespace FilterMini.Utils;` — `internal sealed class Formatter` mit public Methode
  `Format(string)` und einer privaten statischen Hilfsmethode.
- **Warum:** Liefert Namespace 2 der Produktionsseite plus einen komplett `internal` Typ (zusätzlich
  zur öffentlichen `Widget`-Klasse) — deckt den Sichtbarkeitsfilter auf Typebene ab (nicht nur
  Member-Ebene).

### Datei 5: `tests/Fixtures/FilterMini/src/FilterMini.Tests/FilterMini.Tests.csproj` (neu)

- **Was:** Minimales SDK-Projekt wie Datei 2, zusätzlich `<ProjectReference Include="..\FilterMini\
  FilterMini.csproj" />`.
- **Warum:** Testprojekt der Fixture — Namenssuffix `.Tests` macht es über
  `TestProjectDetector.IsTestProject` als Testprojekt erkennbar (Namenssuffix-Fallback, kein echtes
  Testframework-Package nötig); die `ProjectReference` liefert den von Konzept Zeile 413 geforderten
  „Projektbezug".

### Datei 6: `tests/Fixtures/FilterMini/src/FilterMini.Tests/Core/WidgetTests.cs` (neu)

- **Was:** `namespace FilterMini.Tests.Core;` — `public sealed class WidgetTests` mit `using
  FilterMini.Core;`, einer public Methode, die `Widget` instanziiert und `Describe()` aufruft
  (reine Struktur, kein echtes Testframework-Attribut nötig — die Fixture muss nur kompilieren, nicht
  laufen), und einer privaten Hilfsmethode.
- **Warum:** Liefert Namespace 3 (`FilterMini.Tests.Core`) — deckt sowohl den
  Test-Namespace-Ausschluss (`ExcludeNamespaces = ["FilterMini.Tests*"]`) als auch die
  Subnamespace-Glob-Logik (`IncludeNamespaces = ["FilterMini.*"]`) ab, die die spätere
  Filtermatrix-Migration (EPIC-4) braucht.

### Datei 7: `src/AiNetLinter.TestKit/FilterMiniSolutionSpec.cs` (neu)

- **Was:** Neue `sealed`/`static class FilterMiniSolutionSpec` mit einer Methode
  `CreateProjectSpecs()`, die ein `ProjectSpec[]` liefert — ein Eintrag für `"FilterMini"`
  (Documents: `Core/Widget.cs`, `Utils/Formatter.cs`), ein Eintrag für `"FilterMini.Tests"`
  (Documents: `Core/WidgetTests.cs`, `ProjectReferences: ["FilterMini"]`). Die Dateiinhalte
  (`Documents`-Tupel `(FileName, Content)`) müssen **textuell identisch** mit den in Datei 3/4/6
  geschriebenen `.cs`-Dateien sein (gleicher Quelltext als String-Literal) — das ist die praktische
  Umsetzung von „dieselbe Definition wird auch durch die In-Memory-Factory materialisiert"
  (Konzept Zeile 416-417). Am einfachsten: die vier Quelldateien zuerst physisch anlegen (Datei 3/4/6),
  dann ihren exakten Inhalt hierher übernehmen (kein Code-Duplizierungsproblem, weil dies der
  bewusste Zweck der Klasse ist — eine deklarative Spec, kein Produktcode).
- **Warum:** Erfüllt Konzept §4 „Wo eine Fixture nur Quelltextstruktur braucht, wird dieselbe
  Definition auch durch die In-Memory-Factory materialisiert" — Fast-Tests (künftig, EPIC-4) und der
  Fidelity-Test (Datei 8, dieser Step) konsumieren dieselbe Spec statt zwei unabhängig gepflegter
  Quellen.

### Datei 8: `src/AiNetLinter.IntegrationTests/Platform/FilterMiniFidelityTests.cs` (neu)

- **Was:** Neue Testklasse (Category=Integration, Trait wie die übrigen Klassen in diesem Ordner),
  die in einem `[Fact]` async Testfall:
  1. `IsolatedFixtureLease.CopyFixture(root, "FilterMini")` + `SourceFileCatalog.LoadAsync(...)`
     lädt die Disk-Fixture real über MSBuild (kein geteilter Host, siehe „Aktueller
     Projektzustand" oben).
  2. `RoslynTestSolutionFactory.CreateSolution(FilterMiniSolutionSpec.CreateProjectSpecs())` baut
     die In-Memory-Solution.
  3. Struktureller Formvergleich zwischen beiden `Solution`-Objekten gemäß Konzept Zeile 434-440:
     Projektanzahl und -namen (`{"FilterMini", "FilterMini.Tests"}` in beiden), Dokumentanzahl pro
     Projekt, `Nullable`-Kontext, `TestProjectDetector.IsTestProject(...)`-Ergebnis pro Projekt
     (muss für `FilterMini.Tests` `true`, für `FilterMini` `false` sein, in beiden Welten
     übereinstimmend).
  4. Eine kleine Verhaltensparität (Konzept Zeile 441-442, „ein bis zwei fachliche Erwartungen"):
     z. B. dass `Widget.Describe()` in beiden geladenen Compilations denselben Rückgabetyp
     (`string`) hat, oder dass in beiden Welten genau ein `internal`-Typ im Produktionsprojekt via
     Symbol-Sichtbarkeit gefunden wird.
  5. Danach `lease?.Dispose()`/`catalog?.Dispose()` in einem `finally`-Block (kein `IAsyncLifetime`
     nötig für diese eine Testklasse).
- **Warum:** Das ist der von Konzept Zeile 426-429 geforderte Fidelity-/Paritätstest — ohne ihn ist
  die Behauptung „dieselbe Definition materialisiert beide Welten identisch" unbelegt, und jede
  spätere Component-Assertion auf `FilterMiniSolutionSpec` wäre wertlos, falls Disk- und
  In-Memory-Fixture strukturell auseinanderdriften.

### Datei 9: `tasks/speedup-tests/codemap.md`

- **Was:** Zeile zu `tests/Fixtures/FilterMini/` von „vorgesehener neuer kalibrierter
  Mehrprojekt-Bestand" auf „real im Bestand" aktualisieren (analog zu den Updates in step-006/007);
  neue Zeilen für `FilterMiniSolutionSpec.cs` (TestKit-Abschnitt ergänzen) und
  `FilterMiniFidelityTests.cs` (Platform-Abschnitt ergänzen).
- **Warum:** Pointer-Pflicht laut Skill — neue reale Strukturen müssen in der Karte auftauchen.

## Tests

- [ ] Neuer `dotnet build AiNetLinter.slnx` bleibt grün (die neue `FilterMiniSolutionSpec.cs` ist
  Teil von `AiNetLinter.TestKit`, muss compilebar sein; die Disk-Fixture selbst ist **nicht** Teil
  der Haupt-Solution und wird nicht mitgebaut).
- [ ] `dotnet test src/AiNetLinter.IntegrationTests --no-build --filter
  FullyQualifiedName~FilterMiniFidelityTests` — grün, belegt den strukturellen Formvergleich und
  die Verhaltensparität.
- [ ] `dotnet test src/AiNetLinter.FastTests --no-build --filter
  FullyQualifiedName~FastTestsDependencyGuardTests` — grün, stellt sicher, dass
  `FilterMiniSolutionSpec.cs` (reines `ProjectSpec`-Datenobjekt, keine MSBuild-Typen) `TestKit.dll`
  nicht mit einer verbotenen MSBuild-/Workspace-Referenz belastet.
- [ ] `dotnet test src/AiNetLinter.IntegrationTests --no-build --filter
  FullyQualifiedName~TestCategoryProfileGuardTests` — grün, stellt sicher, dass die neue
  Testklasse korrekt kategorisiert ist.

Kein voller `Category!=Stress`-Lauf nötig für diesen Zwischenschritt (Roadmap Tech-Stack-Notiz,
„sparsame Verifikation").

## Definition of Done

- [ ] Alle „Konkrete Änderungen" umgesetzt (9 Dateien/Ordner)
- [ ] Build-Command aus Tech-Stack-Notiz (`roadmap.md`) grün
- [ ] Test-Command aus Tech-Stack-Notiz grün (siehe „Tests" oben, gefilterte Läufe)
- [ ] Commit auf aktuellem Branch (Conventional Commit)
- [ ] `step-008/step-result.md` geschrieben
- [ ] `status` in `step-plan.md` von `in_progress` auf `done (pending audit)` gesetzt

## Rules-Refs

- `.agents/rules/AiNetLinter.mdc` — Methodenlänge ≤60 (Testprojekte 100), max. 4 Parameter,
  `sealed`-Pflicht für konkrete Klassen, Test-Projekt-Override (`*Tests`/`AiNetLinter.TestKit`,
  relevant für die neue `FilterMiniFidelityTests.cs` und `FilterMiniSolutionSpec.cs`).
- `.agents/rules/AiNetLinterRichtlinien.mdc` §5 „Sparsamer Einsatz von Code-Kommentaren" — keine
  Referenzen auf `step-008`/`speedup-tests`/Task-Artefakte in XML-Doc- oder Inline-Kommentaren der
  neuen Dateien (siehe TD-004 in `tech-debt.md`, das genau diesen Fehler in step-007 zeigt — hier
  bewusst vermeiden statt wiederholen).

## Bekannte Ausnahmen

- Keine.

## Code-Skizze (optional)

```csharp
// src/AiNetLinter.TestKit/FilterMiniSolutionSpec.cs (Auszug)
public static class FilterMiniSolutionSpec
{
    public static ProjectSpec[] CreateProjectSpecs() =>
    [
        new ProjectSpec("FilterMini", new (string, string)[]
        {
            ("Core/Widget.cs", WidgetSource),
            ("Utils/Formatter.cs", FormatterSource),
        }),
        new ProjectSpec("FilterMini.Tests", new (string, string)[]
        {
            ("Core/WidgetTests.cs", WidgetTestsSource),
        }, ProjectReferences: ["FilterMini"]),
    ];

    private const string WidgetSource = """
        namespace FilterMini.Core;

        public sealed class Widget
        {
            public string Name { get; }

            public Widget(string name) => Name = name;

            public string Describe() => $"Widget: {Name}";

            private string BuildInternalLabel() => $"[{Name}]";
        }
        """;

    // FormatterSource, WidgetTestsSource analog -- Inhalt identisch zu den physischen .cs-Dateien
    // unter tests/Fixtures/FilterMini/.
}
```

## Notes

- **Kein neuer geteilter Assembly-Host für `FilterMini`.** `MsBuildFixtureHost` bleibt bewusst
  BaselineMini-spezifisch (siehe „Aktueller Projektzustand"). Sollte eine künftige EPIC-4-Migration
  mehrere Testklassen gegen dieselbe geladene `FilterMini`-Instanz brauchen, ist das der richtige
  Zeitpunkt, `MsBuildFixtureHost` zu parametrisieren oder eine zweite Host-Klasse zu bauen — nicht
  vorher spekulativ bauen.
- **Fixture darf nicht wachsen** (Konzept Zeile 420-424): `FilterMini` bleibt auf die hier
  angelegten drei Namespaces/zwei Projekte beschränkt. Braucht die spätere Filtermatrix-Migration
  (EPIC-4) mehr Szenarien, ist das ein bewusster Erweiterungsschritt dort, kein impliziter Nebeneffekt
  dieses Steps.
- **`WidgetTests.cs` ist bewusst kein lauffähiger Test** (kein `[Fact]`/`[Test]`-Attribut, kein
  Testframework-Package im `.csproj`) — die Fixture muss nur strukturell als Testprojekt erkennbar
  sein (`TestProjectDetector`-Namenssuffix), nicht tatsächlich Tests ausführen. Das hält das
  `.csproj` minimal und vermeidet eine unnötige Testframework-Abhängigkeit in einer reinen
  Struktur-Fixture.
