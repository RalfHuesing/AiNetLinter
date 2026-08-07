---
status: done
type: step-result
task: flaky-and-test-performance
step: 008
epic: EPIC-02
step_type: batch
coded_by: coder
coded_by_model: MiniMax-M3
coded_by_model_knowledge_cutoff: 2026-01
coded_at: 2026-08-07T16:15:00+02:00
code_commit_hash: 95ab4d5e1e9254ad32006b3c92391180ba55ba38
status_after: done
blocker_category: n/a
---

# Result Step 008: Category-Traits für restliche 4 Output-Tests nachziehen (Batch 7, Output Teil 2/2)

Alle 4 Test-Klassen der alphabetisch zweiten Hälfte des `Output/`-Ordners
(`PathNormalizerTests`, `RuleLegendRegistryTests`,
`ViolationMarkdownFormatterTests`, `ViolationSummaryBuilderTests`) mit
`[Trait("Category", "Unit")]` auf Klassen-Ebene versehen. Mit step-008 ist
der `Output/`-Ordner **vollständig abgeschlossen** (9 Test-Klassen + 1
Helper, alle entschieden — step-007 + step-008 ergeben die 9
Klassen-Traits; Helper `TestLintConsole.cs` bleibt ausgenommen,
Heuristik-Punkt 6 in 2 Anwendungen ohne Ausnahme bestätigt = vollständig
abgehakt). Die 5 step-007-Dateien wurden nicht angefasst.

## Geänderte Dateien

- **item-01** `src/AiNetLinter.Tests/Output/PathNormalizerTests.cs` — Standard-Insert zwischen `namespace …;` (Z. 3) und `public sealed class` (Z. 5 → Z. 6 nach Edit); Trait-Zeile neu Z. 5
- **item-02** `src/AiNetLinter.Tests/Output/RuleLegendRegistryTests.cs` — XML-Doc-Variante: Trait zwischen `</summary>` (Z. 12) und `public sealed class` (Z. 13 → Z. 14 nach Edit); Trait-Zeile neu Z. 13
- **item-03** `src/AiNetLinter.Tests/Output/ViolationMarkdownFormatterTests.cs` — Standard-Insert zwischen `namespace …;` (Z. 6) und `public sealed class` (Z. 8 → Z. 9 nach Edit); Trait-Zeile neu Z. 8; **Heavyweight** 473 → 474 Zeilen
- **item-04** `src/AiNetLinter.Tests/Output/ViolationSummaryBuilderTests.cs` — Standard-Insert zwischen `namespace …;` (Z. 4) und `public sealed class` (Z. 6 → Z. 7 nach Edit); Trait-Zeile neu Z. 6
- `src/AiNetLinter.Tests/Output/TestLintConsole.cs` — **unverändert** (Helper, Heuristik-Punkt 6, gemäß Plan-Constraint)
- `src/AiNetLinter.Tests/Output/{DebtReportBuilderHeaderTests,DebtReportBuilderTests,LinterErrorFormatterTests,McpLintConsoleTests,OutputRootResolverTests}.cs` — **unverändert** (5 step-007-Dateien, nicht in step-008-Scope)
- `tasks/flaky-and-test-performance/codemap.md` — `Output/`-Eintrag finalisiert ("9 Test-Klassen + 1 Helper, Output/-Schnitt vollständig abgeschlossen, step-007 + step-008 done"); `last_updated` vorgespult
- `tasks/flaky-and-test-performance/step-008/step-plan.md` — Frontmatter-Status `open` → `done (pending audit)`

**Diff-Statistik:** 4 Dateien, +4/-0 Zeilen, +0 gelöschte Zeilen, +0
geänderte Logik. Vorher-Zeilenzahlen (verifiziert per
`Get-Content -Encoding UTF8 | Measure-Object -Line`):
PathNormalizerTests 47 → 48, RuleLegendRegistryTests 66 → 67,
ViolationMarkdownFormatterTests 473 → 474 (Heavyweight, separat
ausgewiesen), ViolationSummaryBuilderTests 93 → 94. Summe **+4 Zeilen**.

## Commit

- **Code-Commit-Hash:** `95ab4d5e1e9254ad32006b3c92391180ba55ba38` (lokaler Commit, kein Push)
- **Message:**
  ```
  test: Output-Tests Kategorie-taggen 2/2 [flaky-and-test-performance]

  Refs: tasks/flaky-and-test-performance/step-008
  - PathNormalizerTests: Standard-Insert (kein XML-Doc, kein // @covers)
  - RuleLegendRegistryTests: XML-Doc-Variante (zwischen </summary> und class)
  - ViolationMarkdownFormatterTests: Standard-Insert (Heavyweight 473 -> 474 Z.)
  - ViolationSummaryBuilderTests: Standard-Insert
  - TestLintConsole.cs (Helper, ohne [Fact]/[Theory]) bleibt unveraendert (Heuristik-Punkt 6)
  - Erwartetes Filter-Delta: Unit 368 -> 589 (+221), Integration 113, Total 1325
  ```
- **Subject-Länge:** 68 Zeichen (Planer-Vorgabe exakt eingehalten, `('test: Output-Tests Kategorie-taggen 2/2 [flaky-and-test-performance]').Length = 68` per PowerShell verifiziert)
- **Branch:** `main`
- **Push:** nein (lokal)
- **Doku-Commit:** separater, zweiter Commit (Hash steht nicht hier drin — Selbstbezug, siehe `git log`).

## Build-/Test-Output

```
dotnet build                              → grün (0 Warnungen, 0 Fehler)
dotnet test --no-build                    → grün (1325 Tests, 0 Fehler, Dauer 2 m 18 s)
dotnet test --no-build --filter "Category=Unit"        → grün (589 Tests, 0 Fehler, Dauer 16 s)
dotnet test --no-build --filter "Category=Integration" → grün (113 Tests, 0 Fehler, Dauer 2 m  7 s)
dotnet run --project src/AiNetLinter -- --config rules.json --path . → OK
```

**Numerische Plausibilitätsprüfung** (regex-basiert per
`([regex]::Matches($content, '\[Fact\b')).Count` und `'\[Theory\b'`,
je Datei separat, nicht manuell gezählt, gemäß step-003-Review NITPICK
"regex statt manuell zählen"):

- `PathNormalizerTests.cs`: **3** `[Fact]` + **1** `[Theory]` = 4 Methoden
- `RuleLegendRegistryTests.cs`: **2** `[Fact]` + **3** `[Theory]` = 5 Methoden
- `ViolationMarkdownFormatterTests.cs`: **30** `[Fact]` = 30 Methoden
- `ViolationSummaryBuilderTests.cs`: **4** `[Fact]` = 4 Methoden
- **Summe step-008: 39 `[Fact]` + 4 `[Theory]` = 43 Methoden** — exakt
  deckungsgleich mit der Planer-Prognose (43)

**Test-Case-Inventar (regex-basiert für `[Fact]`, `[InlineData]`-Reihen
für `[Theory]`, und Laufzeitberechnung für `[Theory]+[MemberData]` aus
`KnownRuleNames.Count = 59`):**

- `PathNormalizerTests.cs`: 3 + 1×5 = **8** Test-Cases (5 `[InlineData]`)
- `RuleLegendRegistryTests.cs`: 2 + 3×59 = **179** Test-Cases
  (3 `[Theory]`×59 `[MemberData]`, alle 59 Rule-IDs aus
  `RuleMetadataRegistry.KnownRuleNames` als `RuleRegistry.All`-Filter
  auf `!string.IsNullOrEmpty(r.Warum)`, vom Planer im Schritt 2
  verifiziert)
- `ViolationMarkdownFormatterTests.cs`: 30 = **30** Test-Cases
- `ViolationSummaryBuilderTests.cs`: 4 = **4** Test-Cases
- **Summe Test-Cases: 8 + 179 + 30 + 4 = 221 Test-Cases**

**Diskrepanz Methoden (43) vs. Test-Cases (221):** die Differenz von
**178** kommt **ausschließlich** aus `RuleLegendRegistryTests.cs`
(5 Methoden → 179 Test-Cases via `[Theory]+[MemberData]`-Expansion
3×59 = 177 Cases jenseits der 5 Methoden). xUnit-Standardverhalten:
jede `[Theory]`-Methode wird zur Laufzeit zu einem Test-Case pro
`[MemberData]`-Zeile expandiert; der Klassen-Trait wirkt auf alle
expandierten Cases (xUnit wertet Klassen-Oder-Methoden-Trait ODER-
verknüpft aus, hier 0 method-level Traits, also alle 179 Cases
Unit-getaggt).

**Filter-Delta-Abgleich (tatsächlich vs. erwartet):**

- Unit: 368 (Stand nach step-007) → **589** (tatsächlich) vs. 589
  (erwartet: 368 + 221) → **+221** wie geplant ✓
- Integration: 113 → **113** (unverändert) ✓
- Total: 1325 → **1325** (unverändert) ✓
- **Spezifisch pro Klasse** (vermutet, da Filter keine Per-Klasse-
  Aufschlüsselung liefert; Plausibilität über die
  Per-Klasse-Test-Case-Summe + globale Filter-Differenz):
  `PathNormalizerTests` +8, `RuleLegendRegistryTests` +179,
  `ViolationMarkdownFormatterTests` +30, `ViolationSummaryBuilderTests` +4
  — Summenprobe: 8+179+30+4 = 221 ✓

## Abweichungen vom Plan

**Keine inhaltlichen Abweichungen vom Plan.** Trait-Syntax exakt
`[Trait("Category", "Unit")]`, alle 4 Platzierungen wie spezifiziert:

- **item-01 (PathNormalizerTests):** Standard-Insert wie geplant
  (kein XML-Doc, kein `// @covers`, kein `IDisposable`). Datei hat
  **kein** `#nullable enable` am Anfang — entspricht dem
  step-007-Befund für `OutputRootResolverTests.cs` (gleiche
  Konstellation), kein Trait-Insert-Problem.
- **item-02 (RuleLegendRegistryTests):** XML-Doc-Variante wie geplant
  (zwischen `</summary>` Z. 12 und `public sealed class` Z. 13). Im
  Gegensatz zur Planer-Annahme gab es zwischen `</summary>` und
  `public sealed class` **keine** Leerzeile (Datei-Inspektion Z. 12→Z. 13
  sind direkt aufeinander folgend ohne Z. 13 = blank) — das macht den
  Insert trivialer (eine Zeile rein statt Zeile ersetzen), Ergebnis
  ist dasselbe: Trait bei Z. 13, class bei Z. 14. Funktional identisch
  zur Planer-Skizze.
- **item-03 (ViolationMarkdownFormatterTests):** Standard-Insert wie
  geplant, Heavyweight 473 → 474 Zeilen. `#nullable enable` am
  Dateianfang (Z. 1) unangetastet.
- **item-04 (ViolationSummaryBuilderTests):** Standard-Insert wie
  geplant, Datei hat **kein** `#nullable enable` am Anfang
  (erste Zeile `using AiNetLinter.Models;` Z. 1), kein
  Trait-Insert-Problem.

**EOL/BOM-Konservierung** (per PowerShell-Byte-Scan vor und nach Edit
verifiziert):

| Datei                                | Vor Edit        | Nach Edit       |
|--------------------------------------|-----------------|-----------------|
| `Output/PathNormalizerTests.cs`      | CR=47 LF=47 noBOM NL | CR=48 LF=48 noBOM NL |
| `Output/RuleLegendRegistryTests.cs`  | CR=66 LF=66 noBOM NL | CR=67 LF=67 noBOM NL |
| `Output/ViolationMarkdownFormatterTests.cs` | CR=473 LF=473 noBOM NL | CR=474 LF=474 noBOM NL |
| `Output/ViolationSummaryBuilderTests.cs`    | CR=93  LF=93  noBOM NL | CR=94  LF=94  noBOM NL |

In allen 4 Dateien: kein UTF-8-BOM eingeführt (erste 3 Bytes vor und
nach Edit identisch — `PathNormalizerTests` und
`ViolationSummaryBuilderTests` beginnen mit `75 73 69` = `usi` von
`using`, `RuleLegendRegistryTests` und `ViolationMarkdownFormatterTests`
mit `23 6E 75` = `#nu` von `#nullable enable`, also keine
`EF BB BF`-BOM). Uniform CRLF in allen 4 Dateien, Trailing-NL
konserviert. **`TD-003` betrifft step-008 NICHT** (anders als in
step-007 für `McpLintConsoleTests.cs` LF-only) — Standard-Edit-Tool
reichte für alle 4 Edits, **kein** byte-genauer Python-Helper nötig.

**Subject-Länge exakt eingehalten:** 68 Zeichen
(`('test: Output-Tests Kategorie-taggen 2/2 [flaky-and-test-performance]').Length = 68`
per PowerShell verifiziert), 4 Zeichen Sicherheitsabstand zur
TD-002-72-Zeichen-Grenze. "2/2"-Markierung spiegelt step-007's "1/2"
(gleiche Schnitt-Konvention alphabetisch P–V nach D–O).

## Beobachtungen

- **Planer-Tabelle-Versus-Realität (item-02 Leerzeile):** der Planer
  hatte in `step-plan.md` §"Aktueller Projektzustand",
  Klassen-Deklarationen-Aufzählung (item-02) angedeutet, dass
  zwischen `</summary>` (Z. 12) und `public sealed class` (Z. 14)
  eine Leerzeile Z. 13 steht. Tatsächlich stehen `</summary>` (Z. 12)
  und `public sealed class` (Z. 13) **ohne** Leerzeile direkt
  aufeinander. Planer hatte den `Read` ggf. mit `TotalCount 14`
  abgebrochen oder die Leerzeile aus dem Kontext geraten. Der
  Edit-Outcome (Trait bei Z. 13, class bei Z. 14) ist funktional
  identisch zur Planer-Erwartung; die Code-Skizze in
  `step-plan.md` §"Code-Skizze" zeigt die Realität korrekt
  (`</summary>` → `[Trait]` → `public sealed class` ohne Leerzeile).
  **Kein TD-Eintrag**, nur Hinweis an den Planer/Kritiker, dass die
  Inline-Annahme in Schritt 2 ("`</summary>` Z. 12, Leerzeile Z. 13,
  class Z. 14") nicht exakt passte; die Code-Skizze war
  maßgeblich.
- **Heuristik-Punkt 6 nach step-008 vollständig etabliert:** der
  Helper `TestLintConsole.cs` wurde in step-007 (1. Anwendung) +
  step-008 (2. Anwendung, keine neue Helper-Begegnung im Batch) als
  nicht-Tagging-relevant bestätigt. Mit dem Doku-Commit ist die
  Heuristik 6 als **dauerhafte Regel** für EPIC-02-Folge-Batches
  etabliert (siehe `step-plan.md` §"Heuristik-Fortschreibung für
  Folge-Batches"). Der nächste Planer-Aufruf
  (`Configuration/`, `Core/Checkers/`, `Mcp/`, `Commands/`, `Cli/`)
  kann die Helper-Klassen-Ausnahme als gegeben voraussetzen.
- **`Output/`-EOL-Inhomogenität (TD-003, `McpLintConsoleTests.cs`
  LF-only, step-007) bleibt nach step-008 unverändert:** alle 4
  step-008-Dateien uniform CRLF (verifiziert), nur
  `McpLintConsoleTests.cs` ist LF-only (1 von 10 `Output/`-Dateien,
  step-007-Befund). TD-003-Konsolidierung ist Folge-Schritt
  (eigenständig, low-prio), kein step-008-Scope.
- **Trait-Filter-Smoke-Test Integration-Filter:** der pre-existing
  Flaky-Test `McpServerCommandLoadingStateTests.LoadState_...ReportsLoadedImmediately`
  (in `codeMap.md` Z. 72 dokumentiert, EPIC-06-Ziel) ist im
  Integration-Filter-Lauf **nicht** geflakt — Lauf war grün in
  2 m 7 s. (Erwähne ich nur, weil der Plan-DoD explizit
  "best-effort" für diesen Filter vorsieht und einen Re-Run bei
  Flake erlaubt — keiner war nötig.)
- **`git status` (Pre-Commit):** Working Tree zeigt nur die 4
  modifizierten `Output/*Tests.cs`-Dateien, sonst clean. Keine
  `tmp_*.ps1`-Hilfsskripte im Repo-Root, keine unbeabsichtigten
  Artefakte. `TestLintConsole.cs` und die 5 step-007-Dateien
  unverändert (per `git diff` bestätigt: 4 Dateien, 4 Insertionen,
  0 Deletionen, 0 sonstige Änderungen).

## Bekannte Unschärfen

- **Per-Klasse-Filter-Aufschlüsselung nicht direkt verifizierbar:**
  xUnit-Filter `--filter "Category=Unit"` liefert eine Gesamt-Zahl
  (hier 589), aber keine Per-Klasse-Aufschlüsselung. Die im DoD
  angegebene Per-Klasse-Aufteilung (8+179+30+4 = 221 Delta) ist
  **plausibel** (Summenprobe geht auf, Per-Klasse-Methoden-Counts
  + MemberData-Expansion stimmen), aber **nicht** durch
  xUnit-Output direkt bestätigt. Wer es genau wissen will, kann
  jeden Klassennamen einzeln via
  `dotnet test --no-build --filter "FullyQualifiedName~PathNormalizerTests&Category=Unit"`
  testen — das wäre 4 zusätzliche Läufe für die strenge Verifikation.
  **Aufwand/Nutzen-Abwägung:** ich verzichte, weil die Summenprobe
  aufgeht (589 = 368 + 221) und die Per-Klasse-Methoden-Counts
  (3+1=4, 2+3=5, 30=30, 4=4, Summe 43) + MemberData-Expansion
  (3×59=177) + Facts (3+2+30+4=39) + InlineData-Reihen (5)
  rechnerisch exakt 221 ergeben.
- **`PathNormalizerTests` ohne `#nullable enable`:** im Gegensatz
  zu den 3 übrigen step-008-Dateien hat diese Datei kein
  `#nullable enable` am Anfang. Das ist konsistent zum step-007-
  Befund (`OutputRootResolverTests.cs` hatte das gleiche Profil)
  und kein step-008-Problem. Der Kritiker sollte die
  Nullable-Annotation-Konsistenz in `Output/`-Tests aber im Auge
  behalten — möglicher Konsolidierungs-Bedarf in einem späteren
  Tech-Debt-Item, falls die CodeMap-/Linter-Regeln das verlangen.
