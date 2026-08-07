---
status: open
type: step-plan
task: flaky-and-test-performance
step: 015
corrects: null
title: "Category-Traits für McpServerCommandTests.cs — letzter EPIC-02-Schritt (20 method-level Items)"
epic: EPIC-02
estimated_risk: low
step_type: batch
items:
  - id: item-01
    title: "ResolveSolutionPathOrError_TwoSlnxFiles_ReportsAmbiguousSolution → Unit (Zeile 32-33)"
    source: "konzept.md §Wie Schritt 2; static McpServerCommand.ResolveSolutionPathOrError in-process, TestLintConsole-Mock, kein Subprozess"
  - id: item-02
    title: "ResolveSolutionPathOrError_NoSolutionFound_ReportsResourceNotFound → Unit (Zeile 56-57)"
    source: "konzept.md §Wie Schritt 2; dito static Resolve*-Helper in-process"
  - id: item-03
    title: "ResolveSolutionPathOrError_SingleCandidate_ReturnsIt → Unit (Zeile 75-76)"
    source: "konzept.md §Wie Schritt 2; dito static Resolve*-Helper in-process"
  - id: item-04
    title: "ResolveSolutionPathOrError_MissingPath_UsesCurrentDirectory → Unit (Zeile 96-97)"
    source: "konzept.md §Wie Schritt 2; dito static Resolve*-Helper in-process, nur Directory.SetCurrentDirectory lokal"
  - id: item-05
    title: "TryLoadSolutionAsync_BrokenSlnx_LogsWarningWithoutThrowing → Unit (Zeile 120-121)"
    source: "konzept.md §Wie Schritt 2; static McpServerCommand.TryLoadSolutionAsync in-process (MSBuildWorkspace-Ladeversuch auf absichtlich kaputter .slnx, kein Subprozess, kein MCP-Client)"
  - id: item-06
    title: "RunAsync_ValidFixture_ServerRespondsWithThirteenTools → Integration (Zeile 144-145)"
    source: "konzept.md §Wie Schritt 2; _baselineMcpFixture.Client (BaselineMcpFixture = echter MCP-Subprozess via StdioClientTransport)"
  - id: item-07
    title: "RunAsync_ValidFixture_GetHotspotsReturnsAllGreenForSmallFixture → Integration (Zeile 165-166)"
    source: "konzept.md §Wie Schritt 2; _symbolGraphMcpFixture.Client (SymbolGraphMcpFixture über [Collection(\"SymbolGraphMcp\")] = geteilter MCP-Subprozess)"
  - id: item-08
    title: "RunAsync_ValidFixture_GetIndexScopeReturnsFileTypeBreakdown → Integration (Zeile 177-178)"
    source: "konzept.md §Wie Schritt 2; _symbolGraphMcpFixture.Client"
  - id: item-09
    title: "RunAsync_ValidFixture_GetViolationsReturnsAtLeastOneViolation → Integration (Zeile 191-192)"
    source: "konzept.md §Wie Schritt 2; _symbolGraphMcpFixture.Client"
  - id: item-10
    title: "RunAsync_ValidFixture_SearchPatternReturnsExpectedHit → Integration (Zeile 203-204)"
    source: "konzept.md §Wie Schritt 2; _symbolGraphMcpFixture.Client"
  - id: item-11
    title: "RunAsync_ValidFixture_FindSymbolReturnsMatch → Integration (Zeile 215-216)"
    source: "konzept.md §Wie Schritt 2; _baselineMcpFixture.Client"
  - id: item-12
    title: "RunAsync_ValidFixture_FindReferencesReturnsCallSite → Integration (Zeile 227-228)"
    source: "konzept.md §Wie Schritt 2; _symbolGraphMcpFixture.Client"
  - id: item-13
    title: "RunAsync_ValidFixture_GetImpactWithGitRefReturnsCallSite → Integration (Zeile 239-240)"
    source: "konzept.md §Wie Schritt 2; GitImpactMiniFixtureWorkspace + McpTestClient.ConnectAsync(fixture.RootPath) — eigener, lokal gestarteter MCP-Subprozess pro Testfall"
  - id: item-14
    title: "RunAsync_ValidFixture_GetImpactWithoutGitRefUncommittedReturnsCallSite → Integration (Zeile 255-256)"
    source: "konzept.md §Wie Schritt 2; dito GitImpactMiniFixtureWorkspace + McpTestClient.ConnectAsync"
  - id: item-15
    title: "RunAsync_ValidFixture_GetFileSkeletonReturnsGreeterSignature → Integration (Zeile 271-272)"
    source: "konzept.md §Wie Schritt 2; _symbolGraphMcpFixture.Client"
  - id: item-16
    title: "RunAsync_ValidFixture_GetTypeHierarchyReturnsBaseGreetingHierarchy → Integration (Zeile 283-284)"
    source: "konzept.md §Wie Schritt 2; _symbolGraphMcpFixture.Client"
  - id: item-17
    title: "ResolveMaxLineCount_ConfigWithCustomMaxLineCount_ReturnsConfiguredValue → Unit (Zeile 296-297)"
    source: "konzept.md §Wie Schritt 2; static McpServerCommand.ResolveMaxLineCount in-process, reine Config-Auswertung auf Temp-rules.json, kein Subprozess"
  - id: item-18
    title: "ResolveMaxLineCount_NoConfigPath_ReturnsMetricsConfigDefault → Unit (Zeile 316-317)"
    source: "konzept.md §Wie Schritt 2; dito static Resolve*-Helper in-process"
  - id: item-19
    title: "ResolveConfig_ConfigWithCustomMaxLineCount_UsesConfigFromArgs → Unit (Zeile 326-327)"
    source: "konzept.md §Wie Schritt 2; static McpServerCommand.ResolveConfig in-process auf Temp-rules.json, kein Subprozess"
  - id: item-20
    title: "ResolveConfig_NoConfigPath_ReturnsDefaultConfig → Unit (Zeile 347-348)"
    source: "konzept.md §Wie Schritt 2; dito static ResolveConfig in-process"
created_by: planer
created_by_model: claude-sonnet-5
created_by_model_knowledge_cutoff: 2026-01
created_at: 2026-08-08T10:00:00+02:00
related_to:
  - step-014
---

# Step 015: Category-Traits für McpServerCommandTests.cs — letzter EPIC-02-Schritt

## Bezug

- **Task:** `flaky-and-test-performance`
- **Epic:** `EPIC-02` aus `roadmap.md` — Category-Traits flächendeckend
  nachziehen. **Letzter verbleibender Batch** dieses Epics: die einzige
  in `step-014` bewusst ausgeklammerte Klasse
  `src/AiNetLinter.Tests/Commands/McpServerCommandTests.cs`. Nach
  `approved` dieses Steps ist EPIC-02 **vollständig abgeschlossen** —
  alle ~1325 Testmethoden im Projekt tragen einen Category-Trait.
- **Konzept-Referenz:** `konzept.md` §"Wie" Schritt 2, §"Muss-Haben"
  Traits-Punkt, §"Definition of Done" Punkt "Alle Tests tragen einen
  Category-Trait".
- **Vorgänger:** `step-014` (approved, Code-Commit `c46d8399b31e`,
  Doku-Commit `98e2e9a`) — Filterstand danach: Unit **1184** /
  Integration **121** / Total **1325**.

## Aktueller Projektzustand (JIT-Kontext)

`McpServerCommandTests.cs` vollständig gelesen (nicht nur die
step-014-Prognose übernommen). Datei: `#nullable enable`,
`[Collection("SymbolGraphMcp")]` + `IClassFixture<BaselineMcpFixture>`
auf Klassen-Ebene (Z. 18-19), zwei injizierte Fixtures im Konstruktor
(`_symbolGraphMcpFixture`, `_baselineMcpFixture`). **23 `[Fact]`-Methoden
total**, davon **3 bereits method-level `[Trait("Category", "Unit")]`**
getaggt (Z. 358-360 `ResolveConfig_ExplicitConfigPath_...`, Z. 395-397
`ResolveConfig_NoExplicitConfigPath_AutoDiscovers...`, Z. 425-427
`ResolveConfig_NoExplicitConfigPath_NoRulesJsonFound_UsesDefault` —
diese dritte ruft `McpServerCommand.RunAsync(args, cts.Token, console)`
mit vorab gecancelltem Token und `TestLintConsole`-Mock in-process auf,
**kein** MCP-Client/Subprozess, daher zu Recht Unit).

**Methode-für-Methode-Verifikation der 20 ungetaggten Facts** (Byte-/
Code-Scan, nicht nur die step-014-Prognose übernommen):

- **9 Unit** — alle rufen ausschließlich statische
  `McpServerCommand.Resolve*`/`TryLoadSolutionAsync`-Helper in-process
  auf, arbeiten auf temporären Verzeichnissen/Dateien
  (`Path.GetTempPath()`-Workspaces), nutzen **keine** der beiden
  Klassen-Fixtures und starten **keinen** Subprozess:
  `ResolveSolutionPathOrError_TwoSlnxFiles_ReportsAmbiguousSolution`,
  `ResolveSolutionPathOrError_NoSolutionFound_ReportsResourceNotFound`,
  `ResolveSolutionPathOrError_SingleCandidate_ReturnsIt`,
  `ResolveSolutionPathOrError_MissingPath_UsesCurrentDirectory`,
  `TryLoadSolutionAsync_BrokenSlnx_LogsWarningWithoutThrowing` (lädt
  eine absichtlich kaputte `.slnx` via `MSBuildWorkspace` in-process —
  schlägt kontrolliert fehl, kein MCP-Transport),
  `ResolveMaxLineCount_ConfigWithCustomMaxLineCount_ReturnsConfiguredValue`,
  `ResolveMaxLineCount_NoConfigPath_ReturnsMetricsConfigDefault`,
  `ResolveConfig_ConfigWithCustomMaxLineCount_UsesConfigFromArgs`,
  `ResolveConfig_NoConfigPath_ReturnsDefaultConfig`.
- **11 Integration** — alle rufen entweder `_symbolGraphMcpFixture.Client`
  oder `_baselineMcpFixture.Client` (beide über echten MCP-Subprozess,
  `StdioClientTransport`/`SymbolGraphMcpFixture`/`BaselineMcpFixture`)
  auf, oder starten über `McpTestClient.ConnectAsync(fixture.RootPath)`
  einen eigenen, lokal instanziierten MCP-Subprozess:
  `RunAsync_ValidFixture_ServerRespondsWithThirteenTools` (`_baselineMcpFixture`),
  `RunAsync_ValidFixture_GetHotspotsReturnsAllGreenForSmallFixture`
  (`_symbolGraphMcpFixture`),
  `RunAsync_ValidFixture_GetIndexScopeReturnsFileTypeBreakdown`
  (`_symbolGraphMcpFixture`),
  `RunAsync_ValidFixture_GetViolationsReturnsAtLeastOneViolation`
  (`_symbolGraphMcpFixture`),
  `RunAsync_ValidFixture_SearchPatternReturnsExpectedHit`
  (`_symbolGraphMcpFixture`),
  `RunAsync_ValidFixture_FindSymbolReturnsMatch` (`_baselineMcpFixture`),
  `RunAsync_ValidFixture_FindReferencesReturnsCallSite`
  (`_symbolGraphMcpFixture`),
  `RunAsync_ValidFixture_GetImpactWithGitRefReturnsCallSite`
  (`GitImpactMiniFixtureWorkspace` + `McpTestClient.ConnectAsync`),
  `RunAsync_ValidFixture_GetImpactWithoutGitRefUncommittedReturnsCallSite`
  (dito),
  `RunAsync_ValidFixture_GetFileSkeletonReturnsGreeterSignature`
  (`_symbolGraphMcpFixture`),
  `RunAsync_ValidFixture_GetTypeHierarchyReturnsBaseGreetingHierarchy`
  (`_symbolGraphMcpFixture`).

**Bestätigung gegenüber step-014-Prognose:** Die Klassifikation
9 Unit / 11 Integration ist **exakt deckungsgleich** mit der Prognose
aus `step-014/step-plan.md` §"Notes" — keine Abweichung. Die konkrete
Zeilen-genaue Verifikation war trotzdem nötig (Skill-Pflicht „nie nur
Prognose übernehmen"), da eine Prognose grundsätzlich unverifiziert ist,
bis der Code selbst gelesen wurde.

**EOL/BOM/Encoding** (Python-Byte-Scan): Datei durchgehend **CRLF**
(CR=479, LF=479, `CR==LF`), **kein** UTF-8-BOM, endet mit `\r\n`
(Trailing-NL erhalten). Keine LF-only-Ausreißer wie in vorherigen
Batches (`OverviewResourceRegistrationTests.cs` u. a.) — Standard-Edit-
Tool (kein Python-Helper nötig).

**Insert-Muster:** Alle 3 bereits vorhandenen method-level Traits
folgen demselben Muster — `[Trait("Category", "...")]` unmittelbar
zwischen `[Fact]` und der Methoden-Signatur (siehe Z. 358-360,
395-397, 425-427). Dieses etablierte Muster wird für alle 20 neuen
Inserts identisch übernommen — keine neue Trait-Platzierungs-Variante
nötig, kein XML-Doc-/`[Collection(...)]`-Sonderfall auf Methoden-Ebene
(die einzige Klassen-Ebene-Annotation `[Collection("SymbolGraphMcp")]`
bleibt unberührt, da die Klasse als Ganzes **nicht** klassen-weit
getaggt werden kann — genau deshalb ist dies ein method-level statt
class-level Batch).

**Rest-EPIC-02-Bestand:** Keiner — `Commands/`-Ordner war laut
`codemap.md` bereits 16/17 abgehakt, `McpServerCommandTests.cs` ist die
letzte Datei. Nach diesem Step sind **alle** Testverzeichnisse aus
`codemap.md` vollständig getaggt.

## Anti-Loop-Check gegen `codemap.md`

`codemap.md` §`Commands/`-Eintrag (zuletzt: step-014) vermerkt exakt:
„`McpServerCommandTests.cs` bewusst ausgeklammert […] braucht eigenen
Folge-Step, letzter verbleibender EPIC-02-Schritt". Dieser Step setzt
genau das um — kein Widerspruch, keine Korrektur eines bestehenden
Eintrags nötig. Die Klasse selbst wurde bereits in `step-001` auf
`[Collection("SymbolGraphMcp")]` umgestellt (Sharing für
`SymbolGraphMcpFixture`) — dieser Step ändert daran nichts, reine
additive Trait-Inserts auf Methoden-Ebene.

## Intention

Alle 20 method-level ungetaggten `[Fact]`-Methoden in
`McpServerCommandTests.cs` erhalten `[Trait("Category", "Unit"/
"Integration")]`, identisch platziert zu den 3 bereits vorhandenen
method-level Traits in derselben Datei. Danach ist **EPIC-02
vollständig abgeschlossen** — jede Testmethode im Projekt trägt einen
Category-Trait, `dotnet test --filter Category=Unit` bzw.
`Category=Integration` decken zusammen den kompletten Testbestand ab
(1193 + 132 = 1325 = Total).

## Konkrete Änderungen

**Hinweis:** Alle 20 Items betreffen dieselbe Datei
(`src/AiNetLinter.Tests/Commands/McpServerCommandTests.cs`); jedes Item
ist trotzdem als eigene, individuell begründete Category-Entscheidung
zu behandeln (kein Klassen-weiter Trait möglich, da die Klasse
methodenweise gemischt ist).

### item-01: ResolveSolutionPathOrError_TwoSlnxFiles_ReportsAmbiguousSolution → Unit (Zeile 32-33)

- **Was:** `[Trait("Category", "Unit")]` zwischen `[Fact]` (Z. 32) und
  `public void ResolveSolutionPathOrError_TwoSlnxFiles_...` (Z. 33);
  Methodensignatur rückt auf Z. 34.
- **Warum:** ruft `McpServerCommand.ResolveSolutionPathOrError` statisch
  in-process auf einem temporären Verzeichnis mit zwei `.slnx`-Dateien
  auf; `TestLintConsole`-Mock statt echter Konsole; kein Subprozess,
  keine Fixture-Nutzung.

### item-02: ResolveSolutionPathOrError_NoSolutionFound_ReportsResourceNotFound → Unit (Zeile 56-57)

- **Was:** Trait zwischen `[Fact]` (Z. 56) und Methode (Z. 57).
- **Warum:** dito statischer Resolve-Helper in-process, leeres
  Temp-Verzeichnis.

### item-03: ResolveSolutionPathOrError_SingleCandidate_ReturnsIt → Unit (Zeile 75-76)

- **Was:** Trait zwischen `[Fact]` (Z. 75) und Methode (Z. 76).
- **Warum:** dito statischer Resolve-Helper in-process, eine `.slnx`
  im Temp-Verzeichnis.

### item-04: ResolveSolutionPathOrError_MissingPath_UsesCurrentDirectory → Unit (Zeile 96-97)

- **Was:** Trait zwischen `[Fact]` (Z. 96) und Methode (Z. 97).
- **Warum:** dito statischer Resolve-Helper in-process; einziger
  Seiteneffekt ist `Directory.SetCurrentDirectory` (lokal, im
  `finally`-Block zurückgesetzt) — kein Subprozess.

### item-05: TryLoadSolutionAsync_BrokenSlnx_LogsWarningWithoutThrowing → Unit (Zeile 120-121)

- **Was:** Trait zwischen `[Fact]` (Z. 120) und
  `public async Task TryLoadSolutionAsync_BrokenSlnx_...` (Z. 121).
- **Warum:** ruft `McpServerCommand.TryLoadSolutionAsync` statisch
  in-process auf einer absichtlich kaputten `.slnx`-Datei auf
  (MSBuildWorkspace-Ladeversuch, kontrolliert fehlschlagend); kein
  MCP-Client, kein `AiNetLinter.exe`-Subprozess.

### item-06: RunAsync_ValidFixture_ServerRespondsWithThirteenTools → Integration (Zeile 144-145)

- **Was:** Trait zwischen `[Fact]` (Z. 144) und
  `public async Task RunAsync_ValidFixture_ServerRespondsWith...`
  (Z. 145).
- **Warum:** nutzt `_baselineMcpFixture.Client.ListToolsAsync()` —
  `BaselineMcpFixture` startet einen echten MCP-Server-Subprozess.

### item-07: RunAsync_ValidFixture_GetHotspotsReturnsAllGreenForSmallFixture → Integration (Zeile 165-166)

- **Was:** Trait zwischen `[Fact]` (Z. 165) und Methode (Z. 166).
- **Warum:** `_symbolGraphMcpFixture.Client.CallToolAsync(...)` — über
  `[Collection("SymbolGraphMcp")]` geteilter MCP-Subprozess.

### item-08: RunAsync_ValidFixture_GetIndexScopeReturnsFileTypeBreakdown → Integration (Zeile 177-178)

- **Was:** Trait zwischen `[Fact]` (Z. 177) und Methode (Z. 178).
- **Warum:** dito `_symbolGraphMcpFixture.Client.CallToolAsync(...)`.

### item-09: RunAsync_ValidFixture_GetViolationsReturnsAtLeastOneViolation → Integration (Zeile 191-192)

- **Was:** Trait zwischen `[Fact]` (Z. 191) und Methode (Z. 192).
- **Warum:** dito `_symbolGraphMcpFixture.Client.CallToolAsync(...)`.

### item-10: RunAsync_ValidFixture_SearchPatternReturnsExpectedHit → Integration (Zeile 203-204)

- **Was:** Trait zwischen `[Fact]` (Z. 203) und Methode (Z. 204).
- **Warum:** dito `_symbolGraphMcpFixture.Client.CallToolAsync(...)`.

### item-11: RunAsync_ValidFixture_FindSymbolReturnsMatch → Integration (Zeile 215-216)

- **Was:** Trait zwischen `[Fact]` (Z. 215) und Methode (Z. 216).
- **Warum:** `_baselineMcpFixture.Client.CallToolAsync(...)` — echter
  MCP-Subprozess.

### item-12: RunAsync_ValidFixture_FindReferencesReturnsCallSite → Integration (Zeile 227-228)

- **Was:** Trait zwischen `[Fact]` (Z. 227) und Methode (Z. 228).
- **Warum:** dito `_symbolGraphMcpFixture.Client.CallToolAsync(...)`.

### item-13: RunAsync_ValidFixture_GetImpactWithGitRefReturnsCallSite → Integration (Zeile 239-240)

- **Was:** Trait zwischen `[Fact]` (Z. 239) und Methode (Z. 240).
- **Warum:** instanziiert lokal `GitImpactMiniFixtureWorkspace` und
  verbindet über `McpTestClient.ConnectAsync(fixture.RootPath)` — ein
  eigener, pro Testfall gestarteter MCP-Subprozess (unabhängig von den
  Klassen-Fixtures).

### item-14: RunAsync_ValidFixture_GetImpactWithoutGitRefUncommittedReturnsCallSite → Integration (Zeile 255-256)

- **Was:** Trait zwischen `[Fact]` (Z. 255) und Methode (Z. 256).
- **Warum:** dito `GitImpactMiniFixtureWorkspace` +
  `McpTestClient.ConnectAsync`.

### item-15: RunAsync_ValidFixture_GetFileSkeletonReturnsGreeterSignature → Integration (Zeile 271-272)

- **Was:** Trait zwischen `[Fact]` (Z. 271) und Methode (Z. 272).
- **Warum:** dito `_symbolGraphMcpFixture.Client.CallToolAsync(...)`.

### item-16: RunAsync_ValidFixture_GetTypeHierarchyReturnsBaseGreetingHierarchy → Integration (Zeile 283-284)

- **Was:** Trait zwischen `[Fact]` (Z. 283) und Methode (Z. 284).
- **Warum:** dito `_symbolGraphMcpFixture.Client.CallToolAsync(...)`.

### item-17: ResolveMaxLineCount_ConfigWithCustomMaxLineCount_ReturnsConfiguredValue → Unit (Zeile 296-297)

- **Was:** Trait zwischen `[Fact]` (Z. 296) und Methode (Z. 297).
- **Warum:** ruft `McpServerCommand.ResolveMaxLineCount` statisch
  in-process auf einer temporären `rules.json` auf; kein Subprozess.

### item-18: ResolveMaxLineCount_NoConfigPath_ReturnsMetricsConfigDefault → Unit (Zeile 316-317)

- **Was:** Trait zwischen `[Fact]` (Z. 316) und Methode (Z. 317).
- **Warum:** dito statischer `ResolveMaxLineCount`-Helper in-process,
  ohne `ConfigPath` (Default-Pfad).

### item-19: ResolveConfig_ConfigWithCustomMaxLineCount_UsesConfigFromArgs → Unit (Zeile 326-327)

- **Was:** Trait zwischen `[Fact]` (Z. 326) und Methode (Z. 327).
- **Warum:** ruft `McpServerCommand.ResolveConfig` statisch in-process
  auf einer temporären `rules.json` auf; kein Subprozess.

### item-20: ResolveConfig_NoConfigPath_ReturnsDefaultConfig → Unit (Zeile 347-348)

- **Was:** Trait zwischen `[Fact]` (Z. 347) und Methode (Z. 348).
- **Warum:** dito statischer `ResolveConfig`-Helper in-process, ohne
  `ConfigPath` (Default-Config).

## Tests

- [ ] Keine neuen Tests — nur Category-Traits.
- [ ] Verifikation über Filter-/Voll-Läufe in der Definition of Done.

## Definition of Done

- [ ] Alle 20 method-level Trait-Inserts umgesetzt (Insert-Muster exakt
  wie bei den 3 bereits vorhandenen: `[Fact]` → `[Trait("Category",
  "...")]` → Methoden-Signatur)
- [ ] `dotnet build` grün (0 Warnungen, 0 Fehler)
- [ ] Self-Lint: `dotnet run --project src/AiNetLinter -- --config
  rules.json --path .` → `OK` (TD-001-konform, **kein** `--self-lint`)
- [ ] `dotnet test --filter "Category=Unit"` → grün, **1193** Tests
  (1184 + 9)
- [ ] `dotnet test --filter "Category=Integration"` → best-effort
  (bekannter EPIC-06-Flaky `McpServerCommandLoadingStateTests…` darf
  failen; nicht step-015-Scope; dessen Testklasse wird von diesem Step
  nicht berührt), **132** Tests (121 + 11)
- [ ] `dotnet test` (Voll) → grün, **1325** Tests (±0 Total)
- [ ] **Numerische Vollständigkeits-Probe (EPIC-02-Abschluss-Kriterium):**
  Unit (1193) + Integration (132) = **1325** = Total — d. h. **jede**
  Testmethode im Projekt ist nach diesem Step kategorisiert
- [ ] **EOL-/BOM-Scan** der Datei vor/nach: CRLF durchgehend erhalten
  (CR==LF, +20 Zeilen), kein BOM vorher/nachher, Trailing-NL erhalten
  (Standard-Edit-Tool ausreichend, kein Python-Helper nötig)
- [ ] **Scope-Disziplin:** nur `McpServerCommandTests.cs` angefasst,
  keine anderen Dateien; die 3 bereits vorhandenen method-level Traits
  (Z. 358-360, 395-397, 425-427) bleiben unverändert; die Klassen-Ebene-
  Annotation `[Collection("SymbolGraphMcp")]` bleibt unverändert
- [ ] Code-Commit zuerst, Hash in `step-result.md`; Subject exakt:
  `test: McpServerCommandTests Method-Traits [flaky-and-test-performance]`
  (**70 Zeichen**, 2 Reserve zur 72-Grenze) — Coder übernimmt
  unverändert (TD-002-Disziplin)
- [ ] Doku-Commit danach inkl. `step-result.md`, Status
  `done (pending audit)`, **CodeMap-Update** (`Commands/`-Eintrag auf
  „vollständig abgehakt 17/17" + neuer Abschluss-Vermerk „EPIC-02
  vollständig abgeschlossen"); Subject-Beispiel:
  `docs(tasks): step-015 Result dokumentieren [flaky-and-test-performance]`
  (**71 Zeichen**)
- [ ] `step-result.md` mit Vorher/Nachher-EOL-Byte-Scan, Filter-Delta,
  Commit-Hashes; keine neuen Hilfsdateien-Leichen im Doku-Commit
  (TD-007-Analogie — Coder-Arbeitsartefakte gehören nicht in den
  Doku-Commit)
- [ ] Pipeline-Konvention step-011/012/013/014: Code-Commit → Result mit
  Hash → Doku-Commit (kein 3-Commits-Mechanismus)

## Rules-Refs

- `.agents/rules/AiNetLinterRichtlinien.mdc` §4 — Conventional Commits
  DE, Subject ≤ 72 Zeichen inkl. Suffix, `### Commit-Vorschlag`-Block-
  Pflicht; §5 Qualitätsdrift-Prävention (sparsame Kommentare, keine
  `step-`/`TD-`/`EPIC-`-Referenzen im Code — hier ohnehin nicht
  einschlägig, da reine `[Trait(...)]`-Attribut-Inserts ohne
  Kommentartext)
- `.agents/rules/AiNetLinter.mdc` — `*.Tests`-Overrides (u. a.
  `EnforceSealedClasses: false`); Trait-Schreibweise CamelCase
  `"Unit"`/`"Integration"` (Konvention aus allen vorherigen EPIC-02-
  Batches); `#nullable enable` (bereits Z. 1 vorhanden, nicht anfassen)

## Bekannte Ausnahmen

- Integration-Filter darf den bekannten EPIC-06-Flaky
  `McpServerCommandLoadingStateTests.LoadState_LoadFuncCompletesSynchronouslyWithCatalog_ReportsLoadedImmediately`
  failen lassen (out of scope; Voll-Lauf muss grün sein; diese Klasse
  liegt in einer anderen Datei und wird von diesem Step nicht berührt).
- TD-001 bis TD-007 **nicht** in diesem Step fixen (TD-001 Nutzer: OUT
  OF SCOPE; TD-002/003/004/005/006 `auto_fixable: nein`; TD-007 bereits
  erledigt in step-013) — kein `auto_fixable: ja`-Treffer im Bereich
  `Commands/` gefunden, der an diesen Step angehängt werden könnte.

## Code-Skizze (optional)

```csharp
// Etabliertes method-level Insert-Muster dieser Datei (identisch zu
// den 3 bereits vorhandenen Traits, Z. 358-360/395-397/425-427):
[Fact]
[Trait("Category", "Unit")]
public void ResolveSolutionPathOrError_TwoSlnxFiles_ReportsAmbiguousSolution()

[Fact]
[Trait("Category", "Integration")]
public async Task RunAsync_ValidFixture_ServerRespondsWithThirteenTools()
```

## Notes

- **Erwartetes Filter-Delta:** Unit 1184 → **1193** (+9); Integration
  121 → **132** (+11); Total **1325** (±0). **Unit + Integration =
  Total nach diesem Step** — das ist das strukturelle Signal, dass
  EPIC-02 damit vollständig abgeschlossen ist (kein ungetaggter Test
  mehr im gesamten Projekt).
- **Diff-Budget:** 20 Trait-Zeilen (1 Zeile pro Item) ≪
  `max_batch_diff_lines: 80`; 20 Items = exakt am eigenen, in diesem
  Task etablierten `max_batch_items: 20`-Deckel (siehe step-011..014),
  aber alle 20 Items sind hier **methodische Einzelentscheidungen
  innerhalb einer Datei**, nicht 20 unabhängige Dateien — das ist die
  vom Auftrag geforderte „gleiche Sorgfaltsstufe wie 20 eigenständige
  Items" (Nutzer-Vorgabe aus `nachfragen.md`: größere Blöcke statt
  Mini-Steps, hier sachlich bedingt durch „nur noch 1 Datei übrig").
- **CodeMap (Coder-Pflicht Schritt 6a):** `Commands/`-Eintrag auf
  „vollständig abgehakt 17/17 (`McpServerCommandTests.cs` method-level
  9 Unit + 11 Integration + 3 bereits vorab getaggt = 23/23 Facts)"
  aktualisieren; zusätzlich ein neuer Abschluss-Vermerk in `codemap.md`
  oder `roadmap.md`, dass EPIC-02 als Ganzes fertig ist.
- **Letzter EPIC-02-Schritt:** Nach `approved` dieses Steps ist EPIC-02
  vollständig abgeschlossen — der nächste Planer-Aufruf geht direkt zu
  EPIC-03 (Fixture-Sharing im großen Stil) über, ohne weiteren
  EPIC-02-Batch.
- **Keine Produktionscode-Änderungen**; Orchestrator committed.
