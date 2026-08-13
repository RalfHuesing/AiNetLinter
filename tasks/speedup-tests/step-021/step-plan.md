---
status: done (pending audit)
type: step-plan
task: speedup-tests
step: 021
corrects: null
title: "MSBuild-/Baseline-/Datei-/Refresh-Super-Step mit 22 Klassen"
epic: EPIC-5
estimated_risk: high
step_type: batch
items:
  - id: item-01
    title: "Geladene Fixture-Lease und MSBuild-Loadbudget konsolidieren"
    source: "konzept.md Leitplanken 2, 4, 6; tech-debt.md#TD-009"
  - id: item-02
    title: "Reine Baseline-/Cache-Policyvertraege nach FastTests migrieren"
    source: "test-migration-ledger.md Baseline/Cache; konzept.md Leitplanke 1"
  - id: item-03
    title: "Baseline-/Catalog-/Web-Dateivertraege nach IntegrationTests migrieren"
    source: "test-migration-ledger.md Baseline; konzept.md Leitplanken 1, 4"
  - id: item-04
    title: "Cache- und LinterEngine-Cache-/Restore-Vertraege isolieren"
    source: "test-migration-ledger.md Cache/Core; konzept.md Leitplanken 2, 7"
  - id: item-05
    title: "MCP-Dateitools auf read-only Host und isolierte Leases migrieren"
    source: "test-migration-ledger.md Mcp/Tools; step-018/019 Abgrenzung"
  - id: item-06
    title: "MCP-Dateidiscovery und Refresh/Staleness migrieren"
    source: "test-migration-ledger.md McpCodeGraphServer*; konzept.md Mutationsmodell"
  - id: item-07
    title: "Coverage, Ledger und gezielte Gates fuer 22 Klassen abschliessen"
    source: "konzept.md Leitplanken 7 bis 9; test-migration-ledger.md"
created_by: planer
created_by_model: gpt-5.6-sol
created_by_model_knowledge_cutoff: nicht ausgewiesen
created_at: 2026-08-13
related_to:
  - step-007
  - step-013
  - step-018
  - step-020
---

# Step 021: MSBuild-/Baseline-/Datei-/Refresh-Super-Step mit 22 Klassen

## Bezug

- **Task:** `speedup-tests`
- **Epic:** `EPIC-5` aus `roadmap.md` — echte MSBuild-, Fixture-, Baseline-, Datei- und
  Refresh-Vertraege in die Infrastruktur-Assembly migrieren und reine Teilvertraege auf die
  guenstigste Ebene senken.
- **Konzept-Referenz:** Muss-Haben und Technische Leitplanken §1, §2, §4, §6 bis §9:
  read-only Solutions teilen, Mutationen isolieren, MSBuild-/Dateisystemgrenzen als Integration
  kategorisieren, Loadparallelitaet begrenzen und Kohorten atomar aus Legacy entfernen.
- **Vorheriger Step:** Step 020 ist durch Re-Audit `28b3cb4` approved; EPIC-4 ist abgeschlossen.

## Aktueller Projektzustand (JIT-Kontext)

Die Kohorte umfasst exakt **22 historische Klassen / 22 Legacy-Dateien / 99 Testmethoden** mit
2.672 Quellzeilen. Der groesste Anteil sind Moves mit Namespace-/Fixtureanpassung; erwartet werden
etwa **34 bis 38 geaenderte Dateien** und **ca. 700 bis 900 inhaltliche Diffzeilen** neben erkannten
Renames. Das liegt am 800-Zeilen-Richtwert, bleibt aber unter dem 40-Dateien-Super-Step-Deckel und
ist durch eine gemeinsame Lade-/Datei-Lebensdauer sowie gemeinsame Gates logisch geschlossen.

Vorhandene Plattform:

- `IsolatedFixtureLease` kopiert kanonische Fixtures ohne `bin`/`obj` und ist fuer mutable Tests
  geeignet.
- `MsBuildFixtureHost` besitzt assembly-weit eine read-only `BaselineMini`-Solution.
- `FindSymbolFileAdapterFixture` besitzt dieselbe Kombination aus Lease, Catalog und Disposal fuer
  `SymbolGraphMini`; dieser zweite reale Konsument macht TD-009 nun konkret konsolidierbar.
- `FilterMiniFidelityTests` und `SkeletonMapBuilderAdapterTests` zeigen die etablierten echten
  MSBuild-/Adaptermuster; FastTests-Guards verbieten den Load in der schnellen Assembly.
- IntegrationTests erlaubt Collection-Parallelitaet mit dynamischer Threadzahl. Neue direkte
  `SourceFileCatalog.LoadAsync`-Aufrufe duerfen daher nicht unbudgetiert aus vielen parallel
  laufenden Klassen starten. Read-only Hosts duerfen parallel konsumiert werden; mutable Catalogs,
  Server, Configs, Baselines und Cacheverzeichnisse werden nie geteilt.

### Verbindliches Klasseninventar

**Baseline (10 Klassen, 37 Methoden):**
`BaselineCliTests` (4), `BaselineComparerTests` (4), `BaselineReaderWriterTests` (2),
`BaselineViolationFilterTests` (2), `FileChecksumCalculatorTests` (1),
`FileSystemExclusionHelpersTests` (8), `ProjectRestoreStateTests` (7),
`SourceFileCatalogBlazorPartialTests` (3), `SourceFileCatalogTests` (4), `WebBaselineTests` (2).

**Cache/Core (5 Klassen, 19 Methoden):**
`AnalysisCacheManagerIsolationTests` (4), `AnalysisCacheManagerTests` (7),
`CacheEntryMapperTests` (4), `LinterEngineCacheTests` (2),
`LinterEngineProjectRestoreTests` (2).

**MCP-Dateitools und Refresh (7 Klassen, 43 Methoden):**
`GetIndexScopeToolTests` (8), `GetServerHealthToolTests` (6), `ReloadConfigToolTests` (7),
`SearchPatternToolTests` (10), `McpCodeGraphServerFileDiscoveryTests` (3),
`McpCodeGraphServerStalenessMtimeCacheTests` (3), `McpCodeGraphServerTests` (6).

Bestehende Kategorien sind teilweise falsch (`Unit` trotz Temp-Datei/Catalog) und zwei
`SourceFileCatalogTests`/`SourceFileCatalogRegisterMSBuildTests` mischen Kategorien pro Klasse.
Zielklassen besitzen jeweils genau eine Kategorie. Read-only Fixture-Sharing darf keine
serialisierende Collection erzeugen. Einzig ein wirklich globales, nicht isolierbares
Cache-Ausgabeverzeichnis darf eng und mit XML-Rationale serialisiert werden, falls der Audit
bestaetigt, dass kein vorhandener injizierbarer Pfad genutzt werden kann; keine breite
`ConsoleTestCollection`.

## Intention

Der Step migriert die groesste Kohorte, die dieselbe Datei-/MSBuild-Plattform und dasselbe
Mutationsmodell teilt: reine Baseline-/Cache-Entscheidungen laufen schnell, echte Loads und
Dateimutationen hermetisch in IntegrationTests. Gemeinsame read-only Catalogs werden nur einmal
geladen, mutable Szenarien erhalten eigene Leases, und ein schmales Loadbudget verhindert, dass
parallel gestartete Integrationklassen den MSBuild-BuildHost ungebremst vervielfachen.

Der Step schliesst EPIC-5 noch nicht zwingend: Konfigurations-/Suppression-Dateifamilien und der
gemischte MSBuild-Registrierungs-/Stressvertrag bleiben fuer den naechsten JIT-Abgleich offen.

## Konkrete Änderungen

### item-01: Geladene Fixture-Lease und MSBuild-Loadbudget konsolidieren (Risiko: medium)

- Unter `src/AiNetLinter.IntegrationTests/Platform/` einen kleinen IntegrationTests-lokalen Owner
  fuer **eine** `IsolatedFixtureLease` plus **einen** `SourceFileCatalog` bereitstellen, bevorzugt
  als `IAsyncDisposable` mit `CreateAsync(fixtureName)`, `RootPath`, `Catalog` und `Solution`.
- Ein statisches, kleines `SemaphoreSlim`-Budget (Richtwert 2 parallele Loads) muss exakt den
  `SourceFileCatalog.LoadAsync`-Lebensabschnitt umfassen; nach erfolgreichem Load wird der Permit
  freigegeben, read-only Nutzung bleibt parallel. Cancellation/Exception darf keinen Permit leaken.
- `MsBuildFixtureHost` und `FindSymbolFileAdapterFixture` auf diesen Owner umstellen. Neue
  `SymbolGraphMini`-read-only-Konsumenten erhalten eine assembly-weite Fixture oder einen
  aequivalent einmalig geladenen Host ohne Collection-Serialisierung. `BaselineMini` bleibt im
  vorhandenen assembly-weiten Host. Blazor/CompileError/SingleCompileError werden nur dort geladen,
  wo ihr eigener MSBuild-Vertrag erforderlich ist.
- `FilterMiniFidelityTests` und weitere direkte Integration-Loads nur dann mitziehen, wenn dies
  mechanisch noetig ist, um das Loadbudget tatsaechlich fuer alle heutigen direkten
  IntegrationTests-Loads durchzusetzen; keine fachlichen Assertions aendern.
- TD-009 danach als geschlossen dokumentieren: Ownership/Disposal liegt in einem gemeinsamen
  Helper mit mehreren realen Konsumenten. TD-010 bleibt offen, weil zahlreiche pending Legacy-
  Fixtures weiterhin `FixtureWorkspaceBase` brauchen; keine breite Legacy-Basisklassenmigration.
- TD-004 im ohnehin beruehrten `MsBuildFixtureHostTests` mechanisch schliessen: den verbotenen
  Step-Verweis aus dem XML-Kommentar entfernen und nur die dauerhafte technische Rationale
  stehenlassen. Keine Assertion oder Teststruktur aendern.
- Plattformtests belegen Owner-Disposal, getrennte Lease-Pfade, read-only Wiederverwendung und das
  maximale parallele Loadbudget ohne absichtlich hohe Last.

### item-02: Reine Baseline-/Cache-Policyvertraege nach FastTests migrieren — 5 historische Klassenanteile, 14 Methoden (Risiko: low)

- Vollstaendig nach FastTests: `BaselineComparerTests`, `BaselineViolationFilterTests`,
  `FileChecksumCalculatorTests`, `CacheEntryMapperTests`.
- Aus `SourceFileCatalogTests` die drei reinen `ShouldIncludeProject`-/`IsGeneratedPath`-Vertraege
  in eine klar benannte FastTests-Policyklasse uebernehmen; In-Memory-Projekte mit der vorhandenen
  `RoslynTestSolutionFactory` statt lokalem Adhoc-Setup materialisieren, sofern semantisch gleich.
- Kategorien `Unit` fuer solutionfreie Vergleiche/Mapper und `Component` nur fuer echte Roslyn-
  Solution-Policy. Keine Datei-, MSBuild-, Prozess- oder Repozugriffe; Dependency-Guard muss gruen.
- Der eine Catalog-Load aus derselben historischen Klasse geht nach item-03. Ledger erhaelt einen
  maschinell gueltigen primaeren Zielpfad; die zusaetzliche Zielassembly wird wie beim Find-Symbol-
  Schnitt in einer Coverage-Notiz dokumentiert.

### item-03: Baseline-/Catalog-/Web-Dateivertraege nach IntegrationTests migrieren — 7 Klassenanteile, 27 Methoden (Risiko: high)

- `BaselineCliTests`, `BaselineReaderWriterTests`, `FileSystemExclusionHelpersTests`,
  `ProjectRestoreStateTests`, `SourceFileCatalogBlazorPartialTests`, `WebBaselineTests` und den
  `LoadAsync_MiniFixture_ReturnsSourceFiles`-Adapter aus `SourceFileCatalogTests` migrieren.
- Read-only `BaselineMini` ueber `MsBuildFixtureHost`; jeder Test, der Config, Source, Webdatei,
  Baseline oder Restore-Mtime veraendert, arbeitet auf eigener geladener/ungeladener
  `IsolatedFixtureLease`. Niemals die kanonische Fixture unter `tests/Fixtures` mutieren.
- `BlazorPartialMini` bleibt echter Razor-SDK-/Generator-/MSBuild-Vertrag und darf nicht in-memory
  nachgebaut werden. Seine drei Methoden teilen innerhalb der Klasse genau einen read-only Load.
- `RecordingLintConsole` statt Legacy-`TestLintConsole`; Cleanup ueber Owner/Lease, keine neuen
  allgemeinen TestHelper-Kopien. In-process Command-Aufrufe bleiben Integration, sind aber keine
  Prozessvertraege.
- TD-008 wird nur entlang der tatsaechlich migrierten Konsumenten reduziert: vorhandene
  Zielassembly-Primitiven statt Kopieren weiterer Legacy-Helper verwenden. Die parallelen
  Helperdateien selbst bleiben bis zu ihren restlichen Legacy-Konsumenten/EPIC-7 bestehen und
  werden nicht vorzeitig zu einem Allzweckhelper zusammengezogen.

### item-04: Cache- und LinterEngine-Cache-/Restore-Vertraege isolieren — 4 Klassen, 15 Methoden (Risiko: high)

- `AnalysisCacheManagerIsolationTests`, `AnalysisCacheManagerTests`, `LinterEngineCacheTests` und
  `LinterEngineProjectRestoreTests` nach IntegrationTests migrieren; ihre Kategorien von `Unit`
  auf `Integration` korrigieren, weil Hash-/Roundtrip-/TTL-/Restore-/Cache-Bypass-Vertraege reale
  Dateien, Mtimes oder persistierten Cachezustand pruefen.
- Jede Klasse/Testinstanz erhaelt einen eindeutigen Temp-Root. Keine Loeschung eines breit
  geteilten Assembly-Ausgabeordners. Falls `LinterEngine` den Cacheort unvermeidbar aus dem
  Assemblypfad ableitet, den bestehenden engsten globalen Vertrag mit einer eigenen begruendeten
  Collection oder einem statischen Lock schuetzen; keine Produkt-Seam ohne reproduzierbaren Bedarf.
- `CacheEntryMapperTests` bleibt als reine Mappingklasse in item-02/FastTests.
- Bestehende Parallelitaetsvertraege innerhalb `AnalysisCacheManagerTests` bleiben erhalten, werden
  aber nicht als Stress kategorisiert: sie pruefen thread-safe In-Process-Zugriffe ohne MSBuild oder
  Prozesse.

### item-05: MCP-Dateitools auf read-only Host und isolierte Leases migrieren — 4 Klassen, 31 Methoden (Risiko: high)

- `GetIndexScopeToolTests`, `GetServerHealthToolTests`, `ReloadConfigToolTests` und
  `SearchPatternToolTests` nach IntegrationTests migrieren und einheitlich `Integration`
  kategorisieren.
- Read-only SymbolGraph-Faelle konsumieren den einmal geladenen Host. Tests fuer `obj`/`bin`/
  Worktree-Ausschluss, Call-Log, Config-Discovery/-Reload und CompileError verwenden eigene Leases
  und Catalogs; keine mutable Datei neben einem geteilten Snapshot.
- CompileError-/SingleCompileError-/SymbolGraph-Merkmale aus den kanonischen Disk-Fixtures nutzen;
  keine neuen In-Memory-Seams fuer echte Datei-Inventar- oder Configvertraege.
- Tool-Dispatch bleibt in-process. Kein MCP-Transport, Prozessstart, Framing, Loading-Retry oder
  Live-Repo-Smoke wird in diesen Step gezogen.

### item-06: MCP-Dateidiscovery und Refresh/Staleness migrieren — 3 Klassen, 12 Methoden (Risiko: high)

- `McpCodeGraphServerFileDiscoveryTests`, `McpCodeGraphServerStalenessMtimeCacheTests` und
  `McpCodeGraphServerTests` nach IntegrationTests migrieren.
- Jeder mutierende Test besitzt eigene `BaselineMini`-Lease, eigenen Catalog und Server. Neue,
  geaenderte, nur beruehrte, geloeschte, generierte und projektfremde Dateien sowie Directory-mtime-
  Cache muessen unveraendert abgedeckt bleiben.
- Der 20-Reader/1-Writer-Vertrag bleibt ein in-process Refresh-Nebenlaeufigkeitsvertrag mit
  Zeitlimit, kein Prozess-Stress. Er erhaelt keine globale serielle Collection; Isolation geschieht
  ueber seine eigene Lease und Serverinstanz.

### item-07: Coverage, Ledger und gezielte Gates fuer 22 Klassen abschliessen (Risiko: high)

- Alle 99 historischen Methoden gegen aktuelle produktive Branches und Fehlerwege auditieren.
  Semantische Duplikate duerfen konsolidiert werden, muessen aber wie in step-020 mit historischer
  Methoden- und einzigartiger Vertragszahl dokumentiert sein; keine Assertion abschwaechen.
- Die 22 Legacy-Dateien erst nach vollstaendig belegter Zielabdeckung physisch loeschen. Alle 22
  Ledger-Zeilen atomar aktualisieren; Split-Zielorte ueber einen existierenden primaeren Pfad plus
  Coverage-Notiz dokumentieren.
- StaticTestSentinel-Delta fuer beruehrte Produktbereiche pruefen. Produktcode nur bei
  reproduzierbarer Abdeckungsluecke/Defekt aendern; keine neue Lade-Seam auf Verdacht.
- Roadmap/CodeMap/Tech-Debt aktualisieren. EPIC-5 bleibt offen, solange die ausgeschlossenen
  Datei-/Suppression-/Config- oder MSBuild-Registrierungsvertraege pending sind.

## Bewusst ausgeschlossen

- `SourceFileCatalogRegisterMSBuildTests`: enthaelt einen 20-fachen parallelen echten MSBuild-Load;
  der Stressanteil darf nicht in dieses normale EPIC-5-Gate geraten. Die Klasse bleibt bis zum
  EPIC-6-JIT-Schnitt komplett pending, statt halb migriert zu werden.
- `GetImpactToolTests`: echte Git-Repository-/Diff-Mutation.
- `McpCodeGraphServerConstructorTests`, reine Options-/Registrierungs-/Formatter-/Core-Resttests:
  keine Datei-/MSBuild-Kohorte; spaetere Restmigration.
- Commands/CLI-Prozess, MCP-Transport, Framing, Loading/Retry, Live-Repo/Dogfood, Performance und
  Stress: EPIC-6.
- `LoadFixtureBuilderTests`/`LoadFixtureMeasurementsTests`: Lastprofil/Performance, EPIC-6.
- Suppression- und breite Configuration-Dateifamilien: eigener verbleibender EPIC-5-JIT-Schnitt,
  da sie weder MSBuild-Host noch Catalog/Refresh-Lebensdauer teilen und den Diff-Richtwert sprengen.
- Legacy-Projektloeschung, finale Profile, Messbericht und Dokuabschluss: EPIC-7.
- TD-001 bleibt bei der ausgeschlossenen Prozess-/Framing-Kohorte in EPIC-6; dieser Step startet
  keinen MCP-Subprozess und kann die lastabhaengige Ursache nicht aussagekraeftig pruefen.
- TD-003 bleibt offen: Es gibt keine `rules.json`-/Regelverhaltensaenderung in dieser Kohorte; die
  mechanische Gesamtsynchronisierung der generierten Agentenregel waere ein sachfremder Diff.
- TD-004 wird in item-01 mitbehoben, weil die betroffene Host-Testdatei ohnehin angefasst wird und
  die Korrektur nur einen veraltenden Kommentar entfernt.
- TD-006 bleibt offen: Die Kategorieguards sind Gates, ihre gemeinsame Trait-Auslesung verlangt
  aber eine neue assembly-uebergreifende Helper-Entscheidung und ist keine Datei-/MSBuild-Plattform.
- TD-007 bleibt offen: Skeleton-Konfigurationen haben weder Konsumenten noch Zieldateien in dieser
  Kohorte; weiterhin nur zwei lokale kurze Helfer.
- TD-008 wird bei migrierten Konsumenten nicht vergroessert, kann aber erst mit den verbleibenden
  Legacy-Konsumenten/EPIC-7 geschlossen werden.
- TD-010 bleibt trotz erneuter Pruefung offen: Keine der 22 Klassen ermoeglicht die Entfernung von
  `FixtureWorkspaceBase`; dessen andere Legacy-Konsumenten bleiben pending. Eine Vereinheitlichung
  wuerde daher gerade die ausgeschlossene breite Legacy-Restmigration vorziehen.
- TD-002 und TD-005 sind bereits geschlossen und werden nicht erneut bearbeitet. TD-009 ist der
  direkt passende Plattformfund und wird in item-01 geschlossen.

## Tests

- [ ] Einmalige kombinierte Legacy-Baseline **vor** der Loeschung fuer exakt die 22 Klassen:
  `dotnet test src/AiNetLinter.Tests --filter "FullyQualifiedName~BaselineCliTests|FullyQualifiedName~BaselineComparerTests|FullyQualifiedName~BaselineReaderWriterTests|FullyQualifiedName~BaselineViolationFilterTests|FullyQualifiedName~FileChecksumCalculatorTests|FullyQualifiedName~FileSystemExclusionHelpersTests|FullyQualifiedName~ProjectRestoreStateTests|FullyQualifiedName~SourceFileCatalogBlazorPartialTests|FullyQualifiedName~SourceFileCatalogTests|FullyQualifiedName~WebBaselineTests|FullyQualifiedName~AnalysisCacheManagerIsolationTests|FullyQualifiedName~AnalysisCacheManagerTests|FullyQualifiedName~CacheEntryMapperTests|FullyQualifiedName~LinterEngineCacheTests|FullyQualifiedName~LinterEngineProjectRestoreTests|FullyQualifiedName~McpCodeGraphServerFileDiscoveryTests|FullyQualifiedName~McpCodeGraphServerStalenessMtimeCacheTests|FullyQualifiedName~McpCodeGraphServerTests|FullyQualifiedName~GetIndexScopeToolTests|FullyQualifiedName~GetServerHealthToolTests|FullyQualifiedName~ReloadConfigToolTests|FullyQualifiedName~SearchPatternToolTests"`
- [ ] `dotnet build`
- [ ] Fast-Zielklassen plus Guards:
  `dotnet test src/AiNetLinter.FastTests --no-build --filter "FullyQualifiedName~BaselineComparerTests|FullyQualifiedName~BaselineViolationFilterTests|FullyQualifiedName~FileChecksumCalculatorTests|FullyQualifiedName~CacheEntryMapperTests|FullyQualifiedName~SourceFileCatalogPolicyTests|FullyQualifiedName~FastTestsDependencyGuardTests|FullyQualifiedName~TestCategoryProfileGuardTests"`
- [ ] Plattform-/Ownership-/Loadbudget-Vertraege zuerst isoliert:
  `dotnet test src/AiNetLinter.IntegrationTests --no-build --filter "FullyQualifiedName~LoadedFixture|FullyQualifiedName~MsBuildFixtureHost|FullyQualifiedName~FindSymbolFileAdapterTests"`
- [ ] Migrierte Integration-Kohorte mit den neuen Zielklassennamen bzw. Namespaces `Baseline`,
  `Cache`, `Core` und den sieben expliziten MCP-Klassen; der Coder dokumentiert den exakten finalen
  Filter im Result. Kein pauschales `Category=Integration`, damit Prozess-/Repo-/andere Epics nicht
  versehentlich mitlaufen.
- [ ] Ledger-/Legacy-/Kategorieguards:
  `dotnet test src/AiNetLinter.IntegrationTests --no-build --filter "FullyQualifiedName~TestMigrationLedgerConsistencyTests|FullyQualifiedName~LegacyProjectBuildGateTests|FullyQualifiedName~TestCategoryProfileGuardTests"`
- [ ] Statischer FastTests-Dependency-Guard ist im Fast-Filter enthalten; zusaetzlich statisch
  pruefen, dass keine der neuen Fast-Zieldateien `SourceFileCatalog.LoadAsync`, MSBuild, Process,
  echtes Repo oder Temp-Dateisystem referenziert.
- [ ] Kein voller Fast-/Integration-/`Category!=Stress`-, Dogfood-, Performance- oder Stresslauf.
  EPIC-5 bleibt nach diesem Step voraussichtlich offen; daher kein Epic-Grenzgate.

## Definition of Done

- [ ] 22 historische Klassen / 99 Methoden sind vollständig migriert oder semantisch transparent
  konsolidiert; keine der 22 Legacy-Dateien bleibt bestehen.
- [ ] Reine Policy-/Mappingvertraege liegen in FastTests; alle echten Datei-/MSBuild-/Mtime-/
  Cache-/Config-/Refresh-Vertraege in IntegrationTests mit genau einer gueltigen Kategorie.
- [ ] Read-only Baseline-/SymbolGraph-Solutions werden ohne Collection-Serialisierung geteilt;
  mutable Tests besitzen getrennte Leases, Catalogs, Server und Ausgabepfade.
- [ ] Das MSBuild-Loadbudget ist exception-safe und durch einen deterministischen kleinen
  Plattformvertrag belegt; kein absichtlicher Lasttest.
- [ ] TD-004 und TD-009 sind nach mechanischer Kommentarkorrektur bzw. realer Wiederverwendung
  geschlossen; TD-010 und alle ausgeschlossenen offenen TDs bleiben offen und werden nicht als
  behoben ausgegeben.
- [ ] Ledger, Legacy-Build-, Kategorie- und Dependency-Guards sowie `dotnet build` sind gruen.
- [ ] EPIC-5 bleibt offen, sofern ausgeschlossene EPIC-5-Klassen weiterhin pending sind;
  EPIC-6/7 unveraendert offen.
- [ ] `git --no-pager diff --check` gruen; kein Voll-/Stresslauf und kein Push.
- [ ] Kohärente Conventional Commits auf Deutsch mit `[speedup-tests]`; `step-021/step-result.md`
  geschrieben, Planstatus `done (pending audit)`.

## Rules-Refs

- `.agents/rules/AiNetLinter.mdc#Kurz-Stil` und `#Projekt-Overrides` — Nullable,
  Testmethodenlimit und aktive Qualitätsgrenzen.
- `.agents/rules/AiNetLinterRichtlinien.mdc#3 Windows-Umgebung & Tool-Regeln` — Windows-Pfade,
  Build/Test/TRX-Diagnose.
- `.agents/rules/AiNetLinterRichtlinien.mdc#4 Updates & Tests` — Kategorien,
  Parallelitaetsbudget, keine breite Collection-Serialisierung, MCP-Tests nur ueber C#-Infrastruktur.
- `.agents/rules/AiNetLinterRichtlinien.mdc#5 Qualitaetsdrift-Praevention` — keine abgeschwaechten
  Assertions, keine Task-IDs in dauerhaftem Code, keine stillen Cleanup-Fehler ohne bestehende
  begruendete Testinfra-Konvention.

## Bekannte Ausnahmen

- TD-010 bleibt als Strangler-Uebergang offen, bis die letzten Konsumenten von
  `FixtureWorkspaceBase` mit dem Legacy-Projekt verschwinden.
- TD-001 betrifft den ausgeschlossenen MCP-Framing-Prozesslauf und ist kein Gate dieses Steps.

## Notes

- Der Klassenzaehler bezieht sich auf historische Ledger-Klassen; ein Split von
  `SourceFileCatalogTests` kann eine zusaetzliche Zielklasse erzeugen, aendert aber nicht die 22
  atomar zu migrierenden Ledger-Zeilen.
- Der 800-Diffzeilen-Wert ist ein Richtwert fuer inhaltliche Aenderungen. Reine Git-Renames der
  2.672 Legacy-Quellzeilen sind keine Begruendung, die kohärente Kohorte künstlich zu zerschneiden.
- Falls die einmalige Legacy-Baseline bereits rot ist, Ergebnis dokumentieren und nur bei
  migrationsrelevantem reproduzierbarem Defekt blockieren; keine Assertions abschwaechen.
