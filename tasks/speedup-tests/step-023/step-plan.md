---
status: done (pending audit)
type: step-plan
task: speedup-tests
step: 023
corrects: null
title: "Config-/Suppression-Dateikohorte und EPIC-5-Grenzgate (21 Klassen)"
epic: EPIC-5
estimated_risk: high
step_type: batch
items:
  - id: item-01
    title: "Reine CLI- und Suppression-Policyvertraege nach FastTests"
    source: "konzept.md §9 Punkt 4 / Legacy-Ledger"
  - id: item-02
    title: "Reine Config- und Roslyn-Configvertraege nach FastTests"
    source: "konzept.md Leitplanke 1 / Legacy-Ledger"
  - id: item-03
    title: "Compound-Suppression-Vertraege nach FastTests"
    source: "konzept.md Coverage-Audit / Legacy-Ledger"
  - id: item-04
    title: "Config-Dateiadapter isoliert nach IntegrationTests"
    source: "konzept.md Leitplanke 4 / Legacy-Ledger"
  - id: item-05
    title: "Suppression-Dateiadapter isoliert nach IntegrationTests"
    source: "konzept.md Leitplanke 4 / Legacy-Ledger"
  - id: item-06
    title: "Disable-All-Command gegen vorhandene isolierte Fixture"
    source: "konzept.md Leitplanke 4 / Legacy-Ledger"
  - id: item-07
    title: "Agentenregel mechanisch synchronisieren"
    source: "tech-debt.md#TD-003"
  - id: item-08
    title: "Coverage, Ledger und EPIC-5-Grenzgates"
    source: "konzept.md Leitplanke 7-10 / Definition of Done"
created_by: planer
created_by_model: gpt-5.6-sol
created_by_model_knowledge_cutoff: nicht ausgewiesen
created_at: 2026-08-13
related_to:
  - step-021
  - step-022
---

# Step 023: Config-/Suppression-Dateikohorte und EPIC-5-Grenzgate (21 Klassen)

## Bezug

- **Task:** `speedup-tests`
- **Epic:** `EPIC-5` aus `roadmap.md` — letzter fachlicher Schnitt der Datei-/Config-/
  Suppression-Familie nach dem in step-021/022 abgeschlossenen MSBuild-/Baseline-/Refresh-Schnitt.
- **Konzept-Referenz:** §9 „Grosse Drift-Loop-Steps" Punkt 4, Leitplanken 1, 4, 7-10 und
  Definition of Done zu Coverage, Isolation, Ledger und Profilgrenzen.

## Aktueller Projektzustand (JIT-Kontext)

Step 021 migrierte 22 Baseline-/Cache-/Datei-/Refresh-Klassen, step-022 korrigierte das globale
MSBuild-Loadbudget und die Ownership des geteilten SymbolGraph-Katalogs; das Review ist mit
`3f94674` approved. Die verbleibende Config-/Suppression-Familie besteht aus 21 Legacy-Klassen,
126 `[Fact]`-/`[Theory]`-Methoden und 140 statisch sichtbaren xUnit-Fällen (Theory-InlineData
aufgefächert). Keine Klasse startet einen Prozess oder lädt einen `SourceFileCatalog`/MSBuild-
Workspace.

Zwölf Klassen mit 85 Methoden/99 Fällen sind reine Parser-, Policy-, Config-, Syntax- oder
vorbereitete Roslyn-Verträge und passen nach FastTests. Neun Klassen mit 41 Methoden/41 Fällen
lesen oder mutieren reale, je Test eindeutige Dateien/Verzeichnisse und passen nach
IntegrationTests. Nur `DisableAllCliTests` verwendet noch eine Legacy-
`BaselineMiniFixtureWorkspace`; die gleichnamige IntegrationTests-Primitive in
`Fixtures/FixtureWorkspaces.cs` kapselt bereits `IsolatedFixtureLease` und wird wiederverwendet.
Weder `LoadedFixture` noch sein Max-2-Loadgate werden benötigt, weil diese Kohorte keinen echten
Katalogload ausführt. Eine neue Produkt-Seam oder globale Test-Collection ist nicht belegt.

`DeveloperExperienceTests.SyncAgentRules_OnSelfRepository_UpdatesMdc` mutiert heute das reale
Arbeitsrepository und assertiert nur die Existenz einer Datei. Der stärkere bestehende Temp-
Vertrag `SyncAgentRules_GeneratesMdcFile_WritesSuccessfully` deckt Schreibpfad und Inhalt ab;
der Self-Repo-Fall ist daher beim Coverage-Audit als semantisch redundant zu konsolidieren, nicht
als Dogfood-Mutation in das normale Integration-Profil zu übernehmen. TD-003 trifft denselben
Agent-Rules-Configbereich und ist als mechanische Regenerierung risikoarm.

TD-010 wurde erneut geprüft: Step 023 entfernt mit `DisableAllCliTests` genau einen Konsumenten der
Legacy-Workspacefamilie, danach referenzieren weiterhin 20 Legacy-Dateien die sechs
`FixtureWorkspaceBase`-Ableitungen bzw. darauf aufbauende Catalog-/MCP-Fixtures. Diese gehoeren zu
Prozess-, Git-, Framing-, Retry-, Loading- und Stressverträgen in EPIC-6. Eine Vereinheitlichung
in diesem Step würde deren Migration vorziehen; TD-010 bleibt daher bis EPIC-7 offen.

## Intention

Nach diesem Step liegt die komplette verbleibende Config-/Suppression-Familie in der passenden
Zielassembly: breite fachliche Matrizen laufen ohne Platte in FastTests, echte Datei- und
Commandadapter arbeiten ausschließlich auf privaten Temp-Wurzeln bzw. einer privaten
`BaselineMini`-Kopie in IntegrationTests. Alle 21 Legacy-Quellen werden nach belegter Zielabdeckung
gelöscht und das Ledger atomar aktualisiert.

Mit grünen zielprojektweiten Korrektheitsprofilen, Guards, Coverage-Audit und dem verpflichtenden
`find_duplicates`-Drift-Audit schließt der Step EPIC-5. Der echte parallele MSBuild-
Registrierungsvertrag bleibt wegen seines Stressanteils ausdrücklich EPIC-6.

## Inventar und Klassifizierung

| Ziel | Historische Klassen | Methoden | xUnit-Fälle | Grenze |
|---|---:|---:|---:|---|
| FastTests `Unit` | `IgnoreSuppressionsCliTests`, `IgnoreSuppressionsIntegrationTests`, `ConfigNormalizerTests`, `PathOverridesTests`, `RuleMetadataRegistryTests`, `CompoundSuppressionEvaluatorTests`, `IgnoreSuppressionsFilterTests`, `SuppressionCommentParserTests`, `SuppressionEvaluatorTests` | 52 | 59 | reine Argument-/Config-/Suppression-Policy |
| FastTests `Component` | `AgentFeaturesTests`, `FileFilterEvaluatorTests`, `CompoundSuppressionIntegrationTests` | 33 | 40 | Adhoc-/vorbereitete Roslyn-Solution, kein MSBuild/FS |
| IntegrationTests `Integration` | `ConfigLoaderRulesJsonTests`, `ConfigSyncerTests`, `DeveloperExperienceTests`, `DisableAllCliTests`, `DisableAllCommentInjectorTests`, `DisableAllCommentRemoverTests`, `SuppressionFileResolverTests`, `SuppressionScannerTests`, `ViolationPathResolverTests` | 41 | 41 | reale private Dateien/Verzeichnisse bzw. isolierte Fixture-Mutation |
| **Gesamt** | **21** | **126** | **140** | keine Prozesse, kein MSBuild, keine Collection |

Alle historischen Testmethodennamen und Theory-Datensätze bleiben erhalten, außer einem explizit
als redundant konsolidierten Self-Repo-Sync-Fall. Wenn weitere semantische Duplikate konsolidiert
werden, dokumentiert das Result historische Methoden-/Fallzahl und eindeutige Zielverträge wie in
step-020; Assertions dürfen nicht abgeschwächt werden.

## Konkrete Änderungen

### item-01: Reine CLI- und Suppression-Policyvertraege nach FastTests (Risiko: medium)

- `Cli/IgnoreSuppressionsCliTests.cs`, `Cli/IgnoreSuppressionsIntegrationTests.cs` sowie
  `Suppression/IgnoreSuppressionsFilterTests.cs`, `SuppressionCommentParserTests.cs` und
  `SuppressionEvaluatorTests.cs` nach `src/AiNetLinter.FastTests/` migrieren.
- Alle fünf Klassen als `Unit` kategorisieren. Der historische Klassenname
  `IgnoreSuppressionsIntegrationTests` darf fachlich präzisiert werden, sofern Ledger und
  Methodenmapping den Zielort eindeutig festhalten; sein Verhalten ist reine In-Process-Policy.
- Vorhandene Parser-/Filter-/Web-Suppression-Einstiegspunkte direkt verwenden; keine neue
  gemeinsame Facade, kein Dateisystem und keine CLI-Prozessausführung ergänzen.

### item-02: Reine Config- und Roslyn-Configvertraege nach FastTests (Risiko: medium)

- `Configuration/ConfigNormalizerTests.cs`, `PathOverridesTests.cs` und
  `RuleMetadataRegistryTests.cs` als `Unit` nach FastTests migrieren.
- `Configuration/AgentFeaturesTests.cs` und `FileFilterEvaluatorTests.cs` als `Component`
  migrieren. Ihre lokalen `AdhocWorkspace`-/Compilation-Builder nach Möglichkeit durch
  `RoslynTestSolutionFactory` bzw. den bereits vorhandenen FastTests-`TestHelper` ersetzen;
  keinen zweiten allgemeinen Factory-/Helper-Layer einführen.
- `AgentFeaturesTests` vollständig gegen die aktuellen produktiven Einstiegspunkte auditieren;
  die breite `@covers`-Liste ist nur Suchsignal, kein Nachweis. Die Klasse darf entlang echter
  Produktverträge in präzisere Zielklassen geschnitten werden, bleibt aber eine atomare Ledger-
  Migration.
- Fast-Zieldateien dürfen keine `File`-/`Directory`-/Temp-, MSBuild-, Prozess- oder echte Repo-
  Abhängigkeit erhalten; der bestehende Dependency-Guard muss grün bleiben.

### item-03: Compound-Suppression-Vertraege nach FastTests (Risiko: medium)

- `Core/CompoundSuppressionEvaluatorTests.cs` als `Unit` und
  `Core/CompoundSuppressionIntegrationTests.cs` als `Component` migrieren; der zweite Name meint
  fachliche Integration innerhalb des Analyzers, nicht die IntegrationTests-Assembly.
- Für die Analyzer-Szenarien den bestehenden FastTests-`TestHelper.ParseCode` bzw.
  `RoslynTestSolutionFactory` wiederverwenden. Erfolgs-, Grenz-, inaktive, Full-Suppression-,
  Relaxed-Limit- und Severity-Override-Verträge vollständig erhalten.
- Produktive Compound-Suppression-Branches und Fehlerwege lesen; nur bei einer reproduzierbaren
  Lücke einen Zieltest ergänzen. Keine Änderung an `rules.json` oder am Regelverhalten in diesem
  Item.

### item-04: Config-Dateiadapter isoliert nach IntegrationTests (Risiko: high)

- `ConfigLoaderRulesJsonTests.cs`, `ConfigSyncerTests.cs` und `DeveloperExperienceTests.cs` unter
  `src/AiNetLinter.IntegrationTests/Configuration/` migrieren und `Integration` kategorisieren.
- Jeder schreibende Test erhält einen eindeutigen Temp-Root bzw. Dateipfad und räumt ihn über
  `try/finally`/besitzende Lifetime auf. Keine gemeinsame mutable `rules.json`, keine globale
  Collection und keine Mutation des Checkout-Verzeichnisses.
- `DeveloperExperienceTests` in reine und file-backed Zielverträge schneiden, falls das ohne
  künstliche Helper-Abstraktion reviewbarer ist. Reine `GenerateContent`-/Resolver-Verträge dürfen
  alternativ nach FastTests verschoben werden; die Inventarsumme und Ledger-Atomarität bleiben
  unverändert.
- `SyncAgentRules_OnSelfRepository_UpdatesMdc` nicht als Repo-Mutation übernehmen. Gegen den
  stärkeren privaten Temp-Sync-Vertrag auf Redundanz prüfen und konsolidieren; falls ein
  eigenständiger Vertrag nachweisbar ist, denselben Vertrag mit isolierter Temp-Wurzel formulieren.
  Kein Dogfood-Lauf und kein schreibender Zugriff auf `.agents/rules` aus dem Test.

### item-05: Suppression-Dateiadapter isoliert nach IntegrationTests (Risiko: medium)

- `DisableAllCommentInjectorTests`, `DisableAllCommentRemoverTests`,
  `SuppressionFileResolverTests`, `SuppressionScannerTests` und `ViolationPathResolverTests` nach
  `src/AiNetLinter.IntegrationTests/Suppression/` migrieren und `Integration` kategorisieren.
- Bestehende reine String-Verträge dürfen in gleichnamige FastTests-Policyklassen geschnitten
  werden, wenn dadurch Datei-Adapter und Kern klar getrennt werden; Ledger nennt einen primären
  Zielpfad, zusätzliche Zielorte kommen in Coverage-Notiz/CodeMap. Keine Produkt-Seam nötig.
- Für File-Adapter eindeutige Temp-Unterverzeichnisse statt lose globale Temp-Dateien bevorzugen;
  parallele Tests dürfen keine Pfade teilen. Worktree-Ausschluss, BOM, LF/CRLF, exakte/partielle
  Marker, Zeilennummern und Deduplizierung unverändert belegen.
- Den veralteten XML-Doc-Verweis in `SuppressionFileResolverTests` auf den gelöschten
  `SourceFileCatalogTests`-Namen fachlich aktualisieren oder entfernen; keine Step-/Task-ID in Code.

### item-06: Disable-All-Command gegen vorhandene isolierte Fixture (Risiko: high)

- `Suppression/DisableAllCliTests.cs` nach IntegrationTests migrieren. Für die zwei mutierenden
  Command-Verträge die vorhandene
  `AiNetLinter.IntegrationTests.Fixtures.BaselineMiniFixtureWorkspace` wiederverwenden; sie besitzt
  bereits eine `IsolatedFixtureLease` und die benötigten Pfade.
- `MaintenanceCommand.TryRunAsync` bleibt in-process und nutzt `RecordingLintConsole` bzw. einen
  bereits vorhandenen assembly-lokalen Sink. Die zwei reinen `Program.Main`-
  Argumentkonfliktverträge bleiben in-process; sie starten keinen externen Prozess.
- Kein `LoadedFixture`, kein `SourceFileCatalog.LoadAsync`, kein Loadgate und keine serielle
  Console-Collection einführen. Falls `Program.Main` tatsächlich globale Console-Umleitung nutzt,
  erst reproduzierbar belegen und dann den engsten vorhandenen Console-Isolationsvertrag nutzen;
  nicht vorsorglich die ganze Kohorte serialisieren.

### item-07: Agentenregel mechanisch synchronisieren (Risiko: low)

- TD-003 im ohnehin berührten Config-/Agent-Rules-Bereich schließen: mit dem dokumentierten
  Generatorpfad `dotnet run --project src/AiNetLinter -- --sync-agent-rules-only` die
  `.agents/rules/AiNetLinter.mdc` aus der bereits korrekten `rules.json` regenerieren.
- Diff fachlich begrenzen und prüfen: Der Abschnitt „Projekt-Overrides" muss `*Tests` sowie den
  separaten `AiNetLinter.TestKit`-Override zeigen; keine manuelle Parallelpflege generierten Texts.
- Den Zielvertrag so schärfen, dass `AgentRulesGenerator.GenerateContent` diese beiden Overrides
  belegt. `tech-debt.md` setzt TD-003 erst nach grünem Nachweis auf „geschlossen in step-023".

### item-08: Coverage, Ledger und EPIC-5-Grenzgates (Risiko: high)

- Alle 126 historischen Methoden/140 statisch sichtbaren Fälle gegen die aktuellen produktiven
  Einstiegspunkte, Branches, Fehlerfälle und dokumentierten Regressionen auditieren. Neue
  nicht-triviale Lücken auf der günstigsten korrekten Ebene schließen; StaticTestSentinel-Delta
  prüfen. Keine Produkt-Seam ohne konkret reproduzierten Bedarf.
- Erst nach vollständiger Zielabdeckung die 21 Legacy-Quellen physisch löschen und alle 21 Ledger-
  Zeilen atomar auf `migrated`/`consolidated` samt existierendem Zielort setzen. Split-Zielorte in
  einer maschinenlesbaren Primärzeile plus Coverage-Notiz dokumentieren.
- Roadmap nach erfolgreichem Audit auf EPIC-5 abgeschlossen setzen und CodeMap/Tech-Debt auf den
  tatsächlichen Stand aktualisieren. EPIC-6/7 bleiben offen.
- Vor EPIC-Abschluss den `drift-audit`-Skill ausführen: `find_duplicates(scopeDir="src",
  minTokens=20)`; jeden `exact`- und `near`-Cluster bewerten. Passende kleine Duplikate im
  berührten Bereich konsolidieren, andere echte Funde als Tech-Debt dokumentieren; `fuzzy` nicht
  auditieren. Bei auffälligem Config-/Temp-/Fixture-Helper optional `refactoring-drift` gegen den
  qualifizierten vorhandenen Helper ausführen.

## Tech-Debt-Entscheidungen

- **TD-003:** einschließen und schließen; `auto_fixable: ja`, derselbe Config-/Agent-Rules-
  Bereich, mechanische Regenerierung plus Zielvertrag.
- **TD-010:** aktiv erneut geprüft, bleibt offen. Ein Legacy-Konsument entfällt, 20 Dateien der
  EPIC-6-Fixture-/Prozessfamilie bleiben; vorzeitige Basisklassenmigration wäre sachfremd.
- **TD-008:** bleibt offen. `CreateDefaultConfig`/`ParseCode`-Konsumenten wechseln auf den bereits
  vorhandenen FastTests-Helper und vergrößern die Drift nicht; Legacy-Kopien haben weiterhin
  Konsumenten bis EPIC-7. Kein Allzweck-TestKit-Helper.
- **TD-006:** bleibt offen; Kategorieguards laufen als Gate, werden aber nicht geändert. Eine
  gemeinsame Trait-Auslesung braucht weiterhin Assembly-/Abhängigkeitsentscheidung.
- **TD-007:** bleibt offen; keine Skeleton-Datei oder dritter `CreateConfig`-Konsument im Scope.
- **TD-001:** bleibt für EPIC-6; Step 023 startet keinen MCP-Subprozess und liefert keine Evidenz
  zur lastabhängigen Framing-Flakiness.
- **TD-002, TD-004, TD-005, TD-009:** bereits geschlossen; nicht erneut bearbeiten.

## Bewusst ausgeschlossen

- `SourceFileCatalogRegisterMSBuildTests`: enthält den parallelen echten MSBuild-Load-/
  Registrierungs-Stressvertrag und bleibt atomar pending für EPIC-6; kein Stress im Grenzgate.
- `GetImpactToolTests`: echte Git-Repository-/Diff-Mutation, EPIC-6.
- Commands/CLI-Prozess, MCP-Transport, Framing, Loading/Retry, Options-/Registrierungsfamilien,
  Live-Repo/Dogfood, Performance und Stress: EPIC-6.
- Output-/Metrics-/Maps-/Core-Restklassen ohne Config-/Suppression-Dateigrenze: Restmigration
  nach dem nächsten JIT-Schnitt, nicht künstlich an diese Kohorte anhängen.
- Legacy-Projektlöschung, finale Profil-/Messmethodik-/Doku-Bereinigung: EPIC-7.

## Tests

- [ ] Einmalige Legacy-Baseline **vor** der Löschung für exakt 21 Klassen:
  `dotnet test src/AiNetLinter.Tests --filter "FullyQualifiedName~IgnoreSuppressionsCliTests|FullyQualifiedName~IgnoreSuppressionsIntegrationTests|FullyQualifiedName~AgentFeaturesTests|FullyQualifiedName~ConfigLoaderRulesJsonTests|FullyQualifiedName~ConfigNormalizerTests|FullyQualifiedName~ConfigSyncerTests|FullyQualifiedName~DeveloperExperienceTests|FullyQualifiedName~FileFilterEvaluatorTests|FullyQualifiedName~PathOverridesTests|FullyQualifiedName~RuleMetadataRegistryTests|FullyQualifiedName~CompoundSuppressionEvaluatorTests|FullyQualifiedName~CompoundSuppressionIntegrationTests|FullyQualifiedName~DisableAllCliTests|FullyQualifiedName~DisableAllCommentInjectorTests|FullyQualifiedName~DisableAllCommentRemoverTests|FullyQualifiedName~IgnoreSuppressionsFilterTests|FullyQualifiedName~SuppressionCommentParserTests|FullyQualifiedName~SuppressionEvaluatorTests|FullyQualifiedName~SuppressionFileResolverTests|FullyQualifiedName~SuppressionScannerTests|FullyQualifiedName~ViolationPathResolverTests"`.
  Erwartungsanker: 126 Methoden, 140 statisch sichtbare Fälle; tatsächliche Runner-Fallzahl im
  Result dokumentieren.
- [ ] `dotnet build`.
- [ ] Gezielte Fast-Zielkohorte plus Dependency-/Kategorieguards; exakten finalen Klassenfilter im
  Result dokumentieren. Keine Fast-Zieldatei darf File/Directory/Temp, MSBuild, Process oder echtes
  Repo referenzieren.
- [ ] Gezielte Integration-Zielkohorte plus `TestCategoryProfileGuardTests`; statisch belegen, dass
  keine neue `SourceFileCatalog.LoadAsync(`-Callsite, kein Prozessstart, keine globale Collection
  und kein schreibender Self-Repo-Test hinzugekommen ist.
- [ ] Ledger-/Legacy-Guards:
  `dotnet test src/AiNetLinter.IntegrationTests --no-build --filter "FullyQualifiedName~TestMigrationLedgerConsistencyTests|FullyQualifiedName~LegacyProjectBuildGateTests|FullyQualifiedName~TestCategoryProfileGuardTests"`.
- [ ] EPIC-5-Grenzgate Fast-Korrektheitsprofile:
  `dotnet test src/AiNetLinter.FastTests --no-build --filter "Category=Unit|Category=Component"`.
- [ ] EPIC-5-Grenzgate Integration-Korrektheitsprofil:
  `dotnet test src/AiNetLinter.IntegrationTests --no-build --filter "Category=Integration"`.
  Kein Dogfood-, Performance- oder Stressprofil und kein Legacy-/Solution-Volltest.
- [ ] TD-003-Generatorlauf und enger Zielvertrag grün; generierten Diff gegen `rules.json` prüfen.
- [ ] Drift-Audit gemäß item-08 vollständig bewertet und Ergebnis im Step-Result dokumentiert.
- [ ] `git --no-pager diff --check`.

## Definition of Done

- [ ] 21 historische Klassen / 126 Methoden / 140 statisch sichtbare Fälle sind vollständig
  migriert oder semantisch transparent konsolidiert; keine der 21 Legacy-Quellen bleibt bestehen.
- [ ] Zwölf reine historische Klassen liegen als Unit/Component in FastTests, neun file-backed
  historische Klassen als Integration-Verträge; eventuelle fachliche Splits sind im Ledger und
  Coverage-Audit nachvollziehbar.
- [ ] Alle Datei-Mutationen besitzen private Temp-Wurzeln oder die vorhandene isolierte
  `BaselineMiniFixtureWorkspace`; keine Checkout-Mutation, kein MSBuild-Load, kein Prozess und
  keine neue globale Collection.
- [ ] TD-003 ist mechanisch geschlossen; TD-001/006/007/008/010 bleiben mit der oben begründeten
  Abgrenzung offen. TD-010 dokumentiert den verbleibenden 20-Dateien-Anker.
- [ ] Ledger-, Legacy-Build-, Kategorie- und Fast-Dependency-Guards sowie `dotnet build` sind grün.
- [ ] Beide EPIC-5-Korrektheitsprofil-Grenzgates und der `find_duplicates`-Drift-Audit sind grün
  bzw. vollständig bewertet; keine Dogfood-/Performance-/Stress- oder Task-Vollausführung.
- [ ] EPIC-5 ist nach erfolgreichem Kritiker-Audit in der Roadmap abgeschlossen; EPIC-6/7 bleiben
  offen und `AiNetLinter.Tests` bleibt wegen pending Einträgen baubar in der Solution.
- [ ] Kohärente Conventional Commits auf Deutsch mit `[speedup-tests]`; kein Amend/Rebase/Push.
  `step-023/step-result.md` geschrieben, Planstatus `done (pending audit)`.

## Rules-Refs

- `.agents/rules/AiNetLinter.mdc#Kurz-Stil` und `#Projekt-Overrides` — Nullable,
  Testmethodenlimit, generierter Override-Stand und aktive Qualitätsgrenzen.
- `.agents/rules/AiNetLinterRichtlinien.mdc#3 Windows-Umgebung & Tool-Regeln` — Windows-Pfade,
  Build/Test/TRX-Diagnose.
- `.agents/rules/AiNetLinterRichtlinien.mdc#4 Updates & Tests` — korrekte Kategorien,
  Parallelität ohne vorsorgliche Collection-Serialisierung und C#-basierte MCP-Nachweise.
- `.agents/rules/AiNetLinterRichtlinien.mdc#5 Qualitätsdrift-Prävention` — keine abgeschwächten
  Assertions, keine Task-IDs in dauerhaftem Code und Ursache statt Symptom-Fix.
- `.agents/skills/drift-audit/SKILL.md` — solutionweiter Exact-/Near-Duplikat-Audit vor
  EPIC-Abschluss ausschließlich über `find_duplicates`.

## Bekannte Ausnahmen

- Der historische Self-Repo-Sync-Test wird nur bei belegter semantischer Redundanz konsolidiert;
  das Result muss diese Entscheidung explizit dem stärkeren isolierten Zielvertrag zuordnen.
- `SourceFileCatalogRegisterMSBuildTests` bleibt als bewusst ungeteilter Stress-/Registrierungs-
  Vertrag pending und verhindert nicht den fachlichen Abschluss von EPIC-5.

## Notes

- Der 800-Diffzeilen-Wert ist ein Richtwert für inhaltliche Änderungen. Reine Renames der 3.218
  Legacy-Quellzeilen sind kein Grund, die geschlossene 21er-Familie in Mini-Steps zu zerlegen;
  tatsächliche Splits/Helper-Änderungen müssen reviewbar bleiben.
- Falls die Legacy-Baseline bereits rot ist, Ergebnis dokumentieren und nur bei einem
  migrationsrelevanten reproduzierbaren Defekt blockieren; keine Assertion abschwächen.
- Der Drift-Audit wird in der Umsetzung an der EPIC-Grenze ausgeführt. Diese Planungsphase führt
  weder `find_duplicates` noch Build oder Tests aus.
