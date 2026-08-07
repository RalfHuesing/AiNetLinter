---
status: done (pending audit)
type: step-plan
task: flaky-and-test-performance
step: 008
corrects: null
title: "Category-Traits für restliche 4 Output-Tests nachziehen (Batch 7 von N, Output Teil 2/2)"
epic: EPIC-02
estimated_risk: low
step_type: batch
items:
  - id: item-01
    title: "PathNormalizerTests → Unit (in-process PathNormalizer.ToRelative + IsTestFile; 3 [Fact] + 1 [Theory]×5 [InlineData] = 8 Test-Cases)"
    source: "konzept.md §Wie Schritt 2, §Muss-Haven Traits-Punkt"
  - id: item-02
    title: "RuleLegendRegistryTests → Unit (in-process RuleMetadataRegistry.KnownRuleNames + RuleLegendRegistry.HasEntry/TryGet/Render; 2 [Fact] + 3 [Theory]×59 [MemberData] = 179 Test-Cases; XML-Doc-Variante; **KnownRuleNames.Count = 59** vom Planer im Schritt 2 verifiziert)"
    source: "konzept.md §Wie Schritt 2; Code-Verifikation Planer-Schritt-2 (4 Partial-Dateien RuleRegistry*.cs, 0 Warum:null-Einträge)"
  - id: item-03
    title: "ViolationMarkdownFormatterTests → Unit (in-process ViolationMarkdownFormatter.Format + RuleViolation-Erstellung; 30 [Fact], 473 Zeilen — Heavyweight; Standard-Insert)"
    source: "konzept.md §Wie Schritt 2"
  - id: item-04
    title: "ViolationSummaryBuilderTests → Unit (in-process ViolationSummaryBuilder.BuildByFile/BuildByRule + RuleViolation-Erstellung; 4 [Fact]; Standard-Insert)"
    source: "konzept.md §Wie Schritt 2"
created_by: planer
created_by_model: MiniMax-M3
created_by_model_knowledge_cutoff: 2026-01
created_at: 2026-08-07T15:30:00+02:00
related_to: []
---

# Step 008: Category-Traits für Output-Tests Teil 2/2 (Batch 7)

## Bezug

- **Task:** `flaky-and-test-performance`
- **Epic:** `EPIC-02` aus `roadmap.md` — Category-Traits flächendeckend
  nachziehen. Siebter von N Batches; **zweiter** und letzter der zwei
  alphabetisch geschnittenen `Output/`-Teilbatches (5+4, da 9 Test-
  Klassen den 8-Item-Deckel von `spec.md` §10.6 reihen würden).
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
  approved), **`step-007` (EPIC-02 Batch 6, Output Teil 1/2, 5 Klassen
  D–O, approved, Commit `9c4269f`)**. Die sechs vorherigen Batches
  lieferten die etablierte Klassifikations-Heuristik (Subprozess-
  Marker = Integration; sonst Unit), die Trait-Syntax-Konvention
  (`[Trait("Category", "Unit")]`, CamelCase-Großbuchstabe), die Trait-
  Platzierungs-Bibliothek (Standard-Insert, `// @covers`-Block+Trait,
  XML-Doc+Trait, additive method-level-Traits), die Heuristik-Punkte
  1–6 (Klassen-Homogenität → Klassen-Trait; bestehende Traits
  respektieren/additiv ergänzen; `null!` als Edge-Input; Klassen-Trait
  additiv zu bestehenden method-level Traits bei homogenen Klassen;
  Hypothesen-Auflösungs-Pflicht für offene
  "möglicherweise…"-Annotationen in der CodeMap; **Helper-Klassen ohne
  Testmethoden sind keine Testklassen**, neu in step-007, in step-008
  bestätigt = vollständig abgehakt), und die DoD-Struktur (Build grün,
  Voll-Test grün, Unit-Filter grün, Integration-Filter best-effort,
  Self-Lint `OK`, numerische Plausibilitätsprüfung, konkreter
  Subject-Vorschlag mit exakter Längen-Angabe).
- **`Output/`-Schnitt-Entscheidung** (siehe `roadmap.md` EPIC-02-Zeile
  Stand step-008-Plan, sowie `codemap.md` Output/-Eintrag Stand
  step-007-Doku-Commit): **alphabetisch 5+4** — step-007 = erste
  5 Klassen D–O (done), **step-008 = restliche 4 Klassen P–V**
  (`PathNormalizerTests`, `RuleLegendRegistryTests`,
  `ViolationMarkdownFormatterTests`, `ViolationSummaryBuilderTests`).
  Mit step-008 ist der `Output/`-Ordner vollständig abgeschlossen
  (9 Test-Klassen + 1 Helper alle behandelt).
- **Anti-Loop-Check** gegen `codemap.md` (Stand step-007-Doku-Commit,
  ~50 Einträge, 6 Sektionen): die `Output/`-Zeile in der Sektion
  "Test-Verzeichnisse — geplant für EPIC-02-Folge-Batches" trägt
  bereits die step-007/008-Schnitt-Annotation ("`step-007` = erste
  5 Klassen ... getaggt, `step-008` = restliche 4 Klassen ...
  noch ausstehend") — **keine** offene Hypothese, **keine**
  bestehende Entscheidung widerspricht diesem Plan. Der in step-007
  etablierte und im step-007-Doku-Commit nachgepflegte Stand
  (9 Test-Klassen + 1 Helper) ist konsistent; der Coder aktualisiert
  die `Output/`-CodeMap-Zeile im Doku-Commit auf "9 Test-Klassen +
  1 Helper, Output/-Schnitt vollständig abgeschlossen" und entfernt
  die step-008-pending-Annotation. **Keine weitere bestehende
  Entscheidung** in der CodeMap widerspricht diesem Plan.

## Aktueller Projektzustand (JIT-Kontext)

Beim Lesen der vier Zieldateien + Inventur des `Output/`-Rests vorgefunden
(relevant für step-008):

- **Ziel-Ordner-Inventar step-008 (4 Test-Klassen, alle alphabetisch
  P–V, homogen Unit):**
  - `src/AiNetLinter.Tests/Output/PathNormalizerTests.cs` —
    1 Test-Klasse, **3 `[Fact]` + 1 `[Theory]` mit 5 `[InlineData]`
    (= 8 Test-Cases zur Laufzeit)**, Standard-Variante
    (kein XML-Doc, kein `// @covers`, kein `IDisposable`).
    Erste Zeile ist `using AiNetLinter.Output;` (Z. 1), kein
    `#nullable enable` am Dateianfang (anders als die übrigen
    step-008-Dateien — verifiziert per Datei-Inspektion).
  - `src/AiNetLinter.Tests/Output/RuleLegendRegistryTests.cs` —
    1 Test-Klasse, **2 `[Fact]` + 3 `[Theory]` mit
    `[MemberData(nameof(AllKnownRuleNames))]` (= 2 + 3×59 = 179
    Test-Cases zur Laufzeit)**, XML-Doc-Variante (Z. 8-12:
    `/// <summary>` Z. 8, "Stellt sicher dass jede in
    `RuleMetadataRegistry` registrierte Regel einen expliziten
    Legende-Eintrag in `RuleLegendRegistry` hat…" Z. 9-11, `</summary>`
    Z. 12, Leerzeile, Klasse Z. 14). Die `AllKnownRuleNames`-
    Property (Z. 16-17) liefert die Rule-IDs aus
    `RuleMetadataRegistry.KnownRuleNames` (LINQ-Select auf
    `RuleRegistry.All`).
  - `src/AiNetLinter.Tests/Output/ViolationMarkdownFormatterTests.cs`
    — 1 Test-Klasse, **30 `[Fact]`, 473 Zeilen** (Heavyweight der
    EPIC-02-Serie), Standard-Variante (kein XML-Doc, kein
    `// @covers`, kein `IDisposable`). Eine private
    Hilfsmethode `CreateViolation(...)` (Z. 214-222) im Klassenrumpf.
  - `src/AiNetLinter.Tests/Output/ViolationSummaryBuilderTests.cs` —
    1 Test-Klasse, **4 `[Fact]`**, Standard-Variante (kein XML-Doc,
    kein `// @covers`, kein `IDisposable`). Eine private
    Hilfsmethode `CreateViolation(...)` (Z. 84-92) im Klassenrumpf.
- **`Output/`-Schnitt-Begründung (5+4 alphabetisch, Schließung
  in step-008):**
  - **Warum 5+4 (nicht anders):** die natürliche Trennlinie im Alphabet
    ist `O` (Output) → `P` (Path), d. h. nach `OutputRootResolverTests`
    folgt `PathNormalizerTests`. step-007 (done) hat die ersten
    5 Klassen D–O genommen, step-008 schließt die verbleibenden
    4 Klassen P–V ab. Mit step-008 ist der `Output/`-Ordner
    **vollständig** durchgetaggtt (9 Test-Klassen + 1 Helper, alle
    entschieden).
  - **Warum nicht "Output/ + Configuration/-Mischung":** die
    4 step-008-Klassen passen locker in den 8-Item-Deckel (4
    Items + 4 Slots Reserve), also kein Misch-Batch nötig. Der reine
    Output/-Schluss-Stein ist die einfachste und vorhersagbarste
    Variante — passt zur "1 Ordner = 1 (Halb-)Batch"-Linie aus
    step-002 bis step-007.
  - **Klassen-Verteilung step-008 (4 Test-Klassen, alle Unit):**
    - `PathNormalizerTests.cs` (1 Datei) — 3 `[Fact]` + 1
      `[Theory]`×5 = 8 Test-Cases, Standard-Variante
    - `RuleLegendRegistryTests.cs` (1 Datei) — 2 `[Fact]` +
      3 `[Theory]`×59 = 179 Test-Cases (Test-Case-Anzahl
      runtime-abhängig von `KnownRuleNames.Count`, hier
      statisch = 59, verifiziert — siehe unten), XML-Doc-Variante
    - `ViolationMarkdownFormatterTests.cs` (1 Datei) —
      30 `[Fact]`, 473 Zeilen, Standard-Variante
    - `ViolationSummaryBuilderTests.cs` (1 Datei) —
      4 `[Fact]`, Standard-Variante
- **Bestehende Trait-Verteilung** (verifiziert per
  `grep -cE '\[Trait\('` über die 4 step-008-Dateien):
  - `PathNormalizerTests.cs`: 0 Klassen-Traits, 0 method-level Traits
  - `RuleLegendRegistryTests.cs`: 0 / 0
  - `ViolationMarkdownFormatterTests.cs`: 0 / 0
  - `ViolationSummaryBuilderTests.cs`: 0 / 0
  - **Insgesamt: 0 Klassen-Traits, 0 method-level Traits.**
    **Alle 4 Klassen sind "jungfräulich"** — keine Vorab-
    Klassifikation zu respektieren, keine method-level Traits
    additiv zu ergänzen. Reiner Klassen-Trait-Insert ist die
    einfachste denkbare Variante.
- **Subprozess-Marker im gesamten 4-Datei-Set** (verifiziert per
  `grep -cE 'Process\.Start|McpTestClient|CliProcessRunner|Program\.Main|IClassFixture'`
  über alle 4 Dateien): **0/0/0/0 Treffer pro Datei.** Damit ist
  der gesamte Batch homogen **Unit** — keine Integration-Klasse.
  Passt zur etablierten Heuristik (Punkte 1–3) und zur
  step-002/003/004/005/006/007-Bestätigung.
- **Testmethoden-Inventar step-008** (regex-basiert per
  `grep -cE '\[(Fact|Theory)\]'`):
  - `PathNormalizerTests.cs`: **3 `[Fact]` + 1 `[Theory]`** = 4 Methoden
  - `RuleLegendRegistryTests.cs`: **2 `[Fact]` + 3 `[Theory]`** = 5 Methoden
  - `ViolationMarkdownFormatterTests.cs`: **30 `[Fact]`** = 30 Methoden
  - `ViolationSummaryBuilderTests.cs`: **4 `[Fact]`** = 4 Methoden
  - **Summe Methoden: 4 + 5 + 30 + 4 = 43 Methoden**
- **Test-Case-Inventar step-008** (regex-basiert für `[Fact]`,
  `[InlineData]`-Reihen für `[Theory]`-Methoden, und **Laufzeit-
  berechnung** für `[Theory]`+`[MemberData]` in RuleLegendRegistryTests
  aus `KnownRuleNames.Count`):
  - `PathNormalizerTests.cs`: 3 + 1×5 = **8 Test-Cases**
  - `RuleLegendRegistryTests.cs`: 2 + 3×59 = **179 Test-Cases**
  - `ViolationMarkdownFormatterTests.cs`: 30 = **30 Test-Cases**
  - `ViolationSummaryBuilderTests.cs`: 4 = **4 Test-Cases**
  - **Summe Test-Cases: 8 + 179 + 30 + 4 = 221 Test-Cases**
- **`KnownRuleNames.Count = 59` — Verifikation durch den Planer
  im Schritt 2:**
  - **Quellcode:** `RuleMetadataRegistry.KnownRuleNames` in
    `src/AiNetLinter/Configuration/RuleMetadataRegistry.cs:13-14`
    delegiert per LINQ an
    `RuleRegistry.All.Where(r => !string.IsNullOrEmpty(r.Warum))
    .Select(r => r.RuleId).ToList().AsReadOnly()`.
  - **`RuleRegistry` ist `partial` und auf 4 Dateien verteilt**
    (verifiziert per `Get-ChildItem` über `src/AiNetLinter/Core`):
    - `src/AiNetLinter/Core/RuleRegistry.cs` — 5 Build-Methoden
      (`BuildMetricsSizeRules` 3, `BuildMetricsComplexityRules` 4,
      `BuildMetricsDependencyRules` 2, `BuildMetricsStructureRules`
      6, `BuildAgentResilientRules` 3) = **18 RuleMetadata-Literale**
    - `src/AiNetLinter/Core/RuleRegistry.Architecture.cs` — 2
      Build-Methoden (`BuildArchitectureRules` 3, `BuildTestCoverageRules` 1)
      = **4 RuleMetadata-Literale**
    - `src/AiNetLinter/Core/RuleRegistry.General.cs` — 5
      Build-Methoden (`BuildGeneralRules` als Top-Level-Spread auf 4
      Sub-Methoden + ggf. Web): `BuildGeneralCoreRules` 7,
      `BuildGeneralAllowRules` 6, `BuildGeneralAdvancedRules` 6,
      `BuildUiSeparationRules` 3 = **22 RuleMetadata-Literale**
    - `src/AiNetLinter/Core/RuleRegistry.Web.cs` — 1 Build-Methode
      (`BuildWebAssetRules` als Top-Level-Aggregator auf 15
      `BuildXxx()`-Helper, jeder `private static RuleMetadata
      BuildXxx() => new(...);`) = **15 RuleMetadata-Literale**
  - **Transitive Summe in `BuildAll()`:**
    18 (Metrics+Agent) + 4 (Architecture+TestCoverage) + 22
    (General sub-methods) + 15 (Web via `BuildGeneralRules→
    BuildWebAssetRules`) = **59 transitive RuleMetadata-Literale**
  - **Kein `Warum: null/empty`-Eintrag** in allen 4 Dateien
    (verifiziert per Regex
    `^\s*Warum:\s*(null|string\.Empty)\s*,` über alle 4 Dateien,
    0/0/0/0 Treffer) — d. h. **alle 59** Regeln landen in
    `KnownRuleNames`. Der LINQ-Filter `!string.IsNullOrEmpty(r.Warum)`
    lässt keine aus.
  - **Erwartete Test-Cases** für `RuleLegendRegistryTests`:
    3 `[Theory]`×59 = **177 `[MemberData]`-Cases** + 2 `[Fact]`
    = **179 Test-Cases** zur Laufzeit.
- **Numerische Plausibilität** für die DoD-Disziplin
  (regex-basiert, gemäß step-003-Review NITPICK "regex statt
  manuell zählen"):
  - **Filter-Delta step-008:** Unit steigt um **+221**
    (= 8 + 179 + 30 + 4), Integration unverändert (+0), Total
    unverändert (+0).
  - **Erwarteter Unit-Filter nach step-008:**
    368 (Stand nach step-007) + 221 = **589**.
  - **Integration bleibt 113, Total bleibt 1325.**
  - **Methoden-Summe (43) ≠ Test-Case-Summe (221)** — die
    Diskrepanz kommt ausschließlich aus
    `RuleLegendRegistryTests.cs` (5 Methoden → 179 Test-Cases
    via `[Theory]+[MemberData]`-Expansion). Der Coder
    dokumentiert im `step-result.md` **beide** Zahlen (regex-
    basierte Methoden-Zählung pro Datei UND tatsächlicher
    Unit-Filter-Lauf-Wert) und gleicht sie gegen die
    Planer-Prognose ab.
- **Klassen-Deklarationen — Trait-Platzierungs-Varianten**
  (verifiziert per `grep -nE 'public sealed class|/// <summary>|
  // @covers'` über die 4 Dateien):
  - **Standard-Insert zwischen `namespace …;` und
    `public sealed class …`** (3 Klassen, kein XML-Doc, kein
    `// @covers`-Marker, kein `: IDisposable`):
    - `PathNormalizerTests.cs:5` (`public sealed class
      PathNormalizerTests` ohne `IDisposable`, ohne XML-Doc,
      ohne `// @covers`; **kein** `#nullable enable` am
      Dateianfang — erste Zeile ist `using AiNetLinter.Output;`
      Z. 1, dann `namespace …;` Z. 3 — die Trait-Zeile gehört
      zwischen Z. 4 (Leerzeile nach Namespace) und Z. 5
      (Klasse))
    - `ViolationMarkdownFormatterTests.cs:8` (`public sealed
      class ViolationMarkdownFormatterTests` ohne `IDisposable`,
      ohne XML-Doc, ohne `// @covers`; `#nullable enable` Z. 1
      am Dateianfang — Standard-Verhalten wie in step-007
      `LinterErrorFormatterTests` ohne XML-Doc, mit `#nullable
      enable` davor)
    - `ViolationSummaryBuilderTests.cs:6` (gleiche Konstellation)
  - **XML-Doc-Variante zwischen `</summary>` und
    `public sealed class …`** (1 Klasse):
    - `RuleLegendRegistryTests.cs:8-14` (XML-Doc Z. 8-11, `/// <summary>` Z. 8
      bis `///</summary>` Z. 12 — die `</summary>`-Zeile endet mit
      `…ergänzen.</summary>`, dann Leerzeile, dann `public sealed class
      RuleLegendRegistryTests` Z. 14)
- **EOL- und Trailing-NL-Status** (verifiziert per PowerShell-Byte-
  Check über alle 4 step-008-Dateien):

  | Datei                                       | BOM  | CR  | LF  | Trailing-NL |
  |---------------------------------------------|------|----:|----:|-------------|
  | `Output/PathNormalizerTests.cs`             |  ✗   |  47 |  47 |     ✓       |
  | `Output/RuleLegendRegistryTests.cs`         |  ✗   |  66 |  66 |     ✓       |
  | `Output/ViolationMarkdownFormatterTests.cs` |  ✗   | 473 | 473 |     ✓       |
  | `Output/ViolationSummaryBuilderTests.cs`    |  ✗   |  93 |  93 |     ✓       |

  **Homogenität über alle 4 step-008-Dateien:** **kein** BOM (alle
  4 ohne UTF-8-BOM, erste 3 Bytes `75 73 69` = `usi` von `using` in
  `PathNormalizerTests` und `ViolationSummaryBuilderTests`, bzw.
  `23 6E 75` = `#nu` von `#nullable enable` in `RuleLegendRegistryTests`
  und `ViolationMarkdownFormatterTests` — kein `EF BB BF`),
  **uniform CRLF** (CR-Zahl = LF-Zahl in allen 4 Dateien, kein
  gemischter Status), **Trailing-NL überall** (letztes Byte = LF in
  allen 4 Dateien). Damit kann der Coder alle 4 Edits mit dem
  **Standard-Edit-Tool** durchführen — **kein** byte-genauer
  Python-Helper nötig (anders als in step-007 für
  `McpLintConsoleTests.cs`, das LF-only war und deshalb TD-003
  getriggert hat; diese Inhomogenität betrifft step-008 nicht,
  weil die 4 step-008-Dateien uniform CRLF sind). Der Coder
  verifiziert dies vorab per
  `Get-Content -Encoding UTF8 <file> | Select-Object -Last 1` (zeigt
  das letzte Byte-Zeichen) und durch `git diff` nach dem Edit.
- **TD-003-Kontext-Hinweis** (NICHT step-008-Scope, nur Beobachtung):
  die in step-007 als TD-003 dokumentierte EOL-Inhomogenität
  (`McpLintConsoleTests.cs` LF-only, alle anderen 9 `Output/`-Dateien
  CRLF) **betrifft step-008 nicht** — alle 4 step-008-Dateien sind
  uniform CRLF, kein byte-genauer Python-Helper nötig. Die
  TD-003-Inhomogenität bleibt nach step-008 bestehen (1 von 10
  Dateien LF-only), unverändert seit step-007. Falls der Nutzer
  die TD-003-Konsolidierung wünscht, ist das ein eigenständiger
  Folge-Schritt (siehe `tech-debt.md` TD-003-Variante (a)).
- **Bündelungs-Begründung (4 Klassen in 1 Halb-Ordner-Batch):**
  die 4 step-008-Klassen sind die alphabetisch zweite Hälfte der
  9 `Output/`-Test-Klassen. Eine Aufteilung in 4 Einzel-Step-
  Planungen wäre reiner Overhead ohne Mehrwert. **Vorteile der
  Bündelung:** (1) **berechtigt durch homogenen Charakter** — alle
  4 Klassen sind `Unit` ohne Subprozess-Marker, einheitliche
  Heuristik-Anwendung; (2) **Klassen-Level-Mix bleibt
  überschaubar** — keine Integration-Klasse im Set, also kein
  Misch-Heuristik-Diskussion; (3) **Trait-Platzierungs-Varianten
  sind lokal pro Datei** behandelbar (Standard-Insert in 3 Klassen,
  XML-Doc-Variante in 1 Klasse — keine Datei benötigt eine
  übergeordnete Sonderbehandlung); (4) **passt locker in den
  8-Item-Deckel** (4 Items + 4 Slots Reserve); (5) **kleiner
  Diff-Umfang** (4 Trait-Zeilen + ggf. Doku-Commit, deutlich unter
  dem 40-Zeilen-Deckel); (6) **folgt der step-002/003/004/005/006/
  007-Logik** für "1 (Halb-)Ordner = 1 Batch" der kleinen/mittleren
  Unit-Ordner; (7) **schließt den `Output/`-Ordner vollständig ab**
  (9 Test-Klassen + 1 Helper alle entschieden) — Heuristik-Punkt 6
  ist damit im 2. Anwendungs-Batch (step-007 erster, step-008
  zweiter) ohne Ausnahme bestätigt = vollständig abgehakt.
- **Alternative, verworfen — Heavyweight isolieren:** die 30
  `[Fact]`/473-Zeilen-Datei `ViolationMarkdownFormatterTests.cs`
  in einem eigenen Step zu taggen, wäre möglich (item-03 allein
  wäre 1 Step, der 8-Item-Deckel voll ausnutzt). Verworfen, weil
  (a) der 1-Datei-1-Step-Overhead den Nutzen nicht rechtfertigt
  (gleicher Diff-Umfang, gleicher Trait-Mechanik), (b) die
  Heavyweight-Behandlung in step-007 nicht speziell war
  (5+1+6+3+3 Facts als Summe-16 im Batch, Heavyweight-Begriff
  relativ), (c) die alphabetische Konsistenz (5+4) durchbrochen
  würde. Heavyweight wird im Batch mitgetaggtt und im
  `step-result.md` mit Diff-Statistik separat ausgewiesen.
- **Alternative, verworfen — `Output/` Teil 2/2 + Configuration/-
  Anfang mischen:** der `Configuration/`-Ordner hat 8 Klassen
  (alle Unit, geplant für spätere Batches). Eine Mischung würde
  zwar Batch-Anzahl reduzieren, aber: (a) `Configuration/` ist
  > 8-Item-Deckel nach Abzug der 4 step-008-Items würde
  4 + 8 = 12 Items ergeben (über Deckel); (b) das Mischen
  zweier Ordner in einem Step verletzt die etablierte
  "1 Ordner = 1 Batch"-Linie (step-002 bis step-007); (c) der
  Folge-Step müsste die verbleibenden Configuration/-Klassen
  aufnehmen — wieder mit Misch-Overhead. Der reine Output/-
  Schluss-Stein ist die **einfachste und vorhersagbarste**
  Variante, schließt den Ordner vollständig ab und bildet den
  sauberen Ausgangspunkt für die nächsten Batches
  (`Configuration/`, `Core/Checkers/`, `Mcp/`, `Commands/`, `Cli/`).

## Intention

Alle 4 Testklassen in der alphabetisch zweiten Hälfte des `Output/`-
Ordners (`PathNormalizerTests`, `RuleLegendRegistryTests`,
`ViolationMarkdownFormatterTests`, `ViolationSummaryBuilderTests`)
mit `[Trait("Category", "Unit")]` auf Klassen-Ebene versehen. Dieser
Step ist der siebte von N Batches, die zusammen die EPIC-02-DoD
erreichen ("alle ~1000 Tests getraggt"), und der **zweite und
letzte** der zwei alphabetisch geschnittenen `Output/`-Teilbatches.
**Mit step-008 ist der `Output/`-Ordner vollständig abgeschlossen**
(9 Test-Klassen + 1 Helper alle entschieden) — der Folge-Schritt
kann auf `Configuration/`, `Core/Checkers/`, `Mcp/`, `Commands/` oder
`Cli/` als nächsten EPIC-02-Batch zielen.

Der Step liefert **vier nennenswerte Befunde**:

1. **`Output/`-Ordner vollständig abgeschlossen:** der Ordner
   enthält 10 `.cs`-Dateien, davon 9 Test-Klassen + 1 Helper
   (`TestLintConsole.cs` = ausgenommen, Heuristik-Punkt 6).
   step-007 hat 5 Klassen D–O getaggtt (done), step-008 schließt
   mit den 4 verbleibenden Klassen P–V ab. Heuristik-Punkt 6 ist
   damit in 2 aufeinanderfolgenden Batches (step-007 + step-008)
   ohne Ausnahme bestätigt = **vollständig abgehakt**. Der
   CodeMap-Eintrag für `Output/` wird im step-008-Doku-Commit
   finalisiert ("9 Test-Klassen + 1 Helper, Output/-Schnitt
   vollständig abgeschlossen").
2. **`KnownRuleNames.Count = 59` als Planer-Verifikations-Fund:**
   die step-007-Prognose "Test-Case-Anzahl von
   `RuleMetadataRegistry.KnownRuleNames`-Größe abhängig, vom
   nächsten Planer im Schritt 2 zu verifizieren" ist verifiziert
   — exakt 59 RuleMetadata-Literale verteilt über 4
   `partial class RuleRegistry`-Dateien
   (`RuleRegistry.cs` 18 + `RuleRegistry.Architecture.cs` 4 +
   `RuleRegistry.General.cs` 22 + `RuleRegistry.Web.cs` 15),
   alle mit `Warum: ...` ≠ null/empty. Damit ist
   `RuleLegendRegistryTests` mit **179 Test-Cases** (2 `[Fact]`
   + 3×59 `[Theory]`) der mit Abstand größte Einzel-Klassen-
   Beitrag im EPIC-02-Lauf. Der Coder dokumentiert die
   Methoden-Summe (43) **und** die Test-Case-Summe (221) separat
   im `step-result.md` und gleicht die Filter-Differenz gegen den
   tatsächlichen Unit-Filter-Lauf ab.
3. **EOL/BOM-Homogenität in `Output/` (step-008-Anteil)
   bestätigt:** alle 4 step-008-Dateien uniform CRLF + Trailing-NL,
   kein BOM — **Standard-Edit-Tool reicht für alle 4 Edits**. Die
   in step-007 als TD-003 dokumentierte LF-Inhomogenität in
   `McpLintConsoleTests.cs` betrifft step-008 **nicht** (kein
   byte-genauer Python-Helper nötig). Falls der Coder beim Edit
   einer der 4 Dateien wider Erwarten einen mixed-EOL-Status
   vorfindet, soll er analog zum step-007-Pattern mit
   byte-genauem Python-Helper reagieren und die Beobachtung im
   `step-result.md` dokumentieren — TD-003 ist ohnehin offen
   und betrifft 1 weitere Datei im selben Ordner.
4. **Output/-Heavyweight im Batch mitgetaggtt:**
   `ViolationMarkdownFormatterTests.cs` (30 `[Fact]`, 473 Zeilen)
   ist die größte Einzel-Datei in der EPIC-02-Serie. Der
   Klassen-Trait-Insert ist ein 1-Zeilen-Edit zwischen
   `namespace …;` (Z. 7) und `public sealed class …` (Z. 8) — kein
   Sonderbehandlungs-Bedarf, keine Method-Level-Doppelt-Traits
   (alle 30 Facts sind "jungfräulich", 0 bestehende Traits in der
   Datei verifiziert). Die Datei wird im `step-result.md` mit
   expliziter Diff-Statistik (1 hinzugefügte Trait-Zeile,
   +1/-0 Zeilen, Datei nun 474 Zeilen) ausgewiesen.

## Klassifikations-Heuristik für diesen Batch

Die in step-002 dokumentierte und in step-003/004/005/006/007
bestätigte Heuristik wird unverändert übernommen:

1. **Bestehende Traits prüfen.** Im Batch sind 0 Klassen-Traits und
   0 method-level `[Trait(`-Vorkommen in allen 4 Dateien
   (verifiziert per `grep -cE '\[Trait\('`, 0/0/0/0). Damit gibt
   es **nichts** zu respektieren oder additiv zu ergänzen — reine
   Klassen-Trait-Inserts. Einfachste denkbare Variante.
2. **Subprozess-Marker prüfen.** Im Batch sind 0 Subprozess-Marker
   vorhanden (verifiziert per
   `grep -cE 'Process\.Start|McpTestClient|CliProcessRunner|Program\.Main|IClassFixture'`
   über alle 4 Dateien, 0/0/0/0 Treffer). Damit ist **keine**
   Klasse in diesem Batch `Integration`.
3. **Sonst: Unit.** Trifft auf alle 4 Klassen in diesem Batch zu.

**Wichtige Negativ-Abgrenzung** (aus step-002/003/005/006/007,
weiterhin gültig, an den 4 Kandidaten verifiziert): die folgenden
Muster sind **KEIN** Subprozess und führen nicht zu `Integration`:

- `PathNormalizer.ToRelative(root, absolute)` /
  `PathNormalizer.IsTestFile(path)` (in `PathNormalizerTests`) —
  in-process `Path.GetFullPath` / `Path.Combine` + String-
  Manipulation, kein Subprozess, kein File-IO auf TempDir
  (anders als z. B. `OutputRootResolverTests` in step-007, das
  tatsächlich `Directory.CreateDirectory` etc. verwendet)
- `RuleMetadataRegistry.KnownRuleNames` +
  `RuleLegendRegistry.HasEntry(ruleName)` /
  `RuleLegendRegistry.TryGet(ruleName)` /
  `RuleLegendRegistry.Render(ruleName, …)` (in
  `RuleLegendRegistryTests`) — in-process Property-Zugriff +
  statische Methoden, kein Subprozess. `RuleMetadataRegistry`
  enthält nur eine `IReadOnlyCollection<string>`-Property
  (`KnownRuleNames`), die per LINQ aus `RuleRegistry.All`
  abgeleitet wird (`src/AiNetLinter/Configuration/
  RuleMetadataRegistry.cs:13-14`).
- `ViolationMarkdownFormatter.Format(violations, OutputRoot)`
  (in `ViolationMarkdownFormatterTests`) — in-process Format-
  Logik mit `RuleViolation`-Array (private Hilfsmethode
  `CreateViolation(...)` Z. 214-222), kein Subprozess.
- `ViolationSummaryBuilder.BuildByFile(violations, root)` /
  `ViolationSummaryBuilder.BuildByRule(violations)` (in
  `ViolationSummaryBuilderTests`) — in-process Builder-Aufrufe
  mit `RuleViolation`-Array (private Hilfsmethode
  `CreateViolation(...)` Z. 84-92), kein Subprozess.
- `IClassFixture<…>` — **kein** Vorkommen in den 4 Dateien
  (verifiziert per `grep -c 'IClassFixture'`, 0 Treffer pro
  Datei).

**Heuristik-Punkt 6 (neu in step-007, hier ohne Ausnahme
bestätigt = vollständig abgehakt):** **Helper-Klassen ohne
Testmethoden sind keine Testklassen.** Eine `.cs`-Datei, die keine
`[Fact]`- oder `[Theory]`-Annotationen enthält, ist **per
Definition kein Test** und darf **nicht** mit
`[Trait("Category", ...)]` versehen werden. Konkret für `Output/`:
der Ordner enthält 10 `.cs`-Dateien, davon 9 Test-Klassen (alle
mit `[Fact]`/`[Theory]`) + 1 Helper (`TestLintConsole.cs`,
ausgenommen).

**Konsolidierung in step-008:** die in step-007 begonnene
Helper-Ausnahme wird in step-008 ohne neue Helper-Begegnung
fortgeführt. Die 4 step-008-Dateien sind alle Test-Klassen mit
≥ 1 `[Fact]`/`[Theory]`-Annotation — keine Helper-Begegnung in
diesem Batch. Heuristik-Punkt 6 ist nach step-007 (1. Anwendung)
+ step-008 (2. Anwendung, ohne Ausnahme) **vollständig etabliert**
und wird ab dem nächsten EPIC-02-Batch (`Configuration/`, dann
`Core/Checkers/`, `Mcp/`, `Commands/`, `Cli/`) als feste Regel
angewandt. Die CodeMap wird im step-008-Doku-Commit
finalisiert.

**Heuristik-Punkt 4 (aus step-005, hier nicht angewandt — alle
4 Klassen "jungfräulich"):** **Klassen-Trait additiv zu
bestehenden method-level Traits bei homogenen Klassen** — ist
in step-008 nicht relevant, weil alle 4 Klassen 0 method-level
Traits haben (kein additiver Fall). Der reine Klassen-Trait-
Insert ist die einzige nötige Aktion.

## Konkrete Änderungen

**Bei `step_type: batch`** (siehe `../spec.md` §10.6): pro Item aus
der `items`-Liste im Frontmatter eine Unterüberschrift.

### item-01: `PathNormalizerTests` → Unit — `src/AiNetLinter.Tests/Output/PathNormalizerTests.cs` (Klassen-Deklaration, Z. 4-5, Standard-Insert)

- **Was:** Zwischen der Leerzeile nach `namespace
  AiNetLinter.Tests.Output;` (Z. 3) und `public sealed class
  PathNormalizerTests` (Z. 5) eine Zeile
  `[Trait("Category", "Unit")]` einfügen (Z. 4 wird zur Trait-Zeile,
  Klassendeklaration rutscht auf Z. 5). Keine XML-Doc, kein
  `// @covers`-Marker, kein `IDisposable` — Standard-Variante
  (analog zu `OutputRootResolverTests` step-007).
- **Warum:** Klasse enthält 3 `[Fact]`-Methoden
  (`ToRelative_ConvertsAbsolutePathToRelativeWithForwardSlashes`,
  `ToRelative_ReturnsFileNameWhenOutsideOutputRoot`,
  `ToRelative_ReturnsEmptyForNullOrEmptyPath`) + 1 `[Theory]`
  mit 5 `[InlineData]` (`IsTestFile_IdentifiesTestFilesCorrectly`),
  die `PathNormalizer.ToRelative(root, absolute)` und
  `PathNormalizer.IsTestFile(path)` direkt auf in-process
  Produktionscode aufrufen. Verwendet `Path.GetFullPath` und
  `Path.Combine` (in-process String-/Path-Manipulation, kein
  Subprozess, kein File-IO auf TempDir). Subprozess-Marker-Grep
  liefert 0 Treffer. Datei hat **kein** BOM (verifiziert per
  PowerShell-Byte-Check, erste 3 Bytes = `75 73 69` = `usi` von
  `using`) — Standard-Edit-Tool reicht, kein byte-genauer
  Helper nötig. Trait-Wert folgt exakt der bestehenden Konvention
  (`[Trait("Category", "Unit")]`, CamelCase-Großbuchstabe).
- **Edge-Hinweis:** die Datei hat **kein** `#nullable enable` am
  Anfang (anders als die übrigen 3 step-008-Dateien, die alle
  mit `#nullable enable` Z. 1 starten). Das ist konsistent zum
  step-007-Befund, dass `OutputRootResolverTests.cs` (dort Z. 4,
  hier Z. 5 Klassendeklaration ohne `#nullable enable` davor) die
  einzige Datei im step-007-Batch ohne explizites
  `#nullable enable` war — kein Trait-Insert-Problem, da der
  Trait zwischen `namespace …;` und `public sealed class …`
  eingefügt wird.

### item-02: `RuleLegendRegistryTests` → Unit — `src/AiNetLinter.Tests/Output/RuleLegendRegistryTests.cs` (Klassen-Deklaration, Z. 12-14, XML-Doc-Variante)

- **Was:** Zwischen `</summary>` (Z. 12, Ende der XML-Doc-Section
  Z. 8-11) und `public sealed class RuleLegendRegistryTests`
  (Z. 14, getrennt durch Leerzeile Z. 13) eine Zeile
  `[Trait("Category", "Unit")]` einfügen (Z. 13 wird zur Trait-
  Zeile, Klassendeklaration rutscht auf Z. 14). **Achtung:** die
  XML-Doc beginnt auf Z. 8 mit `/// <summary>` und endet auf Z. 12
  mit `///</summary>` (Text: "Stellt sicher dass jede in
  `RuleMetadataRegistry` registrierte Regel einen expliziten
  Legende-Eintrag in `RuleLegendRegistry` hat. Schlägt an wenn
  eine neue Regel hinzugefügt wird ohne gleichzeitig Warum-Text
  und Fix-Alternativen zu ergänzen.") — der Trait gehört
  **zwischen** `</summary>` und `public sealed class`, nicht
  davor (analog zur `LinterErrorFormatterTests` step-007).
- **Warum:** Klasse enthält 2 `[Fact]`-Methoden
  (`Render_IncludesConfigKeyHintWhenPresent`,
  `Render_OmitsConfigKeyHintWhenAbsent`) + 3 `[Theory]` mit
  `[MemberData(nameof(AllKnownRuleNames))]`
  (`AllRegisteredRulesHaveExplicitLegendEntry`,
  `AllLegendEntriesHaveNonEmptyContent`,
  `RenderedLegendEntryContainsRuleName`), die
  `RuleLegendRegistry.HasEntry` / `TryGet` / `Render` direkt auf
  in-process Produktionscode aufrufen. Die `AllKnownRuleNames`-
  Property (Z. 16-17) liefert per LINQ die Rule-IDs aus
  `RuleMetadataRegistry.KnownRuleNames` → `RuleRegistry.All` (4
  Partial-Dateien, **59 transitive RuleMetadata-Literale**, alle
  mit `Warum: ...` ≠ null/empty — vom Planer im Schritt 2
  verifiziert). Subprozess-Marker-Grep liefert 0 Treffer. Datei
  hat **kein** BOM (verifiziert) — Standard-Edit-Tool reicht.
  Trait-Wert folgt exakt der Konvention.
- **Spezialfall `[Theory]+[MemberData]`-Expansion:** der
  Klassen-Trait wirkt auf **alle 179 Test-Cases** zur Laufzeit
  (2 `[Fact]` + 3×59 `[Theory]`-Cases). Der Coder dokumentiert
  im `step-result.md` **beide** Zahlen: (a) die regex-basiert
  gezählte Methoden-Summe (5 = 2 `[Fact]` + 3 `[Theory]`) und
  (b) den tatsächlichen Unit-Filter-Lauf-Wert nach Edit (179
  Test-Cpaces erwartet, **+179 Delta** für `RuleLegendRegistryTests`
  allein). xUnit expandiert `[MemberData]`-Cases pro
  `[Theory]`-Methode zur Laufzeit — der Klassen-Trait wird vom
  Filter als Oder-verknüpft mit etwaigen method-level Traits
  ausgewertet (hier 0 method-level Traits, also nur Klassen-Trait
  → alle 179 Cases Unit-getaggt).
- **Diskrepanz-Hinweis im DoD:** Methoden-Summe (43 über alle
  4 Klassen) ≠ Test-Case-Summe (221 über alle 4 Klassen).
  Die Diskrepanz von **178** kommt **ausschließlich** aus
  `RuleLegendRegistryTests` (5 Methoden vs. 179 Test-Cases).
  Der Coder dokumentiert diese Diskrepanz explizit im
  `step-result.md` §"Numerische Plausibilitätsprüfung".

### item-03: `ViolationMarkdownFormatterTests` → Unit — `src/AiNetLinter.Tests/Output/ViolationMarkdownFormatterTests.cs` (Klassen-Deklaration, Z. 7-8, Standard-Insert, Heavyweight)

- **Was:** Zwischen der Leerzeile nach `namespace
  AiNetLinter.Tests.Output;` (Z. 6) und `public sealed class
  ViolationMarkdownFormatterTests` (Z. 8) eine Zeile
  `[Trait("Category", "Unit")]` einfügen (Z. 7 wird zur Trait-
  Zeile, Klassendeklaration rutscht auf Z. 8). Keine XML-Doc,
  kein `// @covers`-Marker, kein `IDisposable` — Standard-
  Variante (analog zu `DebtReportBuilderTests` step-007). Die
  Datei hat `#nullable enable` am Dateianfang (Z. 1), das
  unangetastet bleibt.
- **Warum:** Klasse enthält 30 `[Fact]`-Methoden (alle ohne
  bestehende method-level Traits, verifiziert per `grep -cE
  '\[Trait\('` = 0), die
  `ViolationMarkdownFormatter.Format(violations, OutputRoot)`
  direkt auf in-process Produktionscode aufrufen, mit privater
  Hilfsmethode `CreateViolation(filePath, line, rule, details)`
  (Z. 214-222) zur `RuleViolation`-Array-Erstellung. Eine
  statische Hilfsvariable `OutputRoot` (Z. 10) liefert den
  Root-Pfad. Subprozess-Marker-Grep liefert 0 Treffer. Datei hat
  **kein** BOM (verifiziert) — Standard-Edit-Tool reicht, kein
  byte-genauer Helper nötig. Trait-Wert folgt exakt der Konvention.
- **Heavyweight-Befund:** 30 `[Fact]`, 473 Zeilen — die größte
  Einzel-Datei in der gesamten EPIC-02-Serie (zum Vergleich:
  step-007 Max = `LinterErrorFormatterTests.cs` mit 79 Zeilen /
  6 Facts, Faktor 6 größer). Der Klassen-Trait-Insert ist ein
  trivialer 1-Zeilen-Edit; die Datei wird im `step-result.md`
  §"Diff-Statistik" mit expliziter Vorher/Nachher-Zeilenzahl
  ausgewiesen (473 → 474, +1/-0).
- **Kein Sonderbehandlungs-Bedarf:** alle 30 Facts sind
  "jungfräulich" (0 bestehende Traits), Heavyweight-Charakter
  bezieht sich nur auf die Zeilen-Anzahl, nicht auf strukturelle
  Komplexität (kein XML-Doc, keine `// @covers`, keine
  bestehenden method-level Traits, keine `IDisposable`-Logik).

### item-04: `ViolationSummaryBuilderTests` → Unit — `src/AiNetLinter.Tests/Output/ViolationSummaryBuilderTests.cs` (Klassen-Deklaration, Z. 5-6, Standard-Insert)

- **Was:** Zwischen der Leerzeile nach `namespace
  AiNetLinter.Tests.Output;` (Z. 4) und `public sealed class
  ViolationSummaryBuilderTests` (Z. 6) eine Zeile
  `[Trait("Category", "Unit")]` einfügen (Z. 5 wird zur Trait-
  Zeile, Klassendeklaration rutscht auf Z. 6). Keine XML-Doc,
  kein `// @covers`-Marker, kein `IDisposable` — Standard-
  Variante. Die Datei hat **kein** `#nullable enable` am
  Dateianfang (erste Zeile ist `using AiNetLinter.Models;` Z. 1,
  dann `using AiNetLinter.Output;` Z. 2, dann `namespace …;`
  Z. 4) — Standard-Verhalten, kein Trait-Insert-Problem.
- **Warum:** Klasse enthält 4 `[Fact]`-Methoden
  (`BuildByFile_GroupsMultipleViolationsPerFile`,
  `BuildByFile_SortsDescendingByCountThenAlphabetically`,
  `BuildByRule_GroupsAndSortsDescendingByCount`,
  `BuildByRule_TieBreaksAlphabeticallyByRuleName`), die
  `ViolationSummaryBuilder.BuildByFile(violations, root)` und
  `ViolationSummaryBuilder.BuildByRule(violations)` direkt auf
  in-process Produktionscode aufrufen, mit privater
  Hilfsmethode `CreateViolation(filePath, line, rule)` (Z. 84-92)
  zur `RuleViolation`-Array-Erstellung. Eine statische
  Hilfsvariable `OutputRoot` (Z. 8) liefert den Root-Pfad.
  Subprozess-Marker-Grep liefert 0 Treffer. Datei hat **kein**
  BOM (verifiziert) — Standard-Edit-Tool reicht. Trait-Wert
  folgt exakt der Konvention.

## Tests

Keine — die Klassifikation ist rein additiv (Attribut setzen).
Existierende Tests müssen **unverändert** grün bleiben. Validierung
erfolgt über den vollen `dotnet test`-Lauf in der Definition of Done
(kein neuer Test, kein geänderter Test).

## Definition of Done

- [ ] Alle 4 Items umgesetzt (je eine `[Trait("Category", "Unit")]`-
      Zeile auf Klassen-Ebene, platziert je nach Datei-Profil:
      Standard-Insert in `PathNormalizerTests` +
      `ViolationMarkdownFormatterTests` +
      `ViolationSummaryBuilderTests`, XML-Doc+Trait in
      `RuleLegendRegistryTests` — siehe Aufzählung oben)
- [ ] **Bestehende Traits respektiert:** keine Datei in diesem
      Batch hat bestehende Traits (verifiziert per `grep -cE
      '\[Trait\('` = 0/0/0/0). Es gibt **nichts** zu erhalten
      oder additiv zu ergänzen — reine Klassen-Trait-Inserts.
      Nach dem Diff sollten in `Output/` alle 9 Test-Klassen
      mit Klassen-Trait ausgestattet sein (5 aus step-007 +
      4 aus step-008), der Helper `TestLintConsole.cs` bleibt
      **unverändert** (Heuristik-Punkt 6).
- [ ] **`TestLintConsole.cs` (Helper) NICHT angetastet:** die
      `Output/TestLintConsole.cs`-Datei (Helper-Klasse ohne
      `[Fact]`/`[Theory]`) bleibt **unverändert** — kein
      `[Trait]`-Attribut wird hinzugefügt (Heuristik-Punkt 6,
      in step-008 ohne Ausnahme bestätigt = vollständig
      abgehakt). Verifiziert per `git diff` (Datei darf nicht
      im Diff auftauchen).
- [ ] **BOM-Erhaltung:** alle 4 step-008-Dateien haben **kein**
      UTF-8-BOM (verifiziert per PowerShell-Byte-Check vor und
      nach dem Edit, erste 3 Bytes = `75 73 69` = `usi` von
      `using` in `PathNormalizerTests` und
      `ViolationSummaryBuilderTests`, bzw. `23 6E 75` = `#nu`
      von `#nullable enable` in `RuleLegendRegistryTests` und
      `ViolationMarkdownFormatterTests` — kein `EF BB BF`).
      Da kein BOM vorhanden ist, gibt es **nichts** zu
      erhalten. Der Standard-Edit-Tool-Pfad reicht.
- [ ] **EOL/Trailing-NL-Konservierung:** alle 4 step-008-Dateien
      behalten CRLF-Zeilenenden und Trailing-NL nach dem Edit
      (verifiziert per PowerShell-`Select-String`-Prüfung und/oder
      `git diff` — keine Zeilenende-Änderungen). Bei diesem Batch
      **kein** byte-genauer Python-Helper nötig, weil alle 4
      Dateien uniform CRLF + Trailing-NL haben — Standard-Edit-
      Tool reicht. **TD-003-Hinweis:** falls der Coder beim
      Edit einer der 4 Dateien wider Erwarten einen mixed-EOL-
      Status vorfindet (LF-only oder LF/CRLF-gemischt), soll er
      analog zum step-007-Pattern mit byte-genauem Python-
      Helper reagieren und die Beobachtung im `step-result.md`
      dokumentieren. Die TD-003-Inhomogenität in
      `McpLintConsoleTests.cs` (LF-only, step-007) ist **nicht**
      step-008-Scope und bleibt nach step-008 unverändert
      bestehen.
- [ ] **Build-Command** aus Tech-Stack-Notiz (`roadmap.md`) grün:
      `dotnet build` (Zero-Warning-Direktive, `TreatWarningsAsErrors=true`
      in beiden Projekten)
- [ ] **Test-Command** aus Tech-Stack-Notiz grün: `dotnet test`
      (voller Lauf, alle Tests müssen weiterhin grün sein — keine
      Test-Logik wurde geändert)
- [ ] **Trait-Filter-Smoke-Test** (vom Coder zusätzlich durchführen,
      um die Klassifikation zu verifizieren):
      - `dotnet test --no-build --filter "Category=Unit"` → muss
        grün sein
      - `dotnet test --no-build --filter "Category=Integration"` →
        **best-effort, ein Lauf grün** (gemäß step-002/003/004/005/
        006/007 NITPICK-Linie: pre-existing Flaky-Test
        `McpServerCommandLoadingStateTests.LoadState_...ReportsLoadedImmediately`
        flake-t gelegentlich unter Last des Integration-Filters;
        nicht step-008-verursacht, Fix in EPIC-06). Der Coder
        dokumentiert im `step-result.md`, wenn der Lauf flaky ist,
        und startet ihn ggf. einmal neu.
      - **Numerische Plausibilitätsprüfung** (gemäß step-003-Review
        NITPICK "regex statt manuell zählen"): der Coder zählt die
        `[Fact]`/`[Theory]`-Methoden in den 4 Klassen **regex-basiert**
        per `grep -cE '\[(Fact|Theory)\]'` (NICHT manuell durchgehen),
        dokumentiert die Summe im `step-result.md` und vergleicht
        sie mit dem erwarteten Unit-Filter-Delta. **Erwartetes
        Delta:** Unit steigt um **+221** (= 8 + 179 + 30 + 4
        Test-Cases; die Methoden-Summe 43 ≠ Test-Case-Summe 221
        wegen `[Theory]+[MemberData]`-Expansion in
        `RuleLegendRegistryTests` mit 5 Methoden → 179 Test-Cases).
        Integration-Zahl bleibt unverändert bei 113. Total bleibt
        unverändert bei 1325. **Erwarteter Unit-Filter-Wert nach
        step-008: 589** (368 aus step-007 + 221 aus step-008).
        **Beide** Zahlen (Methoden 43, Test-Cases 221) sind im
        `step-result.md` §"Numerische Plausibilitätsprüfung"
        explizit zu nennen, mit dem tatsächlichen Filter-Delta
        abgeglichen.
- [ ] **Self-Lint** (TD-001-konform, semantisch identisch zu
      `--self-lint`): `dotnet run --project src/AiNetLinter --
      --config rules.json --path .` → muss `OK` ausgeben
- [ ] **Commit auf aktuellem Branch** (Conventional Commit auf
      Deutsch, imperativ, mit Task-Suffix `[flaky-and-test-performance]`):
      **konkreter Subject-Vorschlag** (gemäß TD-002, "kürzere
      Subject-Bodies vorgeben"):
      `test: Output-Tests Kategorie-taggen 2/2 [flaky-and-test-performance]`
      → **68 Zeichen** inkl. Suffix (exakt verifiziert per
      `('test: Output-Tests Kategorie-taggen 2/2 [flaky-and-test-performance]').Length`
      in PowerShell = `68`; deckt 4 Zeichen Sicherheitsabstand zur
      72-Zeichen-Grenze). Pattern spiegelt step-007's
      `test: Output-Tests Kategorie-taggen 1/2 [flaky-and-test-performance]`
      (68 Zeichen) — gleicher Aufbau, konsistent zur EPIC-02-Batch-
      Serie. **"2/2"** markiert den Output/-Halb-Batch-Schnitt
      (step-007 = erste Hälfte alphabetisch D–O done, step-008 =
      zweite Hälfte alphabetisch P–V). **Falls** der Coder den
      Subject abwandeln will, **muss** er 72 Zeichen einhalten und
      die neue exakte Länge im `step-result.md` dokumentieren — bei
      Überschreitung TD-002-Eintrag aktualisieren.
- [ ] `step-008/step-result.md` geschrieben mit: Diff-Statistik
      (Anzahl hinzugefügter Trait-Zeilen pro Item, Gesamt-Diff;
      insbesondere ViolationMarkdownFormatterTests mit 473 → 474
      Zeilen separat ausgewiesen), Testergebnis (Gesamt-Lauf + 2
      Filter-Läufe mit Test-Zahlen — die per `grep -c` regex-basiert
      verifizierte Methoden-Summe **43** explizit nennen, das
      **+221**-Test-Case-Delta explizit nennen, mit dem tatsächlichen
      Filter-Delta abgleichen, die **178-Methoden/Test-Case-
      Diskrepanz** aus `RuleLegendRegistryTests` explizit
      dokumentieren), Build-Output, Self-Lint-Output, Commit-Hash,
      Subject mit exakter Längen-Angabe (68 Zeichen).
      `### Commit-Vorschlag`-Block am Ende der Antwort (Pflicht —
      siehe `AiNetLinterRichtlinien.mdc` §4, Commit-Vorschlag-Pflicht).
- [ ] `codemap.md` aktualisiert (Doku-Commit): die `Output/`-Zeile
      in der Sektion „Test-Verzeichnisse — geplant für EPIC-02-Folge-
      Batches" wird auf "9 Test-Klassen + 1 Helper (`TestLintConsole.cs`,
      ausgenommen — Helper-Klasse ohne `[Fact]`/`[Theory]`,
      Heuristik-Punkt 6 vollständig abgehakt); `Output/`-Schnitt
      vollständig abgeschlossen (step-007 = 5 Klassen D–O done,
      step-008 = 4 Klassen P–V done)" aktualisiert. `last_updated`
      der `codemap.md` wird ebenfalls aktualisiert. (`Output/`-
      Eintrag aktuell auf `zuletzt: step-007` / `step-008 (geplant)`
      — wird auf `zuletzt: step-008` (ohne Suffix "geplant") gesetzt.)
- [ ] `roadmap.md` ist in step-008 **NICHT** weiter zu ändern
      (der Schritt 1 im Planer-Schritt-Modus hat den In-Arbeit-
      Marker auf step-008 gerollt und das erwartete Filter-Delta
      eingetragen — `step-result.md` berichtet die tatsächlichen
      Werte, eine weitere Roadmap-Aktualisierung erfolgt erst beim
      Planer-Aufruf für step-009, wenn der nächste Batch geplant
      wird).
- [ ] `status` in `step-plan.md` von `open` auf `in_progress`
      (durch Orchestrator nach Coder-Start) und nach
      `step-result.md`-Schreiben auf `done (pending audit)` (durch
      Coder) gesetzt.

## Rules-Refs

- `.agents/rules/AiNetLinterRichtlinien.mdc` §4 "Testsuite-Parallelität
  bewahren" — relevant nur als Ausschluss: Trait-Attribute haben
  **keinen** Einfluss auf Parallelismus, nur `[Collection(...)]` /
  `DisableParallelization`. Dieser Step berührt die Parallelität
  nicht, ist also nicht regel-restriktiv hier.
- `.agents/rules/AiNetLinterRichtlinien.mdc` §4 "Commit-Konvention"
  — relevant für Subject-Disziplin: Conventional Commit auf Deutsch,
  imperativ, Subject ≤ 72 Zeichen, Suffix `[flaky-and-test-performance]`
  (TD-002-DoD-Zeile gibt konkreten Subject vor).
- `.agents/rules/AiNetLinterRichtlinien.mdc` §5 "Sparsame Kommentare"
  — relevant nur als Ausschluss: keine `step-`/`TD-`/`EPIC-`-
  Verweise in Code-Kommentaren (wird nicht verletzt — der
  hinzugefügte Trait-Kommentar `[Trait("Category", "Unit")]` ist
  ein xUnit-Attribut, kein freier Kommentar).
- `.agents/rules/AiNetLinterRichtlinien.mdc` §5 "Subject-Disziplin"
  (TD-002) — relevant für DoD-Disziplin: konkreter Subject-
  Vorschlag mit exakter Längen-Angabe (68 Zeichen) im DoD.
- `.agents/rules/AiNetLinter.mdc` (auto-generiert) — relevant für
  `TreatWarningsAsErrors=true` (DoD-Build-Check) und `*.Tests`-
  Overrides (z. B. `MaxMethodLineCount: 100`, `EnforceSealedClasses:
  false` — letzteres ist relevant, weil alle 4 step-008-Klassen
  `sealed` sind, was bei strikter `EnforceSealedClasses`-Regel ein
  Warn-/Fehler-Punkt wäre; die `*.Tests`-Override hebt das auf).
  **Heavyweight-Hinweis:** `ViolationMarkdownFormatterTests.cs`
  ist 473 Zeilen — weit unter `MaxLineCount: 500` für Klassen, aber
  relevant für `MaxMethodLineCount: 100` (die längste Methode
  sollte verifiziert werden; `step-007` hat bei
  `LinterErrorFormatterTests.cs` (79 Zeilen) und
  `DebtReportBuilderHeaderTests.cs` (34 Zeilen) keine Überschreitung
  festgestellt; hier sind 30 separate `[Fact]`-Methoden + 1
  Hilfsmethode, alle deutlich unter 100 Zeilen — keine
  DoD-Überschreitung erwartet; Coder verifiziert per
  PowerShell-Zeilen-pro-Methode-Check vor Commit).

## Bekannte Ausnahmen

- **`McpLintConsoleTests` mit 3 method-level `Unit`-Traits (step-007):**
  die 3 method-level Traits sind bereits vorhanden und werden durch
  den Klassen-Trait **additiv** ergänzt. xUnit wertet Klassen-Oder-
  Methoden-Trait, also keine Doppelt-Zählung im Filter-Lauf. Nach
  step-008: alle 5 `McpLintConsoleTests`-Methoden (3 method-level
  + 0 weitere) sind Unit-getaggt. **Kein** zusätzlicher Aufwand in
  step-008 nötig — die step-007-Maßnahme bleibt unverändert.
- **`RuleLegendRegistryTests` mit `[Theory]+[MemberData]`-Expansion:
  die 3 `[Theory]`-Methoden werden zur Laufzeit zu **59 Test-Cases
  pro Methode** expandiert (xUnit-Standardverhalten), ergibt
  2 + 3×59 = **179 Test-Cases** für die Klasse. Der Klassen-Trait
  wirkt auf alle 179 Cases. **Im `step-result.md` separat
  dokumentieren:** (a) regex-basiert gezählte Methoden-Summe (5),
  (b) erwartete Test-Case-Summe (179) aus `KnownRuleNames.Count = 59`
  (vom Planer im Schritt 2 verifiziert), (c) tatsächlicher
  Unit-Filter-Delta (sollte +179 für `RuleLegendRegistryTests` allein
  sein), (d) gesamter step-008-Filter-Delta (sollte +221 sein).
- **TD-003-Kontext (`McpLintConsoleTests.cs` LF-only, step-007):**
  die in step-007 als TD-003 dokumentierte EOL-Inhomogenität
  betrifft step-008 **nicht** (alle 4 step-008-Dateien uniform
  CRLF, Standard-Edit-Tool reicht). Falls der Coder beim Edit
  wider Erwarten mixed-EOL findet, analog step-007 mit
  Python-Helper reagieren. TD-003 bleibt nach step-008
  unverändert (1 von 10 `Output/`-Dateien LF-only, low-prio,
  Nutzer-Entscheidung zur Konsolidierung — siehe
  `tech-debt.md` TD-003 Variante (a) `git add --renormalize .`).
- **`Output/`-Schnitt-Markierung "1/2" / "2/2" in den Commit-
  Subjects:** diese Markierung ist eine **funktionale Konvention**
  zwischen step-007 und step-008, keine Regel-Anforderung. Sie
  hilft dem nächsten Planer, den Schnitt aus dem Git-Log
  wiederzufinden, falls die `roadmap.md` zwischenzeitlich nicht
  gelesen wird.
- **Kein** "möglicherweise…"-Hypothese-Eintrag im aktuellen
  `codemap.md` `Output/`-Bereich, der in step-008 aufzulösen
  wäre (anders als in step-006, wo die `ListEvalsCommandTests`-
  Hypothese aufzulösen war) — Heuristik-Punkt 5 nicht anwendbar
  in diesem Step. Stattdessen ist Heuristik-Punkt 6 (Helper-
  Klassen-Scope) anwendbar und wird in step-008 **ohne Ausnahme
  bestätigt** = vollständig abgehakt.

## Code-Skizze (optional)

```
// src/AiNetLinter.Tests/Output/PathNormalizerTests.cs (item-01), beispielhaft
// fuer die Standard-Insert-Variante:

using AiNetLinter.Output;

namespace AiNetLinter.Tests.Output;

[Trait("Category", "Unit")]                       // → NEU in step-008
public sealed class PathNormalizerTests
{
    // ... 3 [Fact] + 1 [Theory]×5 = 8 Test-Cases, alle unveraendert
}
```

```
// src/AiNetLinter.Tests/Output/RuleLegendRegistryTests.cs (item-02), beispielhaft
// fuer die XML-Doc-Variante:

/// <summary>
/// Stellt sicher dass jede in RuleMetadataRegistry registrierte Regel einen expliziten
/// Legende-Eintrag in RuleLegendRegistry hat. Schlaegt an wenn eine neue Regel hinzugefuegt
/// wird ohne gleichzeitig Warum-Text und Fix-Alternativen zu ergaenzen.
/// </summary>
[Trait("Category", "Unit")]                       // → NEU in step-008
public sealed class RuleLegendRegistryTests
{
    public static IEnumerable<object[]> AllKnownRuleNames =>
        RuleMetadataRegistry.KnownRuleNames.Select(n => new object[] { n });

    [Theory]
    [MemberData(nameof(AllKnownRuleNames))]       // → BESTEHEND, 59 Cases
    public void AllRegisteredRulesHaveExplicitLegendEntry(string ruleName) { ... }

    // ... 2 weitere [Theory]+AllKnownRuleNames (je 59 Cases) + 2 [Fact]
    // = 2 + 3×59 = 179 Test-Cases zur Laufzeit
}
```

```
// src/AiNetLinter.Tests/Output/ViolationMarkdownFormatterTests.cs (item-03), beispielhaft
// fuer die Standard-Insert-Variante am Heavyweight (473 Zeilen):

#nullable enable

using AiNetLinter.Models;
using AiNetLinter.Output;

namespace AiNetLinter.Tests.Output;

[Trait("Category", "Unit")]                       // → NEU in step-008
public sealed class ViolationMarkdownFormatterTests
{
    // ... 30 [Fact]-Methoden unveraendert (473 Zeilen gesamt, +1 Zeile nach Edit = 474)
}
```

## Notes

- **Output/-Schnitt-Konsolidierung in step-008:** mit diesem Step
  ist der `Output/`-Ordner **vollständig abgeschlossen** — alle
  9 Test-Klassen haben `[Trait("Category", "Unit")]` auf
  Klassen-Ebene (5 aus step-007 + 4 aus step-008), der Helper
  `TestLintConsole.cs` bleibt ausgenommen (Heuristik-Punkt 6).
  Der nächste Planer-Aufruf kann auf `Configuration/`,
  `Core/Checkers/`, `Mcp/`, `Commands/` oder `Cli/` als nächsten
  EPIC-02-Batch zielen, ohne den `Output/`-Bereich noch einmal
  zu adressieren.
- **Expected Filter-Delta step-008:** Unit 368 → **589** (+221),
  Integration 113 → **113** (unverändert), Total 1325 → **1325**
  (unverändert). Konkret: 8 (`PathNormalizerTests` Facts +
  InlineData-Reihen) + 179 (`RuleLegendRegistryTests` Facts +
  3×59 MemberData-Cases) + 30 (`ViolationMarkdownFormatterTests`
  Facts) + 4 (`ViolationSummaryBuilderTests` Facts) = 221 neue
  Unit-Test-Cases. **Diskrepanz Methoden (43) vs. Test-Cases
  (221):** die 178 fehlenden Methoden kommen ausschließlich aus
  der `[Theory]+[MemberData]`-Expansion in
  `RuleLegendRegistryTests` (5 Methoden → 179 Test-Cases, davon
  174 = 3×58 zusätzliche Cases jenseits der 5 Methoden).
- **Bezug zu TD-002 (Subject-Disziplin):** der konkrete Subject-
  Vorschlag (68 Zeichen) im DoD ist direkter Ausfluss der
  TD-002-Empfehlung Variante (a) "Planer-Disziplin + Skill-
  Präzisierung". Der Coder akzeptiert den Vorschlag unverändert
  (Pattern aus step-002/003/004/005/006/007).
- **Bezug zu TD-003 (EOL-Inhomogenität):** TD-003 betrifft
  `McpLintConsoleTests.cs` (LF-only, step-007). Die 4
  step-008-Dateien sind uniform CRLF (verifiziert) — TD-003
  bleibt nach step-008 unverändert (1 von 10 Dateien LF-only
  im `Output/`-Ordner). Falls der Nutzer die TD-003-Konsolidierung
  wünscht, ist das ein eigenständiger Folge-Schritt (siehe
  `tech-debt.md` TD-003 Variante (a) `git add --renormalize .`).
- **Heuristik-Fortschreibung für Folge-Batches:** Punkt 6
  ("Helper-Klassen ohne Testmethoden sind keine Testklassen")
  ist eine **dauerhafte** Regel, die nach step-007 (1. Anwendung)
  + step-008 (2. Anwendung, ohne Ausnahme) **vollständig
  etabliert** ist und ab sofort auf alle EPIC-02-Folge-Batches
  angewandt wird. Konkret: bei jedem zukünftigen Planer-Schritt-2
  wird die Klassen-Zahl eines Batches per
  `grep -cE '\[(Fact|Theory)\]'` pro Zieldatei verifiziert;
  Dateien mit 0 Treffern werden aus dem Tagging-Scope
  ausgenommen und in der Bestandsaufnahme explizit als
  "Helper (ausgenommen)" aufgeführt.
- **Anti-Loop-Hinweis für step-009+:** die `Output/`-CodeMap-
  Zeile wird im step-008-Doku-Commit finalisiert ("9 Test-
  Klassen + 1 Helper, Output/-Schnitt vollständig abgeschlossen,
  step-007 + step-008 done"). Der nächste Planer findet dort
  die abgeschlossene Schnitt-Info + Helper-Ausnahme-Begründung
  vor und muss den `Output/`-Bereich nicht erneut rekonstruieren.
  Er kann direkt mit dem nächsten EPIC-02-Batch (z. B.
  `Configuration/`, 8 Klassen, rein Unit) beginnen.
- **`KnownRuleNames.Count = 59` als langfristig stabiler Wert:**
  die 59 Regeln sind hartkodiert in den 4 `RuleRegistry*.cs`-
  Dateien. Eine Änderung der Zahl ist nur durch Hinzufügen/
  Entfernen einer `BuildXxx()`-Methode oder einer `new(...)`-
  Definition in den 4 Dateien möglich — beides sind
  Produktionscode-Änderungen, die über einen eigenen
  Konzept-Schritt laufen würden. Der Planer für step-009+
  kann also **dauerhaft** mit `KnownRuleNames.Count = 59`
  rechnen, solange keine neue Regel hinzukommt (was im Rahmen
  der EPIC-02-Batch-Reihe **nicht** zu erwarten ist — EPIC-02
  ist ein reines Test-Tagging, kein Regelwerk-Refactoring).
