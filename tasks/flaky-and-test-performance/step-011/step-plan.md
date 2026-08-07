---
status: done (pending audit)
type: step-plan
task: flaky-and-test-performance
step: 011               # flach, Task-weite Sequenz — auch Korrekturen liegen hier, nie in einem Unterordner
corrects: null             # <null | step-NNN> — nur gesetzt, wenn dieser Step eine Korrektur ist (treibt das Kettenbudget, siehe ../spec.md §10.5/§10.6)
title: "Category-Traits für Core/Checkers-Rest (12 Klassen M–W) und Core-Anfang (8 Klassen A–LinterEngineCache) nachziehen (Batch 10, Mega-Batch 1 von 2 für Checkers+Core)"
epic: EPIC-02          # Bezug zum Epic in roadmap.md, dem dieser Step zuarbeitet (bei corrects: vom korrigierten Step übernommen)
estimated_risk: low  # Einschätzung des Planers, siehe skills/planer/SKILL.md
step_type: batch  # single (Default) | batch — siehe ../spec.md §10.6. Bei batch: items-Liste unten füllen.
items:  # nur bei step_type: batch. Ein Eintrag pro gebündeltem Mini-Befund innerhalb des Epics (oder pro opportunistisch angehängtem auto_fixable-Tech-Debt, siehe ../spec.md §9.1/§10.6):
  - id: item-01
    title: "MethodParameterCountIgnoreTypePrefixesTests → Unit (in-process LinterAnalyzer mit Method-Param-Count-Check + Ignore-Type-Prefixes; 5 [Fact], classLine=12; Standard-Insert; **BOM-tragend** — Byte-Scan vorher/nachher, CRLF+TrNL, #nullable enable Z.1)"
    source: "konzept.md §Wie Schritt 2, §Muss-Haven Traits-Punkt"
  - id: item-02
    title: "MethodParameterCountOverrideTests → Unit (in-process LinterAnalyzer mit Method-Param-Count-Check + Override-Logik; 12 [Fact], classLine=12; Standard-Insert; **BOM-tragend** — Byte-Scan vorher/nachher, CRLF+TrNL, #nullable enable Z.1)"
    source: "konzept.md §Wie Schritt 2"
  - id: item-03
    title: "MiddleManCheckerTests → Unit (in-process MiddleManChecker.Check mit Verbatim-Test-Source als `string TestHelperTypes` Z.13-19 — **kein** [Fact] im Verbatim-Block, String-Literal-Scan bestätigt 0 Treffer; 9 [Fact], classLine=11; Standard-Insert; kein BOM, CRLF+TrNL, #nullable enable Z.1)"
    source: "konzept.md §Wie Schritt 2"
  - id: item-04
    title: "NamespaceCouplingCheckerTests → Unit (in-process NamespaceCouplingChecker; 1 [Fact], classLine=11; Standard-Insert; kein BOM, CRLF+TrNL, #nullable enable Z.1 — kleinste Datei im Batch 1443 Bytes)"
    source: "konzept.md §Wie Schritt 2"
  - id: item-05
    title: "NamingCheckerTests → Unit (in-process NamingChecker; 3 [Fact], classLine=10; Standard-Insert; kein BOM, CRLF+TrNL, #nullable enable Z.1; **String-Literal-Hinweis:** 3 TripleQuoted-Blöcke (raw strings), Planer-Scan ergab 0 [Fact]-Verschachtelung)"
    source: "konzept.md §Wie Schritt 2"
  - id: item-06
    title: "PhantomDependencyCheckerTests → Unit (in-process PhantomDependencyChecker; 1 [Fact], classLine=11; Standard-Insert; kein BOM, CRLF+TrNL, #nullable enable Z.1 — kleinste Datei im Batch 1080 Bytes)"
    source: "konzept.md §Wie Schritt 2"
  - id: item-07
    title: "SealedClassCheckerTests → Unit (in-process SealedClassChecker.Check; 5 [Fact], classLine=11; Standard-Insert; kein BOM, CRLF+TrNL, #nullable enable Z.1)"
    source: "konzept.md §Wie Schritt 2"
  - id: item-08
    title: "SilentCatchAllowedTypesTests → Unit (in-process SilentCatch-Check mit Allowed-Types-Liste; 4 [Fact], classLine=12; Standard-Insert; **BOM-tragend** — Byte-Scan vorher/nachher, CRLF+TrNL, #nullable enable Z.1)"
    source: "konzept.md §Wie Schritt 2"
  - id: item-09
    title: "SwitchDispatcherDetectorTests → Unit (in-process SwitchDispatcherDetector; 7 [Fact], classLine=12; Standard-Insert; **BOM-tragend** — Byte-Scan vorher/nachher, CRLF+TrNL, #nullable enable Z.1)"
    source: "konzept.md §Wie Schritt 2"
  - id: item-10
    title: "UiFileSeparationCheckerTests → Unit (in-process UiFileSeparationChecker.ScanDirectory + RazorNeedsCss/RazorHasInlineCode; 19 [Fact] + 4 [Theory]×(12+8+3+4)=27 [InlineData] = **46 Test-Cases zur Laufzeit**; classLine=14, **: IDisposable**-Interface; Standard-Insert funktioniert (Trait vor class-Deklaration, Interface无关); kein BOM, CRLF+TrNL, #nullable enable Z.1 — **Spezialfall:** komplexeste Test-Case-Klasse im Batch, ähnlich step-008 `RuleLegendRegistryTests` 8 Theory+56 InlineData=221 Cases)"
    source: "konzept.md §Wie Schritt 2; Code-Verifikation Planer-Schritt-2 (Z.206-219 Theory#1 mit 12 [InlineData], Z.224-236 Theory#2 mit 8, Z.292-299 Theory#3 mit 3, Z.301-309 Theory#4 mit 4)"
  - id: item-11
    title: "ValueObjectCheckerTests → Unit (in-process ValueObjectChecker; 3 [Fact], classLine=11; Standard-Insert; kein BOM, CRLF+TrNL, #nullable enable Z.1)"
    source: "konzept.md §Wie Schritt 2"
  - id: item-12
    title: "WpfCodeBehindTests → Unit (in-process Wpf-Code-Behind-Check; 8 [Fact], classLine=13; Standard-Insert; **BOM-tragend** — Byte-Scan vorher/nachher, CRLF+TrNL, #nullable enable Z.1)"
    source: "konzept.md §Wie Schritt 2"
  - id: item-13
    title: "AutoFixerTests → Unit (in-process LinterAutoFixer.FixAsync mit AdhocWorkspace + Raw-String-Test-Sources; 4 [Fact], classLine=22; **XML-Doc-Variante (3-Schichten):** // @covers Z.17, Leerzeile Z.18, /// <summary>…</summary> Z.19-21, public sealed class Z.22 — Trait zwischen Z.21 und Z.22, class verschiebt sich auf Z.23; kein BOM, CRLF+TrNL, #nullable enable Z.1 — Heavyweight 7204 Bytes, komplexester Trait-Platzierungs-Fall im Core/-Teil des Batches)"
    source: "konzept.md §Wie Schritt 2; Code-Verifikation Planer-Schritt-2 (XML-Doc-Variante analog step-009 `DeveloperExperienceTests` und step-008 `RuleLegendRegistryTests`)"
  - id: item-14
    title: "ClassInfoCollectorTests → Unit (in-process ClassInfoCollector; 2 [Fact], classLine=12; Standard-Insert; kein BOM, CRLF+TrNL, #nullable enable Z.1)"
    source: "konzept.md §Wie Schritt 2"
  - id: item-15
    title: "CompoundSuppressionEvaluatorTests → Unit (in-process CompoundSuppression-Evaluator mit Code-Generator für TestSources; 16 [Fact], classLine=10; Standard-Insert; kein BOM, CRLF+TrNL, #nullable enable Z.1 — **Spezialfall:** 2. größte Datei im Core/-Teil 11843 Bytes, kein Verbatim-Block mit [Fact]-Verschachtelung)"
    source: "konzept.md §Wie Schritt 2"
  - id: item-16
    title: "CompoundSuppressionIntegrationTests → Unit (in-process CompoundSuppression mit Code-Generator für TestSources via StringBuilder; 12 [Fact], classLine=14; Standard-Insert; **NAMENS-SONDERFALL:** Klassenname enthält \"Integration\" aber **trotzdem Unit** — 0 Subprozess-Marker (Process.Start/CliProcessRunner/IClassFixture/SubprocessConcurrencyGate/McpTestClient/Program.Main), rein in-process Code-Generierung; **Heuristik-Bestätigung Punkt 2:** Subprozess-Marker-Check überschreibt Namens-Heuristik; kein BOM, CRLF+TrNL, #nullable enable Z.1 — **größte Datei im Batch 16668 Bytes**)"
    source: "konzept.md §Wie Schritt 2; Heuristik aus step-002 §Klassifikations-Heuristik Punkt 2 (Subprozess-Marker-Check)"
  - id: item-17
    title: "ControlFlowResilienceTests → Unit (in-process Control-Flow-Resilience-Checks; 16 [Fact], classLine=10; Standard-Insert; **BOM-tragend** — Byte-Scan vorher/nachher, CRLF+TrNL, #nullable enable Z.1 — Heavyweight 13111 Bytes, einzige BOM-tragende Datei im Core/-Teil des Batches)"
    source: "konzept.md §Wie Schritt 2"
  - id: item-18
    title: "DiffImpactAnalyzerTests → Unit (in-process DiffImpactAnalyzer.ParseGitDiffHunks mit Raw-String-Diff-Input; 1 [Fact], classLine=14; **XML-Doc-Variante (3-Schichten):** // @covers Z.9, Leerzeile Z.10, /// <summary>…</summary> Z.11-13, public sealed class Z.14 — Trait zwischen Z.13 und Z.14, class verschiebt sich auf Z.15; kein BOM, CRLF+TrNL, #nullable enable Z.1 — kleinerer XML-Doc-Variante-Fall im Core/-Teil, aber komplexester Trait-Platzierungs-Fall für 1-Fact-Klasse)"
    source: "konzept.md §Wie Schritt 2; Code-Verifikation Planer-Schritt-2"
  - id: item-19
    title: "LinterAnalyzerTests → Unit (in-process LinterAnalyzer mit Pipeline-Tests; 19 [Fact], classLine=10; Standard-Insert; kein BOM, CRLF+TrNL, **#nullable enable FEHLT** — TD-004-Inhomogenität für `Output/` betrifft `LinterAnalyzerTests` NICHT (anderer Ordner), aber Datei gehört zur 1/20-Minderheit ohne Direktive; **Trait-Insertion darf die Direktive nicht hinzufügen** (out of scope, würde Datei-Regel verändern) — Heavyweight 15565 Bytes, größte Datei im Core/-Teil des Batches)"
    source: "konzept.md §Wie Schritt 2"
  - id: item-20
    title: "LinterEngineCacheTests → Unit (in-process LinterEngineCache; 2 [Fact], classLine=17; Standard-Insert; kein BOM, CRLF+TrNL, #nullable enable Z.1 — classLine höher als üblich wegen 14 using-Imports Z.3-16, Standard-Insert funktioniert unverändert)"
    source: "konzept.md §Wie Schritt 2"
created_by: planer  # planer | orchestrator (nur bei mechanischem Korrektur-Transkript ohne Ermessen, siehe ../spec.md §6.2.1)
created_by_model: MiniMax-M3
created_by_model_knowledge_cutoff: 2026-01
created_at: 2026-08-08T09:00:00+02:00
related_to: []  # Pointer auf andere step-NNN (Task-interne Abhängigkeiten) oder auf step-review.md (Fix-Modus) — nie Fakten cachen, nur verweisen. Siehe ../spec.md §10.6. Nicht zu verwechseln mit `corrects` oben (eigene, budget-relevante Semantik).
---

# Step 011: Category-Traits für Core/Checkers-Rest und Core-Anfang nachziehen (Batch 10, Mega-Batch)

## Bezug

- **Task:** `flaky-and-test-performance`
- **Epic:** `EPIC-02` aus `roadmap.md` — Category-Traits flächendeckend
  nachziehen. Zehnter von N Batches; **erster Mega-Batch** dieses Tasks
  (zweiter folgt voraussichtlich in step-012 für den `Core/`-Rest) — der
  gelockerte `max_batch_items: 20` aus `config.md` (vom 2026-08-08)
  erlaubt das Bündeln der ursprünglich geplanten `Core/Checkers/`-Rest-
  Schritte (8+4 = 12 Klassen, M–W) **plus** der ersten 8 `Core/`-Klassen
  (A–`LinterEngineCacheTests`) in **einem** Step.
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
  **`step-010` (EPIC-02 Batch 9, `Core/Checkers/` Teil 1/3,
  8 Klassen A–`MethodParameterCountAccessibility`, approved,
  Commit `44956b7`)**. Die neun vorherigen Batches lieferten die
  etablierte Klassifikations-Heuristik (Subprozess-Marker = Integration;
  sonst Unit), die Trait-Syntax-Konvention (`[Trait("Category", "Unit")]`,
  CamelCase-Großbuchstabe), die Trait-Platzierungs-Bibliothek
  (Standard-Insert, `// @covers`-Block-Insert, XML-Doc-Variante,
  additive method-level Traits), die Heuristik-Punkte 1–8
  (Klassen-Homogenität → Klassen-Trait; bestehende Traits
  respektieren/additiv ergänzen; `null!` als Edge-Input; Klassen-Trait
  additiv zu bestehenden method-level Traits bei homogenen Klassen;
  Hypothesen-Auflösungs-Pflicht für offene "möglicherweise…"-Annotationen
  in der CodeMap; **Helper-Klassen ohne Testmethoden sind keine
  Testklassen**; **BOM-Inhomogenität in `Configuration/` als TD-005
  elevated**; **BOM-Inhomogenität in `Core/Checkers/` als TD-006
  elevated**), und die DoD-Struktur (Build grün, Voll-Test grün,
  Unit-Filter grün, Integration-Filter best-effort, Self-Lint `OK`,
  numerische Plausibilitätsprüfung mit
  String-Literal-`[Fact]`-Ausschluss-Methodik aus step-009, konkreter
  Subject-Vorschlag mit exakter Längen-Angabe).
- **Schnitt-Entscheidung (20 Klassen in 1 Batch, am
  `max_batch_items: 20`-Deckel):** der ursprüngliche
  `Core/Checkers/`-Plan aus step-010 sah 8+8+4 = 3 Batches vor (auf dem
  alten 8-Item-Deckel basierend). Mit dem gelockerten 20-Item-Deckel aus
  `config.md` (2026-08-08) faltet sich der Plan wie folgt:
  - **step-011 (= dieser Plan, Mega-Batch 1/2 für Checkers+Core):**
    12 verbleibende `Core/Checkers/` (M–W, vereint die ursprünglichen
    8+4 = step-011+step-012) + 8 erste `Core/` (A–`LinterEngineCacheTests`)
    = **20 Klassen total, am Deckel**.
  - **step-012 (geplant, Mega-Batch 2/2 für Core/):**
    11 verbleibende `Core/` (`LinterEngineTests`–`ViolationDescriptionTests`)
    = **1 Batch** (11 ≤ 20-Item-Cap).
  - **Was nicht in step-011/012 enthalten ist und Folge-Steps braucht:**
    `Maps/` + `Maps/Skeleton/` (6 Klassen), `Mcp/Tools/` (17 Klassen,
    2–3 Batches), `Mcp/` (19 Klassen, gemischt — Subprozess-Anteil),
    `Baseline/` (10 Klassen, gemischt), `Commands/` (17 Klassen, stark
    gemischt — pro-Methode-Tagging für `McpServerCommandTests` als
    eigener Step nötig), `Cli/` (6 Klassen, gemischt).
- **Schnitt-Wahl-Begründung (20 statt 19 oder 12):**
  - **Warum 20 (Orchestrator-Option A) statt 19 (Option B):** die
    8 ersten `Core/`-Klassen sind moderat komplex (max 19 Facts in
    `LinterAnalyzerTests`, 2 XML-Doc-Varianten in `AutoFixerTests` und
    `DiffImpactAnalyzerTests` — beide Muster aus step-009
    `DeveloperExperienceTests` bereits etabliert). Keine unerwarteten
    Risiken erkennbar (alle 20 Klassen sind homogen Unit, 0/20 mit
    Subprozess-Marker, 0/20 mit String-Literal-`[Fact]`-Verschachtelung,
    19/20 mit `#nullable enable`, 6/20 mit BOM). 1 Reserve-Item bringt
    keinen Mehrwert, der Orchestrator hat explizit "bündle größer"
    gefordert.
  - **Warum 20 statt 12 (Orchestrator-Option C):** 12 Checkers allein
    würden 8 Reserve-Items verschwenden und einen zweiten Step nur für
    die 4 Checkers `SwitchDispatcher`–`WpfCodeBehind` bedeuten — reiner
    Overhead, da diese 4 Klassen technisch identisch zu den 8 Klassen
    M–`SilentCatchAllowedTypes` sind (gleiche Subprozess-Marker-Lage,
    gleiche Standard-Insert-Mechanik, gleiche BOM-Verteilung 30 %).
  - **Anti-Loop-Check** gegen `codemap.md` (Stand step-010-Doku-Commit):
    die `Core/Checkers/`-Zeile in der Sektion "Test-Verzeichnisse —
    geplant für EPIC-02-Folge-Batches" trägt den Vermerk "27 Klassen
    total, davon 7 bereits getaggt; 20 ungetaggte Klassen in 3
    alphabetischen Batches 8+8+4 = step-010 + step-011 + step-012" —
    der **8+4-Anteil** (ursprünglich step-011+step-012 = 12 Klassen) wird
    durch diesen Plan zu **einem** Schritt konsolidiert (Mega-Batch),
    die `Core/`-Zeile dokumentiert weiterhin "19 Klassen; rein Unit,
    mehrere Batches". **Keine** bestehende Entscheidung widerspricht
    diesem Plan — der Coder aktualisiert die `Core/Checkers/`-Zeile im
    Doku-Commit auf "20 ungetaggte Klassen in 1 Mega-Batch step-011
    (12 Klassen M–W) + 8 erste `Core/` in step-011" und die `Core/`-Zeile
    auf "step-011 nimmt erste 8 Klassen (A–LinterEngineCache) als
    Mega-Batch-Anteil; step-012 verbleibend mit 11 Klassen".

## Aktueller Projektzustand (JIT-Kontext)

Beim Lesen der 20 Zieldateien + Inventur des `Core/Checkers/`- und
`Core/`-Ordners vorgefunden (relevant für step-011):

- **Ordner-Inventar `Core/Checkers/` (27 `.cs`-Dateien, davon 7
  Test-Klassen bereits getaggt — Stand step-010-Doku-Commit, durch
  Refactoring-Commits `8cae25c` und `d744dc9` aus dem
  `[codegraph-mcp-finish]`-Feature-Branch):**
  - **Mit `[Trait("Category", "Unit")]` auf Klassen-Ebene bereits
    getaggt (7, NICHT in step-011-Scope):**
    `MaxInheritanceDepthTests.cs:13`, `MaxConstructorDependenciesTests.cs:13`,
    `MaxBoolParameterCountTests.cs:13`, `MaxPublicMembersPerTypeTests.cs:13`,
    `MaxSwitchArmsTests.cs:16`, `NamespaceDirectoryMappingTests.cs:15`,
    `NestedTypesCheckerTests.cs:13` — alle regex-verifiziert per
    `grep -nE '\[Trait\('`.
  - **In step-010 getaggt (8, bereits done — nicht erneut):**
    `AsciiIdentifiersTests`, `AsyncVoidCheckerTests`,
    `BlockingTaskCheckerTests`, `CouplingSemanticTests`,
    `DynamicTypeCheckerTests`, `LinqChainLengthCheckerTests`,
    `MaxPartialClassFilesTests`, `MethodParameterCountAccessibilityTests`.
  - **In step-011 (12 Klassen M–W, dieser Plan — vervollständigt
    `Core/Checkers/`-Ordner auf 27/27 = 100 %):**
    `MethodParameterCountIgnoreTypePrefixesTests`,
    `MethodParameterCountOverrideTests`, `MiddleManCheckerTests`,
    `NamespaceCouplingCheckerTests`, `NamingCheckerTests`,
    `PhantomDependencyCheckerTests`, `SealedClassCheckerTests`,
    `SilentCatchAllowedTypesTests`, `SwitchDispatcherDetectorTests`,
    `UiFileSeparationCheckerTests`, `ValueObjectCheckerTests`,
    `WpfCodeBehindTests`.
- **Ordner-Inventar `Core/` (19 `.cs`-Dateien, alle 19 ungetaggt, alle
  19 homogen Unit per Subprozess-Marker-Verifikation):**
  - **In step-011 (8 Klassen A–`LinterEngineCacheTests`):**
    `AutoFixerTests`, `ClassInfoCollectorTests`,
    `CompoundSuppressionEvaluatorTests`,
    `CompoundSuppressionIntegrationTests`, `ControlFlowResilienceTests`,
    `DiffImpactAnalyzerTests`, `LinterAnalyzerTests`,
    `LinterEngineCacheTests`.
  - **In step-012 (11 Klassen, geplant):** `LinterEngineTests`,
    `NamespaceFilterTests`, `NullCoalescingInitializerClassifierTests`,
    `PlaybookGeneratorRound2Tests`, `ResultPatternNamespaceTests`,
    `RuleRegistryTests`, `ScopeImmutabilityTests`,
    `StaticTestSentinelExemptionTests`, `TestCoverageResolverTests`,
    `TestProjectDetectorSuffixTests`, `ViolationDescriptionTests`.
- **step-011-Klassen — Detail-Inventar (20 Klassen, alle homogen
  Unit):**

  **12 `Core/Checkers/`-Klassen M–W:**

  | Datei                                       | classLine | Facts | Theory | InlineData | BOM  | Nullable | Test-Cases |
  |---------------------------------------------|----------:|------:|-------:|-----------:|:----:|:--------:|-----------:|
  | `MethodParameterCountIgnoreTypePrefixesTests.cs` |    12 |   5 |      0 |          0 |  ✓   |    ✓     |          5 |
  | `MethodParameterCountOverrideTests.cs`           |    12 |  12 |      0 |          0 |  ✓   |    ✓     |         12 |
  | `MiddleManCheckerTests.cs`                       |    11 |   9 |      0 |          0 |  ✗   |    ✓     |          9 |
  | `NamespaceCouplingCheckerTests.cs`               |    11 |   1 |      0 |          0 |  ✗   |    ✓     |          1 |
  | `NamingCheckerTests.cs`                          |    10 |   3 |      0 |          0 |  ✗   |    ✓     |          3 |
  | `PhantomDependencyCheckerTests.cs`               |    11 |   1 |      0 |          0 |  ✗   |    ✓     |          1 |
  | `SealedClassCheckerTests.cs`                     |    11 |   5 |      0 |          0 |  ✗   |    ✓     |          5 |
  | `SilentCatchAllowedTypesTests.cs`                |    12 |   4 |      0 |          0 |  ✓   |    ✓     |          4 |
  | `SwitchDispatcherDetectorTests.cs`               |    12 |   7 |      0 |          0 |  ✓   |    ✓     |          7 |
  | `UiFileSeparationCheckerTests.cs`                |    14 |  19 |      4 |         27 |  ✗   |    ✓     |     **46** |
  | `ValueObjectCheckerTests.cs`                     |    11 |   3 |      0 |          0 |  ✗   |    ✓     |          3 |
  | `WpfCodeBehindTests.cs`                          |    13 |   8 |      0 |          0 |  ✓   |    ✓     |          8 |
  | **Summe Facts**                                  |       |  77 |      4 |         27 |      |          |    **104** |

  **8 `Core/`-Klassen A–`LinterEngineCacheTests`:**

  | Datei                                       | classLine | Facts | Theory | InlineData | BOM  | Nullable | Test-Cases |
  |---------------------------------------------|----------:|------:|-------:|-----------:|:----:|:--------:|-----------:|
  | `AutoFixerTests.cs`                              |    22 |   4 |      0 |          0 |  ✗   |    ✓     |          4 |
  | `ClassInfoCollectorTests.cs`                     |    12 |   2 |      0 |          0 |  ✗   |    ✓     |          2 |
  | `CompoundSuppressionEvaluatorTests.cs`           |    10 |  16 |      0 |          0 |  ✗   |    ✓     |         16 |
  | `CompoundSuppressionIntegrationTests.cs`         |    14 |  12 |      0 |          0 |  ✗   |    ✓     |         12 |
  | `ControlFlowResilienceTests.cs`                  |    10 |  16 |      0 |          0 |  ✓   |    ✓     |         16 |
  | `DiffImpactAnalyzerTests.cs`                     |    14 |   1 |      0 |          0 |  ✗   |    ✓     |          1 |
  | `LinterAnalyzerTests.cs`                         |    10 |  19 |      0 |          0 |  ✗   |    ✗     |         19 |
  | `LinterEngineCacheTests.cs`                      |    17 |   2 |      0 |          0 |  ✗   |    ✓     |          2 |
  | **Summe Facts**                                  |       |  72 |      0 |          0 |      |          |     **72** |

  **Gesamt: 149 Facts + 4 Theory + 27 InlineData = 176 Test-Cases** in
  20 Klassen.

  **Beobachtungen (gemäß etablierter Heuristik):**
  - **Alle 20 Klassen homogen Unit** — 0/20 mit Subprozess-Marker
    (`Process\.Start|CliProcessRunner|IClassFixture|SubprocessConcurre
    ncyGate|McpTestClient|Program\.Main`, regex-verifiziert pro Datei
    = 0/0/0/0/0/0/0/0). Damit überschreitet die Subprozess-Marker-
    Heuristik (Punkt 2 aus step-002) die Namens-Heuristik für
    `CompoundSuppressionIntegrationTests` (Klassenname enthält
    "Integration", aber 0 Subprozess-Marker → **Unit**).
  - **BOM-Verteilung 6/20 mit BOM (30 %), 14/20 ohne (70 %):** die
    step-010-Hypothese "10/27 = 37 % mit BOM in `Core/Checkers/`"
    bestätigt sich im 12-Klassen-Restbatch: 5/12 mit BOM
    (`MethodParameterCountIgnoreTypePrefixes`,
    `MethodParameterCountOverride`, `SilentCatchAllowedTypes`,
    `SwitchDispatcher`, `WpfCodeBehind`) + 1/8 Core/-Datei mit BOM
    (`ControlFlowResilienceTests`) = 6/20. **Heuristik-Punkt 8
    (TD-006) bleibt offen** — Konsolidierung out of scope step-011.
  - **EOL uniform CRLF** in allen 20 Dateien (CR-Zahl = LF-Zahl
    verifiziert, kein gemischter Status) — Standard-Edit-Tool reicht
    für EOL-Erhaltung. **TD-003 (LF-only `McpLintConsoleTests.cs` in
    `Output/`) betrifft step-011 NICHT** — beide Ordner uniform CRLF.
  - **Trailing-NL: alle 20 mit Trailing-NL** (letztes Byte = LF) —
    Standard-Edit-Tool reicht.
  - **`#nullable enable` am Dateianfang: 19/20 mit, 1/20 ohne** (nur
    `LinterAnalyzerTests.cs` ohne — Z. 1 = `using System;`).
    `LinterAnalyzerTests.cs` ist die einzige Datei im step-011-Batch
    ohne die Direktive. **Heuristik-Hinweis:** die
    `AiNetLinter.Tests`-Profil-Overrides (analog zu
    `EnforceSealedClasses` siehe `AiNetLinter.mdc:83`) heben die
    `EnforceNullableEnable`-Regel wahrscheinlich für `*.Tests` auf
    (analog TD-004-Beobachtung in `Output/`); die fehlende Direktive
    in `LinterAnalyzerTests.cs` ist **kein Build-Error** und damit
    kein step-011-Problem. **Wichtig:** der Coder darf die Direktive
    **nicht** im step-011-Trait-Insert hinzufügen, weil das eine
    Datei-Regel-Veränderung wäre (out of scope für diesen Step).
  - **String-Literal-`[Fact]`-Vorkommen (NITPICK-Linie aus step-009
    NITPICK):** alle 20 Dateien per PowerShell-Roh-String-Scan
    geprüft (`[Fact]` innerhalb `"""…"""`-Blöcke oder String-Literal-
    Zeilen mit `"`): **0/20 Treffer** — keine Datei im step-011-Batch
    verschachtelt `[Fact]` in einem String-Literal. Damit ist die
    Methoden-Inventur (regex-basiert) **gleich** der
    Test-Case-Inventur (kein String-Literal-Diskrepanz-Faktor
    anzuwenden), im Gegensatz zu step-009
    `AgentFeaturesTests.cs:241` (16 Planer-Count, 15 echte
    xUnit-Tests, −1).
  - **Bestehende Trait-Verteilung:** 0/20 Klassen mit bestehendem
    `[Trait(`-Attribut (regex-verifiziert per `grep -cE '\[Trait\('`
    pro Datei = 0/0/0/…/0). Alle 20 Klassen sind "jungfräulich" —
    keine Vorab-Klassifikation zu respektieren, keine method-level
    Traits additiv zu ergänzen. Reiner Klassen-Trait-Insert.
  - **Trait-Platzierungs-Bibliothek vollständig ausreichend:**
    - **Standard-Insert (18 Klassen):** alle 12 `Core/Checkers/` +
      6 `Core/` (`ClassInfoCollectorTests`,
      `CompoundSuppressionEvaluatorTests`,
      `CompoundSuppressionIntegrationTests`,
      `ControlFlowResilienceTests`, `LinterAnalyzerTests`,
      `LinterEngineCacheTests`).
    - **XML-Doc-Variante (3-Schichten: `// @covers` + Leerzeile +
      XML-Doc + class), 2 Klassen:** `AutoFixerTests.cs` (Trait
      zwischen Z. 21 `</summary>` und Z. 22 class, class → Z. 23),
      `DiffImpactAnalyzerTests.cs` (Trait zwischen Z. 13
      `</summary>` und Z. 14 class, class → Z. 15). Beide analog
      step-009 `DeveloperExperienceTests:23-32` und step-008
      `RuleLegendRegistryTests` (Etablierte XML-Doc-Variante).
    - **`// @covers`-Block-Insert: 0 Klassen** in diesem Batch
      (keine `// @covers`-Marker ohne gleichzeitig vorhandene
      XML-Doc — die `// @covers` in `AutoFixerTests` und
      `DiffImpactAnalyzerTests` sind Teil der 3-Schichten-XML-Doc-
      Variante).
    - **Hinweis `UiFileSeparationCheckerTests:14`:** die Klasse
      implementiert `: IDisposable` (zusätzlich zum Standard-Pattern).
      Standard-Insert funktioniert unverändert (Trait wird vor der
      `public sealed class …`-Deklaration eingefügt, Interface-
      Deklaration ist Teil der class-Signatur und bleibt unangetastet).
- **`Core/`-Sonderfall `CompoundSuppressionIntegrationTests.cs` (16):
  Klassenname enthält "Integration", aber Unit per Heuristik.** Verifiziert:
  - **Subprozess-Marker:** 0/0/0/0/0/0 = 0 Treffer pro Marker-Kategorie
    (`Process\.Start|CliProcessRunner|IClassFixture|SubprocessConcurre
    ncyGate|McpTestClient|Program\.Main`).
  - **Klassenrumpf:** enthält eine `GenerateMethodCode(int lineCount,
    int parameterCount, int cc)`-Methode (Z. 16-…) + `StringBuilder`-
    basierte Test-Source-Generierung mit `sb.AppendLine` für
    `#nullable enable`, `public class TestClass {`, Parameter-Liste und
    CC-Branches (Z. 19-…) — alles in-process String-Konstruktion,
    keine externe Subprozess-Invokation, keine `McpTestClient`-
    Verwendung, keine `IClassFixture`.
  - **Konsequenz:** der Klassenname ist irreführend (historisch
    entstanden, vermutlich während des `[codegraph-mcp-finish]`-
    Features, wo "Integration" lose verwendet wurde für
    "umfassendere Compound-Suppression-Szenarien"); die
    Klassifikations-Heuristik (Punkt 2) sagt **klar Unit**, weil
    kein Subprozess involviert ist. Der Coder taggt als `Unit`.
  - **Risiko-Hinweis:** die `AiNetLinterRichtlinien.mdc` §5
    "Symptom-Fixing verboten" trifft hier **nicht** zu (wir ändern
    weder den Klassennamen noch die Test-Logik — wir setzen nur ein
    additives Attribut). Der Coder dokumentiert die
    Namens-Heuristik-Override-Begründung im `step-result.md`
    §"Beobachtungen".
- **`UiFileSeparationCheckerTests` Spezialfall (10):** 4 `[Theory]`-
  Methoden (Z. 206, 224, 292, 301) mit zusammen 27 `[InlineData]`-
  Einträgen (12+8+3+4 = 27) ergeben 27 zusätzliche Test-Cases
  zusätzlich zu den 19 `[Fact]`-Methoden = **46 Test-Cases zur
  Laufzeit** aus **23 Methoden** (19 Facts + 4 Theories). Der
  Klassen-Trait erfasst alle 46 Cases via xUnit-Vererbung — analog
  step-008 `RuleLegendRegistryTests` (8 Theory+56 InlineData=221
  Cases, alle erfasst vom Unit-Klassen-Trait). **Numerische
  Plausibilität:** Methoden-Inventar 23 ≠ Test-Case-Inventar 46
  (Diskrepanz +23, kommt von 4 `[Theory]`-Methoden mit 27
  `[InlineData]`-Einträgen). Der Coder dokumentiert **beide** Zahlen
  im `step-result.md` §"Numerische Plausibilität" und gleicht sie
  gegen den `dotnet test --filter "Category=Unit"`-Lauf-Delta ab.
- **`AutoFixerTests` & `DiffImpactAnalyzerTests` (13, 18) — XML-Doc-
  Variante:** beide haben 3-Schichten-Struktur (`// @covers` + Leerzeile
  + XML-Doc `/// <summary>…</summary>` + class). Der Trait wird
  zwischen `</summary>` und class eingefügt — kein Eingriff in die
  `// @covers`-Zeilen, kein Eingriff in die XML-Doc-Zeilen. Nur die
  class-Zeile verschiebt sich um 1 nach unten. Die XML-Doc-Variante
  ist seit step-008/009 etabliert, keine neue Bibliothek-Erweiterung
  nötig.
- **Numerische Plausibilität (Plan-DoD-Verifikation):**
  - **Methoden-Inventar pro Datei (regex-basiert per
    `Select-String -Pattern '\[(Fact|Theory)\]'`):** 20 Klassen
    ergeben **149 Facts + 4 Theories = 153 Methoden**.
  - **Test-Case-Inventar pro Datei (regex-basiert, mit
    String-Literal-Ausschluss):** 20 Klassen ergeben **149 Facts +
    27 InlineData-Expansionen = 176 Test-Cases** (alle
    `[Theory]`+`[InlineData]`-Expansions manuell verifiziert:
    `UiFileSeparationCheckerTests` Z. 206-219 Theory#1 mit 12
    InlineData, Z. 224-236 Theory#2 mit 8, Z. 292-299 Theory#3 mit
    3, Z. 301-309 Theory#4 mit 4 = 27 InlineData, alle 4 Theories
    in der gleichen Klasse).
  - **Diskrepanz Methoden (153) vs. Test-Cases (176) = +23** —
    kommt **ausschließlich** aus `UiFileSeparationCheckerTests`
    (4 Theories mit 27 InlineData = +23 Cases jenseits der 4
    Methoden, zusätzlich zu den 19 Facts). Der Coder dokumentiert
    **beide** Zahlen (regex-basierte Methoden-Zählung pro Datei UND
    tatsächlicher `dotnet test --filter "Category=Unit"`-Lauf-Wert)
    und gleicht sie gegen die Planer-Prognose ab. Bei abweichendem
    Delta ist **zwingend** die String-Literal-`[Fact]`-Methodik aus
    step-009 NITPICK anzuwenden (Brutto vs. Netto-Count, [Fact] in
    String-Literalen ausschließen).
  - **Filter-Delta step-011:** Unit steigt um **+176**, Integration
    unverändert (+0), Total unverändert (+0).
  - **Erwarteter Unit-Filter nach step-011:**
    706 (Stand nach step-010) + 176 = **882**.
  - **Integration bleibt 113, Total bleibt 1325.**
- **Klassen-Deklarationen — Trait-Platzierungs-Varianten**
  (verifiziert per `grep -nE 'public sealed class|/// <summary>|
  // @covers'` über alle 20 Dateien):
  - **Standard-Insert zwischen `namespace …;` und
    `public sealed class …`** (18 Klassen — Details in der Item-
    Liste im Frontmatter; Klassen-Zeile verschiebt sich um +1 nach
    unten für alle 18 Dateien).
  - **XML-Doc-Variante zwischen `</summary>` und
    `public sealed class …`** (2 Klassen): `AutoFixerTests.cs:22`
    und `DiffImpactAnalyzerTests.cs:14` (siehe Items 13 und 18 im
    Frontmatter für die exakten 3-Schichten-Positionen).
- **EOL-/BOM-/Trailing-NL-Status** (verifiziert per PowerShell-Byte-
  Check über alle 20 step-011-Dateien):

  | Datei                                       | BOM  |    CR |    LF | TrNL | Erste 3 Bytes          | Bytes  |
  |---------------------------------------------|:----:|------:|------:|:----:|------------------------|-------:|
  | `MethodParameterCountIgnoreTypePrefixesTests.cs` |  ✓   |   164 |   164 |  ✓  | `EF BB BF` (BOM)       |  6035 |
  | `MethodParameterCountOverrideTests.cs`           |  ✓   |   264 |   264 |  ✓  | `EF BB BF` (BOM)       |  9974 |
  | `MiddleManCheckerTests.cs`                       |  ✗   |   400 |   400 |  ✓  | `23 6E 75` (`#nu`)     | 13466 |
  | `NamespaceCouplingCheckerTests.cs`               |  ✗   |    50 |    50 |  ✓  | `23 6E 75` (`#nu`)     |  1443 |
  | `NamingCheckerTests.cs`                          |  ✗   |    93 |    93 |  ✓  | `23 6E 75` (`#nu`)     |  3699 |
  | `PhantomDependencyCheckerTests.cs`               |  ✗   |    36 |    36 |  ✓  | `23 6E 75` (`#nu`)     |  1080 |
  | `SealedClassCheckerTests.cs`                     |  ✗   |    95 |    95 |  ✓  | `23 6E 75` (`#nu`)     |  3428 |
  | `SilentCatchAllowedTypesTests.cs`                |  ✓   |   142 |   142 |  ✓  | `EF BB BF` (BOM)       |  4572 |
  | `SwitchDispatcherDetectorTests.cs`               |  ✓   |   248 |   248 |  ✓  | `EF BB BF` (BOM)       |  8547 |
  | `UiFileSeparationCheckerTests.cs`                |  ✗   |   324 |   324 |  ✓  | `23 6E 75` (`#nu`)     | 13932 |
  | `ValueObjectCheckerTests.cs`                     |  ✗   |    59 |    59 |  ✓  | `23 6E 75` (`#nu`)     |  2380 |
  | `WpfCodeBehindTests.cs`                          |  ✓   |   203 |   203 |  ✓  | `EF BB BF` (BOM)       |  6940 |
  | `AutoFixerTests.cs`                              |  ✗   |   194 |   194 |  ✓  | `23 6E 75` (`#nu`)     |  7204 |
  | `ClassInfoCollectorTests.cs`                     |  ✗   |    54 |    54 |  ✓  | `23 6E 75` (`#nu`)     |  1719 |
  | `CompoundSuppressionEvaluatorTests.cs`           |  ✗   |   332 |   332 |  ✓  | `23 6E 75` (`#nu`)     | 11843 |
  | `CompoundSuppressionIntegrationTests.cs`         |  ✗   |   438 |   438 |  ✓  | `23 6E 75` (`#nu`)     | 16668 |
  | `ControlFlowResilienceTests.cs`                  |  ✓   |   436 |   436 |  ✓  | `EF BB BF` (BOM)       | 13111 |
  | `DiffImpactAnalyzerTests.cs`                     |  ✗   |    48 |    48 |  ✓  | `23 6E 75` (`#nu`)     |  1454 |
  | `LinterAnalyzerTests.cs`                         |  ✗   |   469 |   469 |  ✓  | `75 73 69` (`using`)   | 15565 |
  | `LinterEngineCacheTests.cs`                      |  ✗   |   199 |   199 |  ✓  | `23 6E 75` (`#nu`)     |  8003 |

  **Beobachtungen:**
  - **EOL-Inhomogenität: keine** — alle 20 Dateien **uniform CRLF**
    (CR-Zahl = LF-Zahl in allen 20 Dateien, kein gemischter Status).
    TD-003 (LF-only `McpLintConsoleTests.cs` in `Output/`) betrifft
    step-011 NICHT — beide Ordner uniform CRLF. Standard-Edit-Tool
    reicht für EOL-Erhaltung. **Trotzdem Stichproben-Pflicht im DoD
    verankert** (siehe §"Definition of Done"): bei 20 Dateien ist
    die Wahrscheinlichkeit, dass 1-2 abweichen, leicht erhöht; Coder
    scannt alle 20 Dateien per PowerShell-Byte-Scan (nicht nur eine
    Stichprobe).
  - **Trailing-NL: alle 20 Dateien mit Trailing-NL** (letztes Byte
    = LF) — Standard-Edit-Tool reicht.
  - **BOM-Inhomogenität: 6 von 20 mit BOM (30 %), 14 ohne (70 %).**
    - **MIT BOM:** `MethodParameterCountIgnoreTypePrefixesTests`,
      `MethodParameterCountOverrideTests`, `SilentCatchAllowedTypesTests`,
      `SwitchDispatcherDetectorTests`, `WpfCodeBehindTests` (5 aus
      `Core/Checkers/`) + `ControlFlowResilienceTests` (1 aus `Core/`).
      **6 BOM-tragende Dateien** im step-011-Batch.
    - **OHNE BOM:** 14 andere Dateien.
    - **Konsequenz für den Coder:** das Standard-Edit-Tool erhält
      die BOM in der Regel (Bytes vor und nach dem Edit sind
      identisch), aber der Coder **muss** für alle 6 BOM-tragenden
      Dateien explizit per
      `[System.IO.File]::ReadAllBytes(...)`-Scan **vor** und
      **nach** dem Edit verifizieren, dass die ersten 3 Bytes
      weiterhin `EF BB BF` sind. Falls das Standard-Edit-Tool die
      BOM überschreibt (z. B. durch "Datei komplett neu schreiben"
      statt "Zeile einfügen"), muss der Coder auf einen byte-genauen
      Python-Helper analog step-007 (`McpLintConsoleTests.cs`
      LF-only) umstellen.
  - **Pattern-Beobachtung:** die BOM-Verteilung 30 % in step-011
    bestätigt die step-010-Hypothese ("10/27 = 37 %" für
    `Core/Checkers/`) ungefähr (5/12 = 42 % im 12-Klassen-Restbatch,
    1/8 = 13 % in den 8 ersten `Core/`-Klassen — beide
    Unterverteilungen im 30-%-Bereich, was die step-010-Aggregat-
    Beobachtung stützt). **Heuristik-Punkt 8 (TD-006) bleibt
    offen** — Konsolidierung out of scope step-011 (kein
    `auto_fixable`-Anhängen, kein TD-Eintrag durch Planer).
- **String-Literal-`[Fact]`-Vorkommen (NITPICK-Linie aus
  step-009-Review):** alle 20 Dateien per PowerShell-Roh-String-Scan
  geprüft (`[Fact]` innerhalb `"""…"""`-Blöcke oder String-Literal-
  Zeilen mit `"`): **0/20 Treffer**. Damit ist die
  Methoden-Inventur (regex-basiert) **gleich** der
  Test-Case-Inventur (kein String-Literal-Diskrepanz-Faktor
  anzuwenden, im Gegensatz zu step-009
  `AgentFeaturesTests.cs:241`). Der Coder dokumentiert im
  `step-result.md` den `Select-String`-Brutto-Count pro Datei
  **und** den per `dotnet test --filter "Category=Unit"`
  verifizierten Netto-Filter-Delta, beide müssen 176 ergeben (oder
  eine Differenz explizit dokumentiert sein).
- **Subprozess-Marker im 20-Datei-Set** (regex-basiert per
  `grep -cE 'Process\.Start|CliProcessRunner|IClassFixture|Subpro
  cessConcurrencyGate|McpTestClient|Program\.Main'`): **0/0/…/0**
  über alle 20 Dateien — keine Klasse startet einen Subprozess.
  Alle 20 Klassen sind homogen **Unit**. Konsistent mit der
  etablierten Heuristik (Punkte 1–3) und der
  step-002/003/004/005/006/007/008/009/010-Bestätigung. **Wichtig:**
  die Namens-Heuristik ("Integration" im Klassennamen →
  Integration-Trait) wird durch den Subprozess-Marker-Check
  überschrieben — siehe `CompoundSuppressionIntegrationTests` (16).

## Intention

Alle 20 in diesem Plan gelisteten Testklassen (12 `Core/Checkers/`-Rest
M–W + 8 erste `Core/`-Klassen A–`LinterEngineCacheTests`) mit
`[Trait("Category", "Unit")]` auf Klassen-Ebene versehen. Dieser Step
schließt den **`Core/Checkers/`-Ordner vollständig ab** (alle
27 Klassen getaggt nach step-011) und startet den **`Core/`-Ordner**
(8 von 19 Klassen getaggt nach step-011, Rest in step-012).
**Konsolidierungs-Beitrag:** nach step-011 sind 27 + 8 = 35 Klassen
in den beiden Ordnern abgehakt, Restbestand `Core/` = 11 Klassen
(geplant für step-012 in einem 1-Batch-Schritt, da 11 ≤ 20-Item-Cap).
Der gelockerte `max_batch_items: 20` aus `config.md` (2026-08-08)
macht diesen Mega-Batch erst möglich — der ursprüngliche 8-Item-Cap
hätte 3+1 = 4 Schritte für diesen Inhalt erfordert (step-011 8
Checkers + step-012 4 Checkers + step-013 8 Core/ + step-014 11
Core/-Rest). Mit dem 20-Item-Cap reduziert sich das auf 2 Schritte
(step-011 dieser Plan + step-012 11 Core/-Rest), entsprechend dem
User-Wunsch "bündle größer".

## Konkrete Änderungen

**Bei `step_type: batch`** (siehe `../spec.md` §10.6): pro Item aus
der `items`-Liste im Frontmatter eine Unterüberschrift.

### item-01: `MethodParameterCountIgnoreTypePrefixesTests` → Unit — `src/AiNetLinter.Tests/Core/Checkers/MethodParameterCountIgnoreTypePrefixesTests.cs` (Zeile 12)

- **Was:** Direkt über `public sealed class
  MethodParameterCountIgnoreTypePrefixesTests` (Z. 12) eine Zeile
  `[Trait("Category", "Unit")]` einfügen. Kein XML-Doc, kein
  `// @covers`, kein `: IDisposable` auf der Klasse vorhanden — daher
  Standard-Insert.
- **Warum:** Klasse testet `MethodParameterCountChecker` mit
  Ignore-Type-Prefixes-Konfiguration rein in-process. 0
  Subprozess-Marker, 0 bestehende Traits, 5 `[Fact]`-Methoden.
  Konsistent mit Heuristik-Punkten 1–3 aus step-002.
- **BOM-Hinweis:** Datei ist **BOM-tragend** (`EF BB BF` als erste
  3 Bytes). Coder scannt `[System.IO.File]::ReadAllBytes(...)` **vor**
  und **nach** dem Edit, verifiziert dass die ersten 3 Bytes
  unverändert `EF BB BF` sind.

### item-02: `MethodParameterCountOverrideTests` → Unit — `src/AiNetLinter.Tests/Core/Checkers/MethodParameterCountOverrideTests.cs` (Zeile 12)

- **Was:** Standard-Insert zwischen `namespace …;` (Z. 10) und
  `public sealed class MethodParameterCountOverrideTests` (Z. 12).
  Class verschiebt sich auf Z. 13.
- **Warum:** 12 `[Fact]`-Methoden, 0 Subprozess-Marker, 0 bestehende
  Traits — homogen Unit.
- **BOM-Hinweis:** Datei ist **BOM-tragend** (`EF BB BF`). Byte-Scan
  vorher/nachher.

### item-03: `MiddleManCheckerTests` → Unit — `src/AiNetLinter.Tests/Core/Checkers/MiddleManCheckerTests.cs` (Zeile 11)

- **Was:** Standard-Insert zwischen `namespace …;` (Z. 9) und
  `public sealed class MiddleManCheckerTests` (Z. 11). Class auf Z. 12.
- **Warum:** 9 `[Fact]`-Methoden, 0 Subprozess-Marker. Die Klasse
  enthält Z. 13-19 eine `TestHelperTypes`-String-Konstante mit
  Verbatim-Test-Source für die Checker-Tests — **kein** `[Fact]`-
  Marker im Verbatim-Block (String-Literal-Scan bestätigt 0 Treffer),
  keine Mis-count-Risiko.
- **BOM-Hinweis:** kein BOM, Standard-Edit ausreichend.

### item-04: `NamespaceCouplingCheckerTests` → Unit — `src/AiNetLinter.Tests/Core/Checkers/NamespaceCouplingCheckerTests.cs` (Zeile 11)

- **Was:** Standard-Insert zwischen `namespace …;` (Z. 9) und
  `public sealed class NamespaceCouplingCheckerTests` (Z. 11). Class
  auf Z. 12.
- **Warum:** 1 `[Fact]`-Methode, 0 Subprozess-Marker — homogen Unit.
  Kleinste Datei im Checkers-Teil (1443 Bytes).
- **BOM-Hinweis:** kein BOM.

### item-05: `NamingCheckerTests` → Unit — `src/AiNetLinter.Tests/Core/Checkers/NamingCheckerTests.cs` (Zeile 10)

- **Was:** Standard-Insert zwischen `namespace …;` (Z. 8) und
  `public sealed class NamingCheckerTests` (Z. 10). Class auf Z. 11.
- **Warum:** 3 `[Fact]`-Methoden, 0 Subprozess-Marker. Die Klasse
  enthält 3 `"""…"""`-TripleQuoted-Blöcke als Test-Inputs für die
  Linter-Engine (raw strings mit Beispiel-Klassennamen) — **kein**
  `[Fact]`-Marker in diesen Blöcken (Planer-Scan bestätigt 0 Treffer
  auf `String-Fact`-Heuristik). 0 Mis-count-Risiko.
- **BOM-Hinweis:** kein BOM.

### item-06: `PhantomDependencyCheckerTests` → Unit — `src/AiNetLinter.Tests/Core/Checkers/PhantomDependencyCheckerTests.cs` (Zeile 11)

- **Was:** Standard-Insert zwischen `namespace …;` (Z. 9) und
  `public sealed class PhantomDependencyCheckerTests` (Z. 11). Class
  auf Z. 12.
- **Warum:** 1 `[Fact]`-Methode, 0 Subprozess-Marker — homogen Unit.
  Kleinste Datei im Batch (1080 Bytes).
- **BOM-Hinweis:** kein BOM.

### item-07: `SealedClassCheckerTests` → Unit — `src/AiNetLinter.Tests/Core/Checkers/SealedClassCheckerTests.cs` (Zeile 11)

- **Was:** Standard-Insert zwischen `namespace …;` (Z. 9) und
  `public sealed class SealedClassCheckerTests` (Z. 11). Class auf Z. 12.
- **Warum:** 5 `[Fact]`-Methoden, 0 Subprozess-Marker.
- **BOM-Hinweis:** kein BOM.

### item-08: `SilentCatchAllowedTypesTests` → Unit — `src/AiNetLinter.Tests/Core/Checkers/SilentCatchAllowedTypesTests.cs` (Zeile 12)

- **Was:** Standard-Insert zwischen `namespace …;` (Z. 10) und
  `public sealed class SilentCatchAllowedTypesTests` (Z. 12). Class
  auf Z. 13.
- **Warum:** 4 `[Fact]`-Methoden, 0 Subprozess-Marker.
- **BOM-Hinweis:** Datei ist **BOM-tragend** (`EF BB BF`). Byte-Scan
  vorher/nachher.

### item-09: `SwitchDispatcherDetectorTests` → Unit — `src/AiNetLinter.Tests/Core/Checkers/SwitchDispatcherDetectorTests.cs` (Zeile 12)

- **Was:** Standard-Insert zwischen `namespace …;` (Z. 10) und
  `public sealed class SwitchDispatcherDetectorTests` (Z. 12). Class
  auf Z. 13.
- **Warum:** 7 `[Fact]`-Methoden, 0 Subprozess-Marker.
- **BOM-Hinweis:** Datei ist **BOM-tragend** (`EF BB BF`). Byte-Scan
  vorher/nachher.

### item-10: `UiFileSeparationCheckerTests` → Unit — `src/AiNetLinter.Tests/Core/Checkers/UiFileSeparationCheckerTests.cs` (Zeile 14)

- **Was:** Standard-Insert zwischen `namespace …;` (Z. 12) und
  `public sealed class UiFileSeparationCheckerTests : IDisposable`
  (Z. 14). Class auf Z. 15. **Spezialfall: `: IDisposable`-Interface**
  ist Teil der class-Signatur — Standard-Insert funktioniert
  unverändert (Trait wird vor der `public sealed class …`-Deklaration
  eingefügt, Interface-Deklaration bleibt in der Signatur).
- **Warum:** 19 `[Fact]` + 4 `[Theory]` (Z. 206, 224, 292, 301) mit
  zusammen 27 `[InlineData]`-Einträgen (12+8+3+4 = 27) ergeben
  **46 Test-Cases zur Laufzeit** (Klassen-Trait erfasst alle 46
  Cases via xUnit-Vererbung). 0 Subprozess-Marker — homogen Unit
  trotz komplexer Test-Methodik.
- **BOM-Hinweis:** kein BOM.
- **Numerischer Hinweis:** Methoden-Inventar 23 ≠ Test-Case-Inventar
  46 (Diskrepanz +23, kommt von 4 `[Theory]`-Methoden mit 27
  `[InlineData]`-Einträgen). Coder dokumentiert im
  `step-result.md` §"Numerische Plausibilität" **beide** Zahlen.

### item-11: `ValueObjectCheckerTests` → Unit — `src/AiNetLinter.Tests/Core/Checkers/ValueObjectCheckerTests.cs` (Zeile 11)

- **Was:** Standard-Insert zwischen `namespace …;` (Z. 9) und
  `public sealed class ValueObjectCheckerTests` (Z. 11). Class auf Z. 12.
- **Warum:** 3 `[Fact]`-Methoden, 0 Subprozess-Marker.
- **BOM-Hinweis:** kein BOM.

### item-12: `WpfCodeBehindTests` → Unit — `src/AiNetLinter.Tests/Core/Checkers/WpfCodeBehindTests.cs` (Zeile 13)

- **Was:** Standard-Insert zwischen `namespace …;` (Z. 11) und
  `public sealed class WpfCodeBehindTests` (Z. 13). Class auf Z. 14.
- **Warum:** 8 `[Fact]`-Methoden, 0 Subprozess-Marker.
- **BOM-Hinweis:** Datei ist **BOM-tragend** (`EF BB BF`). Byte-Scan
  vorher/nachher.

### item-13: `AutoFixerTests` → Unit — `src/AiNetLinter.Tests/Core/AutoFixerTests.cs` (Zeile 22)

- **Was:** **XML-Doc-Variante (3-Schichten):** zwischen `</summary>`
  (Z. 21) und `public sealed class AutoFixerTests` (Z. 22) eine Zeile
  `[Trait("Category", "Unit")]` einfügen. Class verschiebt sich auf
  Z. 23. **Kein** Eingriff in die `// @covers LinterAutoFixer`-Zeile
  (Z. 17), **kein** Eingriff in die XML-Doc-Zeilen (Z. 19-21). Der
  Trait wird gemäß etablierter XML-Doc-Variante (siehe step-009
  `DeveloperExperienceTests:23-32`) **nach** dem XML-Doc, **vor** der
  class eingefügt.
- **Warum:** 4 `[Fact]`-Methoden, 0 Subprozess-Marker, homogen Unit
  trotz Heavyweight (7204 Bytes, 4 komplexe Test-Szenarien mit
  AdhocWorkspace + Raw-String-Test-Sources). Heavyweight im
  Core/-Teil des Batches.
- **BOM-Hinweis:** kein BOM.

### item-14: `ClassInfoCollectorTests` → Unit — `src/AiNetLinter.Tests/Core/ClassInfoCollectorTests.cs` (Zeile 12)

- **Was:** Standard-Insert zwischen `namespace …;` (Z. 10) und
  `public sealed class ClassInfoCollectorTests` (Z. 12). Class auf Z. 13.
- **Warum:** 2 `[Fact]`-Methoden, 0 Subprozess-Marker.
- **BOM-Hinweis:** kein BOM.

### item-15: `CompoundSuppressionEvaluatorTests` → Unit — `src/AiNetLinter.Tests/Core/CompoundSuppressionEvaluatorTests.cs` (Zeile 10)

- **Was:** Standard-Insert zwischen `namespace …;` (Z. 8) und
  `public sealed class CompoundSuppressionEvaluatorTests` (Z. 10).
  Class auf Z. 11.
- **Warum:** 16 `[Fact]`-Methoden, 0 Subprozess-Marker. Heavyweight
  (11843 Bytes, 2.-größte Datei im Core/-Teil des Batches), aber
  rein in-process String-/Code-Generierung.
- **BOM-Hinweis:** kein BOM.

### item-16: `CompoundSuppressionIntegrationTests` → Unit — `src/AiNetLinter.Tests/Core/CompoundSuppressionIntegrationTests.cs` (Zeile 14)

- **Was:** Standard-Insert zwischen `namespace …;` (Z. 12) und
  `public sealed class CompoundSuppressionIntegrationTests` (Z. 14).
  Class auf Z. 15.
- **Warum:** **NAMENS-SONDERFALL:** Klassenname enthält
  "Integration", aber die Klasse ist **trotzdem Unit** per
  Klassifikations-Heuristik (Punkt 2 aus step-002): 0
  Subprozess-Marker (verifiziert per
  `grep -cE 'Process\.Start|CliProcessRunner|IClassFixture|Subpro
  cessConcurrencyGate|McpTestClient|Program\.Main'` = 0/0/0/0/0/0).
  Die Klasse enthält eine `GenerateMethodCode(int, int, int)`-
  Methode mit `StringBuilder`-basierter Test-Source-Generierung —
  rein in-process, keine externe Subprozess-Invokation. 12 `[Fact]`-
  Methoden. Heavyweight (16668 Bytes, **größte Datei im
  step-011-Batch**).
- **BOM-Hinweis:** kein BOM.
- **Begründung Namens-Heuristik-Override:** die
  `AiNetLinterRichtlinien.mdc` §5 "Symptom-Fixing verboten" trifft
  hier **nicht** zu (wir ändern weder den Klassennamen noch die
  Test-Logik — wir setzen nur ein additives Attribut). Der Coder
  dokumentiert die Namens-Heuristik-Override-Begründung im
  `step-result.md` §"Beobachtungen". Heuristik-Punkt 2 (Subprozess-
  Marker-Check) hat Vorrang vor Namens-Heuristik — keine Ausnahme.

### item-17: `ControlFlowResilienceTests` → Unit — `src/AiNetLinter.Tests/Core/ControlFlowResilienceTests.cs` (Zeile 10)

- **Was:** Standard-Insert zwischen `namespace …;` (Z. 8) und
  `public sealed class ControlFlowResilienceTests` (Z. 10). Class auf
  Z. 11.
- **Warum:** 16 `[Fact]`-Methoden, 0 Subprozess-Marker. Heavyweight
  (13111 Bytes), einzige BOM-tragende Datei im Core/-Teil des
  Batches.
- **BOM-Hinweis:** Datei ist **BOM-tragend** (`EF BB BF`). Byte-Scan
  vorher/nachher.

### item-18: `DiffImpactAnalyzerTests` → Unit — `src/AiNetLinter.Tests/Core/DiffImpactAnalyzerTests.cs` (Zeile 14)

- **Was:** **XML-Doc-Variante (3-Schichten):** zwischen `</summary>`
  (Z. 13) und `public sealed class DiffImpactAnalyzerTests` (Z. 14)
  eine Zeile `[Trait("Category", "Unit")]` einfügen. Class
  verschiebt sich auf Z. 15. **Kein** Eingriff in die
  `// @covers DiffImpactAnalyzer`-Zeile (Z. 9), **kein** Eingriff in
  die XML-Doc-Zeilen (Z. 11-13). XML-Doc-Variante analog
  `AutoFixerTests` (item-13) und step-009 `DeveloperExperienceTests`.
- **Warum:** 1 `[Fact]`-Methode, 0 Subprozess-Marker — homogen Unit
  trotz komplexester Trait-Platzierung in einer 1-Fact-Klasse.
- **BOM-Hinweis:** kein BOM.

### item-19: `LinterAnalyzerTests` → Unit — `src/AiNetLinter.Tests/Core/LinterAnalyzerTests.cs` (Zeile 10)

- **Was:** Standard-Insert zwischen `namespace …;` (Z. 8) und
  `public sealed class LinterAnalyzerTests` (Z. 10). Class auf Z. 11.
- **Warum:** 19 `[Fact]`-Methoden, 0 Subprozess-Marker. Heavyweight
  (15565 Bytes, **größte Datei im Core/-Teil des Batches nach
  `CompoundSuppressionIntegrationTests`**). Rein in-process
  Pipeline-Tests.
- **Nullable-Hinweis:** Datei **OHNE** `#nullable enable` am
  Dateianfang (Z. 1 = `using System;`) — einzige Datei im
  step-011-Batch ohne Direktive (analog TD-004 für `Output/`, aber
  TD-005/TD-006 betreffen BOM, nicht Nullable). Der Coder **darf die
  Direktive NICHT im step-011-Trait-Insert hinzufügen**, weil das
  eine Datei-Regel-Veränderung wäre (out of scope für diesen Step).
  Heuristik-Hinweis: wahrscheinlich hebt das
  `AiNetLinter.Tests`-Profil `EnforceNullableEnable` analog zu
  `EnforceSealedClasses` auf — der `dotnet build` (TreatWarnings-
  AsErrors) läuft grün. Coder dokumentiert im `step-result.md`
  §"Beobachtungen" das Nullable-Fehlen als Out-of-Scope-Hinweis.
- **BOM-Hinweis:** kein BOM.

### item-20: `LinterEngineCacheTests` → Unit — `src/AiNetLinter.Tests/Core/LinterEngineCacheTests.cs` (Zeile 17)

- **Was:** Standard-Insert zwischen `namespace …;` (Z. 15) und
  `public sealed class LinterEngineCacheTests` (Z. 17). Class auf
  Z. 18. Die höhere `classLine` kommt von 14 `using`-Imports
  (Z. 3-16), Standard-Insert funktioniert unverändert.
- **Warum:** 2 `[Fact]`-Methoden, 0 Subprozess-Marker.
- **BOM-Hinweis:** kein BOM.

## Tests

Keine — die Klassifikation ist rein additiv (Attribut setzen). Existierende
Tests müssen **unverändert** grün bleiben. Validierung erfolgt über den
vollen `dotnet test`-Lauf in der Definition of Done (kein neuer Test,
kein geänderter Test).

## Definition of Done

- [ ] Alle 20 Items umgesetzt (je eine
  `[Trait("Category", "Unit")]`-Zeile an der in den Items
  spezifizierten Position; 18× Standard-Insert, 2× XML-Doc-Variante
  für `AutoFixerTests` und `DiffImpactAnalyzerTests`)
- [ ] **Bestehende Traits respektiert:** keine vorhandenen
  Trait-Attribute überschrieben oder entfernt (Trifft im Batch
  nicht zu, da 0/20 Klassen bestehende Traits tragen — als
  Plausibilitäts-Check zu verifizieren: nach dem Diff sollten in den
  20 step-011-Dateien jeweils genau 1 Klassen-Trait
  `[Trait("Category", "Unit")]` existieren.)
- [ ] **Build-Command** aus Tech-Stack-Notiz (`roadmap.md`) grün:
  `dotnet build`
- [ ] **Test-Command** aus Tech-Stack-Notiz grün: `dotnet test`
  (voller Lauf, alle 1325 Tests müssen weiterhin grün sein — keine
  Test-Logik wurde geändert)
- [ ] **Trait-Filter-Smoke-Test** (vom Coder zusätzlich durchführen):
  - `dotnet test --no-build --filter "Category=Unit"` → muss grün
    sein, **erwartete Test-Anzahl 882** (= 706 nach step-010 + 176
    step-011-Delta)
  - `dotnet test --no-build --filter "Category=Integration"` → muss
    grün sein, **erwartete Test-Anzahl 113** (unverändert)
  - Summe der beiden Filter = Total 1325 (unverändert)
- [ ] **Numerische Plausibilitätsprüfung** (NITPICK-Linie aus
  step-009, mit String-Literal-`[Fact]`-Ausschluss):
  - **Brutto-Methoden-Count pro Datei** per
    `Select-String -Pattern '\[(Fact|Theory)\]'` =
    Planer-Erwartung (149 Facts + 4 Theories = 153 Methoden über
    20 Dateien, mit `UiFileSeparationCheckerTests` als Spitzenreiter
    mit 19+4 = 23 Methoden).
  - **Brutto-Test-Case-Count pro Datei** (Facts +
    InlineData-Expansionen) = Planer-Erwartung (176 Test-Cases).
  - **`dotnet test --filter "Category=Unit"`-Delta** muss
    exakt +176 ergeben (entspricht 882 − 706 = 176).
  - **Diskrepanz Methoden (153) vs. Test-Cases (176) = +23** —
    kommt **ausschließlich** aus `UiFileSeparationCheckerTests`
    (4 Theories mit 27 InlineData = +23 Cases). Coder dokumentiert
    **beide** Zahlen im `step-result.md` §"Numerische Plausibilität"
    und gleicht sie gegen den Lauf-Delta ab.
  - Bei **jedem** abweichenden Delta ist **zwingend** die
    String-Literal-`[Fact]`-Methodik aus step-009 NITPICK
    anzuwenden (Brutto vs. Netto-Count, [Fact] in String-Literalen
    ausschließen).
- [ ] **BOM-Konservierung** (alle 6 BOM-tragenden Dateien): Vor- und
  Nach-Edit-Byte-Scan per
  `[System.IO.File]::ReadAllBytes(...)` über
  `MethodParameterCountIgnoreTypePrefixesTests.cs`,
  `MethodParameterCountOverrideTests.cs`,
  `SilentCatchAllowedTypesTests.cs`,
  `SwitchDispatcherDetectorTests.cs`, `WpfCodeBehindTests.cs` und
  `ControlFlowResilienceTests.cs` — die ersten 3 Bytes müssen vor
  und nach dem Edit identisch `EF BB BF` sein. Bei **jeder**
  Abweichung (z. B. Standard-Edit-Tool überschreibt BOM): Wechsel
  auf byte-genauen Python-Helper analog step-007
  (`McpLintConsoleTests.cs` LF-only) und Re-Edit.
- [ ] **EOL-Konservierung** (alle 20 Dateien, **Pflicht-Vollscan**
  wegen 20-Datei-Mega-Batch — nicht nur Stichprobe): Vor- und
  Nach-Edit-Byte-Scan per
  `[System.IO.File]::ReadAllBytes(...)` über alle 20 Dateien —
  die CR-Zahl muss der LF-Zahl entsprechen (uniform CRLF) und das
  letzte Byte muss LF sein (Trailing-NL). Bei **jeder** Abweichung:
  byte-genauen Python-Helper verwenden.
- [ ] **`#nullable enable`-Disziplin:** für
  `LinterAnalyzerTests.cs` (item-19, einzige Datei ohne Direktive):
  der Trait-Insert **darf** die Direktive **nicht** hinzufügen.
  Vor-Edit-Byte-Scan verifiziert erste Zeile = `using System;`
  (oder ein anderer `using`-Import), Nach-Edit-Byte-Scan
  verifiziert gleiche erste Zeile. **Kein** Eingriff in die
  Datei-Regel — Out-of-Scope-Hinweis im `step-result.md`
  §"Beobachtungen".
- [ ] **`CompoundSuppressionIntegrationTests` Namens-Heuristik-
  Override** (item-16): der Coder dokumentiert im
  `step-result.md` §"Beobachtungen" explizit die
  Namens-vs-Subprozess-Marker-Heuristik-Konflikt-Auflösung
  (Heuristik-Punkt 2 aus step-002 hat Vorrang: 0
  Subprozess-Marker → Unit, unabhängig vom Klassennamen).
- [ ] **Self-Lint** (TD-001-konform, semantisch identisch zu
  `--self-lint`): `dotnet run --project src/AiNetLinter -- --config
  rules.json --path .` → muss `OK` ausgeben
- [ ] **Commit auf aktuellem Branch** (Conventional Commit auf
  Deutsch, imperativ, mit Task-Suffix `[flaky-and-test-performance]`):
  empfohlener Subject:
  `test: Checkers+Core-Tests Kategorie-taggen [flaky-and-test-performance]`
  (Subject-Länge mit Suffix: **71 Zeichen, 1 Zeichen Reserve zur
  72-Grenze**, verifiziert per PowerShell
  `('test: Checkers+Core-Tests Kategorie-taggen
  [flaky-and-test-performance]').Length` = 71). Coder übernimmt
  den Subject-Vorschlag **ohne Änderung** (TD-002-Disziplin,
  Variante (a)-Empfehlung), Body mit Ref-Block
  `Ref: tasks/flaky-and-test-performance/step-011`.
- [ ] `step-011/step-result.md` geschrieben mit: Diff-Statistik
  (Anzahl hinzugefügter Trait-Zeilen pro Item, Gesamt-Diff in
  Zeilen und Bytes), Test-Case-Inventar pro Datei (regex-basiert
  + String-Literal-Ausschluss), BOM-Konservierungs-Tabelle für alle
  6 BOM-Dateien, EOL-Konservierungs-Tabelle für alle 20 Dateien,
  Testergebnis (Gesamt-Lauf + 2 Filter-Läufe mit Test-Zahlen +
  Delta-Abgleich), Build-Output, Self-Lint-Output, Commit-Hash
  (Code-Commit + Doku-Commit), Subject. CodeMap-Update dokumentiert
  (`Core/Checkers/`-Zeile auf "20 ungetaggte Klassen in 1
  Mega-Batch step-011 abgeschlossen" + `Core/`-Zeile auf "step-011
  nimmt erste 8 Klassen als Mega-Batch-Anteil; step-012 verbleibend
  mit 11 Klassen").
- [ ] `status` in `step-plan.md` von `open` auf `in_progress`
  (durch Orchestrator nach Coder-Start) und nach
  `step-result.md`-Schreiben auf `done (pending audit)` (durch
  Coder) gesetzt.

## Rules-Refs

- `.agents/rules/AiNetLinterRichtlinien.mdc` §4 "Testsuite-Parallelität
  bewahren" — relevant nur als Ausschluss: Trait-Attribute haben
  **keinen** Einfluss auf Parallelismus, nur
  `[Collection(...)]` / `DisableParallelization`. Dieser Step berührt
  die Parallelität nicht, ist also nicht regel-restriktiv hier.
- `.agents/rules/AiNetLinterRichtlinien.mdc` §4 "Commit-Vorschlag
  Pflicht" — betrifft die Coder-Antwort, ist im DoD-Punkt oben
  explizit referenziert.
- `.agents/rules/AiNetLinterRichtlinien.mdc` §4 "Conventional
  Commits auf Deutsch, imperativ" — Subject-Vorschlag
  `test: Checkers+Core-Tests Kategorie-taggen
  [flaky-and-test-performance]` (71 Zeichen) folgt dieser Konvention.
- `.agents/rules/AiNetLinterRichtlinien.mdc` §5 "Sparsame
  Kommentare" — die hinzugefügten Trait-Zeilen sind XML-Attribute,
  keine Kommentare. Kein Bezug.
- `.agents/rules/AiNetLinterRichtlinien.mdc` §5 "Zero-Warning-
  Direktive" — die Trait-Attribute sind `[Trait("Category",
  "Unit")]`, exakt die im Projekt etablierte Schreibweise
  (Großbuchstabe am Wortanfang). Keine Warnung erwartet.
- `.agents/rules/AiNetLinterRichtlinien.mdc` §5
  "Symptom-Fixing verboten" — relevant für item-16
  `CompoundSuppressionIntegrationTests`: der irreführende Klassenname
  wird **nicht** im step-011-Scope umbenannt (nur additives Attribut).
  Out-of-Scope-Hinweis im `step-result.md` §"Beobachtungen".
- `.agents/rules/AiNetLinter.mdc` (auto-generiert) — `EnforceSealed-
  Classes` ist für `*.Tests` aufgehoben (Z. 83), alle 20 Klassen
  sind `public sealed class` — konsistent. `EnforceNullableEnable`
  ist im `AiNetLinter.Tests`-Profil vermutlich ebenfalls aufgehoben
  (analog TD-004-Beobachtung in `Output/`), `LinterAnalyzerTests.cs`
  ohne Direktive ist **kein** Build-Error. `MaxMethodLineCount: 100`
  für `*.Tests` ist erfüllt (alle 20 Klassen ≤ 470 Zeilen = max für
  `LinterAnalyzerTests.cs`, alle Methoden weit unter 100 Zeilen).
- `config.md` (NEU seit 2026-08-08) — `max_batch_items: 20` ist
  die **Voraussetzung** für diesen Mega-Batch (12 Checkers + 8
  Core/ = 20 Klassen am Cap). Ohne `config.md`-Update wäre der
  Plan nicht ausführbar (würde den 8-Item-Cap aus
  `task-state.md` Default-Config verletzen).

## Bekannte Ausnahmen

- **`CompoundSuppressionIntegrationTests.cs` (item-16):** Klassenname
  enthält "Integration", wird aber als `Unit` getaggt — siehe
  §"Aktueller Projektzustand" oben für die detaillierte
  Subprozess-Marker-Verifikation und Heuristik-Punkt-2-Begründung.
- **`UiFileSeparationCheckerTests.cs` (item-10):** 4 `[Theory]`-Methoden
  mit 27 `[InlineData]`-Einträgen ergeben 46 Test-Cases aus 23
  Methoden — siehe §"Aktueller Projektzustand" für die
  numerische-Diskrepanz-Dokumentation. Klassen-Trait erfasst alle
  46 Cases via xUnit-Vererbung (analog step-008
  `RuleLegendRegistryTests` 8 Theory+56 InlineData=221 Cases).
- **`LinterAnalyzerTests.cs` (item-19):** einzige Datei im
  step-011-Batch **ohne** `#nullable enable` am Dateianfang — siehe
  §"Aktueller Projektzustand" für den Out-of-Scope-Hinweis.
  Trait-Insert **darf** die Direktive **nicht** hinzufügen.
- **`AutoFixerTests.cs` (item-13) & `DiffImpactAnalyzerTests.cs`
  (item-18):** XML-Doc-Variante mit 3-Schichten-Struktur — siehe
  §"Aktueller Projektzustand" für die exakten
  Trait-Platzierungs-Positionen. XML-Doc-Variante seit step-008/009
  etabliert, keine neue Bibliothek-Erweiterung nötig.
- **BOM-Inhomogenität in `Core/Checkers/` (TD-006) und analog in
  `Core/`:** 6/20 Dateien mit BOM im step-011-Batch (30 %),
  Heuristik-Punkt 8 (TD-006) bleibt offen — Konsolidierung out of
  scope step-011. Coder dokumentiert die BOM-Konservierung pro
  Datei (Byte-Scan vorher/nachher) als Beobachtung im
  `step-result.md`.

## Code-Skizze (optional)

Vorher (Beispiel: `MiddleManCheckerTests.cs:11`):

```csharp
namespace AiNetLinter.Tests.Core.Checkers;

public sealed class MiddleManCheckerTests
{
    private static readonly string TestHelperTypes = @"
```

Nachher (Standard-Insert):

```csharp
namespace AiNetLinter.Tests.Core.Checkers;

[Trait("Category", "Unit")]
public sealed class MiddleManCheckerTests
{
    private static readonly string TestHelperTypes = @"
```

Für `AutoFixerTests.cs:22` (XML-Doc-Variante, 3-Schichten:

```csharp
// @covers LinterAutoFixer

/// <summary>
/// Unit-Tests für den LinterAutoFixer zur Verifizierung der Korrektur-Operationen.
/// </summary>
public sealed class AutoFixerTests
{
```

wird zu:

```csharp
// @covers LinterAutoFixer

/// <summary>
/// Unit-Tests für den LinterAutoFixer zur Verifizierung der Korrektur-Operationen.
/// </summary>
[Trait("Category", "Unit")]
public sealed class AutoFixerTests
{
```

## Notes

- **Batch-Umfang:** 20 Klassen × je 1 Trait-Zeile = **20–22
  Diff-Zeilen** (zzgl. evtl. BOM/EOL-Header-Bytes, die unverändert
  bleiben müssen). Deutlich unter dem `max_batch_diff_lines: 80`-
  Deckel (Spec-Wert 2× default 40 = 80 — bewusste Reserve für
  BOM-Konservierungs-Kontext-Zeilen). Bei 5-Diff-Zeilen/Klasse
  (Trait + ggf. XML-Doc/Klassen-Deklaration) ergibt 20 Klassen
  ~60–100 Zeilen, aber im Standard-Insert-Fall sind es nur
  1–2 Zeilen pro Datei.
- **Schritt-Typ `low`-Risk-Begründung:** rein additives Attribut
  auf Klassen, das weder Build-Verhalten noch Test-Verhalten noch
  Parallelität ändert. Trait-Werte folgen exakt der bestehenden
  Konvention (`Unit`, CamelCase-Großbuchstabe). Kein Eingriff in
  Produktionscode, keine Fixture-Änderung, keine Test-Logik-
  Änderung. Die XML-Doc-Variante für 2/20 Klassen ist seit
  step-009 etabliert (`DeveloperExperienceTests`).
- **Mega-Batch-Spezialfall:** `max_batch_items: 20` per `config.md`
  (2026-08-08) ist die Voraussetzung für diesen Plan — ohne
  `config.md`-Update wäre der Plan nicht ausführbar. Der
  Orchestrator hat mit dem User-Feedback "bündle größer"
  (2026-08-08) diese Lockerung etabliert.
- **`Core/Checkers/`-Schnitt-Abschluss:** nach step-011 sind alle
  27 Klassen in `Core/Checkers/` getaggt — der Ordner ist
  **vollständig abgehakt** (7 vorab-getaggt aus Refactoring-
  Commits + 8 step-010 + 12 step-011 = 27/27). Die CodeMap-
  Annotation für `Core/Checkers/` wird im Doku-Commit von
  "20 ungetaggte Klassen in 3 alphabetischen Batches 8+8+4" auf
  "20 ungetaggte Klassen in 1 Mega-Batch step-011 (12 Klassen
  M–W) + 8 erste `Core/` in step-011" aktualisiert.
- **`Core/`-Schnitt-Anfang:** nach step-011 sind 8 von 19 Klassen
  in `Core/` getaggt. Restbestand 11 Klassen
  (`LinterEngineTests`–`ViolationDescriptionTests`) für step-012
  in 1 Batch (11 ≤ 20-Item-Cap). step-012 wird voraussichtlich der
  zweite und letzte Mega-Batch für `Core/`, bevor die nächsten
  Ordner (`Maps/`, `Mcp/Tools/`, `Mcp/`, `Baseline/`, `Commands/`,
  `Cli/`) in eigenen Steps drankommen.
- **Heuristik-Punkte-Bestätigung:** alle 8 bisherigen
  Heuristik-Punkte sind in step-011 bestätigt (Punkte 1–3:
  Klassen-Homogenität, Traits respektieren, Subprozess-Marker;
  Punkte 4–8: Helper-Klassen-Ausschluss, BOM-TD-005, BOM-TD-006,
  String-Literal-`[Fact]`-Ausschluss, Hypothesen-Auflösung).
  **Keine** neuen Heuristik-Punkte notwendig — die
  `CompoundSuppressionIntegrationTests`-Namens-Heuristik-Override
  ist ein **Anwendungsfall** von Heuristik-Punkt 2 (kein neuer
  Punkt).
- **Folge-Steps (NICHT in diesem Plan geplant, nur informativ):**
  1. **step-012:** `Core/`-Rest, 11 Klassen
     (`LinterEngineTests`–`ViolationDescriptionTests`) in 1 Batch
     (Mega-Batch 2/2 für `Core/`).
  2. **step-013 ff.:** `Maps/` + `Maps/Skeleton/` (6 Klassen),
     `Mcp/Tools/` (17 Klassen, 2–3 Batches), `Mcp/` (19 Klassen,
     gemischt), `Baseline/` (10 Klassen, gemischt), `Commands/`
     (17 Klassen, stark gemischt), `Cli/` (6 Klassen, gemischt).
- **Gesamt-Fortschritt nach step-011 (Planer-Erwartung):** 656
  (Stand nach step-009) + 50 (step-010) + 176 (step-011) = **882
  Unit**, 113 Integration unverändert, 1325 Total unverändert.
  Stand step-010: 706 Unit / 113 Integration / 1325 Total. Nach
  step-011: **882 Unit / 113 Integration / 1325 Total** (Delta
  +176 Unit, ±0 Integration, ±0 Total). Restbestand: 1325 − 882 =
  443 ungetaggte Methoden in ca. 30–35 Klassen über 6+ Ordner.
- **Doku-Pflicht:** nach step-011 aktualisiert der Coder die
  CodeMap-Annotationen für `Core/Checkers/` (vollständig
  abgehakt) und `Core/` (8/19 done, 11 verbleibend für
  step-012). Die `roadmap.md` ist bereits durch den Planer in
  diesem Aufruf aktualisiert (In-Arbeit-Annotation auf step-011
  gerollt, step-010 als done markiert, step-011 als
  Mega-Batch-Beschreibung).
