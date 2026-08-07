---
status: done (pending audit)
type: step-plan
task: flaky-and-test-performance
step: 014
corrects: null
title: "Category-Traits für Rest-EPIC-02 (Mcp/-Root + Baseline/ + Commands/-Teil + Cli/, 20 Klassen, Mega-Batch)"
epic: EPIC-02
estimated_risk: low
step_type: batch
items:
  - id: item-01
    title: "McpCodeGraphServerTests → Unit (BaselineMiniFixtureWorkspace + SourceFileCatalog.LoadAsync, in-process; 6 [Fact], classLine=9; Standard-Insert; kein BOM, CRLF+TrNL, #nullable enable FEHLT)"
    source: "konzept.md §Wie Schritt 2; Heuristik-Punkt-2-Negativ-Abgrenzung (Mini-Fixture statt Subprozess)"
  - id: item-02
    title: "McpServerOptionsFactoryTests → Unit (kein Fixture, reiner In-Process-Aufruf McpServerOptionsFactory.Create; 1 [Fact], classLine=15; XML-Doc-Variante; kein BOM, CRLF+TrNL, #nullable enable Z.1)"
    source: "konzept.md §Wie Schritt 2"
  - id: item-03
    title: "McpToolResultsTests → Unit (statische McpToolResults-Aufrufe, kein Fixture; 4 [Fact], classLine=9; Standard-Insert; kein BOM, CRLF+TrNL, #nullable enable Z.1)"
    source: "konzept.md §Wie Schritt 2"
  - id: item-04
    title: "OverviewResourceRegistrationTests → Unit (McpCodeGraphServer in-process, kein Subprozess; 5 [Fact], classLine=21; XML-Doc-Variante; **LF-only (CR=0, LF=96)** — Python-Helper Pflicht analog step-013; kein BOM, #nullable enable Z.1)"
    source: "konzept.md §Wie Schritt 2; TD-003-Analogon"
  - id: item-05
    title: "SymbolGraphToolRegistrationsTests → Unit (McpCodeGraphServer in-process, kein Subprozess; 1 [Fact], classLine=18; XML-Doc-Variante; kein BOM, CRLF+TrNL, #nullable enable Z.1)"
    source: "konzept.md §Wie Schritt 2"
  - id: item-06
    title: "BaselineComparerTests → Unit (reine In-Memory-Vergleichslogik, kein I/O); 4 [Fact], classLine=6; Standard-Insert; kein BOM, CRLF+TrNL, #nullable enable FEHLT)"
    source: "konzept.md §Wie Schritt 2"
  - id: item-07
    title: "BaselineReaderWriterTests → Unit (Temp-Datei-Roundtrip, kein Subprozess); 2 [Fact], classLine=6; Standard-Insert; kein BOM, CRLF+TrNL, #nullable enable FEHLT)"
    source: "konzept.md §Wie Schritt 2"
  - id: item-08
    title: "BaselineViolationFilterTests → Unit (reine In-Memory-Filterlogik); 2 [Fact], classLine=7; Standard-Insert; kein BOM, CRLF+TrNL, #nullable enable FEHLT)"
    source: "konzept.md §Wie Schritt 2"
  - id: item-09
    title: "FileChecksumCalculatorTests → Unit (Temp-Datei-Hashing, kein Subprozess); 1 [Fact], classLine=6; Standard-Insert; kein BOM, CRLF+TrNL, #nullable enable FEHLT)"
    source: "konzept.md §Wie Schritt 2"
  - id: item-10
    title: "FileSystemExclusionHelpersTests → Unit (reine Pfad-Prädikate, kein I/O); 6 [Fact], classLine=10; Standard-Insert; kein BOM, CRLF+TrNL, #nullable enable FEHLT)"
    source: "konzept.md §Wie Schritt 2"
  - id: item-11
    title: "SourceFileCatalogBlazorPartialTests → Unit (BlazorPartialMiniFixtureWorkspace + SourceFileCatalog.LoadAsync + direkte ExecuteAsync-Tool-Aufrufe, in-process, kein Transport); 3 [Fact], classLine=22; XML-Doc-Variante; kein BOM, CRLF+TrNL, #nullable enable FEHLT)"
    source: "konzept.md §Wie Schritt 2; Heuristik-Punkt-2-Negativ-Abgrenzung"
  - id: item-12
    title: "SourceFileCatalogTests → Unit (BaselineMini-Fixture-Pfad, SourceFileCatalog.LoadAsync in-process); 2 [Fact], classLine=9; Standard-Insert; kein BOM, CRLF+TrNL, #nullable enable FEHLT)"
    source: "konzept.md §Wie Schritt 2"
  - id: item-13
    title: "WebBaselineTests → **Integration** (CliProcessRunner.RunLinterAsync, echter AiNetLinter.exe-Subprozess); 2 [Fact], classLine=12; Standard-Insert; kein BOM, CRLF+TrNL, #nullable enable Z.1)"
    source: "konzept.md §Wie Schritt 2; Subprozess-Nachweis CliProcessRunner"
  - id: item-14
    title: "ListRulesCommandTests → Unit (TestLintConsole-Mock, In-Process-Aufruf ListRulesCommand.ListAll/ByCategory, kein Subprozess); 9 [Fact], classLine=12; XML-Doc-Variante; kein BOM, CRLF+TrNL, #nullable enable Z.1)"
    source: "konzept.md §Wie Schritt 2; Widerlegungsmuster analog step-006 ListEvalsCommandTests"
  - id: item-15
    title: "McpServerCommandErrorHandlingTests → **Integration** (startet AiNetLinter.exe direkt via StdioClientTransport + SubprocessConcurrencyGate); 2 [Fact], classLine=22; XML-Doc-Variante; kein BOM, CRLF+TrNL, #nullable enable Z.1)"
    source: "konzept.md §Wie Schritt 2; Subprozess-Nachweis StdioClientTransport"
  - id: item-16
    title: "McpServerCommandFindReferencesTests → **Integration** ([Collection(\"SymbolGraphMcp\")] + SymbolGraphMcpFixture, geteilter MCP-Subprozess); 1 [Fact], classLine=10; Trait zwischen [Collection(...)] Z.9 und class Z.10; kein BOM, CRLF+TrNL, #nullable enable FEHLT)"
    source: "konzept.md §Wie Schritt 2; step-001-Collection-Umstellung als Vorarbeit"
  - id: item-17
    title: "McpServerCommandFindSymbolTests → **Integration** (dito [Collection(\"SymbolGraphMcp\")] + SymbolGraphMcpFixture); 1 [Fact], classLine=10; Trait zwischen [Collection(...)] Z.9 und class Z.10; kein BOM, CRLF+TrNL, #nullable enable FEHLT)"
    source: "konzept.md §Wie Schritt 2; step-001-Collection-Umstellung als Vorarbeit"
  - id: item-18
    title: "McpServerCommandGetImpactTests → **Integration** ([Collection(\"SymbolGraphMcp\")] + SymbolGraphMcpFixture bzw. lokal McpTestClient.ConnectAsync + GitImpactMiniFixtureWorkspace für den Git-Branch-Test); 2 [Fact], classLine=14; XML-Doc-Variante + Trait zwischen [Collection(...)] Z.13 und class Z.14; kein BOM, CRLF+TrNL, #nullable enable FEHLT)"
    source: "konzept.md §Wie Schritt 2; step-001-Collection-Umstellung als Vorarbeit"
  - id: item-19
    title: "IgnoreSuppressionsCliTests → Unit (CliCommandBuilder.Build/Parse rein in-process, **kein** Subprozess trotz 'Cli' im Namen); 5 [Fact], classLine=8; Trait zwischen '// @covers LinterArgs' Z.7 und class Z.8; kein BOM, CRLF+TrNL, #nullable enable FEHLT)"
    source: "konzept.md §Wie Schritt 2; Namens-Fehlschluss-Warnung wie step-006 (Name suggeriert Subprozess, Code widerlegt es)"
  - id: item-20
    title: "IgnoreSuppressionsIntegrationTests → Unit (IgnoreSuppressionsFilter/SuppressionEvaluator/WebSuppressionDetector rein in-process, **kein** Subprozess trotz 'Integration' im Namen); 3 [Fact], classLine=15; Trait zwischen letztem '// @covers' Z.14 und class Z.15; kein BOM, CRLF+TrNL, #nullable enable FEHLT)"
    source: "konzept.md §Wie Schritt 2; Namens-Fehlschluss-Warnung — 'Integration' im Klassennamen bezieht sich auf Komponenten-Zusammenspiel, nicht auf Subprozess/Category-Taxonomie"
created_by: planer
created_by_model: claude-sonnet-5
created_by_model_knowledge_cutoff: 2026-01
created_at: 2026-08-08T09:15:00+02:00
related_to:
  - step-013
---

# Step 014: Category-Traits für Rest-EPIC-02 (Mcp/-Root + Baseline/ + Commands/-Teil + Cli/)

## Bezug

- **Task:** `flaky-and-test-performance`
- **Epic:** `EPIC-02` aus `roadmap.md` — Category-Traits flächendeckend
  nachziehen. **Dreizehnter Batch** dieses Epics; deckt den kompletten
  Rest-Bestand der vier in `step-013` als „nur informativ" benannten
  Restbereiche ab — **bis auf eine einzige, bewusst ausgeklammerte
  Klasse** (`McpServerCommandTests`, siehe unten).
- **Konzept-Referenz:** `konzept.md` §"Wie" Schritt 2, §"Muss-Haben"
  Traits-Punkt, §"Definition of Done" Punkt "Alle Tests tragen einen
  Category-Trait".
- **Vorgänger:** `step-013` (approved, Commit `0d5cee2`/`5c4600c`) —
  Filterstand danach: Unit **1130** / Integration **113** / Total
  **1325**.

### Schnitt-Entscheidung (20 Items ≤ 20-Item-Deckel)

`step-013`s „Restbestand EPIC-02"-Notiz war eine **Prognose**, keine
verifizierte Ist-Aufnahme — dieser Schritt hat den kompletten Code in
allen vier Bereichen (`Mcp/`-Root, `Baseline/`, `Commands/`, `Cli/`)
selbst gelesen (§"Aktueller Projektzustand" unten). Ergebnis:

- **`Mcp/`-Root** (19 `.cs`-Dateien): 11 bereits getaggt (bestätigt,
  keine Änderung nötig), 3 Helper ohne `[Fact]`/`[Theory]`
  (`CompileErrorHeaderAssertions`, `McpTestClient`,
  `McpTestClientRetryOptions` — Heuristik-Punkt 6, ausgenommen), **5
  echte Testklassen ungetaggt** → item-01..05, alle Unit.
- **`Baseline/`** (10 Dateien): 2 bereits **vollständig** getaggt
  (`BaselineCliTests` class-level Integration,
  `SourceFileCatalogRegisterMSBuildTests` **method-level** Unit auf
  allen 3 Methoden — die `roadmap.md`-Prognose "20 parallele Aufrufe
  ohne Gate-Schutz als Integration" ist damit **überholt**: die Datei
  ist bereits vollständig und korrekt getaggt, kein Handlungsbedarf),
  **8 ungetaggte Klassen** → item-06..13 (7 Unit + 1 Integration
  `WebBaselineTests`).
- **`Commands/`** (17 Dateien): 11 bereits **vollständig** getaggt
  (class-level oder method-level durchgängig, verifiziert per
  Fact-vs-Trait-Zählung + Stichprobe), **5 einfache ungetaggte
  Klassen** → item-14..18 (1 Unit + 4 Integration), **plus 1
  Sonderfall** `McpServerCommandTests` (23 `[Fact]`, davon nur 3
  bereits getaggt Unit — die restlichen 20 sind **innerhalb derselben
  Klasse gemischt**: 9 weitere Unit + 11 Integration, methodenweise
  unterschiedlich, je nachdem ob eine Methode `_symbolGraphMcpFixture`/
  `_baselineMcpFixture`/`McpTestClient.ConnectAsync` — Subprozess —
  nutzt oder rein statische `McpServerCommand.Resolve*`-Helper
  in-process aufruft). **Bewusst nicht in diesem Batch:** 20
  method-level Trait-Entscheidungen in einer einzigen Klasse sind
  qualitativ etwas anderes als die homogenen Klassen-Ebene-Inserts
  dieses und aller vorherigen EPIC-02-Batches — jede der 20
  Einzelentscheidungen braucht dieselbe Sorgfalt wie ein Item hier,
  würde den 20-Item-Deckel für sich allein auffüllen und den Rest
  dieses ohnehin schon großen, aber sauber homogenen Batches
  verdrängen. Bereits in `codemap.md` (step-002) und `step-013`
  vermerkt: „erfordert pro-Methode-Tagging in eigenem Step" — das ist
  jetzt der **letzte verbleibende EPIC-02-Schritt nach diesem hier**.
- **`Cli/`** (6 Dateien): 4 bereits vollständig getaggt, **2 ungetaggte
  Klassen** → item-19..20, **beide entgegen ihres Klassennamens Unit**
  (kein Subprozess-Marker trotz "Cli"/"Integration" im Namen — Fehlschluss
  bewusst dokumentiert, analog zur `ListEvalsCommandTests`-Widerlegung
  in step-006).
- **20 Items total** (5+8+5+2) = exakt am `max_batch_items: 20`-Deckel;
  Diff ≈ 20 Trait-Zeilen ≪ `max_batch_diff_lines: 80`.
- **Warum trotzdem 1 Batch statt 4 (pro Ordner):** alle 20 Items sind
  mechanisch identisch (1 Trait-Zeile pro Klasse, Klassen-Ebene,
  Standard- oder XML-Doc-Variante), keine strukturellen Abhängigkeiten
  zwischen den vier Ordnern; Nutzer-Vorgabe (`nachfragen.md`) fordert
  größere Blöcke statt Mini-Steps.

### Anti-Loop-Check gegen `codemap.md`

Die vier Restbereiche waren in `codemap.md` als „gemischt, geplant für
Batch X" markiert — kein Widerspruch, dieser Step setzt genau das um.
Einzige Korrektur: `SourceFileCatalogRegisterMSBuildTests` wird in der
CodeMap künftig als „bereits vollständig getaggt" statt „Integration
wegen 20 paralleler Aufrufe ohne Gate" geführt (die Coder eines
früheren Steps — vermutlich beim A3-Fix-Commit — haben das Tagging
bereits methodenweise als Unit vorgenommen; kein Widerspruch zur alten
Notiz, nur deren Überholtheit).

## Aktueller Projektzustand (JIT-Kontext)

Vollständiger Code-Read aller `.cs`-Dateien in `Mcp/` (Root),
`Baseline/`, `Commands/`, `Cli/` plus Fact-/Trait-/Subprozess-Marker-Scan
(`grep -cE 'Process\.Start|McpTestClient|CliProcessRunner|Program\.Main|IClassFixture|\[Collection\('`)
über jede Kandidatendatei:

- **Bereits vollständig getaggt (keine Änderung, nur zur Vollständigkeit
  verifiziert):** `Mcp/` 11 Klassen (`McpCallLogTests`,
  `McpCodeGraphServerConstructorTests`,
  `McpCodeGraphServerFileDiscoveryTests`,
  `McpCodeGraphServerStalenessMtimeCacheTests`,
  `McpDocumentationSmokeTests`, `McpLiveRepositoryTests`,
  `McpServerAllToolsE2ETests`, `McpServerCommandJsonRpcFramingTests`,
  `McpServerOptionsBuilderTests`, `McpTestClientParallelTests`,
  `McpTestClientRetryTests`); `Baseline/` 2 (`BaselineCliTests`,
  `SourceFileCatalogRegisterMSBuildTests`); `Commands/` 11
  (`AuditCommandTests`, `CliBatchRegressionTests`, `DocsCommandTests`,
  `McpServerCommandAmbiguityE2ETests`,
  `McpServerCommandCacheBypassTests`, `McpServerCommandCallLogTests`,
  `McpServerCommandLoadingStateTests`, `McpServerCommandMissHintTests`,
  `McpServerCommandStalenessTests`, `PlaybookCheckCommandTests`,
  `SyncAgentRulesCommandTests`); `Cli/` 4
  (`CliCommandBuilderMcpLogTests`, `CliIntegrationTests`,
  `FilterCliIntegrationTests`, `ProgramTests`).
- **Helper ohne Facts, aus dem Scope ausgenommen (Heuristik-Punkt 6):**
  `Mcp/CompileErrorHeaderAssertions.cs` (`internal static class`, 0
  Facts), `Mcp/McpTestClient.cs` (`public sealed class … :
  IAsyncDisposable`, Test-Client-Implementierung, 0 Facts),
  `Mcp/McpTestClientRetryOptions.cs` (Options-Typ, 0 Facts).
- **20 zu taggende Klassen** (Details siehe Items oben):
  - **Unit (15):** item-01..05, item-06..12, item-14, item-19, item-20
    — durchweg in-process (Mini-Fixture-Workspaces, statische
    Helper-Aufrufe, direkte `ExecuteAsync`-Tool-Aufrufe ohne
    MCP-Transport), 0 Subprozess-Marker.
  - **Integration (5):** item-13 (`WebBaselineTests`,
    `CliProcessRunner`), item-15..18 (`Commands/`-Rest:
    `StdioClientTransport`/`[Collection("SymbolGraphMcp")]` +
    `SymbolGraphMcpFixture`/`McpTestClient.ConnectAsync`).
  - **Zwei Namens-Fehlschlüsse bewusst dokumentiert:** `Cli/`-Dateien
    mit "Cli"/"Integration" im Klassennamen (`IgnoreSuppressionsCliTests`,
    `IgnoreSuppressionsIntegrationTests`) sind **beide Unit** — der
    Code selbst enthält keinerlei Subprozess-Start, nur In-Process-
    Aufrufe von `CliCommandBuilder`/`IgnoreSuppressionsFilter`/
    `SuppressionEvaluator`/`WebSuppressionDetector`. Gleiches Muster
    wie die `ListEvalsCommandTests`-Widerlegung in step-006: Klassenname
    ist keine verlässliche Kategorie-Quelle, nur der tatsächliche Code.
- **EOL:** 19/20 Dateien uniform CRLF (kein BOM); **1 LF-only:**
  `Mcp/OverviewResourceRegistrationTests.cs` (CR=0, LF=96) —
  Python-Helper Pflicht (byte-genau, analog step-007/012/013).
- **BOM:** 0/20 mit UTF-8-BOM.
- **`#nullable enable`:** 8/20 mit Direktive (`McpServerOptionsFactoryTests`,
  `McpToolResultsTests`, `OverviewResourceRegistrationTests`,
  `SymbolGraphToolRegistrationsTests`, `WebBaselineTests`,
  `ListRulesCommandTests`, `McpServerCommandErrorHandlingTests` — 7 mit
  Direktive im Mcp/Commands-Cluster + keine im Baseline/Cli-Cluster
  außer `WebBaselineTests`); 12/20 ohne — Trait-Insertion darf
  Direktive **nicht** nachziehen (TD-004 out of scope).
- **XML-Doc-Variante (Trait zwischen `</summary>` und `class`):**
  item-02, item-04, item-05, item-11, item-14, item-15, item-18 (7
  Dateien).
- **`[Collection(...)]`-Variante (Trait zwischen `[Collection(...)]` und
  `class`):** item-16, item-17, item-18 (item-18 kombiniert XML-Doc
  **und** Collection — Reihenfolge: XML-Doc, dann `[Collection(...)]`,
  dann neu `[Trait(...)]`, dann `class`).
- **`// @covers`-Variante (Trait zwischen letzter `@covers`-Zeile und
  `class`):** item-19, item-20 — etabliertes Muster aus
  `PerformanceProfilerTests`/`AgentFeaturesTests`/`FilterCliIntegrationTests`
  (Trait folgt direkt auf den letzten `// @covers`-Kommentar).
- **Fact-Inventar (0 `[Theory]`/`[InlineData]` in allen 20 Dateien,
  also Filter-Delta = Fact-Summe, keine Runtime-Expansion):**
  - Unit: 6+1+4+5+1 (Mcp-Root item-01..05 = 17) + 4+2+2+1+6+3+2
    (Baseline item-06..12 = 20) + 9 (item-14) + 5+3 (item-19+20 = 8)
    = **54 Unit**
  - Integration: 2 (item-13 `WebBaselineTests`) + 2+1+1+2 (item-15..18
    = 6) = **8 Integration**
  - **Filter-Delta: Unit +54, Integration +8, Total ±0** (bereits
    existierende Tests, nur Tagging).

## Intention

Alle 20 verbliebenen, homogen klassifizierbaren Testklassen in
`Mcp/`-Root, `Baseline/`, `Commands/` (ohne `McpServerCommandTests`)
und `Cli/` erhalten Klassen-Ebene `[Trait("Category", ...)]`. Danach
ist EPIC-02 bis auf genau eine Klasse (`McpServerCommandTests`, eigener
Folge-Step wegen Methoden-gemischter Kategorie) vollständig
abgeschlossen.

## Konkrete Änderungen

### item-01: McpCodeGraphServerTests → Unit — `src/AiNetLinter.Tests/Mcp/McpCodeGraphServerTests.cs` (Zeile 9)

- **Was:** `[Trait("Category", "Unit")]` unmittelbar vor
  `public sealed class McpCodeGraphServerTests` (Z.9 → 10).
- **Warum:** nutzt `BaselineMiniFixtureWorkspace` + `SourceFileCatalog.LoadAsync`
  in-process, kein Subprozess.

### item-02: McpServerOptionsFactoryTests → Unit — `…/McpServerOptionsFactoryTests.cs` (Zeile 15)

- **Was:** Trait zwischen `</summary>` (Z.14) und class (Z.15); class → Z.16.
- **Warum:** reiner In-Process-Aufruf `McpServerOptionsFactory.Create`.

### item-03: McpToolResultsTests → Unit — `…/McpToolResultsTests.cs` (Zeile 9)

- **Was:** Standard-Insert vor class Z.9.
- **Warum:** statische `McpToolResults`-Aufrufe, kein Fixture.

### item-04: OverviewResourceRegistrationTests → Unit — `…/OverviewResourceRegistrationTests.cs` (Zeile 21)

- **Was:** Trait zwischen `</summary>` (Z.20) und class (Z.21); class → Z.22.
  **LF-only (CR=0, LF=96):** byte-genauer Python-Helper
  (`[Trait("Category", "Unit")]\n`, EOL bleibt LF-only, kein CRLF-Umbau).
- **Warum:** `McpCodeGraphServer` in-process, kein Subprozess;
  TD-003-Analogon (LF-only-Ausreißer in sonst CRLF-Ordner).

### item-05: SymbolGraphToolRegistrationsTests → Unit — `…/SymbolGraphToolRegistrationsTests.cs` (Zeile 18)

- **Was:** Trait zwischen `</summary>` (Z.17) und class (Z.18); class → Z.19.
- **Warum:** `McpCodeGraphServer` in-process, kein Subprozess.

### item-06: BaselineComparerTests → Unit — `src/AiNetLinter.Tests/Baseline/BaselineComparerTests.cs` (Zeile 6)

- **Was:** Standard-Insert vor class Z.6.
- **Warum:** reine In-Memory-Vergleichslogik.

### item-07: BaselineReaderWriterTests → Unit — `…/BaselineReaderWriterTests.cs` (Zeile 6)

- **Was:** Standard-Insert vor class Z.6.
- **Warum:** Temp-Datei-Roundtrip, kein Subprozess.

### item-08: BaselineViolationFilterTests → Unit — `…/BaselineViolationFilterTests.cs` (Zeile 7)

- **Was:** Standard-Insert vor class Z.7.
- **Warum:** reine In-Memory-Filterlogik.

### item-09: FileChecksumCalculatorTests → Unit — `…/FileChecksumCalculatorTests.cs` (Zeile 6)

- **Was:** Standard-Insert vor class Z.6.
- **Warum:** Temp-Datei-Hashing, kein Subprozess.

### item-10: FileSystemExclusionHelpersTests → Unit — `…/FileSystemExclusionHelpersTests.cs` (Zeile 10)

- **Was:** Standard-Insert vor class Z.10.
- **Warum:** reine Pfad-Prädikate, kein I/O.

### item-11: SourceFileCatalogBlazorPartialTests → Unit — `…/SourceFileCatalogBlazorPartialTests.cs` (Zeile 22)

- **Was:** Trait zwischen `</summary>` (Z.21) und class (Z.22); class → Z.23.
- **Warum:** `BlazorPartialMiniFixtureWorkspace` + `SourceFileCatalog.LoadAsync`
  + direkte `ExecuteAsync`-Tool-Aufrufe, alles in-process ohne Transport.

### item-12: SourceFileCatalogTests → Unit — `…/SourceFileCatalogTests.cs` (Zeile 9)

- **Was:** Standard-Insert vor class Z.9.
- **Warum:** `SourceFileCatalog.LoadAsync` auf BaselineMini-Fixture-Pfad,
  in-process.

### item-13: WebBaselineTests → Integration — `…/WebBaselineTests.cs` (Zeile 12)

- **Was:** Standard-Insert vor class Z.12.
- **Warum:** `CliProcessRunner.RunLinterAsync` startet echten
  `AiNetLinter.exe`-Subprozess.

### item-14: ListRulesCommandTests → Unit — `src/AiNetLinter.Tests/Commands/ListRulesCommandTests.cs` (Zeile 12)

- **Was:** Trait zwischen `</summary>` (Z.11) und class (Z.12); class → Z.13.
- **Warum:** `TestLintConsole`-Mock, In-Process-Aufruf von
  `ListRulesCommand.ListAll`/`ByCategory`, kein Subprozess — analog zur
  step-006-Widerlegung bei `ListEvalsCommandTests`.

### item-15: McpServerCommandErrorHandlingTests → Integration — `…/McpServerCommandErrorHandlingTests.cs` (Zeile 22)

- **Was:** Trait zwischen `</summary>` (Z.21) und class (Z.22); class → Z.23.
- **Warum:** startet `AiNetLinter.exe` direkt via `StdioClientTransport`
  + `SubprocessConcurrencyGate.AcquireAsync`.

### item-16: McpServerCommandFindReferencesTests → Integration — `…/McpServerCommandFindReferencesTests.cs` (Zeile 10)

- **Was:** Trait zwischen `[Collection("SymbolGraphMcp")]` (Z.9) und
  class (Z.10); class → Z.11.
- **Warum:** `SymbolGraphMcpFixture` über Collection = geteilter
  MCP-Subprozess.

### item-17: McpServerCommandFindSymbolTests → Integration — `…/McpServerCommandFindSymbolTests.cs` (Zeile 10)

- **Was:** Trait zwischen `[Collection("SymbolGraphMcp")]` (Z.9) und
  class (Z.10); class → Z.11.
- **Warum:** dito `SymbolGraphMcpFixture`.

### item-18: McpServerCommandGetImpactTests → Integration — `…/McpServerCommandGetImpactTests.cs` (Zeile 14)

- **Was:** Trait zwischen `[Collection("SymbolGraphMcp")]` (Z.13) und
  class (Z.14); class → Z.15. XML-Doc (Z.10-12) bleibt unverändert
  darüber.
- **Warum:** ein Testfall über `SymbolGraphMcpFixture`, der zweite via
  `McpTestClient.ConnectAsync` — beide Subprozess-basiert.

### item-19: IgnoreSuppressionsCliTests → Unit — `src/AiNetLinter.Tests/Cli/IgnoreSuppressionsCliTests.cs` (Zeile 8)

- **Was:** Trait zwischen `// @covers LinterArgs` (Z.7) und class (Z.8);
  class → Z.9.
- **Warum:** `CliCommandBuilder.Build()`/`.Parse()` rein in-process,
  **kein** Subprozess trotz „Cli" im Klassennamen.

### item-20: IgnoreSuppressionsIntegrationTests → Unit — `…/IgnoreSuppressionsIntegrationTests.cs` (Zeile 15)

- **Was:** Trait zwischen letztem `// @covers WebSuppressionDetector`
  (Z.14) und class (Z.15); class → Z.16.
- **Warum:** `IgnoreSuppressionsFilter`/`SuppressionEvaluator`/
  `WebSuppressionDetector` rein in-process, **kein** Subprozess trotz
  „Integration" im Klassennamen (bezieht sich auf
  Komponenten-Zusammenspiel, nicht auf die Category-Taxonomie).

## Tests

- [ ] Keine neuen Tests — nur Category-Traits.
- [ ] Verifikation über Filter-/Voll-Läufe in der Definition of Done.

## Definition of Done

- [ ] Alle 20 Trait-Inserts umgesetzt
- [ ] `dotnet build` grün (0 Warnungen, 0 Fehler)
- [ ] Self-Lint: `dotnet run --project src/AiNetLinter -- --config rules.json --path .` → `OK` (TD-001-konform, **kein** `--self-lint`)
- [ ] `dotnet test --filter "Category=Unit"` → grün, **1184** Tests (1130 + 54)
- [ ] `dotnet test --filter "Category=Integration"` → best-effort
  (bekannter EPIC-06-Flaky `McpServerCommandLoadingStateTests…` darf
  failen; nicht step-014-Scope), **121** Tests (113 + 8)
- [ ] `dotnet test` (Voll) → grün, **1325** Tests (±0 Total)
- [ ] **EOL-Vollscan** aller 20 Dateien: 19 CRLF erhalten (CR==LF
  vor/nach, +1 Zeile), 1 LF-only erhalten (CR=0, LF+1); Trailing-NL
  erhalten; 0 BOM vorher/nachher
- [ ] **Numerische Plausibilität:** 62 attr-`[Fact]` (54 Unit + 8
  Integration), 0 `[Theory]` → Filter-Delta exakt +54/+8
- [ ] **Scope-Disziplin:** `McpServerCommandTests.cs` **nicht**
  angefasst — bleibt für den letzten EPIC-02-Folge-Step
- [ ] Code-Commit zuerst, Hash in `step-result.md`; Subject exakt:
  `test: EPIC-02 Rest-Batch Traits nachziehen [flaky-and-test-performance]`
  (**71 Zeichen**, 1 Reserve zur 72-Grenze) — Coder übernimmt
  unverändert (TD-002-Disziplin)
- [ ] Doku-Commit danach inkl. `step-result.md`, Status
  `done (pending audit)`, **CodeMap-Update** (`Mcp/`-Root, `Baseline/`,
  `Commands/` [bis auf `McpServerCommandTests`], `Cli/` je auf
  „vollständig abgehakt bis auf …"); Subject-Beispiel:
  `docs(tasks): step-014 Result dokumentieren [flaky-and-test-performance]`
  (**71 Zeichen**)
- [ ] `step-result.md` mit EOL-Tabelle (20 Zeilen), Filter-Delta,
  Commit-Hashes; keine neuen Hilfsdateien-Leichen im Doku-Commit
- [ ] Pipeline-Konvention step-011/012/013: Code-Commit → Result mit
  Hash → Doku-Commit (kein 3-Commits-Mechanismus)

## Rules-Refs

- `.agents/rules/AiNetLinterRichtlinien.mdc` §4 — Conventional Commits
  DE, Subject ≤ 72 Zeichen; §5 Parallelität unberührt (nur Traits,
  keine `[Collection(...)]`-Änderungen)
- `.agents/rules/AiNetLinter.mdc` — `*.Tests`-Overrides; Trait-
  Schreibweise CamelCase `"Unit"`/`"Integration"`; `#nullable`/BOM
  nicht anfassen

## Bekannte Ausnahmen

- Integration-Filter darf den bekannten EPIC-06-Flaky
  `McpServerCommandLoadingStateTests.LoadState_LoadFuncCompletesSynchronouslyWithCatalog_ReportsLoadedImmediately`
  failen lassen (out of scope; Voll-Lauf muss grün sein; Datei selbst
  bereits vollständig getaggt, wird von diesem Step nicht berührt).
- TD-001/002/003/004/005/006 **nicht** in diesem Step fixen (TD-001
  Nutzer: OUT OF SCOPE; übrige ohne klare Auto-Fix-Richtung /
  `auto_fixable: nein`; kein offener `auto_fixable: ja`-Eintrag
  gefunden, der diesen Bereich träfe).

## Code-Skizze (optional)

```csharp
// Standard-Insert (CRLF-Dateien, kein XML-Doc/Collection davor):
[Trait("Category", "Unit")]
public sealed class BaselineComparerTests

// XML-Doc-Variante:
/// </summary>
[Trait("Category", "Unit")]
public sealed class McpServerOptionsFactoryTests

// [Collection(...)]-Variante:
[Collection("SymbolGraphMcp")]
[Trait("Category", "Integration")]
public sealed class McpServerCommandFindReferencesTests

// XML-Doc + [Collection(...)] kombiniert:
/// </summary>
[Collection("SymbolGraphMcp")]
[Trait("Category", "Integration")]
public sealed class McpServerCommandGetImpactTests

// // @covers-Variante:
// @covers WebSuppressionDetector
[Trait("Category", "Unit")]
public sealed class IgnoreSuppressionsIntegrationTests

// LF-only (Python, item-04): insert b'[Trait("Category", "Unit")]\n' vor class-Zeile
```

## Notes

- **Erwartetes Filter-Delta:** Unit 1130 → **1184** (+54); Integration
  113 → **121** (+8); Total **1325** (±0).
- **Diff-Budget:** 20 Trait-Zeilen ≪ 80 Diff-Zeilen / genau 20 Items.
- **CodeMap (Coder-Pflicht Schritt 6a):** `Mcp/`-Root, `Baseline/`,
  `Commands/`, `Cli/` je auf „vollständig getaggt" aktualisieren;
  `Commands/` explizit mit Hinweis „bis auf `McpServerCommandTests`
  (eigener Folge-Step)".
- **Letzter EPIC-02-Schritt danach (nur informativ, nicht geplant):**
  `McpServerCommandTests.cs` — 20 method-level Trait-Entscheidungen (9
  Unit: `ResolveSolutionPathOrError_*` ×4, `TryLoadSolutionAsync_BrokenSlnx_*`,
  `ResolveMaxLineCount_*` ×2, `ResolveConfig_ConfigWithCustomMaxLineCount_*`,
  `ResolveConfig_NoConfigPath_*`; 11 Integration:
  `RunAsync_ValidFixture_*` ×9, `RunAsync_ValidFixture_GetImpactWith*` ×2)
  neben den bereits vorhandenen 3 method-level Unit-Traits. Nach diesem
  Folge-Step ist EPIC-02 **vollständig** abgeschlossen (alle
  Testmethoden im Projekt mit Category-Trait).
- **Keine Produktionscode-Änderungen**; Orchestrator committed.
