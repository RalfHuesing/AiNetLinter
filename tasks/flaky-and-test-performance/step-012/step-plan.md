---
status: done
type: step-plan
task: flaky-and-test-performance
step: 012               # flach, Task-weite Sequenz — auch Korrekturen liegen hier, nie in einem Unterordner
corrects: null             # <null | step-NNN> — nur gesetzt, wenn dieser Step eine Korrektur ist (treibt das Kettenbudget, siehe ../spec.md §10.5/§10.6)
title: "Category-Traits für Core-Rest (11 Klassen) und Maps/+Maps/Skeleton/ (6 Klassen) nachziehen (Mega-Batch 2/2 für Core+Maps)"
epic: EPIC-02          # Bezug zum Epic in roadmap.md, dem dieser Step zuarbeitet (bei corrects: vom korrigierten Step übernommen)
estimated_risk: low  # Einschätzung des Planers, siehe skills/planer/SKILL.md
step_type: batch  # single (Default) | batch — siehe ../spec.md §10.6. Bei batch: items-Liste unten füllen.
items:  # nur bei step_type: batch. Ein Eintrag pro gebündeltem Mini-Befund innerhalb des Epics (oder pro opportunistisch angehängtem auto_fixable-Tech-Debt, siehe ../spec.md §9.1/§10.6):
  - id: item-01
    title: "LinterEngineTests → Unit (in-process LinterAnalyzer/LinterEngine mit AdhocWorkspace + TestHelper; 10 [Fact], classLine=9; Standard-Insert; **SPEZIALFALL: 2. Klasse** `public class HighlyRelevantServiceTests` auf Z. 269 ohne [Fact]/[Theory] — Heuristik-Punkt 6 (Helper-Klassen ohne Testmethoden sind keine Testklassen), wird NICHT getaggt; kein BOM, CRLF+TrNL, **#nullable enable FEHLT** (Z. 1 = `using Xunit;`) — Trait-Insertion darf die Direktive nicht hinzufügen, out of scope)"
    source: "konzept.md §Wie Schritt 2, §Muss-Haven Traits-Punkt; Heuristik-Punkt 6 aus step-006"
  - id: item-02
    title: "NamespaceFilterTests → Unit (in-process NamespaceFilter mit Filter-Logik; 2 [Fact], classLine=8; Standard-Insert; kein BOM, CRLF+TrNL, **#nullable enable FEHLT**; kleinste Core/-Datei im Batch 1223 Bytes)"
    source: "konzept.md §Wie Schritt 2"
  - id: item-03
    title: "NullCoalescingInitializerClassifierTests → Unit (in-process Null-Coalescing-Initializer-Classifier; 6 [Fact], classLine=14; Standard-Insert; **BOM-tragend** — Byte-Scan vorher/nachher, CRLF+TrNL, #nullable enable Z.1)"
    source: "konzept.md §Wie Schritt 2"
  - id: item-04
    title: "PlaybookGeneratorRound2Tests → Unit (in-process RepoPlaybookGenerator.BuildContentAsync + PlaybookSyntaxWalker; 8 [Fact], classLine=22; **XML-Doc-Variante (3-Schichten mit 2-fach // @covers):** // @covers Z.15-16, Leerzeile Z.17, /// <summary>…</summary> Z.18-21, public sealed class Z.22 — Trait zwischen Z.21 (`</summary>`) und Z.22 (class), class verschiebt sich auf Z.23 — **Variante 1b der XML-Doc-Bibliothek** (item-13/18 in step-011 hatten Variante 1a mit 1-fach // @covers; Mechanik identisch); kein BOM, CRLF+TrNL, #nullable enable Z.1)"
    source: "konzept.md §Wie Schritt 2; XML-Doc-Variante analog step-009/011"
  - id: item-05
    title: "ResultPatternNamespaceTests → Unit (in-process Result-Pattern-Namespace-Detection; 6 [Fact], classLine=10; Standard-Insert; **BOM-tragend** — Byte-Scan vorher/nachher, CRLF+TrNL, **#nullable enable FEHLT**)"
    source: "konzept.md §Wie Schritt 2"
  - id: item-06
    title: "RuleRegistryTests → Unit (in-process RuleRegistry; 10 [Fact], classLine=13; Standard-Insert; kein BOM, CRLF+TrNL, #nullable enable Z.1)"
    source: "konzept.md §Wie Schritt 2"
  - id: item-07
    title: "ScopeImmutabilityTests → Unit (in-process Scope-Immutability-Check; 7 [Fact], classLine=10; Standard-Insert; **BOM-tragend** — Byte-Scan vorher/nachher, CRLF+TrNL, **#nullable enable FEHLT**)"
    source: "konzept.md §Wie Schritt 2"
  - id: item-08
    title: "StaticTestSentinelExemptionTests → Unit (in-process Static-Test-Sentinel-Exemption-Logik; 9 [Fact], classLine=13; Standard-Insert; **BOM-tragend** — Byte-Scan vorher/nachher, CRLF+TrNL, #nullable enable Z.1 — 2. größte Datei im Batch 11301 Bytes)"
    source: "konzept.md §Wie Schritt 2"
  - id: item-09
    title: "TestCoverageResolverTests → Unit (in-process TestCoverageResolver; 3 [Fact], classLine=6; Standard-Insert; kein BOM, CRLF+TrNL, **#nullable enable FEHLT** — kleinste Core/-Datei im Batch 1378 Bytes, namespace und class sehr nahe beieinander)"
    source: "konzept.md §Wie Schritt 2"
  - id: item-10
    title: "TestProjectDetectorSuffixTests → Unit (in-process TestProjectDetector.IsTestProject mit AdhocWorkspace; 3 [Fact] + 2 [Theory]×(7+4)=11 [InlineData] = **14 Test-Cases zur Laufzeit**; classLine=8; Standard-Insert; kein BOM, CRLF+TrNL, **#nullable enable FEHLT** — **Spezialfall:** Diskrepanz Methoden-vs-Test-Cases = +9, ausschließlich aus dieser einen Klasse; analog step-007 `OutputRootResolverTests` und step-008 `RuleLegendRegistryTests` — Klassen-Trait erfasst alle 14 Cases via xUnit-Vererbung)"
    source: "konzept.md §Wie Schritt 2; Code-Verifikation Planer-Schritt-2 (Z.22-34 Theory#1 mit 7 [InlineData], Z.36-45 Theory#2 mit 4 [InlineData])"
  - id: item-11
    title: "ViolationDescriptionTests → Unit (in-process ViolationDescription; 1 [Fact], classLine=9; Standard-Insert; kein BOM, CRLF+TrNL, #nullable enable Z.1 — **kleinste Datei im Batch 804 Bytes**)"
    source: "konzept.md §Wie Schritt 2"
  - id: item-12
    title: "HotspotMapBuilderTests → Unit (in-process HotspotMapBuilder mit AdhocWorkspace; 3 [Fact], classLine=12; Standard-Insert; **SPEZIALFALL: : IDisposable** in Klassensignatur, Interface-Deklaration bleibt unverändert; kein BOM, CRLF+TrNL, #nullable enable Z.1)"
    source: "konzept.md §Wie Schritt 2; Code-Verifikation Planer-Schritt-2"
  - id: item-13
    title: "SkeletonMapBuilderTests → Unit (in-process SkeletonMapBuilder.BuildAsync; 2 [Fact], classLine=12; Standard-Insert; kein BOM, CRLF+TrNL, #nullable enable Z.1)"
    source: "konzept.md §Wie Schritt 2"
  - id: item-14
    title: "SkeletonStableIdTests → Unit (in-process SkeletonMapBuilder.ExtractFromDocumentAsync mit IClassFixture<SymbolGraphCatalogFixture>; 1 [Fact], classLine=11; Standard-Insert; **SPEZIALFALL: IClassFixture<SymbolGraphCatalogFixture>** = Unit per Heuristik-Punkt-2-Negativ-Abgrenzung aus step-002 (`IClassFixture<SymbolGraphCatalogFixture>` / `IClassFixture<BaselineCatalogFixture>` — in-process, laden Mini-Solution, KEIN Subprozess); **WICHTIG: LF-only-Datei (CR=0, LF=42)** — **TD-003-Analogon für Maps/-Ordner** (analog `McpLintConsoleTests.cs` LF-only in `Output/`); Coder MUSS Python-Helper analog step-007 verwenden (byte-genaues Einfügen, sonst CRLF/LF-Mismatch beim `git status`); kein BOM, TrNL=Y, **#nullable enable FEHLT**; kleinste Maps/-Datei 1645 Bytes)"
    source: "konzept.md §Wie Schritt 2; Heuristik-Punkt-2-Negativ-Abgrenzung aus step-002 Z.147-150; TD-003-Analogon"
  - id: item-15
    title: "SkeletonSyntaxWalkerTests → Unit (in-process SkeletonSyntaxWalker; 11 [Fact], classLine=10; Standard-Insert; kein BOM, CRLF+TrNL, #nullable enable Z.1 — **2. größte Maps/-Datei im Batch 7065 Bytes**)"
    source: "konzept.md §Wie Schritt 2"
  - id: item-16
    title: "StructureMapBuilderTests → Unit (in-process StructureMapBuilder; 4 [Fact], classLine=12; Standard-Insert; **SPEZIALFALL: : IDisposable** in Klassensignatur, Interface-Deklaration bleibt unverändert; kein BOM, CRLF+TrNL, #nullable enable Z.1)"
    source: "konzept.md §Wie Schritt 2; Code-Verifikation Planer-Schritt-2"
  - id: item-17
    title: "VocabularyMapBuilderTests → Unit (in-process VocabularyMapBuilder; 5 [Fact], classLine=12; Standard-Insert; **SPEZIALFALL: : IDisposable** in Klassensignatur, Interface-Deklaration bleibt unverändert; kein BOM, CRLF+TrNL, #nullable enable Z.1)"
    source: "konzept.md §Wie Schritt 2; Code-Verifikation Planer-Schritt-2"
created_by: planer  # planer | orchestrator (nur bei mechanischem Korrektur-Transkript ohne Ermessen, siehe ../spec.md §6.2.1)
created_by_model: MiniMax-M3
created_by_model_knowledge_cutoff: 2026-01
created_at: 2026-08-08T10:00:00+02:00
related_to: []  # Pointer auf andere step-NNN (Task-interne Abhängigkeiten) oder auf step-review.md (Fix-Modus) — nie Fakten cachen, nur verweisen. Siehe ../spec.md §10.6. Nicht zu verwechseln mit `corrects` oben (eigene, budget-relevante Semantik).
---

# Step 012: Category-Traits für Core-Rest und Maps/+Maps/Skeleton/ nachziehen (Mega-Batch)

## Bezug

- **Task:** `flaky-and-test-performance`
- **Epic:** `EPIC-02` aus `roadmap.md` — Category-Traits flächendeckend
  nachziehen. **Elfter Batch** dieses Epics; **zweiter Mega-Batch** nach
  step-011 (der erste Mega-Batch deckte `Core/Checkers/`-Rest + erste
  8 `Core/`-Klassen ab). Dieser Plan konsolidiert den **`Core/`-Rest**
  (11 Klassen `LinterEngineTests`–`ViolationDescriptionTests`) **plus**
  den **`Maps/` + `Maps/Skeleton/`-Ordner** (6 Klassen) in **einem**
  Step — beide laut codemap homogen Unit.
- **Konzept-Referenz:** `konzept.md` §"Wie" Schritt 2 ("Category-Traits
  nachziehen — alle ~1000 ungetraggten Tests einordnen"), §"Muss-Haven"
  Traits-Punkt ("konsequente Category-Traits ... auf **allen** Tests —
  aktuell nur 86 von ~1087"), §"Definition of Done" Punkt "Alle Tests
  tragen einen Category-Trait".
- **Vorgänger-Steps:** `step-001` (EPIC-01, approved, Spike-Befund
  negativ), `step-002` (EPIC-02 Batch 1, Suppression, 8 Klassen,
  approved), `step-003` (EPIC-02 Batch 2, Metrics, 7 Klassen, approved),
  `step-004` (EPIC-02 Batch 3, Web, 5 Klassen, approved),
  `step-005` (EPIC-02 Batch 4, Arch/Diag/FalsePositives/Cache,
  7 Klassen, approved), `step-006` (EPIC-02 Batch 5, Evals, 3 Klassen,
  approved), `step-007` (EPIC-02 Batch 6, Output Teil 1/2, 5 Klassen
  D–O, approved), `step-008` (EPIC-02 Batch 7, Output Teil 2/2,
  4 Klassen P–V, approved), `step-009` (EPIC-02 Batch 8, Configuration,
  8 Klassen, approved, Commits `b484627`/`b4a8c59`),
  `step-010` (EPIC-02 Batch 9, `Core/Checkers/` Teil 1/3,
  8 Klassen A–`MethodParameterCountAccessibility`, approved,
  Commit `44956b7`), **`step-011` (EPIC-02 Batch 10, `Core/Checkers/`-
  Rest 12 Klassen + erste 8 `Core/`-Klassen, **20 Klassen total**,
  alle Unit, Mega-Batch, approved, Commits `bb39619`+`daad777`)**. Die
  zehn vorherigen Batches lieferten die etablierte
  Klassifikations-Heuristik (Subprozess-Marker = Integration; sonst
  Unit, mit `IClassFixture<SymbolGraphCatalogFixture>` explizit als
  Unit-Negativ-Abgrenzung in der Heuristik), die Trait-Syntax-Konvention
  (`[Trait("Category", "Unit")]`, CamelCase-Großbuchstabe), die
  Trait-Platzierungs-Bibliothek (Standard-Insert,
  `// @covers`-Block-Insert, XML-Doc-Variante mit 1-fach oder 2-fach
  `// @covers`, additive method-level Traits, `IDisposable`-Spezialfall),
  die Heuristik-Punkte 1–8 (Klassen-Homogenität → Klassen-Trait;
  bestehende Traits respektieren/additiv ergänzen; `null!` als
  Edge-Input; Klassen-Trait additiv zu bestehenden method-level Traits
  bei homogenen Klassen; Hypothesen-Auflösungs-Pflicht für offene
  "möglicherweise…"-Annotationen in der CodeMap; **Helper-Klassen ohne
  Testmethoden sind keine Testklassen**; **BOM-Inhomogenität als TD-005
  elevated**; **BOM-Inhomogenität als TD-006 elevated**),
  die String-Literal-`[Fact]`-Ausschluss-Methodik (NITPICK-Linie aus
  step-009), die `#nullable enable`-Disziplin (Trait-Insertion **darf**
  die Direktive **nicht** hinzufügen, out of scope; analog `LinterAnalyzerTests.cs`
  step-011), und die DoD-Struktur (Build grün, Voll-Test grün,
  Unit-Filter grün, Integration-Filter best-effort, Self-Lint `OK`,
  numerische Plausibilitätsprüfung mit
  String-Literal-`[Fact]`-Ausschluss-Methodik, konkreter
  Subject-Vorschlag mit exakter Längen-Angabe).
- **Schnitt-Entscheidung (17 Klassen in 1 Batch, deutlich unter
  `max_batch_items: 20`):** der ursprüngliche Plan aus step-002
  §"Notes" sah `Maps/` als eigenen 1-Batch (6 Klassen) **und**
  `Core/`-Rest als eigenen 1-Batch (11 Klassen) in zwei separaten
  Steps vor. Mit dem gelockerten 20-Item-Deckel aus `config.md`
  (2026-08-08) faltet sich der Plan wie folgt:
  - **step-012 (= dieser Plan, Mega-Batch 2/2 für Core+Maps):**
    11 verbleibende `Core/` (`LinterEngineTests`–`ViolationDescriptionTests`)
    + 6 `Maps/`+`Maps/Skeleton/` (`HotspotMapBuilderTests`–`VocabularyMapBuilderTests`)
    = **17 Klassen total**, deutlich unter dem 20-Item-Deckel (3
    Reserve-Items). Spart einen kompletten Planer-Coder-Kritiker-Zyklus.
  - **Was nicht in step-012 enthalten ist und Folge-Steps braucht:**
    `Mcp/Tools/` (17 Klassen, 2–3 Batches), `Mcp/` (19 Klassen,
    gemischt — Subprozess-Anteil), `Baseline/` (10 Klassen, gemischt),
    `Commands/` (17 Klassen, stark gemischt — pro-Methode-Tagging für
    `McpServerCommandTests` als eigener Step nötig), `Cli/`
    (6 Klassen, gemischt).
- **Schnitt-Wahl-Begründung (17 statt 11 oder 6):**
  - **Warum 17 (Orchestrator-Option A) statt 11 (Option B):** die
    6 `Maps/`-Klassen sind technisch identisch zu den 11 `Core/`-Klassen
    (gleiche Subprozess-Marker-Lage, gleiche Standard-Insert-Mechanik,
    gleiche BOM-Verteilung 24 %, gleiche CRLF/TrNL-Lage für 16/17
    Dateien), nur **eine** `Maps/`-Datei (`SkeletonStableIdTests.cs`)
    weicht als LF-only-Datei von der CRLF-Mehrheit ab — das wäre
    ein eigener Step mit nur 6 Klassen + 1 LF-only-Datei
    = reiner Overhead (1 zusätzlicher Planer-Coder-Kritiker-Zyklus
    ohne technischen Mehrwert). Der Orchestrator hat explizit
    "bündle größer" gefordert.
  - **Warum 17 (Option A) statt 16 (Orchestrator-Option C mit
    `Mcp/Tools/`-Anteil):** `Mcp/Tools/` ist laut codemap "fast alle
    Unit über Mini-Fixture-Workspaces" und somit ein **anderer**
    Verzeichnis-Mix. Eine Mischung von `Core/`+`Maps/`+`Mcp/Tools/`
    verletzt die etablierte "1 Batch = thematisch zusammenhängende
    Ordner"-Linie (step-002..011). Zudem ist die
    `Maps/`-Trait-Sortierung thematisch eng verwandt mit `Core/`
    (beide Test-Hauptordner, beide in-process-Logik).
  - **Anti-Loop-Check** gegen `codemap.md` (Stand step-011-Doku-Commit):
    die `Core/`-Zeile in der Sektion "Test-Verzeichnisse — geplant für
    EPIC-02-Folge-Batches" trägt den Vermerk "step-011 nimmt erste 8
    Klassen (A–`LinterEngineCacheTests`) als Mega-Batch-Anteil;
    step-012 verbleibend mit 11 Klassen (`LinterEngineTests`–
    `ViolationDescriptionTests`)" — passt exakt. Die
    `Maps/`+`Maps/Skeleton/`-Zeile trägt den Vermerk "6 Klassen; rein
    Unit, dito (zuletzt: step-002)" — passt. **Keine** bestehende
    Entscheidung widerspricht diesem Plan. Der Coder aktualisiert
    die `Core/`-Zeile im Doku-Commit auf "step-011 + step-012 = 19/19
    abgeschlossen, Ordner vollständig abgehakt" und die
    `Maps/`+`Maps/Skeleton/`-Zeile auf "6/6 abgeschlossen in step-012,
    Ordner vollständig abgehakt".

## Aktueller Projektzustand (JIT-Kontext)

Beim Lesen der 17 Zieldateien + Inventur des `Core/`- und `Maps/`-Ordners
vorgefunden (relevant für step-012):

- **Ordner-Inventar `Core/` (19 `.cs`-Dateien, davon 8 in step-011
  getaggt, 11 verbleibend für step-012):**
  - **In step-011 getaggt (8, bereits done — nicht erneut):**
    `AutoFixerTests`, `ClassInfoCollectorTests`,
    `CompoundSuppressionEvaluatorTests`,
    `CompoundSuppressionIntegrationTests`, `ControlFlowResilienceTests`,
    `DiffImpactAnalyzerTests`, `LinterAnalyzerTests`,
    `LinterEngineCacheTests`.
  - **In step-012 (11 Klassen, dieser Plan — vervollständigt `Core/`-
    Ordner auf 19/19 = 100 %):**
    `LinterEngineTests`, `NamespaceFilterTests`,
    `NullCoalescingInitializerClassifierTests`, `PlaybookGeneratorRound2Tests`,
    `ResultPatternNamespaceTests`, `RuleRegistryTests`,
    `ScopeImmutabilityTests`, `StaticTestSentinelExemptionTests`,
    `TestCoverageResolverTests`, `TestProjectDetectorSuffixTests`,
    `ViolationDescriptionTests`.
- **Ordner-Inventar `Maps/` + `Maps/Skeleton/` (6 `.cs`-Dateien, alle
  6 ungetaggt, alle 6 homogen Unit):**
  - **In `Maps/` direkt (3 Klassen):** `HotspotMapBuilderTests`,
    `StructureMapBuilderTests`, `VocabularyMapBuilderTests` — alle 3 mit
    `: IDisposable` in der Klassensignatur (analog step-011
    `UiFileSeparationCheckerTests` und `LinterEngineCacheTests`).
  - **In `Maps/Skeleton/` (3 Klassen):** `SkeletonMapBuilderTests`,
    `SkeletonStableIdTests`, `SkeletonSyntaxWalkerTests` —
    `SkeletonStableIdTests` mit `IClassFixture<SymbolGraphCatalogFixture>`
    (Unit per Heuristik-Punkt-2-Negativ-Abgrenzung, **NICHT** per
    `IClassFixture<McpLiveRepositoryFixture>`-Heuristik), und
    **LF-only** (CR=0, LF=42) als `Maps/`-TD-003-Analogon.
- **step-012-Klassen — Detail-Inventar (17 Klassen, alle homogen
  Unit):**

  **11 `Core/`-Klassen L–V:**

  | Datei                                       | classLine | Facts | Theory | InlineData | BOM  | Nullable | EOL  | TrNL | Test-Cases |
  |---------------------------------------------|----------:|------:|-------:|-----------:|:----:|:--------:|:----:|:----:|-----------:|
  | `LinterEngineTests.cs`                      |         9 |   10 |      0 |          0 |  ✗   |    ✗     | CRLF |  ✓   |         10 |
  | `NamespaceFilterTests.cs`                   |         8 |    2 |      0 |          0 |  ✗   |    ✗     | CRLF |  ✓   |          2 |
  | `NullCoalescingInitializerClassifierTests.cs` |      14 |    6 |      0 |          0 |  ✓   |    ✓     | CRLF |  ✓   |          6 |
  | `PlaybookGeneratorRound2Tests.cs`           |        22 |    8 |      0 |          0 |  ✗   |    ✓     | CRLF |  ✓   |          8 |
  | `ResultPatternNamespaceTests.cs`            |        10 |    6 |      0 |          0 |  ✓   |    ✗     | CRLF |  ✓   |          6 |
  | `RuleRegistryTests.cs`                      |        13 |   10 |      0 |          0 |  ✗   |    ✓     | CRLF |  ✓   |         10 |
  | `ScopeImmutabilityTests.cs`                 |        10 |    7 |      0 |          0 |  ✓   |    ✗     | CRLF |  ✓   |          7 |
  | `StaticTestSentinelExemptionTests.cs`       |        13 |    9 |      0 |          0 |  ✓   |    ✓     | CRLF |  ✓   |          9 |
  | `TestCoverageResolverTests.cs`              |         6 |    3 |      0 |          0 |  ✗   |    ✗     | CRLF |  ✓   |          3 |
  | `TestProjectDetectorSuffixTests.cs`         |         8 |    3 |      2 |         11 |  ✗   |    ✗     | CRLF |  ✓   |     **14** |
  | `ViolationDescriptionTests.cs`              |         9 |    1 |      0 |          0 |  ✗   |    ✓     | CRLF |  ✓   |          1 |
  | **Summe Facts**                             |           |   65 |      2 |         11 |      |          |      |      |     **76** |

  **6 `Maps/`+`Maps/Skeleton/`-Klassen H–V:**

  | Datei                                       | classLine | Facts | Theory | InlineData | BOM  | Nullable | EOL  | TrNL | Test-Cases |
  |---------------------------------------------|----------:|------:|-------:|-----------:|:----:|:--------:|:----:|:----:|-----------:|
  | `HotspotMapBuilderTests.cs`                 |        12 |    3 |      0 |          0 |  ✗   |    ✓     | CRLF |  ✓   |          3 |
  | `Skeleton/SkeletonMapBuilderTests.cs`       |        12 |    2 |      0 |          0 |  ✗   |    ✓     | CRLF |  ✓   |          2 |
  | `Skeleton/SkeletonStableIdTests.cs`         |        11 |    1 |      0 |          0 |  ✗   |    ✗     | **LF** |  ✓   |          1 |
  | `Skeleton/SkeletonSyntaxWalkerTests.cs`     |        10 |   11 |      0 |          0 |  ✗   |    ✓     | CRLF |  ✓   |         11 |
  | `StructureMapBuilderTests.cs`               |        12 |    4 |      0 |          0 |  ✗   |    ✓     | CRLF |  ✓   |          4 |
  | `VocabularyMapBuilderTests.cs`              |        12 |    5 |      0 |          0 |  ✗   |    ✓     | CRLF |  ✓   |          5 |
  | **Summe Facts**                             |           |   26 |      0 |          0 |      |          |      |      |     **26** |

  **Gesamt: 91 Facts + 2 Theory + 11 InlineData = 93 Methoden,
  102 Test-Cases** in 17 Klassen (91 + 11 = 102 Test-Cases).

  **Beobachtungen (gemäß etablierter Heuristik):**
  - **Alle 17 Klassen homogen Unit** — Subprozess-Marker-Verifikation
    (regex-basiert pro Datei für
    `McpTestClient\.ConnectAsync|CliProcessRunner\.RunLinterAsync|CliProcessRunner\.RunAsync|Program\.Main|IClassFixture<McpLiveRepositoryFixture>|Process\.Start|SubprocessConcurrencyGate`):
    **0/0/0/0/0/0/0** über alle 17 Dateien. Damit überschreitet die
    Subprozess-Marker-Heuristik (Punkt 2 aus step-002) etwaige
    Namens-Heuristik-Hinweise (z. B. "`TestProjectDetectorSuffixTests`"
    klingt nicht nach Subprozess; "`SkeletonStableIdTests`" hat
    `IClassFixture<SymbolGraphCatalogFixture>`, was per
    Negativ-Abgrenzung **Unit** ist).
  - **`LinterEngineTests.cs` Helper-Klassen-Spezialfall
    (Heuristik-Punkt 6):** Datei enthält **2 Klassen** — Test-Klasse
    `LinterEngineTests` auf Z. 9 (Test-Klasse, 10 Facts) + Helper-Klasse
    `public class HighlyRelevantServiceTests` auf Z. 269 (kein
    `[Fact]`/`[Theory]`, interne Test-Fixture für den LinterEngine-
    Workflow). **Wir taggen nur die Test-Klasse `LinterEngineTests`
    auf Z. 9** — die Helper-Klasse bleibt ohne Trait (Heuristik-Punkt
    6: "Helper-Klassen ohne Testmethoden sind keine Testklassen").
    Standard-Insert funktioniert (Trait vor `public sealed class
    LinterEngineTests`, Helper-Klasse auf Z. 269 bleibt unangetastet).
  - **`PlaybookGeneratorRound2Tests.cs` XML-Doc-Variante
    (Variante 1b der Bibliothek, item-04):** Datei hat
    **3-Schichten-Struktur mit 2-fach `// @covers`** — Z. 15-16
    `// @covers RepoPlaybookGenerator` + `// @covers PlaybookSyntaxWalker`,
    Z. 17 Leerzeile, Z. 18-21 `/// <summary>…</summary>`, Z. 22
    `public sealed class PlaybookGeneratorRound2Tests`. Trait wird
    zwischen Z. 21 (`</summary>`) und Z. 22 (class) eingefügt, class
    verschiebt sich auf Z. 23. **Mechanik identisch zu Variante 1a
    (item-13/18 in step-011 hatten 1-fach `// @covers`)** — kein
    Eingriff in die `// @covers`-Zeilen, kein Eingriff in die
    XML-Doc-Zeilen. **Erweiterung der XML-Doc-Bibliothek:** Variante
    1b (2-fach `// @covers`) wird in step-012 etabliert und in
    Folge-Steps wiederverwendet.
  - **`TestProjectDetectorSuffixTests.cs` Theory-Spezialfall
    (item-10):** 2 `[Theory]`-Methoden (Z. 22-34 mit 7 `[InlineData]`,
    Z. 36-45 mit 4 `[InlineData]`) + 3 `[Fact]`-Methoden = **5
    Methoden, 14 Test-Cases zur Laufzeit** (Diskrepanz Methoden-vs-
    Test-Cases = +9, ausschließlich aus dieser einen Klasse). Der
    Klassen-Trait erfasst alle 14 Cases via xUnit-Vererbung (analog
    step-007 `OutputRootResolverTests`, step-008 `RuleLegendRegistryTests`,
    step-011 `UiFileSeparationCheckerTests`). Coder dokumentiert
    **beide** Zahlen (regex-basierte Methoden-Zählung pro Datei UND
    tatsächlicher `dotnet test --filter "Category=Unit"`-Lauf-Wert)
    und gleicht sie gegen die Planer-Prognose 14 ab.
  - **3 `Maps/`-Klassen mit `: IDisposable` (items-12/16/17):**
    `HotspotMapBuilderTests`, `StructureMapBuilderTests`,
    `VocabularyMapBuilderTests` — alle 3 mit
    `public sealed class XTests : IDisposable` in der Klassensignatur.
    Standard-Insert funktioniert unverändert (Trait wird vor
    `public sealed class X`-Deklaration eingefügt, Interface-
    Deklaration ist Teil der class-Signatur und bleibt unangetastet —
    analog step-011 `UiFileSeparationCheckerTests:14` und
    `LinterEngineCacheTests:17`).
  - **`SkeletonStableIdTests.cs` Spezialfälle-Häufung (item-14):**
    3 kombinierte Spezialfälle in **einer** Datei:
    - **LF-only-EOL (CR=0, LF=42, 1645 Bytes):** `Maps/`-TD-003-
      Analogon (analog `McpLintConsoleTests.cs` LF-only in `Output/`).
      Coder **muss** Python-Helper analog step-007
      (`McpLintConsoleTests.cs`-LF-only) verwenden — Standard-Edit
      könnte die Datei auf CRLF umstellen und damit einen
      `git status`-Hinweis und ein `git diff` über die gesamte Datei
      auslösen (EOL-Wechsel ist eine inhaltliche Änderung, die nicht
      im Step-Scope ist). BOM=N, TrNL=Y (letztes Byte = LF, aber
      kein CR davor).
    - **`IClassFixture<SymbolGraphCatalogFixture>` (Z. 11) = Unit:**
      Heuristik-Punkt-2-Negativ-Abgrenzung aus step-002 Z. 147-150
      (`IClassFixture<SymbolGraphCatalogFixture>` / `IClassFixture<
      BaselineCatalogFixture>` — in-process, laden Mini-Solution).
      `SymbolGraphCatalogFixture` ist laut codemap §75–79
      ("Test-Fixtures — im Plan-Scope, noch nicht umgestellt") "nur
      1× verwendet ... in Mini-Solution + `SourceFileCatalog.LoadAsync`,
      in-process". **Subprozess-Marker-Detailcheck** (0 Treffer pro
      Marker-Kategorie):
      `McpTestClient\.ConnectAsync`=0, `CliProcessRunner\.RunLinterAsync`=0,
      `CliProcessRunner\.RunAsync`=0, `Program\.Main`=0,
      `IClassFixture<McpLiveRepositoryFixture>`=0, `Process\.Start`=0,
      `SubprocessConcurrencyGate`=0. **Klassifikation: Unit.**
    - **`#nullable enable` FEHLT (Z. 1 = `using System.Linq;`):**
      Trait-Insertion **darf** die Direktive **nicht** hinzufügen
      (analog `LinterAnalyzerTests.cs` step-011, item-19 — out of
      scope, würde Datei-Regel verändern). Wahrscheinlich hebt das
      `AiNetLinter.Tests`-Profil `EnforceNullableEnable` analog zu
      `EnforceSealedClasses` auf (siehe `AiNetLinter.mdc:83`);
      `dotnet build` läuft grün ohne Warnung.
- **BOM-Verteilung 4/17 mit BOM (24 %), 13/17 ohne (76 %):** die
  step-010/011-Hypothese "30 % mit BOM" bestätigt sich im 17-Klassen-
  Batch (4/17 = 24 %). **MIT BOM:** `NullCoalescingInitializerClassifierTests`,
  `ResultPatternNamespaceTests`, `ScopeImmutabilityTests`,
  `StaticTestSentinelExemptionTests` (4 aus `Core/`). **OHNE BOM:**
  7 `Core/`-Dateien + 6 `Maps/`-Dateien. **Heuristik-Punkt 8 (TD-006)
  bleibt offen** — Konsolidierung out of scope step-012 (kein
  `auto_fixable`-Anhängen, kein TD-Eintrag durch Planer).
- **EOL-Inhomogenität: 1/17 LF-only (`SkeletonStableIdTests.cs`,
  5.9 %), 16/17 uniform CRLF.** TD-003 (LF-only `McpLintConsoleTests.cs`
  in `Output/`) betrifft `Maps/` analog — `SkeletonStableIdTests.cs`
  ist die einzige `Maps/`-Datei mit `CR=0, LF=42`. **Coder MUSS
  Python-Helper analog step-007 verwenden** (byte-genaues Einfügen
  der Trait-Zeile ohne EOL-Änderung). Die anderen 16 Dateien
  uniform CRLF — Standard-Edit-Tool reicht.
- **Trailing-NL: alle 17 Dateien mit Trailing-NL** (letztes Byte =
  LF in allen 17 Dateien, `SkeletonStableIdTests.cs` hat kein
  vorausgehendes `0D` davor, aber das letzte Byte ist LF) —
  Standard-Edit-Tool bzw. Python-Helper reicht.
- **`#nullable enable` am Dateianfang: 10/17 mit, 7/17 ohne
  (41 %):** die step-011-Hypothese ("19/20 mit Direktive in
  `Core/Checkers/`+`Core/`-A-Teil") bestätigt sich im `Core/`-L–V-Teil
  nicht so klar (7/11 `Core/`-Dateien ohne Direktive = 64 % ohne,
  vs. 1/20 in step-011 = 5 % ohne). **Konsequenz für den Coder:**
  Trait-Insertion **darf** die Direktive **nicht** hinzufügen
  (out of scope, würde Datei-Regel verändern) — analog
  `LinterAnalyzerTests.cs` step-011. **Betroffen (alle ohne Direktive,
  7/17):** `LinterEngineTests.cs` (Z. 1 = `using Xunit;`),
  `NamespaceFilterTests.cs` (Z. 1 = `using Xunit;`),
  `ResultPatternNamespaceTests.cs` (Z. 1 = `using System;` — BOM
  davor, also `EF BB BF 75 73 69 6E 67`),
  `ScopeImmutabilityTests.cs` (Z. 1 = `using System;` — BOM davor),
  `TestCoverageResolverTests.cs` (Z. 1 = `using System;`),
  `TestProjectDetectorSuffixTests.cs` (Z. 1 = `using Microsoft.CodeAnalysis;`),
  `SkeletonStableIdTests.cs` (Z. 1 = `using System.Linq;`).
  `dotnet build` läuft grün (TreatWarningsAsErrors) — entweder
  hebt das `AiNetLinter.Tests`-Profil `EnforceNullableEnable` auf
  (analog TD-004-Beobachtung in `Output/`), oder Build ist tolerant
  gegenüber `*.Tests`-Dateien.
- **String-Literal-`[Fact]`-Vorkommen (NITPICK-Linie aus step-009
  NITPICK):** alle 17 Dateien per PowerShell-Substring-Scan geprüft
  (`.Contains('[Fact]')` und Zählen der Treffer, die gleichzeitig
  ein `"`-Zeichen in derselben Zeile haben): **0/17 Treffer** — keine
  Datei im step-012-Batch verschachtelt `[Fact]` in einem String-
  Literal. Damit ist die Methoden-Inventur (regex-basiert) **gleich**
  der Test-Case-Inventur-Brutto-Zahl (91) **plus** der
  `[InlineData]`-Expansionen (11) = 102 Test-Cases. **Kein**
  Mis-count analog step-009 `AgentFeaturesTests.cs:241` (16 → 15).
- **Bestehende Trait-Verteilung:** 0/17 Klassen mit bestehendem
  `[Trait(`-Attribut (regex-verifiziert per `Select-String`-Pattern
  `'[Trait(' -AllMatches -SimpleMatch` pro Datei = 0/0/0/…/0). Alle
  17 Klassen sind "jungfräulich" — keine Vorab-Klassifikation zu
  respektieren, keine method-level Traits additiv zu ergänzen.
  Reiner Klassen-Trait-Insert.
- **Trait-Platzierungs-Bibliothek vollständig ausreichend:**
  - **Standard-Insert zwischen `namespace …;` und `public sealed
    class …`** (16 Klassen): alle 11 `Core/` außer `PlaybookGeneratorRound2Tests`
    + 5 `Maps/`+`Maps/Skeleton/` außer `SkeletonStableIdTests` (der
    zwar Standard-Insert hat, aber byte-genau per Python-Helper
    wegen LF-only).
  - **XML-Doc-Variante zwischen `</summary>` und `public sealed
    class …`** (1 Klasse, **Variante 1b mit 2-fach `// @covers`**):
    `PlaybookGeneratorRound2Tests.cs:22` (Trait zwischen Z. 21
    `</summary>` und Z. 22 class, class → Z. 23). Etabliert in
    step-012 die Variante 1b der XML-Doc-Bibliothek.
  - **`// @covers`-Block-Insert: 0 Klassen** in diesem Batch
    (keine `// @covers`-Marker ohne gleichzeitig vorhandene
    XML-Doc — die `// @covers` in `PlaybookGeneratorRound2Tests`
    sind Teil der 3-Schichten-XML-Doc-Variante 1b).
  - **Hinweis `HotspotMapBuilderTests:12`, `StructureMapBuilderTests:12`,
    `VocabularyMapBuilderTests:12`:** die Klassen implementieren
    `: IDisposable` (zusätzlich zum Standard-Pattern). Standard-Insert
    funktioniert unverändert (Trait wird vor der `public sealed
    class …`-Deklaration eingefügt, Interface-Deklaration ist Teil
    der class-Signatur und bleibt unangetastet) — analog step-011
    `UiFileSeparationCheckerTests:14` und `LinterEngineCacheTests:17`.
  - **Hinweis `LinterEngineTests:9` vs. `LinterEngineTests:269`:** die
    Datei hat 2 Klassen — `LinterEngineTests` (Z. 9, Test-Klasse,
    wird getaggt) und `HighlyRelevantServiceTests` (Z. 269, Helper-
    Klasse ohne Testmethoden, wird **nicht** getaggt per
    Heuristik-Punkt 6).
- **Numerische Plausibilität (Plan-DoD-Verifikation):**
  - **Methoden-Inventar pro Datei (regex-basiert per
    `Select-String -Path ... -Pattern '[Fact]' -AllMatches -SimpleMatch`
    + analog für `[Theory]`):** 17 Klassen ergeben **91 Facts + 2
    Theories = 93 Methoden** (Items 01-11 `Core/`: 10+2+6+8+6+10+7+9+3+5+1
    = 65 Facts + 2 Theories; Items 12-17 `Maps/`: 3+2+1+11+4+5 = 26 Facts).
  - **Test-Case-Inventar pro Datei (regex-basiert, mit
    String-Literal-Ausschluss):** 17 Klassen ergeben **91 Facts +
    11 InlineData-Expansionen = 102 Test-Cases** (alle
    `[Theory]`+`[InlineData]`-Expansions manuell verifiziert:
    `TestProjectDetectorSuffixTests` Z. 22-34 Theory#1 mit 7
    InlineData, Z. 36-45 Theory#2 mit 4 InlineData = 11 InlineData).
  - **Diskrepanz Methoden (93) vs. Test-Cases (102) = +9** — kommt
    **ausschließlich** aus `TestProjectDetectorSuffixTests` (2
    Theories mit 11 InlineData = +9 Cases jenseits der 5 Methoden,
    zusätzlich zu den 3 Facts). Der Coder dokumentiert **beide**
    Zahlen (regex-basierte Methoden-Zählung pro Datei UND
    tatsächlicher `dotnet test --filter "Category=Unit"`-Lauf-Wert)
    und gleicht sie gegen die Planer-Prognose 102 ab. Bei
    abweichendem Delta ist **zwingend** die String-Literal-`[Fact]`-
    Methodik aus step-009 NITPICK anzuwenden (Brutto vs. Netto-Count,
    `[Fact]` in String-Literalen ausschließen).
  - **Filter-Delta step-012:** Unit steigt um **+102**, Integration
    unverändert (+0), Total unverändert (+0).
  - **Erwarteter Unit-Filter nach step-012:**
    882 (Stand nach step-011) + 102 = **984**.
  - **Integration bleibt 113, Total bleibt 1325.**
- **Klassen-Deklarationen — Trait-Platzierungs-Variante**
  (verifiziert per `grep -nE 'public sealed class|/// <summary>|
  // @covers'` über alle 17 Dateien):
  - **Standard-Insert zwischen `namespace …;` und
    `public sealed class …`** (16 Klassen — Details in der Item-
    Liste im Frontmatter; Klassen-Zeile verschiebt sich um +1 nach
    unten für alle 16 Dateien).
  - **XML-Doc-Variante zwischen `</summary>` und
    `public sealed class …`** (1 Klasse, **Variante 1b**):
    `PlaybookGeneratorRound2Tests.cs:22` (siehe Item 04 im Frontmatter
    für die exakte 3-Schichten-Position mit 2-fach `// @covers`).
- **EOL-/BOM-/Trailing-NL-Status** (verifiziert per PowerShell-Byte-
  Check über alle 17 step-012-Dateien):

  | Datei                                       | BOM  |    CR |    LF | TrNL | EOL       | Erste 3 Bytes          | Bytes  |
  |---------------------------------------------|:----:|------:|------:|:----:|-----------|------------------------|-------:|
  | `Core/LinterEngineTests.cs`                 |  ✗   |   333 |   333 |  ✓  | CRLF      | `75 73 69` (`using`)   | 11176 |
  | `Core/NamespaceFilterTests.cs`              |  ✗   |    30 |    30 |  ✓  | CRLF      | `75 73 69` (`using`)   |  1223 |
  | `Core/NullCoalescingInitializerClassifierTests.cs` | ✓ |  206 |  206 |  ✓  | CRLF      | `EF BB BF` (BOM)       |  7844 |
  | `Core/PlaybookGeneratorRound2Tests.cs`      |  ✗   |   229 |   229 |  ✓  | CRLF      | `23 6E 75` (`#nu`)     |  9389 |
  | `Core/ResultPatternNamespaceTests.cs`       |  ✓   |   192 |   192 |  ✓  | CRLF      | `EF BB BF` (BOM)       |  6188 |
  | `Core/RuleRegistryTests.cs`                 |  ✗   |   149 |   149 |  ✓  | CRLF      | `23 6E 75` (`#nu`)     |  5761 |
  | `Core/ScopeImmutabilityTests.cs`            |  ✓   |   209 |   209 |  ✓  | CRLF      | `EF BB BF` (BOM)       |  7801 |
  | `Core/StaticTestSentinelExemptionTests.cs`  |  ✓   |   313 |   313 |  ✓  | CRLF      | `EF BB BF` (BOM)       | 11301 |
  | `Core/TestCoverageResolverTests.cs`         |  ✗   |    46 |    46 |  ✓  | CRLF      | `75 73 69` (`using`)   |  1378 |
  | `Core/TestProjectDetectorSuffixTests.cs`    |  ✗   |    69 |    69 |  ✓  | CRLF      | `75 73 69` (`using`)   |  2242 |
  | `Core/ViolationDescriptionTests.cs`         |  ✗   |    26 |    26 |  ✓  | CRLF      | `23 6E 75` (`#nu`)     |   804 |
  | `Maps/HotspotMapBuilderTests.cs`            |  ✗   |    76 |    76 |  ✓  | CRLF      | `23 6E 75` (`#nu`)     |  2538 |
  | `Maps/Skeleton/SkeletonMapBuilderTests.cs`  |  ✗   |    61 |    61 |  ✓  | CRLF      | `23 6E 75` (`#nu`)     |  2146 |
  | `Maps/Skeleton/SkeletonStableIdTests.cs`    |  ✗   |     0 |    42 |  ✓  | **LF-only** | `75 73 69` (`using`) |  1645 |
  | `Maps/Skeleton/SkeletonSyntaxWalkerTests.cs`|  ✗   |   214 |   214 |  ✓  | CRLF      | `23 6E 75` (`#nu`)     |  7065 |
  | `Maps/StructureMapBuilderTests.cs`          |  ✗   |    86 |    86 |  ✓  | CRLF      | `23 6E 75` (`#nu`)     |  2811 |
  | `Maps/VocabularyMapBuilderTests.cs`         |  ✗   |    94 |    94 |  ✓  | CRLF      | `23 6E 75` (`#nu`)     |  3144 |

  **Beobachtungen:**
  - **EOL-Inhomogenität: 1 Datei** — `Maps/Skeleton/SkeletonStableIdTests.cs`
    ist **LF-only** (CR=0, LF=42, 1645 Bytes). **TD-003-Analogon
    für `Maps/`-Ordner** (analog `McpLintConsoleTests.cs` LF-only in
    `Output/`). Coder MUSS für diese eine Datei den **Python-Helper
    analog step-007** verwenden (byte-genaues Einfügen einer
    `\n`-Trait-Zeile ohne EOL-Änderung). Standard-Edit-Tool könnte
    die Datei auf CRLF umstellen — das wäre ein außer-Scope-EOL-
    Wechsel. **Konsequenz:** die 16 CRLF-Dateien mit Standard-Edit-
    Tool + 1 LF-only-Datei mit Python-Helper = 17 Dateien mit
    unterschiedlicher Edit-Mechanik. **Coder dokumentiert beide
    Wege** im `step-result.md` §"Geänderte Dateien" und §"EOL-
    Konservierungstabelle".
  - **Trailing-NL: alle 17 Dateien mit Trailing-NL** (letztes Byte
    = LF in allen 17 Dateien; bei `SkeletonStableIdTests.cs` ist
    das letzte Byte = `0A`, ohne vorausgehendes `0D`) — Standard-
    Edit-Tool bzw. Python-Helper reicht.
  - **BOM-Inhomogenität: 4 von 17 mit BOM (24 %), 13 ohne (76 %).**
    - **MIT BOM:** `NullCoalescingInitializerClassifierTests`,
      `ResultPatternNamespaceTests`, `ScopeImmutabilityTests`,
      `StaticTestSentinelExemptionTests` (4 aus `Core/`).
    - **OHNE BOM:** 7 `Core/`-Dateien + 6 `Maps/`-Dateien.
    - **Konsequenz für den Coder:** das Standard-Edit-Tool erhält
      die BOM in der Regel (Bytes vor und nach dem Edit sind
      identisch), aber der Coder **muss** für alle 4 BOM-tragenden
      Dateien explizit per `[System.IO.File]::ReadAllBytes(...)`-
      Scan **vor** und **nach** dem Edit verifizieren, dass die
      ersten 3 Bytes weiterhin `EF BB BF` sind. Falls das Standard-
      Edit-Tool die BOM überschreibt (z. B. durch "Datei komplett
      neu schreiben" statt "Zeile einfügen"), muss der Coder auf
      einen byte-genauen Python-Helper umstellen.
  - **Pattern-Beobachtung:** die BOM-Verteilung 24 % in step-012
    bestätigt die step-010/011-Hypothese ("30 % mit BOM" für
    `Core/Checkers/`+`Core/`-A-Teil) ungefähr (4/17 = 24 % im
    17-Klassen-Mega-Batch, 0/6 in `Maps/` = 0 %). **Heuristik-Punkt
    8 (TD-006) bleibt offen** — Konsolidierung out of scope
    step-012 (kein `auto_fixable`-Anhängen, kein TD-Eintrag durch
    Planer).
- **Subprozess-Marker im 17-Datei-Set** (regex-basiert per
  `Select-String -Path ... -Pattern 'McpTestClient\.ConnectAsync|CliProcessRunner\.RunLinterAsync|CliProcessRunner\.RunAsync|Program\.Main|IClassFixture<McpLiveRepositoryFixture>|Process\.Start|SubprocessConcurrencyGate' -AllMatches -SimpleMatch`):
  **0/0/0/0/0/0/0** über alle 17 Dateien — keine Klasse startet einen
  Subprozess. Alle 17 Klassen sind homogen **Unit**. Konsistent mit
  der etablierten Heuristik (Punkte 1–3) und der
  step-002/003/004/005/006/007/008/009/010/011-Bestätigung.

## Intention

Alle 17 in diesem Plan gelisteten Testklassen (11 `Core/`-Rest L–V +
6 `Maps/`+`Maps/Skeleton/` H–V) mit `[Trait("Category", "Unit")]`
auf Klassen-Ebene versehen. Dieser Step **schließt den `Core/`-Ordner
vollständig ab** (alle 19 Klassen getaggt nach step-012) und
**schließt den `Maps/`+`Maps/Skeleton/`-Ordner vollständig ab**
(alle 6 Klassen getaggt nach step-012). **Konsolidierungs-Beitrag:**
nach step-012 sind 19 + 6 = 25 Klassen in den beiden Ordnern
abgehakt. **Keine** Mischklassen (alle 17 Klassen homogen Unit,
keine method-level-Trait-Diskussion nötig). Der gelockerte
`max_batch_items: 20` aus `config.md` (2026-08-08) macht diesen
Mega-Batch erst möglich — der ursprüngliche 8-Item-Cap hätte
3+1 = 4 Schritte für diesen Inhalt erfordert (step-012a 8 Core/ +
step-012b 4 Core/ + step-012c 5 Maps/ + step-012d 1 Maps/).
Mit dem 20-Item-Cap reduziert sich das auf 1 Schritt, entsprechend
dem User-Wunsch "bündle größer".

**Heuristik-Bestätigung step-012:**
- **Heuristik-Punkt 6** (Helper-Klassen ohne Testmethoden sind keine
  Testklassen) in `LinterEngineTests.cs` bestätigt: Helper-Klasse
  `HighlyRelevantServiceTests` auf Z. 269 wird **nicht** getaggt.
- **Heuristik-Punkt 2 Negativ-Abgrenzung** (`IClassFixture<SymbolGraphCatalogFixture>`
  = Unit, nicht Integration) in `SkeletonStableIdTests.cs` bestätigt:
  Klassifikation Unit, kein Marker-Konflikt.
- **XML-Doc-Bibliothek Variante 1b** (2-fach `// @covers`) in
  `PlaybookGeneratorRound2Tests.cs` etabliert.
- **`#nullable enable`-Disziplin** in 7/17 Dateien ohne Direktive
  bestätigt: Trait-Insertion **darf** die Direktive **nicht**
  hinzufügen.
- **LF-only-EOL-Disziplin** in `SkeletonStableIdTests.cs` als
  `Maps/`-TD-003-Analogon bestätigt: Python-Helper analog step-007
  ist Pflicht.

## Konkrete Änderungen

**Bei `step_type: batch`** (siehe `../spec.md` §10.6): pro Item aus
der `items`-Liste im Frontmatter eine Unterüberschrift.

### item-01: `LinterEngineTests` → Unit — `src/AiNetLinter.Tests/Core/LinterEngineTests.cs` (Zeile 9)

- **Was:** Direkt über `public sealed class LinterEngineTests`
  (Z. 9) eine Zeile `[Trait("Category", "Unit")]` einfügen. Class
  verschiebt sich auf Z. 10. **WICHTIG:** die Datei enthält eine
  **2. Klasse** `public class HighlyRelevantServiceTests` auf Z. 269
  ohne `[Fact]`/`[Theory]` — Helper-Klasse, die **nicht** getaggt
  wird (Heuristik-Punkt 6). Standard-Edit erfasst nur die Zeilen
  um die Test-Klassen-Deklaration; die Helper-Klasse auf Z. 269
  bleibt unverändert.
- **Warum:** Klasse testet `LinterEngine` mit AdhocWorkspace + TestHelper
  rein in-process. 10 `[Fact]`-Methoden, 0 Subprozess-Marker, 0
  bestehende Traits — homogen Unit.
- **BOM-Hinweis:** kein BOM, Standard-Edit ausreichend.
- **Nullable-Disziplin:** Datei hat **keine** `#nullable enable`-
  Direktive am Dateianfang (Z. 1 = `using Xunit;`). Trait-Insertion
  **darf** die Direktive **nicht** hinzufügen (out of scope — würde
  Datei-Regel verändern, analog `LinterAnalyzerTests.cs` step-011).
  Wahrscheinlich hebt das `AiNetLinter.Tests`-Profil
  `EnforceNullableEnable` analog zu `EnforceSealedClasses` auf
  (siehe `AiNetLinter.mdc:83`); `dotnet build` läuft grün ohne
  Warnung.

### item-02: `NamespaceFilterTests` → Unit — `src/AiNetLinter.Tests/Core/NamespaceFilterTests.cs` (Zeile 8)

- **Was:** Standard-Insert zwischen `namespace …;` (Z. 6) und
  `public sealed class NamespaceFilterTests` (Z. 8). Class auf Z. 9.
- **Warum:** 2 `[Fact]`-Methoden, 0 Subprozess-Marker — homogen
  Unit. Kleinste `Core/`-Datei im Batch (1223 Bytes).
- **BOM-Hinweis:** kein BOM, Standard-Edit ausreichend.
- **Nullable-Disziplin:** Datei hat **keine** `#nullable enable`-
  Direktive am Dateianfang (Z. 1 = `using Xunit;`). Trait-Insertion
  **darf** die Direktive **nicht** hinzufügen.

### item-03: `NullCoalescingInitializerClassifierTests` → Unit — `src/AiNetLinter.Tests/Core/NullCoalescingInitializerClassifierTests.cs` (Zeile 14)

- **Was:** Standard-Insert zwischen `namespace …;` (Z. 12) und
  `public sealed class NullCoalescingInitializerClassifierTests`
  (Z. 14). Class auf Z. 15.
- **Warum:** 6 `[Fact]`-Methoden, 0 Subprozess-Marker — homogen
  Unit.
- **BOM-Hinweis:** Datei ist **BOM-tragend** (`EF BB BF`). Byte-Scan
  vorher/nachher.

### item-04: `PlaybookGeneratorRound2Tests` → Unit — `src/AiNetLinter.Tests/Core/PlaybookGeneratorRound2Tests.cs` (Zeile 22)

- **Was:** **XML-Doc-Variante (Variante 1b mit 2-fach `// @covers`):**
  neue Zeile `[Trait("Category", "Unit")]` zwischen Z. 21
  (`</summary>`) und der bisherigen Z. 22 (class); class verschiebt
  sich auf Z. 23. Kein Eingriff in `// @covers RepoPlaybookGenerator`
  (Z. 15) und `// @covers PlaybookSyntaxWalker` (Z. 16) und in
  XML-Doc (Z. 18-21).
- **Warum:** Klasse testet `RepoPlaybookGenerator.BuildContentAsync`
  + `PlaybookSyntaxWalker` rein in-process mit AdhocWorkspace. 8
  `[Fact]`-Methoden, 0 Subprozess-Marker, 0 bestehende Traits —
  homogen Unit.
- **BOM-Hinweis:** kein BOM, Standard-Edit ausreichend.
- **Heuristik-Bibliothek-Erweiterung:** Variante 1b (2-fach
  `// @covers`) wird in step-012 etabliert. Mechanik identisch zu
  Variante 1a (item-13/18 in step-011 mit 1-fach `// @covers`).

### item-05: `ResultPatternNamespaceTests` → Unit — `src/AiNetLinter.Tests/Core/ResultPatternNamespaceTests.cs` (Zeile 10)

- **Was:** Standard-Insert zwischen `namespace …;` (Z. 8) und
  `public sealed class ResultPatternNamespaceTests` (Z. 10). Class
  auf Z. 11.
- **Warum:** 6 `[Fact]`-Methoden, 0 Subprozess-Marker — homogen
  Unit.
- **BOM-Hinweis:** Datei ist **BOM-tragend** (`EF BB BF`). Byte-Scan
  vorher/nachher.
- **Nullable-Disziplin:** Datei hat **keine** `#nullable enable`-
  Direktive am Dateianfang (Z. 1 = `using System;`, erste Bytes
  = `EF BB BF 75 73 69 6E 67`). Trait-Insertion **darf** die
  Direktive **nicht** hinzufügen.

### item-06: `RuleRegistryTests` → Unit — `src/AiNetLinter.Tests/Core/RuleRegistryTests.cs` (Zeile 13)

- **Was:** Standard-Insert zwischen `namespace …;` (Z. 11) und
  `public sealed class RuleRegistryTests` (Z. 13). Class auf Z. 14.
- **Warum:** 10 `[Fact]`-Methoden, 0 Subprozess-Marker — homogen
  Unit.
- **BOM-Hinweis:** kein BOM, Standard-Edit ausreichend.

### item-07: `ScopeImmutabilityTests` → Unit — `src/AiNetLinter.Tests/Core/ScopeImmutabilityTests.cs` (Zeile 10)

- **Was:** Standard-Insert zwischen `namespace …;` (Z. 8) und
  `public sealed class ScopeImmutabilityTests` (Z. 10). Class auf
  Z. 11.
- **Warum:** 7 `[Fact]`-Methoden, 0 Subprozess-Marker — homogen
  Unit.
- **BOM-Hinweis:** Datei ist **BOM-tragend** (`EF BB BF`). Byte-Scan
  vorher/nachher.
- **Nullable-Disziplin:** Datei hat **keine** `#nullable enable`-
  Direktive am Dateianfang (Z. 1 = `using System;`, erste Bytes
  = `EF BB BF 75 73 69 6E 67`). Trait-Insertion **darf** die
  Direktive **nicht** hinzufügen.

### item-08: `StaticTestSentinelExemptionTests` → Unit — `src/AiNetLinter.Tests/Core/StaticTestSentinelExemptionTests.cs` (Zeile 13)

- **Was:** Standard-Insert zwischen `namespace …;` (Z. 11) und
  `public sealed class StaticTestSentinelExemptionTests` (Z. 13).
  Class auf Z. 14.
- **Warum:** 9 `[Fact]`-Methoden, 0 Subprozess-Marker — homogen
  Unit. 2. größte Datei im Batch (11301 Bytes).
- **BOM-Hinweis:** Datei ist **BOM-tragend** (`EF BB BF`). Byte-Scan
  vorher/nachher.

### item-09: `TestCoverageResolverTests` → Unit — `src/AiNetLinter.Tests/Core/TestCoverageResolverTests.cs` (Zeile 6)

- **Was:** Standard-Insert zwischen `namespace …;` (Z. 5) und
  `public sealed class TestCoverageResolverTests` (Z. 6). Class auf
  Z. 7. **Spezialfall:** classLine sehr nahe an namespace (nur 1
  Leerzeile dazwischen), Standard-Insert funktioniert unverändert.
- **Warum:** 3 `[Fact]`-Methoden, 0 Subprozess-Marker — homogen
  Unit. Kleinste `Core/`-Datei im Batch (1378 Bytes).
- **BOM-Hinweis:** kein BOM, Standard-Edit ausreichend.
- **Nullable-Disziplin:** Datei hat **keine** `#nullable enable`-
  Direktive am Dateianfang (Z. 1 = `using System;`). Trait-Insertion
  **darf** die Direktive **nicht** hinzufügen.

### item-10: `TestProjectDetectorSuffixTests` → Unit — `src/AiNetLinter.Tests/Core/TestProjectDetectorSuffixTests.cs` (Zeile 8)

- **Was:** Standard-Insert zwischen `namespace …;` (Z. 6) und
  `public sealed class TestProjectDetectorSuffixTests` (Z. 8).
  Class auf Z. 9.
- **Warum:** 3 `[Fact]` + 2 `[Theory]`-Methoden mit 11 `[InlineData]`-
  Einträgen = **5 Methoden, 14 Test-Cases zur Laufzeit**
  (Diskrepanz +9, ausschließlich aus dieser Klasse). Klassen-Trait
  erfasst alle 14 Cases via xUnit-Vererbung (analog step-007
  `OutputRootResolverTests`, step-008 `RuleLegendRegistryTests`,
  step-011 `UiFileSeparationCheckerTests`).
- **BOM-Hinweis:** kein BOM, Standard-Edit ausreichend.
- **Nullable-Disziplin:** Datei hat **keine** `#nullable enable`-
  Direktive am Dateianfang (Z. 1 = `using Microsoft.CodeAnalysis;`).
  Trait-Insertion **darf** die Direktive **nicht** hinzufügen.
- **Theory-InlineData-Positionen (verifiziert per Planer-Code-Scan):**
  Z. 22-34 Theory#1 mit 7 `[InlineData]`, Z. 36-45 Theory#2 mit 4
  `[InlineData]` = 11 InlineData gesamt.

### item-11: `ViolationDescriptionTests` → Unit — `src/AiNetLinter.Tests/Core/ViolationDescriptionTests.cs` (Zeile 9)

- **Was:** Standard-Insert zwischen `namespace …;` (Z. 7) und
  `public sealed class ViolationDescriptionTests` (Z. 9). Class auf
  Z. 10.
- **Warum:** 1 `[Fact]`-Methode, 0 Subprozess-Marker — homogen
  Unit. **Kleinste Datei im Batch (804 Bytes).**
- **BOM-Hinweis:** kein BOM, Standard-Edit ausreichend.

### item-12: `HotspotMapBuilderTests` → Unit — `src/AiNetLinter.Tests/Maps/HotspotMapBuilderTests.cs` (Zeile 12)

- **Was:** Standard-Insert zwischen `namespace …;` (Z. 10) und
  `public sealed class HotspotMapBuilderTests : IDisposable`
  (Z. 12). Class auf Z. 13. **Spezialfall `: IDisposable`** —
  Interface-Deklaration in der class-Signatur bleibt unverändert
  (Trait wird vor `public sealed class …` eingefügt, Interface-
  Deklaration ist Teil der class-Signatur und bleibt unangetastet
  — analog step-011 `UiFileSeparationCheckerTests:14` und
  `LinterEngineCacheTests:17`).
- **Warum:** 3 `[Fact]`-Methoden, 0 Subprozess-Marker — homogen
  Unit.
- **BOM-Hinweis:** kein BOM, Standard-Edit ausreichend.

### item-13: `SkeletonMapBuilderTests` → Unit — `src/AiNetLinter.Tests/Maps/Skeleton/SkeletonMapBuilderTests.cs` (Zeile 12)

- **Was:** Standard-Insert zwischen `namespace …;` (Z. 10) und
  `public sealed class SkeletonMapBuilderTests` (Z. 12). Class auf
  Z. 13.
- **Warum:** 2 `[Fact]`-Methoden, 0 Subprozess-Marker — homogen
  Unit.
- **BOM-Hinweis:** kein BOM, Standard-Edit ausreichend.

### item-14: `SkeletonStableIdTests` → Unit — `src/AiNetLinter.Tests/Maps/Skeleton/SkeletonStableIdTests.cs` (Zeile 11)

- **Was:** Standard-Insert zwischen `namespace …;` (Z. 9) und
  `public sealed class SkeletonStableIdTests :
  IClassFixture<SymbolGraphCatalogFixture>` (Z. 11). Class auf Z. 12.
- **Warum:** 1 `[Fact]`-Methode, **0 Subprozess-Marker** im
  strengen Sinne (Heuristik-Punkt 2 Negativ-Abgrenzung:
  `IClassFixture<SymbolGraphCatalogFixture>` = Unit, in-process,
  Mini-Solution-Load). Marker-Detailcheck (0 Treffer pro
  Marker-Kategorie): `McpTestClient\.ConnectAsync`=0,
  `CliProcessRunner\.RunLinterAsync`=0, `CliProcessRunner\.RunAsync`=0,
  `Program\.Main`=0, `IClassFixture<McpLiveRepositoryFixture>`=0,
  `Process\.Start`=0, `SubprocessConcurrencyGate`=0.
  `SymbolGraphCatalogFixture` ist laut codemap §79 "nur 1× verwendet
  ... in Mini-Solution + `SourceFileCatalog.LoadAsync`, in-process".
  **Klassifikation: Unit.**
- **BOM-Hinweis:** kein BOM, Standard-Edit ausreichend (für die
  Bytes 0-2), **ABER Python-Helper analog step-007 ist Pflicht**
  wegen LF-only-EOL (siehe nächster Punkt).
- **EOL-Hinweis — KRITISCH:** Datei ist **LF-only** (CR=0, LF=42,
  1645 Bytes) — `Maps/`-TD-003-Analogon (analog
  `McpLintConsoleTests.cs` LF-only in `Output/`). **Coder MUSS
  Python-Helper analog step-007 verwenden** (byte-genaues Einfügen
  einer `\n`-Trait-Zeile `[Trait("Category", "Unit")]\n` ohne
  EOL-Änderung). Standard-Edit-Tool könnte die Datei auf CRLF
  umstellen — das wäre ein außer-Scope-EOL-Wechsel und ein
  `git diff` über die gesamte Datei. **Konsequenz:** die 16
  CRLF-Dateien mit Standard-Edit-Tool + 1 LF-only-Datei mit
  Python-Helper = 17 Dateien mit unterschiedlicher Edit-Mechanik.
  Der Coder dokumentiert **beide** Wege im `step-result.md`
  §"Geänderte Dateien" und §"EOL-Konservierungstabelle".
- **Nullable-Disziplin:** Datei hat **keine** `#nullable enable`-
  Direktive am Dateianfang (Z. 1 = `using System.Linq;`). Trait-
  Insertion **darf** die Direktive **nicht** hinzufügen.

### item-15: `SkeletonSyntaxWalkerTests` → Unit — `src/AiNetLinter.Tests/Maps/Skeleton/SkeletonSyntaxWalkerTests.cs` (Zeile 10)

- **Was:** Standard-Insert zwischen `namespace …;` (Z. 8) und
  `public sealed class SkeletonSyntaxWalkerTests` (Z. 10). Class auf
  Z. 11.
- **Warum:** 11 `[Fact]`-Methoden, 0 Subprozess-Marker — homogen
  Unit. 2. größte `Maps/`-Datei im Batch (7065 Bytes).
- **BOM-Hinweis:** kein BOM, Standard-Edit ausreichend.

### item-16: `StructureMapBuilderTests` → Unit — `src/AiNetLinter.Tests/Maps/StructureMapBuilderTests.cs` (Zeile 12)

- **Was:** Standard-Insert zwischen `namespace …;` (Z. 10) und
  `public sealed class StructureMapBuilderTests : IDisposable`
  (Z. 12). Class auf Z. 13. **Spezialfall `: IDisposable`** —
  Interface-Deklaration in der class-Signatur bleibt unverändert
  (Trait wird vor `public sealed class …` eingefügt, Interface-
  Deklaration ist Teil der class-Signatur und bleibt unangetastet).
- **Warum:** 4 `[Fact]`-Methoden, 0 Subprozess-Marker — homogen
  Unit.
- **BOM-Hinweis:** kein BOM, Standard-Edit ausreichend.

### item-17: `VocabularyMapBuilderTests` → Unit — `src/AiNetLinter.Tests/Maps/VocabularyMapBuilderTests.cs` (Zeile 12)

- **Was:** Standard-Insert zwischen `namespace …;` (Z. 10) und
  `public sealed class VocabularyMapBuilderTests : IDisposable`
  (Z. 12). Class auf Z. 13. **Spezialfall `: IDisposable`** —
  Interface-Deklaration in der class-Signatur bleibt unverändert
  (Trait wird vor `public sealed class …` eingefügt, Interface-
  Deklaration ist Teil der class-Signatur und bleibt unangetastet).
- **Warum:** 5 `[Fact]`-Methoden, 0 Subprozess-Marker — homogen
  Unit.
- **BOM-Hinweis:** kein BOM, Standard-Edit ausreichend.

## Tests

Keine — die Klassifikation ist rein additiv (Attribut setzen).
Existierende Tests müssen **unverändert** grün bleiben. Validierung
erfolgt über den vollen `dotnet test`-Lauf in der Definition of Done
(kein neuer Test, kein geänderter Test).

## Definition of Done

- [ ] Alle 17 Items umgesetzt (je eine `[Trait("Category",
      "Unit")]`-Zeile über der jeweiligen Test-Klassen-Deklaration —
      **NICHT** über Helper-Klassen, siehe item-01
      `LinterEngineTests.cs:269` Heuristik-Punkt 6).
- [ ] **Bestehende Traits respektiert:** keine vorhandenen
      Trait-Attribute überschrieben oder entfernt (Trifft im Batch
      nicht zu, aber als Plausibilitäts-Check zu verifizieren: nach
      dem Diff sollten in `Core/` 19 Klassen mit Trait-Attribut
      existieren, 0 ohne; in `Maps/`+`Maps/Skeleton/` 6 Klassen mit
      Trait-Attribut, 0 ohne).
- [ ] **EOL-Pflicht-Vollscan** (TD-003-Empfindlichkeit): Coder
      scannt per PowerShell `[System.IO.File]::ReadAllBytes(...)`
      über **alle 17 Dateien** **vor** und **nach** dem Edit, um
      sicherzustellen, dass:
      - 16/17 Dateien **uniform CRLF** bleiben (CR-Zahl = LF-Zahl
        jeweils, kein gemischter Status).
      - **1/17 Datei (`Maps/Skeleton/SkeletonStableIdTests.cs`)
        bleibt LF-only** (CR=0, LF-Zahl = 42+1 = 43 nach Trait-Insert
        — exakt ein zusätzliches LF durch die eingefügte Trait-Zeile
        `[Trait("Category", "Unit")]\n`).
      - **Pflicht-Python-Helper analog step-007 für die 1 LF-only-
        Datei** — Standard-Edit-Tool könnte die Datei auf CRLF
        umstellen. Coder verifiziert die ersten 3 Bytes (`75 73 69` =
        `using`) und die CR/LF-Bilanz.
      - **Pflicht-Vollscan, keine Stichprobe** — bei 17-Datei-Batch
        ist Wahrscheinlichkeit für EOL-Abweichung erhöht, vollständige
        Verifikation ist Pflicht.
- [ ] **BOM-Pflicht-Vollscan** (TD-005/TD-006-Empfindlichkeit):
      Coder scannt per `[System.IO.File]::ReadAllBytes(...)` über
      **alle 4 BOM-tragenden Dateien** (`NullCoalescingInitializerClassifierTests`,
      `ResultPatternNamespaceTests`, `ScopeImmutabilityTests`,
      `StaticTestSentinelExemptionTests`) **vor** und **nach** dem
      Edit, um sicherzustellen, dass die ersten 3 Bytes weiterhin
      `EF BB BF` sind. Für die 13 Nicht-BOM-Dateien keine Pflicht-
      Verifikation, aber Stichprobe empfohlen. **Falls** das
      Standard-Edit-Tool die BOM überschreibt, muss der Coder auf
      einen byte-genauen Python-Helper umstellen (analog step-007).
- [ ] **String-Literal-`[Fact]`-Pflicht-Check** (NITPICK-Linie aus
      step-009): Coder macht für **alle 17 Dateien** einen Brutto-
      `Select-String`-Count (`Pattern '[Fact]' -AllMatches
      -SimpleMatch`) und einen Netto-Count (Filterung auf Zeilen
      ohne `"`-Vorkommen). Brutto und Netto dokumentieren. Bei
      Diskrepanz: zwingend String-Literal-Ausschluss-Methodik
      anwenden. Erwartung step-012: Brutto 91, Netto 91 (0/17
      Dateien mit Verschachtelung).
- [ ] **`#nullable enable`-Disziplin** (TD-004-Empfindlichkeit): für
      die **7/17 Dateien ohne Direktive** (`LinterEngineTests`,
      `NamespaceFilterTests`, `ResultPatternNamespaceTests`,
      `ScopeImmutabilityTests`, `TestCoverageResolverTests`,
      `TestProjectDetectorSuffixTests`, `SkeletonStableIdTests`):
      Coder bestätigt per Code-Scan, dass die Trait-Insertion die
      Direktive **nicht** hinzugefügt hat (Z. 1 unverändert). Out of
      scope — würde Datei-Regel verändern. **Wichtig:** keine
      "Aufräum"-Beifügung der Direktive (analog `LinterAnalyzerTests.cs`
      step-011 Out-of-Scope).
- [ ] **`IClassFixture<SymbolGraphCatalogFixture>`-Klassifikation**
      (Heuristik-Punkt-2-Negativ-Abgrenzung): Coder bestätigt per
      Marker-Detailcheck für `Maps/Skeleton/SkeletonStableIdTests.cs`,
      dass alle 7 Subprozess-Marker-Patterns (`McpTestClient\.ConnectAsync`,
      `CliProcessRunner\.RunLinterAsync`, `CliProcessRunner\.RunAsync`,
      `Program\.Main`, `IClassFixture<McpLiveRepositoryFixture>`,
      `Process\.Start`, `SubprocessConcurrencyGate`) 0 Treffer haben.
      Klassifikation **Unit** begründet.
- [ ] **Helper-Klassen-Disziplin** (Heuristik-Punkt 6): Coder
      bestätigt per Code-Scan, dass die Helper-Klasse
      `public class HighlyRelevantServiceTests` auf Z. 269 in
      `LinterEngineTests.cs` **nicht** getaggt wurde. Standard-Edit
      erfasst nur die Zeilen um die Test-Klassen-Deklaration auf
      Z. 9; die Helper-Klasse auf Z. 269 bleibt unverändert.
- [ ] **Build-Command** aus Tech-Stack-Notiz (`roadmap.md`) grün:
      `dotnet build` (TreatWarningsAsErrors).
- [ ] **Test-Command** aus Tech-Stack-Notiz grün: `dotnet test`
      (voller Lauf, alle 1325 Tests müssen weiterhin grün sein).
- [ ] **Trait-Filter-Smoke-Test** (vom Coder zusätzlich durchführen):
      - `dotnet test --no-build --filter "Category=Unit"` → muss
        grün sein, exakt **+102** Tests gegenüber step-011
        (882 → 984).
      - `dotnet test --no-build --filter "Category=Integration"` →
        muss grün sein, **±0** Veränderung (113 bleibt 113).
      - Numerische Begründung im `step-result.md` dokumentieren
        (Brutto-Count pro Datei + Netto-Count + Filter-Delta).
- [ ] **Self-Lint** (TD-001-konform, semantisch identisch zu
      `--self-lint`): `dotnet run --project src/AiNetLinter --
      --config rules.json --path .` → muss `OK` ausgeben.
- [ ] **Commit auf aktuellem Branch** (Conventional Commit auf
      Deutsch, imperativ, mit Task-Suffix `[flaky-and-test-performance]`):
      empfohlener Subject:
      `test: Core+Maps-Tests Kategorie-taggen [flaky-and-test-performance]`
      (Subject-Länge mit Suffix: **67 Zeichen, 5 Zeichen Reserve zur
      72-Grenze**, verifiziert per PowerShell
      `('test: Core+Maps-Tests Kategorie-taggen [flaky-and-test-performance]').Length`
      = 67). Konsistent mit step-011-Subject "test:
      Checkers+Core-Tests Kategorie-taggen [flaky-and-test-performance]"
      (71 Zeichen) und 4 Zeichen kürzer. Coder übernimmt den
      Subject-Vorschlag **ohne Änderung** (TD-002-Disziplin,
      Variante (a)-Empfehlung), Body mit Ref-Block
      `Ref: tasks/flaky-and-test-performance/step-012`.
- [ ] **DoD — Pipeline-Konvention (Kritiker-Hinweis aus step-011):**
      Coder schreibt den **Code-Commit ZUERST** mit dem finalen
      Code-Hash, referenziert den Hash **direkt** in
      `step-result.md` (kein Placeholder), schreibt erst **DANACH**
      den Doku-Commit (verweist auf den finalen Code-Hash). Damit
      ist der 3-Commits-Mechanismus aus step-011 (Code + Doku +
      Hash-Korrektur) strukturell überflüssig. Das ist **kein**
      Verstoß gegen History-Reset (`spec.md` §10.7), sondern eine
      Coder-Pipeline-Konvention. **Subject-Länge für beide Commits
      ≤ 72 Zeichen inkl. Suffix `[flaky-and-test-performance]`**
      (TD-002-Disziplin). Doku-Commit-Subject kann z. B. lauten:
      `docs(tasks): step-012 Result dokumentieren [flaky-and-test-performance]`
      (66 Zeichen).
- [ ] `step-012/step-result.md` geschrieben mit: Diff-Statistik
      (Anzahl hinzugefügter Trait-Zeilen pro Item, Gesamt-Diff in
      Zeilen und Bytes — **16× +29 Bytes via Standard-Edit-Tool
      + 1× +27 Bytes via Python-Helper (LF-only-Datei) = +29
      + 16×29 + 27 = +491 Bytes**), Test-Case-Inventar pro Datei
      (regex-basiert + String-Literal-Ausschluss, **erwartet: 91
      Brutto = 91 Netto + 11 InlineData = 102 Test-Cases**),
      BOM-Konservierungs-Tabelle für alle 4 BOM-Dateien,
      EOL-Konservierungstabelle für alle 17 Dateien (mit separater
      Spalte für `SkeletonStableIdTests.cs` LF-only-Verifikation),
      Testergebnis (Gesamt-Lauf + 2 Filter-Läufe mit Test-Zahlen +
      Delta-Abgleich, **erwartet: Unit 882→984, Integration 113±0,
      Total 1325±0**), Build-Output, Self-Lint-Output,
      Commit-Hashes (Code-Commit + Doku-Commit), Subjects. CodeMap-
      Update dokumentiert (`Core/`-Zeile auf "step-011 + step-012 =
      19/19 abgeschlossen, Ordner vollständig abgehakt" +
      `Maps/`+`Maps/Skeleton/`-Zeile auf "6/6 abgeschlossen in
      step-012, Ordner vollständig abgehakt").
- [ ] `status` in `step-plan.md` von `open` auf `in_progress`
      (durch Orchestrator nach Coder-Start) und nach
      `step-result.md`-Schreiben auf `done (pending audit)` (durch
      Coder) gesetzt.

## Rules-Refs

- `.agents/rules/AiNetLinterRichtlinien.mdc` §4 "Testsuite-Parallelität
  bewahren" — relevant nur als Ausschluss: Trait-Attribute haben
  **keinen** Einfluss auf Parallelismus, nur
  `[Collection(...)]` / `DisableParallelization`. Dieser Step
  berührt die Parallelität nicht, ist also nicht regel-restriktiv
  hier.
- `.agents/rules/AiNetLinterRichtlinien.mdc` §4 "Commit-Vorschlag
  Pflicht" — betrifft die Coder-Antwort, ist im DoD-Punkt oben
  explizit referenziert.
- `.agents/rules/AiNetLinterRichtlinien.mdc` §4 "Conventional
  Commits auf Deutsch, imperativ" — Subject-Vorschlag
  `test: Core+Maps-Tests Kategorie-taggen [flaky-and-test-performance]`
  (67 Zeichen) folgt dieser Konvention. Subject für Doku-Commit
  z. B. `docs(tasks): step-012 Result dokumentieren
  [flaky-and-test-performance]` (66 Zeichen).
- `.agents/rules/AiNetLinterRichtlinien.mdc` §5 "Sparsame
  Kommentare" — die hinzugefügten Trait-Zeilen sind XML-Attribute,
  keine Kommentare. Kein Bezug.
- `.agents/rules/AiNetLinterRichtlinien.mdc` §5 "Zero-Warning-
  Direktive" — die Trait-Attribute sind `[Trait("Category",
  "Unit")]`, exakt die im Projekt etablierte Schreibweise
  (Großbuchstabe am Wortanfang). Keine Warnung erwartet.
- `.agents/rules/AiNetLinterRichtlinien.mdc` §5
  "Symptom-Fixing verboten" — relevant für item-01
  `LinterEngineTests.cs` (Helper-Klasse `HighlyRelevantServiceTests`
  auf Z. 269 wird **nicht** im step-012-Scope umbenannt oder
  umstrukturiert, nur additives Attribut auf der Test-Klasse).
  Out-of-Scope-Hinweis im `step-result.md` §"Beobachtungen".
- `.agents/rules/AiNetLinter.mdc` (auto-generiert) —
  `EnforceSealedClasses` ist für `*.Tests` aufgehoben (Z. 83),
  alle 17 Klassen sind `public sealed class` (außer
  `LinterEngineTests.cs:269` Helper-Klasse) — konsistent.
  `EnforceNullableEnable` ist im `AiNetLinter.Tests`-Profil
  vermutlich ebenfalls aufgehoben (analog TD-004-Beobachtung in
  `Output/`), 7/17 Dateien ohne Direktive sind **kein**
  Build-Error. `MaxMethodLineCount: 100` für `*.Tests` ist erfüllt
  (alle 17 Klassen ≤ 333 Zeilen = max für `LinterEngineTests.cs`,
  alle Methoden weit unter 100 Zeilen).
- `config.md` (NEU seit 2026-08-08) — `max_batch_items: 20` ist
  die **Voraussetzung** für diesen Mega-Batch (11 `Core/` + 6
  `Maps/` = 17 Klassen, deutlich unter dem 20-Item-Cap). Ohne
  `config.md`-Update wäre der Plan nicht ausführbar (würde den
  8-Item-Cap aus `task-state.md` Default-Config verletzen).

## Bekannte Ausnahmen

- **`LinterEngineTests.cs` (item-01):** Datei enthält **2 Klassen** —
  Test-Klasse `LinterEngineTests` (Z. 9, wird getaggt) und
  Helper-Klasse `public class HighlyRelevantServiceTests` (Z. 269,
  wird **nicht** getaggt per Heuristik-Punkt 6). Die Helper-Klasse
  enthält keine `[Fact]`/`[Theory]`-Methoden, sondern ist eine
  interne Test-Fixture-Klasse. **Konsequenz:** der Trait wird **nur**
  über der Test-Klassen-Deklaration auf Z. 9 eingefügt, die
  Helper-Klassen-Deklaration auf Z. 269 bleibt unverändert.
  `dotnet build` (TreatWarningsAsErrors) läuft grün, weil die
  Helper-Klasse nicht `sealed` ist (Heuristik-Punkt 6 erlaubt
  Helper-Klassen ohne `sealed`).
- **`PlaybookGeneratorRound2Tests.cs` (item-04):** XML-Doc-Variante
  **1b mit 2-fach `// @covers`** — siehe §"Aktueller Projektzustand"
  oben für die exakte 3-Schichten-Position. Variante 1b wird in
  step-012 etabliert; Mechanik identisch zu Variante 1a (1-fach
  `// @covers`, item-13/18 in step-011). **Bibliotheks-Erweiterung:**
  Variante 1b ab sofort verfügbar.
- **`TestProjectDetectorSuffixTests.cs` (item-10):** 2 `[Theory]`-
  Methoden (Z. 22-34 mit 7 `[InlineData]`, Z. 36-45 mit 4
  `[InlineData]`) + 3 `[Fact]`-Methoden = **5 Methoden, 14
  Test-Cases zur Laufzeit** — siehe §"Aktueller Projektzustand"
  für die numerische-Diskrepanz-Dokumentation (+9). Klassen-Trait
  erfasst alle 14 Cases via xUnit-Vererbung (analog step-007
  `OutputRootResolverTests`, step-008 `RuleLegendRegistryTests`,
  step-011 `UiFileSeparationCheckerTests`).
- **`SkeletonStableIdTests.cs` (item-14):** 3 kombinierte Spezialfälle
  in **einer** Datei — siehe §"Aktueller Projektzustand" oben für
  die ausführliche Diskussion:
  1. `IClassFixture<SymbolGraphCatalogFixture>` (Z. 11) = Unit
     per Heuristik-Punkt-2-Negativ-Abgrenzung.
  2. **LF-only-EOL (CR=0, LF=42)** — `Maps/`-TD-003-Analogon
     (analog `McpLintConsoleTests.cs` in `Output/`). **Python-Helper
     analog step-007 ist Pflicht** (byte-genaues Einfügen einer
     `\n`-Trait-Zeile).
  3. **Kein `#nullable enable`** am Dateianfang (analog
     `LinterAnalyzerTests.cs` step-011). Trait-Insertion fügt die
     Direktive nicht hinzu.
- **3 `Maps/`-Klassen mit `: IDisposable` (items-12/16/17):**
  `HotspotMapBuilderTests`, `StructureMapBuilderTests`,
  `VocabularyMapBuilderTests` — Interface-Deklaration in der
  class-Signatur bleibt unverändert (Standard-Insert funktioniert).
- **BOM-Inhomogenität in `Core/` (4/17 = 24 % mit BOM) und
  `Maps/` (0/6 = 0 % mit BOM):** die step-010/011-Hypothese ("30 %
  mit BOM" für `Core/Checkers/`+`Core/`-A-Teil) bestätigt sich im
  17-Klassen-Mega-Batch (4/17 = 24 % im Gesamt, 0/6 in `Maps/`).
  **Heuristik-Punkt 8 (TD-006) bleibt offen** — Konsolidierung
  out of scope step-012. Coder dokumentiert die BOM-Konservierung
  pro Datei (Byte-Scan vorher/nachher) als Beobachtung im
  `step-result.md`.
- **EOL-Inhomogenität in `Maps/` (1/6 = 17 % LF-only):** einzig
  `SkeletonStableIdTests.cs` ist LF-only (analog `McpLintConsoleTests.cs`
  LF-only in `Output/`). Konsolidierung out of scope step-012
  (kein TD-Eintrag durch Planer — TD-003 deckt den
  `Output/`-Fall bereits ab, und TD-007 würde diese Beobachtung
  für `Maps/` konsolidieren; das ist Nutzer-Sache, nicht im
  step-012-Scope).
- **Nullable-Disziplin in 7/17 Dateien ohne Direktive** (analog
  step-011 item-19 `LinterAnalyzerTests.cs`): Trait-Insertion
  fügt die Direktive nicht hinzu, out of scope. `dotnet build`
  läuft grün ohne Warnung. Falls Kritiker dennoch einen
  Build-Error vermutet: die Tests-Profil-Overrides heben die
  Regel wahrscheinlich auf.

## Code-Skizze (optional)

Vorher (Beispiel: `LinterEngineTests.cs:9`):

```csharp
namespace AiNetLinter.Tests.Core;

public sealed class LinterEngineTests
{
    private static Config CreateDefaultConfig()
    {
```

Nachher (Standard-Insert, 1. Klasse Z. 9 wird getaggt, 2. Klasse
Z. 269 bleibt unangetastet):

```csharp
namespace AiNetLinter.Tests.Core;

[Trait("Category", "Unit")]
public sealed class LinterEngineTests
{
    private static Config CreateDefaultConfig()
    {
```

Für `PlaybookGeneratorRound2Tests.cs:22` (XML-Doc-Variante 1b mit
2-fach `// @covers`):

```csharp
// @covers RepoPlaybookGenerator
// @covers PlaybookSyntaxWalker

/// <summary>
/// Tests für die in Round 2 eingeführten Playbook-Features:
/// BuildContentAsync, --playbook --check, Ordner-Slices, projektinternes Result.
/// </summary>
public sealed class PlaybookGeneratorRound2Tests
{
```

wird zu:

```csharp
// @covers RepoPlaybookGenerator
// @covers PlaybookSyntaxWalker

/// <summary>
/// Tests für die in Round 2 eingeführten Playbook-Features:
/// BuildContentAsync, --playbook --check, Ordner-Slices, projektinternes Result.
/// </summary>
[Trait("Category", "Unit")]
public sealed class PlaybookGeneratorRound2Tests
{
```

Für `HotspotMapBuilderTests.cs:12` (Standard-Insert mit
`: IDisposable`):

```csharp
namespace AiNetLinter.Tests.Maps;

public sealed class HotspotMapBuilderTests : IDisposable
{
```

wird zu:

```csharp
namespace AiNetLinter.Tests.Maps;

[Trait("Category", "Unit")]
public sealed class HotspotMapBuilderTests : IDisposable
{
```

Für `Maps/Skeleton/SkeletonStableIdTests.cs:11` (Standard-Insert,
aber **Python-Helper** wegen LF-only):

```python
# Python-Helper analog step-007
path = "src/AiNetLinter.Tests/Maps/Skeleton/SkeletonStableIdTests.cs"
with open(path, "rb") as f:
    data = f.read()
# Finde die Zeile "public sealed class SkeletonStableIdTests"
# Füge davor eine Zeile '[Trait("Category", "Unit")]\n' ein
# (mit \n statt \r\n, weil Datei LF-only ist)
trait_line = b'[Trait("Category", "Unit")]\n'
target = b'public sealed class SkeletonStableIdTests'
idx = data.index(target)
new_data = data[:idx] + trait_line + data[idx:]
# Wichtig: EOL nicht verändern! Wenn Datei LF-only war, bleibt sie LF-only.
with open(path, "wb") as f:
    f.write(new_data)
```

## Notes

- **Batch-Umfang:** 17 Klassen × je 1 Trait-Zeile = **17–18
  Diff-Zeilen** (zzgl. evtl. BOM/EOL-Header-Bytes, die unverändert
  bleiben müssen). Deutlich unter dem `max_batch_diff_lines: 80`-
  Deckel (Spec-Wert 2× default 40 = 80 — bewusste Reserve für
  BOM-Konservierungs-Kontext-Zeilen). Bei 1-Diff-Zeile/Klasse
  (Standard-Insert) ergibt 16 Klassen ~16–17 Zeilen, plus 1
  XML-Doc-Variante (1 Zeile) = 17–18 Zeilen Gesamt-Diff.
- **Schritt-Typ `low`-Risk-Begründung:** rein additives Attribut
  auf Klassen, das weder Build-Verhalten noch Test-Verhalten noch
  Parallelität ändert. Trait-Werte folgen exakt der bestehenden
  Konvention (`Unit`, CamelCase-Großbuchstabe). Kein Eingriff in
  Produktionscode, keine Fixture-Änderung, keine Test-Logik-
  Änderung. Die XML-Doc-Variante 1b für 1/17 Klasse ist seit
  step-011 (item-04/13/18 mit 1-fach `// @covers`) etabliert.
  Der Python-Helper für 1/17 LF-only-Datei ist seit step-007
  (`McpLintConsoleTests.cs`) etabliert.
- **Mega-Batch-Spezialfall:** `max_batch_items: 20` per `config.md`
  (2026-08-08) ist die Voraussetzung für diesen Plan — ohne
  `config.md`-Update wäre der Plan nicht ausführbar. Der
  Orchestrator hat mit dem User-Feedback "bündle größer"
  (2026-08-08) diese Lockerung etabliert.
- **`Core/`-Schnitt-Abschluss:** nach step-012 sind alle 19 Klassen
  in `Core/` getaggt — der Ordner ist **vollständig abgehakt** (8
  step-011 + 11 step-012 = 19/19). Die CodeMap-Annotation für
  `Core/` wird im Doku-Commit von "step-011 nimmt erste 8
  Klassen als Mega-Batch-Anteil; step-012 verbleibend mit 11
  Klassen (`LinterEngineTests`–`ViolationDescriptionTests`)" auf
  "step-011 + step-012 = 19/19 abgeschlossen, Ordner vollständig
  abgehakt" aktualisiert.
- **`Maps/`-Schnitt-Abschluss:** nach step-012 sind alle 6 Klassen
  in `Maps/`+`Maps/Skeleton/` getaggt — der Ordner ist
  **vollständig abgehakt** (3 `Maps/`-Klassen + 3 `Maps/Skeleton/`-
  Klassen = 6/6 in 1 Batch). Die CodeMap-Annotation für `Maps/`
  wird im Doku-Commit von "6 Klassen; rein Unit, dito (zuletzt:
  step-002)" auf "6/6 abgeschlossen in step-012, Ordner vollständig
  abgehakt" aktualisiert.
- **Heuristik-Punkte-Bestätigung:** alle 8 bisherigen
  Heuristik-Punkte sind in step-012 bestätigt (Punkte 1–3:
  Klassen-Homogenität, Traits respektieren, Subprozess-Marker mit
  Negativ-Abgrenzung für `IClassFixture<SymbolGraphCatalogFixture>`
  = Unit; Punkte 4–8: Helper-Klassen-Ausschluss, BOM-TD-005,
  BOM-TD-006, String-Literal-`[Fact]`-Ausschluss,
  Hypothesen-Auflösung). **Neu in step-012** ist die explizite
  Bestätigung der Heuristik-Punkt-2-Negativ-Abgrenzung für
  `SkeletonStableIdTests.cs` und die Etablierung der XML-Doc-
  Variante 1b (2-fach `// @covers`) in `PlaybookGeneratorRound2Tests.cs`.
  Beide Erweiterungen sind in den Folge-Planern wiederverwendbar.
- **Pipeline-Konvention (Kritiker-Hinweis aus step-011):** die
  in step-011 etablierte Pipeline-Konvention "Code-Commit zuerst,
  Hash direkt in `step-result.md` referenzieren (statt Placeholder),
  Doku-Commit danach" wird in step-012 **konsequent** angewendet —
  siehe DoD-Punkt "Pipeline-Konvention". Damit ist der
  3-Commits-Mechanismus aus step-011 (Code + Doku + Hash-Korrektur)
  strukturell überflüssig. Der Korrektur-Commit-Subject
  `docs(tasks): step-011 step-result Hash-Korrektur
  [flaky-and-test-performance]` (77 Zeichen) bleibt als
  TD-002-MINOR-Beobachtung dokumentiert; step-012-Commits halten
  die 72-Zeichen-Grenze von vornherein ein.
- **Folge-Steps (NICHT in diesem Plan geplant, nur informativ):**
  1. **step-013 ff.:** `Mcp/Tools/` (17 Klassen, 2–3 Batches),
     `Mcp/` (19 Klassen, gemischt), `Baseline/` (10 Klassen,
     gemischt), `Commands/` (17 Klassen, stark gemischt — pro-
     Methode-Tagging für `McpServerCommandTests` als eigener Step
     nötig), `Cli/` (6 Klassen, gemischt).
- **Gesamt-Fortschritt nach step-012 (Planer-Erwartung):** 882
  (Stand nach step-011) + 102 (step-012) = **984 Unit**,
  113 Integration unverändert, 1325 Total unverändert.
  Stand step-011: 882 Unit / 113 Integration / 1325 Total. Nach
  step-012: **984 Unit / 113 Integration / 1325 Total** (Delta
  +102 Unit, ±0 Integration, ±0 Total). Restbestand: 1325 − 984 =
  341 ungetaggte Methoden in ca. 75+ Klassen über 5+ Ordner
  (`Mcp/Tools/`, `Mcp/`, `Baseline/`, `Commands/`, `Cli/`).
- **Doku-Pflicht:** nach step-012 aktualisiert der Coder die
  CodeMap-Annotationen für `Core/` (vollständig abgehakt) und
  `Maps/`+`Maps/Skeleton/` (vollständig abgehakt). Die `roadmap.md`
  ist bereits durch den Planer in diesem Aufruf aktualisiert
  (In-Arbeit-Annotation auf step-012 gerollt, step-011 als done
  markiert, step-012 als Mega-Batch-Beschreibung).
