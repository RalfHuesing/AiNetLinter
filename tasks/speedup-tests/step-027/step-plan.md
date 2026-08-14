---
status: done (pending audit)
type: step-plan
task: speedup-tests
step: 027
corrects: step-026
title: "Korrektur: Git-Workspace-Cleanup und Kategorieguard abschliessen"
epic: EPIC-6
estimated_risk: medium
step_type: batch
items:
  - id: item-01
    title: "Git-Impact-Workspace idempotent freigeben"
    source: "step-026/step-review.md Finding 1"
  - id: item-02
    title: "Zehn redundante Unit-Methodentraits entfernen"
    source: "step-026/step-review.md Finding 2"
  - id: item-03
    title: "Kategorieguard vollständig im TestKit konsolidieren"
    source: "step-026/step-review.md Finding 3 / TD-006"
created_by: planer
created_by_model: gpt-5.6-sol
created_by_model_knowledge_cutoff: nicht ausgewiesen
created_at: 2026-08-14T12:05:49+02:00
related_to:
  - step-026/step-review.md
---

# Step 027: Korrektur: Git-Workspace-Cleanup und Kategorieguard abschliessen

## Bezug

- **Task:** `speedup-tests`
- **Epic:** `EPIC-6` — Abschluss der in Step 026 migrierten Mini-MCP-Hostkohorte.
- **Korrigiert:** `step-026`; zweite Korrektur der Kette `step-025 <- step-026 <- step-027`,
  unter `max_fix_rounds_per_step: 6`.
- **Exakter Scope:** ausschließlich die drei Findings aus `step-026/step-review.md`. Die dortigen
  „Sonstigen Beobachtungen“ sind kein eigener Scope; die zeitgebundenen XML-Kommentare in den
  beiden ohnehin geänderten Kategorieguard-Dateien dürfen jedoch gemäß Projektregel auf eine
  zeitstabile technische Begründung gekürzt werden.

## Aktueller Projektzustand (JIT-Kontext)

- `TestResults/step026-command-contracts.trx` ist rot: 11/13. Nur
  `RunAsync_ValidFixture_GetImpactWithGitRefReturnsCallSite` und
  `RunAsync_ValidFixture_GetImpactWithoutGitRefUncommittedReturnsCallSite` scheitern, jeweils
  nach erfolgreicher fachlicher Assertion beim Teardown in
  `GitImpactMiniFixtureWorkspace.Dispose()` (`FixtureWorkspaces.cs:69`). Der nachträgliche
  Einzel-Lauf 2/2 ersetzt dieses gemeinsame Gate nicht.
- Ownership ist bereits richtig geschnitten: `McpProcessHost.StartAsync(FixtureWorkspace,
  TimeSpan, CancellationToken)` übernimmt den Workspace. Bei erfolgreichem Start gibt ausschließlich
  `McpProcessHost.DisposeAsync()` in der Reihenfolge Client/Transport -> Workspace ->
  `McpProcessLifetimeGate`-Lease frei; im Startfehlerpfad räumt der `catch` Workspace und Lease auf.
  Die drei Git-Command-Aufrufer erzeugen den Workspace ohne eigenes `using` und übergeben ihn an
  den Host. Diese Ownership und Reihenfolge bleiben unverändert.
- Der konkrete Defekt liegt im spezialisierten Cleanup: `IsolatedFixtureLease.Dispose()` ist für
  ein bereits fehlendes Root-Verzeichnis idempotent, aber der davor laufende Override
  `GitImpactMiniFixtureWorkspace.Dispose()` enumeriert `RootPath` bedingungslos. Nach einer erneuten
  Freigabe ist das Root bereits gelöscht und die Vorstufe wirft, bevor die idempotente Basis erreicht
  wird. Deshalb wird nicht der Hostvertrag gelockert und kein Cleanup-Fehler verschluckt, sondern
  die Git-spezifische Vorstufe in denselben einmaligen Dispose-Zyklus wie die besitzende Lease gelegt.
- `McpServerCommandTests` besitzt korrekt genau einen Klassen-Trait `Category=Unit`, daneben aber
  an allen zehn Methoden denselben redundanten Trait. Die Kategorie bleibt nach dessen Entfernung
  unverändert.
- `TestCategoryTraitInspector` ist bereits xUnit-frei im TestKit vorhanden, zentralisiert aber nur
  `GetTestClasses(Assembly)` und `GetCategoryTraits(Type)`. Beide Assemblyguards duplizieren weiterhin
  den vollständigen Projektions-/Filter-/Formatierungsblock; `find_duplicates(scopeDir="src",
  minTokens=20)` meldet deshalb den exact-Cluster der beiden
  `EveryTestClass_HasExactlyOneValidCategoryTrait`-Methoden (1,00; 133 Tokens). Eine weitere
  assemblyübergreifende Kopie ist nicht zulässig.

## Intention

Step 027 macht den besitzenden Git-Workspace-Teardown eng und nachweisbar idempotent, ohne fachliche
Command-Assertions oder Prozesslimits abzuschwächen. Danach besitzt jede Fast-Testklasse nur ihren
einen wirksamen Kategorie-Trait, und die gesamte Kategorieprüfung liegt in einer xUnit-freien
TestKit-API, sodass der exact-Guard-Cluster verschwindet und TD-006 erst auf Basis dieses Nachweises
geschlossen wird.

## Konkrete Änderungen

### item-01: Git-Impact-Workspace idempotent freigeben

#### `src/AiNetLinter.IntegrationTests/Fixtures/FixtureWorkspaces.cs`

- `FixtureWorkspace.Dispose()` zur einzigen öffentlichen Dispose-Schablone machen; nicht länger
  virtuell überschreiben lassen. Mit privatem `int disposed` und
  `Interlocked.Exchange(ref disposed, 1)` exakt einmal in den Cleanup-Zyklus eintreten.
- Exakte Reihenfolge innerhalb dieses Zyklus:
  1. geschützten Hook `PrepareForDelete()` ausführen,
  2. anschließend im `finally` immer `lease.Dispose()` ausführen.
  Die Lease bleibt der einzige Owner der rekursiven Verzeichnislöschung.
- Neue Signatur exakt: `protected virtual void PrepareForDelete() { }`.
- `GitImpactMiniFixtureWorkspace` überschreibt nur
  `protected override void PrepareForDelete()`. Darin ausschließlich dann über
  `Directory.EnumerateFileSystemEntries(RootPath, "*", SearchOption.AllDirectories)` iterieren,
  wenn `Directory.Exists(RootPath)` wahr ist, und die bestehenden Attribute auf `Normal` setzen.
  Keine eigene `base.Dispose()`-Kette, kein zweiter Löschpfad, kein Catch-all.
- `using System.Threading;` ergänzen. `IsolatedFixtureLease`, Produktcode,
  `McpProcessHost`-Ownership und die Reihenfolge Client -> Workspace -> Permit nicht ändern.
- Falls das einmalige Dispose-Template trotz des nachfolgenden Ursachetests nicht genügt: stoppen
  und im Step-Result mit Stacktrace melden; keine Retry-Schleife, keine Timeout-Erhöhung und kein
  pauschales Verschlucken von `IOException`/`DirectoryNotFoundException` hinzufügen.

#### `src/AiNetLinter.IntegrationTests/Mcp/Tools/SymbolGraph/GetImpactToolIntegrationTests.cs`

- Einen engen, prozessfreien Ursachevertrag ergänzen:
  `GitImpactMiniFixtureWorkspace_DisposeTwice_DeletesRootWithoutThrowing`.
- Mechanik: Workspace ohne `using` erzeugen, `RootPath` sichern, einmal disponieren, das zweite
  `Dispose()` mit `Record.Exception` beobachten; `Assert.Null(exception)` und
  `Assert.False(Directory.Exists(rootPath))`. Keine Git-/MCP-Fachassertion ersetzen.
- Der Test liegt in der bestehenden `Category=Integration`-Klasse; kein Method-Trait ergänzen.

#### `src/AiNetLinter.IntegrationTests/Mcp/McpServerCommandContractTests.cs`

- Beide bestehenden Git-Impact-Verträge und ihre `Assert.Contains("CalculatorCaller.cs", ...)`
  unverändert lassen. Keine separate Caller-Disposal-Ownership hinzufügen: der Host bleibt Owner.

### item-02: Zehn redundante Unit-Methodentraits entfernen

#### `src/AiNetLinter.FastTests/Mcp/McpServerCommandTests.cs`

- Den Klassen-Trait `[Trait("Category", "Unit")]` an Zeile 13 behalten.
- Ausschließlich die zehn Methoden-Traits entfernen, jeweils direkt unter `[Fact]`:
  1. `ResolveSolutionPathOrError_TwoSlnxFiles_ReportsAmbiguousSolution`
  2. `ResolveSolutionPathOrError_NoSolutionFound_ReportsResourceNotFound`
  3. `ResolveSolutionPathOrError_SingleCandidate_ReturnsIt`
  4. `ResolveSolutionPathOrError_MissingPath_UsesCurrentDirectory`
  5. `ResolveMaxLineCount_ConfigWithCustomMaxLineCount_ReturnsConfiguredValue`
  6. `ResolveMaxLineCount_NoConfigPath_ReturnsMetricsConfigDefault`
  7. `ResolveConfig_ConfigWithCustomMaxLineCount_UsesConfigFromArgs`
  8. `ResolveConfig_NoConfigPath_ReturnsDefaultConfig`
  9. `ResolveConfig_ExplicitConfigPath_TakesPrecedenceOverAutoDiscovered`
  10. `ResolveConfig_NoExplicitConfigPath_AutoDiscoversRulesJsonInSolutionDirectory`
- Testkörper, Namen, Faktenzahl und Assertions nicht ändern.

### item-03: Kategorieguard vollständig im TestKit konsolidieren

#### `src/AiNetLinter.TestKit/TestCategoryTraitInspector.cs`

- Die bisher in beiden Guards duplizierte Pipeline vollständig hierher ziehen. Exakte neue API:
  `public static void EnsureEveryTestClassHasExactlyOneValidCategoryTrait(Assembly assembly,
  params string[] allowedCategories)`.
- Die Methode validiert Argumente, ermittelt über die vorhandene Reflection-Regel alle öffentlichen,
  konkreten Klassen mit eigenen öffentlichen `[Fact]`-/`[Theory]`-Methoden, extrahiert ausschließlich
  Klassen-Traits mit `Name == "Category"`, verlangt exakt einen Wert aus `allowedCategories` und
  wirft bei Verstößen eine `InvalidOperationException` mit der bisherigen vollständigen Liste
  `FullName [Category,...]` sowie den erlaubten Kategorien. Bei leerer Violation-Liste kehrt sie
  normal zurück.
- `GetTestClasses(Assembly)` und `GetCategoryTraits(Type)` nach der Konsolidierung `private static`
  machen; keine zweite öffentliche Teil-API und keine xUnit-Abhängigkeit ins TestKit aufnehmen.

#### `src/AiNetLinter.FastTests/Architecture/TestCategoryProfileGuardTests.cs`

- `EveryTestClass_HasExactlyOneValidCategoryTrait()` auf genau den einen Aufruf reduzieren:
  `TestCategoryTraitInspector.EnsureEveryTestClassHasExactlyOneValidCategoryTrait(
  typeof(TestCategoryProfileGuardTests).Assembly, "Unit", "Component");`
- Lokales `AllowedCategories`, LINQ, Formatierung und nicht mehr benötigte `using` entfernen.
- XML-Dokumentation nur auf den zeitstabilen technischen Vertrag kürzen; keine Referenz auf
  `konzept.md`, Step oder TD hinterlassen.

#### `src/AiNetLinter.IntegrationTests/Architecture/TestCategoryProfileGuardTests.cs`

- Dieselbe Ein-Zeilen-API mit den Werten `"Integration", "Dogfood", "Performance", "Stress"`
  aufrufen. Lokales `AllowedCategories`, LINQ, Formatierung und unbenutzte `using` entfernen.
- XML-Dokumentation zeitstabil halten. Keine Sibling-Assembly lesen und keine Guard-Logik lokal
  nachbauen. Die beiden winzigen Guardmethoden müssen unter dem `minTokens=20`-Duplikatfenster
  bleiben; nicht durch lokale Wrapper oder Meldungsaufbereitung wieder vergrößern.

#### `tasks/speedup-tests/tech-debt.md`

- TD-006 im Index und Volltext zunächst nicht als geschlossen voraussetzen. Erst nachdem beide
  Kategorieguards grün sind und `find_duplicates` keinen exact-Cluster der beiden Guardmethoden
  mehr liefert, Status präzisieren zu „geschlossen in step-027“. Bei verbleibendem exact-Cluster
  TD-006 wieder auf `offen` setzen und Step 027 nicht als erledigt melden.
- TD-001, TD-007, TD-008 und TD-010 nicht ändern.

#### Task-Artefakte nach erfolgreicher Implementierung

- `tasks/speedup-tests/step-026/step-result.md`: die falsche pauschale Gate-Aussage nicht
  historisch umschreiben; stattdessen knappe Korrektur-Evidenz/Verweis auf Step 027 ergänzen.
- `tasks/speedup-tests/step-027/step-result.md` schreiben; neue Testzahlen, TRX-Namen,
  Duplikatnachweis und Code-Commit dokumentieren. `step-plan.md` auf `done (pending audit)` setzen.
- `tasks/speedup-tests/codemap.md` JIT um die einmalige `FixtureWorkspace`-Dispose-Schablone und
  die vollständige TestKit-Kategorie-API ergänzen. `roadmap.md` in diesem Fix-Step nicht ändern.

## Tests und Gates

Alle Läufe mit `--no-restore`; nach einer Codeänderung zuerst bauen, danach Tests mit `--no-build`.
Jeder relevante Testlauf erhält per `--logger "trx;LogFileName=<name>.trx"` eine eigene Datei.

- [ ] **Ursachetest Cleanup:** nur
  `FullyQualifiedName~GitImpactMiniFixtureWorkspace_DisposeTwice_DeletesRootWithoutThrowing`;
  1/1 grün, Root nach erstem Dispose weg, zweiter Dispose ohne Ausnahme.
- [ ] **Blockierendes Command-Gate:**
  `dotnet test src/AiNetLinter.IntegrationTests --no-build --filter
  "FullyQualifiedName~McpServerCommandContractTests" --logger
  "trx;LogFileName=step027-command-contracts.trx"`; exakt **13/13**, beide Git-Impact-Methoden
  enthalten ihre unveränderte Caller-Assertion, TRX insgesamt `Passed`, kein Cleanup-Fehler.
- [ ] **Fast-Trait-/Guard-Gate:** die zehn `McpServerCommandTests` plus
  `AiNetLinter.FastTests.Architecture.TestCategoryProfileGuardTests` sowie statischer und Runtime-
  Dependency-Guard in einem Testhost; erwartbar gegenüber Step 026 weiterhin **13/13** (10 Command
  + Kategorieguard + zwei Dependency-Guards), kein Assembly-Cleanup-Fehler.
- [ ] **Integration-Kategorieguard:**
  `FullyQualifiedName~AiNetLinter.IntegrationTests.Architecture.TestCategoryProfileGuardTests`;
  1/1 grün.
- [ ] **Aktuelle enge Step-026-Matrix erneut:** derselbe Fast-Zielkohortenfilter wie
  `step026-fast-contracts.trx`/`step026-fast-guards.trx`, unverändert **69/69**; derselbe
  Integration-Kohortenfilter plus Process-Callsiteguard, zuvor **63/63**, jetzt wegen des einen
  neuen Cleanup-Ursachetests erwartbar **64/64**. Falls Discovery andere Zahlen liefert, nicht
  kompensieren: FQN-Liste gegen Step-026-TRX diffen und im Result begründen.
- [ ] **Ledger-/Legacyguards:** `TestMigrationLedgerConsistencyTests` und
  `LegacyProjectBuildGateTests`; weiterhin exakt **53 pending**.
- [ ] `dotnet build` über alle fünf Solution-Projekte, 0 Warnungen/Fehler.
- [ ] `git --no-pager diff --check`.
- [ ] `find_duplicates(scopeDir="src", minTokens=20)`: kein exact-Cluster, der beide
  `EveryTestClass_HasExactlyOneValidCategoryTrait`-Methoden umfasst. Anschließend Refactoring-
  Drift gezielt prüfen: beide Guards müssen die neue TestKit-API aufrufen; keine lokale
  `GetCategoryTraits`-/Trait-Reflection-Kopie.
- [ ] **Verboten:** kein voller Fast-/Integration-`Category!=Stress`-Lauf, kein Legacy-Volltest,
  kein Dogfood-, Performance- oder Stressprofil; keine globale Collection-Serialisierung.

## Definition of Done

- [ ] Alle drei Findings gemeinsam geschlossen; keine Assertion, Guard-Allowlist oder Kategorie
  abgeschwächt.
- [ ] Git-Workspace hat genau einen besitzenden Löschpfad, dessen Vorstufe und Lease-Dispose
  reentranzfest geordnet sind; 13/13 Command-Verträge laufen gemeinsam grün.
- [ ] Genau die zehn gelisteten Method-Traits fehlen, der Klassen-Trait bleibt.
- [ ] Trait-Discovery, Kategorieprüfung und Violation-Formatierung liegen ausschließlich in
  `TestCategoryTraitInspector`; beide Assemblyguards sind Ein-Zeilen-Konsumenten.
- [ ] TD-006 nur bei null exact-Guard-Cluster auf „geschlossen in step-027“; sonst Step stoppen.
- [ ] Enger Gate-Satz, Build, Drift-Audit und `git diff --check` grün; keine verbotenen Profile.
- [ ] Ein kohärenter Code-Commit, danach Step-Result/Doku-Commit nach Drift-Loop-Regel; kein Amend,
  Rebase, Push oder Commit außerhalb dieses Scopes.

## Fixbudget und Stop-Kriterien

- Maximal **zwei** Implementierungsversuche für item-01 und **ein** Implementierungsversuch für
  item-02/item-03, danach die engen Gates einmal final ausführen. Kein wiederholtes grünes Gate.
- Sofort stoppen, wenn die zwei Git-Verträge nach dem einmaligen Dispose-Template weiterhin an
  Cleanup scheitern, wenn eine Produktcodeänderung nötig erscheint, wenn der Host nicht mehr
  alleiniger Workspace-Owner wäre oder wenn der exact-Guard-Cluster bestehen bleibt. Dann
  Stacktrace/Cluster und Diff dokumentieren, aber keinen breiteren Umbau beginnen.
- Der Coder darf höchstens diese Dateien ändern: die sieben oben genannten `.cs`-Dateien,
  `tech-debt.md`, `codemap.md`, `step-026/step-result.md`, `step-027/step-plan.md` und das neue
  `step-027/step-result.md`. Ledger, Roadmap, Produktcode, Projektdateien und öffentliche Doku sind
  gesperrt.

## Rules-Refs

- `.agents/rules/AiNetLinter.mdc#Kurz-Stil` und `#Projekt-Overrides` — Nullable- und
  Testprojektgrenzen.
- `.agents/rules/AiNetLinterRichtlinien.mdc#4 Updates & Tests` — gezielte Ownership statt globaler
  Serialisierung.
- `.agents/rules/AiNetLinterRichtlinien.mdc#5 Qualitätsdrift-Prävention` — Ursache statt
  Symptomfix, keine Assertion-/Guard-Abschwächung, zeitstabile Kommentare.

## Bekannte Ausnahmen

- Die fehlende Pre-Move-Laufbaseline aus Step 026 bleibt historische Evidenzlücke und ist kein
  Scope dieses Fixes.
- TD-007, TD-008 und TD-010 bleiben offen. Dogfood, Performance, Stress und allgemeine CLI-
  Self-Repo-Verträge folgen erst nach Freigabe dieses Korrektur-Steps.

