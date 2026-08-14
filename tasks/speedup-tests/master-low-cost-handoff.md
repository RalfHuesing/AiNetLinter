---
status: ready
task: speedup-tests
step: 029
mode: hybrid-low-cost
created_at: 2026-08-14
baseline_head: 32b0150
pending_at_plan: 53
packages: 3
---

# Master-Low-Cost-Handoff: Rest von `speedup-tests`

Dies ist der einzige Detailplan fuer den Task-Rest. Er ersetzt weitere Planer-/Kritiker-Schleifen
zwischen Teilkohorten. Ein externer Coder arbeitet Paket 1 bis 3 nacheinander ab, setzt nach jedem
Paket einen Commit-/Statuspunkt und wartet nicht auf einen neuen Plan. Ein Audit erfolgt erst nach
dem vollstaendigen jeweiligen Paket.

## Verifizierter Startzustand

- Step 028 ist `approved`: `step028-fast-matrix.trx` enthaelt 69/69 und
  `step028-integration-matrix.trx` 64/64 gruene, eindeutige FQNs; Discovery- und TRX-Diffs sind
  je 0 Byte.
- Externe lokale Commits auf `main` seit `origin/main`: `399a463` (Code Step 027), `479a7a7`
  (Result Step 027), `32b0150` (Review Step 027). Step 028 hatte keinen Codecommit.
- Ledger: 183 Zeilen = 53 `pending`, 129 `migrated`, 1 `consolidated`, 0
  `removed-trivial`.
- Verbleibende Quellbasis: 53 Legacy-Testklassen, 301 statische `[Fact]`-/`[Theory]`-Methoden,
  mindestens 331 statisch sichtbare Faelle; die reale Discovery ist vor jedem Move die
  massgebliche Baseline. Das Legacy-Projekt hat aktuell 83 getrackte Dateien, davon 28
  Support-`.cs`-Dateien ohne eigene Ledgerzeile.
- Offene Tech-Debts: TD-007 (Skeleton-`CreateConfig`, nur bei realem drittem Konsumenten), TD-008
  (parallele Legacy-/Zielhelper) und TD-010 (doppelte Workspace-Kopie). TD-008/TD-010 werden in
  Paket 3 geschlossen; TD-007 wird nur geschlossen, falls Paket 2 tatsaechlich einen dritten
  identischen `CreateConfig`-Konsumenten erzeugt, sonst bleibt es begruendet offen.

## Globale Arbeitsregeln fuer alle Pakete

1. Vor Beginn `git --no-pager status --short` und `git --no-pager log --oneline -5` sichern.
   Vorhandene fremde Aenderungen nicht ueberschreiben. Kein Amend, Rebase oder Push.
2. Jede Legacy-Klasse wird vor dem Edit ueber ihre Ledgerzeile ausgewaehlt. Keine Klasse ausserhalb
   der konkreten Paketliste mitnehmen. Nach einem Paket muss der erwartete Pending-Stand exakt
   stimmen: 38, 0, 0.
3. Vor jedem Move Klassen-Discovery in eine Textdatei schreiben. Die aktuelle Anzahl/Identitaet
   einzelner Testmethoden ist zwar keine globale Invariante, aber jeder nichttriviale historische
   Vertrag braucht eine explizite Zielzuordnung. Eine geringere Zielmenge ist nur fuer eine im
   Ledger konkret begruendete Konsolidierung/Trivialentfernung zulaessig.
4. Zielprofile: FastTests nur `Unit`/`Component`; IntegrationTests nur `Integration`/`Dogfood`/
   `Performance`/`Stress`. Genau ein Klassen-Trait. Keine redundanten Methodentraits. Unit und
   Component duerfen kein MSBuild, keinen Prozess und kein echtes Repository nutzen.
5. Bestehende Zielinfrastruktur wiederverwenden: `RoslynTestSolutionFactory`,
   `PreparedSolutionFixture`, `RecordingLintConsole`, `IsolatedFixtureLease`, `LoadedFixture`,
   `FixtureWorkspace`, `McpProcessHost` und das assembly-weite Prozessbudget. Kein neuer
   produktiver Seam, keine neue oeffentliche Produkt-API, kein `#if TESTING`, keine Kopie von
   `McpTestClient`, kein zweites universelles TestHelper-Sammelbecken.
6. Produktcode ist in Paket 1/2 gesperrt. Wenn eine Testmigration ohne Produktcodeaenderung nicht
   moeglich ist: Paket stoppen. Nur Paket 3 darf die obsolete
   `InternalsVisibleTo("AiNetLinter.Tests")`-Zeile entfernen.
7. Keine Assertion abschwaechen. Absolute Wall-Clock-Assertions der Performanceklasse werden nicht
   still geloescht, sondern in Paket 1 bewusst zu Messdatenerzeugung umgebaut; die relative
   Bewertung erfolgt in Paket 3 im Messbericht.
8. Keine neue globale xUnit-Collection. Echte globale Console-Umleitung darf die vorhandene
   `ConsoleTestCollection` in IntegrationTests erhalten; MCP-Dogfood teilt einen lazy,
   thread-sicheren Assembly-Host ohne Zwangsserialisierung.
9. Pro Paket maximal sechs Fixversuche insgesamt. Nach einem roten breiten Gate zuerst genau einen
   engsten Reproduktionslauf; danach Ursache fixen. Ein siebter Versuch ist verboten: Status mit
   TRX/Stacktrace dokumentieren und stoppen.
10. Nach jedem Testlauf pruefen, dass kein neu gestarteter `AiNetLinter`-/`dotnet`-Kindprozess
    verbleibt. Vorher/Nachher-PIDs erfassen; nur vom Test gestartete, eindeutig identifizierte
    Prozessbaeume beenden. Keine fremden Prozesse beenden.
11. Zwischencommits innerhalb eines Pakets sind erlaubt und erwuenscht. Jede verschobene Klasse,
    ihre geloeschte Legacyquelle und ihre Ledgerzeile gehoeren in denselben kohaerenten Commit.
    Ein Audit/Review erst nach dem ganzen Paket; keine neue Planung zwischen Commits.
12. Jeder Paketabschluss: Kategorie-/Dependency-/Callsite-/Ledgerguards, `dotnet build`,
    `git --no-pager diff --check` und Arbeitsbaumkontrolle. Keine Stressausfuehrung ohne neue,
    ausdrueckliche Nutzerfreigabe.

## Gemeinsame mechanische Baseline-Strategie

Fuer jede Paketliste werden zwei PowerShell-Arrays gepflegt: vollqualifizierte Legacy-Klassen und
vollqualifizierte Zielklassen. Filter werden ausschliesslich so erzeugt:

```powershell
$classFilter = ($classes | ForEach-Object { "FullyQualifiedName~$_." }) -join '|'
dotnet test <projekt> --no-build --no-restore --list-tests --filter $classFilter |
  Tee-Object TestResults/<name>-discovery.txt
```

Zusaetzlich wird vor/nach dem Move je Quelldatei die Zahl eigener `[Fact]`-/`[Theory]`-Methoden
und ihrer `[InlineData]`-Zeilen notiert. Die Ziel-Discovery wird gegen eine methodengenaue
Mappingtabelle im `step-029/step-result.md` geprueft. Bei Splitklassen steht dort jede alte Methode
mit neuer Zielklasse. Kein Ausgleich durch fremde Tests.

## Paket 1 — EPIC-6-Rest: CLI, MCP-Dogfood, Performance und Stress

### Deterministische Auswahl und Groesse

Exakt die folgenden 15 Ledgerklassen; derzeit 73 Testmethoden und mindestens 84 statisch sichtbare
Faelle. Nach dem Paket: exakt 38 `pending`.

| # | Legacyklasse | sichtbare Faelle | Ziel / Profil |
|---:|---|---:|---|
| 1 | `SourceFileCatalogRegisterMSBuildTests` | 3 | Split: Fast/Unit Reflection, Integration/Integration sequenziell, Integration/Stress 20-fach parallel |
| 2 | `CliCommandBuilderMcpLogTests` | 4 | Fast/Unit |
| 3 | `CliIntegrationTests` | 8 | Split: Mini-/Fehlervertraege Integration, echte Repo-Vertraege Dogfood; assertionloser DiagnosticDump im Ledger begruendet konsolidieren |
| 4 | `ProgramTests` | 6 | vier Parsermethoden Fast/Unit, zwei `Program.Main`-Vertraege Integration |
| 5 | `AuditCommandTests` | 1 | Integration/Integration |
| 6 | `CliBatchRegressionTests` | 1 | Integration/Integration gegen isolierte `SymbolGraphMini`-Kopie |
| 7 | `DocsCommandTests` | 14 | Integration/Integration, vorhandene Console-Exklusivitaet |
| 8 | `ListRulesCommandTests` | 9 | Fast/Unit mit `RecordingLintConsole` |
| 9 | `PlaybookCheckCommandTests` | 1 | Integration/Integration; kein stilles `return`, kontrollierte Fixture muss existieren |
| 10 | `SyncAgentRulesCommandTests` | 13 | reine Path-/Rendervertraege Fast/Unit, reale Datei-/Console-Vertraege Integration |
| 11 | `LoadFixtureBuilderTests` | 1 | Integration/Integration |
| 12 | `LoadFixtureMeasurementsTests` | 2 | Integration/Performance |
| 13 | `McpDocumentationSmokeTests` | 4 | Integration/Dogfood |
| 14 | `McpLiveRepositoryTests` | 16 | Integration/Dogfood |
| 15 | `McpTestClientParallelTests` | 1 | Integration/Stress |

Auswahlregel: genau die Ledgerzeilen mit diesen Klassennamen, unabhaengig von Dateireihenfolge.
Wenn eine fehlt, doppelt ist oder bereits einen anderen Status hat: stoppen.

### Zielstruktur, Hosts und Isolation

- Die bisherigen Klassennamen duerfen fuer Vollmoves bestehen bleiben. Splits heissen eindeutig:
  `SourceFileCatalogRegistrationPolicyTests`, `SourceFileCatalogRegistrationTests`,
  `SourceFileCatalogRegistrationStressTests`, `ProgramParsingTests`, `ProgramAdapterTests`,
  `CliRepositoryDogfoodTests`, `CliFixtureIntegrationTests`, `SyncAgentRulesPolicyTests` und
  `SyncAgentRulesFileIntegrationTests`.
- `LoadFixtureBuilder.cs` und `LoadFixtureHandle.cs` ziehen nach
  `AiNetLinter.IntegrationTests/Fixtures`; keine TestKit-Extraktion. Buildertest und
  Performanceklasse verwenden genau diese Kopie, die Legacykopie wird entfernt.
- Das heutige MCP-Prozessbudget wird testassembly-weit generalisiert: Typen
  `McpProcessLifetimeGate/Budget` mechanisch zu `SubprocessLifetimeGate/Budget` unter
  `IntegrationTests/Platform` umbenennen und alle bestehenden Integration-Aufrufer umstellen.
  Kapazitaet 2 und Lease-Lebensdauer bleiben unveraendert.
- `McpProcessHost` erhaelt nur in Testcode einen kleinen Zielrecord aus `RootPath` und optionalem
  besitzendem `IDisposable`. Der bestehende Fixture-Overload delegiert darauf. Ein neuer lazy
  `RepositoryMcpHostFixture` benutzt `SolutionRootLocator.Find()`, besitzt das Repository nicht,
  wird als Assembly-Fixture registriert und startet erst beim ersten Dogfood-Call. Er wird von
  beiden Dogfoodklassen geteilt. Kein Legacy-`McpTestClient`/`McpLiveRepositoryFixture` kopieren.
- Ein Integration-lokaler `CliProcessRunner` nutzt dasselbe `SubprocessLifetimeBudget` fuer die
  komplette Prozesslebensdauer, captured stdout/stderr, Timeout und Kill des eigenen Prozessbaums.
  Der Prozess-Callsiteguard scannt danach die gesamte Integration-Assembly und erlaubt Starts nur
  in den vorhandenen MCP-Ownern und diesem CLI-Runner.
- Der Stressvertrag mit 16 Tasks erzeugt je Task eine eigene `BaselineMiniFixtureWorkspace`,
  startet `McpProcessHost`, fuehrt mindestens `ListToolsAsync` oder einen repraesentativen Toolcall
  aus und disposed Host/Workspace im selben Task. Niemals 16 Hosts sammeln und erst spaeter
  disposen; mit Max-2-Lifetime-Leases waere das ein Deadlock.
- `LoadAsync_TwentyParallelCallsAcrossFixtures_AllSucceed` bleibt `Stress`. Der Reflectionvertrag
  bleibt Unit; nur der zweite sequenzielle echte Load ist normales Integrationprofil.
- Performance: beide Methoden schreiben strukturierte Samples (Szenario, Dateien/LOC, Iterationen,
  min/median/mean/max bzw. ColdStart) in xUnit-Output und pruefen nur fachliche Gueltigkeit,
  Vollstaendigkeit und endliche/nichtnegative Werte. Keine absolute Sekunden-Schwelle. Die
  kontrollierte relative Bewertung steht ausschliesslich im Paket-3-Messbericht.
- Echte Repo-CLI-/MCP-Tests erhalten `Category=Dogfood` und nutzen ausschliesslich
  `SolutionRootLocator.Find()` im Hauptarbeitsverzeichnis. Kein Worktree-Nachweis.

### Erlaubt / verboten

Erlaubt sind Moves/Splits der 15 Klassen, die genannten Integration-Testhosts, Anpassungen an
Integration-Assembly-Fixture/Callsiteguard, Ledger/CodeMap/Step-Result. Verboten sind Produktcode,
neue Produkt-Seams, Kopie des Legacy-MCP-Harness, Timeout-Erhoehung als Fix, globale
Collection-Serialisierung, Ausfuehrung von Dogfood/Performance/Stress in diesem Paket.

### Kommandos und Evidenz

1. `dotnet build --no-restore` vor Baseline.
2. Alle 15 Legacyklassen per `--list-tests` nach
   `TestResults/step029-p1-legacy-discovery.txt`; Sollzuordnung 73 Methoden / mindestens 84
   sichtbare Faelle, reale Discoveryzahl im Result festhalten.
3. Einmalige sichere Legacy-Baseline ohne die gemischte `CliIntegrationTests`, Dogfood,
   Performance und Stress: exakte Klassenfilter fuer `CliCommandBuilderMcpLogTests`,
   `ProgramTests`, `AuditCommandTests`, `CliBatchRegressionTests`, `DocsCommandTests`,
   `ListRulesCommandTests`, `PlaybookCheckCommandTests`, `SyncAgentRulesCommandTests`,
   `LoadFixtureBuilderTests` plus exakt die beiden Methoden
   `RegisterMSBuild_HasStaticLockField_ForThreadSafeRegistration` und
   `LoadAsync_SecondSequentialCall_DoesNotRepatchBuildHost`.

```powershell
dotnet test src/AiNetLinter.Tests --no-build --no-restore --filter $p1LegacySafeFilter `
  --logger "trx;LogFileName=step029-p1-legacy-safe.trx"
```

4. Nach Migration `dotnet build --no-restore`.
5. Alle Zielklassen `--list-tests` nach `step029-p1-target-discovery.txt`; jede der 73 alten
   Methoden muss gemappt sein. Der DiagnosticDump darf nur als explizite Konsolidierung entfallen;
   neue Coverage-Audit-Tests duerfen die Zahl erhoehen.
6. Nur neue Unit-/Integration-Methoden (keine Dogfood/Performance/Stress) plus Fast Kategorie-,
   static- und runtime-Dependencyguard in `step029-p1-fast-safe.trx` und Integration Kategorie-,
   Process-Callsite-/Ledgerguard in `step029-p1-integration-safe.trx` ausfuehren.
7. Stress nur discovern, nicht ausfuehren:

```powershell
dotnet test src/AiNetLinter.IntegrationTests --no-build --no-restore --list-tests `
  --filter "Category=Stress&(FullyQualifiedName~SourceFileCatalogRegistrationStressTests.|FullyQualifiedName~McpTestClientParallelTests.)" |
  Tee-Object TestResults/step029-p1-stress-discovery.txt
```

   Erwartung: beide Klassen und beide Stressmethoden sichtbar. Dogfood/Performance ebenfalls nur
   discovern und im Result zaehlen.
8. Ledger-/Legacyguards: `TestMigrationLedgerConsistencyTests|LegacyProjectBuildGateTests`,
   `step029-p1-ledger.trx`, exakt 38 pending. Danach `git --no-pager diff --check`.

### Commits und Stopkriterien

- Commit A: allgemeiner Subprozess-/CLI-Host + CLI/MSBuild/Builder-Migration samt Ledger.
- Commit B: MCP-Dogfood-/Performance-/Stressmigration samt Ledger.
- Commit C: Paket-1-Checkpoint in `step-029/step-result.md`, CodeMap und State.
- Stoppen bei Produktcodebedarf, fehlender historischer Methode, Dogfood aus Worktree,
  unbudgetierter Process-/Transport-Callsite, Stressausfuehrung, verbliebenem Legacy-Duplikat,
  Pending ungleich 38, Prozessleck oder sechs verbrauchten Fixversuchen.

## Paket 2 — EPIC-7-Restmigration: alle 38 verbleibenden Klassen

### Deterministische Auswahl und Groesse

Vorbedingung: Paket 1 committed, Ledger exakt 38 pending. Auswahl ist danach **jede** verbleibende
`pending`-Zeile, sortiert nach Quelldatei. Die konkrete aktuelle Liste lautet:

| # | Klasse | sichtbare Faelle | Ziel |
|---:|---|---:|---|
| 1 | `AutoFixerTests` | 4 | Integration/Integration (echte Schreibgrenze) |
| 2 | `ClassInfoCollectorTests` | 2 | Fast/Unit |
| 3 | `ControlFlowResilienceTests` | 16 | Fast/Unit |
| 4 | `DiffImpactAnalyzerTests` | 1 | Fast/Unit |
| 5 | `LinterEngineTests` | 10 | Split: 8 In-Memory Fast/Component, 2 Datei-Suppression Integration |
| 6 | `NamespaceFilterTests` | 2 | Fast/Unit |
| 7 | `NullCoalescingInitializerClassifierTests` | 6 | Fast/Unit |
| 8 | `PlaybookGeneratorRound2Tests` | 8 | Split: 6 In-Memory Fast/Component, 2 GenerateAsync-Dateivertraege Integration |
| 9 | `ResultPatternNamespaceTests` | 6 | Fast/Unit |
| 10 | `RuleRegistryTests` | 10 | Fast/Unit |
| 11 | `ScopeImmutabilityTests` | 7 | Fast/Unit |
| 12 | `StaticTestSentinelExemptionTests` | 9 | Fast/Component |
| 13 | `TestCoverageResolverTests` | 3 | Fast/Unit |
| 14 | `TestProjectDetectorSuffixTests` | 14 | Fast/Unit |
| 15 | `ViolationDescriptionTests` | 1 | Fast/Unit |
| 16 | `PerformanceProfilerTests` | 3 | Integration/Integration (Config-/Report-Dateien; nicht Performanceprofil) |
| 17 | `FalsePositiveExtensionsTests` | 12 | Fast/Unit |
| 18 | `FalsePositiveTests` | 15 | Fast/Unit |
| 19 | `TD016aRefactorTests` | 8 | Integration/Integration, auf Ziel-`FixtureWorkspace`-Typen umstellen |
| 20 | `HotspotMapBuilderTests` | 3 | Integration/Integration |
| 21 | `SkeletonStableIdTests` | 1 | Fast/Component gegen `McpInMemoryTestContext`, keine Catalog-Collection |
| 22 | `SkeletonSyntaxWalkerTests` | 11 | Fast/Unit |
| 23 | `AIContextFootprintDeduplicationTests` | 5 | Fast/Unit |
| 24 | `CognitiveComplexityGuidanceTests` | 5 | Fast/Unit |
| 25 | `CognitiveComplexityWalkerTests` | 1 | Fast/Unit |
| 26 | `FileLimitGuidanceTests` | 3 | Fast/Unit |
| 27 | `MaxDirectoryChildrenTests` | 9 | Integration/Integration |
| 28 | `MethodLineCounterTests` | 4 | Fast/Unit |
| 29 | `PostAnalysisChecksPathOverrideTests` | 5 | Fast/Unit |
| 30 | `DebtReportBuilderHeaderTests` | 3 | Fast/Unit |
| 31 | `DebtReportBuilderTests` | 1 | Integration/Integration |
| 32 | `LinterErrorFormatterTests` | 6 | Fast/Unit |
| 33 | `McpLintConsoleTests` | 3 | Fast/Unit |
| 34 | `OutputRootResolverTests` | 3 | Integration/Integration |
| 35 | `PathNormalizerTests` | 8 | Fast/Unit |
| 36 | `RuleLegendRegistryTests` | mindestens 5 | Fast/Unit; reale Theory-Discovery ist Baseline |
| 37 | `ViolationMarkdownFormatterTests` | 30 | Fast/Unit |
| 38 | `ViolationSummaryBuilderTests` | 4 | Fast/Unit |

Aktuelle Quellbaseline: 228 Testmethoden und mindestens 247 statisch sichtbare Faelle. Nach Paket:
exakt 0 pending. Die Groesse ist als 38-Klassen-Move explizit freigegeben; keine Unterkohorten-
Planungsrunde.

### Mechanische Umsetzung und Abweichungen, die stoppen muessen

- Vollmoves behalten Dateiname, Klasse, Testmethoden und Assertions; nur Rootnamespace, Zielpfad
  und genau ein Klassen-Trait aendern. Fast-Helperbedarf wird in den bestehenden
  `AiNetLinter.FastTests/TestHelper.cs` integriert, nicht in eine neue Helperklasse.
- `TestLintConsole`-Konsumenten verwenden `RecordingLintConsole`; keine weitere Kopie.
- Lokale Adhoc-Solution-Builder in `LinterEngineTests`, `PlaybookGeneratorRound2Tests` und
  `AutoFixerTests` werden auf `RoslynTestSolutionFactory` umgestellt. Kein Produktcode.
- Die zwei diskbasierten `LinterEngineTests` wechseln als
  `LinterEngineFileSuppressionIntegrationTests`; die acht uebrigen bleiben
  `LinterEngineTests` in Fast/Component. Die zwei `GenerateAsync`-Dateimethoden wechseln als
  `PlaybookGeneratorFileIntegrationTests`; sechs reine Methoden bleiben Fast/Component.
- `SkeletonStableIdTests` verwendet `McpInMemoryTestContext.Solution`, sucht dort `Greeter.cs` und
  ruft denselben `ExtractFromDocumentAsync`-Vertrag auf. `SymbolGraphCatalogFixture` und
  `[Collection("SymbolGraphCatalog")]` entfallen; keine neue Seam.
- `TD016aRefactorTests` wird nicht gegen geloeschte Legacytypen konserviert: Zielklasse
  `FixtureWorkspaceArchitectureTests` prueft die vorhandenen Integration-Workspace-Typen gegen
  `FixtureWorkspace` und die Abwesenheit eigener Kopier-/Roothelper. Die acht Datenfaelle bleiben.
- Temp-Dateien/-Verzeichnisse sind pro Test eindeutig und werden im `finally`/`Dispose` entfernt.
  Kein geteilter mutable Workspace, keine neue serielle Collection.
- Produktseitiger Coverage-Audit je Bereich: oeffentliche/interne Einstiegspunkte und Branches
  lesen; neue nichttriviale Luecken duerfen nur als Tests in der billigsten korrekten Ebene
  ergaenzt werden. Jede notwendige Produktcodeaenderung stoppt dagegen das Paket.
- TD-007 nur anfassen, wenn eine dritte identische Skeleton-`CreateConfig`-Methode entsteht; dann
  einen schmalen lokalen Skeleton-Testhelper verwenden und TD-007 schliessen. Sonst keine
  Zweier-Abstraktion erzwingen.

### Kommandos und Evidenz

1. Vor Move: alle 38 Klassen `--list-tests` nach
   `TestResults/step029-p2-legacy-discovery.txt`; zusaetzlich einmal:

```powershell
dotnet test src/AiNetLinter.Tests --no-build --no-restore --filter $p2LegacyFilter `
  --logger "trx;LogFileName=step029-p2-legacy.trx"
```

2. Nach Move `dotnet build --no-restore`.
3. Exakte Zielallowlists getrennt ausfuehren:

```powershell
dotnet test src/AiNetLinter.FastTests --no-build --no-restore --filter $p2FastFilter `
  --logger "trx;LogFileName=step029-p2-fast.trx"
dotnet test src/AiNetLinter.IntegrationTests --no-build --no-restore --filter $p2IntegrationFilter `
  --logger "trx;LogFileName=step029-p2-integration.trx"
```

   Zusammen muessen mindestens die 228 historischen Methoden/247 sichtbaren Faelle methodengenau
   gemappt sein; Mehrzahl nur durch dokumentierte Coverage-Audit-Zugaenge. Keine fremde Klasse zum
   Zaehlen verwenden.
4. Fast Zielallowlist plus `TestCategoryProfileGuardTests`, beide static Dependencyguards und
   Runtime-Abschlussguard in einem Host: `step029-p2-fast-guards.trx`.
5. Integration Zielallowlist plus Kategorie-/Process-Callsiteguard und beide Migrationsguards:
   `step029-p2-integration-guards.trx`; Ledger exakt 0 pending, aber Legacyprojekt baut bis Paket 3.
6. `git --no-pager diff --check`; kein Dogfood-, Performance- oder Stressprofil.

### Commits und Stopkriterien

- Commit A: Fast-Vollmoves und beide In-Memory-Splits samt Ledger.
- Commit B: Datei-/Fixture-Integrationvertraege samt Ledger, pending=0.
- Commit C: Paket-2-Checkpoint in Step-Result/CodeMap/State.
- Stoppen bei Pending ungleich 0, fehlendem Mapping, neuer Produkt-Seam, MSBuild/Process/Repo im
  Fast-Ziel, fehlendem Temp-Cleanup, neuem globalem Collectionzwang, rotem Legacy-Build,
  Prozessleck oder sechs Fixversuchen.

## Paket 3 — Legacy-Loeschung, Abschlussprofile und Messbericht

### Deterministische Loesch- und Bereinigungsregel

Vorbedingung: Paket 2 committed; Ledgerguard gruen, 0 pending; jede der 183 Zeilen ist
`migrated`, `consolidated` oder begruendet `removed-trivial`; unter `src/AiNetLinter.Tests` gibt es
keine Testklasse mehr.

1. `git ls-files src/AiNetLinter.Tests` in
   `TestResults/step029-p3-legacy-files-before-delete.txt` sichern. Dann **alle** dort gelisteten
   Restdateien loeschen (Support, `.csproj`, `xunit.runner.json`); keine Archivkopie.
2. `AiNetLinter.slnx` auf die vier Zielprojekte reduzieren. In
   `LinterEngine.cs` nur das obsolete IVT fuer `AiNetLinter.Tests` entfernen; IVT fuer Fast/
   Integration bleibt.
3. `LegacyProjectBuildGateTests` und `TestMigrationLedgerConsistencyTests` zu einem finalen
   `MigrationCompletionGuardTests` konsolidieren: 0 pending, alle Zielorte vorhanden,
   Legacyprojekt weder in Solution noch auf Platte. Der Guard darf ein fehlendes Legacyverzeichnis
   nicht enumerieren.
4. `.runsettings` behaelt nur `ResultsDirectory`; das fixe `latest.trx` entfaellt. Alle Gates und
   AGENTS-Befehle nennen eigene `LogFileName`-Werte.
5. `AGENTS.md`: Projektueberblick, vier Solutionprojekte, Standardgate und TRX-Diagnose auf finalen
   Vertrag umstellen; Quarantaene-/Legacy-Impactfilter entfernen. Verbindliches Gate bleibt exakt
   `dotnet build`, Fast `Category!=Stress`, Integration `Category!=Stress`.
6. `.agents/rules/AiNetLinterRichtlinien.mdc`: Legacy-/`latest.trx`-Text und pending-MCP-Hinweis
   entfernen; keine Gate-Befehle duplizieren. `Docs/ROADMAP.md` beschreibt die aktuelle
   Fast/Integration/TestKit-Struktur sachlich. `Docs/agent-api.md`-Self-Repo-Beispiel gegen den
   aktuellen Tooloutput aktualisieren. `Docs/configuration.md` nur aendern, wenn Codevergleich
   eine konkret falsche Projekt-/TestSentinel-Aussage zeigt. README/integration bleiben bei
   generischen, weiterhin richtigen Nutzeraussagen unveraendert.
7. Repositoryweite Suche:

```powershell
rg -n --glob '!tasks/speedup-tests/**' --glob '!TestResults/**' `
  "AiNetLinter\.Tests|src/AiNetLinter.Tests|latest\.trx" AGENTS.md README.md Docs .agents src tests .runsettings AiNetLinter.slnx rules.json
```

   Aktive konkrete Legacyreferenzen muessen weg. Historische/generische Beispiele und bewusst
   getestete Pfadstrings nur mit sachlicher Begruendung im Step-Result behalten.
8. TD-008 schliessen, wenn keine Legacy-Helperkopie/-referenz bleibt. TD-010 schliessen, wenn
   `FixtureWorkspaceBase` geloescht und nur Zielprimitiven bleiben. TD-007 nach obiger Regel.

### Abschlussgates und Messung

Kein Parallelstart von Profilen. Vor jedem Prozessprofil PIDs sichern und danach auf neue
uebriggebliebene Prozessketten pruefen.

```powershell
dotnet build --no-restore
dotnet test src/AiNetLinter.FastTests --no-build --no-restore --filter "Category!=Stress" `
  --logger "trx;LogFileName=step029-final-fast-gate.trx"
dotnet test src/AiNetLinter.IntegrationTests --no-build --no-restore --filter "Category!=Stress" `
  --logger "trx;LogFileName=step029-final-integration-gate.trx"
```

Diese drei Befehle sind das AGENTS-Abschlussgate und muessen gruen sein. Danach fuer den
Vorher-/Nachher-Bericht drei Round-Robin-Runden, immer `--no-build`, eigene TRX:

```text
Runde 1: Unit -> Component -> Integration -> Dogfood -> Performance
Runde 2: Unit -> Component -> Integration -> Dogfood -> Performance
Runde 3: Unit -> Component -> Integration -> Dogfood -> Performance
```

Namensschema:
`step029-final-unit-{1..3}.trx`, `step029-final-component-{1..3}.trx`,
`step029-final-integration-{1..3}.trx`, `step029-final-dogfood-{1..3}.trx`,
`step029-final-performance-{1..3}.trx`. Commands jeweils:

```powershell
dotnet test <Zielprojekt> --no-build --no-restore --filter "Category=<Profil>" `
  --logger "trx;LogFileName=<Name>.trx"
```

- Messbericht `tasks/speedup-tests/final-measurement.md`: Maschine/Bedingungen, separate Buildzeit,
  je Profil Testzahl, Wall Clock, aggregierte Testdauer, Median, Minimum/Maximum bzw. Streuung,
  Fremdlastnotiz; Vergleich mit `baseline-measurement.md` nur fuer semantisch vergleichbare
  Profile. Kein Bestlauf, kein absolutes SLO, Dogfood separat.
- `Stress` wird nach aktueller Nutzeranweisung **nicht ausgefuehrt**. Nur Build und Discovery:

```powershell
dotnet test src/AiNetLinter.IntegrationTests --no-build --no-restore --list-tests `
  --filter "Category=Stress" | Tee-Object TestResults/step029-final-stress-discovery.txt
```

  Im Messbericht und Task-Summary exakt: „migriert und kompiliert; nicht ausgefuehrt, weil keine
  neue ausdrueckliche Nutzerfreigabe vorlag“. Niemals „Stress gruen“ behaupten. Die aktuelle
  Nutzeranweisung ist die dokumentierte Abweichung von der aelteren Konzeptforderung, Stress am
  Task-Ende einmal laufen zu lassen.
- Danach Guards gezielt, `git --no-pager diff --check`, kein Prozessleck.
- Drift-Audit gemaess `.agents/skills/drift-audit/SKILL.md`: ausschliesslich MCP
  `find_duplicates(scopeDir="src", minTokens=20)`, alle `exact`-Cluster entscheiden, je
  `near`-Cluster 1-2 Beispiele pruefen; fuer zentrale neue Helper optional
  `mode="refactoring-drift"`. Wenn MCP nicht verfuegbar ist: Abschluss stoppen, nicht durch
  Text-Grep simulieren.
- Erst nach allen Gates: Roadmap EPIC-6/7 schliessen, `task-summary.md` schreiben, Task-State
  `done`; Stress-Waiver und ggf. offen gebliebenes TD-007 sichtbar nennen.

### Commits und Stopkriterien

- Commit A: Legacyprojekt/Support/Solution/IVT/Finalguard und aktive Doku bereinigen.
- Commit B: Abschluss-TRX-Auswertung, Messbericht, Drift-Audit-Ergebnis, Roadmap/State/Summary.
- Stoppen bei pending >0, noch referenziertem Legacyprojekt, rotem Build/Fast-/Integrationgate,
  fehlendem Zielort, falschem Hauptarbeitsverzeichnis fuer Dogfood, Prozessleck, signifikanter
  kontrollierter Performance-Regression ohne Ursachenanalyse, nicht verfuegbarem Drift-Audit-MCP
  oder sechs Fixversuchen. Stress nie ohne neue Nutzerfreigabe starten.

## Einziger kopierbarer Prompt fuer den externen Coder

```text
Arbeite im Repository C:\Daten\Entwicklung\Ralf\AiNetLinter den gesamten Rest von
tasks/speedup-tests autonom ab. Lies zuerst vollstaendig AGENTS.md, beide Dateien unter
.agents/rules/, .agents/Agent-Scaffolding/dev-loop/drift-loop/skills/coder/SKILL.md,
tasks/speedup-tests/konzept.md, roadmap.md, codemap.md, tech-debt.md,
test-migration-ledger.md, step-028/step-review.md, step-029/step-plan.md und
master-low-cost-handoff.md.

master-low-cost-handoff.md ist der einzige Detailplan. Fuehre Paket 1, danach Paket 2, danach
Paket 3 ohne neue Planer-/Kritiker-Schleife und ohne auf neue Planung zu warten aus. Setze nach
jedem Paket die dort genannten kohaerenten deutschen Conventional Commits mit Suffix
[speedup-tests], aktualisiere step-029/step-result.md um einen exakten Statuspunkt und fahre bei
gruenen Paketgates selbststaendig fort. Kein Amend, Rebase oder Push.

Halte die deterministischen Klassenlisten, Zielprofile, Fixture-/Host-/Isolationregeln,
Methoden-/Discovery-Baselines, TRX-Namen, Pending-Zahlen, maximal sechs Fixversuche und
Stopkriterien wortgetreu ein. Produktcode ist in Paket 1/2 gesperrt; keine spekulative
Produkt-Seam, keine Assertion-Abschwaechung, kein Legacy-McpTestClient-Copy, keine globale
Collection. Bei jeder Migration Legacyquelle, Zieltest und Ledgerzeile im selben Commit.

Stress wird nur migriert, kompiliert und discovered, niemals ausgefuehrt, solange keine neue
ausdrueckliche Nutzerfreigabe vorliegt. Dogfood laeuft nur im Hauptarbeitsverzeichnis und erst im
Abschlusspaket. Fuehre keine Profile parallel aus und pruefe nach Prozesslaeufen auf Leaks.

Wenn eine zwingende Stopbedingung eintritt, committe keinen halbfertigen Paketabschluss. Schreibe
den Blocker mit kleinstem Repro, TRX/Stacktrace, aktuellem Diff und verbrauchten Fixversuchen in
step-029/step-result.md und stoppe. Andernfalls beende Paket 3 erst nach dotnet build, FastTests
Category!=Stress, IntegrationTests Category!=Stress, dreifacher Profilmessung, dokumentierter
Stress-Nichtausfuehrung und dem MCP-basierten drift-audit.
```
