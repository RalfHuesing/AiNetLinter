---
status: open
type: step-plan
task: flaky-and-test-performance
step: 004
corrects: null
title: "Category-Traits für alle Tests in src/AiNetLinter.Tests/Web/ nachziehen (Batch 3 von N)"
epic: EPIC-02
estimated_risk: low
step_type: batch
items:
  - id: item-01
    title: "CssAnalyzerTests → Unit (reine in-process CSS-Analyse)"
    source: "konzept.md §Wie Schritt 2, §Muss-Haven Traits-Punkt"
  - id: item-02
    title: "JsAnalyzerTests → Unit"
    source: "konzept.md §Wie Schritt 2"
  - id: item-03
    title: "RazorAnalyzerTests → Unit"
    source: "konzept.md §Wie Schritt 2"
  - id: item-04
    title: "RazorAnalyzerExtendedTests → Unit (Zusatzklasse in RazorAnalyzerTests.Extended.cs)"
    source: "konzept.md §Wie Schritt 2"
  - id: item-05
    title: "WebSuppressionDetectorTests → Unit"
    source: "konzept.md §Wie Schritt 2"
created_by: planer
created_by_model: MiniMax-M3
created_by_model_knowledge_cutoff: 2026-01
created_at: 2026-08-07T11:30:00+02:00
related_to: []
---

# Step 004: Category-Traits für `src/AiNetLinter.Tests/Web/` (Batch 3)

## Bezug

- **Task:** `flaky-and-test-performance`
- **Epic:** `EPIC-02` aus `roadmap.md` — Category-Traits flächendeckend nachziehen.
  Dritter von N Batches (deckt 5 weitere ungetaggte Testklassen ab; nach
  step-002 (Suppression, 8) und step-003 (Metrics, 7) sind 15 Klassen +
  43 Testmethoden aus dem ~168-Klassen-/~1040-Methoden-Bestand getaggt).
- **Konzept-Referenz:** `konzept.md` §"Wie" Schritt 2 ("Category-Traits nachziehen —
  alle ~1000 ungetraggten Tests einordnen"), §"Muss-Haven" Traits-Punkt
  ("konsequente Category-Traits ... auf **allen** Tests — aktuell nur 86 von
  ~1087"), §"Definition of Done" Punkt "Alle Tests tragen einen Category-Trait".
- **Vorgänger-Steps:** `step-002` (approved) und `step-003` (approved) — beide
  lieferten die Klassifikations-Heuristik, die Trait-Syntax-Konvention und die
  DoD-Struktur. Dieser Step wendet das identische Vorgehen auf den nächsten
  homogenen Unit-Ordner an (`Web/`, 5 Klassen, alle Unit, alle mit XML-Doc).

## Aktueller Projektzustand (JIT-Kontext)

Beim Lesen des Projekts vorgefunden (relevant für step-004):

- **Test-Inventar im Ziel-Ordner:** 5 `*.cs`-Dateien unter
  `src/AiNetLinter.Tests/Web/`:
  1. `CssAnalyzerTests.cs`
  2. `JsAnalyzerTests.cs`
  3. `RazorAnalyzerTests.cs`
  4. `RazorAnalyzerTests.Extended.cs`
  5. `WebSuppressionDetectorTests.cs`
- **Konzept-/Code-Map-Schätzung vs. Realität:** "5 Klassen" aus
  `step-002/step-plan.md` Notes und `codemap.md` exakt bestätigt (5 Dateien,
  5 Testklassen — 1:1). **CodeMap-Korrektur in diesem Schritt:** die in
  step-002 angelegte `codemap.md`-Aufzählung der `Web/`-Klassen hatte
  `RazorAnalyzerExtendedTests` (in `RazorAnalyzerTests.Extended.cs`) vergessen
  — die Karten-Zeile listete nur 4 Klassen. Im Zuge dieses Plans in
  `codemap.md` korrigiert (5. Klasse nachgetragen, Schritt-Referenz von
  "zuletzt: step-002" auf "zuletzt: step-002; Korrektur in step-004"
  aktualisiert), damit die Karte vollständig ist und der nächste Planer-
  Aufruf für `Architecture/`/`Diagnostics/`/`FalsePositives/` nicht die
  gleiche Lücke wieder einführt.
- **Bestehende Trait-Verteilung im Ordner:** **0 Klassen mit Trait** (verifiziert
  per `Select-String -Pattern 'Trait\('` über `src/AiNetLinter.Tests/Web/` —
  keine Treffer).
- **Subprozess-Marker im Ordner:** **0 Treffer** für `McpTestClient`,
  `CliProcessRunner`, `Program.Main`, `IClassFixture<McpLiveRepositoryFixture>`
  (verifiziert per `Select-String` über alle 5 Dateien, 0/0/0/0 Treffer). Damit
  ist der gesamte Ordner homogen **Unit** — keine Integration-Klasse.
- **Testmethoden-Inventar** (regex-basiert per `Select-String -Pattern '\[Fact\]'` /
  `\[Theory\]'` — gemäß step-003-Review NITPICK "regex statt manuell zählen"):
  - `CssAnalyzerTests.cs`: **15 `[Fact]`** (0 `[Theory]`, 0 `[InlineData]`)
  - `JsAnalyzerTests.cs`: **20 `[Fact]`** (0 `[Theory]`, 0 `[InlineData]`)
  - `RazorAnalyzerTests.cs`: **15 `[Fact]`** (0 `[Theory]`, 0 `[InlineData]`)
  - `RazorAnalyzerTests.Extended.cs`: **18 `[Fact]`** (0 `[Theory]`, 0
    `[InlineData]`)
  - `WebSuppressionDetectorTests.cs`: **6 `[Fact]`** (0 `[Theory]`, 0
    `[InlineData]`)
  - **Summe: 15+20+15+18+6 = 74 Testmethoden** (alle `[Fact]`, keine `[Theory]`
    mit `[InlineData]`-Reihen). Alle 74 werden über die 5 Klassen-Traits
    abgedeckt — keine Methoden-Ebenen-Traits nötig, da alle Klassen homogen
    Unit sind.
- **Klassen-Deklarationen mit XML-Doc:** **Alle 5 Klassen** haben eine
  XML-Doc-Sektion (`/// <summary> ... </summary>`) direkt über der
  `public sealed class ...`-Zeile. **Der Trait gehört zwischen `</summary>`
  und Klassendeklaration** (analog zu step-003's Variante für
  `CognitiveComplexityGuidanceTests`/`FileLimitGuidanceTests`/
  `PostAnalysisChecksPathOverrideTests`).
- **Klassen-Deklarationen ohne XML-Doc:** **0 von 5** — im Gegensatz zu
  step-002 (1 von 8 ohne) und step-003 (4 von 7 ohne) ist `Web/` der erste
  vollständig-XML-Doc-Batch. Das vereinfacht die Trait-Platzierung auf eine
  einzige lokale Variante.
- **Klassen-Deklarations-Zeilen + `</summary>`-Zeilen** (verifiziert per
  `Select-String -Pattern '</summary>'` und `'public sealed class'`):
  - `CssAnalyzerTests.cs`: `</summary>` Z. 14, Klasse Z. 15
  - `JsAnalyzerTests.cs`: `</summary>` Z. 14, Klasse Z. 15
  - `RazorAnalyzerTests.cs`: `</summary>` Z. 16, Klasse Z. 17
  - `RazorAnalyzerTests.Extended.cs`: `</summary>` Z. 13, Klasse Z. 14
  - `WebSuppressionDetectorTests.cs`: `</summary>` Z. 11, Klasse Z. 12
  In allen 5 Fällen liegt `</summary>` exakt eine Zeile über der
  Klassendeklaration → Trait-Zeile wird an Z. (`Klasse - 1`) eingefügt
  (zwischen den beiden bestehenden Zeilen).
- **`// @covers`-Marker:** 3 von 5 Dateien haben einen `// @covers`-Kommentar
  (`CssAnalyzerTests.cs:6`, `JsAnalyzerTests.cs:6`, `RazorAnalyzerTests.cs:7`)
  — **aber** zwischen `using` und `namespace`, also **oberhalb** der
  XML-Doc-Section und **oberhalb** der Trait-Einfügestelle. Sie bleiben
  unverändert und interferieren nicht mit der Trait-Platzierung. Verifiziert
  per `Select-String -Pattern '// @covers'` über alle 5 Dateien.
- **Spezialfall `RazorAnalyzerTests.Extended.cs`:** Die Klasse heißt
  `RazorAnalyzerExtendedTests` (nicht `RazorAnalyzerTestsExtendedTests` und auch
  nicht `RazorAnalyzerExtendedTestsTests` o. ä.) — der `Tests`-Suffix ist im
  Konventionssinn vorhanden. Die Datei `*.Extended.cs`-Namens-Konvention ist
  projekt-intern (siehe `RazorAnalyzerTests.cs:13-15` XML-Doc: "weitere Tests
  ... sind in `RazorAnalyzerExtendedTests` ausgelagert, um die Datei unter
  MaxLineCount (500) zu halten"). Sie ist eindeutig eine Testklasse
  (18 `[Fact]`-Methoden, `using Xunit;`) — keine Sonderbehandlung nötig.
- **Spezialfall `RazorAnalyzerExtendedTests`:** Verwendet in Z. 30 den
  `null!`-Operator (`RazorAnalyzer.Analyze(null!, ...)`) — bewusster
  Test-Input, kein Subprozess-Hinweis, ändert nichts an der Unit-Klassifikation.
  Passt zur Negativ-Abgrenzung "rein in-process API-Tests mit Edge-Inputs"
  (analog zu step-002's `DisableAllCliTests`-Pattern, dort aber Subprozess).
- **Gewählter Batch-Begründung:** `Web/` ist der **drittkleinste** Ordner
  unter den EPIC-02-Folge-Batches (5 Klassen, alle Unit, alle XML-Doc,
  homogen — kein einziger Subprozess-Marker) und passt damit unter den
  `max_batch_items: 8`-Deckel von `spec.md` §10.6. Die Empfehlung aus
  step-002's Notes (Reihenfolge-Vorschlag des Planer-Teams) listet `Web/` an
  Position 1 der "Reine-Unit-Ordner, klein"-Serie; ich folge dieser Reihenfolge,
  weil sie in sich konsistent ist (kleinster homogener Unit-Ordner zuerst,
  dann die nächst-kleinen). Die Alternative, mehrere kleine Ordner
  (`Web/`+`Architecture/`+`Diagnostics/` = 7 Klassen) in einen Batch zu
  bündeln, wurde erwogen, aber verworfen, weil:
  1. Jeder Misch-Ordner-Batch würde die step-002/003-Konvention
     "ein Ordner pro Step" brechen und die Schritt-Geschichte unsauber
     machen.
  2. Bei 5 Klassen ist noch "Luft" im 8-Item-Deckel — ein Slot für
     eventuelle Problemerkennung bleibt frei.
  3. Die Klassifikations-Heuristik (Negativ-Abgrenzung "in-process Analyzer-
     Aufrufe") ist im `Web/`-Batch isoliert von anderen Heuristik-Varianten
     (z. B. `Configuration/` mit Config-Loading, `Mcp/` mit
     `McpLiveRepositoryFixture`) — leichtere Begründung pro Item.

## Intention

Alle 5 Testklassen unter `src/AiNetLinter.Tests/Web/` mit
`[Trait("Category", "Unit")]` auf Klassen-Ebene versehen, zwischen
`</summary>` und `public sealed class` platziert (alle 5 haben XML-Doc). Dieser
Step ist der dritte von N Batches, die zusammen die EPIC-02-DoD erreichen
("alle ~1000 Tests getraggt"). Er bestätigt die in step-002/003 bewährte
Vorgehensweise an einem **rein Unit-dominierten Ordner mit vollständiger
XML-Doc-Abdeckung** (5/5 Klassen mit XML-Doc — im Gegensatz zu step-002
mit 7/8 und step-003 mit 3/7) und liefert damit eine dritte Template-Validierung
für die Folge-Batches. Insbesondere verifiziert er die Heuristik, dass auch
`Analyzer.Analyze(null!, ...)`-Aufrufe in-process und damit Unit bleiben (im
Gegensatz zu `CliProcessRunner.RunLinterAsync`/`Program.Main`-Aufrufen, die
Integration wären).

## Klassifikations-Heuristik für diesen Batch

Die in step-002 dokumentierte Heuristik wird unverändert übernommen und an
diesem Batch bestätigt:

1. **Bestehende Traits prüfen.** Im `Web/`-Ordner keine bestehenden Traits
   (verifiziert per `Select-String -Pattern 'Trait\('`).
2. **Subprozess-Marker prüfen.** Im `Web/`-Ordner keine Subprozess-Marker
   (verifiziert per `Select-String` über `McpTestClient`, `CliProcessRunner`,
   `Program.Main`, `IClassFixture<McpLiveRepositoryFixture>`). Damit ist
   **keine** Klasse in diesem Batch `Integration`.
3. **Sonst: Unit.** Trifft auf alle 5 Klassen in diesem Batch zu.

**Wichtige Negativ-Abgrenzung** (aus step-002, weiterhin gültig, an `Web/`-
spezifischen Gegebenheiten verifiziert): Die folgenden Muster sind **KEIN**
Subprozess und führen nicht zu `Integration`:

- `Analyzer.Analyze(...)`-Aufrufe (egal ob mit `null!`, leerem String oder
  komplexem Razor/CSS/JS-Input) — in-process API-Tests, kein Subprozess
- `WebSuppressionDetector.IsSuppressed(...)` — in-process String-Parsing
- `new CssConfig()` / `new JsConfig()` / `new RazorConfig()` / ähnliche
  Config-Konstruktor-Aufrufe — in-process
- `// @covers`-Marker-Kommentare — reine Linter-Metadaten, irrelevant für
  Trait-Klassifikation
- Direkt-Aufrufe wie `Path.GetTempPath()` / `Path.GetTempFileName()` — in-process
- Keine Datei in `Web/` nutzt eine `IClassFixture<...>` (verifiziert per
  `Select-String -Pattern 'IClassFixture'` — 0 Treffer in `Web/`)

**Neu in diesem Batch bestätigt** (Heuristik-Fortschreibung Punkt 3 aus
`SKILL.md` Step-Modus Schritt 1a): `null!`-Übergabe an Analyzer-Methoden
(`RazorAnalyzerExtendedTests.cs:30`: `RazorAnalyzer.Analyze(null!, ...)`) ist
**kein** Subprozess-Hinweis — es ist ein Edge-Input-Test in-process. Die
Heuristik-Regel "Subprozess-Marker = Integration" bleibt unverändert; dieser
Fall fällt sauber unter "in-process API-Edge-Test".

## Konkrete Änderungen

**Bei `step_type: batch`** (siehe `../spec.md` §10.6): pro Item aus der
`items`-Liste im Frontmatter eine Unterüberschrift.

### item-01: `CssAnalyzerTests` → Unit — `src/AiNetLinter.Tests/Web/CssAnalyzerTests.cs` (Z. 14-15, zwischen XML-Doc und Klasse)

- **Was:** Zwischen `</summary>` (Z. 14) und `public sealed class
  CssAnalyzerTests` (Z. 15) eine Zeile `[Trait("Category", "Unit")]` einfügen.
  Genau **eine** Zeile, kein zusätzlicher Leerraum.
- **Warum:** Klasse testet `CssAnalyzer.Analyze(...)` rein in-process auf
  String-Input (`const string css = """...""";` als Test-Input) und
  `NewCssConfig()` (lokaler Helper, baut eine `CssConfig` in-process). Keine
  Subprozess-Marker im File-Grep (0 Treffer für `McpTestClient`/
  `CliProcessRunner`/`Program.Main`/`IClassFixture<McpLiveRepositoryFixture>`).
  Trait-Wert folgt exakt der bestehenden Konvention
  (`[Trait("Category", "Unit")]`, CamelCase-Großbuchstabe). Der `// @covers
  CssConfig`-Marker auf Z. 6 (zwischen `using` und `namespace`) bleibt
  unverändert — er liegt **oberhalb** der Trait-Einfügestelle und
  interferiert nicht.

### item-02: `JsAnalyzerTests` → Unit — `src/AiNetLinter.Tests/Web/JsAnalyzerTests.cs` (Z. 14-15, zwischen XML-Doc und Klasse)

- **Was:** Zwischen `</summary>` (Z. 14) und `public sealed class
  JsAnalyzerTests` (Z. 15) eine Zeile `[Trait("Category", "Unit")]` einfügen.
- **Warum:** Klasse testet `JsAnalyzer.Analyze(...)` rein in-process auf
  String-Input (ES6-Modul-Code als `const string js = """...""";`). Analog
  zu item-01: in-process, keine Subprozess-Marker, gleiche Trait-Platzierung.
  Der `// @covers JsConfig`-Marker auf Z. 6 bleibt unverändert.

### item-03: `RazorAnalyzerTests` → Unit — `src/AiNetLinter.Tests/Web/RazorAnalyzerTests.cs` (Z. 16-17, zwischen XML-Doc und Klasse)

- **Was:** Zwischen `</summary>` (Z. 16) und `public sealed class
  RazorAnalyzerTests` (Z. 17) eine Zeile `[Trait("Category", "Unit")]`
  einfügen.
- **Warum:** Klasse testet `RazorAnalyzer.Analyze(...)` rein in-process auf
  Razor-Markup-String-Input (`const string razor = """...""";`). Die
  XML-Doc-Section (Z. 9-16) verweist in Z. 13 explizit auf die
  `RazorAnalyzerExtendedTests`-Klasse in `RazorAnalyzerTests.Extended.cs`
  (item-04) — die Datei-Aufteilung dient der `MaxLineCount (500)`-Konformität
  der Hauptdatei (siehe Z. 14-16: "weitere Tests ... sind in
  `<see cref="RazorAnalyzerExtendedTests"/>` ausgelagert"). Beide Klassen
  sind unabhängige Test-Klassen mit identischer Klassifikation (`Unit`) —
  in der EPIC-02-Batch-Serie werden sie als **zwei getrennte Items**
  behandelt, weil sie in **zwei verschiedenen Dateien** liegen. Der
  `// @covers RazorConfig`-Marker auf Z. 7 bleibt unverändert.

### item-04: `RazorAnalyzerExtendedTests` → Unit — `src/AiNetLinter.Tests/Web/RazorAnalyzerTests.Extended.cs` (Z. 13-14, zwischen XML-Doc und Klasse)

- **Was:** Zwischen `</summary>` (Z. 13) und `public sealed class
  RazorAnalyzerExtendedTests` (Z. 14) eine Zeile
  `[Trait("Category", "Unit")]` einfügen.
- **Warum:** Zusatz-Testklasse (Edge-Cases, Helper-Methoden-Tests,
  Szenarien-Kombinationen — siehe XML-Doc Z. 9-12) — rein in-process.
  Spezialfall-Notiz: Verwendet in Z. 30 `RazorAnalyzer.Analyze(null!, ...)`
  (bewusster Edge-Input-Test, kein Subprozess-Hinweis — passt zur
  Negativ-Abgrenzung "in-process API-Edge-Test" in der Heuristik-Fortschreibung
  oben). Klassenname folgt der Konvention (Suffix `Tests`), die
  `*.Extended.cs`-Dateinamen-Konvention ist projekt-intern und ändert nichts
  an der Klassifikation.

### item-05: `WebSuppressionDetectorTests` → Unit — `src/AiNetLinter.Tests/Web/WebSuppressionDetectorTests.cs` (Z. 11-12, zwischen XML-Doc und Klasse)

- **Was:** Zwischen `</summary>` (Z. 11) und `public sealed class
  WebSuppressionDetectorTests` (Z. 12) eine Zeile `[Trait("Category", "Unit")]`
  einfügen.
- **Warum:** Klasse testet `WebSuppressionDetector.IsSuppressed(content,
  ruleName)` rein in-process auf String-Input (CSS-/JS-/Razor-Content mit
  `/* ainetlinter-disable ... */`-Kommentaren). Kleinste Klasse im Batch
  (nur 6 `[Fact]`-Methoden), aber morphologisch identisch zu den vier
  Analyzer-Klassen. Kein `// @covers`-Marker (im Gegensatz zu den
  drei Analyzer-Klassen mit `// @covers XxxConfig`-Markern auf
  `CssConfig`/`JsConfig`/`RazorConfig`) — irrelevant für Klassifikation,
  da der `// @covers`-Marker Bestand ist und der Trait-Pfad davon unabhängig.

## Tests

Keine — die Klassifikation ist rein additiv (Attribut setzen). Existierende Tests
müssen **unverändert** grün bleiben. Validierung erfolgt über den vollen
`dotnet test`-Lauf in der Definition of Done (kein neuer Test, kein geänderter
Test).

## Definition of Done

- [ ] Alle 5 Items umgesetzt (je eine `[Trait("Category", "Unit")]`-Zeile zwischen
  `</summary>` und `public sealed class` in den 5 `Web/`-Dateien)
- [ ] **Bestehende Traits respektiert:** keine vorhandenen Trait-Attribute
  überschrieben oder entfernt (Trifft im Batch nicht zu, aber als
  Plausibilitäts-Check zu verifizieren: nach dem Diff sollten in `Web/`
  5 Klassen mit Trait-Attribut existieren, 0 ohne.)
- [ ] **Build-Command** aus Tech-Stack-Notiz (`roadmap.md`) grün:
  `dotnet build` (Zero-Warning-Direktive, `TreatWarningsAsErrors=true` in
  beiden Projekten)
- [ ] **Test-Command** aus Tech-Stack-Notiz grün: `dotnet test` (voller Lauf,
  alle Tests müssen weiterhin grün sein — keine Test-Logik wurde geändert)
- [ ] **Trait-Filter-Smoke-Test** (vom Coder zusätzlich durchführen, um die
  Klassifikation zu verifizieren):
  - `dotnet test --no-build --filter "Category=Unit"` → muss grün sein
  - `dotnet test --no-build --filter "Category=Integration"` →
    **best-effort, ein Lauf grün** (gemäß step-002/step-003 NITPICK-Linie:
    pre-existing Flaky-Test
    `McpServerCommandLoadingStateTests.LoadState_LoadFuncCompletesSynchronouslyWithCatalog_ReportsLoadedImmediately`
    flake-t gelegentlich unter Last des Integration-Filters; nicht
    step-004-verursacht, Fix in EPIC-06). Der Coder dokumentiert im
    `step-result.md`, wenn der Lauf flaky ist, und startet ihn ggf. einmal
    neu.
  - **Numerische Plausibilitätsprüfung** (gemäß step-003-Review NITPICK
    "regex statt manuell zählen"): der Coder zählt die
    `[Fact]`/`[Theory]`-Methoden in den 5 `Web/`-Klassen **regex-basiert**
    per `Select-String -Pattern '\[Fact\]'` / `'\[Theory\]'` (NICHT manuell
    durchgehen), dokumentiert die Summe im `step-result.md` und vergleicht
    sie mit dem erwarteten Unit-Filter-Delta. Erwartetes Delta: Unit-Zahl
    steigt um **74** (15+20+15+18+6, verifiziert per `Select-String` durch
    den Planer; siehe "Aktueller Projektzustand" oben). Integration-Zahl
    bleibt unverändert bei 113. Total bleibt unverändert bei 1325.
- [ ] **Self-Lint** (TD-001-konform, semantisch identisch zu `--self-lint`):
  `dotnet run --project src/AiNetLinter -- --config rules.json --path .` →
  muss `OK` ausgeben
- [ ] **Commit auf aktuellem Branch** (Conventional Commit auf Deutsch,
  imperativ, mit Task-Suffix `[flaky-and-test-performance]`):
  **konkreter Subject-Vorschlag** (gemäß TD-002, "kürzere Subject-Bodies
  vorgeben"):
  `test: Web-Tests Kategorie-taggen [flaky-and-test-performance]`
  → **61 Zeichen** inkl. Suffix (exakt verifiziert per
  `('test: Web-Tests Kategorie-taggen [flaky-and-test-performance]').Length`
  in PowerShell; deckt 11 Zeichen Sicherheitsabstand zur 72-Zeichen-Grenze).
  Pattern spiegelt step-002's `test: Suppression-Tests Kategorie-taggen
  [flaky-and-test-performance]` (69 Zeichen) — gleicher Aufbau,
  konsistent zur EPIC-02-Batch-Serie. **Falls** der Coder den Subject
  abwandeln will (z. B. weil ihm der Verb "kategorie-taggen" nicht passt),
  **muss** er 72 Zeichen einhalten und die neue exakte Länge im
  `step-result.md` dokumentieren — bei Überschreitung TD-002-Eintrag
  aktualisieren.
- [ ] `step-004/step-result.md` geschrieben mit: Diff-Statistik (Anzahl
  hinzugefügter Trait-Zeilen pro Item, Gesamt-Diff), Testergebnis
  (Gesamt-Lauf + 2 Filter-Läufe mit Test-Zahlen — die per `Select-String`
  regex-basiert verifizierte Summe **74** explizit nennen und mit dem
  tatsächlichen Filter-Delta abgleichen), Build-Output, Self-Lint-Output,
  Commit-Hash, Subject mit exakter Längen-Angabe.
  `### Commit-Vorschlag`-Block am Ende der Antwort (Pflicht — siehe
  `AiNetLinterRichtlinien.mdc` §4, Commit-Vorschlag-Pflicht).
- [ ] `status` in `step-plan.md` von `open` auf `in_progress` (durch
  Orchestrator nach Coder-Start) und nach `step-result.md`-Schreiben auf
  `done (pending audit)` (durch Coder) gesetzt.

## Rules-Refs

- `.agents/rules/AiNetLinterRichtlinien.mdc` §4 "Testsuite-Parallelität
  bewahren" — relevant nur als Ausschluss: Trait-Attribute haben **keinen**
  Einfluss auf Parallelismus, nur `[Collection(...)]` / `DisableParallelization`.
  Dieser Step berührt die Parallelität nicht, ist also nicht regel-restriktiv
  hier.
- `.agents/rules/AiNetLinterRichtlinien.mdc` §5 "Sparsame Kommentare" — die
  hinzugefügten Trait-Zeilen sind XML-Attribute, keine Kommentare. Kein
  Bezug.
- `.agents/rules/AiNetLinterRichtlinien.mdc` §5 "Zero-Warning-Direktive" — die
  Trait-Attribute sind `[Trait("Category", "Unit")]`, exakt die im Projekt
  etablierte Schreibweise (Großbuchstabe am Wortanfang). Keine Warnung
  erwartet, da das exakt der bestehenden Konvention folgt (verifiziert per
  Grep über die 100+ bestehenden Trait-Vorkommen im Projekt).
- `.agents/rules/AiNetLinterRichtlinien.mdc` §4 "Commit-Vorschlag Pflicht" —
  betrifft die Coder-Antwort, ist im DoD-Punkt oben explizit referenziert.
- `.agents/rules/AiNetLinterRichtlinien.mdc` §5 "Symptom-Fixing verboten" —
  betrifft diesen Step nicht direkt, aber als Plausibilitäts-Check: wenn ein
  Test rot wird, ist die Ursache zu suchen, nicht der Test abzuschwächen.
- `.agents/rules/AiNetLinterRichtlinien.mdc` §3 "Windows/PowerShell" — relevant
  für `Select-String` als Zähl-Werkzeug (statt `grep`/`rg`-Befehle im
  Bash-Stil) — im DoD-Punkt "Numerische Plausibilitätsprüfung" entsprechend
  formuliert.

## Bekannte Ausnahmen

- **Pre-Existing-Flaky-Test im Integration-Filter** (aus step-002/step-003-
  Reviews übernommen): `McpServerCommandLoadingStateTests.
  LoadState_LoadFuncCompletesSynchronouslyWithCatalog_ReportsLoadedImmediately`
  flake-t gelegentlich unter Last des `Category=Integration`-Filters. Nicht
  step-004-verursacht (rein additives Attribut, keine Logik-/Parallelitäts-
  Änderung), Fix in EPIC-06. Der Coder behandelt den Integration-Filter-
  Lauf als "best-effort, ein Lauf grün" (siehe DoD).

## Code-Skizze (optional)

Vorher (Beispiel: `WebSuppressionDetectorTests.cs`, Z. 7-13):

```csharp
namespace AiNetLinter.Tests.Web;

/// <summary>
/// Unit-Tests fuer WebSuppressionDetector. Verifiziert dateiweite und regel-spezifische
/// Suppression-Kommentare in Web-Dateien.
/// </summary>
public sealed class WebSuppressionDetectorTests
{
    [Fact]
    public void IsSuppressed_ReturnsTrue_WhenDisableAllPresent()
```

Nachher:

```csharp
namespace AiNetLinter.Tests.Web;

/// <summary>
/// Unit-Tests fuer WebSuppressionDetector. Verifiziert dateiweite und regel-spezifische
/// Suppression-Kommentare in Web-Dateien.
/// </summary>
[Trait("Category", "Unit")]
public sealed class WebSuppressionDetectorTests
{
    [Fact]
    public void IsSuppressed_ReturnsTrue_WhenDisableAllPresent()
```

Für `CssAnalyzerTests.cs` (Beispiel mit zusätzlichem `// @covers`-Marker
oberhalb des `namespace`, Z. 5-17 — Trait-Platzierung **unterhalb** des
`@covers`-Markers und zwischen `</summary>` und Klasse, keine Interferenz):

```csharp
// @covers CssConfig (StaticTestSentinel: Kognitive Komplexitaet 6 > Schwellwert 5; Konfiguration ist ueber diese Tests abgedeckt.)
namespace AiNetLinter.Tests.Web;

/// <summary>
/// Unit-Tests fuer CssAnalyzer. Implementiert die Test-Szenarien A-H aus
/// Research/Extend-Web-Features/01_CSS_Linting.md Abschnitt 5.
/// </summary>
public sealed class CssAnalyzerTests
```

wird zu:

```csharp
// @covers CssConfig (StaticTestSentinel: Kognitive Komplexitaet 6 > Schwellwert 5; Konfiguration ist ueber diese Tests abgedeckt.)
namespace AiNetLinter.Tests.Web;

/// <summary>
/// Unit-Tests fuer CssAnalyzer. Implementiert die Test-Szenarien A-H aus
/// Research/Extend-Web-Features/01_CSS_Linting.md Abschnitt 5.
/// </summary>
[Trait("Category", "Unit")]
public sealed class CssAnalyzerTests
```

Der `// @covers CssConfig`-Marker auf Z. 6 bleibt **unverändert** (zwischen
`using` und `namespace`); er ist Bestand und wird vom Step nicht berührt.

## Notes

- **Batch-Umfang:** 5 Klassen × je 1 Trait-Zeile ≈ 5 Diff-Zeilen. Deutlich
  unter dem `max_batch_diff_lines: 40`-Deckel und unter dem
  `max_batch_items: 8`-Deckel.
- **Schritt-Typ `low`-Risk-Begründung:** rein additives Attribut auf Klassen,
  das weder Build-Verhalten noch Test-Verhalten noch Parallelität ändert.
  Trait-Wert folgt exakt der bestehenden Konvention (`Unit`,
  CamelCase-Großbuchstabe). Kein Eingriff in Produktionscode, keine
  Fixture-Änderung, keine Test-Logik-Änderung.
- **Subject-Länge 61 Zeichen** (im DoD vorgegeben): 11 Zeichen Reserve zur
  72-Zeichen-Grenze. Falls der Coder den Verb "kategorie-taggen" nicht mag
  (z. B. weil er Anglizismen-scheu ist), sind folgende Alternativen
  ebenfalls unter 72 Zeichen:
  - `test: Web-Tests mit Unit-Trait versehen [flaky-and-test-performance]`
    = 64 Zeichen
  - `chore(tests): Web-Tests mit Traits [flaky-and-test-performance]`
    = 58 Zeichen
  - `chore(tests): Web-Tests mit Unit-Trait versehen
    [flaky-and-test-performance]` = 70 Zeichen (knapp)
  Der Coder kann unter diesen Varianten wählen, **muss** aber die exakte
  Länge per `('Subject').Length` in PowerShell verifizieren und im
  `step-result.md` dokumentieren.
- **Heuristik-Fortschreibung Punkt 3** (gemäß `SKILL.md` Step-Modus Schritt
  1a, "Wenn du beim Inspizieren einer Klasse gegen die Heuristik stolperst,
  dokumentiere das"): in diesem Batch ist die `RazorAnalyzerExtendedTests`
  mit `RazorAnalyzer.Analyze(null!, ...)` (Z. 30) ein
  `null!`-Edge-Input-Test — bewusst in-process, kein Subprozess-Hinweis.
  Die in step-002 etablierte Heuristik "Subprozess-Marker = Integration"
  trifft sauber zu, weil `null!` kein Subprozess-Marker ist (kein
  `McpTestClient`/`CliProcessRunner`/`Program.Main`/
  `IClassFixture<McpLiveRepositoryFixture>`). Diese Beobachtung wird hier
  nur als Bestätigung der Heuristik dokumentiert, **nicht** als
  Heuristik-Erweiterung — die Regel bleibt unverändert.
- **Folge-Batches (NICHT in diesem Step geplant):** Die EPIC-02-Arbeit
  umfasst weiterhin ca. 145 verbleibende ungetaggte Testklassen (168 −
  15 (step-002+003) − 5 (step-004) − 3 bereits-vor-Step-001-getaggte
  Fixture-Tests = ~145; grobe Schätzung). Vorschlag für die Reihenfolge
  der nächsten Step-Modus-Aufrufe (rein informativ — Planung der einzelnen
  Folge-Steps ist Sache der jeweiligen Planer-Aufrufe, nicht dieses
  Plans):
  1. **Reine-Unit-Ordner, klein** (einfachster Fall, Klassen-Trait
     durchgängig):
     - `Architecture/` (1 Klasse, Unit)
     - `Diagnostics/` (1 Klasse, Unit)
     - `FalsePositives/` (2 Klassen, Unit)
     - `Cache/` (3 Klassen, Unit)
     - `Evals/` (3 Klassen, Unit — `ListEvalsCommandTests` möglicherweise
       Integration via Subprozess, JIT zu prüfen)
     - `Output/` (10 Klassen, alle Unit)
  2. **Reine-Unit-Ordner, groß** (gleiche Heuristik, aber mehr Items pro
     Batch aufteilen):
     - `Configuration/` (8 Klassen, alle Unit)
     - `Core/Checkers/` (27 Klassen, alle Unit) — mehrere Batches
     - `Core/` (19 Klassen, alle Unit) — mehrere Batches
     - `Maps/` + `Maps/Skeleton/` (6 Klassen, alle Unit)
  3. `Mcp/Tools/` (17 Klassen, fast alle Unit, Mini-Fixture-Workspace →
     Unit) — 2–3 Batches
  4. **Verzeichnisse mit echtem Subprozess-Anteil** (Heuristik-Ausnahmen,
     erfordern mehr Sorgfalt):
     - `Mcp/` (19 Klassen, gemischt; `McpCodeGraphServer*Tests` Unit,
       `McpLiveRepositoryTests`/`McpDocumentationSmokeTests` Integration)
     - `Baseline/` (10 Klassen, gemischt; `BaselineCliTests`/
       `WebBaselineTests` Integration, `SourceFileCatalog*Tests` Unit)
  5. **`Commands/`** (17 Klassen, stark gemischt; `McpServerCommandTests`
     ist die prominenteste gemischte Klasse — 5 Unit + 18 Integration in
     einer Klasse, erfordert pro-Methode-Tagging) — mehrere Batches,
     höchste Komplexität. **Empfehlung:** die gemischte
     `McpServerCommandTests.cs` als eigenen Step planen (voraussichtlich
     `step-XXX` mit `step_type: single`), um die Methoden-Ebene-Heuristik
     sauber zu dokumentieren.
  6. **`Fixtures/`-eigene Tests** (`LoadFixtureBuilderTests`,
     `LoadFixtureMeasurementsTests`, `TD016aRefactorTests` — bereits
     getraggt) und die `Cli/`-Klasse `CliCommandBuilderMcpLogTests`
     (Unit) — am Ende als Aufräum-Batch, falls noch nicht in vorherigen
     Batches erledigt.
- **Gesamt-Fortschritt nach step-004:** 86 (pre-Step-001) + 8 (step-002) +
  7 (step-003) + 5 (step-004) = **106 von 168 Testklassen** getaggt
  (entspricht ca. 63 % der Klassen; bei den 1325 Testmethoden über die
  Filter-Läufe rekonstruierbar — Schätzung: ~1025 ungetragte → 300
  getaggte, da die Klassen-Traits mehrere Methoden abdecken). Rest-Bestand
  nach step-004: ca. 62 ungetaggte Klassen (168 − 106) — EPIC-02 nähert
  sich dem Ende, ist aber **noch nicht** abgeschlossen (insbesondere
  `Commands/`, `Baseline/`, `Mcp/`, `Cli/` fehlen noch vollständig).
  **Erwartet** und kein Planungsfehler — die DoD wird über mehrere
  Folge-Steps (vermutlich 8-12 weitere Batches) erreicht. Doku-Pflicht
  (roadmap.md EPIC-02-Abhaken) gehört in den **letzten** EPIC-02-Batch,
  nicht in step-004.
- **Kein `// @covers`-Marker-Konflikt:** 3 von 5 Dateien haben einen
  `// @covers XxxConfig`-Marker auf Z. 6-7 (zwischen `using` und
  `namespace`). Diese Marker bleiben **vollständig unverändert** — der
  Trait wird zwischen `</summary>` (Z. 11-16 je nach Datei) und
  Klassendeklaration platziert, also räumlich **getrennt** vom
  `@covers`-Marker. Coder prüft visuell, dass beide Strukturen
  (Marker + Trait) sauber coexistieren (siehe Code-Skizze für
  `CssAnalyzerTests`).
- **Keine `auto_fixable: ja`-Tech-Debt-Items zum Anhängen:** TD-001 ist
  "out of scope" laut Kritiker, TD-002 betrifft Commit-Disziplin
  (nicht im Code anhängbar). Keine opportunistischen Items.
