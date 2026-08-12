---
status: open
type: step-plan
task: speedup-tests
step: 006
corrects: null
title: "Testplattform-Fundament Teil 1 — RoslynTestSolutionFactory und PreparedSolutionFixture"
epic: EPIC-2
estimated_risk: medium
step_type: single
items: []
created_by: planer
created_by_model: claude-sonnet-5
created_by_model_knowledge_cutoff: 2026-01
created_at: 2026-08-12
related_to: []
---

# Step 006: Testplattform-Fundament Teil 1 — RoslynTestSolutionFactory und PreparedSolutionFixture

## Bezug

- **Task:** `speedup-tests`
- **Epic:** `EPIC-2` aus `roadmap.md` — Testplattform-Fundamente. Bisher komplett offen (EPIC-1
  ist mit step-005 abgeschlossen); dieser Step ist der erste Teil-Step des Epics.
- **Konzept-Referenz:** `konzept.md` §2 „Gemeinsame Testplattform" (die vier Bausteine
  `RoslynTestSolutionFactory`, `PreparedSolutionFixture`, `MsBuildFixtureHost`,
  `IsolatedFixtureLease` sowie die drei Zusatzpflichten „geteilte, gecachte
  `MetadataReference`n", „lazy Materialisierung pro Szenario", „geteilter Workspace ist
  write-once"), Leitplanke 1 (Ebenen-Tabelle: Component erlaubt `AdhocWorkspace`/vorbereitete
  `Solution`, verbietet `SourceFileCatalog.LoadAsync`/MSBuild/Prozess), Leitplanke 11 (TestKit
  wird nicht vorsorglich zum Sammelbecken — nur bauen, was reale Konsumenten brauchen).

## Aktueller Projektzustand (JIT-Kontext)

- `src/AiNetLinter.TestKit/` existiert als Projekt (referenziert nur `AiNetLinter`, keine
  xUnit-Abhängigkeit, siehe `codemap.md`), enthält aber noch **keine einzige `.cs`-Datei** — der
  gesamte Baustein aus Konzept §2 ist bisher nur beschrieben, nicht gebaut.
- Es existiert bereits genau ein realer Bedarfsfall für `AdhocWorkspace`-basierte Solutions:
  `src/AiNetLinter.FastTests/Core/LinterEngineSolutionAnalysisTests.cs` (aus step-004, Teil der
  Minimum Safety Envelope) hat eine **lokale, private** `CreateAdhocSolution(...)`-Helper-Methode
  (Zeilen 30-55), die genau das tut, was `RoslynTestSolutionFactory` laut Konzept zentral
  übernehmen soll: `AdhocWorkspace` aufbauen, `ProjectInfo` mit `MetadataReference`n und
  `CSharpCompilationOptions` (inkl. `NullableContextOptions`) erzeugen, Dokumente hinzufügen.
  Dieser Step migriert genau diese Stelle als ersten echten Konsumenten — kein
  Fantasie-Anwendungsfall.
- `src/AiNetLinter.Tests/TestHelper.cs` (Legacy) hat ein verwandtes, aber bewusst nicht
  wiederverwendbares Muster: `ParseCode(...)` baut den `MetadataReference`-Satz bei **jedem
  Aufruf neu** per vollem `AppDomain.CurrentDomain.GetAssemblies()`-Scan (Zeilen 36-41) — genau
  die in Konzept-Fehlannahme 17 („In-Memory ist nicht automatisch schnell") beschriebene
  Wiederholungskosten-Falle. `RoslynTestSolutionFactory` baut diesen Referenzsatz stattdessen
  **einmal statisch gecacht**, nicht als Kopie des Legacy-Musters.
- `AiNetLinter.FastTests/Architecture/FastTestsRuntimeDependencyGuardFixture.cs` zeigt das
  bisher einzige Fixture-Muster im neuen Bestand: eine `ICollectionFixture<T>` mit
  `[CollectionDefinition(...)]`. Es gibt noch **keine** echte xUnit-v3-Assembly-Fixture im
  Bestand — Konzept §2 verlangt aber ausdrücklich Assembly-Fixture-Semantik für geteilte
  read-only Snapshots (keine erzwungene Serialisierung ganzer Testklassen nur wegen des
  Sharings, siehe Konzept-Fehlannahme 1 und `AiNetLinterRichtlinien.mdc` §4). `xunit.v3.core`
  3.2.2 ist bereits referenziert (`AiNetLinter.FastTests.csproj`); die genaue
  Assembly-Fixture-Registrierungssyntax (`Xunit.v3`-Namespace, `[assembly: ...]`) ist beim
  Vorbereiten dieses Plans nicht aus Bytecode verifiziert worden (nur die NuGet-Metapakete
  liegen lokal vor, kein direkt lesbares DLL-API) — der Coder muss die exakte API beim
  Implementieren gegen die offizielle Doku (bereits in `konzept.md` §2 verlinkt:
  https://xunit.net/docs/shared-context) verifizieren. Falls die Assembly-Fixture-API in
  3.2.2 wider Erwarten fehlt oder unausgereift ist: ersatzweise eine
  `ICollectionFixture`-Collection **ausschließlich** für die neuen Platform-Tests dieses Steps
  verwenden (nicht die ganze `FastTests`-Assembly serialisieren) und das als bewusste
  Abweichung im `step-result.md` dokumentieren.
- `rules.json` hat seit step-001 einen eigenen Override-Schlüssel `"AiNetLinter.TestKit"`
  (`EnforceSealedClasses: false`, `MaxMethodLineCount: 100`) — die Testplattform-Klassen in
  diesem Step unterliegen also nicht den vollen Produktionsregeln, aber `#nullable enable`,
  `MaxMethodParameterCount` (4) und die übrigen Metrik-Grenzwerte aus `AiNetLinter.mdc` gelten
  weiterhin.
- `FilterMini` (Konzept §4, in `roadmap.md` EPIC-2 mitgelistet) hat noch keinen realen
  Konsumenten — `FilterCliIntegrationTests` (der einzige denkbare Abnehmer) ist erst
  EPIC-4-Migrationsstoff. Dieser Step baut `FilterMini` **nicht** mit; siehe „Bekannte
  Ausnahmen" unten und die Notiz an `EPIC-2` in `roadmap.md`.

## Intention

Nach diesem Step existiert die deklarative Kernplattform aus Konzept §2 real im Bestand:
`RoslynTestSolutionFactory` (mehrprojekt-fähige, deklarative In-Memory-Solution-Erzeugung mit
einmalig gecachtem Kern-Referenzsatz) und `PreparedSolutionFixture` (thread-sicheres,
pro-Szenario lazy materialisierendes Assembly-Fixture-Cache). Der bereits vorhandene
`LinterEngineSolutionAnalysisTests`-Test wird auf die Factory umgestellt, damit der erste
Konsument nicht synthetisch, sondern der reale MSE-Baustein aus step-004 ist. Zusätzlich
bekommt die Plattform selbst ein paar Verhaltenstests (Mehrprojekt-Referenzen, Nullable-Context,
Preprocessor-Symbole, Referenz-Caching, Lazy-Materialisierung, Thread-Sicherheit) — das ist kein
Vorratsbau, sondern die einzige Möglichkeit, den in Konzept-Fehlannahmen 1/17/18 beschriebenen
Anspruch („wirklich gecacht", „wirklich lazy", „Assembly-Fixture serialisiert nicht versehentlich")
mechanisch zu belegen statt zu behaupten.

## Konkrete Änderungen

### Datei 1 (neu): `src/AiNetLinter.TestKit/RoslynTestSolutionFactory.cs`

- **Was:**
  - Statische Klasse `RoslynTestSolutionFactory` mit:
    - Einem `internal` (öffentlich für `AiNetLinter.FastTests`/`AiNetLinter.IntegrationTests`
      über die Projektreferenz — als `public` API des TestKits, da TestKit kein
      `InternalsVisibleTo` an die Ziel-Assemblies vergibt und auch nicht braucht, solange
      keine Produkt-Interna berührt werden) `Lazy<ImmutableArray<MetadataReference>>` als
      privates statisches Feld für den Kern-Referenzsatz, einmalig gebaut (Kandidat: alle
      aktuell geladenen, nicht-dynamischen `AppDomain.CurrentDomain`-Assemblies mit
      `Location`, analog zum bestehenden Muster in `TestHelper.ParseCode`, aber **einmal**
      statt pro Aufruf berechnet und über `CoreReferences`-Property exponiert).
    - `public sealed record ProjectSpec(string Name, IReadOnlyList<(string FileName, string
      Content)> Documents, IReadOnlyList<string>? ProjectReferences = null,
      IReadOnlyList<MetadataReference>? AdditionalReferences = null, NullableContextOptions
      Nullable = NullableContextOptions.Enable, IReadOnlyList<string>? PreprocessorSymbols =
      null, OutputKind OutputKind = OutputKind.DynamicallyLinkedLibrary)` — deklarative
      Projektbeschreibung; `ProjectReferences` referenziert andere `ProjectSpec.Name`-Werte
      **desselben** `CreateSolution`-Aufrufs.
    - `public sealed record RoslynTestSolution(Solution Solution, Workspace Workspace) :
      IDisposable` mit `Dispose() => Workspace.Dispose()` — Besitzer-Übergabe wie in Konzept §2
      gefordert („gibt einen immutable Solution-Snapshot plus den Besitzer des Workspaces zur
      kontrollierten Entsorgung zurück").
    - `public static RoslynTestSolution CreateSolution(params ProjectSpec[] specs)`: baut ein
      neues `AdhocWorkspace`, legt für jeden Spec ein `ProjectInfo` mit `CoreReferences` +
      `AdditionalReferences` als `MetadataReferences`, `CSharpCompilationOptions(OutputKind,
      nullableContextOptions: Nullable)` und `CSharpParseOptions` (bei gesetzten
      `PreprocessorSymbols`) an; fügt danach alle Dokumente hinzu; verdrahtet abschließend
      `ProjectReferences` über `Solution.AddProjectReference` anhand der Namensauflösung
      zwischen den `specs`. Wirft eine sprechende `InvalidOperationException`, wenn ein
      referenzierter Name nicht in `specs` vorkommt (kein stiller Fehlerfall).
  - `#nullable enable`, `sealed` wo möglich (Override erlaubt `EnforceSealedClasses: false`,
    heißt aber nicht „nie sealed" — statische Klasse ist ohnehin implizit sealed).
- **Warum:** zentraler deklarativer Solution-Builder aus Konzept §2, ersetzt künftig lokale
  `AdhocWorkspace`-Handrollungen wie in `LinterEngineSolutionAnalysisTests`; einmalig gecachter
  Referenzsatz ist die technische Grundlage für Konzept-Fehlannahme 17 („In-Memory ist nicht
  automatisch schnell").

### Datei 2 (neu): `src/AiNetLinter.TestKit/PreparedSolutionFixture.cs`

- **Was:** `public sealed class PreparedSolutionFixture : IDisposable` mit:
  - Privates `ConcurrentDictionary<string, Lazy<RoslynTestSolution>>` als Szenario-Cache.
  - `public Solution GetOrCreate(string scenarioName, Func<RoslynTestSolution> factory)`:
    holt/erstellt einen `Lazy<RoslynTestSolution>` mit
    `LazyThreadSafetyMode.ExecutionAndPublication` (echte Thread-Sicherheit bei parallelen
    Testklassen derselben Assembly-Fixture) und gibt `.Value.Solution` zurück — `factory` wird
    dadurch garantiert höchstens einmal pro `scenarioName` ausgeführt, unabhängig davon, wie
    viele Testklassen gleichzeitig danach fragen.
  - `public void Dispose()`: iteriert über alle Einträge, deren `Lazy<T>.IsValueCreated == true`
    (nicht materialisierte Szenarien werden nicht angefasst), und disposed jeweils
    `.Value.Workspace`.
  - XML-Doc-Kommentar hält fest: geteilter `Workspace` ist write-once (Konzept §2) — Konsumenten
    dürfen auf der zurückgegebenen `Solution` keine `TryApplyChanges`/Mutation aufrufen, nur auf
    einem eigenen abgeleiteten Snapshot oder einer eigenen `RoslynTestSolutionFactory`-Instanz.
- **Warum:** Assembly-weit geteilter, aber pro Szenario lazy materialisierender Cache aus
  Konzept §2 — verhindert sowohl „jede Testklasse baut ihre eigene Solution neu" als auch „ein
  eager aufgebautes Gesamtportfolio zahlt der gefilterte Einzeltestlauf mit" (Konzept-Zusatzpflicht
  „Lazy Materialisierung pro Szenario").

### Datei 3 (neu): `src/AiNetLinter.FastTests/Platform/PreparedSolutionAssemblyFixture.cs`

- **Was:** Registriert `PreparedSolutionFixture` als echte xUnit-v3-Assembly-Fixture für
  `AiNetLinter.FastTests` (API laut `konzept.md`-Link https://xunit.net/docs/shared-context
  gegen den tatsächlichen Stand von `xunit.v3.core` 3.2.2 verifizieren — siehe „Aktueller
  Projektzustand" oben für den Fallback, falls die API abweicht). Datei enthält ausschließlich
  die Registrierung (kein Testcode), damit sie unabhängig vom Ort der eigentlichen
  Platform-Tests wiedergefunden wird.
- **Warum:** Konzept §2 verlangt Assembly-Fixture-Semantik für geteilte read-only Snapshots,
  explizit **nicht** über eine erzwungene serielle Collection (Konzept-Fehlannahme 1,
  `AiNetLinterRichtlinien.mdc` §4 „Testsuite-Parallelität bewahren").

### Datei 4 (Refactoring): `src/AiNetLinter.FastTests/Core/LinterEngineSolutionAnalysisTests.cs`

- **Was:** private `CreateAdhocSolution(...)`-Methode (Zeilen 30-55) entfernen; der bestehende
  `[Fact]` `RunAsync_PreparedSolutionWithSealedClassViolation_...` baut die Solution stattdessen
  über `RoslynTestSolutionFactory.CreateSolution(new ProjectSpec("SolutionAnalysisTestProject",
  [("UnsealedService.cs", violatingClass), ("SealedService.cs", compliantClass)]))` und disposed
  das Ergebnis (`using`). Assertions und Testverhalten bleiben unverändert — reines
  Infrastruktur-Refactoring, kein neuer Fachvertrag hier.
- **Warum:** erster echter Konsument der neuen Factory statt eines zweiten, parallel
  existierenden `AdhocWorkspace`-Bauplans; belegt, dass die deklarative API den realen
  MSE-Baustein aus step-004 tatsächlich trägt.

### Datei 5 (neu): `src/AiNetLinter.FastTests/Platform/RoslynTestSolutionFactoryTests.cs`

- **Was:** `[Trait("Category", "Component")]`-Testklasse mit mindestens:
  - Mehrprojekt-Test: zwei `ProjectSpec`s, zweites referenziert erstes über `ProjectReferences`;
    ein Symbol aus Projekt A wird in Projekt B semantisch aufgelöst (`GetSemanticModel` +
    `GetTypeByMetadataName` oder äquivalent) → belegt echte Projekt-zu-Projekt-Verdrahtung, nicht
    nur zwei unabhängige Compilations.
  - Nullable-Context-Test: `Nullable: NullableContextOptions.Disable` vs. `.Enable` erzeugt
    nachweislich unterschiedliches Diagnose-Verhalten bei identischem, absichtlich
    nullable-verletzendem Quelltext (z. B. CS8600-Klasse Warnung vorhanden/fehlend).
  - Preprocessor-Symbol-Test: ein `#if SYMBOL`-Block wird nur bei gesetztem Symbol kompiliert
    (z. B. via bedingter Typdeklaration, deren Vorhandensein per `GetTypeByMetadataName`/
    Compilation-Diagnostics geprüft wird).
  - Referenz-Caching-Test: zwei unabhängige `CreateSolution`-Aufrufe referenzieren
    denselben `MetadataReference`-Objektsatz (Referenzgleichheit mindestens eines konkreten
    Eintrags, z. B. der `mscorlib`/`System.Private.CoreLib`-Referenz, über beide Aufrufe hinweg
    geprüft) — belegt, dass der Kern-Referenzsatz wirklich einmal gebaut und nicht pro Aufruf neu
    von der Platte gelesen wird.
  - Namensauflösungsfehler-Test: `ProjectReferences` mit unbekanntem Namen wirft
    `InvalidOperationException` mit dem fehlenden Namen in der Meldung.
- **Warum:** Vertragstest für die Plattform selbst (Konzept §4 „Fidelity-/Paritätstests"-Prinzip
  sinngemäß auf die Factory angewendet) — ohne diese Tests bleiben „gecacht" und „Referenzen
  funktionieren wirklich projektübergreifend" unbelegte Behauptungen im Kommentar.

### Datei 6 (neu): `src/AiNetLinter.FastTests/Platform/PreparedSolutionFixtureTests.cs`

- **Was:** `[Trait("Category", "Component")]`-Testklasse, die den `PreparedSolutionFixture` über
  Konstruktor-Injektion der Assembly-Fixture (Datei 3) erhält:
  - Lazy-Materialisierung: `GetOrCreate("scenario-a", factory)` zweimal mit demselben
    Szenarionamen, aber unterschiedlicher `factory`-Instanz aufrufen → zweiter Aufruf liefert
    dieselbe `Solution`-Instanz zurück (Referenzgleichheit), zweite `factory` wird nie
    ausgeführt (Zähler-Assertion).
  - Isolation zwischen Szenarien: zwei unterschiedliche Szenarionamen liefern unterschiedliche
    `Solution`-Instanzen.
  - Thread-Sicherheit: `GetOrCreate` für denselben neuen Szenarionamen aus mehreren parallelen
    Tasks aufrufen (`Task.WhenAll`), Factory-Aufruf-Zähler bleibt bei 1.
- **Warum:** belegt mechanisch die drei in Konzept §2 geforderten Eigenschaften der Fixture
  (lazy, pro Szenario materialisierend, thread-sicher) statt sie nur im XML-Doc zu behaupten.

## Tests

- [ ] `dotnet build` (Solution) — muss grün bleiben, insbesondere `AiNetLinter.TestKit` (jetzt
  mit echtem Code) und `AiNetLinter.FastTests`.
- [ ] `dotnet test src/AiNetLinter.FastTests --filter "FullyQualifiedName~LinterEngineSolutionAnalysisTests"`
      — bestehender MSE-Baustein bleibt nach der Migration auf die Factory grün.
- [ ] `dotnet test src/AiNetLinter.FastTests --filter "FullyQualifiedName~RoslynTestSolutionFactoryTests"`
      — neue Plattform-Vertragstests (Mehrprojekt, Nullable, Preprocessor, Caching, Fehlerpfad).
- [ ] `dotnet test src/AiNetLinter.FastTests --filter "FullyQualifiedName~PreparedSolutionFixtureTests"`
      — neue Fixture-Vertragstests (Lazy, Isolation, Thread-Sicherheit).
- [ ] `dotnet test src/AiNetLinter.FastTests --filter "FullyQualifiedName~FastTestsDependencyGuardTests|FullyQualifiedName~TestCategoryProfileGuardTests"`
      — bestehende Architekturguards bleiben grün: `AdhocWorkspace`/`ConcurrentDictionary`/
      `Lazy<T>` stehen nicht auf der Deny-Liste, aber der neue Code muss trotzdem gegen die
      Guards laufen, um das mechanisch zu bestätigen (kein manuelles „sollte gehen").
- [ ] Ledger-Konsistenzguard NICHT gezielt nötig — dieser Step migriert keine Legacy-Testklasse
      und ändert `test-migration-ledger.md` nicht.
- Sparsame Verifikation laut `konzept.md` §7/`roadmap.md` Tech-Stack-Notiz: kein voller
  `Category!=Stress`-Lauf für diesen Step; die oben gefilterten Läufe decken den geänderten
  Vertrag ab.

## Definition of Done

- [ ] Alle „Konkrete Änderungen" umgesetzt
- [ ] Build-Command aus Tech-Stack-Notiz (`roadmap.md`) grün
- [ ] Gezielte Test-Filter oben grün (kein Vollauf nötig laut Konzept §7)
- [ ] Commit auf aktuellem Branch (Conventional Commit, `[speedup-tests]`-Suffix)
- [ ] `step-006/step-result.md` geschrieben
- [ ] `status` in `step-plan.md` von `in_progress` auf `done (pending audit)` gesetzt
- [ ] `codemap.md` um die neuen Dateien (`RoslynTestSolutionFactory.cs`,
      `PreparedSolutionFixture.cs`, `PreparedSolutionAssemblyFixture.cs`,
      `RoslynTestSolutionFactoryTests.cs`, `PreparedSolutionFixtureTests.cs`) sowie die
      Umwidmung von `LinterEngineSolutionAnalysisTests.cs` ergänzt (Coder-Pflicht laut
      `../spec.md` §5, nicht Teil der Planer-Änderungen dieses Steps)

## Rules-Refs

- `.agents/rules/AiNetLinterRichtlinien.mdc` §4 „Testsuite-Parallelität bewahren" — begründet,
  warum dieser Step eine echte Assembly-Fixture statt einer zwangsserialisierenden Collection
  baut (Datei 3), und warum das ggf. nötige `ICollectionFixture`-Fallback nur die neuen
  Platform-Tests betreffen darf, nicht die ganze `FastTests`-Assembly.
- `.agents/rules/AiNetLinter.mdc` „Projekt-Overrides" — `AiNetLinter.TestKit` hat eigene
  Overrides (`EnforceSealedClasses: false`, `MaxMethodLineCount: 100`, siehe `rules.json`
  Zeile 414ff., nicht identisch mit der in der `.mdc`-Datei noch veralteten `*.Tests`-Zeile,
  siehe `tech-debt.md` TD-003 — für diesen Step ohne Bedeutung, da kein Treffer im berührten
  Code).

## Bekannte Ausnahmen

- `FilterMini`-Fixture (Konzept §4) wird in diesem Step bewusst **nicht** gebaut — noch kein
  realer Konsument im Bestand (erst mit der `FilterCliIntegrationTests`-Migration in EPIC-4
  sinnvoll), siehe „Aktueller Projektzustand" und die Notiz an `EPIC-2` in `roadmap.md`.
- `MsBuildFixtureHost`/`IsolatedFixtureLease` (die beiden übrigen Konzept-§2-Bausteine) sind
  ebenfalls nicht Teil dieses Steps — sie betreffen echte MSBuild-/Dateisystem-Fixtures für
  `AiNetLinter.IntegrationTests` und haben noch keinen migrierten Konsumenten; folgen als
  eigener EPIC-2-Teil-Step, sobald ein echter Integrationstest sie braucht (frühestens
  EPIC-5/6, oder früher falls ein Fidelity-Test aus Konzept §4 vorgezogen wird).
- Sollte die xUnit-v3-Assembly-Fixture-API in `xunit.v3.core` 3.2.2 beim Implementieren
  abweichen oder fehlen: der in Datei 3 dokumentierte `ICollectionFixture`-Fallback ist
  zulässig, aber nur für die in diesem Step neu hinzugefügten Platform-Testklassen (Dateien 5/6),
  nicht als Präzedenzfall für spätere Kohorten-Migrationen ohne erneute Prüfung.

## Notes

- `RoslynTestSolutionFactory`/`PreparedSolutionFixture` werden bewusst `public` (nicht
  `internal`) in `AiNetLinter.TestKit`, weil sie von `AiNetLinter.FastTests` **und** künftig
  `AiNetLinter.IntegrationTests` konsumiert werden sollen (projektübergreifende TestKit-API) —
  kein `InternalsVisibleTo`-Bedarf, solange keine Produkt-Interna in der Signatur auftauchen
  (Leitplanke 19 betrifft nur echte `internal`-Seam-Durchreichung, hier nicht der Fall).
- Bewusst **kein** Umbau der Deny-Listen-Architekturguards (`FastTestsDependencyGuardTests`,
  `FastTestsRuntimeDependencyGuardFixture`) in diesem Step — `AdhocWorkspace`,
  `ConcurrentDictionary`, `Lazy<T>` stehen nicht auf der Deny-Liste und lösen sie nicht aus; die
  bestehenden Guards laufen als Regressionsnachweis einfach mit (siehe Tests-Abschnitt).
- Der Referenz-Caching-Test (Datei 5) darf sich nicht auf Objektidentität des gesamten
  `ImmutableArray<MetadataReference>` verlassen (Value-Type-Vergleich von `ImmutableArray`
  wäre kein Beweis für „gleiche zugrunde liegenden Referenzobjekte") — stattdessen Identität
  einzelner `MetadataReference`-Einträge (Referenztyp) prüfen.
- `LinterEngineSolutionAnalysisTests` bleibt bei direkter `RoslynTestSolutionFactory.CreateSolution`-Nutzung
  (nicht über `PreparedSolutionFixture`) — die dort gebaute Solution ist test-spezifisch (zwei
  bewusst gewählte Klassen für genau diesen einen Test) und kein über mehrere Testklassen
  wiederverwendetes Szenario; die Fixture wäre hier keine Einsparung, sondern nur Umweg. Wird
  in einer späteren Kohorte ein tatsächlich mehrfach genutztes Szenario sichtbar, registriert
  der jeweilige Migrations-Step es über die Fixture — nicht vorab hier erfinden (Leitplanke 11).
