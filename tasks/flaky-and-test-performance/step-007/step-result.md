---
status: done
type: step-result
task: flaky-and-test-performance
step: 007
epic: EPIC-02
step_type: batch
coded_by: coder
coded_by_model: MiniMax-M3
coded_by_model_knowledge_cutoff: 2026-01
coded_at: 2026-08-07T14:45:00+02:00
code_commit_hash: 9c4269f
status_after: done
blocker_category: n/a
---

# Result Step 007: Category-Traits für erste 5 Output-Tests nachziehen (Batch 6, Output Teil 1/2)

Alle 5 Test-Klassen der alphabetisch ersten Hälfte des `Output/`-Ordners
(`DebtReportBuilderHeaderTests`, `DebtReportBuilderTests`,
`LinterErrorFormatterTests`, `McpLintConsoleTests`,
`OutputRootResolverTests`) mit `[Trait("Category", "Unit")]` auf
Klassen-Ebene versehen. Die Helper-Klasse `TestLintConsole.cs` (ohne
`[Fact]`/`[Theory]`, Heuristik-Punkt 6) blieb unverändert; die 4
step-008-Klassen (`PathNormalizer`, `RuleLegendRegistry`,
`ViolationMarkdownFormatter`, `ViolationSummaryBuilder`) wurden nicht
angefasst.

## Geänderte Dateien

- **item-01** `src/AiNetLinter.Tests/Output/DebtReportBuilderHeaderTests.cs` — `[Trait("Category", "Unit")]` zwischen `// @covers DebtReportBuilder` (Z. 7) und `public sealed class …` (Z. 9) eingefügt
- **item-02** `src/AiNetLinter.Tests/Output/DebtReportBuilderTests.cs` — Standard-Insert zwischen `namespace …;` und Klasse (Z. 8 neu)
- **item-03** `src/AiNetLinter.Tests/Output/LinterErrorFormatterTests.cs` — XML-Doc-Variante zwischen `</summary>` (Z. 10) und Klasse (Z. 12 neu)
- **item-04** `src/AiNetLinter.Tests/Output/McpLintConsoleTests.cs` — XML-Doc-Variante zwischen `</summary>` (Z. 15) und Klasse (Z. 16 neu); die 3 bestehenden method-level `[Trait("Category", "Unit")]` (Z. 20, 39, 58 nach +1-Zeilen-Shift) **unverändert** (additiv, Heuristik-Punkt 4)
- **item-05** `src/AiNetLinter.Tests/Output/OutputRootResolverTests.cs` — Standard-Insert zwischen `namespace …;` und Klasse (Z. 4 neu)
- `src/AiNetLinter.Tests/Output/TestLintConsole.cs` — **unverändert** (Helper, Heuristik-Punkt 6)
- `tasks/flaky-and-test-performance/codemap.md` — `Output/`-Eintrag auf "9 Test-Klassen + 1 Helper" + step-007/008-Schnitt aktualisiert; `last_updated` vorgespult
- `tasks/flaky-and-test-performance/step-007/step-plan.md` — Frontmatter-Status `open` → `done (pending audit)`

## Commit

- **Code-Commit-Hash:** `9c4269f`
- **Message:**
  ```
  test: Output-Tests Kategorie-taggen 1/2 [flaky-and-test-performance]

  Refs: tasks/flaky-and-test-performance/step-007
  - DebtReportBuilderHeaderTests: [Trait(Category, Unit)] zwischen // @covers und class
  - DebtReportBuilderTests: Standard-Insert (kein XML-Doc, kein // @covers)
  - LinterErrorFormatterTests: XML-Doc-Variante (zwischen </summary> und class)
  - McpLintConsoleTests: XML-Doc-Variante + 3 method-level Traits additiv (unveraendert)
  - OutputRootResolverTests: Standard-Insert
  - TestLintConsole.cs (Helper, ohne [Fact]/[Theory]) bleibt unveraendert (Heuristik-Punkt 6)
  - Erwartetes Filter-Delta: Unit 355 -> 368 (+13), Integration 113, Total 1325
  ```
- **Subject-Länge:** 68 Zeichen (Planer-Vorgabe exakt eingehalten, `('test: Output-Tests Kategorie-taggen 1/2 [flaky-and-test-performance]').Length = 68` per PowerShell verifiziert)
- **Branch:** `main`
- **Push:** nein (lokal)
- **Doku-Commit:** separater, zweiter Commit (Hash steht nicht hier drin — Selbstbezug, siehe `git log`).

## Build-/Test-Output

```
dotnet build → grün (0 Warnungen, 0 Fehler)
dotnet test --no-build → grün (1325 Tests, 0 Fehler, Dauer 1 m 46 s)
dotnet test --no-build --filter "Category=Unit"        → grün ( 368 Tests, 0 Fehler, Dauer 12 s)
dotnet test --no-build --filter "Category=Integration" → grün ( 113 Tests, 0 Fehler, Dauer 1 m 57 s)
dotnet run --project src/AiNetLinter -- --config rules.json --path . → OK
```

**Numerische Plausibilitätsprüfung** (regex-basiert per
`Select-String -Path … -Pattern '\[Fact|\[Theory'`, je Datei separat,
nicht manuell gezählt):
- `DebtReportBuilderHeaderTests.cs`: **3** `[Fact]`
- `DebtReportBuilderTests.cs`: **1** `[Fact]`
- `LinterErrorFormatterTests.cs`: **6** `[Fact]`
- `McpLintConsoleTests.cs`: **3** `[Fact]`
- `OutputRootResolverTests.cs`: **3** `[Fact]`
- **Summe step-007: 16 `[Fact]`, 0 `[Theory]`** — exakt deckungsgleich
  mit der Planer-Prognose (16, davon 3 method-level bereits in
  `McpLintConsoleTests.cs` Unit-getaggt, also +13 neues Unit-Delta).

**Filter-Delta-Abgleich (tatsächlich vs. erwartet):**
- Unit: 355 → **368** (tatsächlich) vs. 368 (erwartet) → **+13** wie geplant ✓
- Integration: 113 → **113** (unverändert) ✓
- Total: 1325 → **1325** (unverändert) ✓

## Abweichungen vom Plan

**EOL-Status `McpLintConsoleTests.cs` weicht von Planer-Tabelle ab.** Der
Planer hatte in seiner Verifikations-Tabelle (Plan §"Aktueller
Projektzustand", EOL-Tabelle) `McpLintConsoleTests.cs` als `CR=62
LF=62` (CRLF) verifiziert. Tatsächlich ist die Datei `CR=0 LF=62`
(LF-only) — die übrigen 9 `Output/`-Dateien sind uniform CRLF
(`CR=LF` in allen 9). Plan-Konsequenz: Planer hatte "Standard-Edit-Tool
reicht für alle 5 step-007-Edits" angenommen, in der Realität musste
für `McpLintConsoleTests.cs` ein byte-genauer Python-Helper analog zum
step-004-Pattern verwendet werden, um die LF-Existenz nicht zu
korrumpieren. Die anderen 4 Dateien sind wie geplant mit dem
Standard-Edit-Tool bearbeitet. **Ergebnis-EOL nach Edit** (per
PowerShell-Byte-Scan verifiziert):
- `DebtReportBuilderHeaderTests.cs`: CR=35 LF=35 (war 34/34, +1 Zeile)
- `DebtReportBuilderTests.cs`: CR=51 LF=51 (war 50/50, +1 Zeile)
- `LinterErrorFormatterTests.cs`: CR=80 LF=80 (war 79/79, +1 Zeile)
- `McpLintConsoleTests.cs`: CR=0 LF=63 (war 0/62, +1 Zeile, **LF-only erhalten**)
- `OutputRootResolverTests.cs`: CR=51 LF=51 (war 50/50, +1 Zeile)

In allen 5 Dateien: kein UTF-8-BOM eingeführt (erste 3 Bytes der
Working-Copy-Bytes vor und nach Edit verifiziert — DebtReportBuilder*
beginnt mit `117 115 105` = `usi` von `using`, die anderen 3 mit
`35 110 117` = `#nu` von `#nullable enable`, also keine `EF BB BF`-BOM).

`git ls-files --eol` bestätigt: Index = `i/lf` für alle 5; Working Copy =
`w/crlf` für 4 Dateien, `w/lf` für `McpLintConsoleTests.cs` (so wie vor
dem Edit; `core.autocrlf=true` ist der Standard-Hintergrund, nicht
edit-verursacht). Die `LF will be replaced by CRLF the next time Git
touches it`-Warnung von `git status`/`diff` für `McpLintConsoleTests.cs`
ist die normale autocrlf-Hinweismeldung — kein Edit-Problem, der Working-
Copy-Status der Datei wurde korrekt konserviert.

**Keine inhaltlichen Abweichungen vom Plan:** Trait-Syntax exakt
`[Trait("Category", "Unit")]`, alle 5 Platzierungen wie spezifiziert
(Standard-Insert / `// @covers`-Variante / XML-Doc-Variante /
XML-Doc+additive-method-level-Variante), Helper `TestLintConsole.cs`
nicht angetastet, 4 step-008-Klassen nicht angefasst.

## Beobachtungen

- **Subject-Längen-Sicherheitsabstand:** der Planer-Vorschlag mit 68
  Zeichen hat 4 Zeichen Sicherheitsabstand zur TD-002-72-Zeichen-
  Grenze. Die "1/2"-Markierung im Subject (analog zu step-008's
  geplantem "2/2") ist eine **funktionale Konvention** zwischen
  step-007/008, hilft dem nächsten Planer, den Schnitt aus dem
  Git-Log wiederzufinden, falls `roadmap.md` zwischenzeitlich nicht
  gelesen wird.
- **`Output/`-EOL-Inhomogenität in der Realität (Planer-
  Verifikationsfehler):** `McpLintConsoleTests.cs` ist LF-only,
  obwohl die anderen 9 `Output/`-Dateien uniform CRLF sind. Das
  deckt sich mit dem `core.autocrlf=true`-Setup, das Git für die
  meisten Dateien LF→CRLF auf Checkout konvertiert, für genau diese
  eine Datei aber LF im Working Copy hinterlässt. Wurde mit
  byte-genauem Python-Helper konserviert (analog step-004-Pattern).
  **Hinweis an Planer/Kritiker:** der CodeMap-Eintrag `Output/`
  sollte ggf. die EOL-Inhomogenität in einem späteren Schritt
  konsolidieren — derzeit aber kein step-007-Scope, nur
  Beobachtung. Der Coder legt keinen Tech-Debt-Eintrag an.
- **Trait-Filter-Smoke-Test Integration-Filter:** der pre-existing
  Flaky-Test `McpServerCommandLoadingStateTests.LoadState_LoadFunc-
  CompletesSynchronouslyWithCatalog_ReportsLoadedImmediately` (in
  `codeMap.md` Z. 72 dokumentiert, EPIC-06-Ziel) ist im
  Integration-Filter-Lauf **nicht** geflakt — Lauf war grün in 1 m
  57 s. (Erwähne ich nur, weil der Plan-DoD explizit
  "best-effort" für diesen Filter vorsieht und einen Re-Run bei
  Flake erlaubt — keiner war nötig.)

## Bekannte Unschärfen

- **Planer-Verifikations-Tabelle vs. Realität (EOL):** wie oben
  dokumentiert — der Planer hatte `McpLintConsoleTests.cs` als
  CRLF verifiziert; tatsächlich ist die Datei LF-only. Der Coder
  hat das vor dem Edit durch eigenen Byte-Scan entdeckt und
  entsprechend mit Python-Helper statt Standard-Edit reagiert. Der
  Kritiker sollte ggf. den Planer-Verifikations-Workflow
  (`grep -cE 'CR|LF'` etc. statt Augenmaß auf den Datei-Output)
  prüfen — ist aber außerhalb des step-007-Scopes.
- **Git-`core.autocrlf=true` Verhalten beim nächsten Checkout:** die
  Working-Copy-Form von `McpLintConsoleTests.cs` ist LF-only, aber
  der Index hat die Datei als `i/lf` (LF), was bei `core.autocrlf=true`
  beim nächsten `git checkout` zu CRLF konvertiert würde. Das ist
  konsistent zum Repo-Default und nicht edit-verursacht. Der
  Kritiker sollte das nicht als "step-007 hat das File kaputt
  gemacht" missinterpretieren — der Commit-Inhalt (LF) entspricht
  dem vor-Edit-Working-Copy-Inhalt (LF), beide korrekt.
