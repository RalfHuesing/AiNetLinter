---
status: done (pending audit)
type: step-plan
task: verbesserungen-mcp
step: 001
title: "Blazor-Partial-Fixture anlegen und Symbolgraph-Diskrepanz reproduzieren"
epic: EPIC-01
estimated_risk: medium
step_type: single
items: []
created_by: planer
created_by_model: claude-sonnet-5
created_by_model_knowledge_cutoff: 2026-01
created_at: 2026-08-05
related_to: []
---

# Step 001: Blazor-Partial-Fixture anlegen und Symbolgraph-Diskrepanz reproduzieren

## Bezug

- **Task:** `verbesserungen-mcp`
- **Epic:** `EPIC-01` aus `roadmap.md` — Blazor-Symbolgraph-Integration (P1).
  Dieser Step deckt **nur** den ersten Teil des Epics ab: die neue
  synthetische Test-Fixture, die das Symptom reproduzierbar macht. Der
  eigentliche Fix in `SourceFileCatalog.cs` (Razor-Source-Generator-
  Output in die Roslyn-`Compilation` einbeziehen) sowie die davon
  abhängige Prüfung des globalen Rausch-Hinweises (`McpCompileDiagnostics.cs`,
  P2) folgen in einem eigenen Folge-Step (voraussichtlich `step-002`),
  sobald der tatsächliche Aufwand des Fixes anhand des dann vorliegenden
  Codestands (inkl. dieser Fixture) bekannt ist. Begründung für den Split
  siehe „Notes" unten.
- **Konzept-Referenz:** `Konzept.md` Scope „P1 — Blazor-Partials" +
  Abschnitt „Wie" („zunächst eine neue synthetische Test-Fixture … anlegen,
  die das Symptom reproduzierbar macht, bevor der eigentliche Fix
  beginnt") + „Verworfene Alternativen" (synthetische Fixture statt
  Verifikation gegen San.smart.Planner.Platform) + „Definition of Done"
  Schnell-Check-Punkte 1 und 2.

## Aktueller Projektzustand (JIT-Kontext)

- `SourceFileCatalog.LoadAsync` (`src/AiNetLinter/Baseline/SourceFileCatalog.cs:39-61`)
  lädt die Solution ausschließlich über `MSBuildWorkspace.Create(LinterEngine.CreateWorkspaceProperties())`
  + `OpenSolutionAsync`. `CreateWorkspaceProperties()` (`src/AiNetLinter/Core/LinterEngine.cs:86-93`)
  setzt `DesignTimeBuild=true` und `SkipCompilerExecution=true` — beides
  Standard-Properties für schnelle Design-Time-Builds, aber vermutlich
  (nicht verifiziert) die Stelle, an der die Razor-Source-Generator-
  Ausgabe (die `.razor.cs`-Partial-Klasse müsste eigentlich mit einer vom
  Generator erzeugten zweiten Partial-Deklaration verschmolzen werden, die
  `: ComponentBase` deklariert) nicht in die vom Workspace berechnete
  `Compilation` einfließt. Kein Code-Änderungsversuch in diesem Step —
  reine Feststellung als Ausgangspunkt für den Fix-Step.
- **Bestehendes Fixture-Muster gefunden und wiederverwendet, keine neue
  Struktur erfunden:** `tests/Fixtures/{BaselineMini,SymbolGraphMini,
  CompileErrorMini,DiRegistrationMini,GitImpactMini}/` sind reale,
  eigenständige Mini-Solutions (eigene `.slnx` + `.csproj`, **nicht** Teil
  von `AiNetLinter.slnx`), die von `FixtureWorkspaceBase`
  (`src/AiNetLinter.Tests/Fixtures/FixtureWorkspaceBase.cs`) in ein
  Temp-Verzeichnis kopiert und danach über den **echten**
  `SourceFileCatalog.LoadAsync`-Pfad (volle `MSBuildWorkspace`, kein
  Mock) geladen werden — exakt der Mechanismus, der auch das
  Symbolgraph-Verhalten aus der Produktion nachbildet.
  `CompileErrorMiniFixtureWorkspace` (`src/AiNetLinter.Tests/Fixtures/CompileErrorMiniFixtureWorkspace.cs`)
  ist das nächstliegende Vorbild: eine Mini-Solution mit absichtlichen
  Compile-Fehlern, gegen die `GetIndexScopeToolTests.
  ExecuteAsync_CompileErrorFixture_OutputStartsWithAggregateWarning`
  (`src/AiNetLinter.Tests/Mcp/Tools/GetIndexScopeToolTests.cs:95-108`)
  bereits exakt das Aggregat-Hinweis-Verhalten prüft, das P2 („Rausch-
  Hinweis eindämmen") im Folge-Step betrifft.
- Die bestehende Razor-Testinfrastruktur (`src/AiNetLinter.Tests/Web/RazorAnalyzerTests.cs`
  + `.Extended.cs`) testet ausschließlich `RazorAnalyzer.Analyze(string razor, ...)`
  — reines String-/Markup-Linting ohne MSBuildWorkspace, ohne echtes
  `Microsoft.NET.Sdk.Razor`-Projekt, ohne Symbolgraph-Bezug. Keine
  Wiederverwendung möglich/nötig für diesen Step — bewusst eine neue,
  separate Fixture, wie in `Konzept.md` „Wie" vorgesehen.
- `tests/Fixtures/SymbolGraphMini/src/SymbolGraphMini/wwwroot/Component.razor`
  ist eine reine `.razor`-Platzhalterdatei außerhalb eines
  `Sdk.Razor`-Projekts (testet nur die „nicht vom Symbolgraph
  abgedeckt"-Zählung in `get_index_scope`) — kein Sdk.Razor-Projekt, kein
  Vorbild für Partial-Class-Merging.
- Lokale Verfügbarkeit geprüft: `dotnet --list-runtimes` zeigt
  `Microsoft.AspNetCore.App 10.0.10` installiert — ein
  `Microsoft.NET.Sdk.Razor`-Projekt mit `<FrameworkReference
  Include="Microsoft.AspNetCore.App" />` benötigt für `ComponentBase`
  damit **kein** zusätzliches NuGet-Package (Shared Framework, keine
  Restore-Abhängigkeit für die Blazor-Typen selbst).
- `get_file_skeleton` (`src/AiNetLinter/Mcp/Tools/GetFileSkeletonTool.cs`)
  rendert Basistypen rein syntaktisch aus `node.BaseList?.Types`
  (`src/AiNetLinter/Maps/Skeleton/SkeletonSyntaxWalker.cs:113`) — ohne
  Symbol-Auflösung. Das erklärt den Mechanismus des Bugs präzise: Im
  typischen Blazor-Codebehind-Muster deklariert **nicht** die
  `.razor.cs`-Partial-Klasse den Basistyp `: ComponentBase`, sondern die
  vom Razor-Compiler aus der `.razor`-Markup-Datei generierte zweite
  Partial-Deklaration. Fehlt diese generierte Deklaration in der
  Compilation (P1-Bug), hat `SiteView` gar keinen Basistyp, wodurch
  `override Task OnInitializedAsync()` etc. gegen keine virtuelle
  Methode mehr matcht → `CS0115`. Das ist der konkrete,
  reproduzierbare Fehlerzustand, den dieser Step als Test festhält.

## Intention

Eine neue synthetische Mini-Solution `BlazorPartialMini` (Sdk.Razor,
`.razor`-Komponente + `.razor.cs`-Partial mit `override`-Lifecycle-
Methoden, Codebehind-Muster ohne expliziten Basistyp im `.razor.cs`) legt
das im Bug-Report beschriebene Symptom offen: Wird sie über den
produktiven `SourceFileCatalog.LoadAsync`-Pfad geladen, meldet die
Roslyn-`Compilation` `CS0115` auf den `override`-Methoden, und
`get_file_skeleton`/`get_index_scope` zeigen entsprechend den
Compile-Fehler-Hinweis bzw. keinen sichtbaren `ComponentBase`-Basistyp.
Diese Tests sind nach diesem Step **grün** (sie dokumentieren den
IST-Zustand, keine TODO-Fails) — der Folge-Step kehrt die betroffenen
Assertions um, sobald der eigentliche Fix landet, und macht damit den
Fortschritt objektiv nachprüfbar.

## Konkrete Änderungen

### Datei 1 (neu): `tests/Fixtures/BlazorPartialMini/BlazorPartialMini.slnx`

- **Was:** Minimale `.slnx`-Solution-Datei nach dem Muster von
  `tests/Fixtures/CompileErrorMini/CompileErrorMini.slnx` — ein
  `<Folder Name="/src/">` mit `<Project Path="src/BlazorPartialMini/BlazorPartialMini.csproj" />`.
- **Warum:** `SourceFileCatalog.LoadAsync` erwartet eine echte
  `.sln`/`.slnx`-Datei (`FindSolutionFile`); dieselbe Struktur wie alle
  bestehenden Mini-Fixtures.

### Datei 2 (neu): `tests/Fixtures/BlazorPartialMini/src/BlazorPartialMini/BlazorPartialMini.csproj`

- **Was:** `<Project Sdk="Microsoft.NET.Sdk.Razor">`,
  `<TargetFramework>net10.0</TargetFramework>`, `ImplicitUsings`/`Nullable`
  wie in den übrigen Fixture-`.csproj` (`enable`), zusätzlich
  `<ItemGroup><FrameworkReference Include="Microsoft.AspNetCore.App" /></ItemGroup>`
  für `Microsoft.AspNetCore.Components.ComponentBase`.
- **Warum:** Erstes `Microsoft.NET.Sdk.Razor`-Projekt in der
  Fixture-Sammlung — bewusst minimal, kein Blazor-Hosting-Setup (kein
  `wwwroot/index.html`, kein `Program.cs`), da nur der
  Symbolgraph-/Compilation-Aspekt getestet wird, nicht das Laufzeit-
  Hosting.

### Datei 3 (neu): `tests/Fixtures/BlazorPartialMini/src/BlazorPartialMini/SiteView.razor`

- **Was:** Einfache Komponente ohne `@code`-Block, z. B.
  ```razor
  <h3>SiteView</h3>
  <p>@Message</p>
  ```
  Name bewusst `SiteView` (identisch zum Dateinamen im Konzept-
  Schnell-Check „`get_file_skeleton(SiteView.razor.cs)`").
- **Warum:** Ohne `@inherits` erbt jede `.razor`-Komponente implizit von
  `ComponentBase` — diese Vererbung wird erst durch den
  Razor-Source-Generator als zweite Partial-Deklaration mit `: ComponentBase`
  materialisiert (siehe „Aktueller Projektzustand").

### Datei 4 (neu): `tests/Fixtures/BlazorPartialMini/src/BlazorPartialMini/SiteView.razor.cs`

- **Was:** Codebehind-Partial-Klasse **ohne** expliziten Basistyp (Blazor-
  Konvention), mit `[Parameter]`-Property und mindestens zwei
  `override`-Lifecycle-Methoden, z. B. `OnInitializedAsync()` und
  `OnParametersSet()`:
  ```csharp
  using Microsoft.AspNetCore.Components;

  namespace BlazorPartialMini;

  public partial class SiteView
  {
      [Parameter]
      public string? Message { get; set; }

      protected override Task OnInitializedAsync() => base.OnInitializedAsync();

      protected override void OnParametersSet() => base.OnParametersSet();
  }
  ```
- **Warum:** Genau das im Konzept beschriebene Muster
  („Partial-Klasse mit `override`-Lifecycle-Methoden"). Der fehlende
  explizite Basistyp ist der entscheidende Punkt — nur so schlägt
  `override` fehl, wenn die generierte Partial-Deklaration in der
  Compilation fehlt.

### Datei 5 (neu): `src/AiNetLinter.Tests/Fixtures/BlazorPartialMiniFixtureWorkspace.cs`

- **Was:** `sealed class BlazorPartialMiniFixtureWorkspace : FixtureWorkspaceBase`,
  Konstruktor `base("BlazorPartialMini", "ainetlinter-blazorpartial-mini-")`,
  plus `public string SiteViewCsPath => Path.Combine(RootPath, "src", "BlazorPartialMini", "SiteView.razor.cs")`
  (analog `SymbolGraphMiniFixtureWorkspace.GreeterPath`).
- **Warum:** Identisches Muster zu allen fünf bestehenden
  `*MiniFixtureWorkspace`-Klassen — keine neue Abstraktion nötig.

### Datei 6 (neu): `src/AiNetLinter.Tests/Baseline/SourceFileCatalogBlazorPartialTests.cs`

- **Was:** Neue Testklasse mit (mindestens) drei Tests:
  1. `LoadAsync_BlazorPartialFixture_ReportsCS0115OnOverrideLifecycleMethod` —
     lädt die Fixture über `SourceFileCatalog.LoadAsync`, ruft
     `McpCompileDiagnostics.GetErrorsByFileAsync(catalog.Solution, ct)` auf
     und prüft, dass der Eintrag für `SiteView.razor.cs` eine
     `CS0115`-Diagnose enthält. Direkter, Implementierungsdetail-armer
     Beweis auf Compilation-Ebene.
  2. `GetIndexScope_BlazorPartialFixture_ShowsAggregateCompileErrorHint` —
     analog `GetIndexScopeToolTests.ExecuteAsync_CompileErrorFixture_OutputStartsWithAggregateWarning`
     (`src/AiNetLinter.Tests/Mcp/Tools/GetIndexScopeToolTests.cs:95-108`):
     `GetIndexScopeTool.ExecuteAsync` gegen einen `McpCodeGraphServer` mit
     dem geladenen Catalog liefert einen mit „Hinweis:" beginnenden Text
     mit „N Dateien haben Compile-Fehler". Bildet Konzept-Schnell-Check
     Punkt 1 im **aktuellen** (noch fehlerhaften) Zustand ab — der
     Folge-Step dreht diese Assertion um.
  3. `GetFileSkeleton_SiteViewRazorCs_MissesComponentBaseBaseType` —
     `GetFileSkeletonTool.ExecuteAsync(state, "src/BlazorPartialMini/SiteView.razor.cs", ct)`
     liefert einen Text, der (a) den dateispezifischen
     Compile-Fehler-Hinweis (`McpCompileDiagnostics.FormatFileWarning`)
     enthält und (b) **keinen** `": ComponentBase"`-Basistyp in der
     Skeleton-Ausgabe zeigt. Bildet Konzept-Schnell-Check Punkt 2 im
     aktuellen Zustand ab.
- **Warum:** Hält den in „Intention" beschriebenen IST-Zustand als
  ausführbaren, grünen Test-Beweis fest — Grundlage für den Folge-Step,
  der exakt diese drei Assertions umkehrt, statt neue Tests zu erfinden.

## Tests

- [ ] `SourceFileCatalogBlazorPartialTests.LoadAsync_BlazorPartialFixture_ReportsCS0115OnOverrideLifecycleMethod`
- [ ] `SourceFileCatalogBlazorPartialTests.GetIndexScope_BlazorPartialFixture_ShowsAggregateCompileErrorHint`
- [ ] `SourceFileCatalogBlazorPartialTests.GetFileSkeleton_SiteViewRazorCs_MissesComponentBaseBaseType`
- [ ] Voller `dotnet test`-Lauf weiterhin grün (neue Tests eingeschlossen,
      keine Regression an bestehenden Fixtures/Tests)

## Definition of Done

- [ ] Alle „Konkrete Änderungen" umgesetzt (6 neue Dateien)
- [ ] `dotnet build` grün (0 Fehler/Warnungen) — betrifft nur
      `AiNetLinter.slnx`; die neue Fixture-Solution ist **nicht** Teil
      davon (wie alle bestehenden Mini-Fixtures) und wird separat nur
      implizit über `MSBuildWorkspace`/`dotnet restore` beim Testlauf
      aufgelöst
- [ ] `dotnet test` (Volllauf) grün, inkl. der 3 neuen Tests
- [ ] Commit auf aktuellem Branch (Conventional Commit, Suffix
      `[verbesserungen-mcp]`)
- [ ] `step-001/step-result.md` geschrieben
- [ ] `status` in diesem `step-plan.md` von `in_progress` auf
      `done (pending audit)` gesetzt

## Rules-Refs

- `.agents/rules/AiNetLinterRichtlinien.mdc#4` — xUnit v3 Pflicht pro
  Logik-Änderung; neue Testklasse **nicht** in eine serialisierende
  Collection aufnehmen (kein erkennbarer Bedarf hier); Commit-Vorschlag
  am Ende jeder Antwort mit Datei-Änderungen (reiner Commit-Text, kein
  Shell-Befehl).
- `.agents/rules/AiNetLinterRichtlinien.mdc#5` — Zero-Warning-Direktive
  (`TreatWarningsAsErrors`, gilt für `AiNetLinter.Tests.csproj`, nicht
  für die separate Fixture-Solution); sparsamer Kommentar-Einsatz, **kein**
  Task-/Step-/Epic-Bezug im Code-Kommentar (auch nicht in der neuen
  Fixture oder Testklasse) — „Symptom reproduzierbar machen" gehört als
  ID-freier Why-Kommentar in die Testklasse, nicht als `step-001`-Verweis.
- `.agents/rules/AiNetLinterRichtlinien.mdc#3` — bei Testfehlern/langem
  Output `TestResults/latest.trx` auslesen statt Testlauf erneut
  unvollständig zu starten.
- `.agents/rules/AiNetLinter.mdc` — Grenzwerte (Methodenlänge,
  Komplexität, Dateilänge) gelten auch für die neue Testklasse und
  Fixture-Workspace-Klasse.

## Bekannte Ausnahmen

- Keine.

## Notes

- **Warum dieser Step nicht das ganze Epic abdeckt:** Der eigentliche
  Fix (Razor-Source-Generator-Output in die vom `MSBuildWorkspace`
  berechnete `Compilation` einbeziehen) ist eine offene Roslyn-/MSBuild-
  Recherchefrage — ob und wie `CreateWorkspaceProperties()`
  (`DesignTimeBuild`/`SkipCompilerExecution`) angepasst werden muss,
  ob `AnalyzerReferences` des geladenen `Project` bereits den
  Razor-Source-Generator enthalten (aber aus anderem Grund nicht
  greifen) oder komplett fehlen, ist vor dem Vorliegen dieser Fixture
  nicht zuverlässig einschätzbar. Erst mit den in diesem Step grün
  laufenden Reproduktions-Tests lässt sich der Fix in einem eigenen,
  fokussierten Folge-Step planen, umsetzen und unabhängig reviewen —
  passend zur Schrittgrößen-Abwägung aus `../spec.md` §10.2 („in einer
  Review-Runde prüfbar", „in sich geschlossen") trotz der Nutzer-Vorgabe
  zu größeren Brocken (dieser Step selbst ist kein Mini-Schritt: neues
  Projekt-SDK in der Fixture-Sammlung, drei neue Tests, mehrschichtige
  Assertions).
  Der Folge-Step wird `related_to: [step-001]` setzen und den
  aktuellen Stand von `step-001/step-result.md` + der hier angelegten
  Dateien nachlesen, bevor er den Fix plant (Pointer-Prinzip, `../spec.md` §10.6).
- **Grüne Tests dokumentieren einen Bug bewusst als grün:** Das ist kein
  „Symptom-Fixing" im Sinne von §5 der Rules — die Assertions behaupten
  nicht, das Verhalten sei korrekt, sie belegen nur den reproduzierbaren
  IST-Zustand für den nachfolgenden Fix. Coder sollte das im
  Kommentar der Testklasse kurz und ID-frei einordnen (z. B. „dokumentiert
  den aktuellen Symbolgraph-Zustand vor der Razor-Generator-Integration"),
  damit spätere Leser nicht versehentlich denken, `CS0115` sei ein
  gewünschtes Verhalten.
- **Restore-Risiko:** Falls das Restore des neuen
  `Microsoft.NET.Sdk.Razor`-Projekts unerwartet Netzwerkzugriff über das
  bereits funktionierende NuGet-Feed-Setup hinaus benötigt und dieser
  nicht verfügbar ist: das ist ein Infrastruktur-Blocker
  (`../spec.md` §11, Tabelle „Build/Test schlägt fehl wegen fehlender
  Infrastruktur/Tooling"), kein Fix-Versuch nötig, sofort `blocked`.
- **Falls CS0115 beim ersten Versuch nicht reproduziert** (z. B. weil
  der Generator entgegen der Annahme doch teilweise greift, oder ein
  anderer Diagnose-Code auftritt): Testassertion auf den tatsächlich
  beobachteten Fehlerzustand anpassen, Grundidee (Basistyp/Override
  fehlt in der geladenen Compilation) beibehalten — kein Blocker, solange
  überhaupt eine Diskrepanz zu `dotnet build` reproduzierbar dokumentiert
  wird.
