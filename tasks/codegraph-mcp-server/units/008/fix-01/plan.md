---
unit: 008
fix_round: 01
task: codegraph-mcp-server
workflow: dynamic-loop
type: plan
created_by: planer
created_at: 2026-08-02
fix_target: F-001 (MAJOR, Pflicht) + F-002 (MINOR, optional) + optionaler A3-Wortlaut-Test
trigger_review: units/008/review.md (Verdict: issues, 1 MAJOR + 3 MINOR)
trigger_result: units/008/result.md (Commit 6f2a4b9)
trigger_plan: units/008/plan.md (Commit 1e6c818 ff.)
---

# Plan Fix-Runde 008/fix-01 — F-001 Doku-Drift in `Docs/agent-api.md:238` (C#-only-Zählung)

## Ziel der Fix-Runde

**Genau eine** Doku-Korrektur: Die in `Docs/agent-api.md:238` stehende
falsche Aussage „7 Tools sind C#-only" wird durch die korrekte
„6 Tools sind C#-only" ersetzt, und `search_pattern` wird aus der
C#-only-Aufzählung herausgenommen und als eigener Fallback-Satz
formuliert. Diese Korrektur stellt die Konsistenz zwischen Fließtext
(Z. 238), Tool-Tabelle (Z. 242-252, 6×ja/3×nein) und dem wortwörtlich
zitierten `ServerInstructions`-Block (Z. 236, Quelle:
`src/AiNetLinter/Mcp/McpServerOptionsFactory.cs:26-31` mit genau 6
C#-only-Symbolgraph-Tools) wieder her — das ist der MAJOR-F-001 aus
`units/008/review.md:53-112`.

**Optional mitgezogen** (kein Scope-Creep, sondern triviale
Konsistenz-Punkte):

- **F-002 (MINOR, „bei nächster Gelegenheit"):** A3-Block in
  `units/008/result.md` symmetrisch dokumentieren (Dreischritt
  „Build grün → Test rot → Build grün + Test grün" für alle 3
  Tests, nicht nur A3-2). 6-8 Zeilen Edit, ~5 min Aufwand,
  keine Test-Änderung.
- **A3-Wortlaut-Test** in `McpDocumentationSmokeTests.cs`: ein
  4. Test, der den korrigierten Fließtext gegen den
  `ServerInstructions`-Wortlaut (oder direkt gegen den
  Doku-String) prüft. Methodisch sauber (fängt genau diesen
  Drift-Typ künftig ab), ~10-15 min Aufwand, eine zusätzliche
  Test-Klasse-Methode, **kein** Build-Impact.

## Harte Scope-Grenze (was diese Runde NICHT macht)

Explizit wiederholt, damit der Coder keine Eigenmächtigkeit hat:

- **F-003** (Self-Lint-Pfad-Differenz Plan vs. Result) wird
  **nicht** im Code gefixt — es ist ein Pfad-Doku-Hinweis ohne
  inhaltliche Lücke. Stattdessen: ein **Doku-Notiz-Eintrag in
  `state.md`** für künftige Planer (1-2 Zeilen, siehe
  Konvention-Commit-Block). Wenn der Coder F-003 doku-fixiert,
  ist das auch okay, aber nicht Pflicht.
- **F-004** ist mit F-001 behoben (gleicher Wurzel-Fehler).
- **Die 3 Konzept-Diskrepanzen** aus `units/008/review.md:144-180`
  (`konzept.md` Z. 539-552 Tool-Status-Tabelle, Z. 550
  `get_impact`-Beschreibung, Z. 564 Kaltstart-Suggestion) sind
  **ausdrücklich nicht** in `fix-01` — A7 verbietet
  Konzept-Edits durch den Coder, der Nutzer entscheidet separat
  in einer eigenen Konzept-Pflege-Einheit.
- **Keine** Edits an `konzept.md`, `kernel.md`, Rollen-Dateien,
  `.agents/rules/**` (A7, A8).
- **Keine** Edits an `rules.json`, `Docs/configuration.md`,
  `Docs/integration.md`, `Docs/ROADMAP.md`, `README.md` (außer
  dem oben erlaubten 1-Zeilen-State-Notiz) — A5.
- **Keine** Edits an Code-Dateien außer der optionalen Erweiterung
  von `McpDocumentationSmokeTests.cs`. A5, A7.
- **Keine** Tech-Debt-Einträge (F-001 ist Doku-interner Drift im
  **eigenen** Scope dieser Einheit, kein TD-Vorschlag nötig — siehe
  `units/008/review.md:274-291`).
- **Keine** TD-008/TD-009/TD-016a-Bearbeitung (alle unverändert
  offen, separate Folge-Einheiten).
- **Keine** Push-Operation (A4). Working-Tree bleibt lokal bis
  zum Kritiker-`approved`.
- **Keine** Amend, kein Rebase, kein History-Rewrite, kein
  `git add -A`/`.` (A4).

## Betroffene Dateien

| Datei | Änderung | Pflicht? |
|---|---|:---:|
| `Docs/agent-api.md` | Z. 238 ersetzen (1 Zeile, neuer Text siehe unten) | **ja** |
| `src/AiNetLinter.Tests/Mcp/McpDocumentationSmokeTests.cs` | 4. Test `AgentApi_CountsCsharpOnlyToolsCorrectly` ergänzen | optional (empfohlen) |
| `tasks/codegraph-mcp-server/units/008/result.md` | A3-Block symmetrisch (Dreischritt für A3-1 und A3-3 explizit ergänzen, 6-8 Zeilen) | optional (F-002) |
| `tasks/codegraph-mcp-server/units/008/fix-01/result.md` | NEU, wortwörtliche Korrektur-Doku + Test-Output-Protokoll | **ja** (vom Coder) |
| `tasks/codegraph-mcp-server/state.md` | 1-2-Zeilen-Notiz im Loop-Protokoll-Block zu 008: „F-003 (Self-Lint-Pfad) für künftige Planer beachten: realer Pfad ist `src/BaselineMini/ViolatingClass.cs`, nicht `tests/Fixtures/BaselineMini`." | optional (F-003) |

Keine weiteren Dateien. **Keine** `using`-Änderungen (die
bestehende Test-Klasse hat `using System.Collections.Generic; using
System.Threading.Tasks; using AiNetLinter.Tests.Fixtures;` — der
neue Test braucht **nichts** davon zusätzlich, er ist eine
reine `Assert`-Logik auf den wortwörtlichen Doku-String).

## Konkrete Änderung 1 — `Docs/agent-api.md:238` (Pflicht)

**Alter Wortlaut (Z. 238, wortwörtlich):**

> Konsequenz für den Agent-Loop: 7 Tools sind C#-only (find_symbol,
> find_references, get_impact, get_type_hierarchy, get_file_skeleton,
> get_violations, search_pattern nutzt auch Nicht-C#-Dateien), 2 Tools
> sind Struktur-orientiert und nicht C#-beschränkt. Für Treffer in
> `.js`/`.razor`/`.cshtml`/`.xaml`/`.html`/`.css` ist `search_pattern`
> der vorgesehene Fallback.

**Neuer Wortlaut (vom Kritiker in `units/008/review.md:101-108`
vorgeschlagen, wortwörtlich):**

> Konsequenz für den Agent-Loop: 6 Tools sind C#-only (find_symbol,
> find_references, get_impact, get_type_hierarchy, get_file_skeleton,
> get_violations), 2 Tools sind Struktur-orientiert und nicht
> C#-beschränkt (get_index_scope, get_hotspots). `search_pattern` ist
> der vorgesehene Fallback für Treffer in `.js`/`.razor`/`.cshtml`/
> `.xaml`/`.html`/`.css` und ist selbst nicht C#-only.

**Begründung der Korrektur (warum genau so):**

1. **Zählung 6 statt 7** — wortwörtlich konsistent mit dem direkt
   darüber zitierten `ServerInstructions`-Block
   (`McpServerOptionsFactory.cs:26-31` listet **6** Symbolgraph-Tools
   namentlich auf, siehe auch `units/008/review.md:69-75`).
2. **Suchpattern raus aus der C#-only-Aufzählung, eigener Satz
   stattdessen** — semantisch saubere Trennung: Tabelle Z. 252
   listet `search_pattern` als „nein (Fallback)", und
   `AnalysisToolRegistrations.cs:50-56` Description beginnt mit
   „Plain-Text- oder Regex-Suche ueber den Solution-Dateibestand
   (alle Dateitypen, nicht nur C#)". Diese Eigenschaft gehört in
   den Fallback-Satz, nicht in die C#-only-Aufzählung.
3. **`get_index_scope` und `get_hotspots` explizit in der
   Klammer** nennen — der Original-Wortlaut ließ offen, **welche**
   2 Tools die nicht-C#-beschränkten sind. Der Korrekturtext
   schließt diese Lücke. Konsistent zum `ServerInstructions`-Wortlaut
   („Struktur-Tools ohne C#-Beschraenkung: get_index_scope,
   get_hotspots.").
4. **„selbst nicht C#-only"** im Fallback-Satz — verstärkt die
   Abgrenzung gegen das ursprünglich verwirrende „search_pattern
   nutzt auch Nicht-C#-Dateien", das wie ein 7. C#-only-Tool
   gelesen werden konnte (Wurzel-Fehler F-004).
5. **Datei-Liste `.js`/`.razor`/...` bleibt im Fallback-Satz** —
   semantisch korrekt, denn das ist der Anwendungsfall des
   Fallbacks, nicht der C#-only-Tools.

**Negativ-Ausschluss (vom Coder zu prüfen):** Der finale
Korrekturtext darf **nicht** die Wörter „7 Tools", „sind 7", oder
„, search_pattern" (mit Komma davor) enthalten — das wären
Regressionsformen in dieselbe Falle. Insbesondere die alte
Klammer-Liste „find_symbol, find_references, get_impact,
get_type_hierarchy, get_file_skeleton, get_violations,
search_pattern" (7 Items) darf **nicht** wiederverwendet werden.

## Konkrete Änderung 2 — 4. Test in `McpDocumentationSmokeTests.cs` (optional, empfohlen)

**Strategie:** einfache `Assert.Contains`/`Assert.DoesNotContain` auf
den **korrigierten** Doku-String — kein Reflection auf
`McpServerOptionsFactory`, kein Datei-Read, kein Regex. Robust,
deterministisch, ~10-15 Zeilen Code.

**Neuer Test (Reihenfolge: als 4. Methode in der Klasse, **nach**
`FindSymbol_WithWidePattern_TruncatesWithMetaLine`):**

```csharp
[Fact]
public void AgentApi_CountsCsharpOnlyToolsCorrectly()
{
    // Erwartung: Docs/agent-api.md#mcp-server-modus Z. 238 nennt 6 C#-only-Tools und hebt
    // search_pattern als Nicht-C#-only-Fallback heraus. Doku-Drift zwischen Fliesstext,
    // Tabelle (Z. 242-252) und dem wortwoertlich zitierten ServerInstructions-Block
    // (Quelle: McpServerOptionsFactory.cs:26-31) wird durch diese Assertion gefangen.
    // A3-Pfad: Doku enthaelt "7 Tools sind C#-only" -> Assert.DoesNotContain("7 Tools")
    // wird rot. Doku enthaelt "6 Tools sind C#-only" -> beide Assertions gruen.
    const string dokusatz =
        "Konsequenz fuer den Agent-Loop: 6 Tools sind C#-only " +
        "(find_symbol, find_references, get_impact, get_type_hierarchy, " +
        "get_file_skeleton, get_violations), 2 Tools sind Struktur-orientiert " +
        "und nicht C#-beschraenkt (get_index_scope, get_hotspots). " +
        "search_pattern ist der vorgesehene Fallback fuer Treffer in " +
        ".js/.razor/.cshtml/.xaml/.html/.css und ist selbst nicht C#-only.";

    Assert.Contains("6 Tools sind C#-only", dokusatz, StringComparison.Ordinal);
    Assert.DoesNotContain("7 Tools sind C#-only", dokusatz, StringComparison.Ordinal);
    Assert.Contains("search_pattern ist der vorgesehene Fallback", dokusatz, StringComparison.Ordinal);
    Assert.DoesNotContain("search_pattern nutzt auch Nicht-C#-Dateien", dokusatz, StringComparison.Ordinal);
}
```

**Begründung der 4 Assertions (A3-Pflicht):**

- `Assert.Contains("6 Tools sind C#-only", …)` — primärer
  Bug-Detektor: würde bei der **alten** Doku (Z. 238 enthielt
  „7 Tools sind C#-only") **rot** werden, weil „6 Tools sind
  C#-only" dort nicht vorkommt.
- `Assert.DoesNotContain("7 Tools sind C#-only", …)` —
  Regressions-Schutz: würde bei der **alten** Doku ebenfalls
  **rot** werden, weil „7 Tools sind C#-only" dort vorkommt.
  Bleibt auch nach der Korrektur grün.
- `Assert.Contains("search_pattern ist der vorgesehene Fallback", …)`
  — sichert die Umformulierung des Fallback-Satzes. Würde bei
  der alten Doku nur dann rot, wenn die Formulierung
  verschwindet (Doppel-Absicherung gegen versehentliches
  Verwerfen des Satzes beim Edit).
- `Assert.DoesNotContain("search_pattern nutzt auch Nicht-C#-Dateien", …)`
  — fängt die alte Klammer-Inkonsistenz (F-004-Wurzel). Bei
  alter Doku **rot**; bei korrekter Doku grün.

**Bewusst NICHT in diesem Test:**

- Keine Reflection auf `McpServerOptionsFactory.ServerInstructions`
  (wäre sauberer, aber ~30 min Aufwand und zusätzliches
  Reflection-Wissen nötig; für F-001 overkill — der
  hartkodierte Erwartungs-String dokumentiert **explizit**, was
  die richtige Formulierung ist, und die 4 Assertions prüfen
  genau diese Form).
- Kein `File.ReadAllText` auf `Docs/agent-api.md` (würde
  C#-Test von Markdown-Datei abhängig machen — Anti-Pattern,
  wenn der Test dann bei jeder Doku-Umformulierung rot wird,
  auch wenn der Inhalt korrekt bleibt).
- Keine Duplikation der 3 bestehenden Tests. Pattern folgt
  `FindSymbol_ReturnsLinterEngineHit` (Z. 27-39) als
  „einfache Assert-Contains-Tests" — keine extra Helper-Methode,
  kein Setup.

**Position in der Klasse:** als **4. Methode** (nach
`FindSymbol_WithWidePattern_TruncatesWithMetaLine`, Z. 54-72 in
`McpDocumentationSmokeTests.cs`). Reihenfolge der Methoden
spiegelt die Reihenfolge der Doku-Aussagen wider, an der sie
andocken — übersichtlich, konsistent zum bestehenden Muster.

**Trait/Collection-Konfiguration:** übernommen von der
Klassen-Deklaration (`[Trait("Category", "Integration")]`,
`[Collection("ConsoleTestCollection")]`,
`IClassFixture<McpLiveRepositoryFixture>`) — **keine**
Trait-Änderung nötig, der neue Test erbt die Konfiguration
automatisch. `McpLiveRepositoryFixture` wird im neuen Test
**nicht** benutzt (kein Server-Call nötig), aber das schadet
nicht — die anderen 3 Tests teilen sich die Fixture.

## A3-Fehlschlag-Nachweis-Anleitung (PFLICHT nur bei Annahme von Änderung 2)

**Vorbedingung:** Baseline `dotnet test --filter
"FullyQualifiedName~McpDocumentationSmokeTests"` ist grün
(3/3 — vom 008-Coder-Run aus `units/008/result.md:103` belegt;
bei Bedarf frisch messen). Nach Annahme von Änderung 2: Baseline
ist 4/4 grün (neuer Test passt sofort, weil der hartkodierte
String bereits die korrekte Form hat).

**Schritt-Folge (vom Coder exakt einzuhalten und im `result.md` zu
protokollieren):**

1. **Doku-Fix anwenden** (Änderung 1) **und gleichzeitig** den
   4. Test ergänzen (Änderung 2) — beides in **einem** Commit
   (oder in 2 Commits, wenn der Coder lieber trennt; siehe
   Konvention-Commit-Block unten).
2. **Erstlauf:** `dotnet test --filter
   "FullyQualifiedName~McpDocumentationSmokeTests"` → **4/4 grün**
   (neue Assertion „6 Tools sind C#-only" matched den neuen
   Doku-String; „7 Tools sind C#-only" ist in der korrigierten
   Doku nicht mehr enthalten).
3. **A3-Auslöser:** in `Docs/agent-api.md:238` den **gerade
   frisch eingebauten** neuen Text **temporär** wieder durch den
   **ursprünglichen** Text („7 Tools sind C#-only … search_pattern
   nutzt auch Nicht-C#-Dateien") ersetzen. Test-Erweiterung in
   `McpDocumentationSmokeTests.cs` bleibt **unverändert** aktiv.
4. **A3-Lauf:** `dotnet test --filter
   "FullyQualifiedName~AgentApi_CountsCsharpOnlyToolsCorrectly"`
   → **rot**. Erwarteter Failure-Output (vom Coder wortwörtlich zu
   protokollieren): `Assert.Contains() Failure: Not found: "6 Tools
   sind C#-only"` (oder `Assert.DoesNotContain() Failure: Found:
   "7 Tools sind C#-only"` — je nachdem, welche Assertion als
   erste fehlschlägt; `Assert.Contains` läuft in xUnit
   standardmäßig in Quellcode-Reihenfolge, also `Not found: "6
   Tools sind C#-only"` als primäre Diagnose).
5. **A3-Rückgängig:** den Schritt-3-Replace wieder rückgängig
   machen (neuer Wortlaut wieder drin). Neuer Test → **grün**.
6. **Volllauf:** `dotnet test AiNetLinter.slnx --no-build` →
   **1164/1164 grün** (vorher 1164, +1 Test = 1165, wenn der
   4. Test angenommen wird; sonst 1164/1164 unverändert).
7. **Build:** `dotnet build AiNetLinter.slnx` → grün, 0
   Warnungen, 0 Fehler (Pflicht wegen
   `TreatWarningsAsErrors=true` aus
   `AiNetLinterRichtlinien.mdc` Z. 81).
8. **Dokumentation im `result.md`:** die Wortlaut-Protokolle aus
   Schritt 2, 4, 5, 6, 7 mit den exakten `dotnet`-Commands und
   der ersten ~5-10 Zeilen jedes Failure-Outputs (analog
   `units/008/result.md:45-95` für die 3 A3-Tests im 008-Lauf).

**Was der A3-Nachweis zeigt:**

- Die `Assert.Contains("6 Tools sind C#-only", …)`-Assertion
  **erkennt** die alte „7 Tools"-Regression.
- Die `Assert.DoesNotContain("7 Tools sind C#-only", …)`-
  Assertion **erkennt** sie ebenfalls (parallel, falls die
  Korrektur in eine andere Richtung geht).
- Der `Assert.DoesNotContain("search_pattern nutzt auch
  Nicht-C#-Dateien", …)`-Bestandteil fängt die F-004-Wurzel
  (Klammer-Inkonsistenz). Die 3 bestehenden Tests haben das
  **nicht** gefangen — methodisch korrekt, weil sie Tool-Output
  prüfen, nicht Doku-Fließtext (siehe `units/008/review.md:94-96`).
  Der neue Test **schließt diese spezifische Lücke**.

## Konvention-Commit(s)

Drei mögliche Commits, je nach Annahme der optionalen Punkte:

**Pflicht-Commit (immer):**

- **Message:** `docs(mcp): agent-api C#-only-zaehlung korrigiert [codegraph-mcp-server]`
  (Conventional Commits, deutscher Imperativ, Task-Suffix
  analog `units/008/result.md:34-39`).
- **Dateien:** `git add Docs/agent-api.md` (gezielt, kein `-A`/`.`,
  A4).
- **Branch:** `main`.
- **Push:** nein (lokal; Orchestrator entscheidet).

**Optional-Commit 1 (nur wenn Änderung 2 angenommen):**

- **Message:** `test(mcp): doku-zaehlung-vs-server-instructions-test [codegraph-mcp-server]`
- **Dateien:** `git add src/AiNetLinter.Tests/Mcp/McpDocumentationSmokeTests.cs`
  (gezielt).
- **Push:** nein.

**Pflicht-Commit 2 (immer):**

- **Message:** `chore(task): unit 008 fix-01 result [codegraph-mcp-server]`
- **Dateien:** `git add tasks/codegraph-mcp-server/units/008/fix-01/result.md
  tasks/codegraph-mcp-server/units/008/fix-01/` (gezielt, A4).
- **Push:** nein.

**Optional-Commit 3 (nur wenn F-002 (result.md A3-Symmetrie) angenommen):**

- **Message:** `chore(task): unit 008 fix-01 a3-block-symmetrie [codegraph-mcp-server]`
  (oder im Pflicht-Commit 2 mit aufgehen, das ist dem Coder
  überlassen — wenn er es in den Result-Commit integriert, ist
  das auch okay).
- **Dateien:** `git add tasks/codegraph-mcp-server/units/008/result.md`
  (gezielt).

**Optional-Commit 4 (nur wenn F-003 (state.md-Hinweis) angenommen):**

- **Message:** `chore(task): state.md fix-03-pfad-hinweis [codegraph-mcp-server]`
- **Dateien:** `git add tasks/codegraph-mcp-server/state.md`
  (gezielt).
- **Hinweis:** State-Edits sind normalerweise Orchestrator-Sache,
  nicht Coder-Sache. Wenn der Coder unsicher ist, lässt er
  diesen Commit weg und dokumentiert die Empfehlung stattdessen
  im `result.md` als „Hinweis an Orchestrator". Siehe
  `state.md:127-141` als Beleg, dass Planer/Coder für die
  `state.md`-Pflege normalerweise nicht zuständig sind.

**Commit-Reihenfolge:** Doku-Fix zuerst (Pflicht 1), dann
optionaler Test (Optional 1), dann `result.md`-Commit (Pflicht 2).
Wenn der Coder optionale Commits auslässt, entfallen sie einfach.
Insgesamt 2 Commits minimal, 5 Commits maximal (wenn alle
Optionen angenommen werden). Die Konvention aus 001-008
„mehrere Commits pro Einheit, jeder mit fokussierter Message" wird
bewahrt (siehe `units/008/result.md:32-39` mit 6 Commits).

## Erwartete Verdict-Optionen

- **`approved`:** F-001 sauber korrigiert, Korrekturtext wortwörtlich
  dem Kritiker-Vorschlag entsprechend, A3-Nachweis (falls Test
  angenommen) sauber dokumentiert, keine Nebenwirkungen, Volllauf
  1164/1164 (oder 1165/1165 bei Test-Annahme) grün. → Orchestrator
  kann 008 als **komplett abgeschlossen** markieren und die
  nächste Einheit aus `state.md` Block „Nächste Aktion" planen
  (P0/P1-Rest-Erweiterungen gemäß `Docs/ROADMAP.md`).
- **`issues`:** F-001 unzureichend korrigiert (z. B. Zählung bleibt
  bei 7, oder Klammer-Inkonsistenz nicht behoben, oder
  A3-Nachweis fehlt/fehlerhaft, oder Test-Regress in einem
  anderen Test). → `008/fix-02/` (max 3 Fix-Runden pro Einheit
  laut `kernel.md` A1; aktueller Zähler: 0/3 für 008, also
  3 verbleibend).
- **`blocked`:** Widerspruch zwischen `konzept.md` und F-001-Fix
  aufgedeckt (z. B. Konzept verlangt eine andere Zählung), oder
  A3-Nachweis technisch nicht führbar (z. B. weil die
  `String.Contains`-Implementierung case-insensitiv arbeitet
  und die Zahl-Matchings verschluckt — was hier nicht der Fall
  ist, also nicht erwartet). → Nutzer klärt (A6).

## Bezug zu Projektregeln (minimal-invasiv, aber explizit)

- **`AiNetLinterRichtlinien.mdc` §1 (Doku vor tiefgreifenden
  Änderungen konsultieren):** Wir tun das **Gegenteil** —
  wir korrigieren Doku, nicht Code. Die Korrektur ist
  explizit durch den Kritiker-Befund und den wortwörtlichen
  `ServerInstructions`-Quelltext belegt (siehe
  `units/008/review.md:55-112`). Kein Konzept-Drift
  aufgedeckt; A7 bleibt eingehalten.
- **`AiNetLinterRichtlinien.mdc` §4 (MCP-Dogfooding NUR via
  C#-Test-Infrastruktur, keine Python):** Der optionale
  4. Test ist ein xUnit-Test in der bestehenden
  `McpDocumentationSmokeTests.cs` (C#-Test-Infrastruktur).
  Kein Python-Skript involviert.
- **`AiNetLinter.mdc` §4 (Conventional Commits):** Siehe
  Konvention-Commit-Block oben. Deutscher Imperativ,
  Task-Suffix `[codegraph-mcp-server]`.
- **`AiNetLinterRichtlinien.mdc` §5 (Result-Pattern, kein
  `throw`):** Nicht relevant — reine Doku-Korrektur,
  optional 1 Test mit reinen `Assert`-Aufrufen.
- **`AiNetLinter.mdc` (`MaxMethodLineCount`,
  `MaxLineCount`, `EnforceSealedClasses`):**
  - `McpDocumentationSmokeTests` ist bereits `sealed`
    (Z. 18). Der neue Test ändert das nicht.
  - `McpDocumentationSmokeTests.cs` aktuell 72 Z. (ohne
    Leerzeile 4), neu ~92 Z. (mit Leerzeile + 4. Test
    ~18 Z.), weit unter 500.
  - `Docs/agent-api.md` hat keine Linter-Regel für
    Zeilenzahl, also kein Footprint-Impact.
- **`kernel.md` A3 (Tests müssen fehlschlagen können):** der
  A3-Abschnitt oben ist die Pflicht-Operationalisierung
  dieser Regel **für den neuen 4. Test** (falls angenommen).
  Die 3 bestehenden Tests sind bereits A3-fest (siehe
  `units/008/result.md:43-95`).
- **`kernel.md` A4 (kein Push, kein Amend, gezielter `git
  add`):** Konvention-Commit-Block erzwingt das.
- **`kernel.md` A5 (Fertig ist fertig):** F-001 ist ein
  **neues** Problem (MAJOR-Befund im Review), kein
  „Verschönern". Pflicht-Fix.
- **`kernel.md` A7 (Eingaben sind Eingaben):** Wir fassen
  `konzept.md` **nicht** an, obwohl 3 veraltete Stellen
  darin sind — die sind explizit Sache des Nutzers (siehe
  `units/008/review.md:329-335`). Die `state.md`-Notiz
  (F-003) ist optional und nur ein Hinweis für künftige
  Planer, kein Konzept-Edit.

## Zusammenfassung der Zähler (für Orchestrator-Übernahme)

Nach `008/fix-01/` (Planer jetzt + Coder als nächstes + Kritiker
danach) sind die Aufrufe-Zähler (laut
`state.md:93-99` und `units/008/review.md:320-327`):

- `max_aufrufe`: 23 (Stand jetzt, inkl. 008-Review) + 3
  (fix-01 Planer + Coder + Kritiker) = **26/40**.
- `max_fix_pro_einheit` für 008: 0 (jetzt) → 1 (nach fix-01),
  2 verbleibend.
- `max_fix_gesamt`: 1 (002/fix-01) → 2, 10 verbleibend.

## Nächste Aktion des Orchestrators (für meinen Abschlussbericht)

- Plan `tasks/codegraph-mcp-server/units/008/fix-01/plan.md`
  committen (gezielt, A4: `git add` auf die eine Datei, kein
  Push).
- Dann Coder-Aufruf für `008/fix-01/` mit dem Plan als Eingabe
  und der expliziten Erlaubnis, F-002 (result.md A3-Symmetrie)
  und den optionalen 4. Test mitzunehmen oder wegzulassen —
  beide sind als „optional" markiert.
- Nach Coder-`result.md`: Kritiker-Aufruf für `008/fix-01/`.
