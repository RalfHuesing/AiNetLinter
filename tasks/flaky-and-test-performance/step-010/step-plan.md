---
status: open
type: step-plan
task: flaky-and-test-performance
step: 010               # flach, Task-weite Sequenz — auch Korrekturen liegen hier, nie in einem Unterordner
corrects: null             # <null | step-NNN> — nur gesetzt, wenn dieser Step eine Korrektur ist (treibt das Kettenbudget, siehe ../spec.md §10.5/§10.6)
title: "Category-Traits für Core/Checkers-Tests nachziehen (Batch 9 von N, Core/Checkers Teil 1/3)"
epic: EPIC-02          # Bezug zum Epic in roadmap.md, dem dieser Step zuarbeitet (bei corrects: vom korrigierten Step übernommen)
estimated_risk: low  # Einschätzung des Planers, siehe skills/planer/SKILL.md
step_type: batch  # single (Default) | batch — siehe ../spec.md §10.6. Bei batch: items-Liste unten füllen.
items:  # nur bei step_type: batch. Ein Eintrag pro gebündeltem Mini-Befund innerhalb des Epics (oder pro opportunistisch angehängtem auto_fixable-Tech-Debt, siehe ../spec.md §9.1/§10.6):
  - id: item-01
    title: "AsciiIdentifiersTests → Unit (in-process LinterAnalyzer mit raw-string Source + AsciiIdentifier-Check; 6 [Fact], classLine=10; Standard-Insert; kein BOM, CRLF+TrNL, #nullable enable Z.1)"
    source: "konzept.md §Wie Schritt 2, §Muss-Haven Traits-Punkt"
  - id: item-02
    title: "AsyncVoidCheckerTests → Unit (in-process AsyncVoidChecker.CheckMethod/CheckLocalFunction; 8 [Fact], classLine=11; Standard-Insert; **BOM-tragend** — Byte-Scan vorher/nachher, CRLF+TrNL, #nullable enable Z.1)"
    source: "konzept.md §Wie Schritt 2"
  - id: item-03
    title: "BlockingTaskCheckerTests → Unit (in-process BlockingTaskChecker.CheckInvocation für .Wait()/.Result/.GetAwaiter().GetResult(); 8 [Fact], classLine=11; Standard-Insert; **BOM-tragend** — Byte-Scan vorher/nachher, CRLF+TrNL, #nullable enable Z.1)"
    source: "konzept.md §Wie Schritt 2"
  - id: item-04
    title: "CouplingSemanticTests → Unit (in-process Coupling-Check via AdhocCompilation + SemanticModel; 2 [Fact], classLine=12; Standard-Insert; **BOM-tragend** — Byte-Scan vorher/nachher, CRLF+TrNL, #nullable enable Z.1)"
    source: "konzept.md §Wie Schritt 2"
  - id: item-05
    title: "DynamicTypeCheckerTests → Unit (in-process DynamicTypeChecker.Check; 1 [Fact], classLine=12; Standard-Insert; kein BOM, CRLF+TrNL, #nullable enable Z.1 — kleinste Datei im Batch 35 Z.)"
    source: "konzept.md §Wie Schritt 2"
  - id: item-06
    title: "LinqChainLengthCheckerTests → Unit (in-process LinqChainLengthChecker.Check; 7 [Fact], classLine=12; Standard-Insert; **BOM-tragend** — Byte-Scan vorher/nachher, CRLF+TrNL, #nullable enable Z.1)"
    source: "konzept.md §Wie Schritt 2"
  - id: item-07
    title: "MaxPartialClassFilesTests → Unit (in-process LinterAnalyzer mit Partial-Class-Test-Source; 7 [Fact], classLine=13; Standard-Insert; kein BOM, CRLF+TrNL, #nullable enable Z.1)"
    source: "konzept.md §Wie Schritt 2"
  - id: item-08
    title: "MethodParameterCountAccessibilityTests → Unit (in-process LinterAnalyzer mit Method-Param-Count-Check + Accessibility; 11 [Fact], classLine=12; Standard-Insert; **BOM-tragend** — Byte-Scan vorher/nachher, CRLF+TrNL, #nullable enable Z.1)"
    source: "konzept.md §Wie Schritt 2"
created_by: planer  # planer | orchestrator (nur bei mechanischem Korrektur-Transkript ohne Ermessen, siehe ../spec.md §6.2.1)
created_by_model: MiniMax-M3
created_by_model_knowledge_cutoff: 2026-01
created_at: 2026-08-08T08:30:00+02:00
related_to: []  # Pointer auf andere step-NNN (Task-interne Abhängigkeiten) oder auf step-review.md (Fix-Modus) — nie Fakten cachen, nur verweisen. Siehe ../spec.md §10.6. Nicht zu verwechseln mit `corrects` oben (eigene, budget-relevante Semantik).
---

# Step 010: Category-Traits für Core/Checkers-Tests nachziehen (Batch 9)

## Bezug

- **Task:** `flaky-and-test-performance`
- **Epic:** `EPIC-02` aus `roadmap.md` — Category-Traits flächendeckend
  nachziehen. Neunter von N Batches; **erster** der drei alphabetisch
  geschnittenen `Core/Checkers/`-Teilbatches (8+8+4 = 20 ungetaggte
  Klassen — 7 von 27 sind bereits durch frühere Refactoring-Commits
  getaggt, siehe „Aktueller Projektzustand").
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
  4 Klassen P–V, approved), **`step-009` (EPIC-02 Batch 8,
  Configuration, 8 Klassen, approved, Commits `b484627`/`b4a8c59`)**.
  Die acht vorherigen Batches lieferten die etablierte
  Klassifikations-Heuristik (Subprozess-Marker = Integration; sonst
  Unit), die Trait-Syntax-Konvention (`[Trait("Category", "Unit")]`,
  CamelCase-Großbuchstabe), die Trait-Platzierungs-Bibliothek
  (Standard-Insert, `// @covers`-Block-Insert, XML-Doc-Variante,
  additive method-level Traits), die Heuristik-Punkte 1–7
  (Klassen-Homogenität → Klassen-Trait; bestehende Traits
  respektieren/additiv ergänzen; `null!` als Edge-Input; Klassen-Trait
  additiv zu bestehenden method-level Traits bei homogenen Klassen;
  Hypothesen-Auflösungs-Pflicht für offene "möglicherweise…"-Annotationen
  in der CodeMap; **Helper-Klassen ohne Testmethoden sind keine
  Testklassen**; **BOM-Inhomogenität in `Configuration/` als TD-005
  elevated**, Beobachtungs-Pflicht), und die DoD-Struktur (Build grün,
  Voll-Test grün, Unit-Filter grün, Integration-Filter best-effort,
  Self-Lint `OK`, numerische Plausibilitätsprüfung mit
  String-Literal-`[Fact]`-Ausschluss-Methodik, konkreter
  Subject-Vorschlag mit exakter Längen-Angabe).
- **`Core/Checkers/`-Schnitt-Entscheidung (20 ungetaggte Klassen in 3
  Batches 8+8+4):** die ursprüngliche Orchestrator-Annahme basierte auf
  27 ungetaggten Klassen und schlug 8+8+8+3 = 4 Batches vor. **Korrektur
  durch aktuelle Code-Inspektion:** 7 von 27 Klassen tragen bereits
  `[Trait("Category", "Unit")]` auf Klassen-Ebene (verifiziert per
  `grep -nE '\[Trait\('` über alle 27 Dateien → 7 Treffer) — diese
  Klassen sind **nicht** durch EPIC-02-Schritte getaggt, sondern durch
  Refactoring-Commits aus dem `[codegraph-mcp-finish]`-Feature-Branch
  (`8cae25c refactor(tests): Core/-Testordner sub-gliedern ...`,
  `d744dc9 refactor(tests): Config-Konstruktion Rest-Cluster auf
  TestHelper konsolidiert ...`). Verbleibend: **20 ungetaggte Klassen**,
  alphabetisch geschnitten in **3 Batches 8+8+4** (statt 4 Batches):
  - **step-010 (in Planung) = 8 Klassen A–MethodParameterCountAccessibility**
  - **step-011 (geplant, nach step-010) = 8 Klassen MethodParameterCountIgnoreTypePrefixes–SilentCatchAllowedTypes**
  - **step-012 (geplant, nach step-011) = 4 Klassen SwitchDispatcherDetector–WpfCodeBehind**
  Die 4-Klassen-Schwanzgruppe in step-012 ist akzeptabel (kein Split
  in 2+2 — die 4 Klassen sind alle homogen Unit mit ähnlicher
  Standard-Insert-Mechanik, ein 2+2-Split wäre reiner Overhead).
- **Anti-Loop-Check** gegen `codemap.md` (Stand step-009-Doku-Commit,
  ~52 Einträge, 6 Sektionen): die `Core/Checkers/`-Zeile in der Sektion
  "Test-Verzeichnisse — geplant für EPIC-02-Folge-Batches" trägt den
  Vermerk "27 Klassen; rein Unit, mehrere Batches (zuletzt: step-002)" —
  **keine** offene Hypothese, **keine** bestehende Entscheidung
  widerspricht diesem Plan, **aber** der CodeMap-Eintrag ist seit
  step-002 **inkorrekt** (behauptet 27 ungetaggte, real 20 ungetaggte +
  7 vorab-getaggte). Der Coder aktualisiert die `Core/Checkers/`-Zeile
  im Doku-Commit auf "27 Klassen total, davon 7 bereits getaggt
  (`MaxInheritanceDepthTests`, `MaxConstructorDependenciesTests`,
  `MaxBoolParameterCountTests`, `MaxPublicMembersPerTypeTests`,
  `MaxSwitchArmsTests`, `NamespaceDirectoryMappingTests`,
  `NestedTypesCheckerTests`); 20 ungetaggte Klassen in 3 alphabetischen
  Batches 8+8+4 = step-010 (8 Klassen A–MethodParamA) + step-011 (8
  Klassen MethodParamI–SilentCatch) + step-012 (4 Klassen
  SwitchDispatch–Wpf) (zuletzt: step-010)". Die Heuristik-Punkte 1–7
  sind in step-002..step-009 etabliert; **Heuristik-Punkt 8 (in spe,
  neu in step-010 beobachtet)**: BOM-Inhomogenität in `Core/Checkers/`
  (10/27 mit, 17/27 ohne = 37 %/63 %, **andere** Verteilung als
  `Configuration/` 50/50 und `Output/` 0/100) — analog TD-005 step-009,
  Beobachtung in diesem Plan dokumentiert, **kein** TD-Eintrag durch
  Planer angelegt (Kritiker kann eskalieren). **Keine weitere
  bestehende Entscheidung** in der CodeMap widerspricht diesem Plan.

## Aktueller Projektzustand (JIT-Kontext)

Beim Lesen der acht Zieldateien + Inventur des `Core/Checkers/`-Ordners
vorgefunden (relevant für step-010):

- **Ordner-Inventar `Core/Checkers/` (27 `.cs`-Dateien, davon 20
  Test-Klassen ungetaggt, 7 Test-Klassen bereits getaggt):**
  - **Bereits mit `[Trait("Category", "Unit")]` auf Klassen-Ebene
    getaggt (7, NICHT in step-010-Scope):**
    `MaxInheritanceDepthTests.cs:13`, `MaxConstructorDependenciesTests.cs:13`,
    `MaxBoolParameterCountTests.cs:13`, `MaxPublicMembersPerTypeTests.cs:13`,
    `MaxSwitchArmsTests.cs:16`, `NamespaceDirectoryMappingTests.cs:15`,
    `NestedTypesCheckerTests.cs:13` — alle regex-verifiziert per
    `grep -nE '\[Trait\('`. **Herkunft:** Refactoring-Commits `8cae25c`
    und `d744dc9` aus dem `[codegraph-mcp-finish]`-Feature-Branch
    (verifiziert per `git log --oneline -n 20 --
    src/AiNetLinter.Tests/Core/Checkers/`), **nicht** durch
    EPIC-02-Schritte. CodeMap-Annotation Stand step-002 ist hier
    veraltet und wird im step-010-Doku-Commit korrigiert.
  - **Ungetaggte Test-Klassen alphabetisch (20):**
    `AsciiIdentifiersTests`, `AsyncVoidCheckerTests`,
    `BlockingTaskCheckerTests`, `CouplingSemanticTests`,
    `DynamicTypeCheckerTests`, `LinqChainLengthCheckerTests`,
    `MaxPartialClassFilesTests`,
    `MethodParameterCountAccessibilityTests`,
    `MethodParameterCountIgnoreTypePrefixesTests`,
    `MethodParameterCountOverrideTests`, `MiddleManCheckerTests`,
    `NamespaceCouplingCheckerTests`, `NamingCheckerTests`,
    `PhantomDependencyCheckerTests`, `SealedClassCheckerTests`,
    `SilentCatchAllowedTypesTests`, `SwitchDispatcherDetectorTests`,
    `UiFileSeparationCheckerTests`, `ValueObjectCheckerTests`,
    `WpfCodeBehindTests`.
- **step-010-Klassen (8 alphabetisch A–MethodParameterCountAccessibility,
  alle homogen Unit):**

  | Datei                                          | classLine | Facts | BOM  | Erste 3 Bytes          | EOL  | TrNL | Nullable |
  |------------------------------------------------|----------:|------:|:-----|------------------------|:----:|:----:|:--------:|
  | `AsciiIdentifiersTests.cs`                     |        10 |     6 |  ✗   | `75 73 69` (`using`)   | CRLF |  ✓   |    ✓     |
  | `AsyncVoidCheckerTests.cs`                     |        11 |     8 |  ✓   | `EF BB BF` (BOM)       | CRLF |  ✓   |    ✓     |
  | `BlockingTaskCheckerTests.cs`                  |        11 |     8 |  ✓   | `EF BB BF` (BOM)       | CRLF |  ✓   |    ✓     |
  | `CouplingSemanticTests.cs`                     |        12 |     2 |  ✓   | `EF BB BF` (BOM)       | CRLF |  ✓   |    ✓     |
  | `DynamicTypeCheckerTests.cs`                   |        12 |     1 |  ✗   | `75 73 69` (`using`)   | CRLF |  ✓   |    ✓     |
  | `LinqChainLengthCheckerTests.cs`               |        12 |     7 |  ✓   | `EF BB BF` (BOM)       | CRLF |  ✓   |    ✓     |
  | `MaxPartialClassFilesTests.cs`                 |        13 |     7 |  ✗   | `75 73 69` (`using`)   | CRLF |  ✓   |    ✓     |
  | `MethodParameterCountAccessibilityTests.cs`    |        12 |    11 |  ✓   | `EF BB BF` (BOM)       | CRLF |  ✓   |    ✓     |
  | **Summe Facts**                                |           | **50**|       |                        |      |      |          |

  **Beobachtungen:**
  - **Alle 8 Klassen folgen dem Standard-Insert-Pattern** (kein
    `// @covers`, kein XML-Doc, kein `: IDisposable`, kein Helper-
    Konstrukt): der Trait wird zwischen `namespace …;` und
    `public sealed class …` eingefügt. Trait-Platzierungs-Bibliothek
    aus step-007/008/009 ist **vollständig ausreichend** — keine
    neue Variante nötig.
  - **BOM-Verteilung: 5/8 mit BOM, 3/8 ohne** (62.5 %/37.5 %).
    Konkret mit BOM: `AsyncVoidCheckerTests`, `BlockingTaskCheckerTests`,
    `CouplingSemanticTests`, `LinqChainLengthCheckerTests`,
    `MethodParameterCountAccessibilityTests`. Ohne BOM:
    `AsciiIdentifiersTests`, `DynamicTypeCheckerTests`,
    `MaxPartialClassFilesTests`. **BOM-Konservierung pro Datei ist
    mechanisch** (Standard-Edit-Tool erhält BOM in der Regel,
    Byte-Scan vorher/nachher verifiziert).
  - **EOL: alle 8 Dateien uniform CRLF** (CR-Zahl = LF-Zahl in
    allen 8 Dateien, kein gemischter Status). **Trailing-NL: alle
    8 mit Trailing-NL** (letztes Byte = LF). **Kein** TD-003-Problem
    (anders als in `Output/`, dort `McpLintConsoleTests.cs` LF-only).
    Standard-Edit-Tool reicht für EOL/TrNL-Erhaltung.
  - **Nullable: alle 8 Dateien mit `#nullable enable` am Dateianfang**
    (verifiziert per `Get-Content -Encoding UTF8 -TotalCount 1`).
    **Kein** TD-004-Problem (anders als in `Output/`, dort 5/10
    ohne Direktive). `Core/Checkers/` ist diesbezüglich sauber.
  - **String-Literal-`[Fact]`-Vorkommen (NITPICK-Linie aus step-009-
    Review):** alle 8 Dateien per PowerShell-Roh-String-Scan
    geprüft (`[Fact]` innerhalb `"""`/`$"..."`/`@"..."`-Blöcke
    detektiert): **0/8 Treffer** — keine Datei im step-010-Batch
    verschachtelt `[Fact]` in einem String-Literal. Damit ist
    die Methoden-Inventur (regex-basiert) **gleich** der
    Test-Case-Inventur (kein String-Literal-Diskrepanz-Faktor
    anzuwenden), im Gegensatz zu step-009
    `AgentFeaturesTests.cs:241` (16 Planer-Count, 15 echte
    xUnit-Tests, −1). Der Coder dokumentiert im
    `step-result.md` den `Select-String`-Brutto-Count pro Datei
    **und** den per `dotnet test --filter "Category=Unit"`
    verifizierten Netto-Filter-Delta, beide müssen 50 ergeben
    (oder eine Differenz explizit dokumentiert sein).
  - **Subprozess-Marker im 8-Datei-Set** (regex-basiert per
    `grep -cE 'Process\.Start|CliProcessRunner|IClassFixture|
    SubprocessConcurrencyGate|McpTestClient|Program\.Main'`):
    **0/0/0/0/0/0/0/0** über alle 8 Dateien — keine Klasse
    startet einen Subprozess. Alle 8 Klassen sind homogen
    **Unit**. Konsistent mit der etablierten Heuristik
    (Punkte 1–3) und step-002/003/004/005/006/007/008/009-
    Bestätigung.
- **`Core/Checkers/`-Schnitt-Begründung (3 Batches 8+8+4):**
  - **Warum 8+8+4 (3 Batches, nicht 4):** die ursprüngliche
    Orchestrator-Annahme `8+8+8+3 = 4 Batches` basierte auf 27
    ungetaggten Klassen. Die Code-Inspektion in Schritt 2 hat
    7 bereits getaggte Klassen identifiziert (Herkunft via
    `git log` verifiziert: Refactoring-Commits aus
    `[codegraph-mcp-finish]`-Feature-Branch, nicht durch
    EPIC-02). Verbleibend: 20 ungetaggte Klassen → 8+8+4.
  - **Warum step-012 nur 4 Items (nicht 2+2 oder 4+0):** die
    4 Klassen in step-012 (`SwitchDispatcherDetectorTests`,
    `UiFileSeparationCheckerTests`, `ValueObjectCheckerTests`,
    `WpfCodeBehindTests`) sind alle homogen Unit mit ähnlicher
    Standard-Insert-Mechanik; ein 2+2-Split wäre reiner Overhead
    (1 zusätzlicher Plan + Coder + Kritiker-Runde ohne technischen
    Mehrwert). 4 Klassen ist die kleinste sinnvolle Bündelung.
  - **Warum nicht `Core/Checkers/` + `Maps/` (6 Klassen) mischen:**
    `Maps/` + `Maps/Skeleton/` haben 6 Klassen (verifiziert per
    `glob`); eine Mischung würde 8 + 6 = 14 Items = **über** dem
    8-Item-Deckel = 2 Batches statt 1 (kein Effizienzgewinn).
    Außerdem verletzt das Mischen zweier Ordner die etablierte
    "1 Ordner = 1 Batch"-Linie (step-002..step-009).
- **Numerische Plausibilität** (regex-basiert, gemäß step-003-Review
  NITPICK "regex statt manuell zählen" und step-009-Review NITPICK
  "String-Literal-Ausschluss-Methodik"):
  - **Methoden-Inventar pro Datei (regex-basiert per
    `Select-String -Pattern '\[(Fact|Theory)\]'`):**
    AsciiIdentifiersTests=6, AsyncVoidCheckerTests=8,
    BlockingTaskCheckerTests=8, CouplingSemanticTests=2,
    DynamicTypeCheckerTests=1, LinqChainLengthCheckerTests=7,
    MaxPartialClassFilesTests=7, MethodParameterCountAccessibilityTests=11
    = **50 Methoden** (50 `[Fact]` + 0 `[Theory]`).
  - **Test-Case-Inventar pro Datei (regex-basiert, mit
    String-Literal-Ausschluss):** alle 8 Dateien per
    PowerShell-Roh-String-Scan geprüft: **0 Dateien mit
    String-Literal-`[Fact]`-Verschachtelung**. Damit ist
    Test-Case-Inventar = Methoden-Inventar = **50 Test-Cases**.
    **Kein** Mis-count analog step-009 `AgentFeaturesTests.cs:241`
    (16 → 15).
  - **Filter-Delta step-010:** Unit steigt um **+50**, Integration
    unverändert (+0), Total unverändert (+0).
  - **Erwarteter Unit-Filter nach step-010:**
    656 (Stand nach step-009) + 50 = **706**.
  - **Integration bleibt 113, Total bleibt 1325.**
  - **Diskrepanz Methoden vs. Test-Cases: 0** (im Gegensatz zu
    step-007 mit +13 vs. +16, step-008 mit +43 vs. +221, step-009
    mit +61 vs. +68) — weil keine `[Theory]+[InlineData]`-Expansion
    und keine String-Literal-`[Fact]`-Verschachtelung im
    step-010-Batch auftritt.
- **Klassen-Deklarationen — Trait-Platzierungs-Variante** (verifiziert
  per `grep -nE 'public sealed class|/// <summary>|// @covers'` über
  alle 8 Dateien):
  - **Standard-Insert zwischen `namespace …;` und
    `public sealed class …`** (8 Klassen, alle einheitlich):
    - `AsciiIdentifiersTests.cs:8-10` (namespace Z. 8, Leerzeile Z. 9,
      class Z. 10 → Trait auf Z. 10, class auf Z. 11)
    - `AsyncVoidCheckerTests.cs:9-11` (namespace Z. 9, Leerzeile Z. 10,
      class Z. 11 → Trait auf Z. 11, class auf Z. 12)
    - `BlockingTaskCheckerTests.cs:9-11` (namespace Z. 9, Leerzeile Z. 10,
      class Z. 11 → Trait auf Z. 11, class auf Z. 12)
    - `CouplingSemanticTests.cs:10-12` (namespace Z. 10, Leerzeile Z. 11,
      class Z. 12 → Trait auf Z. 12, class auf Z. 13)
    - `DynamicTypeCheckerTests.cs:10-12` (namespace Z. 10, Leerzeile Z. 11,
      class Z. 12 → Trait auf Z. 12, class auf Z. 13)
    - `LinqChainLengthCheckerTests.cs:10-12` (namespace Z. 10,
      Leerzeile Z. 11, class Z. 12 → Trait auf Z. 12, class auf Z. 13)
    - `MaxPartialClassFilesTests.cs:11-13` (namespace Z. 11,
      Leerzeile Z. 12, class Z. 13 → Trait auf Z. 13, class auf Z. 14)
    - `MethodParameterCountAccessibilityTests.cs:10-12` (namespace Z. 10,
      Leerzeile Z. 11, class Z. 12 → Trait auf Z. 12, class auf Z. 13)
- **EOL-/BOM-/Trailing-NL-Status** (verifiziert per PowerShell-Byte-
  Check über alle 8 step-010-Dateien):

  | Datei                                       | BOM  | CR    | LF   | Trailing-NL | Erste 3 Bytes          |
  |---------------------------------------------|:----:|------:|-----:|:-----------:|------------------------|
  | `AsciiIdentifiersTests.cs`                  |  ✗   |   168 |  168 |     ✓       | `75 73 69` (`using`)   |
  | `AsyncVoidCheckerTests.cs`                  |  ✓   |   182 |  182 |     ✓       | `EF BB BF` (BOM)       |
  | `BlockingTaskCheckerTests.cs`               |  ✓   |   203 |  203 |     ✓       | `EF BB BF` (BOM)       |
  | `CouplingSemanticTests.cs`                  |  ✓   |    90 |   90 |     ✓       | `EF BB BF` (BOM)       |
  | `DynamicTypeCheckerTests.cs`                |  ✗ |    35 |   35 |     ✓       | `75 73 69` (`using`)   |
  | `LinqChainLengthCheckerTests.cs`            |  ✓   |   188 |  188 |     ✓       | `EF BB BF` (BOM)       |
  | `MaxPartialClassFilesTests.cs`              |  ✗   |   148 |  148 |     ✓       | `75 73 69` (`using`)   |
  | `MethodParameterCountAccessibilityTests.cs` |  ✓   |   190 |  190 |     ✓       | `EF BB BF` (BOM)       |

  **Beobachtungen:**
  - **EOL-Inhomogenität: keine** — alle 8 Dateien **uniform CRLF**
    (CR-Zahl = LF-Zahl in allen 8 Dateien, kein gemischter Status).
    **TD-003 (LF-only `McpLintConsoleTests.cs` in `Output/`)
    betrifft step-010 NICHT** — `Core/Checkers/` ist diesbezüglich
    sauber, Standard-Edit-Tool reicht für EOL-Erhaltung.
  - **Trailing-NL: alle 8 Dateien mit Trailing-NL** (letztes Byte
    = LF in allen 8 Dateien) — Standard-Edit-Tool reicht auch hier.
  - **BOM-Inhomogenität: 5 von 8 mit BOM, 3 ohne** (= 62.5 %/37.5 %).
    Konsequenz für den Coder: das Standard-Edit-Tool erhält
    die BOM in der Regel (Bytes vor und nach dem Edit sind
    identisch), aber der Coder **muss** für alle 5 BOM-tragenden
    Dateien explizit per
    `[System.IO.File]::ReadAllBytes(...)`-Scan **vor** und
    **nach** dem Edit verifizieren, dass die ersten 3 Bytes
    weiterhin `EF BB BF` sind. Falls das Standard-Edit-Tool die
    BOM überschreibt: byte-genauen Python-Helper analog
    step-007 (`McpLintConsoleTests.cs` LF-only) umstellen.
  - **Pattern-Beobachtung (Heuristik-Punkt 8, in spe, neu in
    step-010 dokumentiert):** die BOM-Inhomogenität in
    `Core/Checkers/` ist die **dritte** beobachtete
    Inhomogenitäts-Dimension Encoding/BOM nach
    `Output/` (0/9 = 0 % mit BOM, einheitlich ohne) und
    `Configuration/` (4/8 = 50/50). Drei verschiedene Ordner,
    drei verschiedene BOM-Verteilungen — wahrscheinlich
    gemeinsame Wurzel in `core.autocrlf` + Editor-Encoding-
    Defaults, aber **kein** step-010-Scope zur Klärung. Die
    BOM-Inhomogenität wird im Plan dokumentiert; **kein**
    TD-Eintrag durch Planer angelegt (Kritiker kann analog
    TD-005-Elevation in step-009-Review eskalieren). Die
    TD-005-Elevations-Linie aus step-009 stützt die Vermutung,
    dass auch diese Beobachtung eskaliert werden könnte —
    **aber** Planer-Scope bleibt auf den additiven
    Trait-Insert pro Datei begrenzt, keine Konsolidierung.
- **7 vorab-getaggte Klassen — Herkunfts-Verifikation:** per
  `git log --oneline -n 20 -- src/AiNetLinter.Tests/Core/Checkers/`
  sind die 7 vorab-getaggten Klassen alle in **2 Commits** aus
  dem `[codegraph-mcp-finish]`-Feature-Branch eingeführt worden
  (vermutlich als Begleit-Änderung beim Refactoring, nicht
  bewusst als "EPIC-02-Vorarbeit"). Die CodeMap-Annotation
  Stand step-002 ist hier **inkorrekt** (behauptet 27
  ungetaggte, real 20 + 7) — der Coder korrigiert das im
  Doku-Commit. **Konsequenz für step-010:** keine
  Doppel-Tagging-Aktion (Klassen-Trait wäre additiv
  zum bereits existierenden Klassen-Trait = keine Wirkung),
  diese 7 Klassen sind aus dem step-010-Scope explizit
  ausgenommen. Der Coder darf sie **nicht** anrühren.

## Intention

Alle 8 ungetaggten Testklassen in `Core/Checkers/` mit
alphabetischem Anfangsbuchstaben A–`MethodParameterCountAccessibility`
(`AsciiIdentifiersTests`, `AsyncVoidCheckerTests`,
`BlockingTaskCheckerTests`, `CouplingSemanticTests`,
`DynamicTypeCheckerTests`, `LinqChainLengthCheckerTests`,
`MaxPartialClassFilesTests`, `MethodParameterCountAccessibilityTests`)
mit `[Trait("Category", "Unit")]` auf Klassen-Ebene versehen. Dieser
Step ist der neunte von N Batches, die zusammen die EPIC-02-DoD
erreichen ("alle ~1000 Tests getaggt"), und der **erste** der drei
`Core/Checkers/`-Teilbatches (8+8+4). **Mit step-010 ist
`Core/Checkers/` Teil 1/3 abgeschlossen** (8 von 20 ungetaggten
Klassen getaggt + 7 vorab-getaggte Klassen bereits getaggt = 15 von
27 Klassen mit `Unit`-Trait, 12 verbleibend für step-011/012).

Der Step liefert **vier nennenswerte Befunde**:

1. **`Core/Checkers/`-Schnitt-Korrektur:** 7 von 27 Klassen sind
   bereits getaggt (Herkunft via `git log` aus
   `[codegraph-mcp-finish]`-Refactoring-Commits, nicht durch
   EPIC-02-Schritte). **20 ungetaggte Klassen** in 3 Batches
   (8+8+4), nicht 27 in 4 Batches (8+8+8+3) wie vom
   Orchestrator angenommen. Die CodeMap-`Core/Checkers/`-Zeile
   wird im step-010-Doku-Commit korrigiert.
2. **Heuristik-Punkt 8 (in spe, neu beobachtet, nicht
   angewendet):** BOM-Inhomogenität in `Core/Checkers/` (10 von
   27 Dateien mit UTF-8-BOM = 37 %, 17 ohne = 63 %) — eine
   **dritte** Inhomogenitäts-Beobachtung nach
   `Output/` (0/9) und `Configuration/` (4/8). Drei Ordner,
   drei BOM-Verteilungen, wahrscheinlich gemeinsame Wurzel
   in `core.autocrlf` + Editor-Defaults. Konsolidierung ist
   Nutzer-/Repo-Konvention-Entscheidung; in der Roadmap
   EPIC-02-Zeile als "Heuristik-Punkt 8 (in spe)" dokumentiert,
   **kein** TD-Eintrag durch Planer angelegt, **kein**
   `auto_fixable`-Anhängen in step-010.
3. **String-Literal-`[Fact]`-Ausschluss-Methodik angewendet
   (NITPICK-Linie aus step-009-Review):** alle 8 step-010-Dateien
   per PowerShell-Roh-String-Scan geprüft — **0/8** Dateien mit
   String-Literal-`[Fact]`-Verschachtelung. Damit ist die
   Methoden-Inventur (50) **gleich** der Test-Case-Inventur
   (50), keine Diskrepanz, **kein** Mis-count-Risiko analog
   step-009 `AgentFeaturesTests.cs:241`.
4. **Trait-Platzierungs-Bibliothek vollständig bestätigt:** alle
   3 in step-007/008 etablierten Varianten (Standard-Insert,
   `// @covers`-Block-Insert, XML-Doc-Variante) sind in den
   bisherigen 8 Batches angewendet; **step-010 braucht nur
   Standard-Insert** in allen 8 Klassen (kein `// @covers`, kein
   XML-Doc, kein `: IDisposable` in der `Core/Checkers/`-
   Konvention). Die Bibliothek ist für die EPIC-02-Folge-Batches
   als abgeschlossen anzusehen.

## Konkrete Änderungen

**Bei `step_type: batch`** (gemäß `../spec.md` §10.6): pro Item aus
der `items`-Liste im Frontmatter eine Unterüberschrift mit Datei-
Pfad und Zeilen-Angabe (verifiziert per Datei-Inspektion in Schritt 2).

### item-01: AsciiIdentifiersTests — `src/AiNetLinter.Tests/Core/Checkers/AsciiIdentifiersTests.cs` (Z. 10, Standard-Insert)

- **Was:** zwischen `namespace AiNetLinter.Tests.Core.Checkers;`
  (Z. 8) und `public sealed class AsciiIdentifiersTests` (Z. 10)
  eine neue Zeile `[Trait("Category", "Unit")]` einfügen;
  class verschiebt sich auf Z. 11 nach Edit.
- **Warum:** Klassen-Homogenität Unit (6 `[Fact]`, alle in-process
  `LinterAnalyzer.Analyze(...)` mit raw-string Source + AsciiIdentifier-
  Check), Standard-Insert-Variante.
- **BOM-Hinweis:** diese Datei hat **keine** BOM, erste 3 Bytes
  `75 73 69` (`using`). Kein BOM-Konservierungs-Scan nötig.
- **Nullable-Hinweis:** diese Datei hat `#nullable enable` am
  Dateianfang (Z. 1), kein Nullable-Edit nötig.

### item-02: AsyncVoidCheckerTests — `src/AiNetLinter.Tests/Core/Checkers/AsyncVoidCheckerTests.cs` (Z. 11, Standard-Insert)

- **Was:** zwischen `namespace AiNetLinter.Tests.Core.Checkers;`
  (Z. 9) und `public sealed class AsyncVoidCheckerTests` (Z. 11)
  eine neue Zeile `[Trait("Category", "Unit")]` einfügen;
  class verschiebt sich auf Z. 12 nach Edit.
- **Warum:** Klassen-Homogenität Unit (8 `[Fact]`, alle in-process
  `AsyncVoidChecker.CheckMethod`/`CheckLocalFunction` + `TestHelper`-
  basierte Config-Konstruktion), Standard-Insert-Variante.
- **BOM-Hinweis:** diese Datei **hat BOM** (erste 3 Bytes
  `EF BB BF`). Der Coder **muss** vor und nach dem Edit per
  `[System.IO.File]::ReadAllBytes(...)` verifizieren, dass die
  ersten 3 Bytes weiterhin `EF BB BF` sind. Falls das Edit-Tool
  die BOM überschreibt: byte-genauen Python-Helper analog
  step-007 verwenden. **Wichtig:** das Standard-Edit-Tool
  (`edit`-Tool) erhält die BOM in der Regel — die Prüfung ist
  eine **Sicherheits-Verifikation**, kein erwarteter Workaround.
- **Nullable-Hinweis:** diese Datei hat `#nullable enable` am
  Dateianfang (Z. 1 nach BOM), kein Nullable-Edit nötig.

### item-03: BlockingTaskCheckerTests — `src/AiNetLinter.Tests/Core/Checkers/BlockingTaskCheckerTests.cs` (Z. 11, Standard-Insert)

- **Was:** zwischen `namespace AiNetLinter.Tests.Core.Checkers;`
  (Z. 9) und `public sealed class BlockingTaskCheckerTests` (Z. 11)
  eine neue Zeile `[Trait("Category", "Unit")]` einfügen;
  class verschiebt sich auf Z. 12 nach Edit.
- **Warum:** Klassen-Homogenität Unit (8 `[Fact]`, alle in-process
  `BlockingTaskChecker.CheckInvocation` für `.Wait()`/`.Result`/
  `.GetAwaiter().GetResult()`), Standard-Insert-Variante.
- **BOM-Hinweis:** diese Datei **hat BOM** (erste 3 Bytes
  `EF BB BF`). Byte-Scan-Verifikation vor/nach Edit erforderlich
  (gleiche Logik wie item-02).
- **Nullable-Hinweis:** diese Datei hat `#nullable enable` am
  Dateianfang (Z. 1 nach BOM), kein Nullable-Edit nötig.

### item-04: CouplingSemanticTests — `src/AiNetLinter.Tests/Core/Checkers/CouplingSemanticTests.cs` (Z. 12, Standard-Insert)

- **Was:** zwischen `namespace AiNetLinter.Tests.Core.Checkers;`
  (Z. 10) und `public sealed class CouplingSemanticTests` (Z. 12)
  eine neue Zeile `[Trait("Category", "Unit")]` einfügen;
  class verschiebt sich auf Z. 13 nach Edit.
- **Warum:** Klassen-Homogenität Unit (2 `[Fact]`, alle in-process
  Coupling-Check via AdhocCompilation + `SemanticModel`-
  Konstruktion), Standard-Insert-Variante. **Sehr kompakte Datei
  (90 Z., 2 Facts) — typischer "kleiner Checker"-Test**.
- **BOM-Hinweis:** diese Datei **hat BOM** (erste 3 Bytes
  `EF BB BF`). Byte-Scan-Verifikation vor/nach Edit erforderlich
  (gleiche Logik wie item-02).
- **Nullable-Hinweis:** diese Datei hat `#nullable enable` am
  Dateianfang (Z. 1 nach BOM), kein Nullable-Edit nötig.

### item-05: DynamicTypeCheckerTests — `src/AiNetLinter.Tests/Core/Checkers/DynamicTypeCheckerTests.cs` (Z. 12, Standard-Insert)

- **Was:** zwischen `namespace AiNetLinter.Tests.Core.Checkers;`
  (Z. 10) und `public sealed class DynamicTypeCheckerTests` (Z. 12)
  eine neue Zeile `[Trait("Category", "Unit")]` einfügen;
  class verschiebt sich auf Z. 13 nach Edit.
- **Warum:** Klassen-Homogenität Unit (1 `[Fact]`, in-process
  `DynamicTypeChecker.Check`), Standard-Insert-Variante.
  **Kleinste Datei im Batch (35 Z., 1 Fact)** — Minimal-Beispiel.
- **BOM-Hinweis:** diese Datei hat **keine** BOM, erste 3 Bytes
  `75 73 69` (`using`). Kein BOM-Konservierungs-Scan nötig.
- **Nullable-Hinweis:** diese Datei hat `#nullable enable` am
  Dateianfang (Z. 1), kein Nullable-Edit nötig.

### item-06: LinqChainLengthCheckerTests — `src/AiNetLinter.Tests/Core/Checkers/LinqChainLengthCheckerTests.cs` (Z. 12, Standard-Insert)

- **Was:** zwischen `namespace AiNetLinter.Tests.Core.Checkers;`
  (Z. 10) und `public sealed class LinqChainLengthCheckerTests` (Z. 12)
  eine neue Zeile `[Trait("Category", "Unit")]` einfügen;
  class verschiebt sich auf Z. 13 nach Edit.
- **Warum:** Klassen-Homogenität Unit (7 `[Fact]`, alle in-process
  `LinqChainLengthChecker.Check` mit Positiv- und Negativ-Tests
  für Chain-Längen-Limit-Überschreitung), Standard-Insert-Variante.
- **BOM-Hinweis:** diese Datei **hat BOM** (erste 3 Bytes
  `EF BB BF`). Byte-Scan-Verifikation vor/nach Edit erforderlich
  (gleiche Logik wie item-02).
- **Nullable-Hinweis:** diese Datei hat `#nullable enable` am
  Dateianfang (Z. 1 nach BOM), kein Nullable-Edit nötig.

### item-07: MaxPartialClassFilesTests — `src/AiNetLinter.Tests/Core/Checkers/MaxPartialClassFilesTests.cs` (Z. 13, Standard-Insert)

- **Was:** zwischen `namespace AiNetLinter.Tests.Core.Checkers;`
  (Z. 11) und `public sealed class MaxPartialClassFilesTests`
  (Z. 13) eine neue Zeile `[Trait("Category", "Unit")]` einfügen;
  class verschiebt sich auf Z. 14 nach Edit.
- **Warum:** Klassen-Homogenität Unit (7 `[Fact]`, alle in-process
  `LinterAnalyzer.Analyze(...)` mit Partial-Class-Test-Source +
  `MetricsConfig.MaxPartialClassFiles`-Konfiguration), Standard-
  Insert-Variante.
- **BOM-Hinweis:** diese Datei hat **keine** BOM, erste 3 Bytes
  `75 73 69` (`using`). Kein BOM-Konservierungs-Scan nötig.
- **Nullable-Hinweis:** diese Datei hat `#nullable enable` am
  Dateianfang (Z. 1), kein Nullable-Edit nötig.

### item-08: MethodParameterCountAccessibilityTests — `src/AiNetLinter.Tests/Core/Checkers/MethodParameterCountAccessibilityTests.cs` (Z. 12, Standard-Insert)

- **Was:** zwischen `namespace AiNetLinter.Tests.Core.Checkers;`
  (Z. 10) und
  `public sealed class MethodParameterCountAccessibilityTests` (Z. 12)
  eine neue Zeile `[Trait("Category", "Unit")]` einfügen;
  class verschiebt sich auf Z. 13 nach Edit.
- **Warum:** Klassen-Homogenität Unit (11 `[Fact]`, alle in-process
  `LinterAnalyzer` mit Method-Param-Count-Check + Accessibility-
  Konfiguration), Standard-Insert-Variante.
- **BOM-Hinweis:** diese Datei **hat BOM** (erste 3 Bytes
  `EF BB BF`). Byte-Scan-Verifikation vor/nach Edit erforderlich
  (gleiche Logik wie item-02).
- **Nullable-Hinweis:** diese Datei hat `#nullable enable` am
  Dateianfang (Z. 1 nach BOM), kein Nullable-Edit nötig.

## Tests

- [ ] `dotnet build` — Solution-Root, muss grün sein (0 Warnungen,
  0 Fehler; `TreatWarningsAsErrors=true` in beiden Projekten)
- [ ] `dotnet test --no-build` — voller Testlauf, muss grün sein
  (1325 Tests, 0 Fehler; Lauf-Dauer als Sekundär-Indikator)
- [ ] `dotnet test --no-build --filter "Category=Unit"` — Unit-Filter,
  muss **706 Tests** zeigen (Stand nach step-009: 656, +50 step-010
  erwartet)
- [ ] `dotnet test --no-build --filter "Category=Integration"` —
  Integration-Filter, muss **113 Tests** zeigen (unverändert)
- [ ] `dotnet run --project src/AiNetLinter -- --config rules.json --path .`
  — Self-Lint-Äquivalent (TD-001-konformer Ersatz für die fehlende
  `--self-lint`-Option), muss `OK` liefern

## Definition of Done

- [ ] Alle 8 "Konkrete Änderungen" (item-01..item-08) umgesetzt —
      jede Datei hat genau **eine** neue Zeile
      `[Trait("Category", "Unit")]` an der im Plan angegebenen
      Stelle, class-Zeile verschiebt sich um +1
- [ ] **Numerische Plausibilitätsprüfung** im `step-result.md`:
      - **Methoden-Inventar pro Datei** (regex-basiert per
        `Select-String -Pattern '\[(Fact|Theory)\]'`, gemäß
        step-003-Review NITPICK):
        AsciiIdentifiersTests=6, AsyncVoidCheckerTests=8,
        BlockingTaskCheckerTests=8, CouplingSemanticTests=2,
        DynamicTypeCheckerTests=1, LinqChainLengthCheckerTests=7,
        MaxPartialClassFilesTests=7,
        MethodParameterCountAccessibilityTests=11 = **50 Methoden**
        (50 `[Fact]` + 0 `[Theory]`)
      - **Test-Case-Inventar pro Datei** (regex-basiert, mit
        String-Literal-`[Fact]`-Ausschluss per PowerShell-
        Roh-String-Scan analog step-009 NITPICK): alle 8
        Dateien ohne String-Literal-`[Fact]`-Verschachtelung
        verifiziert → Test-Case-Inventar = Methoden-Inventar =
        **50 Test-Cases**
      - **Filter-Delta:** Unit 656 → 706 (+50 ✓), Integration
        113 → 113 (±0 ✓), Total 1325 → 1325 (±0 ✓)
      - **Diskrepanz Methoden (50) vs. Test-Cases (50) = 0** —
        keine `[Theory]+[InlineData]`-Expansion und keine
        String-Literal-`[Fact]`-Verschachtelung im step-010-
        Batch (anders als step-009 mit −1 Diskrepanz durch
        `AgentFeaturesTests.cs:241` String-Literal)
      - **NITPICK-Linie-Verifikation:** `Select-String`-Brutto-
        Count pro Datei (regex-basiert) muss exakt dem
        `dotnet test --filter "Category=Unit"`-Delta entsprechen
        (oder Differenz explizit dokumentiert sein)
- [ ] Build-Command `dotnet build` grün
- [ ] Test-Command `dotnet test` grün
- [ ] Unit-Filter-Lauf (`--filter "Category=Unit"`) grün mit
      **706 Tests** (s. numerische Plausibilität)
- [ ] Integration-Filter-Lauf (`--filter "Category=Integration"`)
      grün mit 113 Tests (best-effort — bei Flake des pre-existing
      `McpServerCommandLoadingStateTests.LoadState_...ReportsLoadedImmediately`-
      Tests 1× Re-Run erlaubt; EPIC-06-Ziel, nicht step-010-Scope)
- [ ] Self-Lint-Äquivalent `dotnet run --project src/AiNetLinter --
      --config rules.json --path .` liefert `OK`
- [ ] **BOM-Konservierung verifiziert** für die 5 BOM-tragenden
      Dateien (`AsyncVoidCheckerTests`, `BlockingTaskCheckerTests`,
      `CouplingSemanticTests`, `LinqChainLengthCheckerTests`,
      `MethodParameterCountAccessibilityTests`):
      `[System.IO.File]::ReadAllBytes(...)` vor und nach dem Edit
      zeigt weiterhin erste 3 Bytes `EF BB BF`. Falls bei einer
      der 5 Dateien die BOM verloren geht: byte-genauen
      Python-Helper analog step-007 einsetzen und im
      `step-result.md` §"Abweichungen" dokumentieren. **Falls
      alle 5 Bytes identisch sind** (was erwartet wird): keine
      Sonderbehandlung nötig, im `step-result.md` §"Beobachtungen"
      explizit als bestätigt vermerken.
- [ ] **EOL-/Trailing-NL-Konservierung stichprobenartig
      verifiziert** für mindestens 3 Dateien (z. B.
      `AsciiIdentifiersTests`, `CouplingSemanticTests`,
      `MethodParameterCountAccessibilityTests`): per
      `[System.IO.File]::ReadAllBytes(...)` vor und nach dem
      Edit CR-Zahl = LF-Zahl, letztes Byte = LF. Alle 8 Dateien
      uniform CRLF + Trailing-NL (verifiziert vom Planer),
      Standard-Edit-Tool reicht.
- [ ] **String-Literal-`[Fact]`-Ausschluss** für alle 8 Dateien
      per PowerShell-Roh-String-Scan verifiziert: 0/8 Treffer
      (keine Datei verschachtelt `[Fact]` in einem String-Literal)
      — im `step-result.md` §"Beobachtungen" als bestätigt
      vermerken.
- [ ] Keine `// @covers`-Marker, keine XML-Doc-Inhalte, keine
      `using`-Statements, keine Methoden-Bodies verändert — rein
      additiver Trait-Insert pro Datei
- [ ] **CodeMap-Update:** `tasks/flaky-and-test-performance/
      codemap.md` `Core/Checkers/`-Zeile in der Sektion
      "Test-Verzeichnisse — geplant für EPIC-02-Folge-Batches"
      aktualisiert von "27 Klassen; rein Unit, mehrere Batches
      (zuletzt: step-002)" auf "27 Klassen total, davon 7 bereits
      getaggt (`MaxInheritanceDepthTests`, `MaxConstructorDependenciesTests`,
      `MaxBoolParameterCountTests`, `MaxPublicMembersPerTypeTests`,
      `MaxSwitchArmsTests`, `NamespaceDirectoryMappingTests`,
      `NestedTypesCheckerTests`); 20 ungetaggte Klassen in 3
      alphabetischen Batches 8+8+4 = **step-010 (8 Klassen
      A–MethodParamA: AsciiIdentifiersTests, AsyncVoidCheckerTests,
      BlockingTaskCheckerTests, CouplingSemanticTests,
      DynamicTypeCheckerTests, LinqChainLengthCheckerTests,
      MaxPartialClassFilesTests, MethodParameterCountAccessibilityTests;
      alle mit `[Trait("Category", "Unit")]` auf Klassen-Ebene
      versehen) + step-011 (8 Klassen MethodParamI–SilentCatch) +
      step-012 (4 Klassen SwitchDispatch–Wpf) (zuletzt: step-010)";
      `last_updated`-Zeitstempel vorgespult
- [ ] `tasks/flaky-and-test-performance/step-010/step-result.md`
      geschrieben
- [ ] `status` in `step-plan.md` von `in_progress` auf
      `done (pending audit)` gesetzt
- [ ] **Commit auf `main`** (Branch-Konvention aus `task-state.md`
      Config `target_branch: main`) — Conventional Commit auf
      Deutsch, **konkreter Subject-Vorschlag** (TD-002-Disziplin,
      exakt eingehaltene ≤72-Zeichen-Grenze inkl. Suffix):
      `test: Checkers-Tests Kategorie-taggen 1/3 [flaky-and-test-performance]`
      **(70 Zeichen, 2 Zeichen Reserve zur 72-Grenze)**, verifiziert
      per PowerShell `('test: Checkers-Tests Kategorie-taggen 1/3
      [flaky-and-test-performance]').Length` = `70 -le 72` = `True` —
      parallel zur etablierten Konvention aus step-002
      (`Suppression-Tests Kategorie-taggen`), step-006
      (`Evals-Tests Kategorie-taggen`), step-009
      (`Configuration-Tests Kategorie-taggen`); `1/3`-Markierung
      weil `Core/Checkers/` in 3 alphabetische Teilbatches
      geschnitten wird (step-010/011/012).

## Rules-Refs

- `.agents/rules/AiNetLinter.mdc` — auto-generierte
  C#-Codequalitätsregeln aus `rules.json`: für step-010
  relevant sind `EnforceSealedClasses: false` für `*.Tests`-
  Override (alle 8 step-010-Klassen sind `public sealed class`,
  der Trait-Insert ändert daran nichts), `MaxMethodLineCount: 100`
  (keine Methode im step-010-Set erreicht 100 Z.), `EnforceNullableEnable`
  (alle 8 step-010-Dateien haben die Direktive am Dateianfang,
  keine Inhomogenität im Batch — Build grün bestätigt).
- `.agents/rules/AiNetLinterRichtlinien.mdc` — manuelle
  Architektur- und Workflow-Leitlinien: für step-010 relevant
  sind §4 (Commit-Konventionen — Subject ≤ 72 Zeichen, Suffix
  `[flaky-and-test-performance]`; konkreter Subject im DoD oben
  vorgegeben, 70 Zeichen, 2 Reserve), §5 (sparsame Kommentare —
  keine Verweise auf `step-NNN`/`TD-XXX`/`EPIC-XX` in Code-Kommentaren
  — step-010 ändert keine Kommentare, nur Trait-Inserts), §6
  (Zero-Warning-Direktive — `TreatWarningsAsErrors=true` muss
  grün bleiben; Trait-Insert führt keine neuen Warnings ein, da
  `[Trait(...)]` ein xUnit-Standard-Attribut ist).

## Bekannte Ausnahmen

- **7 vorab-getaggte Klassen** (`MaxInheritanceDepthTests`,
  `MaxConstructorDependenciesTests`, `MaxBoolParameterCountTests`,
  `MaxPublicMembersPerTypeTests`, `MaxSwitchArmsTests`,
  `NamespaceDirectoryMappingTests`, `NestedTypesCheckerTests`):
  bereits mit `[Trait("Category", "Unit")]` auf Klassen-Ebene
  versehen (Herkunft: Refactoring-Commits `8cae25c`/`d744dc9`
  aus `[codegraph-mcp-finish]`-Feature-Branch, **nicht** durch
  EPIC-02-Schritte). Diese 7 Klassen sind aus dem step-010-Scope
  **explizit ausgenommen** — der Coder darf sie **nicht** anrühren
  (additiver Klassen-Trait auf bereits getaggter Klasse = keine
  Wirkung, würde nur den Diff-Umfang künstlich aufblähen). Die
  CodeMap-Annotation Stand step-002 ist diesbezüglich veraltet
  und wird im step-010-Doku-Commit korrigiert.
- **`MaxPublicMembersPerTypeTests.cs:241`** (bereits getaggt,
  **nicht** in step-010-Scope): enthält String-Literal-`[Fact]`-
  Verschachtelung (analog `AgentFeaturesTests.cs:241` in step-009),
  aber die Klasse ist bereits getaggt → step-010 braucht die
  NITPICK-Linie nicht anzuwenden. **Folge-Planer** (step-011/012)
  sollte `Select-String`-basierte Counts **mit** String-Literal-
  Ausschluss anwenden, falls eine Datei mit potenziellem
  String-Literal-`[Fact]` in deren Batch rutscht (in step-010
  nicht der Fall).
- **BOM-Inhomogenität in `Core/Checkers/` (10/27 mit, 17/27 ohne
  = 37 %/63 %):** Heuristik-Punkt 8 (in spe, neu in step-010
  beobachtet), analog TD-003 (EOL-Inhomogenität in `Output/`)
  und TD-005 (BOM-Inhomogenität in `Configuration/`, durch
  step-009-Kritiker als TD-005 eleviert). Eine
  Repository-Konsistenz-Frage, **nicht** in step-010-Scope
  gelöst. Konsolidierung ist Nutzer-/Repo-Konvention-Entscheidung
  (out of scope; in `roadmap.md` EPIC-02-Zeile als
  Heuristik-Punkt 8 in spe dokumentiert). **Konservierung pro
  Datei ist in step-010-Scope** (Standard-Edit-Tool erhält BOM,
  Byte-Scan vor/nach Edit verifiziert — siehe DoD).
- **EOL-Inhomogenität TD-003 (`Output/McpLintConsoleTests.cs`
  LF-only):** betrifft step-010 **nicht** (alle 8 `Core/Checkers/`-
  Dateien uniform CRLF), keine Sonderbehandlung in step-010.
  TD-003 bleibt offen, eigenständiger Folge-Schritt.
- **Nullable-Inhomogenität TD-004 (`Output/`, 5/10 ohne
  `#nullable enable`):** betrifft step-010 **nicht** (alle 8
  `Core/Checkers/`-Dateien mit `#nullable enable`). `Core/Checkers/`
  ist diesbezüglich sauber.
- **BOM-Inhomogenität TD-005 (`Configuration/`, 4/8 mit BOM,
  4/8 ohne):** betrifft step-010 **nicht direkt** (`Core/Checkers/`
  hat eigene BOM-Verteilung 10/27), aber als Pattern-Beobachtung
  relevant: 3 Ordner mit 3 verschiedenen BOM-Verteilungen
  (`Output/` 0/9, `Core/Checkers/` 10/27, `Configuration/` 4/8)
  stützen die TD-005-Wurzel-Hypothese (gemeinsames
  `core.autocrlf` + Editor-Encoding-Default). **Kein** step-010-
  Scope zur Konsolidierung.
- **Pre-existing Flaky-Test `McpServerCommandLoadingStateTests
  .LoadState_LoadFuncCompletesSynchronouslyWithCatalog_ReportsLoadedImmediately`
  (in `Commands/McpServerCommandLoadingStateTests.cs` Z. 112-150,
  EPIC-06-Ziel):** Integration-Filter-Lauf kann den Flake
  reproduzieren — laut Konzept §"Definition of Done" und DoD-
  Konvention aus step-008/009 ist **1 Re-Run erlaubt**; bei
  wiederholtem Flake ist es kein step-010-Issue, sondern
  EPIC-06-Problem.
- **`Core/Checkers/`-Schnitt am 8-Item-Deckel = exakt:** keine
  Reserve, kein Misch-Batch mit anderen Ordnern möglich ohne
  Deckel-Verletzung. Der 3-Batch-Schnitt (8+8+4) folgt der
  etablierten alphabetischen Sortierung und passt genau
  in den 8-Item-Deckel für step-010 und step-011; step-012
  akzeptiert die 4-Klassen-Schwanzgruppe (kein 2+2-Split
  Overhead).
- **CodeMap-Inkorrektheit Stand step-002:** die
  `Core/Checkers/`-Zeile behauptet "27 Klassen; rein Unit,
  mehrere Batches (zuletzt: step-002)" — real sind 7 von
  27 Klassen bereits getaggt (Herkunft dokumentiert). Der
  Coder korrigiert die CodeMap-Zeile im Doku-Commit;
  **kein** Planer-Scope zur retrospektiven Korrektur.

## Code-Skizze (Beispiel für die einzige Platzierungs-Variante)

```
// Standard-Insert (8 Klassen, einheitlich, z. B. AsciiIdentifiersTests.cs)
#nullable enable

using System.Linq;
using Xunit;
using AiNetLinter.Configuration;
using AiNetLinter.Core;

namespace AiNetLinter.Tests.Core.Checkers;

[Trait("Category", "Unit")]
public sealed class AsciiIdentifiersTests
{
    // ...
}
```

## Notes

- **BOM-Konservierungs-Workflow für die 5 BOM-tragenden Dateien**
  (`AsyncVoidCheckerTests`, `BlockingTaskCheckerTests`,
  `CouplingSemanticTests`, `LinqChainLengthCheckerTests`,
  `MethodParameterCountAccessibilityTests`):

  ```powershell
  # Vor Edit:
  $bytes = [System.IO.File]::ReadAllBytes('pfad/zur/datei.cs')
  $bom = ($bytes[0..2] -join ',') -eq '239,187,191'  # 0xEF,0xBB,0xBF
  Write-Output "BOM-vor-Edit: $bom"

  # Edit durchfuehren (edit-Tool mit old_string/new_string)

  # Nach Edit:
  $bytes2 = [System.IO.File]::ReadAllBytes('pfad/zur/datei.cs')
  $bom2 = ($bytes2[0..2] -join ',') -eq '239,187,191'
  Write-Output "BOM-nach-Edit: $bom2"
  if (-not $bom2) { Write-Error "BOM verloren! byte-genauen Python-Helper einsetzen." }
  ```

  Das `edit`-Tool erhält die BOM in der Regel (es ersetzt nur
  den exakten `old_string`-Match, nicht die Datei-Header-Bytes).
  Der Byte-Scan ist eine **Sicherheits-Verifikation** — wenn er
  bei allen 5 Dateien `BOM-vor-Edit: True` UND
  `BOM-nach-Edit: True` zeigt, ist alles in Ordnung und keine
  Sonderbehandlung nötig. Falls bei einer Datei `BOM-nach-Edit:
  False` auftaucht: das ist der Trigger für den byte-genauen
  Python-Helper analog step-007.
- **EOL-Konservierung:** alle 8 Dateien uniform CRLF + Trailing-NL
  (verifiziert), Standard-Edit-Tool reicht. Stichprobe (3 Dateien)
  in DoD vorgeschrieben, weil ein Vorab-Scan **aller** 8 Dateien
  Overhead wäre — die Uniformität erlaubt die Stichprobe.
- **String-Literal-`[Fact]`-Ausschluss-Verifikation** (NITPICK-
  Linie aus step-009-Review): der Coder prüft pro Datei
  explizit per PowerShell-Roh-String-Scan:

  ```powershell
  $content = Get-Content -Path 'pfad/zur/datei.cs' -Encoding UTF8
  $inRawString = $false
  $hasStringLiteralFact = $false
  for ($i = 0; $i -lt $content.Count; $i++) {
    $line = $content[$i]
    $rawCount = ([regex]::Matches($line, '"""')).Count
    if ($rawCount % 2 -eq 1) { $inRawString = -not $inRawString }
    if ($inRawString -and $line -match '\[Fact\]') { $hasStringLiteralFact = $true; break }
  }
  Write-Output "StringLiteralFact: $hasStringLiteralFact"
  ```

  Erwartetes Ergebnis für alle 8 step-010-Dateien:
  `StringLiteralFact: False` (kein String-Literal-`[Fact]`
  im Batch). Im `step-result.md` §"Beobachtungen" als
  bestätigt vermerken.
- **Anti-Loop-Check nochmal explizit für die 5 BOM-tragenden
  Dateien:** keine der 5 Dateien hat eine offene Hypothese
  in `codemap.md` (alle 8 step-010-Klassen sind in der
  `Core/Checkers/`-Zeile als "27 Klassen" zusammengefasst,
  keine Per-Datei-Annotation); der Trait-Insert ändert
  an der BOM-Situation nichts (er fügt eine Zeile ein, kein
  Datei-Header-Replace).
- **Trait-Platzierungs-Bibliothek nach step-010:** alle 3 in
  step-007/008 etablierten Varianten sind in step-009
  angewendet und ohne Sonderbehandlung bestätigt. In
  step-010 ist **nur Standard-Insert** nötig (kein
  `// @covers`, kein XML-Doc, kein `: IDisposable` in der
  `Core/Checkers/`-Konvention). Die Bibliothek ist für die
  EPIC-02-Folge-Batches (step-011/012 in `Core/Checkers/`,
  dann `Core/`, `Maps/`, `Mcp/`, `Commands/`, `Cli/`,
  `Baseline/`) als abgeschlossen anzusehen.
- **Numerische Plausibilität step-010 vs. step-009:** der
  step-010-Batch ist mit +50 Unit-Delta aus 8 Klassen
  **kleiner** als der step-009-Batch (+67 aus 8 Klassen),
  weil step-009 einen String-Literal-`[Fact]`-Mis-count (−1
  Planer-Fehler) und keine `[Theory]+[InlineData]`-
  Expansion hatte, während step-010 weder String-Literal-
  Verschachtelung noch `[Theory]` enthält. Filter-Delta-
  Erwartung: **Unit 656 → 706 (+50)**, Integration
  unverändert, Total unverändert.
- **Schritt-2-Verifikations-Anker für den Coder:** alle
  Zeilen-Angaben in diesem Plan (Z. 8-13-Bereiche je
  Datei) sind per `Read`-Tool bzw. `Get-Content -Encoding
  UTF8` direkt vor dem Edit verifizierbar; bei Zeilen-Drift
  (z. B. weil eine andere Person zwischen Planer-Aufruf und
  Coder-Aufruf an der Datei editiert hat) muss der Coder die
  exakten Zeilen frisch per `Read` ermitteln und das Edit
  entsprechend anpassen — **nicht** blind die Plan-Zahlen
  übernehmen.
- **`Core/Checkers/`-Schnitt-Begründung im Plan =
  `Core/Checkers/`-Schnitt-Begründung in der Roadmap:** die
  Roadmap EPIC-02-Zeile wurde im Schritt 1 dieses Planer-
  Aufrufs aktualisiert (In-Arbeit-Annotation auf step-010
  gerollt, step-009 done vermerkt, 7 vorab-getaggte Klassen
  dokumentiert, 8+8+4 = 3 Batches erklärt, Heuristik-Punkt 8
  als neue Beobachtung dokumentiert). Der Roadmap-Diff ist
  Teil dieses Step-Commits (Orchestrator committet
  Roadmap-Diff + neuen Step-Plan zusammen, siehe
  `../../orchestrator.md` Schritt 3b).
- **Schritt-10 vs. Schritt-2 Naming-Konvention:** dieser
  Step heißt `step-010`, nicht `step-10` oder `step-10` —
  flache Task-weite Sequenz mit Null-Padding gemäß Template
  `../../templates/step-plan.md` (Schritt-Vorgabe: `NNN` =
  nächste freie dreistellige Nummer, fortlaufend über den
  ganzen Task). Der Subagent-Auftrag referenziert diese
  Konvention explizit.
