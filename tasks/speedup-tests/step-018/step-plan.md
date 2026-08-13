---
status: open
type: step-plan
task: speedup-tests
step: 018
corrects: null
title: "Read-only MCP-Roslyn-Toolkohorten als In-Memory-Super-Step migrieren"
epic: EPIC-4
estimated_risk: medium
step_type: batch
items:
  - id: item-01
    title: "Gemeinsamen read-only MCP-In-Memory-Testkontext bereitstellen"
    source: "konzept.md Technische Leitplanken §2/§5; task-state.md Super-Step-Vorgabe"
  - id: item-02
    title: "Duplicate-Detection-Tooldispatch migrieren"
    source: "test-migration-ledger.md: DuplicateDetectionToolTests + DuplicateDetectionToolRefactoringDriftTests"
  - id: item-03
    title: "Dependency-Graph-Scanner und Tool migrieren"
    source: "test-migration-ledger.md: DependencyGraphScannerTests + DependencyGraphToolTests"
  - id: item-04
    title: "Call-Graph-Traversal und Call-Tree-Tool migrieren"
    source: "test-migration-ledger.md: CallGraphTraversalTests + GetCallTreeToolTests"
  - id: item-05
    title: "Symbolauflösung, References und Symbol-Body migrieren"
    source: "test-migration-ledger.md: SymbolIdentifierResolverTests + FindReferencesToolTests + GetSymbolBodyToolTests"
  - id: item-06
    title: "File-Skeleton und CSharp-Hotspots migrieren"
    source: "test-migration-ledger.md: GetFileSkeletonToolTests + GetHotspotsToolTests"
  - id: item-07
    title: "Type-Hierarchy und DI-Heuristik migrieren"
    source: "test-migration-ledger.md: GetTypeHierarchyToolTests + DiRegistrationHeuristicsTests"
  - id: item-08
    title: "Violations-Tool migrieren"
    source: "test-migration-ledger.md: GetViolationsToolTests"
  - id: item-09
    title: "Metrics-Tree-Scanner und Tool migrieren"
    source: "test-migration-ledger.md: MetricsTreeRoslynScannerTests + MetricsTreeToolTests"
  - id: item-10
    title: "Pattern-Detect-Scanner und Tool migrieren"
    source: "test-migration-ledger.md: PatternDetectScannerTests + PatternDetectToolTests"
  - id: item-11
    title: "Safeguard-Scanner und Tool migrieren"
    source: "test-migration-ledger.md: SafeguardScannerTests + SafeguardToolTests"
  - id: item-12
    title: "Gemeinsame MCP-Toolresult-Verträge migrieren"
    source: "test-migration-ledger.md: McpToolResultsTests"
  - id: item-13
    title: "Suppression-Scanner migrieren"
    source: "test-migration-ledger.md: SuppressionScannerTests"
  - id: item-14
    title: "LinterAnalyzer-Semantikverträge migrieren"
    source: "test-migration-ledger.md: ArchitectureTests + LinterAnalyzerTests"
created_by: planer
created_by_model: gpt-5.6-sol
created_by_model_knowledge_cutoff: nicht ausgewiesen
created_at: 2026-08-13
related_to:
  - step-006
  - step-015
  - step-016
  - step-017
---

# Step 018: Read-only MCP-Roslyn-Toolkohorten als In-Memory-Super-Step migrieren

## Bezug

- **Task:** `speedup-tests`
- **Epic:** `EPIC-4` aus `roadmap.md` — reine Scanner- und direkte MCP-Toolverträge sollen breit
  gegen vorbereitete In-Memory-`Solution`-Snapshots laufen.
- **Konzept-Referenz:** `konzept.md` Scope/Muss-Haben sowie Technische Leitplanken §1, §2, §5,
  §7, §8 und §9: breite Toolmatrix ohne Prozess, gecachte Roslyn-Referenzen, read-only Sharing,
  Strangler-Löschung, Coverage-Audit und sparsame Verifikation.
- **Task-State-Override:** Commit `04dce94` setzt `max_batch_items: 40` und
  `max_batch_diff_lines: 800`; reine Strukturmigrationen müssen als große Super-Steps gebündelt
  werden. Dieser Batch enthält 24 Legacy-Dateien/Testklassen in 13 fachlichen Migrationsitems plus
  einem gemeinsamen Infrastrukturitem.

## Aktueller Projektzustand (JIT-Kontext)

Die Zielarchitektur steht: `RoslynTestSolutionFactory`, gecachte `MetadataReference`n,
`PreparedSolutionFixture`, FastTests-Architekturguards und der interne
`SourceFileCatalog(Solution, ...)`-/`McpCodeGraphServer`-Direkteinstieg sind vorhanden. Step 015 bis
017 haben Duplicate-Detection-Scanner und -Engines bereits auf diese Plattform migriert.

Der aktuelle Legacy-Bestand enthält 27 pending Dateien unter `Mcp/Tools`. Davon sind 20 reine,
read-only Roslyn-/direkte Toolverträge; hinzu kommen die unmittelbar von ihnen verwendeten
`McpToolResultsTests`, der reine `SuppressionScannerTests`-Parservertrag und die zwei reinen
`LinterAnalyzer`-Semantikkohorten. Diese 24 Klassen prüfen
Semantik, Argumentvalidierung, Load-error-
Darstellung, Truncation, Structured Content und Formatierung über vorhandene `Solution`-Snapshots.
Viele konsumieren heute `SymbolGraphCatalogFixture`; andere bauen lokal `AdhocWorkspace`,
`MetadataReference`n und Temp-Dateien. Diese Infrastruktur ist nicht Gegenstand ihrer Verträge und
kann ohne Produkt-Seam durch eine FastTests-lokale In-Memory-Spiegelung und einen gemeinsamen
besitzenden Serverkontext ersetzt werden. Read-only Assembly-Fixture-Sharing darf dabei keine
Collection-Serialisierung übernehmen.

Bewusst außerhalb bleiben sieben nicht kompatible Dateien:

- `FindSymbolScannerTests` und `FindSymbolToolTests` enthalten den Nicht-C#-Miss-Hint über reale
  Dateisuche.
- `GetIndexScopeToolTests` und `SearchPatternToolTests` prüfen Web-/XAML-/HTML-Dateien sowie
  obj/bin-/Worktree-Ausschlüsse und sind damit echte Dateisystemverträge.
- `ReloadConfigToolTests` mutiert und entdeckt `rules.json` auf Platte.
- `GetServerHealthToolTests` schreibt/liest den Call-Log-Vertrag auf Platte.
- `GetImpactToolTests` enthält echte Git-Repository- und uncommitted-diff-Verträge.

Diese Grenzen sind EPIC-5/6-nah und werden nicht durch Teilmoves oder abgeschwächte Assertions in
den In-Memory-Batch gezwungen. Es gibt keinen relevanten `auto_fixable: ja`-Tech-Debt-Treffer im
berührten C#-/Ledger-Scope.

## Intention

Der Super-Step migriert alle 24 kompatiblen Legacy-Testklassen vollständig nach
`AiNetLinter.FastTests`, kategorisiert reine Parserverträge als `Unit` und alle
Solution-/Serververträge als `Component`, löscht die Altdateien und aktualisiert das Ledger in
einem konsistenten Durchlauf. Ein gemeinsames, schlankes Testarrangement materialisiert die
kanonische Symbolgraph-Spezifikation lazy und erzeugt bei Bedarf direkt einen Server über den
Snapshot; lokale Spezial-Szenarien bleiben deklarative `ProjectSpec`s, damit der Helper nicht zum
neuen Sammelbecken wird.

## Konkrete Änderungen

### item-01: Gemeinsamen read-only MCP-In-Memory-Testkontext bereitstellen — `src/AiNetLinter.FastTests/Mcp/Tools/` (Risiko: medium)

- **Was:** Eine FastTests-lokale deklarative Spiegelung der für diese 20 Klassen benötigten
  `SymbolGraphMini`-Merkmale und einen kleinen besitzenden MCP-Testkontext ergänzen. Der Kontext
  verwendet `PreparedSolutionFixture.GetOrCreate` für den immutable Standard-Snapshot und
  `RoslynTestSolutionFactory` für isolierte Spezial-Szenarien; er erzeugt
  `SourceFileCatalog(Solution, hasLoadingErrors)` und `McpCodeGraphServer` direkt, unterstützt nur
  bereits produktiv vorhandene Config-/MaxLineCount-Parameter und entsorgt jeden selbst besessenen
  Server/Workspace deterministisch. Kein `SourceFileCatalog.LoadAsync`, keine Temp-Dateien, kein
  MSBuild und keine neue serialisierende Collection.
- **Warum:** Die bisherigen Catalog-/Ad-hoc-Builder sind in vielen Klassen dupliziert. 20 reale
  FastTests-Konsumenten rechtfertigen eine lokale kleine Testhülle; eine neue Produkt-Seam oder ein
  TestKit-Helper ohne IntegrationTests-Konsumenten wäre unnötig.

### item-02: Duplicate-Detection-Tooldispatch migrieren — 2 Legacy-Dateien (Risiko: medium)

- **Scope:** `DuplicateDetectionToolTests.cs`, `DuplicateDetectionToolRefactoringDriftTests.cs`.
- **Was:** Alle 19 Clone-/Refactoring-Drift-Dispatchverträge als `Component` übernehmen und auf
  den gemeinsamen Kontext bzw. lokale `ProjectSpec`s umstellen. Mode-/Argumentfehler,
  Scannerfehler-Durchreichung, Structured Content, Sufficiency und Truncation unverändert erhalten;
  Temp-Verzeichnis, lokaler Ad-hoc-Builder und per-Test-Referenzaufbau entfernen.
- **Warum:** Beide Dateien testen die zwei Modi desselben direkten Tools und hängen an den bereits
  migrierten Scanner-/Engineverträgen aus step-015 bis step-017.

### item-03: Dependency-Graph-Scanner und Tool migrieren — 2 Legacy-Dateien (Risiko: medium)

- **Scope:** `DependencyGraphScannerTests.cs`, `DependencyGraphToolTests.cs`.
- **Was:** Datei-/Typ-, incoming/outgoing/both-, Depth-, Zyklus-, Aggregations-, Self-edge-,
  Truncation-, Argument- und Responseverträge auf virtuelle Mehrdatei-`ProjectSpec`s umstellen und
  als `Component` migrieren.
- **Warum:** Scanner und Tool bilden eine geschlossene semantische Dependency-Graph-Kohorte ohne
  Dateisystemvertrag; virtuelle FilePaths sind Teil der Roslyn-Identität.

### item-04: Call-Graph-Traversal und Call-Tree-Tool migrieren — 2 Legacy-Dateien (Risiko: low)

- **Scope:** `CallGraphTraversalTests.cs`, `GetCallTreeToolTests.cs`.
- **Was:** Caller-Gruppierung, Depth-Cap, Top-N, ASCII-/Mermaid-Dispatch, Symbolfehler und
  Compile-error-Warntext gegen den vorbereiteten Symbolgraph-Snapshot übernehmen; als `Component`
  kategorisieren.
- **Warum:** Beide Klassen konsumieren denselben read-only Callergraph und brauchen weder den
  Legacy-Catalog-Load noch Collection-Serialisierung.

### item-05: Symbolauflösung, References und Symbol-Body migrieren — 3 Legacy-Dateien (Risiko: medium)

- **Scope:** `SymbolGraph/SymbolIdentifierResolverTests.cs`,
  `SymbolGraph/FindReferencesToolTests.cs`, `GetSymbolBodyToolTests.cs`.
- **Was:** Den reinen Position-/Line-only-Parser als `Unit` und die qualifizierten/stabilen/
  positionsbasierten Auflösungs-, Ambiguitäts-, References-, Depth-, Truncation- und Bodyverträge
  als `Component` migrieren. Windows-Pfade bleiben virtuelle Pfadwerte; kein physischer
  Dateizugriff wird eingeführt.
- **Warum:** Parser und beide Tools teilen denselben Symbol-Identifier-Vertrag und den
  Symbolgraph-Snapshot; der Parser selbst benötigt keine Solution.

### item-06: File-Skeleton und CSharp-Hotspots migrieren — 2 Legacy-Dateien (Risiko: low)

- **Scope:** `GetFileSkeletonToolTests.cs`, `GetHotspotsToolTests.cs`.
- **Was:** Relative/absolute virtuelle Pfadauflösung, Skeleton-Output, Zeilen-Schwellen,
  Scopefilter, Structured Content und Compile-error-Aggregatwarnungen als `Component` übernehmen.
  Compile-error-Szenarien durch In-Memory-Quellen plus expliziten `hasLoadingErrors`-Status
  kalibrieren, nicht durch MSBuild-Fixtures.
- **Warum:** Diese beiden Klassen werten ausschließlich C#-Dokumente im geladenen Snapshot aus;
  Nicht-C#-Dateiinventar und reale Ladefehlerdiagnostik bleiben außerhalb.

### item-07: Type-Hierarchy und DI-Heuristik migrieren — 2 Legacy-Dateien (Risiko: medium)

- **Scope:** `GetTypeHierarchyToolTests.cs`, `DiRegistrationHeuristicsTests.cs`.
- **Was:** Base-/Interface-/Derived-, External-Type-, Truncation-, Symbolfehler- und
  DI-Registration-Verträge auf deklarative In-Memory-Projekte umstellen und als `Component`
  migrieren. Den lokalen `DiRegistrationMiniFixtureWorkspace` samt MSBuild-Load entfernen.
- **Warum:** DI-Erkennung ist eine Roslyn-Heuristik über Syntax/Semantik und bildet gemeinsam mit
  dem Hierarchie-Tool eine geschlossene read-only Typanalyse-Kohorte.

### item-08: Violations-Tool migrieren — 1 Legacy-Datei (Risiko: medium)

- **Scope:** `GetViolationsToolTests.cs`.
- **Was:** Known-violation-, Scope-, Markdown-/Structured-Content-, Truncation-, Zero-result-,
  Compile-error- und Malfunction-Verträge als `Component` gegen kalibrierte In-Memory-Solutions
  übernehmen; direkte Format-Helperverträge erhalten.
- **Warum:** `LinterEngine.RunAsync(Solution)` und das gleiche In-Memory-Muster sind bereits durch
  step-006 etabliert; ein Catalog-/MSBuild-Load ist hier nicht Vertragsgegenstand.

### item-09: Metrics-Tree-Scanner und Tool migrieren — 2 Legacy-Dateien (Risiko: medium)

- **Scope:** `MetricsTreeRoslynScannerTests.cs`, `MetricsTreeToolTests.cs`.
- **Was:** Code-size-, Comment-density-, Violation-density-, Complexity-, Sortierungs-, Root-,
  Depth-, Top-N-, Filter- und Hint-Verträge auf vorbereitete bzw. szenariolokale In-Memory-
  Dokumente umstellen und als `Component` migrieren. Bestehendes Fixture-Dateischreiben
  vollständig entfernen.
- **Warum:** Scanner und Tool arbeiten read-only auf C#-Dokumenttext und Roslyn-Metriken; sie sind
  fachlich zusammenhängend und benötigen keine physische Dateiänderung.

### item-10: Pattern-Detect-Scanner und Tool migrieren — 2 Legacy-Dateien (Risiko: medium)

- **Scope:** `PatternDetectScannerTests.cs`, `PatternDetectToolTests.cs`.
- **Was:** Sechs Patternzuordnungen, Scope-/Subset-/Empty-, Truncation-, Structured-Content-,
  Sufficiency-, Invalid-Argument- und Malfunction-Verträge auf Factory/Prepared-Snapshot umstellen
  und als `Component` migrieren; lokale Ad-hoc-/FilePath-Builder entfernen.
- **Warum:** Scanner und Dispatch verwenden ausschließlich `Solution` und Config und bilden eine
  vollständige fachliche Toolkohorte.

### item-11: Safeguard-Scanner und Tool migrieren — 2 Legacy-Dateien (Risiko: medium)

- **Scope:** `SafeguardScannerTests.cs`, `SafeguardToolTests.cs`.
- **Was:** Score-/Threshold-/Determinismus-, Retry-/Cancellation-, Remediation-, Scope-, Override-,
  Structured-Content- und Malfunction-Verträge als `Component` migrieren. Lokale Ad-hoc-Builder
  durch Factory-Szenarien ersetzen; bestehende kontrollierte Compilation-Delegates für Retry-
  Fehlerpfade unverändert erhalten.
- **Warum:** Scanner und Tool bilden eine geschlossene read-only Qualitätsgate-Kohorte; Retry hier
  betrifft die in-process Roslyn-Compilation, nicht MCP-Prozessstart/Loading.

### item-12: Gemeinsame MCP-Toolresult-Verträge migrieren — 1 Legacy-Datei (Risiko: low)

- **Scope:** `src/AiNetLinter.Tests/Mcp/McpToolResultsTests.cs`.
- **Was:** Die fünf reinen Error-/SolutionNotLoaded-/Text-/Structured-Content-/CompilationError-
  Verträge als `Unit` nach `AiNetLinter.FastTests/Mcp/` migrieren und die Legacy-Datei löschen.
- **Warum:** `McpToolResults` ist der gemeinsame direkte Response-Baustein aller Toolitems dieses
  Batches; seine prozess- und solutionfreie Vertragsklasse gehört kohärent in denselben Super-Step.

### item-13: Suppression-Scanner migrieren — 1 Legacy-Datei (Risiko: low)

- **Scope:** `src/AiNetLinter.Tests/Suppression/SuppressionScannerTests.cs`.
- **Was:** Den reinen Parservertrag für die verschiedenen Suppression-Stile als `Unit` nach
  `AiNetLinter.FastTests/Suppression/` migrieren und die Legacy-Datei löschen; Assertions und
  Syntaxvarianten unverändert erhalten.
- **Warum:** Pattern Detect, Violations und Safeguard konsumieren Suppressionsverhalten in ihren
  Roslyn-Analysen; der Scanner selbst benötigt weder Solution noch Dateisystem und ist ein
  kompatibler Low-Risk-Strukturmove.

### item-14: LinterAnalyzer-Semantikverträge migrieren — 2 Legacy-Dateien (Risiko: low)

- **Scope:** `src/AiNetLinter.Tests/Architecture/ArchitectureTests.cs`,
  `src/AiNetLinter.Tests/Core/LinterAnalyzerTests.cs`.
- **Was:** Beide reinen SyntaxTree-/SemanticModel-Vertragsklassen als `Unit` nach
  `AiNetLinter.FastTests/Core/` migrieren, dabei die irreführend benannte `ArchitectureTests` als
  `LinterAnalyzerArchitectureRuleTests` fachlich korrekt ablegen. Alle Regel-, Suppression-, Config-,
  Exemption-, Complexity-, Immutability-, Exception-, Phantom-Dependency- und Namespacefilter-
  Assertions erhalten; gemeinsame Compilation-Erzeugung nur mit dem bestehenden FastTests-
  `TestHelper` konsolidieren, sofern dessen Optionen semantisch identisch sind.
- **Warum:** Beide Klassen sind reine In-Memory-Roslyn-Verträge desselben `LinterAnalyzer` und
  hängen fachlich direkt unter Pattern Detect, Violations und Safeguard. Sie getrennt in einem
  späteren Struktur-Step zu verschieben widerspräche der Super-Step-Vorgabe.

### Batch-weite Ledger-/CodeMap-Aktualisierung

- **Dateien:** `tasks/speedup-tests/test-migration-ledger.md`,
  `tasks/speedup-tests/codemap.md`.
- **Was:** Alle 24 Legacy-Klassen erst nach grünem Alt-/Neu-Abgleich physisch löschen, ihre
  Ledger-Zeilen atomar auf `migrated` mit FastTests-Zielpfad, Ebene und Verifikationsevidenz setzen
  und Codemap-Pointer für den gemeinsamen Kontext sowie die 14 abgeschlossenen Items ergänzen.
- **Warum:** Kein Batch-Item darf einen halb migrierten Ledger-/Dateizustand hinterlassen.

### Batch-weite produktseitige Coverage-Audits

- **Scope:** Die bestehenden produktiven Analyzer-/Scanner-/Tool-/Response-Dateien der 13
  Migrationsitems,
  grundsätzlich unverändert.
- **Was:** Pro Item Legacy-Assertions mit den aktuellen öffentlichen/internen Verzweigungen,
  Fehlercodes, Truncation-/Sufficiency- und Structured-Content-Verträgen abgleichen. Produktcode nur
  bei einem reproduzierbaren Defekt ändern; dann ausschließlich den engsten Component-
  Regressionstest im betroffenen Item ergänzen. Keine neue Seam, keine Registrierungs-, Prozess-,
  Refresh-, Git- oder Dateisystemarbeit.
- **Warum:** Das Konzept verlangt kohortenweise Coverage-Audits, die Super-Step-Vorgabe erweitert
  aber nicht den produktiven Architektur-Scope.

## Tests

- [ ] Vor der Löschung einmaliger kombinierter Legacy-Baselinefilter für genau die 24 Klassen:
  `dotnet test src/AiNetLinter.Tests --filter "FullyQualifiedName~ArchitectureTests|FullyQualifiedName~CallGraphTraversalTests|FullyQualifiedName~DependencyGraphScannerTests|FullyQualifiedName~DependencyGraphToolTests|FullyQualifiedName~DiRegistrationHeuristicsTests|FullyQualifiedName~DuplicateDetectionToolRefactoringDriftTests|FullyQualifiedName~DuplicateDetectionToolTests|FullyQualifiedName~FindReferencesToolTests|FullyQualifiedName~GetCallTreeToolTests|FullyQualifiedName~GetFileSkeletonToolTests|FullyQualifiedName~GetHotspotsToolTests|FullyQualifiedName~GetSymbolBodyToolTests|FullyQualifiedName~GetTypeHierarchyToolTests|FullyQualifiedName~GetViolationsToolTests|FullyQualifiedName~LinterAnalyzerTests|FullyQualifiedName~McpToolResultsTests|FullyQualifiedName~MetricsTreeRoslynScannerTests|FullyQualifiedName~MetricsTreeToolTests|FullyQualifiedName~PatternDetectScannerTests|FullyQualifiedName~PatternDetectToolTests|FullyQualifiedName~SafeguardScannerTests|FullyQualifiedName~SafeguardToolTests|FullyQualifiedName~SuppressionScannerTests|FullyQualifiedName~SymbolIdentifierResolverTests"`
- [ ] Nach vollständiger Migration: `dotnet build`.
- [ ] Gemeinsamer FastTests-Kohortengate:
  `dotnet test src/AiNetLinter.FastTests --no-build --filter "FullyQualifiedName~AiNetLinter.FastTests.Mcp|FullyQualifiedName~AiNetLinter.FastTests.Suppression|FullyQualifiedName~LinterAnalyzerArchitectureRuleTests|FullyQualifiedName~LinterAnalyzerTests|FullyQualifiedName~RoslynTestSolutionFactoryTests|FullyQualifiedName~PreparedSolutionFixtureTests|FullyQualifiedName~FastTestsDependencyGuardTests|FullyQualifiedName~TestCategoryProfileGuardTests"`
- [ ] Ledger- und Legacy-Build-Invarianten:
  `dotnet test src/AiNetLinter.IntegrationTests --no-build --filter "FullyQualifiedName~TestMigrationLedgerConsistencyTests|FullyQualifiedName~LegacyProjectBuildGateTests"`
- [ ] Bei einem item-lokalen Fehlschlag zuerst nur die betroffene(n) Zielklasse(n) filtern; den
  kombinierten Gate erst nach lokalem Fix wiederholen.
- [ ] Kein Stress-, `Category!=Stress`-, MSBuild-, Dogfood-, Prozess- oder Repo-Profil: Trotz Größe
  bleibt step-018 eine EPIC-4-In-Memory-Strukturmigration und schließt das Epic noch nicht ab.

## Definition of Done

- [ ] Alle 14 Items umgesetzt; 24 historische Legacy-Testklassen liegen vollständig in
  `AiNetLinter.FastTests`, mit `Unit` nur für reine Parserlogik und sonst `Component`.
- [ ] Alle nicht-trivialen Assertions, Fehler-/Negativfälle, Truncation-/Sufficiency- und
  Structured-Content-Verträge sind erhalten oder durch engere gleichwertige Assertions ersetzt.
- [ ] Die 24 Legacy-Dateien sind physisch gelöscht; Ledger und Codemap zeigen ausschließlich die
  neuen Abdeckungsorte und der Konsistenzguard ist grün.
- [ ] Kein migrierter Test verwendet `SourceFileCatalog.LoadAsync`, MSBuild, Prozessstart, echtes
  Repository, Git, Config-/Call-Log-Dateimutationen oder eine serialisierende Collection.
- [ ] Prepared Standard-Snapshots materialisieren lazy und read-only; isolierte Spezial-Szenarien
  besitzen und entsorgen ihre Workspaces/Server deterministisch.
- [ ] Kein Produkt-Seam und keine generische TestKit-Abstraktion wurde für diesen Batch ergänzt;
  produktive Änderungen beschränken sich auf nachgewiesene Defekte samt Regressionstest.
- [ ] `dotnet build` und alle unter „Tests" genannten gezielten Gates sind grün; kein Voll- oder
  Stressprofil wurde ausgeführt.
- [ ] Commit auf aktuellem Branch (Conventional Commit auf Deutsch mit `[speedup-tests]`)
- [ ] `step-018/step-result.md` mit item-genauer Evidenz und verbleibenden EPIC-4-Resten geschrieben
- [ ] `status` in `step-plan.md` von `in_progress` auf `done (pending audit)` gesetzt

## Rules-Refs

- `.agents/rules/AiNetLinter.mdc#Kurz-Stil` und `#general` — nullable-fähige, kleine Helper,
  Namespace-/Verzeichnis-Mapping und Duplicate-Code-Vermeidung.
- `.agents/rules/AiNetLinterRichtlinien.mdc#4-Updates--Tests` — xUnit-v3, keine neue
  serialisierende Collection und MCP-Verifikation ausschließlich in C#-Testinfrastruktur.
- `.agents/rules/AiNetLinterRichtlinien.mdc#5-Qualitätsdrift-Prävention` — keine abgeschwächten
  Assertions, kein stilles Cleanup und keine Task-/Step-Referenzen in C#-Kommentaren.

## Bekannte Ausnahmen

- Die sieben im JIT-Kontext aufgezählten Datei-/Config-/Call-Log-/Git-Klassen bleiben bewusst
  pending; ihre teuren Grenzen sind Vertragsgegenstand und dürfen nicht in-memory simuliert werden.
- Der Batch ist mit 24 Legacy-Dateien deutlich unter `max_batch_items: 40`; weitere pending
  `Mcp/Tools`-Dateien sind nicht logisch kompatibel, nicht künstlich kleinteilig zurückgehalten.

## Notes

- `SymbolGraphMini` nur in den fachlich benötigten Merkmalen deklarativ spiegeln; keine zufälligen
  Fixture-Details übernehmen und kein wachsendes Universal-Szenario erzeugen.
- Der gemeinsame Serverkontext darf keine Loading-/Refresh-/Call-Log-/Registrierungslogik
  abstrahieren. Er ist ausschließlich eine Besitzerhülle für direkte read-only Toolaufrufe.
- Nach diesem Super-Step bleibt als konkreter EPIC-4-Grenzrest die gemischte Find-Symbol-Kohorte
  (`FindSymbolScannerTests`/`FindSymbolToolTests`): ihre Roslyn-Matrix ist componentfähig, ihre
  Nicht-C#-Miss-Hints sind echte Dateisystemverträge. Der nächste JIT-Abgleich muss entscheiden,
  ob die Klassen ohne Produkt-Seam sauber entlang dieser Grenze geteilt werden können. Die übrigen
  fünf ausgeschlossenen Tooldateien gehören wegen Dateiinventar, Config-/Call-Log-Mutation oder
  Git-Impact zu EPIC-5/6; Server-, Registrierungs- und Prozessklassen bleiben ebenfalls dort.
