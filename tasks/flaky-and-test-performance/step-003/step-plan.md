---
status: open
type: step-plan
task: flaky-and-test-performance
step: 003
title: "Category-Traits für alle Tests in src/AiNetLinter.Tests/Metrics/ nachziehen (Batch 2 von N)"
epic: EPIC-02
estimated_risk: low
step_type: batch
items:
  - id: item-01
    title: "AIContextFootprintDeduplicationTests → Unit (reine in-process Logik)"
    source: "konzept.md §Wie Schritt 2, §Muss-Haven Traits-Punkt"
  - id: item-02
    title: "CognitiveComplexityGuidanceTests → Unit"
    source: "konzept.md §Wie Schritt 2"
  - id: item-03
    title: "CognitiveComplexityWalkerTests → Unit"
    source: "konzept.md §Wie Schritt 2"
  - id: item-04
    title: "FileLimitGuidanceTests → Unit"
    source: "konzept.md §Wie Schritt 2"
  - id: item-05
    title: "MaxDirectoryChildrenTests → Unit"
    source: "konzept.md §Wie Schritt 2"
  - id: item-06
    title: "MethodLineCounterTests → Unit"
    source: "konzept.md §Wie Schritt 2"
  - id: item-07
    title: "PostAnalysisChecksPathOverrideTests → Unit"
    source: "konzept.md §Wie Schritt 2"
created_by: planer
created_by_model: MiniMax-M3
created_by_model_knowledge_cutoff: 2026-01
created_at: 2026-08-07T10:09:36+02:00
related_to: []
---

# Step 003: Category-Traits für `src/AiNetLinter.Tests/Metrics/` (Batch 2)

## Bezug

- **Task:** `flaky-and-test-performance`
- **Epic:** `EPIC-02` aus `roadmap.md` — Category-Traits flächendeckend nachziehen.
  Zweiter von N Batches (deckt 7 weitere ungetaggte Testklassen ab).
- **Konzept-Referenz:** `konzept.md` §"Wie" Schritt 2 ("Category-Traits nachziehen —
  alle ~1000 ungetraggten Tests einordnen"), §"Muss-Haven" Traits-Punkt ("konsequente
  Category-Traits ... auf **allen** Tests — aktuell nur 86 von ~1087"), §"Definition
  of Done" Punkt "Alle Tests tragen einen Category-Trait".
- **Vorgänger-Step:** `step-002` (approved am 2026-08-07, zwei NITPICKs) — Batch 1
  mit 8 Klassen aus `Suppression/`. Lieferte die Klassifikations-Heuristik, die
  Trait-Syntax-Konvention und die DoD-Struktur. Dieser Step wendet das identische
  Vorgehen auf den nächsten homogenen Unit-Ordner an.

## Aktueller Projektzustand (JIT-Kontext)

Beim Lesen des Projekts vorgefunden (relevant für step-003):

- **Test-Inventar im Ziel-Ordner:** 7 `*.cs`-Dateien unter
  `src/AiNetLinter.Tests/Metrics/`:
  1. `AIContextFootprintDeduplicationTests.cs`
  2. `CognitiveComplexityGuidanceTests.cs`
  3. `CognitiveComplexityWalkerTests.cs`
  4. `FileLimitGuidanceTests.cs`
  5. `MaxDirectoryChildrenTests.cs`
  6. `MethodLineCounterTests.cs`
  7. `PostAnalysisChecksPathOverrideTests.cs`
- **Konzept-Schätzung vs. Realität:** Konzept-Schätzung "7 Klassen" exakt bestätigt.
- **Bestehende Trait-Verteilung im Ordner:** **0 Klassen mit Trait** (verifiziert
  per `grep -E "\[Trait\("` über `src/AiNetLinter.Tests/Metrics/` — keine Treffer).
- **Subprozess-Marker im Ordner:** **0 Treffer** für `McpTestClient`,
  `CliProcessRunner`, `Program.Main`, `IClassFixture<McpLiveRepositoryFixture>`
  (verifiziert per `grep` über das Verzeichnis — keine Treffer). Damit ist der
  gesamte Ordner homogen Unit.
- **Klassen-Deklarationen mit XML-Doc** (3 von 7, d. h. die Trait-Zeile muss
  **zwischen** XML-Doc und `public sealed class ...` eingefügt werden, nicht
  davor — analog zur IgnoreSuppressionsFilter-Konvention aus step-002):
  - `CognitiveComplexityGuidanceTests.cs` — XML-Doc endet Z. 13, Klasse Z. 14
  - `FileLimitGuidanceTests.cs` — XML-Doc endet Z. 14, Klasse Z. 15
  - `PostAnalysisChecksPathOverrideTests.cs` — XML-Doc endet Z. 18, Klasse Z. 19
- **Klassen-Deklarationen ohne XML-Doc** (4 von 7, Trait-Zeile direkt über
  `public sealed class ...`):
  - `AIContextFootprintDeduplicationTests.cs:9`
  - `CognitiveComplexityWalkerTests.cs:8`
  - `MethodLineCounterTests.cs:11`
  - `MaxDirectoryChildrenTests.cs:13` (implementiert zusätzlich `IDisposable` —
    verwendet im Konstruktor `Path.GetTempPath()` + `Directory.CreateDirectory`
    zum Anlegen eines Temp-Verzeichnisses, in `Dispose` analog
    `Directory.Delete(..., recursive: true)`. **Rein in-process**, passt zur
    Negativ-Abgrenzung "Mini-Fixture-Workspace / TempDir-Operationen" aus
    step-002.)
- **Interne Hilfsklassen in `MethodLineCounterTests.cs`:** Die Datei enthält in
  den Zeilen 25, 45, 85 drei weitere `public sealed class Sample`-Deklarationen
  (innerhalb der `namespace Test;`-Scopes). Das sind **Hilfsklassen für
  Roslyn-SyntaxTree-Sample-Code** und keine Testklassen — sie tragen weder
  `[Fact]`/`[Theory]`-Methoden noch einen Klassennamen mit `Tests`-Suffix
  im Konvention-Sinne. Sie sind **nicht** Teil dieses Steps und bleiben
  unverändert. Der Coder verifiziert dies visuell beim Edit und weicht
  entsprechend ab, falls eine andere Lesart plausibler ist (siehe Notes).
- **Gewählter Batch-Begründung:** `Metrics/` ist der einfachste
  Folge-Batch nach `Suppression/` — homogen Unit (kein einziger Subprozess-
  Marker im Ordner), 7 Klassen (passt in den 8-Item-Deckel von `spec.md` §10.6
  mit einem Slot Reserve), keine bestehenden Traits (kein Konflikt mit
  bereits getaggten Methoden), klein und klar abgegrenzt. Ideal als
  Template-Validierung: demonstriert die Heuristik an einem **rein
  Unit-dominierten** Ordner (im Gegensatz zu step-002, das mit
  `DisableAllCliTests` auch eine Integration-Klasse im selben Ordner
  hatte).

## Intention

Alle 7 Testklassen unter `src/AiNetLinter.Tests/Metrics/` mit
`[Trait("Category", "Unit")]` auf Klassen-Ebene versehen. Dieser Step ist der
zweite von N Batches, die zusammen die EPIC-02-DoD erreichen ("alle ~1000 Tests
getraggt"). Er bestätigt die in step-002 bewährte Vorgehensweise (Klassen-
Heuristik, Diff-Umfang, Validierung) an einem rein Unit-dominierten Ordner und
liefert damit eine zweite Template-Validierung für die Folge-Batches, bevor
diese in die größeren, gemischten Verzeichnisse (`Core/Checkers/`,
`Mcp/`, `Commands/`) vorstoßen. Er **demonstriert** außerdem die korrekte
Trait-Platzierung in zwei lokalen Varianten: mit XML-Doc über der Klasse
(3 Klassen) und ohne XML-Doc (4 Klassen).

## Klassifikations-Heuristik für diesen Batch

Die in step-002 dokumentierte Heuristik wird unverändert übernommen:

1. **Bestehende Traits prüfen.** Im `Metrics/`-Ordner keine bestehenden Traits
   (verifiziert per `grep`).
2. **Subprozess-Marker prüfen.** Im `Metrics/`-Ordner keine Subprozess-Marker
   (verifiziert per `grep` über `McpTestClient`, `CliProcessRunner`,
   `Program.Main`, `IClassFixture<McpLiveRepositoryFixture>`). Damit ist
   **keine** Klasse in diesem Batch `Integration`.
3. **Sonst: Unit.** Trifft auf alle 7 Klassen in diesem Batch zu.

**Wichtige Negativ-Abgrenzung** (aus step-002, weiterhin gültig): Die
folgenden Muster sind **KEIN** Subprozess und führen nicht zu `Integration`:

- `BaselineMiniFixtureWorkspace`, `CompileErrorMiniFixtureWorkspace`,
  `SymbolGraphMiniFixtureWorkspace`, `BlazorPartialMiniFixtureWorkspace`,
  `GitImpactMiniFixtureWorkspace`, `SingleCompileErrorMiniFixtureWorkspace` —
  alles in-process, erzeugen nur ein Mini-Filesystem im TempDir
- Direkte `Path.GetTempPath()` / `Path.GetTempFileName()`-Verwendungen
  (`MaxDirectoryChildrenTests.cs` Konstruktor + Dispose) — in-process
- `SourceFileCatalog.LoadAsync(` auf einer Mini-Solution — in-process
- `IClassFixture<SymbolGraphCatalogFixture>` /
  `IClassFixture<BaselineCatalogFixture>` — in-process

## Konkrete Änderungen

**Bei `step_type: batch`** (siehe `../spec.md` §10.6): pro Item aus der
`items`-Liste im Frontmatter eine Unterüberschrift.

### item-01: `AIContextFootprintDeduplicationTests` → Unit — `src/AiNetLinter.Tests/Metrics/AIContextFootprintDeduplicationTests.cs` (Klassen-Deklaration, Z. 9)

- **Was:** Direkt über `public sealed class AIContextFootprintDeduplicationTests`
  (Z. 9) eine Zeile `[Trait("Category", "Unit")]` einfügen. Keine XML-Doc
  vorhanden — daher genügt eine Zeile direkt über der Klassendeklaration.
- **Warum:** Klasse enthält 4 Testmethoden auf rein in-process Deduplizierungs-
  Logik (vermutlich auf AI-Context-Footprint-Datenstrukturen). Keine
  Subprozess-Marker im File-Grep. `Path.GetTempPath()` o. ä. möglich, wäre
  in-process.

### item-02: `CognitiveComplexityGuidanceTests` → Unit — `src/AiNetLinter.Tests/Metrics/CognitiveComplexityGuidanceTests.cs` (Z. 13-14, zwischen XML-Doc und Klasse)

- **Was:** Zwischen dem XML-Doc-Abschluss (Z. 13, `</summary>`) und
  `public sealed class CognitiveComplexityGuidanceTests` (Z. 14) eine Zeile
  `[Trait("Category", "Unit")]` einfügen. **Achtung:** Die XML-Doc ist Teil
  des Klassen-Symbols — das Trait-Attribut gehört **zwischen** XML-Doc und
  Klassendeklaration, nicht darüber (analog zur `IgnoreSuppressionsFilter`-
  Konvention aus step-002).
- **Warum:** Guidance-Tests (Konfigurations-empfehlungen) — rein in-process,
  keine Subprozess-Marker im File-Grep.

### item-03: `CognitiveComplexityWalkerTests` → Unit — `src/AiNetLinter.Tests/Metrics/CognitiveComplexityWalkerTests.cs` (Klassen-Deklaration, Z. 8)

- **Was:** Direkt über `public sealed class CognitiveComplexityWalkerTests`
  (Z. 8) eine Zeile `[Trait("Category", "Unit")]` einfügen. Keine XML-Doc
  vorhanden.
- **Warum:** SyntaxWalker-Tests — rein in-process Roslyn-Operationen, keine
  Subprozess-Marker im File-Grep.

### item-04: `FileLimitGuidanceTests` → Unit — `src/AiNetLinter.Tests/Metrics/FileLimitGuidanceTests.cs` (Z. 14-15, zwischen XML-Doc und Klasse)

- **Was:** Zwischen dem XML-Doc-Abschluss (Z. 14) und
  `public sealed class FileLimitGuidanceTests` (Z. 15) eine Zeile
  `[Trait("Category", "Unit")]` einfügen (zwischen XML-Doc und Klasse, wie
  item-02).
- **Warum:** Guidance-Tests auf File-Limit-Empfehlungen — rein in-process,
  keine Subprozess-Marker im File-Grep.

### item-05: `MaxDirectoryChildrenTests` → Unit — `src/AiNetLinter.Tests/Metrics/MaxDirectoryChildrenTests.cs` (Klassen-Deklaration, Z. 13)

- **Was:** Direkt über `public sealed class MaxDirectoryChildrenTests :
  IDisposable` (Z. 13) eine Zeile `[Trait("Category", "Unit")]` einfügen.
  Keine XML-Doc vorhanden.
- **Warum:** Konstruktor legt ein Temp-Verzeichnis an (`Path.GetTempPath()` +
  `Directory.CreateDirectory`), `Dispose` löscht es rekursiv. **Rein
  in-process** — passt zur Negativ-Abgrenzung "TempDir-Operationen". Datei
  nutzt `TestHelper.CreateDefaultConfig()` (in-process Config-Builder) und
  `AiNetLinter.Configuration`/`Core`/`Models` (alle in-process). Keine
  Subprozess-Marker im File-Grep.

### item-06: `MethodLineCounterTests` → Unit — `src/AiNetLinter.Tests/Metrics/MethodLineCounterTests.cs` (Klassen-Deklaration, Z. 11)

- **Was:** Direkt über `public sealed class MethodLineCounterTests` (Z. 11)
  eine Zeile `[Trait("Category", "Unit")]` einfügen. Keine XML-Doc vorhanden.
- **Warum:** MethodLineCounter-Tests (vermutlich Line-Counting über
  Roslyn-SyntaxTree) — rein in-process. Die drei internen `public sealed
  class Sample`-Deklarationen in den `namespace Test;`-Scopes (Z. 25, 45, 85)
  sind **keine** Testklassen und bleiben **unverändert** — siehe Notes.

### item-07: `PostAnalysisChecksPathOverrideTests` → Unit — `src/AiNetLinter.Tests/Metrics/PostAnalysisChecksPathOverrideTests.cs` (Z. 18-19, zwischen XML-Doc und Klasse)

- **Was:** Zwischen dem XML-Doc-Abschluss (Z. 18) und
  `public sealed class PostAnalysisChecksPathOverrideTests` (Z. 19) eine Zeile
  `[Trait("Category", "Unit")]` einfügen (zwischen XML-Doc und Klasse, wie
  item-02).
- **Warum:** Path-Override-Tests — rein in-process String/Path-Operationen,
  keine Subprozess-Marker im File-Grep.

## Tests

Keine — die Klassifikation ist rein additiv (Attribut setzen). Existierende Tests
müssen **unverändert** grün bleiben. Validierung erfolgt über den vollen
`dotnet test`-Lauf in der Definition of Done (kein neuer Test, kein geänderter
Test).

## Definition of Done

- [ ] Alle 7 Items umgesetzt (je eine `[Trait("Category", "Unit")]`-Zeile auf
  Klassen-Ebene, ggf. zwischen XML-Doc und Klassendeklaration)
- [ ] **Bestehende Traits respektiert:** keine vorhandenen Trait-Attribute
  überschrieben oder entfernt (Trifft im Batch nicht zu, aber als Plausibilitäts-
  Check zu verifizieren: nach dem Diff sollten in `Metrics/` 7 Klassen mit
  Trait-Attribut existieren, 0 ohne.)
- [ ] **Build-Command** aus Tech-Stack-Notiz (`roadmap.md`) grün: `dotnet build`
- [ ] **Test-Command** aus Tech-Stack-Notiz grün: `dotnet test` (voller Lauf,
  alle Tests müssen weiterhin grün sein — keine Test-Logik wurde geändert)
- [ ] **Trait-Filter-Smoke-Test** (vom Coder zusätzlich durchführen, um die
  Klassifikation zu verifizieren):
  - `dotnet test --no-build --filter "Category=Unit"` → muss grün sein
  - `dotnet test --no-build --filter "Category=Integration"` →
    **best-effort, ein Lauf grün** (siehe step-002 NITPICK: pre-existing Flaky-
    Test `McpServerCommandLoadingStateTests.LoadState_LoadFuncCompletesSynchronouslyWithCatalog_ReportsLoadedImmediately`
    flake-t gelegentlich unter Last des Integration-Filters; nicht step-003-
    verursacht, Fix in EPIC-06). Der Coder dokumentiert im `step-result.md`,
    wenn der Lauf flaky ist, und startet ihn ggf. einmal neu.
  - Numerische Begründung im `step-result.md` dokumentieren: die
    Unit-Filter-Zahl sollte im Vergleich zu step-002 um die in
    `Metrics/` hinzugefügten Methoden steigen, die Integration-Filter-Zahl
    unverändert bleiben.
- [ ] **Self-Lint** (TD-001-konform, semantisch identisch zu `--self-lint`):
  `dotnet run --project src/AiNetLinter -- --config rules.json --path .` →
  muss `OK` ausgeben
- [ ] **Commit auf aktuellem Branch** (Conventional Commit auf Deutsch,
  imperativ, mit Task-Suffix `[flaky-and-test-performance]`): empfohlener
  Subject: `chore(tests): Metrics-Tests mit Category-Traits versehen
  [flaky-and-test-performance]` (71 Zeichen, unter 72-Zeichen-Grenze). Der
  Coder hält die 72-Zeichen-Grenze ein, dokumentiert die Wahl und passt
  Subject-Präfix/Suffix-Kombination bei Bedarf an, solange Conventional-Commit-
  Konvention und Suffix erhalten bleiben.
- [ ] `step-003/step-result.md` geschrieben mit: Diff-Statistik (Anzahl
  hinzugefügter Trait-Zeilen pro Item, Gesamt-Diff), Testergebnis
  (Gesamt-Lauf + 2 Filter-Läufe mit Test-Zahlen), Build-Output, Self-Lint-Output,
  Commit-Hash, Subjekt. `### Commit-Vorschlag`-Block am Ende der Antwort
  (Pflicht — siehe `AiNetLinterRichtlinien.mdc` §4, Commit-Vorschlag-Pflicht).
- [ ] `status` in `step-plan.md` von `open` auf `in_progress` (durch Orchestrator
  nach Coder-Start) und nach `step-result.md`-Schreiben auf `done (pending
  audit)` (durch Coder) gesetzt.

## Rules-Refs

- `.agents/rules/AiNetLinterRichtlinien.mdc` §4 "Testsuite-Parallelität bewahren"
  — relevant nur als Ausschluss: Trait-Attribute haben **keinen** Einfluss auf
  Parallelismus, nur `[Collection(...)]` / `DisableParallelization`. Dieser Step
  berührt die Parallelität nicht, ist also nicht regel-restriktiv hier.
- `.agents/rules/AiNetLinterRichtlinien.mdc` §5 "Sparsame Kommentare" — die
  hinzugefügten Trait-Zeilen sind XML-Attribute, keine Kommentare. Kein Bezug.
- `.agents/rules/AiNetLinterRichtlinien.mdc` §5 "Zero-Warning-Direktive" — die
  Trait-Attribute sind `[Trait("Category", "Unit")]`, exakt die im Projekt
  etablierte Schreibweise (Großbuchstabe am Wortanfang). Keine Warnung erwartet,
  da das exakt der bestehenden Konvention folgt.
- `.agents/rules/AiNetLinterRichtlinien.mdc` §4 "Commit-Vorschlag Pflicht" —
  betrifft die Coder-Antwort, ist im DoD-Punkt oben explizit referenziert.
- `.agents/rules/AiNetLinterRichtlinien.mdc` §5 "Symptom-Fixing verboten" —
  betrifft diesen Step nicht direkt, aber als Plausibilitäts-Check: wenn ein
  Test rot wird, ist die Ursache zu suchen, nicht der Test abzuschwächen.

## Bekannte Ausnahmen

- **Pre-Existing-Flaky-Test im Integration-Filter** (aus step-002-Review
  übernommen): `McpServerCommandLoadingStateTests.LoadState_LoadFuncCompletesSynchronouslyWithCatalog_ReportsLoadedImmediately`
  flake-t gelegentlich unter Last des `Category=Integration`-Filters. Nicht
  step-003-verursacht (rein additives Attribut, keine Logik-/Parallelitäts-
  Änderung), Fix in EPIC-06. Der Coder behandelt den Integration-Filter-Lauf
  als "best-effort, ein Lauf grün" (siehe DoD).

## Code-Skizze (optional)

Vorher (Beispiel: `AIContextFootprintDeduplicationTests.cs`, Z. 7-10):

```csharp
using Xunit;

namespace AiNetLinter.Tests.Metrics;

public sealed class AIContextFootprintDeduplicationTests
{
    [Fact]
    public void Deduplicate_EmptyInput_ReturnsEmpty()
```

Nachher:

```csharp
using Xunit;

namespace AiNetLinter.Tests.Metrics;

[Trait("Category", "Unit")]
public sealed class AIContextFootprintDeduplicationTests
{
    [Fact]
    public void Deduplicate_EmptyInput_ReturnsEmpty()
```

Für `CognitiveComplexityGuidanceTests.cs` (Beispiel mit XML-Doc, Z. 12-16):

```csharp
/// <summary>
/// Tests fuer ...
/// </summary>
public sealed class CognitiveComplexityGuidanceTests
{
    [Fact]
    public void SomeTest()
```

wird zu:

```csharp
/// <summary>
/// Tests fuer ...
/// </summary>
[Trait("Category", "Unit")]
public sealed class CognitiveComplexityGuidanceTests
{
    [Fact]
    public void SomeTest()
```

## Notes

- **Batch-Umfang:** 7 Klassen × je 1 Trait-Zeile ≈ 7–10 Diff-Zeilen. Deutlich
  unter dem `max_batch_diff_lines: 40`-Deckel.
- **Schritt-Typ `low`-Risk-Begründung:** rein additives Attribut auf Klassen, das
  weder Build-Verhalten noch Test-Verhalten noch Parallelität ändert. Trait-Wert
  folgt exakt der bestehenden 86-Eintrag-Konvention (`Unit`, CamelCase-Großbuchstabe).
  Kein Eingriff in Produktionscode, keine Fixture-Änderung, keine Test-Logik-Änderung.
- **Spezialfall `MethodLineCounterTests.cs` (item-06):** Die Datei enthält drei
  interne `public sealed class Sample`-Deklarationen in `namespace Test;`-Scopes
  (Z. 25, 45, 85). Diese sind **Hilfsklassen für Roslyn-SyntaxTree-Sample-Code**
  (Method-Line-Counter-Tests erzeugen programmatisch SyntaxTrees mit bekannten
  Strukturen, um die Counter-Logik zu prüfen). Sie tragen weder `[Fact]`/
  `[Theory]`-Methoden noch einen `Tests`-Suffix im Konvention-Sinne. **Sie sind
  nicht Teil dieses Steps** und bleiben unverändert. Der Coder verifiziert dies
  visuell beim Edit (eine kurze Inspektion der 3 Deklarationen + umgebender
  Zeilen genügt) und passt nur die **einzige** Testklasse
  `MethodLineCounterTests` an (Z. 11).
- **Spezialfall `MaxDirectoryChildrenTests` (item-05):** Die Klasse
  implementiert `IDisposable` für TempDir-Cleanup. Konstruktor + Dispose sind
  beide rein in-process (nur `Path.GetTempPath()` + `Directory.CreateDirectory`
  / `Directory.Delete(..., recursive: true)`). Das `IDisposable`-Interface
  ändert nichts an der Klassifikation — die Klasse bleibt `Unit`.
- **Folge-Batches (NICHT in diesem Step geplant):** Die EPIC-02-Arbeit umfasst
  weiterhin ca. 152 verbleibende ungetaggte Testklassen. Vorschlag für die
  Reihenfolge der nächsten Step-Modus-Aufrufe (rein informativ — Planung der
  einzelnen Folge-Steps ist Sache der jeweiligen Planer-Aufrufe, nicht dieses
  Plans):
  1. **Reine-Unit-Ordner, klein** (einfachster Fall, Klassen-Trait durchgängig):
     - `Web/` (5 Klassen, alle Unit)
     - `Architecture/` (1 Klasse, Unit)
     - `Diagnostics/` (1 Klasse, Unit)
     - `FalsePositives/` (2 Klassen, Unit)
     - `Cache/` (3 Klassen, Unit)
     - `Evals/` (3 Klassen, Unit — SpecLoader/EvalAssembler/Command-Tests,
       Spezialfall `ListEvalsCommandTests` möglicherweise Integration via
       Subprozess, JIT zu prüfen)
     - `Output/` (10 Klassen, alle Unit)
  2. **Reine-Unit-Ordner, groß** (gleiche Heuristik, aber mehr Items pro Batch
     aufteilen):
     - `Configuration/` (8 Klassen, alle Unit)
     - `Core/Checkers/` (27 Klassen, alle Unit) — mehrere Batches
     - `Core/` (19 Klassen, alle Unit) — mehrere Batches
     - `Maps/` + `Maps/Skeleton/` (6 Klassen, alle Unit)
  3. `Mcp/Tools/` (17 Klassen, fast alle Unit, Mini-Fixture-Workspace → Unit) —
     2–3 Batches
  4. **Verzeichnisse mit echtem Subprozess-Anteil** (Heuristik-Ausnahmen,
     erfordern mehr Sorgfalt):
     - `Mcp/` (19 Klassen, gemischt; `McpCodeGraphServer*Tests` Unit,
       `McpLiveRepositoryTests`/`McpDocumentationSmokeTests` Integration)
     - `Baseline/` (10 Klassen, gemischt; `BaselineCliTests`/`WebBaselineTests`
       Integration, `SourceFileCatalog*Tests` Unit)
  5. **`Commands/`** (17 Klassen, stark gemischt; `McpServerCommandTests` ist
     die prominenteste gemischte Klasse — 5 Unit + 18 Integration in einer
     Klasse, erfordert pro-Methode-Tagging) — mehrere Batches, höchste
     Komplexität. **Empfehlung:** die gemischte `McpServerCommandTests.cs` als
     eigenen Step planen (voraussichtlich `step-XXX` mit `step_type: single`),
     um die Methoden-Ebene-Heuristik sauber zu dokumentieren.
  6. **`Fixtures/`-eigene Tests** (`LoadFixtureBuilderTests`,
     `LoadFixtureMeasurementsTests`, `TD016aRefactorTests` — bereits getraggt)
     und die `Cli/`-Klasse `CliCommandBuilderMcpLogTests` (Unit) — am Ende als
     Aufräum-Batch, falls noch nicht in vorherigen Batches erledigt.
- **Gesamt-Fortschritt nach step-003 (geschätzt):** 94 → 94+7 = 101 getaggte
  Klassen/Methoden (entspricht ca. 60 % der 168 Testklassen; bei den 1085
  Methoden eher ~9 %, da die 86 Ist-Werte Klassen-Level-Traits sind, die viele
  Methoden abdecken). **Rest-Bestand nach step-003:** ca. 153 ungetaggte
  Klassen, ca. 983 ungetaggte Methoden — EPIC-02 ist noch **weit** von "alle
  Tests getraggt" entfernt; die DoD wird über mehrere Folge-Steps erreicht.
  Dies ist **erwartet** und kein Planungsfehler.
- **Numerische Vorab-Erwartung an die Filter-Läufe:** Aus step-002 wissen wir
  `Category=Unit` = 172 Tests und `Category=Integration` = 113 Tests (172+113=285
  getaggte Methoden aus 1325 Gesamt, 1040 ungetragte Methoden). Nach step-003
  ist eine **Erhöhung** der `Category=Unit`-Zahl um die Anzahl der Testmethoden
  in den 7 `Metrics/`-Klassen zu erwarten (vom Coder im step-result zu zählen
  und dokumentieren); die `Category=Integration`-Zahl sollte unverändert bei
  113 bleiben. `dotnet test` (voller Lauf) sollte weiterhin 1325 Tests zeigen.
- **Doku-Pflicht:** Nach Abschluss aller EPIC-02-Batches (nicht nach jedem
  Batch) muss `roadmap.md` aktualisiert werden, um den EPIC-02 als abgeschlossen
  zu markieren und die DoD-Punkte aus `konzept.md` §"Definition of Done" durch-
  zugehen. Diese Pflicht ist **nicht** Teil von step-003, sondern gehört in den
  letzten EPIC-02-Batch oder in den EPIC-08-Abschluss-Validierungs-Step.
