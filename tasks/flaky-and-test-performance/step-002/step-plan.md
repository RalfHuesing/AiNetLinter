---
status: done (pending audit)
type: step-plan
task: flaky-and-test-performance
step: 002
title: "Category-Traits für alle Tests in src/AiNetLinter.Tests/Suppression/ nachziehen (Batch 1 von N)"
epic: EPIC-02
estimated_risk: low
step_type: batch
items:
  - id: item-01
    title: "DisableAllCliTests → Integration (Subprozess-Starts)"
    source: "konzept.md §Wie Schritt 2, §Muss-Haven Traits-Punkt"
  - id: item-02
    title: "DisableAllCommentInjectorTests → Unit (reine in-process Logik)"
    source: "konzept.md §Wie Schritt 2"
  - id: item-03
    title: "DisableAllCommentRemoverTests → Unit"
    source: "konzept.md §Wie Schritt 2"
  - id: item-04
    title: "IgnoreSuppressionsFilterTests → Unit"
    source: "konzept.md §Wie Schritt 2"
  - id: item-05
    title: "SuppressionCommentParserTests → Unit"
    source: "konzept.md §Wie Schritt 2"
  - id: item-06
    title: "SuppressionEvaluatorTests → Unit"
    source: "konzept.md §Wie Schritt 2"
  - id: item-07
    title: "SuppressionScannerTests → Unit"
    source: "konzept.md §Wie Schritt 2"
  - id: item-08
    title: "ViolationPathResolverTests → Unit"
    source: "konzept.md §Wie Schritt 2"
created_by: planer
created_by_model: MiniMax-M3
created_by_model_knowledge_cutoff: 2026-01
created_at: 2026-08-07T11:00:00+02:00
related_to: []
---

# Step 002: Category-Traits für `src/AiNetLinter.Tests/Suppression/` (Batch 1)

## Bezug

- **Task:** `flaky-and-test-performance`
- **Epic:** `EPIC-02` aus `roadmap.md` — Category-Traits flächendeckend nachziehen.
  Erster von N Batches (deckt nur 8 von ~168 ungetaggten Testklassen ab).
- **Konzept-Referenz:** `konzept.md` §"Wie" Schritt 2 ("Category-Traits nachziehen —
  alle ~1000 ungetraggten Tests einordnen"), §"Muss-Haven" Traits-Punkt ("konsequente
  Category-Traits ... auf **allen** Tests — aktuell nur 86 von ~1087"), §"Definition
  of Done" Punkt "Alle Tests tragen einen Category-Trait".
- **Vorgänger-Step:** `step-001` (approved) — Spike `SymbolGraphMcpFixture` →
  `ICollectionFixture`. Konzept-/CLI-Diskrepanz `--self-lint` als TD-001 dokumentiert
  (kein Einfluss auf step-002).

## Aktueller Projektzustand (JIT-Kontext)

Beim Lesen des Projekts vorgefunden (relevant für step-002):

- **Test-Inventar:** 192 `*.cs`-Dateien unter `src/AiNetLinter.Tests/` (ohne `bin/obj`),
  ca. 1085 `[Fact]`/`[Theory]`-Methoden, 168 Testklassen insgesamt.
- **Bestehende Trait-Verteilung:** 86 getaggte Methoden / Klassen (67 `Unit`, 19
  `Integration`); ca. 999 Methoden ohne Trait. Trait-Attribut konsequent als
  `[Trait("Category", "Unit")]` / `[Trait("Category", "Integration")]` (CamelCase-Werte
  mit Großbuchstaben, exakt diese Schreibweise ist die Konvention).
- **Existierende Trait-Platzierung:** Sowohl Klassen-Ebene (über `public sealed
  class X`) als auch Methoden-Ebene (zwischen `[Fact]`/`[Theory]` und
  Methoden-Signatur) sind etabliert. Klasse-Ebene dominiert bei homogenen Klassen;
  Methoden-Ebene bei teilweise getaggten / gemischten Klassen (z. B. bereits zu
  sehen in `Commands/McpServerCommandTests.cs:359` — Unit-Trait an einer Methode
  in einer ansonsten Integration-dominierten Klasse).
- **xUnit-Runner-Konfig:** `src/AiNetLinter.Tests/xunit.runner.json` setzt
  `parallelizeTestCollections: true`, `maxParallelThreads: 0`, `longRunningTestSeconds: 3`.
  Traits sind **rein filter-/selektions-relevant**; sie haben KEINEN Einfluss auf
  Parallelismus (nur `[Collection(...)]` / `DisableParallelization` tut das). Daher
  keine `AiNetLinterRichtlinien.mdc` §4-Berührung in diesem Step.
- **Klassifikations-Heuristik** (aus Code-Inspektion abgeleitet, vollständige
  Beschreibung unten unter „Klassifikations-Heuristik für diesen Batch"):
  - **Integration:** `McpTestClient.ConnectAsync(...)`, `CliProcessRunner.RunLinterAsync(...)`
    / `RunAsync(...)`, `Program.Main(...)` als Entry-Point-Aufruf, `IClassFixture<McpLiveRepositoryFixture>`.
  - **Unit:** Alles andere in `Suppression/` (Klassen ohne `CliProcessRunner` /
    `Program.Main` / `McpTestClient`).
- **Gewählter Batch:** `Suppression/` — 8 Testklassen, alle ungetraggt, klar
  abgegrenzt, demonstriert die Heuristik an einem Klassen-Level-Mix (7 Unit + 1
  Integration). Passt genau in den 8-Item-Deckel von `spec.md` §10.6
  (`max_batch_items: 8`).
- **Aus dem Batch ausgenommene Klassen mit Komplexitäts-Bedarf** (bewusst NICHT
  in step-002, gehören in spätere Steps): `Commands/McpServerCommandTests.cs` ist
  eine **gemischte** Klasse — 5 Unit-Tests (`ResolveSolutionPathOrError_*` ohne
  Fixture) neben 18 Integration-Tests (mit `_symbolGraphMcpFixture` /
  `_baselineMcpFixture`); bereits eine Methode hat ein Unit-Trait. Erfordert
  pro-Methode-Tagging. Soll in einem eigenen Step mit größerer Methodenzahl und
  klarer Begründung pro Methode geplant werden (step-003 oder später).

## Intention

Alle 8 Testklassen unter `src/AiNetLinter.Tests/Suppression/` mit
`[Trait("Category", ...)]` auf Klassen-Ebene versehen, gemäß der unten dokumentierten
Heuristik (7 × `Unit`, 1 × `Integration`). Dieser Step ist der erste von N Batches,
die zusammen die EPIC-02-DoD erreichen ("alle ~1000 Tests getraggt"). Er dient
gleichzeitig als **Template** für die Folge-Batches: das hier bewährte Vorgehen
(Klassen-Heuristik, Diff-Umfang, Validierung) wird in den nächsten Step-Modus-
Aufrufen direkt auf andere Verzeichnisse angewendet (Reihenfolge-Vorschlag siehe
Notes). Er **demonstriert** außerdem, wie mit einer gemischten Verzeichnis-Gruppe
(Unit-Klassen + Integration-Klassen im selben Ordner) umzugehen ist — pro Klasse
einheitlich taggen, nicht pro Methode, solange die Klasse homogen ist.

## Klassifikations-Heuristik für diesen Batch

Pro Testklasse entscheiden, ob **alle** ihre Methoden denselben Trait-Wert
tragen sollen (Klassen-Trait) oder ob pro Methode individuell getaggt werden muss
(Method-Trait). Im `Suppression/`-Batch sind alle 8 Klassen homogen → Klassen-Trait
in allen 8 Fällen.

**Heuristik-Schritte (vom Coder streng in dieser Reihenfolge anwenden):**

1. **Bestehende Traits prüfen.** Wenn eine Klasse bereits Trait-Attribute auf
   Klassen- oder Methoden-Ebene trägt: existierende Klassifikation übernehmen und
   nur die noch leeren Stellen füllen. *Trifft in `Suppression/` auf keine
   Klasse zu (kein bestehender Trait).*
2. **Subprozess-Marker prüfen.** Wenn die Klasse mindestens eine dieser Verwendungen
   enthält → **Integration**:
   - `McpTestClient.ConnectAsync(` (Fixture-Start oder Methoden-lokal)
   - `CliProcessRunner.RunLinterAsync(` oder `CliProcessRunner.RunAsync(`
   - Direkt-Aufruf `Program.Main(` (Entry-Point-Aufruf, gleiches Last-Profil wie
     Subprozess-Call)
   - `IClassFixture<McpLiveRepositoryFixture>` (echtes Repo, schwerster Load)
   *Trifft im `Suppression/`-Batch zu auf: `DisableAllCliTests` (verwendet
   `CliProcessRunner.RunLinterAsync` in Methoden 1+2 und `Program.Main` in Methoden
   3+4).*
3. **Sonst: Unit.** Rein in-process, reine Logik-Klassen, Mini-Fixture-Workspaces
   (`BaselineMiniFixtureWorkspace` o. ä. ohne Subprozess), reine Symbol/Parser/
   Rule-Operationen. *Trifft im `Suppression/`-Batch zu auf die übrigen 7 Klassen.*

**Wichtige Negativ-Abgrenzung:** Die folgenden Muster sind **KEIN** Subprozess
und führen nicht zu `Integration`:

- `BaselineMiniFixtureWorkspace`, `CompileErrorMiniFixtureWorkspace`,
  `SymbolGraphMiniFixtureWorkspace`, `BlazorPartialMiniFixtureWorkspace`,
  `GitImpactMiniFixtureWorkspace`, `SingleCompileErrorMiniFixtureWorkspace` —
  alles in-process, erzeugen nur ein Mini-Filesystem im TempDir
- `SourceFileCatalog.LoadAsync(` auf einer Mini-Solution — in-process, kein
  Subprozess (auch wenn MSBuildWorkspace-global-State berührt wird)
- `IClassFixture<SymbolGraphCatalogFixture>` /
  `IClassFixture<BaselineCatalogFixture>` — in-process, laden Mini-Solution
- `IClassFixture<SymbolGraphMcpFixture>` /
  `IClassFixture<BaselineMcpFixture>` — **DOCH** Subprozess (über `McpTestClient.
  ConnectAsync` im Fixture-Konstruktor); Klassen mit dieser Fixture sind
  `Integration`

## Konkrete Änderungen

**Bei `step_type: batch`** (siehe `../spec.md` §10.6): pro Item aus der
`items`-Liste im Frontmatter eine Unterüberschrift.

### item-01: `DisableAllCliTests` → Integration — `src/AiNetLinter.Tests/Suppression/DisableAllCliTests.cs` (Zeile 8)

- **Was:** Direkt über `public sealed class DisableAllCliTests` (Z. 8) eine Zeile
  `[Trait("Category", "Integration")]` einfügen. Keine XML-Doc auf der Klasse
  vorhanden — daher genügt eine Zeile.
- **Warum:** Die Klasse verwendet in Methoden 1+2 `CliProcessRunner.RunLinterAsync`
  (echter `dotnet AiNetLinter.dll`-Subprozess) und in Methoden 3+4 `Program.Main`
  (Entry-Point-Aufruf mit identischem Last-Profil). Konsistent mit `Cli/ProgramTests.cs:15`
  (auch Integration) und `Baseline/BaselineCliTests.cs:8`.

### item-02: `DisableAllCommentInjectorTests` → Unit — `src/AiNetLinter.Tests/Suppression/DisableAllCommentInjectorTests.cs` (Klassen-Deklaration)

- **Was:** Über `public sealed class DisableAllCommentInjectorTests` eine Zeile
  `[Trait("Category", "Unit")]` einfügen (genau dort, wo die Klassendeklaration
  steht, ohne XML-Doc).
- **Warum:** Klasse testet `DisableAllCommentInjector.PrependDisableAll(...)` rein
  in-process auf String-Operationen — kein Subprozess, keine
  `McpTestClient`/`CliProcessRunner`-Verwendung (verifiziert per
  `grep` über die Datei).

### item-03: `DisableAllCommentRemoverTests` → Unit — `src/AiNetLinter.Tests/Suppression/DisableAllCommentRemoverTests.cs` (Klassen-Deklaration)

- **Was:** Über `public sealed class DisableAllCommentRemoverTests` eine Zeile
  `[Trait("Category", "Unit")]` einfügen.
- **Warum:** Rein in-process String-Parsing auf `DisableAllCommentRemover`. Keine
  Subprozess- oder Workspace-Verwendung im File-Grep.

### item-04: `IgnoreSuppressionsFilterTests` → Unit — `src/AiNetLinter.Tests/Suppression/IgnoreSuppressionsFilterTests.cs` (Klassen-Deklaration)

- **Was:** Über `public sealed class IgnoreSuppressionsFilterTests` eine Zeile
  `[Trait("Category", "Unit")]` einfügen.
- **Warum:** Reine Filter-Logik auf String-Listen, in-process.

### item-05: `SuppressionCommentParserTests` → Unit — `src/AiNetLinter.Tests/Suppression/SuppressionCommentParserTests.cs` (Klassen-Deklaration)

- **Was:** Über `public sealed class SuppressionCommentParserTests` eine Zeile
  `[Trait("Category", "Unit")]` einfügen.
- **Warum:** Parser-Tests, rein in-process String-Analyse. Hinweis: enthält
  `[Theory]`-Methoden mit `[InlineData]` — der Trait gilt für die ganze Methode
  (nicht pro InlineData-Eintrag), das ist korrekt.

### item-06: `SuppressionEvaluatorTests` → Unit — `src/AiNetLinter.Tests/Suppression/SuppressionEvaluatorTests.cs` (Klassen-Deklaration)

- **Was:** Über `public sealed class SuppressionEvaluatorTests` eine Zeile
  `[Trait("Category", "Unit")]` einfügen.
- **Warum:** In-process Evaluator-Logik.

### item-07: `SuppressionScannerTests` → Unit — `src/AiNetLinter.Tests/Suppression/SuppressionScannerTests.cs` (Klassen-Deklaration)

- **Was:** Über `public sealed class SuppressionScannerTests` eine Zeile
  `[Trait("Category", "Unit")]` einfügen.
- **Warum:** Scanner-Logik, in-process.

### item-08: `ViolationPathResolverTests` → Unit — `src/AiNetLinter.Tests/Suppression/ViolationPathResolverTests.cs` (Klassen-Deklaration)

- **Was:** Über `public sealed class ViolationPathResolverTests` eine Zeile
  `[Trait("Category", "Unit")]` einfügen.
- **Warum:** Path-Resolver, rein in-process.

## Tests

Keine — die Klassifikation ist rein additiv (Attribut setzen). Existierende Tests
müssen **unverändert** grün bleiben. Validierung erfolgt über den vollen
`dotnet test`-Lauf in der Definition of Done (kein neuer Test, kein geänderter
Test).

## Definition of Done

- [ ] Alle 8 Items umgesetzt (je eine `[Trait("Category", ...)]`-Zeile über der
  jeweiligen Klassendeklaration)
- [ ] **Bestehende Traits respektiert:** keine vorhandenen Trait-Attribute
  überschrieben oder entfernt (Trifft im Batch nicht zu, aber als Plausibilitäts-
  Check zu verifizieren: nach dem Diff sollten in `Suppression/` 8 Klassen mit
  Trait-Attribut existieren, 0 ohne.)
- [ ] **Build-Command** aus Tech-Stack-Notiz (`roadmap.md`) grün: `dotnet build`
- [ ] **Test-Command** aus Tech-Stack-Notiz grün: `dotnet test` (voller Lauf, alle
  1325 Tests müssen weiterhin grün sein — keine Test-Logik wurde geändert)
- [ ] **Trait-Filter-Smoke-Test** (vom Coder zusätzlich durchführen, um die
  Klassifikation zu verifizieren):
  - `dotnet test --no-build --filter "Category=Unit"` → muss grün sein
  - `dotnet test --no-build --filter "Category=Integration"` → muss grün sein
  - Die Summe der gefilterten Test-Zahlen sollte der Gesamt-Test-Zahl
    entsprechen (oder, falls andere Klassen noch Traits auf Methoden-Ebene
    tragen, in `Suppression/` allein die korrekte Verteilung 7+1 zeigen).
    Numerische Begründung im `step-result.md` dokumentieren.
- [ ] **Self-Lint** (TD-001-konform, semantisch identisch zu
  `--self-lint`): `dotnet run --project src/AiNetLinter -- --config rules.json
  --path .` → muss `OK` ausgeben
- [ ] **Commit auf aktuellem Branch** (Conventional Commit auf Deutsch,
  imperativ, mit Task-Suffix `[flaky-and-test-performance]`):
  empfohlener Subject: `chore(tests): Category-Traits für Suppression-Tests
  nachziehen [flaky-and-test-performance]` (Subject-Länge mit Suffix prüfen —
  exakt 84 Zeichen, über der 72-Zeichen-Grenze; Variante mit kürzerem Verb:
  `chore(tests): Suppression-Tests mit Category-Traits versehen
  [flaky-and-test-performance]` → 79 Zeichen, immer noch zu lang; kürzeste
  akzeptable Variante: `test: Suppression-Tests Kategorie-taggen
  [flaky-and-test-performance]` → 63 Zeichen. Coder entscheidet finale Form,
  hält 72-Zeichen-Grenze ein, dokumentiert die Wahl.)
- [ ] `step-002/step-result.md` geschrieben mit: Diff-Statistik (Anzahl
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
  Trait-Attribute sind `[Trait("Category", "Unit")]` / `"Integration"`, exakt die
  im Projekt etablierte Schreibweise (Großbuchstabe am Wortanfang). Keine
  Warnung erwartet, da das exakt der bestehenden Konvention folgt (verifiziert per
  `grep` über die 19 bestehenden Integration-Klassen — alle nutzen diese
  Schreibweise).
- `.agents/rules/AiNetLinterRichtlinien.mdc` §4 "Commit-Vorschlag Pflicht" —
  betrifft die Coder-Antwort, ist im DoD-Punkt oben explizit referenziert.
- `.agents/rules/AiNetLinterRichtlinien.mdc` §5 "Symptom-Fixing verboten" —
  betrifft diesen Step nicht direkt, aber als Plausibilitäts-Check: wenn ein
  Test rot wird, ist die Ursache zu suchen, nicht der Test abzuschwächen.

## Bekannte Ausnahmen

- **Gibt es in diesem Batch keine.** Keine Tests in `Suppression/` sind flaky
  oder known-broken; keine bekannten Auslassungen in den
  Klassifikations-Kategorien.

## Code-Skizze (optional)

Vorher (Beispiel: `DisableAllCommentInjectorTests.cs`):

```csharp
public sealed class DisableAllCommentInjectorTests
{
    [Fact]
    public void PrependDisableAll_NoExistingDisable_AddsLine()
```

Nachher:

```csharp
[Trait("Category", "Unit")]
public sealed class DisableAllCommentInjectorTests
{
    [Fact]
    public void PrependDisableAll_NoExistingDisable_AddsLine()
```

Für `DisableAllCliTests.cs` (Integration-Beispiel, Achtung: hier ist die Zeile
Z. 8 direkt die Klassen-Deklaration ohne vorherige XML-Doc):

```csharp
public sealed class DisableAllCliTests
{
    [Fact]
    public async Task AddDisableAll_OnViolatingFixture_InjectOnlyIntoViolatingFiles()
```

wird zu:

```csharp
[Trait("Category", "Integration")]
public sealed class DisableAllCliTests
{
    [Fact]
    public async Task AddDisableAll_OnViolatingFixture_InjectOnlyIntoViolatingFiles()
```

## Notes

- **Batch-Umfang:** 8 Klassen × je 1 Trait-Zeile ≈ 8–10 Diff-Zeilen (zzgl. evtl.
  Leerzeilen, je nach lokaler Formatierung). Deutlich unter dem
  `max_batch_diff_lines: 40`-Deckel.
- **Schritt-Typ `low`-Risk-Begründung:** rein additives Attribut auf Klassen, das
  weder Build-Verhalten noch Test-Verhalten noch Parallelität ändert. Trait-Werte
  folgen exakt der bestehenden 86-Eintrag-Konvention (`Unit` / `Integration`,
  CamelCase-Großbuchstabe). Kein Eingriff in Produktionscode, keine Fixture-
  Änderung, keine Test-Logik-Änderung.
- **Folge-Batches (NICHT in diesem Step geplant):** Die EPIC-02-Arbeit umfasst
  ca. 160 verbleibende ungetaggte Testklassen. Vorschlag für die Reihenfolge der
  nächsten Step-Modus-Aufrufe (rein informativ — Planung der einzelnen Folge-Steps
  ist Sache der jeweiligen Planer-Aufrufe, nicht dieses Plans):
  1. **Reine-Unit-Ordner, klein** (einfachster Fall, Klassen-Trait durchgängig):
     - `Metrics/` (7 Klassen, alle Unit)
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
  3. **Mcp/Tools/** (17 Klassen, fast alle Unit, Mini-Fixture-Workspace → Unit) —
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
- **Gesamt-Fortschritt nach step-002:** 86 → 86+8 = 94 getaggte Klassen/Methoden
  (entspricht ca. 9 % der 168 Testklassen; bei den 1085 Methoden eher
  ~8 %, da die 86 Ist-Werte Klassen-Level-Traits sind, die viele Methoden
  abdecken). **Rest-Bestand nach step-002:** ca. 160 ungetaggte Klassen, ca. 990
  ungetaggte Methoden — EPIC-02 ist noch **weit** von "alle Tests getraggt"
  entfernt; die DoD wird über mehrere Folge-Steps erreicht. Dies ist **erwartet**
  und kein Planungsfehler — der User-Prompt hat diese Aufteilung in mehrere
  step-NNN explizit so verlangt.
- **Doku-Pflicht:** Nach Abschluss aller EPIC-02-Batches (nicht nach jedem
  Batch) muss `roadmap.md` aktualisiert werden, um den EPIC-02 als abgeschlossen
  zu markieren und die DoD-Punkte aus `konzept.md` §"Definition of Done" durch-
  zugehen. Diese Pflicht ist **nicht** Teil von step-002, sondern gehört in den
  letzten EPIC-02-Batch oder in den EPIC-08-Abschluss-Validierungs-Step.
