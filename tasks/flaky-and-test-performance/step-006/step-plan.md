---
status: open
type: step-plan
task: flaky-and-test-performance
step: 006
corrects: null
title: "Category-Traits für src/AiNetLinter.Tests/Evals/ nachziehen (Batch 5 von N)"
epic: EPIC-02
estimated_risk: low
step_type: batch
items:
  - id: item-01
    title: "EvalAssemblerTests → Unit (in-process EvalAssembler.Assemble + TestTempDirectory + EvalRegistry.TryResolve)"
    source: "konzept.md §Wie Schritt 2, §Muss-Haven Traits-Punkt"
  - id: item-02
    title: "SpecLoaderTests → Unit (in-process SpecLoader.Load + TestTempDirectory + File-IO)"
    source: "konzept.md §Wie Schritt 2"
  - id: item-03
    title: "ListEvalsCommandTests → Unit (in-process ListEvalsCommand.Run mit TestLintConsole-Mock aus AiNetLinter.Tests.Output; **Hypothese aus codemap.md step-002 'möglicherweise Integration via Subprozess' widerlegt**)"
    source: "konzept.md §Wie Schritt 2; Anti-Loop-Check-Fund gegen codemap.md Evals/-Eintrag (step-002)"
created_by: planer
created_by_model: MiniMax-M3
created_by_model_knowledge_cutoff: 2026-01
created_at: 2026-08-07T13:10:00+02:00
related_to: []
---

# Step 006: Category-Traits für Evals-Ordner (Batch 5)

## Bezug

- **Task:** `flaky-and-test-performance`
- **Epic:** `EPIC-02` aus `roadmap.md` — Category-Traits flächendeckend
  nachziehen. Fünfter von N Batches; **bündelt** den kompletten
  `Evals/`-Ordner (3 Klassen, homogen Unit) in **einem** Step.
- **Konzept-Referenz:** `konzept.md` §"Wie" Schritt 2 ("Category-Traits
  nachziehen — alle ~1000 ungetraggten Tests einordnen"), §"Muss-Haben"
  Traits-Punkt ("konsequente Category-Traits ... auf **allen** Tests —
  aktuell nur 86 von ~1087"), §"Definition of Done" Punkt "Alle Tests
  tragen einen Category-Trait".
- **Vorgänger-Steps:** `step-001` (EPIC-01, approved, Spike-Befund
  negativ — Sharing-Hebel nicht ausreichend), `step-002` (EPIC-02 Batch 1,
  Suppression, 8 Klassen, 7 Unit + 1 Integration, approved),
  `step-003` (EPIC-02 Batch 2, Metrics, 7 Klassen, alle Unit, approved),
  `step-004` (EPIC-02 Batch 3, Web/, 5 Klassen, alle Unit, approved),
  `step-005` (EPIC-02 Batch 4, Arch/Diag/FalsePositives/Cache, 7 Klassen,
  alle Unit, approved). Die vier vorherigen Batches lieferten die
  etablierte Klassifikations-Heuristik (Subprozess-Marker = Integration;
  sonst Unit), die Trait-Syntax-Konvention (`[Trait("Category", "Unit")]`,
  CamelCase-Großbuchstabe), die Trait-Platzierungs-Bibliothek
  (einfach-direkt, XML-Doc, `// @covers`-Block, `IDisposable`,
  `IDisposable + XML-Doc`, `IDisposable + XML-Doc + method-level-Traits`),
  die Heuristik-Fortschreibung Punkt 4 (Klassen-Trait additiv zu
  bestehenden method-level Traits bei homogenen Klassen), und die
  DoD-Struktur (Build grün, Voll-Test grün, Unit-Filter grün,
  Integration-Filter best-effort, Self-Lint `OK`, numerische
  Plausibilitätsprüfung, konkreter Subject-Vorschlag mit exakter
  Längen-Angabe).
- **Anti-Loop-Check** gegen `codemap.md` (Stand 2026-08-07, 47 Einträge,
  6 Sektionen): **konkreter Befund** — der `Evals/`-Eintrag
  (`codemap.md` Z. 100, Stand 2026-08-07, zuletzt step-005) trug
  bislang die Annotation "`ListEvalsCommandTests` möglicherweise
  Integration via Subprozess, JIT zu prüfen" (gesetzt in step-002 und
  fortgeschrieben in step-005). Diese Hypothese ist **gegenstandslos**:
  der `JIT`-Prüfungs-Auftrag fällt genau in diesen Step (`step-006`),
  und die unten dokumentierte Code-Inspektion zeigt, dass
  `ListEvalsCommandTests` *kein* Subprozess startet — `dotnet AiNetLinter.dll`
  ist nirgendwo im Spiel. Die CodeMap-Annotation ist eine **konkrete
  Lücke im Ist-Stand** (eine widerlegte Hypothese, die ohne
  Aktualisierung den nächsten Planer in die Irre führen würde) und
  wird deshalb **vor** dem Schreiben dieses Plans im `codemap.md`
  korrigiert: alte Annotation gestrichen, neue klare Aussage "alle 3
  Unit" + explizite Widerlegungs-Begründung + `step-006`-Referenz.
  Damit ist die CodeMap zum Plan-Schreiben wieder konsistent mit dem
  tatsächlichen Code. **Keine** weitere bestehende Entscheidung in der
  CodeMap widerspricht diesem Plan.

## Aktueller Projektzustand (JIT-Kontext)

Beim Lesen der drei Zieldateien vorgefunden (relevant für step-006):

- **Ziel-Ordner-Inventar (3 Klassen in 1 Ordner):**
  - `src/AiNetLinter.Tests/Evals/EvalAssemblerTests.cs` — 1 Klasse
  - `src/AiNetLinter.Tests/Evals/SpecLoaderTests.cs` — 1 Klasse
  - `src/AiNetLinter.Tests/Evals/ListEvalsCommandTests.cs` — 1 Klasse
- **Konzept-/CodeMap-Schätzung vs. Realität:** exakt bestätigt — 3
  Klassen, passt locker in den 8-Item-Deckel von `spec.md` §10.6 mit 5
  Slots Reserve.
- **Bestehende Trait-Verteilung:** 0 Klassen mit Klassen-Trait in allen
  3 Dateien, **0** method-level `[Trait(`-Vorkommen (verifiziert per
  `Select-String -Pattern '\[Trait\('` über die 3 Dateien, 0/0/0
  Treffer). **Alle 3 Klassen sind "jungfräulich"** — keine
  Vorab-Klassifikation zu respektieren, keine method-level Traits
  additiv zu ergänzen. Der reine Klassen-Trait-Insert ohne
  Bestandsschutz ist die einfachste denkbare Variante.
- **Subprozess-Marker im gesamten 3-Datei-Set** (verifiziert per
  PowerShell-`Select-String` mit Regex `McpTestClient`,
  `CliProcessRunner`, `Program\.Main`,
  `IClassFixture<McpLiveRepositoryFixture>`, `Process\.Start` über alle
  3 Dateien): **0/0/0/0/0 Treffer pro Datei**. Damit ist der gesamte
  Batch homogen **Unit** — keine Integration-Klasse. **Wichtigster
  Befund:** die in `codemap.md` (Stand step-002) und im
  `step-005/step-plan.md` §"Notes" (Stand 2026-08-07) explizit
  formulierte **Hypothese, `ListEvalsCommandTests` könnte ein
  Subprozess-Test sein, ist widerlegt** — siehe
  Klassifikations-Heuristik §3 unten.
- **Testmethoden-Inventar** (regex-basiert per
  `Select-String -Pattern '\[Fact\]'` / `'\[Theory\]'` — gemäß
  step-003-Review NITPICK "regex statt manuell zählen" und analog zur
  step-005-Validierung):

  | Datei                                       | `[Fact]` | `[Theory]` |
  |---------------------------------------------|---------:|-----------:|
  | `Evals/EvalAssemblerTests.cs`               |       11 |          0 |
  | `Evals/SpecLoaderTests.cs`                  |       10 |          0 |
  | `Evals/ListEvalsCommandTests.cs`            |        2 |          0 |
  | **Summe**                                   |  **23**  |     **0**  |

  Alle 23 sind `[Fact]`, keine `[Theory]` mit `[InlineData]`-Reihen,
  keine method-level Traits. **Erwartetes Filter-Delta nach
  step-006:** Unit steigt um **+23**, Integration unverändert, Total
  unverändert. Konkret: Unit 332 → **355**, Integration **113**, Total
  **1325** (nachvollziehbar im `step-result.md` zu verifizieren). Das
  ist die **kleinste erwartete Δ** aller bisherigen EPIC-02-Batches
  (zum Vergleich: step-002 +69, step-003 +57, step-004 +50,
  step-005 +54) — passt zur kleinsten Klassen-Anzahl (3).
- **Klassen-Deklarationen — Trait-Platzierungs-Variante** (verifiziert
  per `read` über die 3 Dateien):
  - **Alle 3 Klassen OHNE XML-Doc und OHNE `// @covers`-Marker über
    der Klasse** → die Trait-Platzierung ist die einfachste Variante:
    **direkt zwischen der Leerzeile nach `namespace …;` und
    `public sealed class …`** (= Standard-Edit-Tool-Insert, keine
    Sonderbehandlung). Konkret:
    - `EvalAssemblerTests.cs:20` (`public sealed class … : IDisposable` —
      `IDisposable` wegen `TestTempDirectory` im Konstruktor / Dispose
      Z. 32; passt zur `MaxDirectoryChildrenTests`-`IDisposable`-Variante
      aus step-003 und zur `AnalysisCacheManagerTests`-Variante aus
      step-005; ändert nichts an der Unit-Klassifikation)
    - `SpecLoaderTests.cs:20` (`public sealed class … : IDisposable` —
      gleiche Konstellation wie `EvalAssemblerTests`)
    - `ListEvalsCommandTests.cs:17` (`public sealed class …` ohne
      `IDisposable`, ohne XML-Doc — passt zur `ArchitectureTests`-Variante
      aus step-005)
  - Damit sind **alle 3 Trait-Platzierungen lokal pro Datei behandelbar**;
  keine Datei benötigt eine übergeordnete Sonderbehandlung.
- **EOL- und Trailing-NL-Status** (verifiziert per PowerShell-Byte-Check
  über alle 3 Dateien): **homogen CRLF + Trailing-NL** in allen 3
  Dateien, **0** Solo-LF-Zeilenenden, alle 3 Dateien **ohne BOM**
  (verifiziert per `[System.IO.File]::ReadAllBytes` + Trippel-Check —
  erste 3 Bytes sind **nicht** `EF BB BF`). **Kein gemischter
  EOL-Status** (anders als in step-004, wo `Web/`-Dateien LF/CRLF
  gemischt hatten und einen byte-genauen Python-Helper nötig
  machten), **kein** BOM-Erhalt-Risiko (alle 3 ohne BOM). Der Coder
  kann alle 3 Edits mit dem Standard-Edit-Tool durchführen, ohne
  Diff-Aufblähung befürchten zu müssen. Der Coder verifiziert dies
  vorab per `Get-Content -Encoding UTF8 <file> | Select-Object -Last 1`
  (zeigt das letzte Byte-Zeichen) und durch `git diff` nach dem Edit.
- **`EvalAssemblerTests` Console.SetError-Spezialfall:** zwei
  `[Fact]`-Methoden (`Assemble_LargePrompt_WritesWarningToStdErr` Z.
  106-122 und `Assemble_SmallPrompt_NoWarningOnStdErr` Z. 124-139)
  kapseln `Console.SetError(capture)` / `Console.SetError(originalError)`
  für `Console.Error`-Capture. Das ist **kein** Subprozess, sondern
  in-process `Console.SetError`/`SetOut` (Standard-Pattern für
  stderr-Capture in xUnit-Tests). Negativ-Abgrenzung analog zu
  `MaxDirectoryChildrenTests` step-003 / `AnalysisCacheManagerTests`
  step-005: `Console.SetError` startet keinen Subprozess und
  führt nicht zu `Integration`. Testet das Verhalten von
  `EvalAssembler.Assemble(...)` selbst (stderr-Warning bei großem
  Prompt), nicht einen externen Prozess.
- **`SpecLoaderTests` File-IO + `Path.Combine`:** verwendet
  `File.WriteAllText`, `Directory.CreateDirectory`, `Path.Combine` —
  alles in-process File-IO, kein Subprozess.
- **`ListEvalsCommandTests` TestLintConsole-Mock:** der
  `TestLintConsole`-Mock wird aus `AiNetLinter.Tests.Output`-Namespace
  importiert (siehe Import Z. 4: `using AiNetLinter.Tests.Output;`).
  `TestLintConsole` ist ein in-memory-Mock für `ILintConsole`, der
  in `AiNetLinter.Tests/Output/TestLintConsole.cs` (per
  Konzept-/CodeMap-Notiz) definiert ist. Der Aufruf
  `ListEvalsCommand.Run(console)` ist ein direkter in-process-Aufruf
  der Produktionsmethode mit dem Mock — **kein** `Process.Start`,
  **kein** `dotnet AiNetLinter.dll`-Subprozess. **Damit ist die
  CodeMap-Hypothese "möglicherweise Integration via Subprozess"
  endgültig widerlegt** (siehe Klassifikations-Heuristik §3 unten
  für die ausführliche Begründung).
- **Bündelungs-Begründung (3 Klassen, 1 Ordner):** die 3 Evals-Klassen
  sind die einzigen in diesem Ordner und thematisch eng zusammen
  (alle testen `AiNetLinter.Evals.*`-Produktionscode). Eine
  Aufteilung in 3 Einzel-Step-Planungen wäre reiner Overhead ohne
  Mehrwert. **Vorteile der Bündelung:** (1) **berechtigt durch
  homogenen Charakter** — alle 3 Klassen sind `Unit` ohne
  Subprozess-Marker, einheitliche Heuristik-Anwendung; (2)
  **Klassen-Level-Mix bleibt überschaubar** — keine
  Integration-Klasse im Set, also kein Misch-Heuristik-Diskussion;
  (3) **Trait-Platzierungs-Variante ist einheitlich einfach** — alle
  3 ohne XML-Doc / `// @covers`, nur 2× mit `IDisposable` (analog
  zu step-003/005 behandelt) + 1× ohne (analog zu step-005
  `ArchitectureTests`); (4) **kleinster Batch der EPIC-02-Serie
  (3 Klassen, +23 Unit-Methoden)** — passt gut in den 8-Item-Deckel
  und in den 40-Zeilen-Diff-Deckel; (5) **folgt der step-002-Logik
  "1 Ordner = 1 Batch"** für die kleinen Unit-Ordner.
- **Alternative, verworfen — `Output/` (10 Klassen) als nächster
  alleiniger Step:** wäre ein 10-Item-Batch, der den 8-Item-Deckel
  reißt; müsste vorab in zwei 5er-Batches aufgeteilt werden. Sinnvoller
  in einem eigenen Step (vermutlich `step-007` oder `step-008`).
  `Evals/` (3 Klassen) als 1-Ordner-Batch vorzuziehen, weil (a) genau
  3 Klassen, also kleinster denkbarer 1-Ordner-Batch; (b) thematisch
  in sich geschlossen; (c) der `Output/`-Ordner bleibt für eine
  spätere 2-Batch-Aufteilung frei.

## Intention

Alle 3 Testklassen in `Evals/` mit `[Trait("Category", "Unit")]` auf
Klassen-Ebene versehen. Dieser Step ist der fünfte von N Batches, die
zusammen die EPIC-02-DoD erreichen ("alle ~1000 Tests getraggt"). Er
schließt den `Evals/`-Ordner in einem Schritt vollständig ab und
liefert die fünfte Template-Validierung für die Folge-Batches, **bevor**
diese in die größeren, gemischten Verzeichnisse (`Output/`,
`Configuration/`, `Core/Checkers/`, `Mcp/`, `Commands/`) vorstoßen.

Der Step liefert **drei nennenswerte Befunde**:

1. **Hypothese-Widerlegung als Anti-Loop-Check-Ertrag:** der
   `Evals/`-Eintrag in `codemap.md` trug seit step-002 die offene
   Hypothese "`ListEvalsCommandTests` möglicherweise Integration via
   Subprozess, JIT zu prüfen". Die JIT-Prüfung in diesem Step
   (Subprozess-Marker-Grep 0/0/0/0/0, Datei-Inspektion, Aufruf-
   Mechanik) **widerlegt** die Hypothese: `ListEvalsCommand.Run(console)`
   ist ein direkter in-process-Aufruf mit `TestLintConsole`-Mock, **kein**
   `dotnet AiNetLinter.dll`-Subprozess. Die Hypothese wird im selben
   Step im `codemap.md` durch eine klare positive Aussage ersetzt
   ("alle 3 Unit" + Begründung) — der nächste Planer-Aufruf findet
   damit eine konsistente Karte vor.
2. **Kleinster Batch der EPIC-02-Serie (3 Klassen, +23 Unit-Methoden)**
   — demonstriert, dass auch 1-Ordner-3-Klassen-Batches sinnvoll
   sind (im Gegensatz zu 1-Ordner-1-Klasse, das wäre Overhead).
3. **Heuristik-Bestätigung an einem "kalibrierten" 3-Klassen-Set**
   — `Evals/` enthält die Mischung "1-Ordner-mit-und-ohne-IDisposable"
   (2× mit, 1× ohne) ohne Method-Complexity-Edge-Cases (keine
   `// @covers`, kein XML-Doc, keine bestehenden method-level Traits),
   was den **Standard-Insert** (1 Zeile pro Datei an immer der
   gleichen Position) als robustes Default-Pattern weiter bestätigt.

## Klassifikations-Heuristik für diesen Batch

Die in step-002 dokumentierte und in step-003/004/005 bestätigte
Heuristik wird unverändert übernommen:

1. **Bestehende Traits prüfen.** Im Batch sind 0 method-level
   `[Trait(`-Vorkommen in allen 3 Dateien (verifiziert per
   `Select-String`). Damit gibt es **nichts** zu respektieren oder
   additiv zu ergänzen — reine Klassen-Trait-Inserts.
2. **Subprozess-Marker prüfen.** Im Batch sind 0 Subprozess-Marker
   vorhanden (verifiziert per `Select-String` über `McpTestClient`,
   `CliProcessRunner`, `Program\.Main`,
   `IClassFixture<McpLiveRepositoryFixture>`, `Process\.Start` über
   alle 3 Dateien, 0/0/0/0/0 Treffer). Damit ist **keine** Klasse
   in diesem Batch `Integration`.
3. **Sonst: Unit.** Trifft auf alle 3 Klassen in diesem Batch zu.

**Wichtige Negativ-Abgrenzung** (aus step-002, weiterhin gültig, an
den 3 Kandidaten verifiziert): die folgenden Muster sind **KEIN**
Subprozess und führen nicht zu `Integration`:

- `EvalAssembler.Assemble(...)` + `EvalRegistry.TryResolve(...)` (in
  `EvalAssemblerTests`) — in-process Produktionscode-Aufruf
- `SpecLoader.Load(...)` + `File.WriteAllText` / `Directory.CreateDirectory` /
  `Path.Combine` (in `SpecLoaderTests`) — in-process Produktionscode-
  Aufruf + File-IO
- `ListEvalsCommand.Run(console)` (in `ListEvalsCommandTests`) —
  in-process Produktionscode-Aufruf mit `TestLintConsole`-Mock aus
  `AiNetLinter.Tests.Output`
- `TestTempDirectory` (in `EvalAssemblerTests` und `SpecLoaderTests`) —
  in-process Temp-Verzeichnis-Wrapper, kein Subprozess
- `Console.SetError(capture)` / `Console.SetError(originalError)` (in
  `EvalAssemblerTests.cs:108-122, 127-138`) — in-process
  stderr-Capture, kein Subprozess
- `IClassFixture<…>` — **kein** Vorkommen in den 3 Dateien
  (verifiziert per `Select-String -Pattern 'IClassFixture'`, 0 Treffer)

**Negativ-Befund Schritt 2 konkret für `ListEvalsCommandTests` —
Hypothese aus `codemap.md` step-002 widerlegt:**

Die Hypothese, `ListEvalsCommandTests` sei ein Subprozess-Test
("möglicherweise Integration via Subprozess, JIT zu prüfen"), ist
durch die folgenden Befunde endgültig widerlegt:

1. **Subprozess-Marker-Grep:** `Process\.Start` → 0 Treffer in
   `ListEvalsCommandTests.cs`; `McpTestClient` → 0; `CliProcessRunner` →
   0; `Program\.Main` → 0; `IClassFixture<McpLiveRepositoryFixture>`
   → 0.
2. **Datei-Inspektion (Z. 9-26):** die Klasse importiert
   `AiNetLinter.Commands` (Z. 3) und `AiNetLinter.Tests.Output` (Z. 4
   — für `TestLintConsole`). Die einzige aufgerufene Produktionsmethode
   ist `ListEvalsCommand.Run(console)` (Z. 15, 24), ein direkter
   in-process-Aufruf der statischen Methode mit dem `TestLintConsole`-
   Mock als Parameter. **Kein** `Process.Start`, **kein** `dotnet`-
   Aufruf, **kein** Fixture-Wrapper, **kein** `await Task.Run` o. ä.
3. **Mock-Pattern:** `TestLintConsole` (definiert in
   `AiNetLinter.Tests/Output/TestLintConsole.cs`) ist ein
   in-memory-Mock für `ILintConsole`, der in derselben Process-Domain
   wie der Test läuft und keinen Subprozess startet. Das Pattern
   "Produktionsmethode direkt mit in-memory-`ILintConsole`-Mock
   aufrufen" ist exakt das gleiche wie in
   `MaxDirectoryChildrenTests` step-003 (Unit) und allen anderen
   direkten `*-Command.Run(console)`-Aufrufen in der Testsuite.

Daher ist `ListEvalsCommandTests` eine **eindeutige Unit-Klasse**,
und die CodeMap-Annotation wird im selben Step als widerlegt
markiert (siehe `codemap.md` Z. 100 nach Edit).

**Heuristik-Punkt 5 (neu in diesem Step, Folge auf Punkt 4 aus
step-005 "Klassen-Trait additiv zu bestehenden method-level Traits
bei homogenen Klassen"):** **Hypothese-Auflösungs-Pflicht für
offene "möglicherweise…"-Annotationen in der CodeMap.** Wenn ein
CodeMap-Eintrag eine offene "möglicherweise…"- oder "JIT zu
prüfen"-Annotation trägt und der Step, der den zugehörigen
Code-Bereich anfasst, die Annotation auflösen kann, dann löst er sie
**vor** dem Schreiben des Plans auf (Edit am `codemap.md` mit
klarer "widerlegt in step-NNN" / "bestätigt in step-NNN"-Notiz
plus Sub-Beweis). Begründung: (a) verhindert, dass die Annotation
beim nächsten Planer-Aufruf als geltend angenommen und die JIT-Prüfung
erneut durchgeführt wird (Effizienz); (b) hält die CodeMap
konsistent mit dem verifizierten Code-Zustand (Anti-Loop-Check
kann sich auf den Eintrag verlassen, ohne ihn selbst noch einmal
verifizieren zu müssen); (c) dokumentiert das negative Wissen
"Subprozess *nicht* verwendet" genauso explizit wie das positive
"Unit-Trait gesetzt". **Wichtig:** die Auflösung darf den
Step-Plan nicht aufblähen — sie ist eine **1-Zeilen-Edit** an
`codemap.md` (siehe oben), nicht ein eigener Punkt im DoD.

## Konkrete Änderungen

**Bei `step_type: batch`** (siehe `../spec.md` §10.6): pro Item aus der
`items`-Liste im Frontmatter eine Unterüberschrift.

### item-01: `EvalAssemblerTests` → Unit — `src/AiNetLinter.Tests/Evals/EvalAssemblerTests.cs` (Klassen-Deklaration, Z. 20, mit `: IDisposable`)

- **Was:** Zwischen der Leerzeile nach `namespace AiNetLinter.Tests.Evals;`
  (Z. 18) und `public sealed class EvalAssemblerTests : IDisposable`
  (Z. 20) eine Zeile `[Trait("Category", "Unit")]` einfügen (Z. 19
  wird zur Trait-Zeile, Klassendeklaration rutscht auf Z. 20).
  Keine XML-Doc, kein `// @covers`-Marker vorhanden — daher genügt
  die direkte Standard-Insertion.
- **Warum:** Klasse enthält 11 `[Fact]`-Methoden, die alle
  `EvalAssembler.Assemble(...)` und `EvalRegistry.TryResolve(...)`
  direkt auf in-process `EvalDefinition`-Instanzen aufrufen
  (verifiziert per Datei-Inspektion). Zwei Methoden kapseln
  `Console.SetError(capture)` für stderr-Capture
  (`Assemble_LargePrompt_WritesWarningToStdErr` Z. 106-122,
  `Assemble_SmallPrompt_NoWarningOnStdErr` Z. 124-139) — das ist
  in-process stderr-Capture, kein Subprozess (Negativ-Abgrenzung
  analog zu `MaxDirectoryChildrenTests` step-003). Die `IDisposable`-
  Implementierung (Z. 32: `public void Dispose() => _tempDir.Dispose();`)
  ist nur `TestTempDirectory`-Cleanup — passt zur
  `MaxDirectoryChildrenTests`-`IDisposable`-Variante aus step-003
  und zur `AnalysisCacheManagerTests`-Variante aus step-005 (das
  `IDisposable`-Interface ändert nichts an der Unit-Klassifikation).
  Subprozess-Marker-Grep liefert 0 Treffer. Trait-Wert folgt exakt
  der bestehenden Konvention (`[Trait("Category", "Unit")]`,
  CamelCase-Großbuchstabe).

### item-02: `SpecLoaderTests` → Unit — `src/AiNetLinter.Tests/Evals/SpecLoaderTests.cs` (Klassen-Deklaration, Z. 20, mit `: IDisposable`)

- **Was:** Zwischen der Leerzeile nach `namespace AiNetLinter.Tests.Evals;`
  (Z. 18) und `public sealed class SpecLoaderTests : IDisposable`
  (Z. 20) eine Zeile `[Trait("Category", "Unit")]` einfügen (Z. 19
  wird zur Trait-Zeile, Klassendeklaration rutscht auf Z. 20).
  Keine XML-Doc, kein `// @covers`-Marker vorhanden.
- **Warum:** Klasse enthält 10 `[Fact]`-Methoden, die `SpecLoader.Load(...)`
  direkt auf in-process `string[]`-Argumenten aufrufen
  (verifiziert per Datei-Inspektion; Tests beschreiben Edge-Cases
  wie leere Liste, einzelne Datei, Verzeichnis-Top-Level-MD-Filterung,
  Non-MD-Datei-Ignorierung, Concatenation-Order, Non-Existent-Path-
  Graceful-Skip, XML-Doc-Tag-Wrapping, MD-Separator-Vermeidung,
  Doc-Tag-nur-FileName-Konvention). Konstruktor (Z. 19-31) erzeugt
  via `TestTempDirectory.Create(...)` und `File.WriteAllText` ein
  In-Memory-Temp-Verzeichnis mit `valid.md`/`spec-a.md`/`spec-b.md`
  — in-process File-IO, kein Subprozess. `IDisposable`-Implementierung
  (Z. 33) ist nur `TestTempDirectory`-Cleanup. Subprozess-Marker-Grep
  liefert 0 Treffer. Trait-Wert folgt der Konvention.

### item-03: `ListEvalsCommandTests` → Unit — `src/AiNetLinter.Tests/Evals/ListEvalsCommandTests.cs` (Klassen-Deklaration, Z. 17, ohne `: IDisposable`)

- **Was:** Zwischen der Leerzeile (Z. 16) und
  `public sealed class ListEvalsCommandTests` (Z. 17) eine Zeile
  `[Trait("Category", "Unit")]` einfügen (Z. 16 wird zur
  Trait-Zeile, Klassendeklaration rutscht auf Z. 17). Keine
  XML-Doc, kein `// @covers`-Marker vorhanden, kein
  `IDisposable`-Interface.
- **Warum:** Klasse enthält 2 `[Fact]`-Methoden, die
  `ListEvalsCommand.Run(console)` direkt mit einem
  `TestLintConsole`-Mock aufrufen (verifiziert per Datei-Inspektion
  Z. 9-26). `TestLintConsole` (importiert via
  `using AiNetLinter.Tests.Output;` Z. 4) ist ein in-memory-Mock für
  `ILintConsole`, der in derselben Process-Domain wie der Test
  läuft. **Damit ist die in `codemap.md` step-002/005 notierte
  Hypothese "möglicherweise Integration via Subprozess" widerlegt:**
  die Klasse ist **Unit**, nicht Integration. Subprozess-Marker-Grep
  (`McpTestClient` / `CliProcessRunner` / `Program\.Main` /
  `IClassFixture<McpLiveRepositoryFixture>` / `Process\.Start`)
  liefert 0/0/0/0/0 Treffer. Trait-Wert folgt der Konvention. Diese
  Klassifikation wird in `codemap.md` Z. 100 explizit als
  "Hypothese widerlegt" vermerkt (siehe Anti-Loop-Check oben).

## Tests

Keine — die Klassifikation ist rein additiv (Attribut setzen). Existierende
Tests müssen **unverändert** grün bleiben. Validierung erfolgt über den
vollen `dotnet test`-Lauf in der Definition of Done (kein neuer Test, kein
geänderter Test).

## Definition of Done

- [ ] Alle 3 Items umgesetzt (je eine `[Trait("Category", "Unit")]`-Zeile
      auf Klassen-Ebene, eingefügt zwischen der Leerzeile nach
      `namespace …;` und der `public sealed class …`-Deklaration —
      siehe Aufzählung oben)
- [ ] **Kein BOM-Risiko:** alle 3 Dateien haben **kein** UTF-8-BOM
      (verifiziert per `[System.IO.File]::ReadAllBytes` —
      erste 3 Bytes sind **nicht** `EF BB BF`). **Keine** explizite
      BOM-Erhaltung erforderlich (Standard-Edit-Tool erhält den
      No-BOM-Status ohnehin). Der Coder dokumentiert den
      No-BOM-Status im `step-result.md` (verifiziert per
      `[System.IO.File]::ReadAllBytes` nach Edit).
- [ ] **EOL/Trailing-NL-Konservierung:** alle 3 Dateien behalten
      CRLF-Zeilenenden und Trailing-NL nach dem Edit (verifiziert per
      PowerShell-`Select-String`-Prüfung und/oder `git diff` — keine
      Zeilenende-Änderungen). Bei diesem Batch **kein** byte-genauer
      Python-Helper nötig (anders als in step-004, wo `Web/`-Dateien
      LF/CRLF gemischt hatten), weil alle 3 Dateien uniform CRLF +
      Trailing-NL haben — Standard-Edit-Tool reicht.
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
    **best-effort, ein Lauf grün** (gemäß step-002/step-003/step-004/
    step-005 NITPICK-Linie: pre-existing Flaky-Test
    `McpServerCommandLoadingStateTests.LoadState_LoadFuncCompletesSynchronouslyWithCatalog_ReportsLoadedImmediately`
    flake-t gelegentlich unter Last des Integration-Filters; nicht
    step-006-verursacht, Fix in EPIC-06). Der Coder dokumentiert im
    `step-result.md`, wenn der Lauf flaky ist, und startet ihn ggf.
    einmal neu.
  - **Numerische Plausibilitätsprüfung** (gemäß step-003-Review
    NITPICK "regex statt manuell zählen"): der Coder zählt die
    `[Fact]`/`[Theory]`-Methoden in den 3 Klassen **regex-basiert**
    per `Select-String -Pattern '\[Fact\]'` / `'\[Theory\]'` (NICHT
    manuell durchgehen), dokumentiert die Summe im `step-result.md`
    und vergleicht sie mit dem erwarteten Unit-Filter-Delta.
    **Erwartetes Delta:** Unit steigt um **23** (11+10+2 = 23 Facts,
    0 Theories; 0 bestehende method-level Traits → 23 neu für den
    Unit-Filter; verifiziert per `Select-String` durch den Planer;
    siehe "Aktueller Projektzustand" oben). Integration-Zahl
    bleibt unverändert bei 113. Total bleibt unverändert bei 1325.
    **Erwarteter Unit-Filter-Wert nach step-006: 355** (332 + 23).
- [ ] **Self-Lint** (TD-001-konform, semantisch identisch zu
      `--self-lint`): `dotnet run --project src/AiNetLinter --
      --config rules.json --path .` → muss `OK` ausgeben
- [ ] **Commit auf aktuellem Branch** (Conventional Commit auf Deutsch,
      imperativ, mit Task-Suffix `[flaky-and-test-performance]`):
      **konkreter Subject-Vorschlag** (gemäß TD-002, "kürzere
      Subject-Bodies vorgeben"):
      `test: Evals-Tests Kategorie-taggen [flaky-and-test-performance]`
      → **63 Zeichen** inkl. Suffix (exakt verifiziert per
      `('test: Evals-Tests Kategorie-taggen [flaky-and-test-performance]').Length`
      in PowerShell; deckt 9 Zeichen Sicherheitsabstand zur
      72-Zeichen-Grenze). Pattern spiegelt step-002's `test:
      Suppression-Tests Kategorie-taggen [flaky-and-test-performance]`,
      step-003's `test: Metrics-Tests Kategorie-taggen
      [flaky-and-test-performance]`, step-004's `test: Web-Tests
      Kategorie-taggen [flaky-and-test-performance]` und step-005's
      `test: 4 Unit-Ordner Kategorie-taggen
      [flaky-and-test-performance]` (65 Zeichen) — gleicher Aufbau,
      konsistent zur EPIC-02-Batch-Serie (kürzester Subject der
      Serie, weil kleinster Batch = 1 Ordner / 3 Klassen / 23
      Methoden). **Falls** der Coder den Subject abwandeln will,
      **muss** er 72 Zeichen einhalten und die neue exakte Länge
      im `step-result.md` dokumentieren — bei Überschreitung
      TD-002-Eintrag aktualisieren.
- [ ] `step-006/step-result.md` geschrieben mit: Diff-Statistik
      (Anzahl hinzugefügter Trait-Zeilen pro Item, Gesamt-Diff
      erwartet `3 files changed, 3 insertions(+)`), Test-Ergebnis
      (Gesamt-Lauf + 2 Filter-Läufe mit Test-Zahlen — die per
      `Select-String` regex-basiert verifizierte Summe **23**
      explizit nennen, das **+23**-Delta explizit nennen, mit dem
      tatsächlichen Filter-Delta abgleichen), Build-Output,
      Self-Lint-Output, Commit-Hash, Subject mit exakter
      Längen-Angabe. Pflicht-Block am Ende der Coder-Antwort
      gemäß `AiNetLinterRichtlinien.mdc` §4
      (Commit-Vorschlag-Pflicht — Markdown-Heading der dritten
      Ebene mit dem Text "Commit-Vorschlag" gefolgt vom
      vorgeschlagenen Code-Commit-Block inkl. Subjekt und Body).
- [ ] `status` in `step-plan.md` von `open` auf `in_progress` (durch
      Orchestrator nach Coder-Start) und nach `step-result.md`-Schreiben
      auf `done (pending audit)` (durch Coder) gesetzt.

## Rules-Refs

- `.agents/rules/AiNetLinterRichtlinien.mdc` §4 "Testsuite-Parallelität
  bewahren" — relevant nur als Ausschluss: Trait-Attribute haben
  **keinen** Einfluss auf Parallelismus, nur `[Collection(...)]` /
  `DisableParallelization`. Dieser Step berührt die Parallelität
  nicht, ist also nicht regel-restriktiv hier.
- `.agents/rules/AiNetLinterRichtlinien.mdc` §5 "Sparsame Kommentare" —
  die hinzugefügten Trait-Zeilen sind XML-Attribute, keine Kommentare.
  Kein Bezug.
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
  step-002/step-003/step-004/step-005-Reviews übernommen):
  `McpServerCommandLoadingStateTests.LoadState_LoadFuncCompletesSynchronouslyWithCatalog_ReportsLoadedImmediately`
  flake-t gelegentlich unter Last des `Category=Integration`-Filters.
  Nicht step-006-verursacht (rein additives Attribut, keine
  Logik-/Parallelitäts-Änderung), Fix in EPIC-06. Der Coder
  behandelt den Integration-Filter-Lauf als "best-effort, ein
  Lauf grün" (siehe DoD).
- **Keine method-level Traits zu respektieren** (anders als
  step-005 mit 4 method-level Traits in
  `AnalysisCacheManagerIsolationTests`): alle 3 Evals-Klassen sind
  "jungfräulich" (0 method-level `[Trait(`-Vorkommen in allen 3
  Dateien verifiziert). Damit greift Heuristik-Punkt 4 aus
  step-005 "Klassen-Trait additiv zu bestehenden method-level
  Traits" in diesem Step nicht — es gibt schlicht keine
  bestehenden Traits, zu denen addiert werden müsste.

## Code-Skizze (optional)

Vorher (`EvalAssemblerTests.cs`, Z. 18-22):

```csharp
namespace AiNetLinter.Tests.Evals;

public sealed class EvalAssemblerTests : IDisposable
{
    private readonly TestTempDirectory _tempDir;
    private readonly EvalDefinition _namingDriftEval;
```

Nachher:

```csharp
namespace AiNetLinter.Tests.Evals;

[Trait("Category", "Unit")]
public sealed class EvalAssemblerTests : IDisposable
{
    private readonly TestTempDirectory _tempDir;
    private readonly EvalDefinition _namingDriftEval;
```

Vorher (`ListEvalsCommandTests.cs`, Z. 14-19):

```csharp
namespace AiNetLinter.Tests.Evals;

public sealed class ListEvalsCommandTests
{
    [Fact]
    public void Run_OutputContainsAllEvalNames()
```

Nachher:

```csharp
namespace AiNetLinter.Tests.Evals;

[Trait("Category", "Unit")]
public sealed class ListEvalsCommandTests
{
    [Fact]
    public void Run_OutputContainsAllEvalNames()
```

(`SpecLoaderTests.cs` ist analog zu `EvalAssemblerTests.cs` — gleiche
Konstellation `IDisposable` + `TestTempDirectory` + Trait-Insert
zwischen `namespace …;` und `public sealed class`.)

## Notes

- **Batch-Umfang:** 3 Klassen × je 1 Trait-Zeile ≈ 3 Diff-Zeilen
  (zzgl. Anpassung der Leerzeile bei direktem Insert). Deutlich
  unter dem `max_batch_diff_lines: 40`-Deckel. Erwartete
  Diff-Statistik: `3 files changed, 3 insertions(+)` (wird vom Coder
  im `step-result.md` verifiziert).
- **Schritt-Typ `low`-Risk-Begründung:** rein additives Attribut
  auf Klassen, das weder Build-Verhalten noch Test-Verhalten noch
  Parallelität ändert. Trait-Wert folgt exakt der bestehenden
  100+-Eintrag-Konvention (`Unit`, CamelCase-Großbuchstabe). Kein
  Eingriff in Produktionscode, keine Fixture-Änderung, keine
  Test-Logik-Änderung. **Keine** bestehenden method-level Traits
  zu respektieren (alle 3 Klassen "jungfräulich", verifiziert per
  `Select-String`) — Heuristik-Punkt 4 aus step-005 greift hier
  nicht.
- **EOL/Trailing-NL-Hinweis (vgl. step-004):** beim Lesen der 3
  Zieldateien wurde **homogener** EOL-Status festgestellt — alle
  3 Dateien sind CRLF + Trailing-NL, alle 3 ohne BOM.
  **Anders als in step-004** (wo `Web/` LF/CRLF gemischt hatte und
  einen byte-genauen Python-Helper nötig machte) ist **kein
  EOL-Helper nötig** — Standard-Edit-Tool reicht für alle 3 Edits.
  Der Coder verifiziert vorab und nach dem Edit per
  `git diff --stat` (sollte ≤ 4 Zeilen pro Datei sein) und
  PowerShell-`[System.IO.File]::ReadAllBytes` für die
  No-BOM-Bestätigung (alle 3 ohne BOM, daher keine BOM-Erhaltung
  erforderlich — anders als in step-002/005 mit 3 BOM-Dateien).
- **Heuristik-Punkt 5 (neu) dokumentiert:** Hypothese-Auflösungs-
  Pflicht für offene "möglicherweise…"-Annotationen in der
  `codemap.md` (siehe Klassifikations-Heuristik §"Negativ-Befund
  Schritt 2" oben). Punkt 1-4 sind aus step-002/003/004/005
  unverändert übernommen; Punkt 5 ist neu in diesem Step und
  ist für die Folge-Batches relevant, falls weitere
  "JIT-zu-prüfen"-Annotationen in der CodeMap auftauchen.
- **Bündelungs-Begründung (1-Ordner-Batch):** die 3 Klassen sind
  die einzigen in `Evals/` und thematisch eng zusammen
  (alle testen `AiNetLinter.Evals.*`-Produktionscode). Eine
  Aufteilung in 3 Einzel-Step-Planungen wäre reiner Overhead
  ohne Mehrwert. **Berechtigt durch:** (a) alle 3 Klassen sind
  homogen `Unit` (0 Subprozess-Marker); (b) Klassen-Level-Mix
  ohne Integration-Klasse; (c) Trait-Platzierungs-Variante
  einheitlich einfach (alle 3 ohne XML-Doc, ohne `// @covers`,
  Standard-Insert); (d) `codemap.md` listet `Evals/` als
  zusammenhängenden Eintrag (1 Ordner, 3 Klassen) — passt zur
  step-002-Logik "1 Ordner = 1 Batch" für die kleinen Unit-Ordner.
- **Alternative (verworfen) — `Output/` (10 Klassen) als nächster
  alleiniger Step:** wäre ein 10-Item-Batch, der den 8-Item-Deckel
  reißt; müsste vorab in zwei 5er-Batches aufgeteilt werden.
  Sinnvoller in einem eigenen Step (vermutlich `step-007` oder
  `step-008`).
- **Numerische Vorab-Erwartung an die Filter-Läufe:** aus
  step-005 wissen wir `Category=Unit` = 332 Tests und
  `Category=Integration` = 113 Tests (332+113=445 getaggte
  Methoden aus 1325 Gesamt, 880 ungetaggte Methoden). Nach
  step-006 ist eine **Erhöhung** der `Category=Unit`-Zahl um
  **23** zu erwarten (11+10+2 = 23 Facts, alle 23 bisher
  ungetraggt → 23 neu für den Unit-Filter). Die
  `Category=Integration`-Zahl sollte unverändert bei 113 bleiben.
  `dotnet test` (voller Lauf) sollte weiterhin 1325 Tests zeigen.
  Konkret: Unit 332 → **355**, Integration **113**, Total
  **1325** (vom Coder im `step-result.md` zu verifizieren).
- **Anti-Loop-Check-Nebenertrag dokumentiert:** die in
  `codemap.md` Z. 100 (Stand step-002/005) offene Hypothese
  "`ListEvalsCommandTests` möglicherweise Integration via
  Subprozess, JIT zu prüfen" ist in diesem Step endgültig
  widerlegt und im selben Edit-Pass durch eine klare
  "alle 3 Unit"-Aussage mit explizitem Sub-Beweis ersetzt
  worden (siehe Anti-Loop-Check oben und Klassifikations-
  Heuristik §"Negativ-Befund Schritt 2"). Die `last_updated`-
  Zeile in `codemap.md` ist auf `2026-08-07T13:10:00+02:00`
  fortgeschrieben.
- **Folge-Batches (NICHT in diesem Step geplant — informativ):**
  die EPIC-02-Arbeit umfasst weiterhin ca. 138 verbleibende
  ungetaggte Testklassen nach step-006. Vorschlag für die
  Reihenfolge der nächsten Step-Modus-Aufrufe (rein informativ
  — Planung der einzelnen Folge-Steps ist Sache der jeweiligen
  Planer-Aufrufe, nicht dieses Plans):
  1. **Reine-Unit-Ordner, mittel** (zwischen 5 und 10 Klassen,
     passend für einen 8-Item-Batch):
     - `Output/` (10 Klassen, alle Unit) — vermutlich zwei
       5er-Batches, oder ein 8er-Batch + eigener 2er-Step
     - `Configuration/` (8 Klassen, alle Unit) — 1 Batch
  2. **Reine-Unit-Ordner, groß** (mehrere Batches pro Ordner):
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
  ist **nicht** Teil von step-006, sondern gehört in den letzten
  EPIC-02-Batch oder in den EPIC-08-Abschluss-Validierungs-Step.

## Sonstige Beobachtungen

- **Subject-Länge bestätigt:** der im DoD vorgegebene Subject
  `test: Evals-Tests Kategorie-taggen [flaky-and-test-performance]`
  ist **63 Zeichen** inkl. Suffix (exakt verifiziert per
  `('test: Evals-Tests Kategorie-taggen [flaky-and-test-performance]').Length`
  in PowerShell; deckt 9 Zeichen Sicherheitsabstand zur
  72-Zeichen-Grenze aus `AiNetLinterRichtlinien.mdc` §4 /
  `spec.md` §10.3). Damit ist die TD-002-Disziplin-Variante (a)
  "Planer gibt Subject konkret vor" eingehalten — der Coder
  übernimmt den Subject **unverändert** (analog zu step-005, der
  diese Disziplin erstmalig erfolgreich umgesetzt hat; step-006
  ist der kürzeste Subject der EPIC-02-Serie).
