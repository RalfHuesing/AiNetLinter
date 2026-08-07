---
status: done (pending audit)
type: step-plan
task: flaky-and-test-performance
step: 005
corrects: null
title: "Category-Traits für 4 kleine Unit-Ordner (Arch/Diag/FP/Cache, 7 Klassen) nachziehen (Batch 4 von N)"
epic: EPIC-02
estimated_risk: low
step_type: batch
items:
  - id: item-01
    title: "ArchitectureTests → Unit (in-process LinterAnalyzer-Analyse)"
    source: "konzept.md §Wie Schritt 2, §Muss-Haven Traits-Punkt"
  - id: item-02
    title: "PerformanceProfilerTests → Unit (in-process Profiler + ConfigLoader)"
    source: "konzept.md §Wie Schritt 2"
  - id: item-03
    title: "FalsePositiveTests → Unit (in-process LinterAnalyzer-Analyse)"
    source: "konzept.md §Wie Schritt 2"
  - id: item-04
    title: "FalsePositiveExtensionsTests → Unit (in-process LinterAnalyzer + AIContextFootprintCalculator)"
    source: "konzept.md §Wie Schritt 2"
  - id: item-05
    title: "AnalysisCacheManagerTests → Unit (in-process AnalysisCacheManager + TestTempDirectory)"
    source: "konzept.md §Wie Schritt 2"
  - id: item-06
    title: "AnalysisCacheManagerIsolationTests → Unit (bereits 4× method-level getaggt; Klassen-Trait additiv)"
    source: "konzept.md §Wie Schritt 2"
  - id: item-07
    title: "CacheEntryMapperTests → Unit (in-process DTO↔Domain-Mapping)"
    source: "konzept.md §Wie Schritt 2"
created_by: planer
created_by_model: MiniMax-M3
created_by_model_knowledge_cutoff: 2026-01
created_at: 2026-08-07T12:30:00+02:00
related_to: []
---

# Step 005: Category-Traits für 4 kleine Unit-Ordner (Batch 4)

## Bezug

- **Task:** `flaky-and-test-performance`
- **Epic:** `EPIC-02` aus `roadmap.md` — Category-Traits flächendeckend
  nachziehen. Vierter von N Batches; **bündelt** die nächsten vier kleinen
  homogenen Unit-Ordner (`Architecture/`, `Diagnostics/`, `FalsePositives/`,
  `Cache/`) in **einem** Step, statt jeden 1-Klasse-Ordner einzeln zu planen
  (Overhead-Vermeidung — siehe Anti-Loop-/Bündelungs-Argument unten).
- **Konzept-Referenz:** `konzept.md` §"Wie" Schritt 2 ("Category-Traits
  nachziehen — alle ~1000 ungetragten Tests einordnen"), §"Muss-Haben"
  Traits-Punkt ("konsequente Category-Traits ... auf **allen** Tests —
  aktuell nur 86 von ~1087"), §"Definition of Done" Punkt "Alle Tests
  tragen einen Category-Trait".
- **Vorgänger-Steps:** `step-002` (approved, Suppression, 8 Klassen, 1
  Integration + 7 Unit), `step-003` (approved, Metrics, 7 Klassen, alle
  Unit), `step-004` (approved, Web, 5 Klassen, alle Unit). Die drei
  vorherigen Batches lieferten die etablierte Klassifikations-Heuristik
  (Subprozess-Marker = Integration; sonst Unit), die Trait-Syntax-Konvention
  (`[Trait("Category", "Unit")]`, CamelCase-Großbuchstabe) und die
  DoD-Struktur (Build grün, Voll-Test grün, Unit-Filter grün,
  Integration-Filter best-effort, Self-Lint `OK`, konkreter Subject-Vorschlag).
  `step-001` (EPIC-01, approved) lieferte die `SymbolGraphMcpCollection`-
  Collection-Definition und das TD-001-Wissen (Self-Lint-Befehl).
- **Anti-Loop-Check** gegen `codemap.md` (Stand 2026-08-07, 47 Einträge,
  6 Sektionen): die vier in diesem Step gebündelten Ordner sind in der
  Sektion „Test-Verzeichnisse — geplant für EPIC-02-Folge-Batches" jeweils
  mit `(zuletzt: step-002)` referenziert (Arch/Diag als 1-Klasse-Ordner,
  FalsePositives/ als 2-Klasse-Ordner, Cache/ als 3-Klasse-Ordner) — keine
  bestehende Entscheidung widerspricht diesem Bündelungs-Plan, die
  CodeMap-Korrektur aus step-004 (Web/ 4 → 5 Klassen) ist nicht auf diese
  Ordner übertragbar. **Keine** CodeMap-Änderung in diesem Step nötig.

## Aktueller Projektzustand (JIT-Kontext)

Beim Lesen der sieben Zieldateien vorgefunden (relevant für step-005):

- **Ziel-Ordner-Inventar (7 Klassen in 4 Ordnern):**
  - `src/AiNetLinter.Tests/Architecture/ArchitectureTests.cs` — 1 Klasse
  - `src/AiNetLinter.Tests/Diagnostics/PerformanceProfilerTests.cs` — 1 Klasse
  - `src/AiNetLinter.Tests/FalsePositives/FalsePositiveTests.cs` — 1 Klasse
  - `src/AiNetLinter.Tests/FalsePositives/FalsePositiveExtensionsTests.cs` — 1 Klasse
  - `src/AiNetLinter.Tests/Cache/AnalysisCacheManagerTests.cs` — 1 Klasse
  - `src/AiNetLinter.Tests/Cache/AnalysisCacheManagerIsolationTests.cs` — 1 Klasse
  - `src/AiNetLinter.Tests/Cache/CacheEntryMapperTests.cs` — 1 Klasse
- **Konzept-/CodeMap-Schätzung vs. Realität:** exakt bestätigt — 1+1+2+3 = 7
  Klassen in den 4 Ordnern, passt in den 8-Item-Deckel von `spec.md` §10.6
  mit einem Slot Reserve.
- **Bestehende Trait-Verteilung:** 0 Klassen mit Klassen-Trait in allen 4
  Ordnern, **aber** 4 method-level `[Trait("Category", "Unit")]` in
  `AnalysisCacheManagerIsolationTests` (Z. 28, 48, 66, 86 — verifiziert per
  `Select-String`). Alle 4 method-level Traits auf `Unit` — passt zur
  homogenen Unit-Klassifikation der Klasse (siehe unten).
- **Subprozess-Marker im gesamten 7-Datei-Set:** **0 Treffer** für
  `McpTestClient`, `CliProcessRunner`, `Program\.Main`,
  `IClassFixture<McpLiveRepositoryFixture>` (verifiziert per PowerShell-`Select-String`
  über alle 7 Dateien, 0/0/0/0 Treffer). Damit ist der gesamte Batch
  homogen **Unit** — keine Integration-Klasse. Passt sauber zur in
  step-002 etablierten Heuristik und zur step-002/step-003/step-004-Bestätigung.
- **Testmethoden-Inventar** (regex-basiert per `Select-String -Pattern '\[Fact\]'`
  / `'\[Theory\]'` — gemäß step-003-Review NITPICK "regex statt manuell
  zählen"):

  | Datei                                            | `[Fact]` | `[Theory]` |
  |--------------------------------------------------|---------:|-----------:|
  | `Architecture/ArchitectureTests.cs`              |       13 |          0 |
  | `Diagnostics/PerformanceProfilerTests.cs`         |        3 |          0 |
  | `FalsePositives/FalsePositiveTests.cs`           |       15 |          0 |
  | `FalsePositives/FalsePositiveExtensionsTests.cs` |       12 |          0 |
  | `Cache/AnalysisCacheManagerTests.cs`             |        7 |          0 |
  | `Cache/AnalysisCacheManagerIsolationTests.cs`    |        4 |          0 |
  | `Cache/CacheEntryMapperTests.cs`                  |        4 |          0 |
  | **Summe**                                         |  **58**  |     **0**  |

  Alle 58 sind `[Fact]`, keine `[Theory]` mit `[InlineData]`-Reihen. 4
  davon (alle in `AnalysisCacheManagerIsolationTests`) sind bereits
  method-level getaggt — diese sind im `Category=Unit`-Filter-Lauf schon
  enthalten; das Hinzufügen des Klassen-Traits ist **rein additiv**
  (xUnit-`Trait`-Filter wertet Klassen-Oder-Methoden-Trait, also keine
  Doppelt-Zählung). **Erwartetes Filter-Delta nach step-005:**
  Unit steigt um **54** (= 58 − 4), Integration unverändert, Total
  unverändert. Konkret: Unit 278 → **332**, Integration **113**, Total
  **1325** (nachvollziehbar im `step-result.md` zu verifizieren).
- **Klassen-Deklarationen — Trait-Platzierungs-Varianten** (verifiziert per
  `Select-String` über die 7 Dateien):
  - **Direkt über `public sealed class …` (3 Klassen, kein XML-Doc über der Klasse):**
    - `ArchitectureTests.cs:9` (kein XML-Doc, kein `// @covers`)
    - `AnalysisCacheManagerTests.cs:14` (kein XML-Doc, kein `// @covers`,
      Klasse `: IDisposable` — analog zu `MaxDirectoryChildrenTests` aus
      step-003)
    - `CacheEntryMapperTests.cs:14` (kein XML-Doc, kein `// @covers`)
  - **Zwischen `</summary>` und `public sealed class …` (2 Klassen, XML-Doc):**
    - `FalsePositiveTests.cs:15` (XML-Doc endet Z. 15, Klasse Z. 16) — analog
      zu `IgnoreSuppressionsFilter`-Konvention aus step-002 / zu den
      Metrics-Klassen mit XML-Doc aus step-003
    - `FalsePositiveExtensionsTests.cs:17` (XML-Doc endet Z. 17, Klasse Z. 18)
  - **Zwischen `// @covers`-Block und `public sealed class …` (1 Klasse, Coverage-Marker):**
    - `PerformanceProfilerTests.cs:12-15` (`// @covers` × 4 Zeilen), Klasse
      Z. 16 — analog zu `IgnoreSuppressionsFilterTests` aus step-002
      (Coverage-Marker bleiben direkt am Symbol, Trait darunter)
  - **Zwischen `</summary>` und `public sealed class … : IDisposable` (1 Klasse, XML-Doc + IDisposable):**
    - `AnalysisCacheManagerIsolationTests.cs:20` (XML-Doc endet Z. 20, Klasse
      Z. 21, `: IDisposable`) — kombiniert die XML-Doc-Variante mit der
      `IDisposable`-Variante aus `MaxDirectoryChildrenTests` (step-003);
      Negativ-Abgrenzung: die bestehenden 4 method-level Traits bleiben
      unangetastet (Z. 28, 48, 66, 86 — direkt über `[Fact]`-Methoden).
- **EOL- und Trailing-NL-Status** (verifiziert per PowerShell über alle 7
  Dateien): **homogen CRLF + Trailing-NL** in allen 7 Dateien. 3 Dateien
  mit BOM (`PerformanceProfilerTests`, `FalsePositiveTests`,
  `FalsePositiveExtensionsTests`), 4 ohne BOM (`ArchitectureTests`,
  `AnalysisCacheManagerTests`, `AnalysisCacheManagerIsolationTests`,
  `CacheEntryMapperTests`). **Kein gemischter Status** (anders als in
  step-004, wo `Web/`-Dateien LF/CRLF gemischt hatten und einen
  byte-genauen Python-Helper nötig machten) — der Coder kann alle 7
  Edits mit dem Standard-Edit-Tool durchführen, ohne Diff-Aufblähung
  befürchten zu müssen. Der Coder verifiziert dies vorab per
  `Get-Content -Encoding UTF8 <file> | Select-Object -Last 1` (zeigt
  das letzte Byte-Zeichen) und durch `git diff` nach dem Edit.
- **Spezialfall `AnalysisCacheManagerIsolationTests` — bereits teilweise
  getaggt:** 4 method-level `[Trait("Category", "Unit")]` sind vorhanden
  (Z. 28, 48, 66, 86). Das Hinzufügen des Klassen-Traits
  `[Trait("Category", "Unit")]` auf Klassen-Ebene ist **rein additiv**
  (xUnit-Trait-Filter wertet Klassen-Oder-Methoden-Trait, keine
  Doppelt-Zählung im Filter-Lauf). Die 4 method-level Traits bleiben
  unverändert — der Klassen-Trait wird **zwischen** dem XML-Doc (Z. 20)
  und der Klassendeklaration (Z. 21) eingefügt. **Heuristik-Fortschreibung
  Punkt 4 (folgt auf Punkt 3 aus step-004 "`null!` als Edge-Input"):**
  bei homogenen Klassen mit bereits teilweise getaggten Methoden
  wird der Klassen-Level-Trait **trotzdem** gesetzt — er ist additiv,
  macht die Klassifikation explizit auf Klassen-Ebene sichtbar und
  ist Voraussetzung dafür, dass spätere neue Methoden in derselben
  Klasse automatisch im Filter erfasst werden, ohne dass der Autor
  daran denken muss, einen method-level Trait zu setzen.
- **Bündelungs-Begründung (Kleine-Klassen-Ordner-Bündel statt Einzelschritte):**
  Die 4 in diesem Step gebündelten Ordner sind zusammen 7 Klassen
  (passt in den 8-Item-Deckel von `spec.md` §10.6). Eine
  1-Klasse-pro-Ordner-Verarbeitung wäre Overhead (jeder Step hat
  Review-/Commit-/Doku-Zyklen) ohne Mehrwert. **Vorteile der Bündelung:**
  (1) **Berechtigt durch homogenen Charakter** — alle 7 Klassen sind
  `Unit` ohne Subprozess-Marker, einheitliche Heuristik-Anwendung;
  (2) **Klassen-Level-Mix bleibt überschaubar** — keine
  Integration-Klasse im Set, also kein Misch-Heuristik-Diskussion;
  (3) **XML-Doc/`// @covers`/`IDisposable`-Varianten sind lokal pro
  Datei** behandelbar (siehe Aufzählung oben), keine übergeordneten
  Sonderfälle; (4) **Platzierung des `Bezug`-Hinweises** in der
  CodeMap-Sektion „Test-Verzeichnisse — geplant für EPIC-02-Folge-
  Batches" passt — die 4 Ordner stehen dort alle unter dem Block
  "Reine-Unit-Ordner, klein" (zusammen mit `Web/`, das in step-004
  abgehakt wurde); die Bündelung räumt diesen Block in einem Schritt
  ab.
- **Alternative, verworfen — `Output/` (10 Klassen) als nächster
  alleiniger Step:** wäre ein 10-Item-Batch, der den 8-Item-Deckel
  reißt; müsste vorab in zwei 5er-Batches aufgeteilt werden. Sinnvoller
  in einem eigenen Step (step-006 oder step-007) mit eigener
  Heuristik-Bestätigung.

## Intention

Alle 7 Testklassen in den 4 Ordnern `Architecture/`, `Diagnostics/`,
`FalsePositives/`, `Cache/` mit `[Trait("Category", "Unit")]` auf
Klassen-Ebene versehen. Dieser Step ist der vierte von N Batches, die
zusammen die EPIC-02-DoD erreichen ("alle ~1000 Tests getraggt"). Er
räumt den in step-002/003/004 etablierten "Reine-Unit-Ordner, klein"-
Block in einem Schritt vollständig ab und liefert damit die
vierte Template-Validierung für die Folge-Batches, **bevor** diese in
die größeren, gemischten Verzeichnisse (`Output/`, `Configuration/`,
`Core/Checkers/`, `Mcp/`, `Commands/`) vorstoßen. Er demonstriert
außerdem drei neue Lokal-Varianten (alle bisherigen Schritte
behandelten 1–2 Varianten):

1. `IDisposable` + XML-Doc auf Klassen-Ebene
   (`AnalysisCacheManagerIsolationTests`) — Kombination aus
   `MaxDirectoryChildrenTests`-IDisposable-Variante (step-003) und
   XML-Doc-Variante (step-003/004).
2. Klassen-Trait additiv zu existierenden method-level Traits
   (`AnalysisCacheManagerIsolationTests`) — Heuristik-Fortschreibung
   Punkt 4.
3. `// @covers`-Block-Plus-Trait (PerformanceProfilerTests) — erweitert
   die step-002-`IgnoreSuppressionsFilter`-Konvention auf einen
   mehrzeiligen `// @covers`-Block.

## Klassifikations-Heuristik für diesen Batch

Die in step-002 dokumentierte und in step-003/004 bestätigte Heuristik
wird unverändert übernommen:

1. **Bestehende Traits prüfen.** Im Batch sind 4 method-level Traits in
   `AnalysisCacheManagerIsolationTests` vorhanden (alle `Unit`,
   verifiziert per `Select-String`). Diese bleiben **unverändert** und
   werden durch den Klassen-Trait **additiv** ergänzt — keine
   bestehenden Trait-Attribute werden überschrieben, entfernt oder
   modifiziert. (In den übrigen 6 Klassen des Batches sind 0 bestehende
   Trait-Attribute vorhanden.)
2. **Subprozess-Marker prüfen.** Im Batch sind 0 Subprozess-Marker
   vorhanden (verifiziert per `Select-String` über `McpTestClient`,
   `CliProcessRunner`, `Program\.Main`,
   `IClassFixture<McpLiveRepositoryFixture>` über alle 7 Dateien, 0/0/0/0
   Treffer). Damit ist **keine** Klasse in diesem Batch `Integration`.
3. **Sonst: Unit.** Trifft auf alle 7 Klassen in diesem Batch zu.

**Wichtige Negativ-Abgrenzung** (aus step-002, weiterhin gültig, an den
4 Kandidatenordnern verifiziert): Die folgenden Muster sind **KEIN**
Subprozess und führen nicht zu `Integration`:

- `CSharpCompilation.Create(...)` / `MetadataReference.CreateFromFile(...)` /
  `LinterAnalyzer.Analyze(...)` (in `Architecture/`, `FalsePositives/`) —
  in-process Roslyn-API, kein Subprozess
- `ConfigLoader.TryLoadConfig(...)` / `PerformanceProfiler` / `ProfilerJsonReport` /
  `AppDomain.CurrentDomain.BaseDirectory` (in `Diagnostics/`) — in-process
- `AnalysisCacheManager.Load(...)` / `CacheEntryMapper.To*` / `ToClassInfo` /
  `RestoreToState` / `TestTempDirectory.Create(...)` (in `Cache/`) —
  in-process File-IO + DTO-Mapping
- `TestTempDirectory` (eine kleine Test-Fixture unter
  `src/AiNetLinter.Tests/Fixtures/TestTempDirectory.cs`, in `Cache/`
  verwendet) — in-process Temp-Verzeichnis-Wrapper, kein Subprozess
- `Task.Run(...)` mit `Task.WaitAll([.. tasks])` (in
  `AnalysisCacheManagerTests.cs:118-133` — 200 concurrent in-process
  Tasks) — in-process, kein Subprozess
- `IClassFixture<…>` — **kein** Vorkommen in den 7 Dateien
  (verifiziert per `Select-String -Pattern 'IClassFixture'`, 0 Treffer)

**Heuristik-Fortschreibung Punkt 4 (neu in diesem Step, Folge auf
Punkt 3 aus step-004 "`null!` als Edge-Input"):** Wenn eine Klasse
bereits teilweise method-level Traits trägt, die alle mit der
Klassen-Homogenität konsistent sind (in diesem Batch: alle 4 vorhandenen
method-level Traits sind `Unit`, alle Methoden sind in-process) → den
Klassen-Trait trotzdem setzen. Begründung: (a) explizite
Klassifikations-Sichtbarkeit auf Klassen-Ebene (man sieht der Klasse
direkt an, dass sie `Unit` ist, ohne die Methoden durchscrollen zu
müssen); (b) neue Methoden, die später zur Klasse hinzukommen, werden
automatisch vom Filter erfasst, ohne dass der Autor einen method-level
Trait explizit setzen muss; (c) xUnit-Trait-Filter wertet
Klassen-Oder-Methoden-Trait, also keine Doppelt-Zählung im Filter-
Lauf. **Wichtig:** bestehende method-level Traits werden **nicht**
entfernt oder modifiziert — nur der Klassen-Trait wird additiv
hinzugefügt.

## Konkrete Änderungen

**Bei `step_type: batch`** (siehe `../spec.md` §10.6): pro Item aus der
`items`-Liste im Frontmatter eine Unterüberschrift.

### item-01: `ArchitectureTests` → Unit — `src/AiNetLinter.Tests/Architecture/ArchitectureTests.cs` (Klassen-Deklaration, Z. 9)

- **Was:** Direkt über `public sealed class ArchitectureTests` (Z. 9) eine
  Zeile `[Trait("Category", "Unit")]` einfügen. Keine XML-Doc, kein
  `// @covers`-Marker vorhanden — daher genügt eine Zeile direkt über
  der Klassendeklaration (Z. 8 bleibt Leerzeile).
- **Warum:** Klasse enthält 13 `[Fact]`-Methoden, die alle
  `LinterAnalyzer.Analyze(...)` auf in-process `CSharpCompilation`-
  Instanzen aufrufen (verifiziert per Datei-Inspektion). Subprozess-
  Marker-Grep liefert 0 Treffer. Trait-Wert folgt exakt der
  bestehenden Konvention (`[Trait("Category", "Unit")]`,
  CamelCase-Großbuchstabe).

### item-02: `PerformanceProfilerTests` → Unit — `src/AiNetLinter.Tests/Diagnostics/PerformanceProfilerTests.cs` (Z. 15-16, zwischen `// @covers`-Block und Klasse)

- **Was:** Zwischen dem letzten `// @covers`-Kommentar (Z. 15:
  `// @covers ProfilerSummary`) und `public sealed class
  PerformanceProfilerTests` (Z. 16) eine Zeile
  `[Trait("Category", "Unit")]` einfügen. **Achtung:** `// @covers`-
  Marker bleiben **direkt** am Symbol (Coverage-Konvention analog zu
  `IgnoreSuppressionsFilterTests` aus step-002), der Trait gehört
  **zwischen** den `// @covers`-Block und die Klassendeklaration
  (nicht davor).
- **Warum:** Klasse enthält 3 `[Fact]`-Methoden, die alle in-process
  auf `PerformanceProfiler`, `ConfigLoader.TryLoadConfig(...)` und
  `File.WriteAllText`/`File.ReadAllText` auf einem `AppDomain.CurrentDomain.BaseDirectory`-
  Subpfad arbeiten. Subprozess-Marker-Grep liefert 0 Treffer.
  Datei hat UTF-8-BOM (verifiziert per PowerShell) — der Coder
  achtet darauf, den BOM beim Edit **nicht** zu verlieren (Standard-
  Edit-Tool erhält ihn; nur bei naivem Re-Write über `Set-Content`
  ohne `-Encoding UTF8` würde er verschwinden). Trait-Wert folgt
  der Konvention.

### item-03: `FalsePositiveTests` → Unit — `src/AiNetLinter.Tests/FalsePositives/FalsePositiveTests.cs` (Z. 15-16, zwischen XML-Doc und Klasse)

- **Was:** Zwischen `</summary>` (Z. 15, Ende der XML-Doc-Section) und
  `public sealed class FalsePositiveTests` (Z. 16) eine Zeile
  `[Trait("Category", "Unit")]` einfügen. **Achtung:** das XML-Doc
  beginnt auf Z. 12 mit `/// <summary>` und endet auf Z. 15 mit
  `///</summary>` — der Trait gehört **zwischen** `</summary>` und
  `public sealed class`, nicht davor (analog zur
  `IgnoreSuppressionsFilter`-Konvention aus step-002 und zur
  XML-Doc-Variante aus step-003/004).
- **Warum:** Klasse enthält 15 `[Fact]`-Methoden, die alle
  `LinterAnalyzer.Analyze(...)` auf in-process `CSharpCompilation`-
  Instanzen aufrufen (verifiziert per Datei-Inspektion; Tests
  beschreiben False-Positive-Szenarien wie `Deconstruct` mit
  `out`-Parametern, switch-expression-Komplexität, `record with`-
  Ausdrücke etc.). Subprozess-Marker-Grep liefert 0 Treffer. Datei
  hat UTF-8-BOM (verifiziert) — der Coder achtet darauf, den BOM
  beim Edit nicht zu verlieren.

### item-04: `FalsePositiveExtensionsTests` → Unit — `src/AiNetLinter.Tests/FalsePositives/FalsePositiveExtensionsTests.cs` (Z. 17-18, zwischen XML-Doc und Klasse)

- **Was:** Zwischen `</summary>` (Z. 17, Ende der XML-Doc-Section) und
  `public sealed class FalsePositiveExtensionsTests` (Z. 18) eine Zeile
  `[Trait("Category", "Unit")]` einfügen (zwischen XML-Doc und Klasse,
  wie item-03).
- **Warum:** Klasse enthält 12 `[Fact]`-Methoden, die
  `LinterAnalyzer.Analyze(...)` und `AIContextFootprintCalculator.Calculate(...)`
  auf in-process `CSharpCompilation`-Instanzen aufrufen. Tests
  beschreiben Erweiterungs-Szenarien (`AllowOutParametersInPrivateMethods`,
  `SemanticNamingExemptMethodNames`, `FootprintIgnoreTypeNames`,
  `SemanticNamingAllowSubstringOfMethodName`). Subprozess-Marker-Grep
  liefert 0 Treffer. Datei hat UTF-8-BOM (verifiziert) — der Coder
  achtet darauf, den BOM beim Edit nicht zu verlieren.

### item-05: `AnalysisCacheManagerTests` → Unit — `src/AiNetLinter.Tests/Cache/AnalysisCacheManagerTests.cs` (Klassen-Deklaration, Z. 14)

- **Was:** Direkt über `public sealed class AnalysisCacheManagerTests
  : IDisposable` (Z. 14) eine Zeile `[Trait("Category", "Unit")]`
  einfügen. Keine XML-Doc, kein `// @covers`-Marker vorhanden.
  **Spezialfall:** die Klasse implementiert `IDisposable` (Zeile 14)
  für `TestTempDirectory`-Cleanup (in-process, kein Subprozess) —
  passt zur Negativ-Abgrenzung "TempDir-Operationen" und zur
  `MaxDirectoryChildrenTests`-`IDisposable`-Variante aus step-003
  (das `IDisposable`-Interface ändert nichts an der
  Unit-Klassifikation).
- **Warum:** Klasse enthält 7 `[Fact]`-Methoden, die alle auf
  `AnalysisCacheManager.Load(...)` mit einem `TestTempDirectory`-
  Pfad arbeiten. Subprozess-Marker-Grep liefert 0 Treffer. Eine
  Methode (`CacheManager_ConcurrentGetAndSet_DoesNotThrow`,
  Z. 110-135) verwendet `Task.Run(...)` mit 200 concurrent in-process
  Tasks — das ist **kein** Subprozess, sondern in-process
  Thread-Pool-Concurrency.

### item-06: `AnalysisCacheManagerIsolationTests` → Unit — `src/AiNetLinter.Tests/Cache/AnalysisCacheManagerIsolationTests.cs` (Z. 20-21, zwischen XML-Doc und Klasse, Klassen-Trait additiv zu 4 bestehenden method-level Traits)

- **Was:** Zwischen `</summary>` (Z. 20, Ende der XML-Doc-Section) und
  `public sealed class AnalysisCacheManagerIsolationTests : IDisposable`
  (Z. 21) eine Zeile `[Trait("Category", "Unit")]` einfügen
  (zwischen XML-Doc und Klasse, kombiniert die XML-Doc-Variante
  aus step-003/004 mit der `IDisposable`-Variante aus
  `MaxDirectoryChildrenTests`). **Wichtig:** die 4 bestehenden
  method-level `[Trait("Category", "Unit")]`-Attribute auf Z. 28,
  48, 66, 86 bleiben **unverändert** — der Klassen-Trait ist rein
  additiv (xUnit-Trait-Filter wertet Klassen-Oder-Methoden-Trait,
  keine Doppelt-Zählung im Filter-Lauf).
- **Warum:** Klasse enthält 4 `[Fact]`-Methoden (alle bereits mit
  method-level `Unit`-Trait), die `AnalysisCacheManager.Load(...)` mit
  `TestTempDirectory` und `CreateFile(...)` verwenden. Subprozess-
  Marker-Grep liefert 0 Treffer. **Heuristik-Fortschreibung Punkt 4**
  (siehe oben): Klassen-Trait additiv zu bestehenden method-level
  Traits — Klassifikations-Sichtbarkeit auf Klassen-Ebene, zukünftige
  neue Methoden werden automatisch vom Filter erfasst.

### item-07: `CacheEntryMapperTests` → Unit — `src/AiNetLinter.Tests/Cache/CacheEntryMapperTests.cs` (Klassen-Deklaration, Z. 14)

- **Was:** Direkt über `public sealed class CacheEntryMapperTests`
  (Z. 14) eine Zeile `[Trait("Category", "Unit")]` einfügen. Keine
  XML-Doc, kein `// @covers`-Marker vorhanden.
- **Warum:** Klasse enthält 4 `[Fact]`-Methoden, die
  `CacheEntryMapper.ToViolation(...)` / `ToClassInfo(...)` /
  `ToPartialPart(...)` / `RestoreToState(...)` auf in-process
  DTO↔Domain-Mapping testen. Subprozess-Marker-Grep liefert 0
  Treffer.

## Tests

Keine — die Klassifikation ist rein additiv (Attribut setzen). Existierende
Tests müssen **unverändert** grün bleiben. Validierung erfolgt über den
vollen `dotnet test`-Lauf in der Definition of Done (kein neuer Test, kein
geänderter Test).

## Definition of Done

- [ ] Alle 7 Items umgesetzt (je eine `[Trait("Category", "Unit")]`-Zeile
  auf Klassen-Ebene, ggf. zwischen XML-Doc/`// @covers`-Block und
  Klassendeklaration — siehe Aufzählung oben)
- [ ] **Bestehende Traits respektiert:** die 4 method-level
  `[Trait("Category", "Unit")]`-Attribute in
  `AnalysisCacheManagerIsolationTests.cs` (Z. 28, 48, 66, 86) bleiben
  **unverändert** (verifiziert per `git diff` nach dem Edit). Nach
  dem Diff sollten in den 4 Ordnern alle 7 Klassen mit Klassen-Trait
  ausgestattet sein, 0 ohne; die 4 method-level Traits sind weiterhin
  vorhanden (additiv).
- [ ] **BOM-Erhaltung:** die 3 Dateien mit UTF-8-BOM
  (`PerformanceProfilerTests`, `FalsePositiveTests`,
  `FalsePositiveExtensionsTests`) behalten ihren BOM nach dem Edit
  (verifiziert per PowerShell `Get-Content -Encoding UTF8 -TotalCount 1
  <file>` — der erste Byte-Trippel muss `EF BB BF` sein).
- [ ] **EOL/Trailing-NL-Konservierung:** alle 7 Dateien behalten
  CRLF-Zeilenenden und Trailing-NL nach dem Edit (verifiziert per
  PowerShell-`Select-String`-Prüfung und/oder `git diff` — keine
  Zeilenende-Änderungen). Bei diesem Batch **kein** byte-genauer
  Python-Helper nötig (anders als in step-004), weil alle 7 Dateien
  uniform CRLF + Trailing-NL haben — Standard-Edit-Tool reicht.
- [ ] **Build-Command** aus Tech-Stack-Notiz (`roadmap.md`) grün:
  `dotnet build` (Zero-Warning-Direktive, `TreatWarningsAsErrors=true`
  in beiden Projekten)
- [ ] **Test-Command** aus Tech-Stack-Notiz grün: `dotnet test`
  (voller Lauf, alle Tests müssen weiterhin grün sein — keine
  Test-Logik wurde geändert)
- [ ] **Trait-Filter-Smoke-Test** (vom Coder zusätzlich durchführen, um
  die Klassifikation zu verifizieren):
  - `dotnet test --no-build --filter "Category=Unit"` → muss grün sein
  - `dotnet test --no-build --filter "Category=Integration"` →
    **best-effort, ein Lauf grün** (gemäß step-002/step-003/step-004
    NITPICK-Linie: pre-existing Flaky-Test
    `McpServerCommandLoadingStateTests.LoadState_LoadFuncCompletesSynchronouslyWithCatalog_ReportsLoadedImmediately`
    flake-t gelegentlich unter Last des Integration-Filters; nicht
    step-005-verursacht, Fix in EPIC-06). Der Coder dokumentiert im
    `step-result.md`, wenn der Lauf flaky ist, und startet ihn ggf.
    einmal neu.
  - **Numerische Plausibilitätsprüfung** (gemäß step-003-Review
    NITPICK "regex statt manuell zählen"): der Coder zählt die
    `[Fact]`/`[Theory]`-Methoden in den 7 Klassen **regex-basiert**
    per `Select-String -Pattern '\[Fact\]'` / `'\[Theory\]'` (NICHT
    manuell durchgehen), dokumentiert die Summe im `step-result.md`
    und vergleicht sie mit dem erwarteten Unit-Filter-Delta.
    **Erwartetes Delta:** Unit steigt um **54** (13+3+15+12+7+4+4
    = 58, davon 4 bereits method-level getaggt → 54 neu für den
    Unit-Filter; verifiziert per `Select-String` durch den Planer;
    siehe "Aktueller Projektzustand" oben). Integration-Zahl
    bleibt unverändert bei 113. Total bleibt unverändert bei 1325.
    **Erwarteter Unit-Filter-Wert nach step-005: 332** (278 + 54).
- [ ] **Self-Lint** (TD-001-konform, semantisch identisch zu
  `--self-lint`): `dotnet run --project src/AiNetLinter --
  --config rules.json --path .` → muss `OK` ausgeben
- [ ] **Commit auf aktuellem Branch** (Conventional Commit auf Deutsch,
  imperativ, mit Task-Suffix `[flaky-and-test-performance]`):
  **konkreter Subject-Vorschlag** (gemäß TD-002, "kürzere
  Subject-Bodies vorgeben"):
  `test: 4 Unit-Ordner Kategorie-taggen [flaky-and-test-performance]`
  → **65 Zeichen** inkl. Suffix (exakt verifiziert per
  `('test: 4 Unit-Ordner Kategorie-taggen [flaky-and-test-performance]').Length`
  in PowerShell; deckt 7 Zeichen Sicherheitsabstand zur
  72-Zeichen-Grenze). Pattern spiegelt step-002's `test:
  Suppression-Tests Kategorie-taggen [flaky-and-test-performance]`
  (69 Zeichen) und step-004's `test: Web-Tests Kategorie-taggen
  [flaky-and-test-performance]` (61 Zeichen) — gleicher Aufbau,
  konsistent zur EPIC-02-Batch-Serie. **Falls** der Coder den
  Subject abwandeln will (z. B. weil ihm der Verb "kategorie-taggen"
  nicht passt), **muss** er 72 Zeichen einhalten und die neue exakte
  Länge im `step-result.md` dokumentieren — bei Überschreitung
  TD-002-Eintrag aktualisieren.
- [ ] `step-005/step-result.md` geschrieben mit: Diff-Statistik
  (Anzahl hinzugefügter Trait-Zeilen pro Item, Gesamt-Diff), Test-
  ergebnis (Gesamt-Lauf + 2 Filter-Läufe mit Test-Zahlen — die per
  `Select-String` regex-basiert verifizierte Summe **58** explizit
  nennen, das **54**-Delta explizit nennen, mit dem tatsächlichen
  Filter-Delta abgleichen), Build-Output, Self-Lint-Output, Commit-
  Hash, Subject mit exakter Längen-Angabe.
  `### Commit-Vorschlag`-Block am Ende der Antwort (Pflicht — siehe
  `AiNetLinterRichtlinien.mdc` §4, Commit-Vorschlag-Pflicht).
- [ ] `status` in `step-plan.md` von `open` auf `in_progress` (durch
  Orchestrator nach Coder-Start) und nach `step-result.md`-Schreiben
  auf `done (pending audit)` (durch Coder) gesetzt.

## Rules-Refs

- `.agents/rules/AiNetLinterRichtlinien.mdc` §4 "Testsuite-Parallelität
  bewahren" — relevant nur als Ausschluss: Trait-Attribute haben
  **keinen** Einfluss auf Parallelismus, nur `[Collection(...)]` /
  `DisableParallelization`. Dieser Step berührt die Parallelität nicht,
  ist also nicht regel-restriktiv hier.
- `.agents/rules/AiNetLinterRichtlinien.mdc` §5 "Sparsame Kommentare" —
  die hinzugefügten Trait-Zeilen sind XML-Attribute, keine Kommentare.
  Kein Bezug. (Die in den 4 Klassen vorhandenen `// @covers`- und
  `/// <summary>`-Kommentare sind Bestand und bleiben unverändert.)
- `.agents/rules/AiNetLinterRichtlinien.mdc` §5 "Zero-Warning-Direktive"
  — die Trait-Attribute sind `[Trait("Category", "Unit")]`, exakt die
  im Projekt etablierte Schreibweise (Großbuchstabe am Wortanfang).
  Keine Warnung erwartet, da das exakt der bestehenden Konvention
  folgt.
- `.agents/rules/AiNetLinterRichtlinien.mdc` §4 "Commit-Vorschlag
  Pflicht" — betrifft die Coder-Antwort, ist im DoD-Punkt oben
  explizit referenziert.
- `.agents/rules/AiNetLinterRichtlinien.mdc` §5 "Symptom-Fixing
  verboten" — betrifft diesen Step nicht direkt, aber als
  Plausibilitäts-Check: wenn ein Test rot wird, ist die Ursache zu
  suchen, nicht der Test abzuschwächen.

## Bekannte Ausnahmen

- **Pre-Existing-Flaky-Test im Integration-Filter** (aus
  step-002/step-003/step-004-Reviews übernommen):
  `McpServerCommandLoadingStateTests.LoadState_LoadFuncCompletesSynchronouslyWithCatalog_ReportsLoadedImmediately`
  flake-t gelegentlich unter Last des `Category=Integration`-Filters.
  Nicht step-005-verursacht (rein additives Attribut, keine
  Logik-/Parallelitäts-Änderung), Fix in EPIC-06. Der Coder behandelt
  den Integration-Filter-Lauf als "best-effort, ein Lauf grün"
  (siehe DoD).
- **Bestehende 4 method-level `[Trait("Category", "Unit")]`-Attribute
  in `AnalysisCacheManagerIsolationTests`:** werden durch den
  Klassen-Trait **additiv ergänzt**, nicht modifiziert oder entfernt
  (Heuristik-Fortschreibung Punkt 4). Numerisch keine Doppelt-Zählung
  im Filter-Lauf (xUnit wertet Klassen-Oder-Methoden-Trait).

## Code-Skizze (optional)

Vorher (Beispiel: `ArchitectureTests.cs`, Z. 7-10):

```csharp
namespace AiNetLinter.Tests.Architecture;

public sealed class ArchitectureTests
{
    [Fact]
    public void Analyze_WithCompliantCode_ReturnsZeroViolations()
```

Nachher:

```csharp
namespace AiNetLinter.Tests.Architecture;

[Trait("Category", "Unit")]
public sealed class ArchitectureTests
{
    [Fact]
    public void Analyze_WithCompliantCode_ReturnsZeroViolations()
```

Für `AnalysisCacheManagerTests.cs` (Beispiel mit `: IDisposable`,
Z. 12-15):

```csharp
namespace AiNetLinter.Tests.Cache;

public sealed class AnalysisCacheManagerTests : IDisposable
{
    private readonly TestTempDirectory _tempDir = TestTempDirectory.Create("ainetlinter-cachetests-");
```

wird zu:

```csharp
namespace AiNetLinter.Tests.Cache;

[Trait("Category", "Unit")]
public sealed class AnalysisCacheManagerTests : IDisposable
{
    private readonly TestTempDirectory _tempDir = TestTempDirectory.Create("ainetlinter-cachetests-");
```

Für `FalsePositiveTests.cs` (Beispiel mit XML-Doc, Z. 10-16):

```csharp
namespace AiNetLinter.Tests.FalsePositives;

/// <summary>
/// Explorations-Suite: legitimer C#-Code, der vom Linter nicht als Fehler gemeldet werden darf.
/// Jeder Test beschreibt ein konkretes FP-Szenario. Fehlschläge beweisen echte False-Positives.
/// </summary>
public sealed class FalsePositiveTests
{
```

wird zu:

```csharp
namespace AiNetLinter.Tests.FalsePositives;

/// <summary>
/// Explorations-Suite: legitimer C#-Code, der vom Linter nicht als Fehler gemeldet werden darf.
/// Jeder Test beschreibt ein konkretes FP-Szenario. Fehlschläge beweisen echte False-Positives.
/// </summary>
[Trait("Category", "Unit")]
public sealed class FalsePositiveTests
{
```

Für `PerformanceProfilerTests.cs` (Beispiel mit `// @covers`-Block,
Z. 10-16):

```csharp
namespace AiNetLinter.Tests.Diagnostics;

// @covers PerformanceProfiler
// @covers DocumentPerformanceEntry
// @covers ProfilerJsonReport
// @covers ProfilerSummary
public sealed class PerformanceProfilerTests
{
```

wird zu:

```csharp
namespace AiNetLinter.Tests.Diagnostics;

// @covers PerformanceProfiler
// @covers DocumentPerformanceEntry
// @covers ProfilerJsonReport
// @covers ProfilerSummary
[Trait("Category", "Unit")]
public sealed class PerformanceProfilerTests
{
```

Für `AnalysisCacheManagerIsolationTests.cs` (Kombination XML-Doc +
`: IDisposable` + bereits 4 method-level Traits, Z. 10-21 + bestehende
method-level Traits Z. 28, 48, 66, 86 — alle unverändert):

```csharp
namespace AiNetLinter.Tests.Cache;

/// <summary>
///: Zwei Cache-Loads mit unterschiedlichen
/// Solution-Pfaden muessen unterschiedliche Cache-Filenamen erzeugen. ...
/// </summary>
public sealed class AnalysisCacheManagerIsolationTests : IDisposable
{
    ...
    [Fact]
    [Trait("Category", "Unit")]
    public void Load_DifferentSolutionPaths_ProduceDifferentHashes()
```

wird zu:

```csharp
namespace AiNetLinter.Tests.Cache;

/// <summary>
///: Zwei Cache-Loads mit unterschiedlichen
/// Solution-Pfaden muessen unterschiedliche Cache-Filenamen erzeugen. ...
/// </summary>
[Trait("Category", "Unit")]
public sealed class AnalysisCacheManagerIsolationTests : IDisposable
{
    ...
    [Fact]
    [Trait("Category", "Unit")]  // bestehend, unverändert
    public void Load_DifferentSolutionPaths_ProduceDifferentHashes()
```

## Notes

- **Batch-Umfang:** 7 Klassen × je 1 Trait-Zeile ≈ 7 Diff-Zeilen
  (zzgl. evtl. Anpassung der Leerzeile bei direktem Insert). Deutlich
  unter dem `max_batch_diff_lines: 40`-Deckel. Tatsächliche
  Diff-Statistik wird vom Coder im `step-result.md` dokumentiert.
- **Schritt-Typ `low`-Risk-Begründung:** rein additives Attribut auf
  Klassen, das weder Build-Verhalten noch Test-Verhalten noch
  Parallelität ändert. Trait-Wert folgt exakt der bestehenden
  100+-Eintrag-Konvention (`Unit`, CamelCase-Großbuchstabe). Kein
  Eingriff in Produktionscode, keine Fixture-Änderung, keine
  Test-Logik-Änderung. **Bestehende method-level Traits in
  `AnalysisCacheManagerIsolationTests` bleiben unangetastet** — der
  Klassen-Trait ist rein additiv (Heuristik-Fortschreibung Punkt 4).
- **EOL/Trailing-NL-Hinweis (vgl. step-004):** beim Lesen der 7
  Zieldateien wurde **homogener** EOL-Status festgestellt — alle 7
  Dateien sind CRLF + Trailing-NL (3 mit BOM, 4 ohne BOM).
  **Anders als in step-004** (wo `Web/` LF/CRLF gemischt hatte und
  einen byte-genauen Python-Helper nötig machte) ist **kein
  EOL-Helper nötig** — Standard-Edit-Tool reicht für alle 7 Edits.
  Der Coder verifiziert vorab und nach dem Edit per
  `git diff --stat` (sollte ≤10 Zeilen pro Datei sein) und
  PowerShell-BOM-Check für die 3 BOM-Dateien.
- **Heuristik-Fortschreibung Punkt 4 dokumentiert:** Klassen-Trait
  additiv zu bestehenden method-level Traits bei homogenen Klassen
  (siehe Klassifikations-Heuristik oben). Punkt 1-3 sind aus
  step-002/003/004 unverändert übernommen; Punkt 4 ist neu in
  diesem Step und ist für die Folge-Batches relevant (mehrere
  EPIC-02-Batches werden vermutlich Klassen mit bereits
  method-level Traits antreffen, z. B. `Commands/McpServerCommandTests.cs`
  mit 5 Unit-Traits auf Methoden-Ebene neben 18 Integration-
  Methoden — diese Klasse wird aber in einem eigenen Step mit
  `step_type: single` und Method-Trait-Planung behandelt, nicht in
  diesem Batch).
- **Bündelungs-Begründung (Kleine-Klassen-Ordner-Bündel):** die 4
  Ordner in diesem Step sind zusammen 7 Klassen (passt in den
  8-Item-Deckel) — eine 1-Klasse-pro-Ordner-Verarbeitung wäre
  Overhead ohne Mehrwert. **Berechtigt durch:** (a) alle 7 Klassen
  sind homogen `Unit` (0 Subprozess-Marker); (b) Klassen-Level-Mix
  ohne Integration-Klasse; (c) Trait-Platzierungs-Varianten lokal
  pro Datei behandelbar (3 ohne XML-Doc, 2 mit XML-Doc, 1 mit
  `// @covers`-Block, 1 mit XML-Doc + `IDisposable` — keine
  Datei benötigt übergeordnete Sonderbehandlung); (d) `codemap.md`
  listet die 4 Ordner alle unter "Reine-Unit-Ordner, klein" (mit
  `Web/` bereits in step-004 abgehakt) — die Bündelung räumt
  diesen Block vollständig ab.
- **Alternative (verworfen) — `Output/` (10 Klassen) als nächster
  alleiniger Step:** wäre ein 10-Item-Batch, der den 8-Item-Deckel
  reißt; müsste vorab in zwei 5er-Batches aufgeteilt werden.
  Sinnvoller in einem eigenen Step (vermutlich step-006 oder
  step-007) mit eigener Heuristik-Bestätigung.
- **Numerische Vorab-Erwartung an die Filter-Läufe:** Aus
  step-004 wissen wir `Category=Unit` = 278 Tests und
  `Category=Integration` = 113 Tests (278+113=391 getaggte
  Methoden aus 1325 Gesamt, 934 ungetragte Methoden). Nach
  step-005 ist eine **Erhöhung** der `Category=Unit`-Zahl um
  **54** zu erwarten (13+3+15+12+7+4+4=58 Tests, davon 4 bereits
  method-level getaggt → 54 neu für den Unit-Filter). Die
  `Category=Integration`-Zahl sollte unverändert bei 113 bleiben.
  `dotnet test` (voller Lauf) sollte weiterhin 1325 Tests zeigen.
  Konkret: Unit 278 → **332**, Integration **113**, Total
  **1325** (vom Coder im `step-result.md` zu verifizieren).
- **Folge-Batches (NICHT in diesem Step geplant — informativ):**
  die EPIC-02-Arbeit umfasst weiterhin ca. 141 verbleibende
  ungetaggte Testklassen nach step-005. Vorschlag für die
  Reihenfolge der nächsten Step-Modus-Aufrufe (rein informativ
  — Planung der einzelnen Folge-Steps ist Sache der jeweiligen
  Planer-Aufrufe, nicht dieses Plans):
  1. **Reine-Unit-Ordner, mittel** (zwischen 5 und 10 Klassen,
     passend für einen 8-Item-Batch):
     - `Evals/` (3 Klassen — `EvalAssemblerTests`, `SpecLoaderTests`,
       `ListEvalsCommandTests`; **Spezialfall `ListEvalsCommandTests`:
       möglicherweise Integration via Subprozess, JIT zu prüfen** —
       wenn Integration-Anteil, eigener Mini-Step)
     - `Output/` (10 Klassen, alle Unit) — vermutlich zwei
       5er-Batches, oder ein 8er-Batch + eigener 2er-Step
  2. **Reine-Unit-Ordner, groß** (mehrere Batches pro Ordner):
     - `Configuration/` (8 Klassen, alle Unit) — 1 Batch
     - `Core/Checkers/` (27 Klassen, alle Unit) — 3-4 Batches
     - `Core/` (19 Klassen, alle Unit) — 2-3 Batches
     - `Maps/` + `Maps/Skeleton/` (6 Klassen, alle Unit) — 1 Batch
  3. `Mcp/Tools/` (17 Klassen, fast alle Unit, Mini-Fixture-Workspace
     → Unit) — 2–3 Batches
  4. **Verzeichnisse mit echtem Subprozess-Anteil** (Heuristik-
     Ausnahmen, erfordern mehr Sorgfalt):
     - `Mcp/` (19 Klassen, gemischt; `McpCodeGraphServer*Tests` Unit,
       `McpLiveRepositoryTests`/`McpDocumentationSmokeTests`
       Integration)
     - `Baseline/` (10 Klassen, gemischt; `BaselineCliTests`/
       `WebBaselineTests` Integration, `SourceFileCatalog*Tests`
       Unit)
  5. **`Commands/`** (17 Klassen, stark gemischt;
     `McpServerCommandTests` ist die prominenteste gemischte Klasse —
     5 Unit + 18 Integration in einer Klasse, erfordert
     pro-Methode-Tagging) — mehrere Batches, höchste Komplexität.
     **Empfehlung:** die gemischte `McpServerCommandTests.cs` als
     eigenen Step planen (voraussichtlich `step-XXX` mit
     `step_type: single`), um die Methoden-Ebene-Heuristik sauber zu
     dokumentieren.
  6. **`Fixtures/`-eigene Tests** (`LoadFixtureBuilderTests`,
     `LoadFixtureMeasurementsTests`, `TD016aRefactorTests` —
     bereits getraggt) und die `Cli/`-Klasse
     `CliCommandBuilderMcpLogTests` (Unit) — am Ende als
     Aufräum-Batch, falls noch nicht in vorherigen Batches erledigt.
- **Doku-Pflicht:** Nach Abschluss aller EPIC-02-Batches (nicht nach
  jedem Batch) muss `roadmap.md` aktualisiert werden, um den
  EPIC-02 als abgeschlossen zu markieren und die DoD-Punkte aus
  `konzept.md` §"Definition of Done" durchzugehen. Diese Pflicht
  ist **nicht** Teil von step-005, sondern gehört in den letzten
  EPIC-02-Batch oder in den EPIC-08-Abschluss-Validierungs-Step.

## Sonstige Beobachtungen

- **Subject-Länge bestätigt:** der im DoD vorgegebene Subject
  `test: 4 Unit-Ordner Kategorie-taggen [flaky-and-test-performance]`
  ist **65 Zeichen** inkl. Suffix (exakt verifiziert per
  `('test: 4 Unit-Ordner Kategorie-taggen [flaky-and-test-performance]').Length`
  in PowerShell; deckt 7 Zeichen Sicherheitsabstand zur
  72-Zeichen-Grenze aus `AiNetLinterRichtlinien.mdc` §4 /
  `spec.md` §10.3). Damit ist die TD-002-Disziplin-Variante (a)
  "Planer gibt Subject konkret vor" eingehalten — der Coder
  übernimmt den Subject **unverändert** (analog zu step-004, der
  diese Disziplin erstmalig erfolgreich umgesetzt hat).
