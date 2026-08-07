---
status: done (pending audit)
type: step-plan
task: flaky-and-test-performance
step: 013
corrects: null
title: "Category-Traits für Mcp/Tools/ (17 Klassen) nachziehen + TD-007 Hilfsdateien löschen (Mega-Batch)"
epic: EPIC-02
estimated_risk: low
step_type: batch
items:
  - id: item-01
    title: "CallGraphTraversalTests → Unit (IClassFixture<SymbolGraphCatalogFixture>, in-process; 3 [Fact], classLine=10; Standard-Insert; kein BOM, CRLF+TrNL, #nullable enable FEHLT)"
    source: "konzept.md §Wie Schritt 2; Heuristik-Punkt-2-Negativ-Abgrenzung"
  - id: item-02
    title: "DiRegistrationHeuristicsTests → Unit (IClassFixture<SymbolGraphCatalogFixture>; 5 [Fact], classLine=13; Standard-Insert; **SPEZIALFALL: Helper** `internal sealed class DiRegistrationMiniFixtureWorkspace` Z.131 ohne [Fact]/[Theory] — Heuristik-Punkt 6, NICHT getaggt; kein BOM, CRLF+TrNL, #nullable enable FEHLT)"
    source: "konzept.md §Wie Schritt 2; Heuristik-Punkt 6"
  - id: item-03
    title: "FindReferencesToolTests → Unit (IClassFixture<SymbolGraphCatalogFixture>; 16 [Fact], classLine=14; Standard-Insert; kein BOM, CRLF+TrNL, #nullable enable FEHLT — größte Fact-Zahl im Batch neben SafeguardScanner)"
    source: "konzept.md §Wie Schritt 2"
  - id: item-04
    title: "FindSymbolScannerTests → Unit (IClassFixture<SymbolGraphCatalogFixture>; 6 [Fact], classLine=11; Standard-Insert; kein BOM, CRLF+TrNL, #nullable enable Z.1)"
    source: "konzept.md §Wie Schritt 2"
  - id: item-05
    title: "FindSymbolToolTests → Unit (**dual** IClassFixture<BaselineCatalogFixture>, IClassFixture<SymbolGraphCatalogFixture>; 13 [Fact], classLine=12; Standard-Insert; beide Fixtures = Unit per Heuristik-Punkt-2-Negativ-Abgrenzung; kein BOM, CRLF+TrNL, #nullable enable FEHLT)"
    source: "konzept.md §Wie Schritt 2; Heuristik-Punkt-2-Negativ-Abgrenzung"
  - id: item-06
    title: "GetFileSkeletonToolTests → Unit (IClassFixture<SymbolGraphCatalogFixture>; 5 [Fact], classLine=13; Standard-Insert; kein BOM, CRLF+TrNL, #nullable enable FEHLT)"
    source: "konzept.md §Wie Schritt 2"
  - id: item-07
    title: "GetHotspotsToolTests → Unit (IClassFixture<SymbolGraphCatalogFixture>; 8 [Fact], classLine=13; Standard-Insert; kein BOM, CRLF+TrNL, #nullable enable FEHLT)"
    source: "konzept.md §Wie Schritt 2"
  - id: item-08
    title: "GetImpactToolTests → Unit (IClassFixture<SymbolGraphCatalogFixture>; 12 [Fact], classLine=13; Standard-Insert; kein BOM, CRLF+TrNL, #nullable enable FEHLT)"
    source: "konzept.md §Wie Schritt 2"
  - id: item-09
    title: "GetIndexScopeToolTests → Unit (IClassFixture<SymbolGraphCatalogFixture>; 7 [Fact], classLine=14; Standard-Insert; kein BOM, CRLF+TrNL, #nullable enable FEHLT)"
    source: "konzept.md §Wie Schritt 2"
  - id: item-10
    title: "GetServerHealthToolTests → Unit (IClassFixture<SymbolGraphCatalogFixture>; 5 [Fact], classLine=18; **XML-Doc-Variante** (ohne // @covers): Trait zwischen </summary> Z.17 und class Z.18 → class auf Z.19; **LF-only (CR=0, LF=130)** — Python-Helper analog step-007/012; kein BOM, TrNL=Y, #nullable enable Z.1)"
    source: "konzept.md §Wie Schritt 2; TD-003-Analogon; XML-Doc-Variante step-009/011/012"
  - id: item-11
    title: "GetSymbolBodyToolTests → Unit (IClassFixture<SymbolGraphCatalogFixture>; 6 [Fact], classLine=12; Standard-Insert; kein BOM, CRLF+TrNL, #nullable enable FEHLT)"
    source: "konzept.md §Wie Schritt 2"
  - id: item-12
    title: "GetTypeHierarchyToolTests → Unit (IClassFixture<SymbolGraphCatalogFixture>; 12 [Fact], classLine=13; Standard-Insert; kein BOM, CRLF+TrNL, #nullable enable FEHLT)"
    source: "konzept.md §Wie Schritt 2"
  - id: item-13
    title: "GetViolationsToolTests → Unit (IClassFixture<SymbolGraphCatalogFixture>; 9 [Fact], classLine=18; Standard-Insert; kein BOM, CRLF+TrNL, #nullable enable FEHLT)"
    source: "konzept.md §Wie Schritt 2"
  - id: item-14
    title: "ReloadConfigToolTests → Unit (**kein** IClassFixture — frische SymbolGraphMiniFixtureWorkspace pro Test, in-process, kein Subprozess; 7 [Fact], classLine=22; **XML-Doc-Variante**: Trait zwischen </summary> Z.21 und class Z.22 → class auf Z.23; **LF-only (CR=0, LF=162)** — Python-Helper; kein BOM, TrNL=Y, #nullable enable Z.1)"
    source: "konzept.md §Wie Schritt 2; Mini-Fixture-Negativ-Abgrenzung"
  - id: item-15
    title: "SafeguardScannerTests → Unit (IClassFixture<SymbolGraphCatalogFixture>; 17 [Fact], classLine=26; **XML-Doc-Variante**: Trait zwischen </summary> Z.25 und class Z.26 → class auf Z.27; **String-Literal-Klassen** Greeter/A/B/C/D/Giant in Quelltext-Strings — Heuristik-Punkt 6, NICHT getaggt; 0 String-Literal-[Fact]; kein BOM, CRLF+TrNL, #nullable enable Z.1 — größte Datei ~21 KB)"
    source: "konzept.md §Wie Schritt 2; Heuristik-Punkt 6; String-Literal-NITPICK step-009"
  - id: item-16
    title: "SafeguardToolTests → Unit (IClassFixture<SymbolGraphCatalogFixture>; 6 [Fact], classLine=29; **XML-Doc-Variante** (längerer summary-Block Z.21-28): Trait zwischen </summary> Z.28 und class Z.29 → class auf Z.30; kein BOM, CRLF+TrNL, #nullable enable Z.1)"
    source: "konzept.md §Wie Schritt 2; XML-Doc-Variante"
  - id: item-17
    title: "SearchPatternToolTests → Unit (IClassFixture<SymbolGraphCatalogFixture>; 9 [Fact], classLine=13; Standard-Insert; kein BOM, CRLF+TrNL, #nullable enable FEHLT)"
    source: "konzept.md §Wie Schritt 2"
  - id: item-18
    title: "TD-007: Hilfsdateien step-012/_insert_trait_skeleton.py + step-012/_code_commit_msg.txt löschen (auto_fixable)"
    source: "tech-debt.md#TD-007"
created_by: planer
created_by_model: Cursor Grok 4.5
created_by_model_knowledge_cutoff: 2026-01
created_at: 2026-08-07T15:45:00+02:00
related_to:
  - step-012
---

# Step 013: Category-Traits für Mcp/Tools/ nachziehen + TD-007

## Bezug

- **Task:** `flaky-and-test-performance`
- **Epic:** `EPIC-02` aus `roadmap.md` — Category-Traits flächendeckend
  nachziehen. **Zwölfter Batch** dieses Epics; nächster homogener
  Mega-Batch nach step-012 (`Core/`+`Maps/` abgeschlossen). Scope:
  gesamter Ordner `src/AiNetLinter.Tests/Mcp/Tools/` (17 Testklassen,
  alle Unit) plus opportunistisch **TD-007** (`auto_fixable: ja`).
- **Konzept-Referenz:** `konzept.md` §"Wie" Schritt 2, §"Muss-Haven"
  Traits-Punkt, §"Definition of Done" Punkt "Alle Tests tragen einen
  Category-Trait".
- **Vorgänger:** `step-012` (approved, Commit `b2477f5`/`7deeff1`) —
  Filterstand danach: Unit **984** / Integration **113** / Total
  **1325**.
- **Schnitt-Entscheidung (17 Traits + 1 TD = 18 Items ≤ 20):**
  - `Mcp/Tools/` hat exakt 17 Testklassen, alle ungetaggt, alle
    homogen Unit (Planer-Schritt-2 verifiziert: 0× `Process.Start` /
    `McpTestClient` / `CliProcessRunner` / `Program.Main` /
    `[Collection(...)]`; 15× `IClassFixture<SymbolGraphCatalogFixture>`
    bzw. dual mit `BaselineCatalogFixture`; 1×
    `ReloadConfigToolTests` rein Mini-Fixture in-process).
  - **Warum nicht mit `Mcp/`-Root mischen:** Root enthält bereits
    Integration-getaggte Subprozess-/Live-Klassen und 5 ungetaggte
    Restklassen — Homogenität würde brechen (etablierte Linie
    step-002..012: ein Batch = thematisch zusammenhängender Ordner
    ohne Unit/Integration-Mix).
  - **Warum gesamter Ordner in einem Step:** 17 ≤ `max_batch_items: 20`;
    Nutzer-Hinweis in `nachfragen.md` fordert größere Code-Blöcke;
    Diff ≈ +17 Zeilen ≪ `max_batch_diff_lines: 80`.
  - **TD-007 angehängt:** trifft denselben Task-Bereich (Step-Artefakte
    aus dem Vorgänger-Step); Spec §9.1/§10.6 opportunistisch.
- **Anti-Loop-Check** gegen `codemap.md`: `Mcp/Tools/`-Eintrag sagt
  „17 Klassen … fast alle Unit … 2-3 Batches (zuletzt: step-002)" —
  mit 20-Item-Deckel wird daraus **1 Batch**. Kein Widerspruch zu
  umgesetzten Entscheidungen; nur Batch-Schnitt angepasst.

## Aktueller Projektzustand (JIT-Kontext)

Beim Lesen aller 17 `Mcp/Tools/*.cs` vorgefunden:

- **0/17 bereits mit Category-Trait** — gesamter Ordner EPIC-02-offen.
- **Klassifikation Unit (Heuristik bestätigt):**
  - 14 Klassen: `IClassFixture<SymbolGraphCatalogFixture>` allein
  - 1 Klasse: dual `BaselineCatalogFixture` + `SymbolGraphCatalogFixture`
    (`FindSymbolToolTests`) — beide Fixtures in-process Mini-Solution,
    **kein** Subprozess (Negativ-Abgrenzung Heuristik-Punkt 2)
  - 1 Klasse: ohne Fixture-Interface, nutzt
    `SymbolGraphMiniFixtureWorkspace` lokal (`ReloadConfigToolTests`)
  - Mini-Fixtures (`CompileErrorMini…`, `GitImpactMini…`,
    `DiRegistrationMini…`, `SingleCompileErrorMini…`) nur lokal in
    einzelnen Facts — ändern die Kategorie nicht
- **Helper / Nicht-Testklassen (Heuristik-Punkt 6):**
  - `DiRegistrationMiniFixtureWorkspace` in
    `DiRegistrationHeuristicsTests.cs` Z.131 — **nicht** taggen
  - String-Literal-`public class Greeter`/`Giant`/… in
    `SafeguardScannerTests.cs` — **nicht** taggen
- **XML-Doc vor class (4 Dateien):** `GetServerHealthToolTests`,
  `ReloadConfigToolTests`, `SafeguardScannerTests`,
  `SafeguardToolTests` — Trait zwischen `</summary>` und `public
  sealed class` (Variante ohne `// @covers`, Mechanik wie step-009+)
- **EOL:** 15/17 uniform CRLF; **2 LF-only:**
  `GetServerHealthToolTests.cs` (CR=0, LF=130),
  `ReloadConfigToolTests.cs` (CR=0, LF=162) — TD-003-Analoga;
  Python-Helper Pflicht (analog step-007/012)
- **BOM:** 0/17 mit UTF-8-BOM
- **`#nullable enable`:** 5/17 mit Direktive
  (`FindSymbolScanner`, `GetServerHealth`, `ReloadConfig`,
  `SafeguardScanner`, `SafeguardTool`); 12/17 ohne — Trait-Insertion
  darf Direktive **nicht** nachziehen (TD-004 out of scope)
- **Fact-Inventar (attr-level, 0 String-Literal-`[Fact]`):**
  3+5+16+6+13+5+8+12+7+5+6+12+9+7+17+6+9 = **146** `[Fact]`,
  0 `[Theory]`/`[InlineData]` → Filter-Delta **+146 Unit**
- **TD-007-Dateien existieren:**
  `step-012/_insert_trait_skeleton.py`,
  `step-012/_code_commit_msg.txt` (beide vorhanden)
- **Restbestand EPIC-02 nach diesem Step (nur informativ):**
  `Mcp/`-Root ungetaggt mit Tests:
  `McpCodeGraphServerTests`, `McpServerOptionsFactoryTests`,
  `McpToolResultsTests`, `OverviewResourceRegistrationTests`,
  `SymbolGraphToolRegistrationsTests` (+ Helper ohne Tests:
  `McpTestClient*`, `CompileErrorHeaderAssertions`);
  `Baseline/` 8 ungetaggte Testklassen (gemischt);
  `Commands/` u. a. 5 völlig ungetaggte + gemischte Restfälle;
  `Cli/` 2 ungetaggte (`IgnoreSuppressions*`)

## Intention

Alle 17 Testklassen in `Mcp/Tools/` erhalten Klassen-Ebene
`[Trait("Category", "Unit")]`, damit der Unit-Filter +146 Cases
sieht und der Ordner EPIC-02-mäßig abgehakt ist. Zusätzlich werden
die step-012-Hilfsdateien-Leichen (TD-007) gelöscht.

## Konkrete Änderungen

### item-01: CallGraphTraversalTests → Unit — `src/AiNetLinter.Tests/Mcp/Tools/CallGraphTraversalTests.cs` (Zeile 10)

- **Was:** `[Trait("Category", "Unit")]` unmittelbar vor
  `public sealed class CallGraphTraversalTests : IClassFixture<…>`
  (classLine 10 → 11).
- **Warum:** in-process Catalog-Fixture, kein Subprozess.

### item-02: DiRegistrationHeuristicsTests → Unit — `…/DiRegistrationHeuristicsTests.cs` (Zeile 13)

- **Was:** Trait vor Testklasse Z.13. Helper
  `DiRegistrationMiniFixtureWorkspace` Z.131 **unverändert**.
- **Warum:** Heuristik-Punkt 6 — Helper ohne Facts sind keine
  Testklassen.

### item-03: FindReferencesToolTests → Unit — `…/FindReferencesToolTests.cs` (Zeile 14)

- **Was:** Standard-Insert vor class Z.14 (16 Facts).
- **Warum:** homogen Unit.

### item-04: FindSymbolScannerTests → Unit — `…/FindSymbolScannerTests.cs` (Zeile 11)

- **Was:** Standard-Insert vor class Z.11.
- **Warum:** homogen Unit; `#nullable enable` belassen.

### item-05: FindSymbolToolTests → Unit — `…/FindSymbolToolTests.cs` (Zeile 12)

- **Was:** Standard-Insert vor dual-Fixture-class Z.12.
- **Warum:** beide Catalog-Fixtures = Unit (Negativ-Abgrenzung).

### item-06: GetFileSkeletonToolTests → Unit — `…/GetFileSkeletonToolTests.cs` (Zeile 13)

- **Was:** Standard-Insert vor class Z.13.
- **Warum:** homogen Unit.

### item-07: GetHotspotsToolTests → Unit — `…/GetHotspotsToolTests.cs` (Zeile 13)

- **Was:** Standard-Insert vor class Z.13.
- **Warum:** homogen Unit.

### item-08: GetImpactToolTests → Unit — `…/GetImpactToolTests.cs` (Zeile 13)

- **Was:** Standard-Insert vor class Z.13.
- **Warum:** homogen Unit.

### item-09: GetIndexScopeToolTests → Unit — `…/GetIndexScopeToolTests.cs` (Zeile 14)

- **Was:** Standard-Insert vor class Z.14.
- **Warum:** homogen Unit.

### item-10: GetServerHealthToolTests → Unit — `…/GetServerHealthToolTests.cs` (Zeile 18)

- **Was:** Trait zwischen `</summary>` (Z.17) und class (Z.18);
  class → Z.19. **LF-only:** byte-genauer Python-Helper
  (`[Trait("Category", "Unit")]\n`, 28 Bytes), EOL bleibt LF-only.
- **Warum:** XML-Doc-Variante + TD-003-Analogon.

### item-11: GetSymbolBodyToolTests → Unit — `…/GetSymbolBodyToolTests.cs` (Zeile 12)

- **Was:** Standard-Insert vor class Z.12.
- **Warum:** homogen Unit.

### item-12: GetTypeHierarchyToolTests → Unit — `…/GetTypeHierarchyToolTests.cs` (Zeile 13)

- **Was:** Standard-Insert vor class Z.13.
- **Warum:** homogen Unit.

### item-13: GetViolationsToolTests → Unit — `…/GetViolationsToolTests.cs` (Zeile 18)

- **Was:** Standard-Insert vor class Z.18.
- **Warum:** homogen Unit.

### item-14: ReloadConfigToolTests → Unit — `…/ReloadConfigToolTests.cs` (Zeile 22)

- **Was:** Trait zwischen `</summary>` (Z.21) und class (Z.22);
  class → Z.23. **LF-only:** Python-Helper analog item-10.
- **Warum:** Mini-Fixture in-process = Unit; XML-Doc + LF-only.

### item-15: SafeguardScannerTests → Unit — `…/SafeguardScannerTests.cs` (Zeile 26)

- **Was:** Trait zwischen `</summary>` (Z.25) und class (Z.26);
  class → Z.27. String-Literal-Klassen und Methoden-Bodies
  unangetastet.
- **Warum:** 17 echte attr-level Facts; keine String-Literal-Facts.

### item-16: SafeguardToolTests → Unit — `…/SafeguardToolTests.cs` (Zeile 29)

- **Was:** Trait zwischen `</summary>` (Z.28) und class (Z.29);
  class → Z.30.
- **Warum:** XML-Doc-Variante, homogen Unit.

### item-17: SearchPatternToolTests → Unit — `…/SearchPatternToolTests.cs` (Zeile 13)

- **Was:** Standard-Insert vor class Z.13.
- **Warum:** homogen Unit.

### item-18: TD-007 Hilfsdateien löschen — `tasks/flaky-and-test-performance/step-012/`

- **Was:** Dateien `_insert_trait_skeleton.py` und
  `_code_commit_msg.txt` löschen (Working Tree + Commit). Bevorzugt
  im **Doku-Commit** dieses Steps (oder zusammen mit Code-Commit,
  wenn praktischer — Hauptsache beide weg und im Diff sichtbar).
- **Warum:** `auto_fixable: ja`; Nutzer will Tech-Debt angehen;
  Spec §9.1/§10.6.

## Tests

- [ ] Keine neuen Tests — nur Category-Traits + Dateilöschung.
- [ ] Verifikation über Filter-/Voll-Läufe in der Definition of Done.

## Definition of Done

- [ ] Alle 17 Trait-Inserts + TD-007-Löschung umgesetzt
- [ ] `dotnet build` grün (0 Warnungen, 0 Fehler)
- [ ] Self-Lint: `dotnet run --project src/AiNetLinter -- --config rules.json --path .` → `OK` (TD-001-konform, **kein** `--self-lint`)
- [ ] `dotnet test --filter "Category=Unit"` → grün,
  **1130** Tests (984 + 146)
- [ ] `dotnet test --filter "Category=Integration"` → best-effort
  (bekannter EPIC-06-Flaky `McpServerCommandLoadingStateTests…`
  darf failen; nicht step-013-Scope)
- [ ] `dotnet test` (Voll) → grün, **1325** Tests (±0 Total)
- [ ] **EOL-Vollscan** aller 17 Tools-Dateien: 15 CRLF erhalten
  (CR==LF vor/nach, +1 Zeile), 2 LF-only erhalten (CR=0, LF+1);
  Trailing-NL erhalten; 0 BOM vorher/nachher
- [ ] **Helper-Disziplin:** `DiRegistrationMiniFixtureWorkspace` und
  String-Literal-Klassen in `SafeguardScannerTests` **ohne** Trait
- [ ] **Numerische Plausibilität:** 146 attr-`[Fact]`, 0 Theory,
  0 String-Literal-`[Fact]`-Extra → +146 Unit
- [ ] Code-Commit zuerst, Hash in `step-result.md`; Subject exakt:
  `test: Mcp/Tools-Tests Kategorie-taggen [flaky-and-test-performance]`
  (**66 Zeichen**, 6 Reserve zur 72-Grenze) — Coder übernimmt
  unverändert (TD-002-Disziplin)
- [ ] Doku-Commit danach inkl. `step-result.md`, Status
  `done (pending audit)`, **CodeMap-Update** (`Mcp/Tools/` 17/17),
  TD-007-Löschung falls nicht schon im Code-Commit; Subject-Beispiel:
  `docs(tasks): step-013 Result dokumentieren [flaky-and-test-performance]`
  (**70 Zeichen**)
- [ ] `step-result.md` mit EOL-Tabelle (17 Zeilen), Filter-Delta,
  Commit-Hashes; keine neuen Hilfsdateien-Leichen im Doku-Commit
- [ ] Pipeline-Konvention step-011/012: Code-Commit → Result mit Hash
  → Doku-Commit (kein 3-Commits-Mechanismus)

## Rules-Refs

- `.agents/rules/AiNetLinterRichtlinien.mdc` §4 — Conventional
  Commits DE, Subject ≤ 72 Zeichen; §5 Parallelität unberührt
  (nur Traits, keine Collections)
- `.agents/rules/AiNetLinter.mdc` — `*.Tests`-Overrides; Trait-
  Schreibweise CamelCase `"Unit"`; `#nullable`/BOM nicht anfassen

## Bekannte Ausnahmen

- Integration-Filter darf den bekannten EPIC-06-Flaky
  `McpServerCommandLoadingStateTests.LoadState_LoadFuncCompletesSynchronouslyWithCatalog_ReportsLoadedImmediately`
  failen lassen (out of scope; Voll-Lauf muss grün sein).
- TD-001/003/004/005/006 **nicht** in diesem Step fixen (TD-001
  Nutzer: OUT OF SCOPE; übrige ohne klare Auto-Fix-Richtung /
  `auto_fixable: nein`).
- TD-002 bleibt Prozess-Disziplin (Subject-Vorgabe oben).

## Code-Skizze (optional)

```csharp
// Standard-Insert (CRLF-Dateien):
[Trait("Category", "Unit")]
public sealed class CallGraphTraversalTests : IClassFixture<SymbolGraphCatalogFixture>

// XML-Doc-Variante:
/// </summary>
[Trait("Category", "Unit")]
public sealed class GetServerHealthToolTests : IClassFixture<SymbolGraphCatalogFixture>

// LF-only (Python): insert b'[Trait("Category", "Unit")]\n' vor class-Zeile
```

## Notes

- **Erwartetes Filter-Delta:** Unit 984 → **1130** (+146);
  Integration **113** (±0); Total **1325** (±0).
- **Diff-Budget:** ~17 Trait-Zeilen + 2 Dateilöschungen ≪ 80
  Diff-Zeilen / 20 Items.
- **CodeMap (Coder-Pflicht Schritt 6a):** `Mcp/Tools/` auf
  „17/17 mit Unit-Trait, Ordner abgehakt (zuletzt: step-013)“;
  Planer hat `Core/` 19/19 und `Maps/` 6/6 bereits nachgezogen.
- **Folge-Steps (nur informativ, nicht geplant):** `Mcp/`-Root-
  Rest (Unit-Cluster der 5 ungetaggten), dann gemischte
  `Baseline/` / `Commands/` / `Cli/` (teilweise Integration,
  ggf. pro-Methode-Tagging für `McpServerCommandTests`).
- **Keine Produktcode-Änderungen**; Orchestrator committed.
