---
status: done (pending audit)
type: step-plan
task: flaky-and-test-performance
step: 007
corrects: null
title: "Category-Traits für erste 5 Output-Tests nachziehen (Batch 6 von N, Output Teil 1/2)"
epic: EPIC-02
estimated_risk: low
step_type: batch
items:
  - id: item-01
    title: "DebtReportBuilderHeaderTests →’ Unit (in-process DebtReportBuilder.BuildAsync; // @covers DebtReportBuilder Coverage-Marker)"
    source: "konzept.md §Wie Schritt 2, §Muss-Haven Traits-Punkt"
  - id: item-02
    title: "DebtReportBuilderTests →’ Unit (in-process DebtReportBuilder.BuildAsync + File-IO auf TempDir)"
    source: "konzept.md §Wie Schritt 2"
  - id: item-03
    title: "LinterErrorFormatterTests →’ Unit (in-process LinterErrorFormatter.Format + LinterErrorCodes + TestLintConsole-Mock; XML-Doc über der Klasse)"
    source: "konzept.md §Wie Schritt 2"
  - id: item-04
    title: "McpLintConsoleTests →’ Unit (in-process McpLintConsole.Instance; XML-Doc; 3— method-level Unit-Traits additiv)"
    source: "konzept.md §Wie Schritt 2"
  - id: item-05
    title: "OutputRootResolverTests →’ Unit (in-process OutputRootResolver.Resolve + Path/IO auf TempDir)"
    source: "konzept.md §Wie Schritt 2"
created_by: planer
created_by_model: MiniMax-M3
created_by_model_knowledge_cutoff: 2026-01
created_at: 2026-08-07T14:15:00+02:00
related_to: []
---

# Step 007: Category-Traits für Output-Tests Teil 1/2 (Batch 6)

## Bezug

- **Task:** `flaky-and-test-performance`
- **Epic:** `EPIC-02` aus `roadmap.md` — Category-Traits flächendeckend
  nachziehen. Sechster von N Batches; **erster** von zwei alphabetisch
  geschnittenen `Output/`-Teilbatches (5+4, da 9 Test-Klassen den
  8-Item-Deckel von `spec.md` §10.6 reihen würden).
- **Konzept-Referenz:** `konzept.md` §"Wie" Schritt 2 ("Category-Traits
  nachziehen — alle ~1000 ungetraggten Tests einordnen"), §"Muss-Haben"
  Traits-Punkt ("konsequente Category-Traits ... auf **allen** Tests —
  aktuell nur 86 von ~1087"), §"Definition of Done" Punkt "Alle Tests
  tragen einen Category-Trait".
- **Vorgänger-Steps:** `step-001` (EPIC-01, approved, Spike-Befund
  negativ), `step-002` (EPIC-02 Batch 1, Suppression, 8 Klassen,
  approved), `step-003` (EPIC-02 Batch 2, Metrics, 7 Klassen, approved),
  `step-004` (EPIC-02 Batch 3, Web, 5 Klassen, approved),
  `step-005` (EPIC-02 Batch 4, Arch/Diag/FalsePositives/Cache,
  7 Klassen, approved), `step-006` (EPIC-02 Batch 5, Evals, 3 Klassen,
  approved). Die fünf vorherigen Batches lieferten die etablierte
  Klassifikations-Heuristik (Subprozess-Marker = Integration; sonst
  Unit), die Trait-Syntax-Konvention (`[Trait("Category", "Unit")]`,
  CamelCase-Großbuchstabe), die Trait-Platzierungs-Bibliothek
  (Standard-Insert, `// @covers`-Block+Trait, XML-Doc+Trait,
  `IDisposable`-Variante, `IDisposable`+XML-Doc, additive
  method-level-Traits), die Heuristik-Punkte 1–5 (Klassen-Homogenität
  →’ Klassen-Trait; bestehende Traits respektieren/additiv ergänzen;
  `null!` als Edge-Input; Klassen-Trait additiv zu bestehenden
  method-level Traits bei homogenen Klassen; Hypothesen-Auflösungs-
  Pflicht für offene "möglicherweise…¦"-Annotationen in der CodeMap),
  und die DoD-Struktur (Build grün, Voll-Test grün, Unit-Filter grün,
  Integration-Filter best-effort, Self-Lint `OK`, numerische
  Plausibilitätsprüfung, konkreter Subject-Vorschlag mit exakter
  Längen-Angabe).
- **`Output/`-Schnitt-Entscheidung** (siehe "Aktueller Projektzustand"
  §Schnitt-Begründung unten): **alphabetisch 5+4** — step-007 = erste
  5 Klassen (`DebtReportBuilderHeaderTests`, `DebtReportBuilderTests`,
  `LinterErrorFormatterTests`, `McpLintConsoleTests`,
  `OutputRootResolverTests`), step-008 (vom nächsten Planer zu
  planen) = restliche 4 Klassen (`PathNormalizerTests`,
  `RuleLegendRegistryTests`, `ViolationMarkdownFormatterTests`,
  `ViolationSummaryBuilderTests`). Diese Information ist in der
  `roadmap.md` EPIC-02-Zeile (Stand step-007-Plan) explizit
  festgehalten — der nächste Planer-Aufruf findet die Schnitt-Info
  dort dokumentiert vor.
- **Anti-Loop-Check** gegen `codemap.md` (Stand 2026-08-07, 47 Einträge,
  6 Sektionen): die `Output/`-Zeile in der CodeMap-Sektion „Test-
  Verzeichnisse — geplant für EPIC-02-Folge-Batches" trägt aktuell die
  Annotation "`Output/` — 10 Klassen; rein Unit, dito (zuletzt:
  step-002)" — **keine** offene Hypothese, **keine** bestehende
  Entscheidung, die diesem Schnitt widerspricht. **Befund zur
  CodeMap-Korrektheit:** die "10 Klassen"-Annotation ist eine
  unpräzise Zählung — `TestLintConsole.cs` ist kein Test, sondern ein
  `internal sealed class TestLintConsole : ILintConsole`-Mock ohne
  `[Fact]`/`[Theory]`-Methoden. Die korrekte Zahl ist **9 Test-Klassen
  + 1 Helper**. Der Coder aktualisiert die `Output/`-CodeMap-Zeile im
  Doku-Commit auf "9 Test-Klassen + 1 Helper (`TestLintConsole.cs`,
  ausgenommen)" + Hinweis auf step-007/008-Schnitt + Verweis auf die
  Heuristik-Fortschreibung Punkt 6 (siehe unten). **Keine weitere
  bestehende Entscheidung** in der CodeMap widerspricht diesem Plan.

## Aktueller Projektzustand (JIT-Kontext)

Beim Lesen der fünf Zieldateien und der `Output/`-Ordner-Inventur
vorgefunden (relevant für step-007):

- **Ziel-Ordner-Inventar (`Output/`, 10 `.cs`-Dateien, davon 9
  Test-Klassen + 1 Helper):**
  - `src/AiNetLinter.Tests/Output/DebtReportBuilderHeaderTests.cs` —
    1 Test-Klasse, 3 `[Fact]`
  - `src/AiNetLinter.Tests/Output/DebtReportBuilderTests.cs` — 1
    Test-Klasse, 1 `[Fact]`
  - `src/AiNetLinter.Tests/Output/LinterErrorFormatterTests.cs` — 1
    Test-Klasse, 6 `[Fact]`
  - `src/AiNetLinter.Tests/Output/McpLintConsoleTests.cs` — 1
    Test-Klasse, 3 `[Fact]` (alle 3 mit method-level
    `[Trait("Category", "Unit")]`)
  - `src/AiNetLinter.Tests/Output/OutputRootResolverTests.cs` — 1
    Test-Klasse, 3 `[Fact]`
  - `src/AiNetLinter.Tests/Output/PathNormalizerTests.cs` — 1
    Test-Klasse, 3 `[Fact]` + 1 `[Theory]` mit 5 `[InlineData]`
    (**step-008**)
  - `src/AiNetLinter.Tests/Output/RuleLegendRegistryTests.cs` — 1
    Test-Klasse, 2 `[Fact]` + 3 `[Theory]` mit 3 `[MemberData]`
    (**step-008**)
  - `src/AiNetLinter.Tests/Output/ViolationMarkdownFormatterTests.cs`
    — 1 Test-Klasse, 30 `[Fact]`, 473 Zeilen (**step-008**)
  - `src/AiNetLinter.Tests/Output/ViolationSummaryBuilderTests.cs` —
    1 Test-Klasse, 4 `[Fact]` (**step-008**)
  - `src/AiNetLinter.Tests/Output/TestLintConsole.cs` — **1 Helper-
    Klasse** (`internal sealed class TestLintConsole : ILintConsole`,
    20 Zeilen, ohne `[Fact]`/`[Theory]`), wird **nicht** mit
    `[Trait]` versehen (siehe Heuristik-Punkt 6 unten).
- **`Output/`-Schnitt-Begründung (5+4 alphabetisch):**
  - **Warum überhaupt zwei Batches:** 9 Test-Klassen in `Output/`
    (ohne den Helper) überschreiten den 8-Item-Deckel von
    `spec.md` §10.6 (`max_batch_items: 8`) um 1 — der 9-Klassen-
    Ordner muss in zwei Batches aufgeteilt werden.
  - **Warum alphabetisch (statt semantisch oder nach LOC):** der
    `Output/`-Ordner ist flach (keine Unterordner, keine thematische
    Cluster-Bildung — `DebtReportBuilder`, `LinterErrorFormatter`,
    `McpLintConsole`, `OutputRootResolver`, `PathNormalizer`,
    `RuleLegendRegistry`, `ViolationMarkdownFormatter`,
    `ViolationSummaryBuilder` sind acht thematisch unabhängige
    Domänen-Klassen rund um die Output-/Reporting-Pipeline).
    Alphabetische Sortierung liefert die **einzige objektive,
    planer-unabhängige Schnittlinie** ohne Wertungs- oder
    Komplexitäts-Ermessensspielraum. Sie liefert zudem die für den
    nächsten Planer-Aufruf **am einfachsten wiederauffindbare**
    Schnitt-Info ("step-007 = D-O, step-008 = P-V") — der nächste
    Planer liest diese Zeile und kann ohne erneute Schnitt-Diskussion
    direkt mit dem step-008-Plan beginnen.
  - **Warum 5+4 (nicht 4+5 oder andere Aufteilung):** die natürliche
    Trennlinie im Alphabet ist `O` (Output) →’ `P` (Path), d. h. nach
    `OutputRootResolverTests` folgt `PathNormalizerTests`. Damit
    bleiben in step-007 genau **5** Klassen (alle vor `P`) und in
    step-008 genau **4** Klassen (alle ab `P`). Die 5+4-Verteilung
    gibt step-007 die Mehrheits-Last (5 Items) und step-008 die
    "Schlussstein"-Last mit der größten Einzel-Datei
    (`ViolationMarkdownFormatterTests.cs` = 30 `[Fact]`, 473 Zeilen) —
    passt zum EPIC-02-Muster "kleinere/mittlere Batches zuerst,
    Heavyweight-Files zuletzt" (vgl. step-002 →’ step-003 →’ step-004
    →’ step-005 →’ step-006 mit konstant abnehmender Klassen-Zahl).
  - **Warum nicht "Output/ + Configuration/-Mischung":** der
    alternativ vorgeschlagene Misch-Batch (Output/ Teil 1 + 3
    Configuration/-Klassen) würde zwar die Batch-Anzahl reduzieren,
    aber: (a) `Configuration/` hat 8 Klassen (auch > 8-Item-Deckel
    nach Abzug der 3 für step-007 verbleiben 5 für step-008 — passt
    zwar, aber die Schritt-Konstruktion wird komplexer); (b) das
    Mischen zweier Ordner in einem Step verletzt die etablierte
    "1 Ordner = 1 Batch"-Linie (step-002 bis step-006); (c) der
    Folge-Step müsste die verbleibenden 4 Output/-Klassen + 5
    Configuration/-Klassen = 9 Items mischen — wieder über dem
    8-Item-Deckel. Der reine Output/-Split ist die **einfachste und
    vorhersagbarste** Variante.
  - **Klassen-Verteilung step-007 (5 Test-Klassen, alle Unit):**
    - `DebtReportBuilderHeaderTests.cs` (1 Datei) — 3 `[Fact]`,
      Coverage-Marker `// @covers DebtReportBuilder`
    - `DebtReportBuilderTests.cs` (1 Datei) — 1 `[Fact]`,
      Standard-Variante
    - `LinterErrorFormatterTests.cs` (1 Datei) — 6 `[Fact]`,
      XML-Doc über der Klasse
    - `McpLintConsoleTests.cs` (1 Datei) — 3 `[Fact]` + 3— method-
      level `Unit`-Trait (additiv), XML-Doc über der Klasse
    - `OutputRootResolverTests.cs` (1 Datei) — 3 `[Fact]`,
      Standard-Variante
- **Konzept-/CodeMap-Schätzung vs. Realität:** CodeMap sagt "10
  Klassen" — die Inventur zeigt **9 Test-Klassen + 1 Helper**
  (siehe Heuristik-Punkt 6). Die Korrektur wird im Coder-Commit
  nachgepflegt.
- **Bestehende Trait-Verteilung** (verifiziert per `grep -c '\[Trait\('`
  über die 5 step-007-Dateien):
  - `DebtReportBuilderHeaderTests.cs`: 0 Klassen-Traits, 0
    method-level Traits
  - `DebtReportBuilderTests.cs`: 0 / 0
  - `LinterErrorFormatterTests.cs`: 0 / 0
  - `McpLintConsoleTests.cs`: 0 / 3 (alle 3 `Unit`, je einer pro
    `[Fact]`-Methode — verifiziert per `grep -c '\[Trait\('` = 3)
  - `OutputRootResolverTests.cs`: 0 / 0
  - **Insgesamt: 0 Klassen-Traits, 3 method-level Traits (alle
    `Unit` in `McpLintConsoleTests`).** 4 von 5 Klassen sind
    "jungfräulich" (keinerlei Trait-Attribute), 1 Klasse hat
    method-level Traits auf allen Methoden (homogen Unit).
- **Subprozess-Marker im gesamten 5-Datei-Set** (verifiziert per
  `grep -cE 'Process\.Start|McpTestClient|CliProcessRunner|Program\.Main|IClassFixture'`
  über alle 5 Dateien): **0/0/0/0/0 Treffer pro Datei.** Damit ist
  der gesamte Batch homogen **Unit** — keine Integration-Klasse.
  Passt zur etablierten Heuristik (Punkte 1–3) und zur
  step-002/003/004/005/006-Bestätigung.
- **Testmethoden-Inventar step-007** (regex-basiert per
  `grep -cE '\[(Fact|Theory)\]'`):

  | Datei                                       | `[Fact]` | `[Theory]` |
  |---------------------------------------------|---------:|-----------:|
  | `Output/DebtReportBuilderHeaderTests.cs`    |        3 |          0 |
  | `Output/DebtReportBuilderTests.cs`          |        1 |          0 |
  | `Output/LinterErrorFormatterTests.cs`       |        6 |          0 |
  | `Output/McpLintConsoleTests.cs`             |        3 |          0 |
  | `Output/OutputRootResolverTests.cs`         |        3 |          0 |
  | **Summe step-007**                          |   **16** |     **0**  |

  Alle 16 sind `[Fact]`, keine `[Theory]` mit `[InlineData]`-Reihen.
  Davon sind 3 in `McpLintConsoleTests.cs` bereits method-level
  `Unit` getaggt (seit step-005/006 in den 355-Unit-Filter
  eingerechnet). **Erwartetes Filter-Delta nach step-007:** Unit
  steigt um **+13** (= 16 -’ 3), Integration unverändert, Total
  unverändert. Konkret: Unit 355 →’ **368**, Integration **113**,
  Total **1325** (nachvollziehbar im `step-result.md` zu
  verifizieren). Das ist ein typisch kleiner Batch (vergleichbar
  step-006 mit +23, aber mit Overlap-Abzug wegen der 3
  method-level Traits).
- **Klassen-Deklarationen — Trait-Platzierungs-Varianten** (verifiziert
  per `grep -nE 'public sealed class|/// <summary>|// @covers'`
  über die 5 Dateien):
  - **Standard-Insert zwischen `namespace …¦;` und `public sealed
    class …¦`** (2 Klassen, kein XML-Doc, kein `// @covers`-Marker,
    kein `: IDisposable`):
    - `DebtReportBuilderTests.cs:9` (Klasse deklariert ohne
      XML-Doc, ohne `// @covers`; analog zu `ArchitectureTests`
      aus step-005)
    - `OutputRootResolverTests.cs:5` (dito)
  - **Standard-Insert zwischen `namespace …¦;` und `public sealed
    class …¦` mit `// @covers`-Block** (1 Klasse, Coverage-Marker
    bleibt direkt am Symbol, Trait darunter — analog zu
    `PerformanceProfilerTests` aus step-005):
    - `DebtReportBuilderHeaderTests.cs:7-8` (`// @covers
      DebtReportBuilder` Z. 7, Leerzeile Z. 8, Klasse Z. 9)
  - **XML-Doc-Variante zwischen `</summary>` und `public sealed
    class …¦`** (1 Klasse, XML-Doc über der Klasse, Trait
    dazwischen — analog zu `FalsePositiveTests` aus step-005):
    - `LinterErrorFormatterTests.cs:8-11` (XML-Doc Z. 8-10,
      `</summary>` Z. 11, Leerzeile, Klasse Z. 13)
  - **XML-Doc + additive method-level-Traits** (1 Klasse, kombiniert
    XML-Doc-Variante + Heuristik-Punkt 4 — analog zu
    `AnalysisCacheManagerIsolationTests` aus step-005):
    - `McpLintConsoleTests.cs:9-16` (XML-Doc Z. 9-15, `</summary>`
      Z. 16, Leerzeile, Klasse Z. 18; 3 method-level Traits bleiben
      unverändert)
  - Damit sind **alle 5 Trait-Platzierungen lokal pro Datei
    behandelbar**; keine Datei benötigt eine übergeordnete
    Sonderbehandlung. Der 5-Klassen-Batch demonstriert **alle
    drei** Trait-Platzierungs-Hauptvarianten der EPIC-02-Serie
    (Standard, `// @covers`, XML-Doc) + die kombinierte
    XML-Doc-additive-Variante — damit ist die
    Trait-Platzierungs-Bibliothek (aus step-005) praktisch
    vollständig abgedeckt.
- **EOL- und Trailing-NL-Status** (verifiziert per PowerShell-Byte-
  Check über alle 5 step-007-Dateien + die 5 weiteren `Output/`-
  Dateien als Vorgriff für step-008):

  | Datei                                       | BOM  | CR  | LF  | Trailing-NL |
  |---------------------------------------------|------|----:|----:|-------------|
  | `Output/DebtReportBuilderHeaderTests.cs`    |  ✗   |  34 |  34 |     ✓       |
  | `Output/DebtReportBuilderTests.cs`          |  ✗   |  50 |  50 |     ✓       |
  | `Output/LinterErrorFormatterTests.cs`       |  ✗   |  79 |  79 |     ✓       |
  | `Output/McpLintConsoleTests.cs`             |  ✗   |  62 |  62 |     ✓       |
  | `Output/OutputRootResolverTests.cs`         |  ✗   |  50 |  50 |     ✓       |
  | `Output/PathNormalizerTests.cs`             |  ✗   |  47 |  47 |     ✓       |
  | `Output/RuleLegendRegistryTests.cs`         |  ✗   |  66 |  66 |     ✓       |
  | `Output/TestLintConsole.cs` (Helper)        |  ✗   |  20 |  20 |     ✓       |
  | `Output/ViolationMarkdownFormatterTests.cs` |  ✗   | 473 | 473 |     ✓       |
  | `Output/ViolationSummaryBuilderTests.cs`    |  ✗   |  93 |  93 |     ✓       |

  **Homogenität über alle 10 `Output/`-Dateien:** **kein** BOM (alle
  10 ohne UTF-8-BOM, erste 3 Bytes jeweils der `#nullable enable`-
  oder `using`-Anfang, kein `EF BB BF`), **uniform CRLF** (CR-Zahl =
  LF-Zahl in allen 10 Dateien, kein gemischter Status), **Trailing-
  NL überall** (letztes Byte = LF in allen 10 Dateien). Damit kann
  der Coder alle 5 step-007-Edits mit dem **Standard-Edit-Tool**
  durchführen, ohne Diff-Aufblähung befürchten zu müssen (anders
  als in step-004, wo `Web/`-Dateien gemischte EOL-Status hatten
  und einen byte-genauen Python-Helper nötig machten). Der Coder
  verifiziert dies vorab per
  `Get-Content -Encoding UTF8 <file> | Select-Object -Last 1` (zeigt
  das letzte Byte-Zeichen) und durch `git diff` nach dem Edit.
- **Spezialfall `McpLintConsoleTests` — bereits 3— method-level
  `Unit` getaggt:** 3 method-level `[Trait("Category", "Unit")]`-
  Attribute sind vorhanden (Z. 19, 38, 57 in der Datei). Das
  Hinzufügen des Klassen-Traits ist **rein additiv** (xUnit-Trait-
  Filter wertet Klassen-Oder-Methoden-Trait, keine Doppelt-
  Zählung). Die 3 method-level Traits bleiben **unverändert** —
  der Klassen-Trait wird **zwischen** dem XML-Doc (Z. 15/16) und
  der Klassendeklaration (Z. 18) eingefügt. Das ist **identisch**
  zur `AnalysisCacheManagerIsolationTests`-Variante aus step-005
  (Heuristik-Punkt 4).
- **Bündelungs-Begründung (5 Klassen in 1 Ordner-Halb-Batch):**
  die 5 step-007-Klassen sind die alphabetisch erste Hälfte der
  9 `Output/`-Test-Klassen. Eine Aufteilung in 5 Einzel-Step-
  Planungen wäre reiner Overhead ohne Mehrwert. **Vorteile der
  Bündelung:** (1) **berechtigt durch homogenen Charakter** —
  alle 5 Klassen sind `Unit` ohne Subprozess-Marker, einheitliche
  Heuristik-Anwendung; (2) **Klassen-Level-Mix bleibt
  überschaubar** — keine Integration-Klasse im Set, also kein
  Misch-Heuristik-Diskussion; (3) **Trait-Platzierungs-Varianten
  sind lokal pro Datei** behandelbar (siehe Aufzählung oben),
  keine übergeordneten Sonderfälle; (4) **passt in den 8-Item-
  Deckel** (5 Items + 3 Slots Reserve); (5) **kleiner Diff-Umfang**
  (5 Trait-Zeilen + ggf. Doku-Commit, deutlich unter dem 40-Zeilen-
  Deckel); (6) **folgt der step-002/003/004/005/006-Logik** für
  "1 (Halb-)Ordner = 1 Batch" der kleinen/mittleren Unit-Ordner.
- **Alternative, verworfen — `Output/` komplett in einem Step:**
  wäre ein 9-Item-Batch, der den 8-Item-Deckel reißt; müsste
  vorab in zwei Batches aufgeteilt werden. Daher ist der
  5+4-Schnitt zwingend.
- **Alternative, verworfen — semantischer Schnitt (nach Datei-
  Typ):** Klassen gruppieren sich lose in "Builder" (`DebtReport-`,
  `ViolationSummaryBuilder`), "Formatter" (`LinterErrorFormatter`,
  `ViolationMarkdownFormatter`), "Resolver" (`OutputRootResolver`,
  `PathNormalizer`), "Registry" (`RuleLegendRegistry`),
  "Console" (`McpLintConsole`). Ein semantischer Schnitt
  ("alle Builder + Formatter" / "alle Resolver + Registry + Console")
  wäre 4+5 (oder 5+4) — derselbe Effekt wie alphabetisch, aber
  ohne objektive Reihenfolge und mit Wertungsbedarf. Alphabetisch
  ist objektiv, planer-unabhängig und für den nächsten Planer
  trivial wiederauffindbar.

## Intention

Alle 5 Testklassen in der alphabetisch ersten Hälfte des `Output/`-
Ordners (`DebtReportBuilderHeaderTests`, `DebtReportBuilderTests`,
`LinterErrorFormatterTests`, `McpLintConsoleTests`,
`OutputRootResolverTests`) mit `[Trait("Category", "Unit")]` auf
Klassen-Ebene versehen. Dieser Step ist der sechste von N Batches,
die zusammen die EPIC-02-DoD erreichen ("alle ~1000 Tests getraggt"),
und der erste von zwei alphabetisch geschnittenen `Output/`-
Teilbatches. Er liefert die sechste Template-Validierung für die
Folge-Batches, **bevor** diese in die größeren, gemischten
Verzeichnisse (`Configuration/`, `Core/Checkers/`, `Mcp/`, `Commands/`,
`Cli/`) vorstoßen.

Der Step liefert **drei nennenswerte Befunde**:

1. **`Output/`-Schnitt-Entscheidung dokumentiert:** der Ordner enthält
   10 `.cs`-Dateien, davon 9 Test-Klassen + 1 Helper. Die korrekte
   Tagging-Zahl ist 9, nicht 10 — siehe Heuristik-Punkt 6 unten.
   Der Schnitt ist alphabetisch 5+4 (step-007 = D–O, step-008 = P–V)
   mit klarer Begründung im Plan und in der `roadmap.md` EPIC-02-
   Zeile. Der nächste Planer-Aufruf findet damit eine eindeutige
   Schnitt-Info vor und kann den step-008-Plan ohne erneute
   Schnitt-Diskussion schreiben.
2. **Trait-Platzierungs-Bibliothek praktisch vollständig
   validiert:** die 5 step-007-Klassen demonstrieren alle drei
   Hauptvarianten (Standard, `// @covers`, XML-Doc) + die kombinierte
   XML-Doc-additive-Variante. Damit ist die in step-005 etablierte
   Trait-Platzierungs-Bibliothek in diesem Batch vollständig
   abgedeckt — der nächste Planer muss keine neue Platzierungs-
   Variante mehr einführen.
3. **EOL/BOM-Homogenität in `Output/` bestätigt:** alle 10
   `Output/`-Dateien uniform CRLF + Trailing-NL, kein BOM, kein
   gemischter Status. Der Coder kann alle 5 Edits mit dem
   Standard-Edit-Tool durchführen — kein byte-genauer Python-Helper
   nötig (anders als in step-004, wo `Web/`-Dateien LF/CRLF
   gemischt hatten). Die Homogenität erstreckt sich auch auf die
   5 step-008-Dateien, was deren Planung vereinfacht.

## Klassifikations-Heuristik für diesen Batch

Die in step-002 dokumentierte und in step-003/004/005/006 bestätigte
Heuristik wird unverändert übernommen:

1. **Bestehende Traits prüfen.** Im Batch sind 0 Klassen-Traits in
   allen 5 Dateien und 3 method-level `[Trait(`-Vorkommen in
   `McpLintConsoleTests` (alle `Unit`, verifiziert per `grep -c`).
   Die 3 method-level Traits bleiben **unverändert** und werden
   durch den Klassen-Trait **additiv** ergänzt — keine bestehenden
   Trait-Attribute werden überschrieben, entfernt oder modifiziert
   (In den übrigen 4 Klassen des Batches sind 0 bestehende
   Trait-Attribute vorhanden.)
2. **Subprozess-Marker prüfen.** Im Batch sind 0 Subprozess-Marker
   vorhanden (verifiziert per `grep -cE 'Process\.Start|
   McpTestClient|CliProcessRunner|Program\.Main|IClassFixture'`
   über alle 5 Dateien, 0/0/0/0/0 Treffer). Damit ist **keine**
   Klasse in diesem Batch `Integration`.
3. **Sonst: Unit.** Trifft auf alle 5 Klassen in diesem Batch zu.

**Wichtige Negativ-Abgrenzung** (aus step-002/003/005/006, weiterhin
gültig, an den 5 Kandidaten verifiziert): die folgenden Muster sind
**KEIN** Subprozess und führen nicht zu `Integration`:

- `DebtReportBuilder.BuildAsync(...)` (in `DebtReportBuilderHeaderTests`,
  `DebtReportBuilderTests`) — in-process Produktionscode-Aufruf mit
  `File.WriteAllTextAsync` / `File.WriteAllText` / `Directory.CreateDirectory` /
  `Directory.Delete` auf `Path.GetTempPath()` (in-process File-IO,
  kein Subprozess)
- `LinterErrorFormatter.Format(...)` + `LinterErrorCodes.*` + `TestLintConsole`-
  Mock (in `LinterErrorFormatterTests`) — in-process Format-Logik
  mit Mock-Capture, kein Subprozess
- `McpLintConsole.Instance.WriteLine(...)` / `WriteError(...)` (in
  `McpLintConsoleTests`) — in-process Singleton-Aufruf mit
  `Console.SetError(capture)`-Capture-Pattern, kein Subprozess
  (Negativ-Abgrenzung analog zu `MaxDirectoryChildrenTests` step-003
  und `EvalAssemblerTests` step-006)
- `OutputRootResolver.Resolve(...)` (in `OutputRootResolverTests`) —
  in-process Path/IO auf TempDir, kein Subprozess
- `Console.SetError(capture)` / `Console.SetError(originalError)` (in
  `McpLintConsoleTests.cs:22-33, 41-52`) — in-process stderr-Capture,
  kein Subprozess
- `IClassFixture<…¦>` — **kein** Vorkommen in den 5 Dateien
  (verifiziert per `grep -c 'IClassFixture'`, 0 Treffer pro Datei)

**Heuristik-Punkt 6 (neu in step-007, Folge auf Punkt 5 aus step-006
"Hypothese-Auflösungs-Pflicht für offene "möglicherweise…¦"-Annotationen
in der CodeMap"):** **Helper-Klassen ohne Testmethoden sind keine
Testklassen.** Eine `.cs`-Datei, die keine `[Fact]`- oder `[Theory]`-
Annotationen enthält, ist **per Definition kein Test** und darf
**nicht** mit `[Trait("Category", ...)]` versehen werden — das
Trait-Attribut wäre semantisch falsch (es klassifiziert Tests, nicht
Hilfsklassen) und würde die Filter-Statistik verfälschen
(`Category=Unit`-Filter würde sonst Mock-/Helper-Klassen-Methoden
mitzählen, obwohl dort keine Tests laufen).

Konkret für `Output/`: der Ordner enthält 10 `.cs`-Dateien, davon
9 Test-Klassen (alle mit `[Fact]`/`[Theory]`) + 1 Helper
(`TestLintConsole.cs` = `internal sealed class TestLintConsole :
ILintConsole`, ohne `[Fact]`/`[Theory]`). Die korrekte Tagging-Zahl
ist 9, nicht 10 — die in der `codemap.md` Z. 101 und in der
`roadmap.md` EPIC-02-Zeile dokumentierte "10 Klassen"-Annotation ist
eine **unpräzise Zählung**, die der Coder im Doku-Commit auf "9
Test-Klassen + 1 Helper (`TestLintConsole.cs`, ausgenommen)" mit
explizitem Hinweis auf die `Output/`-Schnitt-Entscheidung
korrigiert.

**Verallgemeinerung der Regel (zukünftige Batches):** vor dem Anwenden
der Klassifikations-Heuristik prüft der Planer/Coder per
`grep -cE '\[(Fact|Theory)\]' <datei>` pro Zieldatei, ob die Datei
überhaupt Testmethoden enthält. Dateien mit 0 Treffern sind
**keine Test-Klassen** und werden aus dem Tagging-Scope
ausgenommen. Die Klassen-Zahl für den Batch umfasst nur Dateien
mit ≥ 1 `[Fact]`/`[Theory]`.

**Heuristik-Punkt 4 (aus step-005, hier nochmals angewandt):**
**Klassen-Trait additiv zu bestehenden method-level Traits bei
homogenen Klassen** — auf `McpLintConsoleTests` (3 method-level
`Unit`-Traits, alle homogen Unit) angewandt: Klassen-Trait wird
**zwischen** `</summary>` (Z. 16) und `public sealed class
McpLintConsoleTests` (Z. 18) eingefügt; die 3 method-level Traits
bleiben **unverändert** (Z. 19, 38, 57 — verifiziert per
`grep -nE '\[Trait\('`). Rein additiv, keine Doppelt-Zählung im
Filter-Lauf.

## Konkrete Änderungen

**Bei `step_type: batch`** (siehe `../spec.md` §10.6): pro Item aus der
`items`-Liste im Frontmatter eine Unterüberschrift.

### item-01: `DebtReportBuilderHeaderTests` →’ Unit — `src/AiNetLinter.Tests/Output/DebtReportBuilderHeaderTests.cs` (Klassen-Deklaration, Z. 8-9, zwischen `// @covers`-Block und Klasse)

- **Was:** Zwischen dem `// @covers DebtReportBuilder`-Kommentar
  (Z. 7) und `public sealed class DebtReportBuilderHeaderTests`
  (Z. 9, getrennt durch Leerzeile Z. 8) eine Zeile
  `[Trait("Category", "Unit")]` einfügen (Z. 8 wird zur Trait-Zeile,
  Klassendeklaration rutscht auf Z. 9). **Achtung:** der
  `// @covers`-Marker bleibt **direkt** am Symbol (Coverage-
  Konvention analog zu `IgnoreSuppressionsFilterTests` aus step-002
  und `PerformanceProfilerTests` aus step-005), der Trait gehört
  **zwischen** den `// @covers`-Block und die Klassendeklaration
  (nicht davor).
- **Warum:** Klasse enthält 3 `[Fact]`-Methoden, die alle
  `DebtReportBuilder.BuildAsync(".")` direkt auf in-process
  Produktionscode aufrufen (verifiziert per Datei-Inspektion;
  Tests beschreiben Header-Formatierungen mit/ohne
  `ignoreSuppressions`-Parameter). Subprozess-Marker-Grep liefert
  0 Treffer. Datei hat **kein** BOM (verifiziert per PowerShell-
  Byte-Check, erste 3 Bytes = `35 110 117` = `#nu` von `using`-
  Anfang) — Standard-Edit-Tool reicht, kein byte-genauer Helper
  nötig. Trait-Wert folgt exakt der bestehenden Konvention
  (`[Trait("Category", "Unit")]`, CamelCase-Großbuchstabe).

### item-02: `DebtReportBuilderTests` →’ Unit — `src/AiNetLinter.Tests/Output/DebtReportBuilderTests.cs` (Klassen-Deklaration, Z. 8-9, Standard-Insert)

- **Was:** Zwischen der Leerzeile nach `namespace
  AiNetLinter.Tests.Output;` (Z. 7) und `public sealed class
  DebtReportBuilderTests` (Z. 9) eine Zeile
  `[Trait("Category", "Unit")]` einfügen (Z. 8 wird zur Trait-Zeile,
  Klassendeklaration rutscht auf Z. 9). Keine XML-Doc, kein
  `// @covers`-Marker, kein `IDisposable` — Standard-Variante
  (analog zu `ArchitectureTests` aus step-005 und zu
  `DebtReportBuilderTests` step-006 `SpecLoaderTests`-Pattern).
- **Warum:** Klasse enthält 1 `[Fact]`-Methode (`BuildAsync_IncludesActiveSuppressionsSection`),
  die `DebtReportBuilder.BuildAsync(tempDir, null)` direkt auf
  in-process Produktionscode aufruft. Verwendet `File.WriteAllTextAsync` /
  `Directory.CreateDirectory` / `Directory.Delete` auf `Path.GetTempPath()`-
  abgeleitetem TempDir (in-process File-IO, kein Subprozess).
  Subprozess-Marker-Grep liefert 0 Treffer. Datei hat **kein** BOM
  (verifiziert) — Standard-Edit-Tool reicht. Trait-Wert folgt
  exakt der Konvention.

### item-03: `LinterErrorFormatterTests` →’ Unit — `src/AiNetLinter.Tests/Output/LinterErrorFormatterTests.cs` (Klassen-Deklaration, Z. 11-13, XML-Doc-Variante)

- **Was:** Zwischen `</summary>` (Z. 11, Ende der XML-Doc-Section
  Z. 8-10) und `public sealed class LinterErrorFormatterTests`
  (Z. 13, getrennt durch Leerzeile Z. 12) eine Zeile
  `[Trait("Category", "Unit")]` einfügen (Z. 12 wird zur Trait-
  Zeile, Klassendeklaration rutscht auf Z. 13). **Achtung:** die
  XML-Doc beginnt auf Z. 8 mit `/// <summary>` und endet auf Z. 11
  mit `///</summary>` — der Trait gehört **zwischen** `</summary>`
  und `public sealed class`, nicht davor (analog zur
  `IgnoreSuppressionsFilter`-Konvention aus step-002 und zur
  XML-Doc-Variante aus step-003/004/005).
- **Warum:** Klasse enthält 6 `[Fact]`-Methoden, die alle
  `LinterErrorFormatter.Format(...)` direkt auf in-process
  Produktionscode aufrufen, plus 1 Methode mit `TestLintConsole`-
  Mock-Capture (in-process). Subprozess-Marker-Grep liefert 0
  Treffer. Datei hat **kein** BOM (verifiziert) — Standard-Edit-
  Tool reicht. Trait-Wert folgt exakt der Konvention.

### item-04: `McpLintConsoleTests` →’ Unit — `src/AiNetLinter.Tests/Output/McpLintConsoleTests.cs` (Klassen-Deklaration, Z. 16-18, XML-Doc-Variante + additive method-level Traits)

- **Was:** Zwischen `</summary>` (Z. 16, Ende der XML-Doc-Section
  Z. 9-15) und `public sealed class McpLintConsoleTests` (Z. 18,
  getrennt durch Leerzeile Z. 17) eine Zeile
  `[Trait("Category", "Unit")]` einfügen (Z. 17 wird zur Trait-
  Zeile, Klassendeklaration rutscht auf Z. 18). **Wichtig:** die
  3 bestehenden method-level `[Trait("Category", "Unit")]`-Attribute
  (Z. 19, 38, 57 — verifiziert per `grep -nE '\[Trait\('`) bleiben
  **unverändert** — der Klassen-Trait ist rein additiv (xUnit-
  Trait-Filter wertet Klassen-Oder-Methoden-Trait, keine Doppelt-
  Zählung im Filter-Lauf). Heuristik-Punkt 4 (aus step-005) ist
  hier angewandt.
- **Warum:** Klasse enthält 3 `[Fact]`-Methoden (alle bereits
  method-level `Unit` getaggt, also homogen Unit), die
  `McpLintConsole.Instance.WriteLine(...)` / `WriteError(...)` direkt
  auf das in-process `McpLintConsole`-Singleton aufrufen. Zwei der
  3 Methoden kapseln `Console.SetError(capture)` / `Console.SetError(originalError)`
  für stderr-Capture (in-process, kein Subprozess — Negativ-
  Abgrenzung analog zu `MaxDirectoryChildrenTests` step-003 und
  `EvalAssemblerTests` step-006). Subprozess-Marker-Grep liefert
  0 Treffer. Datei hat **kein** BOM (verifiziert) — Standard-Edit-
  Tool reicht. Trait-Wert folgt exakt der Konvention.

### item-05: `OutputRootResolverTests` →’ Unit — `src/AiNetLinter.Tests/Output/OutputRootResolverTests.cs` (Klassen-Deklaration, Z. 4-5, Standard-Insert)

- **Was:** Zwischen der Leerzeile nach `namespace
  AiNetLinter.Tests.Output;` (Z. 3) und `public sealed class
  OutputRootResolverTests` (Z. 5) eine Zeile
  `[Trait("Category", "Unit")]` einfügen (Z. 4 wird zur Trait-
  Zeile, Klassendeklaration rutscht auf Z. 5). Keine XML-Doc, kein
  `// @covers`-Marker, kein `IDisposable` — Standard-Variante
  (analog zu `DebtReportBuilderTests` item-02 in diesem Step).
- **Warum:** Klasse enthält 3 `[Fact]`-Methoden, die alle
  `OutputRootResolver.Resolve(...)` direkt auf in-process
  Produktionscode aufrufen. Verwendet `Directory.CreateDirectory` /
  `Directory.Delete` / `File.WriteAllText` auf `Path.GetTempPath()`-
  abgeleitetem TempDir (in-process File-IO, kein Subprozess). Die
  3. Methode (`Resolve_ThrowsWhenPathDoesNotExist`) verwendet
  `Assert.Throws<DirectoryNotFoundException>(...)` — in-process,
  kein Subprozess. Subprozess-Marker-Grep liefert 0 Treffer.
  Datei hat **kein** BOM (verifiziert) — Standard-Edit-Tool
  reicht. Trait-Wert folgt exakt der Konvention.

## Tests

Keine — die Klassifikation ist rein additiv (Attribut setzen).
Existierende Tests müssen **unverändert** grün bleiben. Validierung
erfolgt über den vollen `dotnet test`-Lauf in der Definition of Done
(kein neuer Test, kein geänderter Test).

## Definition of Done

- [ ] Alle 5 Items umgesetzt (je eine `[Trait("Category", "Unit")]`-
      Zeile auf Klassen-Ebene, platziert je nach Datei-Profil:
      Standard-Insert in `DebtReportBuilderTests` + `OutputRootResolverTests`,
      `// @covers`-Block+Trait in `DebtReportBuilderHeaderTests`,
      XML-Doc+Trait in `LinterErrorFormatterTests`,
      XML-Doc+Trait+additive method-level Traits in
      `McpLintConsoleTests` — siehe Aufzählung oben)
- [ ] **Bestehende Traits respektiert:** die 3 method-level
      `[Trait("Category", "Unit")]`-Attribute in
      `McpLintConsoleTests.cs` (Z. 19, 38, 57) bleiben **unverändert**
      (verifiziert per `git diff` nach dem Edit). Nach dem Diff
      sollten in `Output/` alle 5 step-007-Klassen mit Klassen-
      Trait ausgestattet sein; die 3 method-level Traits sind
      weiterhin vorhanden (additiv).
- [ ] **`TestLintConsole.cs` (Helper) NICHT angetastet:** die
      `Output/TestLintConsole.cs`-Datei (Helper-Klasse ohne
      `[Fact]`/`[Theory]`) bleibt **unverändert** — kein
      `[Trait]`-Attribut wird hinzugefügt (Heuristik-Punkt 6).
      Verifiziert per `git diff` (Datei darf nicht im Diff
      auftauchen).
- [ ] **BOM-Erhaltung:** alle 5 step-007-Dateien haben **kein** UTF-8-
      BOM (verifiziert per PowerShell-Byte-Check vor und nach dem
      Edit, erste 3 Bytes = `35 110 117` = `#nu` von `using`-
      Anfang ≠ `EF BB BF`). Da kein BOM vorhanden ist, gibt es
      **nichts** zu erhalten — anders als in step-002/005 mit 3
      BOM-Dateien. Der Standard-Edit-Tool-Pfad reicht.
- [ ] **EOL/Trailing-NL-Konservierung:** alle 5 step-007-Dateien
      behalten CRLF-Zeilenenden und Trailing-NL nach dem Edit
      (verifiziert per PowerShell-`Select-String`-Prüfung und/oder
      `git diff` — keine Zeilenende-Änderungen). Bei diesem Batch
      **kein** byte-genauer Python-Helper nötig (anders als in
      step-004), weil alle 5 Dateien uniform CRLF + Trailing-NL
      haben — Standard-Edit-Tool reicht.
- [ ] **Build-Command** aus Tech-Stack-Notiz (`roadmap.md`) grün:
      `dotnet build` (Zero-Warning-Direktive, `TreatWarningsAsErrors=true`
      in beiden Projekten)
- [ ] **Test-Command** aus Tech-Stack-Notiz grün: `dotnet test`
      (voller Lauf, alle Tests müssen weiterhin grün sein — keine
      Test-Logik wurde geändert)
- [ ] **Trait-Filter-Smoke-Test** (vom Coder zusätzlich durchführen, um
      die Klassifikation zu verifizieren):
      - `dotnet test --no-build --filter "Category=Unit"` →’ muss grün sein
      - `dotnet test --no-build --filter "Category=Integration"` →’
        **best-effort, ein Lauf grün** (gemäß step-002/003/004/005/006
        NITPICK-Linie: pre-existing Flaky-Test
        `McpServerCommandLoadingStateTests.LoadState_LoadFuncCompletesSynchronouslyWithCatalog_ReportsLoadedImmediately`
        flake-t gelegentlich unter Last des Integration-Filters; nicht
        step-007-verursacht, Fix in EPIC-06). Der Coder dokumentiert im
        `step-result.md`, wenn der Lauf flaky ist, und startet ihn ggf.
        einmal neu.
      - **Numerische Plausibilitätsprüfung** (gemäß step-003-Review
        NITPICK "regex statt manuell zählen"): der Coder zählt die
        `[Fact]`/`[Theory]`-Methoden in den 5 Klassen **regex-basiert**
        per `grep -cE '\[(Fact|Theory)\]'` (NICHT manuell durchgehen),
        dokumentiert die Summe im `step-result.md` und vergleicht sie
        mit dem erwarteten Unit-Filter-Delta. **Erwartetes Delta:**
        Unit steigt um **+13** (3+1+6+3+3 = 16, davon 3 bereits
        method-level getaggt in `McpLintConsoleTests` →’ 13 neu für den
        Unit-Filter; verifiziert per `grep -c '\[Trait\('` durch den
        Planer; siehe "Aktueller Projektzustand" oben). Integration-
        Zahl bleibt unverändert bei 113. Total bleibt unverändert bei
        1325. **Erwarteter Unit-Filter-Wert nach step-007: 368**
        (355 + 13).
- [ ] **Self-Lint** (TD-001-konform, semantisch identisch zu
      `--self-lint`): `dotnet run --project src/AiNetLinter --
      --config rules.json --path .` →’ muss `OK` ausgeben
- [ ] **Commit auf aktuellem Branch** (Conventional Commit auf Deutsch,
      imperativ, mit Task-Suffix `[flaky-and-test-performance]`):
      **konkreter Subject-Vorschlag** (gemäß TD-002, "kürzere
      Subject-Bodies vorgeben"):
      `test: Output-Tests Kategorie-taggen 1/2 [flaky-and-test-performance]`
      →’ **68 Zeichen** inkl. Suffix (exakt verifiziert per
      `('test: Output-Tests Kategorie-taggen 1/2 [flaky-and-test-performance]').Length`
      in PowerShell = `68`; deckt 4 Zeichen Sicherheitsabstand zur
      72-Zeichen-Grenze). Pattern spiegelt step-006's `test:
      Evals-Tests Kategorie-taggen [flaky-and-test-performance]`
      (63 Zeichen) — gleicher Aufbau, konsistent zur EPIC-02-Batch-
      Serie. **"1/2"** markiert den Output/-Halb-Batch-Schnitt
      (step-007 = erste Hälfte alphabetisch D–O, step-008 =
      zweite Hälfte alphabetisch P–V). **Falls** der Coder den
      Subject abwandeln will, **muss** er 72 Zeichen einhalten und
      die neue exakte Länge im `step-result.md` dokumentieren — bei
      Überschreitung TD-002-Eintrag aktualisieren.
- [ ] `step-007/step-result.md` geschrieben mit: Diff-Statistik
      (Anzahl hinzugefügter Trait-Zeilen pro Item, Gesamt-Diff),
      Testergebnis (Gesamt-Lauf + 2 Filter-Läufe mit Test-Zahlen —
      die per `grep -c` regex-basiert verifizierte Summe **16**
      explizit nennen, das **+13**-Delta explizit nennen, mit dem
      tatsächlichen Filter-Delta abgleichen), Build-Output, Self-
      Lint-Output, Commit-Hash, Subject mit exakter Längen-Angabe
      (68 Zeichen).
      `### Commit-Vorschlag`-Block am Ende der Antwort (Pflicht — siehe
      `AiNetLinterRichtlinien.mdc` §4, Commit-Vorschlag-Pflicht).
- [ ] `codemap.md` aktualisiert (Doku-Commit): die `Output/`-Zeile in
      der Sektion „Test-Verzeichnisse — geplant für EPIC-02-Folge-
      Batches" wird auf "9 Test-Klassen + 1 Helper (`TestLintConsole.cs`,
      ausgenommen — Helper-Klasse ohne `[Fact]`/`[Theory]`,
      Heuristik-Punkt 6); step-007 = erste 5 Klassen (alphabetisch
      D–O) getaggt, step-008 = restliche 4 Klassen (alphabetisch
      P–V) noch ausstehend" aktualisiert. `last_updated` der
      `codemap.md` wird ebenfalls aktualisiert. (`Output/`-Eintrag
      aktuell auf `zuletzt: step-002` — wird auf `zuletzt: step-007`/
      `step-008 (geplant)` gesetzt.)
- [ ] `status` in `step-plan.md` von `open` auf `in_progress` (durch
      Orchestrator nach Coder-Start) und nach `step-result.md`-
      Schreiben auf `done (pending audit)` (durch Coder) gesetzt.

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
  Overrides (z. B. `MaxMethodLineCount: 100`, `EnforceSealedClasses: false`
  — letzteres ist relevant, weil alle 5 step-007-Klassen `sealed` sind,
  was bei strikter `EnforceSealedClasses`-Regel ein Warn-/Fehler-
  Punkt wäre; die `*.Tests`-Override hebt das auf).

## Bekannte Ausnahmen

- **`McpLintConsoleTests` mit 3 method-level `Unit`-Traits:** die
  3 method-level Traits (Z. 19, 38, 57) sind bereits vorhanden und
  werden durch den Klassen-Trait **additiv** ergänzt. xUnit
  wertet Klassen-Oder-Methoden-Trait, also keine Doppelt-Zählung
  im Filter-Lauf. Erwarteter Filter-Delta: 0 (für diese 3 Tests
  schon vorher im Unit-Filter), +13 für die übrigen 13
  Unit-Tests in step-007 (alle 5 Klassen summiert).
- **`Output/`-Schnitt-Markierung "1/2" / "2/2" in den Commit-
  Subjects:** diese Markierung ist eine **funktionale Konvention**
  zwischen step-007 und step-008, keine Regel-Anforderung. Sie
  hilft dem nächsten Planer, den Schnitt aus dem Git-Log
  wiederzufinden, falls die `roadmap.md` zwischenzeitlich nicht
  gelesen wird.
- **Kein** "möglicherweise…¦"-Hypothese-Eintrag im aktuellen
  `codemap.md` `Output/`-Eintrag (anders als in step-006, wo die
  `ListEvalsCommandTests`-Hypothese aufzulösen war) — Heuristik-
  Punkt 5 nicht anwendbar in diesem Step. Stattdessen ist
  Heuristik-Punkt 6 (Helper-Klassen-Scope) anwendbar.

## Code-Skizze (optional)

```
// src/AiNetLinter.Tests/Output/LinterErrorFormatterTests.cs (item-03), beispielhaft
// für die XML-Doc-Variante:

/// <summary>
/// Tests fuer <see cref="LinterErrorFormatter"/> und <see cref="LinterErrorCodes"/>.
/// </summary>
[Trait("Category", "Unit")]                  // → NEU in step-007
public sealed class LinterErrorFormatterTests
{
    // ... 6 [Fact]-Methoden unverändert ...
}
```

```
// src/AiNetLinter.Tests/Output/McpLintConsoleTests.cs (item-04), beispielhaft
// für die XML-Doc + additive method-level-Traits-Variante (Heuristik-Punkt 4):

/// <summary>
/// Unit-Tests fuer <see cref="McpLintConsole"/>: ... [XML-Doc unverändert]
/// </summary>
[Trait("Category", "Unit")]                  // → NEU in step-007
public sealed class McpLintConsoleTests
{
    [Fact]
    [Trait("Category", "Unit")]              // → BESTEHEND, unverändert
    public void WriteLine_RoutesToStderr() { ... }

    [Fact]
    [Trait("Category", "Unit")]              // → BESTEHEND, unverändert
    public void WriteError_RoutesToStderr() { ... }

    [Fact]
    [Trait("Category", "Unit")]              // → BESTEHEND, unverändert
    public void Instance_ReturnsSameSingleton() { ... }
}
```

```
// src/AiNetLinter.Tests/Output/DebtReportBuilderHeaderTests.cs (item-01),
// beispielhaft für die // @covers-Block+Trait-Variante:

// @covers DebtReportBuilder                   // → BESTEHEND, bleibt direkt am Symbol
[Trait("Category", "Unit")]                   // → NEU in step-007
public sealed class DebtReportBuilderHeaderTests
{
    // ... 3 [Fact]-Methoden unverändert ...
}
```

## Notes

- **`Output/`-Schnitt-Info für den nächsten Planer-Aufruf
  (step-008):** der nächste Planer-Aufruf im Step-Modus plant
  `step-008` mit den **restlichen 4 alphabetisch letzten Klassen**
  im `Output/`-Ordner:
  - `src/AiNetLinter.Tests/Output/PathNormalizerTests.cs` —
    1 Test-Klasse, 3 `[Fact]` + 1 `[Theory]` mit 5 `[InlineData]`
    (8 Test-Cases), Standard-Variante (kein XML-Doc, kein
    `// @covers`)
  - `src/AiNetLinter.Tests/Output/RuleLegendRegistryTests.cs` —
    1 Test-Klasse, 2 `[Fact]` + 3 `[Theory]` mit 3 `[MemberData]`
    (Test-Case-Anzahl = 2 + 3—N, wobei N = Anzahl Einträge in
    `RuleMetadataRegistry.KnownRuleNames` — vom nächsten Planer
    im Schritt 2 per `grep -cE '<Regelname>'` oder
    `SourceFileCatalog`/`RuleMetadataRegistry`-Inspektion zu
    verifizieren); XML-Doc-Variante
  - `src/AiNetLinter.Tests/Output/ViolationMarkdownFormatterTests.cs`
    — 1 Test-Klasse, 30 `[Fact]`, 473 Zeilen, Standard-Variante
    (kein XML-Doc, kein `// @covers`); die größte Einzel-Datei
    im Batch
  - `src/AiNetLinter.Tests/Output/ViolationSummaryBuilderTests.cs`
    — 1 Test-Klasse, 4 `[Fact]`, Standard-Variante
  - 4 Klassen, 0/0/0/0 Subprozess-Marker pro Datei (alle Unit,
    homogen) — passt locker in den 8-Item-Deckel. EOL/BOM-
    Homogenität bestätigt (alle 4 step-008-Dateien uniform CRLF +
    Trailing-NL, kein BOM — Standard-Edit-Tool reicht, kein
    byte-genauer Helper nötig).
- **Erwarteter Filter-Delta step-008** (vom nächsten Planer im
  Schritt 2 zu verifizieren): Unit steigt um **+N**, wobei N =
  (3+5) [PathNormalizer] + (2 + 3—|KnownRuleNames|) [RuleLegend]
  + 30 [ViolationMD] + 4 [ViolationSum] = **45 + 3—|KnownRuleNames|**.
  Bei typischen ~10–15 Regelnamen →’ Unit 368 →’ ~443–458. Total
  bleibt unverändert bei 1325. Integration bleibt 113.
- **Schnitt-Markierung im step-008-Subject:** analog zu step-007
  `test: Output-Tests Kategorie-taggen 1/2 [flaky-and-test-performance]`
  (68 Zeichen) sollte step-008 den Subject
  `test: Output-Tests Kategorie-taggen 2/2 [flaky-and-test-performance]`
  (68 Zeichen) verwenden. Konvention aus step-007, im DoD des
  step-008-Plans explizit als Subject-Vorschlag zu setzen.
- **Anti-Loop-Hinweis für step-008:** die `Output/`-CodeMap-Zeile
  wird im step-007-Doku-Commit auf "9 Test-Klassen + 1 Helper,
  step-007/step-008-Schnitt" aktualisiert. Der nächste Planer
  findet dort die vollständige Schnitt-Info + Helper-Ausnahme-
  Begründung vor und muss diese nicht erneut rekonstruieren.
- **Bezug zu TD-002 (Subject-Disziplin):** der konkrete
  Subject-Vorschlag (68 Zeichen) im DoD ist direkter Ausfluss
  der TD-002-Empfehlung Variante (a) "Planer-Disziplin + Skill-
  Präzisierung". Der Coder akzeptiert den Vorschlag unverändert
  (Pattern aus step-002/003/004/005/006).
- **Heuristik-Fortschreibung für Folge-Batches:** Punkt 6
  ("Helper-Klassen ohne Testmethoden sind keine Testklassen")
  ist eine **dauerhafte** Regel, die ab sofort auf alle
  EPIC-02-Folge-Batches angewandt wird. Konkret: bei jedem
  zukünftigen Planer-Schritt-2 wird die Klassen-Zahl eines
  Batches per `grep -cE '\[(Fact|Theory)\]'` pro Zieldatei
  verifiziert; Dateien mit 0 Treffern werden aus dem Tagging-
  Scope ausgenommen und in der Bestandsaufnahme explizit als
  "Helper (ausgenommen)" aufgeführt.
