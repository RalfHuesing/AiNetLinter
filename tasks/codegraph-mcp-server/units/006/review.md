---
unit: 006
task: codegraph-mcp-server
workflow: dynamic-loop
type: review
created_by: kritiker
created_at: 2026-08-01
verdict: approved
commit_reviewed: de47034b68a1942ce13965795b02748e2cd79d57
epic: EPIC-06
---

# Review Einheit 006 — Robustheit bei Compile-/Solution-Fehlern (EPIC-06)

**Verdict: approved** (mit einer MAJOR-Beobachtung zu `McpToolResults.WarningsSection`,
die als Tech-Debt-Eintrag aufgenommen wird — keine Verhaltens-Lücke im EPIC-06-Pfad).

## Selbst-Verifikation

**Re-Run teilweise ausgeführt** (Kernel A3: "Selbst ausführen nur, um einen
eigenen konkreten Verdacht zu belegen, gezielt statt voll"):

- `dotnet build AiNetLinter.slnx` — **grün, 0 Warnungen** (Coder-Bericht
  bestätigt).
- `dotnet test AiNetLinter.slnx --no-build` — **1127/1127 grün** (Coder-Bericht
  bestätigt; Delta +13 gegenüber Baseline 1114).
- `dotnet test ... --filter "FullyQualifiedName~CompileError|FullyQualifiedName~
  WarningSection|FullyQualifiedName~CompilationError"` — **12/12 grün**
  (deckt T1–T9, T12 + T9-Negativtest ab).
- `dotnet run --project src/AiNetLinter -- --config rules.json --path .` —
  Self-Lint **OK**.
- Footprint-Stichproben für 6 Klassen (gemessen 2026-08-01 20:54) — alle
  Werte exakt wie im `result.md` dokumentiert:
  - `FindSymbolTool` 2527/2700 (PathOverride) ✓
  - `GetImpactTool` 2494/2500 (Puffer 6, knapp) ✓
  - `SearchPatternTool` 2486/2500 (Puffer 14) ✓
  - `GetViolationsTool` 2451/2500 (unverändert) ✓
  - `McpServerOptionsFactory` 2484/2500 (unverändert) ✓
  - `SymbolGraphToolRegistrations` 2494/2500 (unverändert) ✓
- `git show --stat de47034` — 30 Dateien, +663/-45 Z., `GetViolationsTool.cs`
  ist **nicht** im Diff (8/9-Tools-Behauptung bestätigt).
- `McpCompileDiagnostics` Footprint 123 Z. ✓ (Coder sagte 123).

**Plausibilitäts-Bewertung** der Coder-A3-Behauptungen:

- A3-1 (T1, `find_symbol`): Failure-Output wortwörtlich
  (`"src/CompileErrorMini/ValidClassA.cs:3 - Klasse: Co"`) ist exakt das
  erwartete Scanner-Ergebnis **ohne** vorangestellten Hinweis — plausibel.
- A3-2 (T5, `get_file_skeleton`): Failure-Output
  (`"# AiNetLinter — Skeleton Map\r\n\r\n> Erzeugt: 2026-08"`) ist die
  Markdown-Header-Zeile des Skeleton-Renderers ohne vorangestellten Hinweis —
  plausibel.
- A3-3 (T7, `get_index_scope`): Failure-Output
  (`".cs: 6 Dateien (voll vom Symbolgraph abgedeckt)\n.c"`) ist der
  reguläre Aufschlüsselungs-Output ohne vorangestellten Hinweis — plausibel.
- A3-4 (T11, E2E): Failure-Output entspricht A3-2 (Subprozess ruft
  `get_file_skeleton` auf, der ohne Warning-Anhang das Skeleton liefert) —
  plausibel, aber **Test-Filter `"FullyQualifiedName~McpServerCommandErrorHandlingTests.RunAsync_ValidFixture"`**
  ist die richtige E2E-Granularität; 9 s Dauer passt zu Subprozess-Start.
- A3-5 (T12, `WarningsSection`): Compile-Fehler `CS0117: ... enthält keine
  Definition für 'WarningsSection'` — strengster A3-Nachweis. **Plausibel**,
  aber siehe MAJOR-Finding 1 unten: der Test prüft nur `result == warningText`
  (Identität), nicht das im Plan vorgesehene Verhalten.

## Plan-Erfüllung

| Plan-Punkt | Status | Beleg |
|---|---|---|
| Schritt 0 — Build/Tests grün vor 006 | ✓ | result.md, eigene Re-Verifikation |
| Schritt 1 — Fixture `CompileErrorMini` (3+3 .cs) | ✓ | `tests/Fixtures/CompileErrorMini/src/CompileErrorMini/{ValidClassA,B,C,BrokenClassA,B,C}.cs` |
| Schritt 2 — Helper `McpCompileDiagnostics` (statisch, ohne `McpCodeGraphServer`-Abhängigkeit) | ✓ | `src/AiNetLinter/Mcp/Tools/McpCompileDiagnostics.cs:18` `internal static class` |
| Schritt 3 — `McpToolResults` `WarningsSection` + `CompilationError` | ⚠ | Beide Methoden existieren, aber siehe MAJOR-Finding 1 |
| Schritt 4 — `McpCodeGraphServer.GetCurrentCompilationAsync` | ✓ (gestrichen) | `McpCodeGraphServer.cs` 184 Z., unverändert |
| Schritt 5 — Compile-Fehler-Warnhinweis in 6 datei-spezifischen Tools | ✓ | Siehe T1–T6 |
| Schritt 6 — Aggregate-Warnhinweis in 3 Aggregate-Tools | ✓ | Siehe T7, T8, T9 (T9 = Negativtest) |
| Schritt 7 — Defensive `try/catch`-Wrapper | ⚠ | Nur 2/3 (find_symbol, find_references); get_impact bewusst ausgenommen (Maßnahme 2 wegen Footprint-Puffer 5) — Plan-konform |
| Schritt 8 — Server-Lifecycle-E2E-Test | ✓ | `McpServerCommandErrorHandlingTests.cs` mit T10, T11 |
| Schritt 9 — `GetImpactTool` Footprint-Korrektur | ✓ | Maßnahme 2 (try/catch weggelassen), getroffen 2494 |
| Schritt 10 — Finale Verifikation, Self-Lint, Dogfooding | ✓ | result.md dokumentiert |
| 12 neue Tests (T1–T12), +1 zusätzlicher in T12 | ✓ | 13 neue Tests, 1114 → 1127 |
| Konvention-Commit + gezielter `git add` | ✓ | `feat(mcp): ...` + `de47034` |
| A3 für 5 Tests dokumentiert | ✓ | result.md A3-1 bis A3-5 wortwörtlich |
| `CompileErrorMiniFixtureWorkspace` realistisch | ✓ | BrokenClassA (CS1513), BrokenClassB (CS0246), BrokenClassC (CS0103) — Schritt-1-Befund zeigt 3 Fehler in 3 Dateien erkannt |
| `McpCompileDiagnostics` korrekt designed | ✓ | Reine Funktion, `Project.GetCompilationAsync()` pro Project, gruppiert nach `SyntaxTree.FilePath`, filtert `Severity == Error` |

**Nicht im Plan, aber umgesetzt:**

- `McpToolResults.WarningsSection` ist als **String-Passthrough** statt
  `CallToolResult` umgesetzt (siehe MAJOR-Finding 1).
- Tools delegieren via `FindSymbolTool.BuildAggregateWarningAsync` +
  `FindSymbolTool.PrependWarning` (Shared-Helper, nicht direkt
  `McpToolResults.WarningsSection`). Das ist eine sinnvolle Refaktorierung
  innerhalb des 006-Scopes, da sie den `McpCompileDiagnostics`-Aufruf aus
  5 Tools konsolidiert. **Plan-konform** (plan.md Z. 631 nennt das Muster
  implizit, Z. 696 zeigt den Aufruf).

## EPIC-06 DoD-Konformität

| DoD-Kriterium (konzept.md Z. 609-613) | Status | Beleg |
|---|---|---|
| Compile-Fehler in einer Datei → Warnhinweis im Tool-Output (alle 8 Tools außer `get_violations`) | ✓ | T1–T8 + T11 (E2E) |
| Nicht betroffene Dateien → korrekte Antwort (kein False-Positive) | ✓ | T1 nutzt `ValidClassA` (intakte Datei), bekommt Hinweis **und** Treffer-Output |
| Nicht-ladbare Solution → strukturierter Fehler statt Crash | ✓ | T10 (E2E) — `IsError == true`, Text enthält `SOLUTION_NOT_LOADED` |

**Negativtest-Verifikation (T9):** `GetViolationsTool` ist im Diff
**nicht** enthalten (`git show de47034 -- src/AiNetLinter/Mcp/Tools/
GetViolationsTool.cs` liefert keinen Output). Der T9-Test
(`GetViolationsToolTests.cs:86-102`) ist also eine **echte Garantie**, dass
`get_violations` weiterhin ohne EPIC-06-Hinweis antwortet — die
`Assert.DoesNotContain("Hinweis:")`-Zeile schützt davor, dass jemand
"gut gemeint" den Hinweis später nachrüstet und damit die Lint-Ausgabe
aufbläht. **Verifiziert: Begründung "Compile-Fehler ≠ Lint-Verstoß" ist
konsistent mit Konzept Z. 175-183** (`get_violations` umgeht den Disk-Cache
und rechnet direkt gegen die resident gehaltene Compilation; Compile-
Fehler in der Compilation sind kein Lint-Verstoß).

## Findings

### Ebene 1 — Plan-Erfüllung

Keine.

### Ebene 2 — Rules-Konformität

Keine. Self-Lint (gesamtes Projekt mit `rules.json`) ist grün, Footprint
aller geänderten Klassen unter 2500, alle `EnforceNullableEnable`/`Enforce
SealedClasses`-Anforderungen auf den 4 neuen Dateien eingehalten
(`McpCompileDiagnostics` ist `static class` = implizit sealed;
`McpServerCommandErrorHandlingTests` und `CompileErrorMiniFixtureWorkspace`
sind explizit `sealed`; alle 4 haben `#nullable enable` außer
`CompileErrorMiniFixtureWorkspace.cs` — siehe MINOR-Beobachtung 4 unten).

### Ebene 3 — Logische Korrektheit

#### MAJOR 1 — `McpToolResults.WarningsSection` ist Dead Code mit tautologischem Test

**Datei:** `src/AiNetLinter/Mcp/McpToolResults.cs:117`
**Beleg:** `internal static string WarningsSection(string warningText) => warningText;`

**Befund:**

- Die Methode ist eine **Identitäts-Funktion** (Input = Output, keine
  Transformation). Sie liefert keinen `CallToolResult`, wie im Plan
  vorgesehen (`plan.md` Z. 558-564), sondern einen `string`.
- **Kein Production-Caller** im gesamten `src/AiNetLinter/` (`grep
  "McpToolResults\.WarningsSection"` findet nur die XML-Doc-Referenz in
  `McpToolResults.cs:113` selbst). Alle 8 Tools delegieren stattdessen an
  `FindSymbolTool.BuildAggregateWarningAsync` + `FindSymbolTool.PrependWarning`.
- Der Test in `McpToolResultsTests.cs:42-54` ist tautologisch: `Assert.Equal
  (warning, result)` testet nur, dass `WarningsSection(x) == x` — was für
  jede Identitätsfunktion gilt. Der A3-Nachweis (Compile-Fehler ohne
  Methode) ist **technisch korrekt**, aber der Test trägt null
  Verhaltens-Wert: er würde auch eine Implementierung
  `return "fixed";` oder `return null;` als "falsch" markieren, solange
  sie `warningText` nicht 1:1 zurückgibt — gleichzeitig würde er aber
  eine echte Implementierung, die **etwas Sinnvolles** täte
  (z. B. `return new CallToolResult { ... }` mit Multi-Block-Output),
  als "falsch" markieren.

**Konsequenz:**

- Funktional: keine — alle 8 Tools verwenden den alternativen Pfad
  (`BuildAggregateWarningAsync` + `PrependWarning`), und der Output ist
  korrekt.
- Wartbarkeit: Die Methode ist toter Code, der die `McpToolResults`-API
  verwässert. Die XML-Doc sagt explizit "diese Methode liefert nur den
  rohen Block, nicht das fertige `CallToolResult`; der Einfachheit
  halber wird der Hint-String in den eigentlichen Output konkateniert"
  — das ist eine **Bewusste Abweichung** vom Plan, dokumentiert im
  `result.md` Abschnitt "Was geändert wurde" aber nicht begründet.

**Entscheidung:** **MAJOR, aber kein `issues`-Verdict** weil:
- Verhalten aller 8 Tools ist korrekt (T1–T8 + T11 sind grün).
- Die Abweichung ist klein und selbst-konsistent (jeder Aufrufer nutzt
  den alternativen Pfad).
- Der tote Code schadet niemandem, solange er nicht versehentlich
  refaktoriert wird.

**Empfehlung:** Tech-Debt-Eintrag TD-015 (siehe unten) — Löschen der
Methode + Test in EPIC-07 oder beim nächsten Anlass, der
`McpToolResults` ohnehin anfasst.

### Ebene 4 — Konzept-Treue

Keine. EPIC-06 ist vollständig umgesetzt; keine Scope-Erweiterung über
`konzept.md` Z. 102-103, 146-153, 609-611 hinaus; keine ungewollte
P0/P1-Extension.

## Sonstige Beobachtungen (MINOR)

1. **TD-003-Umgehung bewertet: ausreichend.** Die 9 Tool-Tests + E2E sind
   in `[Collection("ConsoleTestCollection")]` serialisiert, der Race
   tritt in 006-Tests nicht auf. Der Vorschlag im `tech-debt.md`
   (statisches Lock in `RegisterMSBuild`) ist Aufgabe für EPIC-07 oder
   später, **nicht** für 006 — TD-003-Verschärfung ist mit 4 betroffenen
   Test-Klassen + 1 E2E-Klasse noch beherrschbar. **Strukturelle
   Empfehlung:** TD-003-Fix in EPIC-07 (Kritiker-Empfehlung an den
   Planer, nicht Teil dieses Reviews).

2. **Fixture-Code-Duplikation TD-Eintrag wert: ja.** Der Coder
   beobachtet korrekt, dass `CompileErrorMiniFixtureWorkspace` die
   3 Methoden `CopyFixture`/`IsGeneratedPath`/`FindSolutionRoot` 1:1
   aus `BaselineMiniFixtureWorkspace` und `SymbolGraphMiniFixtureWorkspace`
   dupliziert (jetzt 3× → 4×). Vorschlag: gemeinsame Basisklasse
   `FixtureWorkspace` (oder `static class FixtureWorkspaceExtensions`)
   mit den 3 Methoden als `protected` Member / Extension. **Bewusst
   NICHT in 006** (A2 + A5: Eingriff in 3 bestehende Test-Fixtures wäre
   Scope-Creep). **TD-015-Eintrag wird unten angelegt.**

3. **`get_violations` Negativtest T9 ist eine echte Verhaltens-Garantie.**
   Der Test (`GetViolationsToolTests.cs:86-102`) prüft **drei**
   Negativ-Aussagen (`DoesNotContain("CS1513")`, `DoesNotContain("CS0246")`,
   `DoesNotContain("Hinweis:")`). Damit ist er kein "toter Test" wie
   `WarningsSection`, sondern ein **dokumentierter Schutzwall** gegen
   versehentliche zukünftige Änderungen. Die Begründung "Compile-Fehler
   ≠ Lint-Verstoß" ist konsistent mit `konzept.md` Z. 175-183 und
   `GetViolationsScanner.cs:62` (Aufruf von `LinterEngine.RunAsync` mit
   `noCache: true` — die `Compilation` enthält die Compile-Fehler, aber
   die Lint-Checker filtern sie nicht als Violations).

4. **`#nullable enable` auf `CompileErrorMiniFixtureWorkspace.cs` fehlt.**
   Die `rules.json` setzt `EnforceNullableEnable: true` global, ohne
   `*.Tests`-Override. Aber **bestehende** Test-Files (`BaselineMiniFixture
   Workspace`, `SymbolGraphMiniFixtureWorkspace`, alle `*ToolTests.cs`)
   haben ebenfalls kein `#nullable enable` und Self-Lint ist grün — also
   ist es offenbar eine **Warnung**, kein Fehler, oder die Regel greift
   für Test-Files nicht. Der Coder folgt der bestehenden Konvention.
   **Keine 006-spezifische Lücke** — aber ein **Projekt-weites
   Beobachtung**: die `EnforceNullableEnable`-Regel ist auf Test-Files
   offenbar nicht durchsetzbar, was im Widerspruch zur Regel-Doku steht.
   Falls eine zukünftige Einheit `nullable enable` für Test-Files
   erzwingen will, ist das ein eigenes Aufräum-Thema.

5. **`McpCompileDiagnostics.GetErrorsByFileAsync` ist korrekt designed.**
   Cognitive-Complexity-Reduktion von 16 → 8 (Plan-Check 6) durch
   Extraktion in `GetProjectErrorsAsync` + `Accumulate` ist sauber
   umgesetzt. Kein State, keine `McpCodeGraphServer`-Abhängigkeit,
   direkt unit-testbar (reine Funktion auf `Solution`).

## Bewertung der TD-003-Umgehung

**Ausreichend.** Die 006-Tests sind in `ConsoleTestCollection`
serialisiert, TD-003-Race ist in 006 nicht aufgetreten. Der Vorschlag
im `tech-debt.md` (statisches Lock in `RegisterMSBuild`) bleibt für
EPIC-07 oder später offen — der Coder hat den Race-Bypass korrekt
umgesetzt, ohne ihn zu verstecken. Eine **strukturelle Lösung in 006
wäre Scope-Creep** gewesen (A2 + A5). TD-003-Verschärfung ist mit 5
betroffenen Test-Klassen in 006 noch beherrschbar, aber mit jeder
weiteren parallelen Test-Klasse (EPIC-07) steigt die
Kollisionswahrscheinlichkeit — der Planer von 007 sollte TD-003 als
**erste Amtshandlung** adressieren.

## Bewertung der Coder-Beobachtung "Fixture-Code-Duplikation"

**TD-Eintrag wert: ja.** Die Duplikation von `CopyFixture` /
`IsGeneratedPath` / `FindSolutionRoot` in 4 Fixture-Klassen
(`BaselineMini`, `GitImpact`, `SymbolGraph`, `CompileError`) ist eine
klassische "Duplikate durch Blindheit"-Gefahr (Kernel Teil B). Der
Coder hat das **richtig erkannt** und **richtig nicht gefixt** (A2).
Vorschlag des Coders (gemeinsame Basisklasse oder Extension-Methoden)
ist plausibel. **Neuer Tech-Debt-Eintrag TD-015** wird unten angelegt.

## Klare Aussage zu `get_violations` Negativtest T9

**Begründung "Compile-Fehler ≠ Lint-Verstoß" ist korrekt und durch den
Code verifiziert:**

- `GetViolationsTool.cs` ist im Diff **nicht** enthalten (`git show
  de47034` zeigt keine Änderungen an dieser Datei).
- `GetViolationsTool.ExecuteAsync` (`GetViolationsTool.cs:24-33`)
  ruft `GetViolationsScanner.BuildViolationsTextAsync` auf.
- `GetViolationsScanner` (`GetViolationsScanner.cs:43-74`) ruft
  `LinterEngine.RunAsync(solution, noCache: true, cacheTtlMinutes: 0,
  ct)` auf.
- Die `LinterEngine` führt die konfigurierten Lint-Checker auf der
  `Compilation` aus. Compile-Fehler in der `Compilation` sind Roslyn-
  Diagnostics, keine `RuleViolation`s — die Lint-Checker filtern sie
  nicht als Lint-Verstöße heraus, weil sie nie welche waren.
- Der T9-Test verifiziert genau das:
  `Assert.DoesNotContain("CS1513", text)` und
  `Assert.DoesNotContain("CS0246", text)` und
  `Assert.DoesNotContain("Hinweis:", text)`.
- **Robustheit:** T9 ist ein **Negativtest**, der eine **dokumentierte
  Eigenschaft** schützt: dass der EPIC-06-Warnhinweis-Pfad nicht
  versehentlich in `get_violations` hineinwächst. Wenn ein zukünftiger
  Coder den Hinweis dort hinzufügen würde, würde T9 sofort fehlschlagen
  — exakt der A3-Schutz, den der Plan verlangt hat (Plan Z. 1083-1086).

**Verifiziert am Code: T9 ist ein wertvoller Test, kein toter Test.**

## Tech-Debt-Eintrag (neu)

### TD-015 — `McpToolResults.WarningsSection` ist Dead Code mit tautologischem Test [Priorität: niedrig]

- **Ort:** `src/AiNetLinter/Mcp/McpToolResults.cs:117` + `src/AiNetLinter.Tests/Mcp/McpToolResultsTests.cs:42-54`.
- **Befund:** `WarningsSection(string warningText) => warningText;` ist eine
  Identitäts-Funktion ohne Production-Caller. Der Test `WarningsSection
  _ReturnsWarningTextUnchanged_ForConcatenationByTool` testet nur
  Identität — tautologisch. A3-Nachweis (Compile-Fehler ohne Methode)
  ist technisch korrekt, trägt aber keinen Verhaltens-Wert: die
  Methode **macht nichts Sinnvolles**, sie ist nur da, damit der
  Helper-Symmetrie-Test kompiliert. Abweichung vom Plan: Plan Z. 558-564
  sah `CallToolResult` als Rückgabetyp vor, nicht `string`.
- **Vorschlag:** Methode + zugehörigen Test löschen beim nächsten
  Anlass, der `McpToolResults` ohnehin anfasst (z. B. EPIC-07 oder
  EPIC-08). Alternative: Methode zu einer echten Implementierung
  umbauen (z. B. `CallToolResult` mit `IsError == false` und dem
  Text als ContentBlock), und Tools entsprechend umstellen — dann
  aber mit echtem Test (kein Identitäts-Assert).
- **Status:** offen

### TD-016 — Fixture-Helper-Duplikation in 4 `*FixtureWorkspace`-Klassen [Priorität: niedrig]

- **Ort:** `src/AiNetLinter.Tests/Fixtures/{Baseline,GitImpact,SymbolGraph,CompileError}MiniFixtureWorkspace.cs`
  (alle 4 duplizieren `CopyFixture`/`IsGeneratedPath`/`FindSolutionRoot` 1:1).
- **Befund:** 4× identische 20-Zeilen-Methoden, kein gemeinsamer
  Code-Pfad. Risiko: künftige Änderungen (z. B. weitere
  Ausschluss-Verzeichnisse) müssen 4× synchron gehalten werden. Die
  Methoden sind rein (`static`), keine Abhängigkeit auf Instanz-State.
- **Vorschlag:** Gemeinsame `internal static class FixtureWorkspaceHelpers`
  mit den 3 Methoden, oder `abstract class FixtureWorkspaceBase : IDisposable`
  mit den Methoden als `protected` Member. Erste Aufräum-Gelegenheit:
  nächste Fixture, die angelegt wird (z. B. für EPIC-07 Last-Fixture
  aus konzept.md Z. 294-304).
- **Status:** offen

## Tech-Debt-Index-Update (für `tech-debt.md`)

Die folgenden zwei Index-Zeilen sind am Anfang der
`tech-debt.md`-Index-Tabelle zu ergänzen (vom Orchestrator, nicht von
mir — A2 + A7):

```markdown
| TD-015 | `src/AiNetLinter/Mcp/McpToolResults.cs` (`WarningsSection`) | niedrig | Identitäts-Passthrough ohne Production-Caller; Test tautologisch. |
| TD-016 | `src/AiNetLinter.Tests/Fixtures/*FixtureWorkspace.cs` | niedrig | `CopyFixture`/`IsGeneratedPath`/`FindSolutionRoot` 4× dupliziert. |
```

---

**Verdict: approved**

**Anzahl Findings nach Severity**: CRITICAL=0, MAJOR=1, MINOR=5

**TD-003-Umgehung bewertet**: ausreichend

**Fixture-Code-Duplikation TD-Eintrag wert?**: ja (TD-016)

**Selbst-Verifikation**: Re-Run gemacht (Build, Tests, 6 Footprint-Stichproben,
Self-Lint, Git-Diff-Verifikation) + Plausibilitäts-Bewertung der A3-Beweise.

**Nächste Aktion des Orchestrators**:
006 ist fertig. EPIC-06 vollständig. Empfehlung: Planer entscheidet JIT
zwischen EPIC-07 (Tests-Ausbau: Staleness-Invalidierung, Integrationstests
je Tool, Miss-Hint, Mehrdeutigkeits-Abbruch, Cache-Isolation, CLI-Regression
— Konzept Z. 104-107, 624) und EPIC-08 (Doku: `Docs/agent-api.md`,
`Docs/integration.md`, `Docs/ROADMAP.md`, `README.md`). TD-003-Fix gehört
in EPIC-07 (Kritiker-Empfehlung wahrscheinlich, aber nicht Teil dieses
Reviews). TD-015 und TD-016 können beim nächsten Anlass, der
`McpToolResults` bzw. Test-Fixtures anfasst, inline mitgenommen werden.
