---
status: done (pending audit)
type: step-plan
task: speedup-tests
step: 019
corrects: null
title: "EPIC-4-Grenze: Find-Symbol-Snapshotmatrix und Nicht-C#-Dateiadapter"
epic: EPIC-4
estimated_risk: medium
step_type: batch
items:
  - id: item-01
    title: "Find-Symbol-Roslyn- und Dispatchmatrix nach FastTests migrieren"
    source: "konzept.md Leitplanken 1, 2, 3, 7 bis 9; step-018 Grenzrest"
  - id: item-02
    title: "Neun C#-Leermengen- und Miss-Hint-Vertraege als hermetischen Dateiadapter migrieren"
    source: "FindSymbolScannerTests/FindSymbolToolTests; konzept.md Leitplanken 1 und 4"
  - id: item-03
    title: "EPIC-4-Coverage, Ledger und Grenzgate abschliessen"
    source: "konzept.md Definition of Done; test-migration-ledger.md"
created_by: planer
created_by_model: gpt-5.6-sol
created_by_model_knowledge_cutoff: nicht ausgewiesen
created_at: 2026-08-13
related_to:
  - step-006
  - step-018
---

# Step 019: EPIC-4-Grenze fuer Find-Symbol

## Bezug

- **Task:** `speedup-tests`
- **Epic:** `EPIC-4` aus `roadmap.md` — letzter noch offener gemischter In-Memory-/Toolvertrag.
- **Konzept-Referenz:** `konzept.md` Muss-Haben sowie Technische Leitplanken §1 bis §4 und §7 bis
  §9: breite Roslyn-Matrix auf vorbereiteten Snapshots, echte Datei-/MSBuild-Grenzen nur in wenigen
  hermetischen Integrationstests, physische Strangler-Migration und Coverage-Audit.
- **Step-018-Abgleich:** Re-Audit-Commit `9cc8b73` bestaetigt step-018 als `approved`; technischer
  Endstand bleibt `e864407 -> f0dbacc`, kumulative Doku-Korrektur `b1a59b7`.

## Aktueller Projektzustand (JIT-Kontext)

`FindSymbolScannerTests` besitzt sechs und `FindSymbolToolTests` vierzehn Testmethoden. Davon sind
11 reine Roslyn-/Dispatch-Vertraege: erfolgreiche Symbolsuche, Case-Insensitivity, Truncation,
Structured Content, Argument-/Loadfehler und Compile-Error-Header. Sie koennen ohne Catalog,
Collection-Serialisierung oder neue Produkt-Seam auf `SymbolGraphMiniSolutionSpec`,
`CompileErrorMiniSolutionSpec` und den besitzenden `McpInMemoryTestContext` aus step-018 sinken.

Neun Methoden erreichen bei einer C#-Leermenge dagegen bewusst `FindSymbolScanner.AppendMissHint`
und damit `SearchPatternScanner.GetFilesWithHits`: positive Treffer in `site.js`,
`Component.razor` und `Page.xaml`, kein Datei-Treffer sowie Kindfilter-Leermengen. Auch der
No-Hint-Fall ist ein Dateivertrag, weil er das Verzeichnis tatsaechlich und ergebnislos scannt.
Diese Dateien sind kein Bestandteil einer Roslyn-`Solution`; der Vertrag wird deshalb nicht
in-memory vorgetaeuscht und nicht durch
eine neue Produkt-Seam umgebaut, sondern als kleine hermetische Integration gegen eine per
`IsolatedFixtureLease` kopierte `tests/Fixtures/SymbolGraphMini` und genau einen
`SourceFileCatalog.LoadAsync`-Adapterload erhalten.

Die fruehere step-018-Abgrenzung bleibt damit bewusst bestehen und wird nur entlang der dort
angekuendigten Klassenteilung verfeinert. `GetIndexScopeToolTests`, `SearchPatternToolTests`,
`ReloadConfigToolTests`, `GetServerHealthToolTests` und `GetImpactToolTests` bleiben wegen echtem
Dateiinventar/Ausschluss, Config-Mutation, Call-Log-Datei bzw. Git-Repository-Vertrag pending und
gehoeren EPIC-5/6. Ebenso ausserhalb: `SuppressionScannerTests`, SourceFileCatalog-/MSBuild-/
Baseline-/Refresh-Kohorten, MCP-Prozess/Framing/Loading/Retry, Repo/Dogfood, Performance und Stress.

Es gibt keinen bereichsnahen offenen `auto_fixable: ja`-Tech-Debt-Treffer. TD-003 betrifft die
generierte Regeldatei, TD-004 einen Integration-Plattformkommentar; beides wird nicht an diesen
fachlichen Grenzstep angehaengt.

## Intention

Der Step schliesst den kompletten EPIC-4-Rest als einen vertikalen, logisch geschlossenen Schnitt:
beide historischen Find-Symbol-Klassen verschwinden aus Legacy, ihre schnellen Vertraege laufen
parallelisierbar auf bestehenden Snapshots und die neun dateibasierten Fallback-Vertraege bleiben
als hermetische Adapter-Nachweise in IntegrationTests. Nach Ledger-, Guard- und Epic-Grenzgate kann
EPIC-4 im Review abgeschlossen werden, ohne EPIC-5/6-Vertraege vorzuziehen.

## Konkrete Änderungen

### item-01: Find-Symbol-Roslyn- und Dispatchmatrix nach FastTests migrieren — 2 historische Klassen, 11 Methoden (Risiko: medium)

- **Scope:** Aus `src/AiNetLinter.Tests/Mcp/Tools/FindSymbolScannerTests.cs` und
  `FindSymbolToolTests.cs` alle Vertraege, die keinen C#-Leermengen-Dateifallback erreichen,
  nach `src/AiNetLinter.FastTests/Mcp/Tools/` uebernehmen. Eine oder zwei Zielklassen sind erlaubt;
  die fachliche Trennung Scanner/Tool ist zu bevorzugen, sofern dadurch keine Fixture-Duplikation
  entsteht.
- **Fixture:** `SymbolGraphMiniSolutionSpec.Create()` und `McpInMemoryTestContext` fuer
  Symbol-/Toolvertraege; `CompileErrorMiniSolutionSpec.CreatePlural()` fuer den Warnheader.
  Vorhandene virtuelle Pfade und der `ReadOnlySolutionSnapshot`-Serverzweig werden wiederverwendet.
- **Kategorien:** direkte reine Truncation-/Argumentlogik nur dann `Unit`, wenn sie keine Solution
  materialisiert; alle Roslyn-/Server-Snapshot-Vertraege `Component`. Keine Collection- oder
  Assembly-Serialisierung nur fuer Sharing.
- **Vertragserhalt:** Symbolfund mit Datei/Zeile/Kind, Max-Result-Truncation, erfolgreiche
  englische/deutsche Kindfilter, unbekannter/leerer Input, Case-Insensitivity, direkte
  Dateilisten-Truncation, Structured Content, Solution-not-loaded und pluraler Compile-Error-Header muessen erhalten
  bleiben. Semantische Duplikate zwischen den beiden Legacy-Klassen duerfen konsolidiert werden,
  aber kein eigenstaendiger Fehler-/Negativ-/Formatvertrag darf verschwinden.
- **Grenze:** Keine `SourceFileCatalog.LoadAsync`-, Temp-, Prozess-, Repo- oder
  `SymbolGraphCatalogFixture`-Nutzung in FastTests; keine neue Produkt-Seam und kein neuer
  TestKit-Allzweckhelper.

### item-02: Neun C#-Leermengen- und Miss-Hint-Vertraege als hermetischen Dateiadapter migrieren — 9 Methoden (Risiko: medium)

- **Scope:** Alle Vertraege, deren Symbolsuche wegen unbekanntem Namen oder ausschliessendem
  Kindfilter leer bleibt und deshalb den Datei-Fallback ausfuehrt, nach
  `src/AiNetLinter.IntegrationTests/Mcp/Tools/FindSymbolFileAdapterTests.cs` oder eine gleichwertig
  klar benannte Zielklasse uebernehmen.
- **Fixture:** Eine IntegrationTests-lokale `IClassFixture` besitzt
  `IsolatedFixtureLease.CopyFixture(FindSolutionRoot(), "SymbolGraphMini")` und genau einen
  `SourceFileCatalog.LoadAsync` fuer alle neun read-only Tests der Zielklasse. Das etablierte Muster aus
  `SkeletonMapBuilderAdapterTests`/`FilterMiniFidelityTests` wiederverwenden; Catalog und Lease
  deterministisch entsorgen. Keine neue TestKit-Fixture, solange kein zweiter realer
  Assembly-uebergreifender Konsument sie verlangt.
- **Assertions:** Positive Nicht-C#-Treffer, Plain-No-Match ohne Hint, englischer/deutscher
  ausschliessender Kindfilter, sichtbarer Miss-Hint, `site.js`, `Component.razor`, `Page.xaml`,
  Verweis auf `search_pattern` und untrunkierte Datei-Liste fuer die drei Fixture-Treffer erhalten.
- **Kategorie:** `Integration`; keine serialisierende Collection, kein Prozess, kein echtes Repo.

### item-03: EPIC-4-Coverage, Ledger und Grenzgate abschliessen (Risiko: medium)

- **Coverage-Audit:** `FindSymbolScanner` und `FindSymbolTool` gegen die 20 historischen Methoden
  abgleichen: `ValidKinds`, Normalisierung von `maxResults < 1`, Loading-Zweig,
  `OperationCanceledException`-Durchreichung versus CompilationError-Fallback,
  Mehrfachdeklarationen/Location-Formatierung, Miss-Hint-Truncation und Compile-Diagnostik.
  Fehlende nicht-triviale Vertraege nur in der guenstigsten korrekten Zielassembly ergaenzen;
  Produktcode ausschliesslich bei reproduzierbarem Defekt aendern.
- **Strangler:** Beide Legacy-Dateien erst loeschen, wenn alle 20 Vertraege in Fast-/IntegrationTests
  nachweisbar sind. Beide Ledger-Zeilen auf `migrated` oder bei echter Zusammenlegung
  `consolidated` mit existierenden neuen Abdeckungsorten setzen; Pflichtfelder/Evidenz nach dem im
  Ledger etablierten Format ergaenzen.
- **CodeMap/Roadmap:** reale Zielorte eintragen. EPIC-4 erst nach gruenem Grenzgate und Review als
  abgeschlossen markieren; EPIC-5 bis EPIC-7 bleiben offen.

## Tests

- [ ] Vor der Legacy-Loeschung einmalige Baseline fuer exakt beide Klassen:
  `dotnet test src/AiNetLinter.Tests --filter "FullyQualifiedName~FindSymbolScannerTests|FullyQualifiedName~FindSymbolToolTests"`
- [ ] `dotnet build`
- [ ] Migrierte FastTests einschliesslich Snapshot-/Factory- und Architekturguards:
  `dotnet test src/AiNetLinter.FastTests --no-build --filter "FullyQualifiedName~FindSymbol|FullyQualifiedName~McpCodeGraphServerReadOnlySnapshotTests|FullyQualifiedName~RoslynTestSolutionFactoryTests|FullyQualifiedName~FastTestsDependencyGuardTests|FullyQualifiedName~TestCategoryProfileGuardTests"`
- [ ] Hermetischer Dateiadapter plus Ledger-/Legacy-Gates:
  `dotnet test src/AiNetLinter.IntegrationTests --no-build --filter "FullyQualifiedName~FindSymbolFileAdapterTests|FullyQualifiedName~TestMigrationLedgerConsistencyTests|FullyQualifiedName~LegacyProjectBuildGateTests|FullyQualifiedName~TestCategoryProfileGuardTests"`
- [ ] EPIC-4-Grenzgate ohne Stress, Dogfood, Performance, Prozess- oder Repo-Profile:
  `dotnet test src/AiNetLinter.FastTests --no-build --filter "Category=Component"`
  sowie der obige hermetische Integration-Filter. Kein volles `Category!=Stress`-Gate; EPIC-5/6
  sind weiterhin offen und deren teure Profile waeren fuer diese Grenze nicht aussagekraeftig.
- [ ] Bei Fehlschlag zuerst nur die konkrete Zielklasse erneut ausfuehren; breitere Filter erst nach
  lokalem Fix wiederholen.

## Definition of Done

- [ ] Alle 20 historischen Find-Symbol-Testmethoden sind erhalten oder mit dokumentierter
  semantischer Gleichwertigkeit konsolidiert; produktseitiger Coverage-Audit ist nachvollziehbar.
- [ ] Beide Legacy-Testdateien sind physisch geloescht; Ledger zeigt gueltige neue Abdeckungsorte
  und der Konsistenzguard ist gruen.
- [ ] FastTests nutzen ausschliesslich vorhandene In-Memory-/Snapshot-Fixtures und bestehen die
  Dependency-/Kategorieguards; kein Catalog, MSBuild, Prozess, Repo oder serialisierende Collection.
- [ ] Genau die neun C#-Leermengen-/Miss-Hint-Vertraege laufen hermetisch als `Integration` gegen die
  isolierte Disk-Fixture; kein echtes Repository und kein Prozess.
- [ ] Keine neue Produkt-Seam oder vorsorgliche TestKit-Abstraktion wurde eingefuehrt.
- [ ] `dotnet build`, gezielte Fast-/Integration-Gates und das Component-Epic-Grenzgate sind gruen;
  kein Stresslauf.
- [ ] `roadmap.md` markiert EPIC-4 erst nach Umsetzung und Review als abgeschlossen; EPIC-5 bis
  EPIC-7 bleiben offen.
- [ ] Commit(s) auf aktuellem Branch als Conventional Commit auf Deutsch mit Suffix
  `[speedup-tests]`; kein Push.
- [ ] `step-019/step-result.md` geschrieben und Planstatus auf `done (pending audit)` gesetzt.

## Rules-Refs

- `.agents/rules/AiNetLinter.mdc#Kurz-Stil` und `#Projekt-Overrides` — Nullable, Testdatei-
  Methodenlimit und aktive Codequalitaetsgrenzen.
- `.agents/rules/AiNetLinterRichtlinien.mdc#4 Updates & Tests` — Kategorien, Parallelitaet und
  zielgerichtete Loesung statt Collection-Serialisierung.
- `.agents/rules/AiNetLinterRichtlinien.mdc#5 Qualitaetsdrift-Praevention` — keine abgeschwaechten
  Assertions, keine Task-IDs in dauerhaftem Code, sichtbare Fehlerpfade.

## Bekannte Ausnahmen

- Keine. TD-001 betrifft erst den EPIC-6-Framing-/Prozesslauf und ist kein Gate dieses Steps.

## Notes

- Die Grenze ist vertraglich, nicht dateibasiert: Das Aufteilen der beiden Legacy-Klassen auf
  FastTests und IntegrationTests ist gerade der Abschluss, keine halbe Migration. Ledger und
  Legacy-Loeschung erfolgen trotzdem atomar fuer beide historischen Klassen.
- `SearchPatternScanner.GetFilesWithHits` liest Nicht-C#-Dateien aus dem Solution-Verzeichnis;
  diese Wirkung nicht durch In-Memory-Dokumente oder erfundene WebFile-APIs simulieren.
- Falls der Coverage-Audit zeigt, dass ein zusaetzlicher echter Datei-/MSBuild-Vertrag noetig ist,
  nur in item-02 aufnehmen, wenn er unmittelbar Find-Symbol-Miss-Hint betrifft. Sonst als
  Beobachtung fuer EPIC-5 dokumentieren, nicht den Step ausweiten.
