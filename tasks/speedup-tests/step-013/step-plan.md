---
status: done
type: step-plan
task: speedup-tests
step: 013
corrects: null
title: "EPIC-4 Teil 1 — Skeleton-Filterkohorte auf vorbereitete FilterMini-Solution migrieren"
epic: EPIC-4
estimated_risk: high
step_type: single
items: []
created_by: planer
created_by_model: "gpt-5.6-sol Medium"
created_by_model_knowledge_cutoff: "nicht ausgewiesen"
created_at: 2026-08-12
related_to: [step-006, step-008]
---

# Step 013: EPIC-4 Teil 1 — Skeleton-Filterkohorte auf vorbereitete FilterMini-Solution migrieren

## Bezug

- **Task:** `speedup-tests`
- **Epic:** `EPIC-4` aus `roadmap.md` — erster geschlossener Vertikalschnitt der
  In-Memory-Roslyn-/Filter-/Scanner-/Tool-Kohorte; dieser Step umfasst ausschließlich die
  Skeleton-/Filtermatrix samt ihrer Lade-/Ausführungs-Seam. Scanner und übrige MCP-Tools bleiben
  offen.
- **Konzept-Referenz:** `konzept.md` §1 „Testebenen", §2 „Gemeinsame Testplattform", §3 „Laden
  und Ausführen trennen", §4 „Fixture-Portfolio", §7 „Sparsame Verifikation", §9 „Sinnvolle
  Kohorten" sowie Definition of Done „komplette 18-Fälle-Filtermatrix gegen eine kalibrierte
  vorbereitete Solution; separater Pfad-/MSBuild-Adaptertest".

## Aktueller Projektzustand (JIT-Kontext)

- `FilterCliIntegrationTests` enthält 18 Filterverträge für Testprojekt-, Projekt-, Namespace-
  und Sichtbarkeitsfilter sowie Kombinationen und Leermengen. Die Klasse parst keine CLI-Argumente,
  sondern baut `LinterArgs` direkt und ruft `SkeletonMapBuilder.BuildAsync(string, ...)` auf. Jeder
  Fall lädt dadurch die komplette `AiNetLinter.slnx` erneut; Assertions hängen an zufälligen
  Repository-Typen und Projektnamen. Das ist der im Konzept benannte größte konsistente Hotspot.
- `SkeletonMapBuilder` besitzt bereits eine interne fachliche Teilgrenze
  `ExtractTypesAsync(Solution, ...)`, koppelt aber Laden, Filtern, Rendern und Console-Ausgabe noch
  im einzigen aufrufbaren Pfad-Einstieg. Das lokale Vorbild ist `LinterEngine`: der bestehende
  Pfad-Adapter bleibt erhalten und delegiert nach genau einem `SourceFileCatalog.LoadAsync` an
  einen objektbasierten Kern. Wegen der Projektregel „höchstens vier Parameter" soll der neue Kern
  `Solution` plus ein schmales Parameter-Record statt einer weiteren langen Overload-Signatur
  erhalten.
- `FilterMiniSolutionSpec`, `RoslynTestSolutionFactory` und die assembly-weite
  `PreparedSolutionFixture` existieren seit step-006/step-008 bereits und werden wiederverwendet.
  Die 18 Fälle können damit auf einem lazy genau einmal materialisierten immutable Snapshot laufen;
  es ist keine neue Fixture- oder Collection-Lebensdauer nötig.
- Vor der Filtermigration ist eine reale Plattformabweichung zu schließen: Der gecachte
  `RoslynTestSolutionFactory.CoreReferences`-Satz wird aus allen geladenen Testhost-Assemblies
  gebildet und trägt deshalb xUnit-Referenzen auch in das Produktionsprojekt `FilterMini` ein.
  `TestProjectDetector.IsTestProject` klassifiziert damit beide In-Memory-Projekte als Testprojekte.
  Der bestehende Fidelity-Test prüft nur den positiven Testprojektfall und übersieht den falschen
  Produktionsfall. Eine deterministische, testframework-freie BCL-Core-Referenzbasis plus explizite
  `AdditionalReferences` ist Voraussetzung für fachlich gültige `ExcludeTests`-/`TestsOnly`-Tests;
  dies entspricht dem Konzeptvertrag und ist nicht als separater Tech-Debt-Step vorgezogen.
- `SkeletonMapBuilderTests` schützt zwei unmittelbar von der Seam betroffene pending Verträge: den
  echten Pfad-/MSBuild-Erfolgsweg und den ungültigen Pfad. Beide werden im selben Vertikalschnitt
  nach `AiNetLinter.IntegrationTests` übernommen und gegen `FilterMini` statt gegen das echte Repo
  kalibriert. Das Legacy-`TestLintConsole` wird noch von zahlreichen anderen pending Klassen
  konsumiert und darf nicht mit diesen zwei Klassen entfernt werden. Fast- und Integrationtests
  werden aber reale Konsumenten desselben kleinen Zielarchitektur-Testdoubles, sodass eine
  einmalige TestKit-Heimat für die neuen Assemblies zwei neue lokale Kopien vermeidet; die
  Legacy-Variante bleibt bis zur Migration ihrer übrigen Konsumenten bestehen.
- Tech-Debt TD-002 beschreibt die fragile Selbstrepo-Kopplung derselben Filterklasse; sie entfällt
  durch die konzeptgemäße Fixture-Migration. TD-005 beschreibt die oben genannte fehlerhafte
  In-Memory-Testprojekterkennung und muss für diesen Step ursächlich geschlossen werden. Die
  `auto_fixable: ja`-Einträge TD-003/TD-004 liegen weder in denselben Dateien noch in diesem Bereich
  und werden nicht angehängt. Die CodeMap-Entscheidungen aus step-006/step-008 werden erweitert,
  nicht umgedreht.

## Intention

Nach diesem Step läuft die vollständige 18-Fälle-Skeleton-Filtermatrix als Component-Kohorte gegen
einen vorbereiteten, read-only `FilterMini`-Snapshot ohne MSBuild, Prozess oder echtes Repository.
Der produktive Pfad-Einstieg bleibt unverändert verfügbar, delegiert aber an einen testbaren
`Solution`-Kern; zwei kleine Integrationstests belegen weiterhin den echten MSBuild-Adapter und
seinen Fehlerweg. Die Plattform erkennt In-Memory-Produktions- und Testprojekt dabei nachweislich
korrekt, und beide Legacy-Klassen sind physisch entfernt.

## Konkrete Änderungen

### `src/AiNetLinter.TestKit/RoslynTestSolutionFactory.cs` — deterministische Core-Referenzen

- **Was:** Den einmalig gecachten Core-Referenzsatz nicht mehr aus dem gerade geladenen
  Testhost-`AppDomain` ableiten. Stattdessen einen stabilen, testframework-freien Satz benötigter
  .NET/BCL-Referenzassemblies bilden und weiterhin dieselben `MetadataReference`-Instanzen über
  alle Solutions teilen; projektspezifische Bibliotheken bleiben ausschließlich
  `ProjectSpec.AdditionalReferences`. Die vorhandenen Mehrprojekt-, Nullable-, Präprozessor- und
  Cache-Verträge erhalten.
- **Warum:** Ein Produktionsprojekt darf nicht allein durch den xUnit-Testhost eine Testreferenz
  erben. Sonst wären die zentralen `ExcludeTests`-/`TestsOnly`-Verträge der Filtermatrix
  falsch-grün bzw. untestbar.

### `src/AiNetLinter.FastTests/Platform/RoslynTestSolutionFactoryTests.cs` und `src/AiNetLinter.IntegrationTests/Platform/FilterMiniFidelityTests.cs` — negative Testprojekterkennung absichern

- **Was:** Einen Factory-Vertrag ergänzen, der ohne explizite Testframework-Referenz ein normal
  benanntes Projekt als Nicht-Testprojekt belegt. Im Fidelity-Test zusätzlich ausdrücklich
  `Assert.False` für das In-Memory-Projekt `FilterMini` verlangen; die bereits vorhandenen
  positiven Assertions für `FilterMini.Tests` und die Disk-Welt beibehalten.
- **Warum:** Der bisherige Formvergleich ließ genau die für die Filtermatrix relevante
  Produktionsseite ungeprüft. Der neue Negativvertrag verhindert eine erneute Testhost-Kontamination.

### `src/AiNetLinter/Maps/Skeleton/SkeletonMapBuilder.cs` — Pfad-Adapter von objektbasiertem Kern trennen

- **Was:** Den bestehenden `BuildAsync(string targetPath, ...)`-Vertrag erhalten: genau einmal
  `SourceFileCatalog.LoadAsync`, Solution-Pfad bestimmen und an eine neue interne Kernoperation
  delegieren. Die Kernoperation nimmt die vorhandene `Solution` direkt plus ein schmales internes
  Parameter-Record für Solution-Anzeigenpfad, `Config`, `ILintConsole` und `LinterArgs`; der
  `CancellationToken` bleibt separater Kontrollparameter. Filtern, parallele Extraktion, Rendern,
  Console-Ausgabe und Exit-Code müssen in beiden Einstiegen identisch bleiben. Keine öffentliche
  API, kein `#if TESTING` und kein scheinbar nicht-besitzender `SourceFileCatalog`-View.
- **Warum:** Breite Roslyn-Filtertests brauchen keine MSBuild-Ladegrenze. Das vorhandene
  `ExtractTypesAsync(Solution, ...)` wird weiterverwendet, statt eine zweite Filterpipeline zu
  bauen; der Pfadadapter bleibt der reale Produktionsvertrag.

### `src/AiNetLinter.TestKit/RecordingLintConsole.cs` — gemeinsames kleines Zielassembly-Console-Testdouble

- **Was:** Für die beiden neuen Zielassembly-Konsumenten eine fachlich identische minimale
  `ILintConsole`-Aufzeichnung im TestKit bereitstellen: getrennte Output-/Error-Zeilen und
  zusammengesetzte Textansichten. Nur dieser kleine Vertrag, keine allgemeine Output-Helper-
  Sammlung. `src/AiNetLinter.Tests/Output/TestLintConsole.cs` bleibt unverändert vorhanden, weil
  zahlreiche nicht migrierte Legacy-Klassen davon abhängen; keine breitflächige Umstellung dieser
  pending Kohorten in diesem Step.
- **Warum:** Sowohl die Component-Filtermatrix als auch die Integration-Adaptertests benötigen
  denselben injizierbaren Console-Sink; zwei lokale Kopien wären unmittelbare Duplikation.

### Verschiebung/Neuschnitt: `src/AiNetLinter.Tests/Cli/FilterCliIntegrationTests.cs` → `src/AiNetLinter.FastTests/Maps/Skeleton/SkeletonMapFilterTests.cs`

- **Was:** Alle 18 bestehenden fachlichen Fälle ohne semantische Zusammenlegung als
  `[Trait("Category", "Component")]` übernehmen. Die Klasse erhält
  `PreparedSolutionFixture` per Assembly-Fixture-Injektion und materialisiert das Szenario
  `FilterMini` lazy über `RoslynTestSolutionFactory.CreateSolution(FilterMiniSolutionSpec.CreateProjectSpecs())`.
  Jeder Fall ruft die neue objektbasierte Builder-Kernoperation mit demselben immutable Snapshot
  auf. Repository-spezifische Erwartungen werden auf stabile Fixture-Marker (`FilterMini`,
  `FilterMini.Tests`, `FilterMini.Core`, `FilterMini.Utils`, `FilterMini.Tests.Core`, `Widget`,
  `Formatter`, öffentliche/private Member) übertragen; Erfolgs-/Leermengen-/Präzedenz- und
  Kombinationsverträge sowie stderr-/Exit-Code-Assertions bleiben erhalten. Keine
  `SourceFileCatalog.LoadAsync`-, Pfadwurzel-, Prozess- oder Collection-Abhängigkeit im Ziel.
- **Warum:** Die Klasse testet die Filter-/Skeleton-Operation, nicht CLI-Parsing oder MSBuild. Der
  neue Name und Namespace machen die tatsächliche Component-Ebene sichtbar und eliminieren den
  18-fachen Real-Repo-Load.

### Verschiebung/Neuschnitt: `src/AiNetLinter.Tests/Maps/Skeleton/SkeletonMapBuilderTests.cs` → `src/AiNetLinter.IntegrationTests/Maps/Skeleton/SkeletonMapBuilderAdapterTests.cs`

- **Was:** Die zwei Pfadverträge als `[Trait("Category", "Integration")]` migrieren. Der
  Erfolgsfall verwendet eine `IsolatedFixtureLease` von `tests/Fixtures/FilterMini`, ruft den
  unveränderten Pfad-Einstieg auf und belegt Exit-Code, fehlerfreien Console-Kanal sowie
  repräsentative Skeleton-Ausgabe. Der Fehlerfall behält den erwarteten
  `FileNotFoundException`-Vertrag für einen garantiert nicht existierenden Pfad. Den bisherigen
  stillen `if (slnPath == null) return`-Skip und die Abhängigkeit von `AiNetLinter.slnx` nicht
  übernehmen; Lease und Workspace vollständig entsorgen.
- **Warum:** Nur diese zwei Tests sollen die echte Pfad-/MSBuild-Grenze zahlen. Sie sind die vom
  Konzept verlangte repräsentative Adapterabsicherung zur breiten In-Memory-Matrix.

### Legacy-Bereinigung und Task-Artefakte

- **Was:** Die beiden Legacy-Dateien nach erfolgreicher Übernahme physisch löschen.
  `test-migration-ledger.md` für `FilterCliIntegrationTests` und `SkeletonMapBuilderTests` auf
  `migrated` setzen und die jeweiligen existierenden Zielpfade eintragen. `codemap.md` um die
  neuen Fast-/Integration-Ziele und die produktive Solution-Seam ergänzen; die beiden Legacy-
  Pointer als obsolet markieren statt still zu entfernen. `last_updated`-Felder nachführen.
- **Warum:** Der Step endet mit konsistentem Strangler-Zustand: keine Parallelkopien, reale
  Abdeckungsorte und weiterhin baubares Legacy-Projekt. Scanner-, SyntaxWalker-, Stable-ID- und
  sonstige Tool-Ledgerzeilen bleiben unverändert `pending`.

## Tests

- [ ] Vor Produkt-/Teständerungen einmalige Legacy-Basis:
  `dotnet test src/AiNetLinter.Tests --filter "FullyQualifiedName~FilterCliIntegrationTests|FullyQualifiedName~SkeletonMapBuilderTests"`
  → 20 bestehende Verträge grün. Kein Legacy-Volllauf.
- [ ] `dotnet build` → alle fünf Solution-Projekte einschließlich des weiter quarantinierten
  Legacy-Projekts grün, keine Warnungen.
- [ ] Enger Component-/Plattformnachweis:
  `dotnet test src/AiNetLinter.FastTests --no-build --filter "FullyQualifiedName~SkeletonMapFilterTests|FullyQualifiedName~RoslynTestSolutionFactoryTests|FullyQualifiedName~FastTestsDependencyGuardTests"`
  → die 18 migrierten Filterfälle, Factory-Verträge und statische Fast/TestKit-Deny-Liste grün.
- [ ] Enger Integration-/Fidelity-/Migrationsnachweis:
  `dotnet test src/AiNetLinter.IntegrationTests --no-build --filter "FullyQualifiedName~SkeletonMapBuilderAdapterTests|FullyQualifiedName~FilterMiniFidelityTests|FullyQualifiedName~TestMigrationLedgerConsistencyTests|FullyQualifiedName~LegacyProjectBuildGateTests"`
  → Pfad-/MSBuild-Adapter, beide Seiten der Testprojekterkennung, Ledger und Legacy-Gate grün.

Kein vollständiges `Category=Component`- oder `Category!=Stress`-Profil und keine
Dogfood-/Performance-/Stress-Ausführung in diesem Step: EPIC-4 bleibt nach der Filterkohorte für
Scanner und Tools offen. Das breite Component-Gate folgt erst an der EPIC-4-Grenze; die beiden
vollständigen `Category!=Stress`-Gates bleiben Task-End-Verifikation gemäß `AGENTS.md`.

## Definition of Done

- [ ] Alle „Konkrete Änderungen" umgesetzt; die 18 fachlichen Filterverträge und zwei
  Pfadadapterverträge sind ohne Abschwächung an ihren neuen Abdeckungsorten vorhanden
- [ ] `SkeletonMapBuilder` lädt im Pfadadapter genau einmal und delegiert danach an denselben
  `Solution`-Kern, den die Component-Tests verwenden
- [ ] `FilterMini` wird in-memory als Produktionsprojekt und `FilterMini.Tests` als Testprojekt
  erkannt; Core-Referenzen bleiben gecacht und enthalten keine zufälligen Testhost-Frameworks
- [ ] Die gezielten Build-/Testkommandos aus „Tests" sind grün; kein Stress-Profil wurde gestartet
- [ ] Beide Legacy-Testklassen sind physisch entfernt; Ledger und CodeMap bilden den realen
  Zielzustand ab; alle nicht berührten EPIC-4-Kohorten bleiben `pending`
- [ ] Commit auf aktuellem Branch (Conventional Commit auf Deutsch mit Suffix `[speedup-tests]`)
- [ ] `step-013/step-result.md` geschrieben
- [ ] `status` in `step-plan.md` von `in_progress` auf `done (pending audit)` gesetzt

## Rules-Refs

- `.agents/rules/AiNetLinter.mdc` „Kurz-Stil", „architecture", „test-coverage" und
  „Projekt-Overrides" — neue C#-Dateien mit `#nullable enable`, Namespace-/Verzeichnismapping und
  kleinen Signaturen; Produkt-Seam und migrierte Tests behalten ein belastbares Abdeckungssignal.
- `.agents/rules/AiNetLinterRichtlinien.mdc` §3 „Windows-Umgebung & Tool-Regeln", §4 „Updates &
  Tests" und §5 „Qualitätsdrift-Prävention" — gezielte Windows-kompatible Läufe, keine künstliche
  Collection-Serialisierung, keine abgeschwächten Assertions und keine Task-/Step-IDs in
  C#-Kommentaren.

## Bekannte Ausnahmen

Keine.

## Notes

- Der neue objektbasierte Einstieg ist eine echte Produktionsgrenze, kein test-only Wrapper. Die
  existierende `ExtractTypesAsync`-/`CollectDocuments`-Pipeline bleibt die einzige fachliche
  Implementierung; insbesondere Filterlogik aus `SourceFileCatalog.ShouldIncludeProject` nicht in
  Tests oder einen zweiten Builder kopieren.
- `PreparedSolutionFixture` bleibt write-once. Die Filterfälle variieren ausschließlich
  `LinterArgs` und Console-Sink; sie mutieren weder Workspace noch Solution und brauchen deshalb
  keine Collection oder serielle Ausführung.
- Der Adapter-Erfolgstest soll bewusst `FilterMini` laden, nicht `AiNetLinter.slnx`; echtes Repo
  bleibt Dogfood. Der Error-Test darf keinen plattformabhängigen Unix-Pfad wie `/nonexistent/path`
  hartcodieren, sondern bildet einen garantiert fehlenden Windows-kompatiblen Pfad.
- Scanner-, Renderer-, Stable-ID-, SyntaxWalker- und MCP-Tooltests sind kein versteckter
  Zusatzscope. Falls die neue Seam dort später nutzbar ist, wird sie in einem folgenden JIT-Step
  wiederverwendet.
