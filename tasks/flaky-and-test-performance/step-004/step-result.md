---
status: done (pending audit)
type: step-result
task: flaky-and-test-performance
step: 004
epic: EPIC-02
step_type: batch
coded_by: coder
coded_by_model: MiniMax-M3
coded_by_model_knowledge_cutoff: 2026-01
coded_at: 2026-08-07T11:10:00+02:00
code_commit_hash: 57f7f03
status_after: done
blocker_category: n/a
---

# Result Step 004: Category-Traits für `src/AiNetLinter.Tests/Web/` (Batch 3)

## Zusammenfassung

Alle 5 Testklassen unter `src/AiNetLinter.Tests/Web/` wurden mit
`[Trait("Category", "Unit")]` auf Klassen-Ebene versehen. Trait wurde in
**alle 5 Fällen** zwischen `</summary>` und `public sealed class` platziert
(alle 5 Klassen haben XML-Doc — homogenster Batch der EPIC-02-Serie, keine
Sonderfälle). Insgesamt 5 Klassen × 1 Trait-Zeile = 5 Diff-Zeilen
(Commit-Statistik: `5 files changed, 5 insertions(+)`).

Klassifikations-Heuristik: alle 5 Klassen sind homogen `Unit` (kein
Subprozess-Marker im `Web/`-Ordner — verifiziert per
`McpTestClient`/`CliProcessRunner`/`Program.Main`/
`IClassFixture<McpLiveRepositoryFixture>`-Grep, 0/0/0/0 Treffer). Der
`RazorAnalyzer.Analyze(null!, ...)`-Aufruf in
`RazorAnalyzerTests.Extended.cs:30` ist ein in-process Edge-Input-Test
(kein Subprozess) — passt sauber zur bestehenden Negativ-Abgrenzung
"in-process API-Edge-Test", ändert nichts an der Unit-Klassifikation.

## Geänderte Dateien

```
src/AiNetLinter.Tests/Web/CssAnalyzerTests.cs            | 1 +
src/AiNetLinter.Tests/Web/JsAnalyzerTests.cs             | 1 +
src/AiNetLinter.Tests/Web/RazorAnalyzerTests.cs          | 1 +
src/AiNetLinter.Tests/Web/RazorAnalyzerTests.Extended.cs | 1 +
src/AiNetLinter.Tests/Web/WebSuppressionDetectorTests.cs | 1 +
5 files changed, 5 insertions(+)
```

Pro Item hinzugefügt: 1 Trait-Zeile. Gesamt-Diff: 5 Zeilen.

- **item-01** (`CssAnalyzerTests`): `[Trait("Category", "Unit")]` zwischen
  `</summary>` (Z. 14) und `public sealed class CssAnalyzerTests` (Z. 15).
  `// @covers CssConfig`-Marker (Z. 8) bleibt unverändert.
- **item-02** (`JsAnalyzerTests`): dito, Z. 14/15. `// @covers JsConfig` (Z. 8)
  bleibt unverändert.
- **item-03** (`RazorAnalyzerTests`): dito, Z. 16/17. `// @covers RazorConfig`
  (Z. 8) bleibt unverändert.
- **item-04** (`RazorAnalyzerExtendedTests`): dito, Z. 13/14. **Spezialfall
  bestätigt:** `RazorAnalyzer.Analyze(null!, ...)` in Z. 30 ist in-process,
  kein Subprozess-Hinweis.
- **item-05** (`WebSuppressionDetectorTests`): dito, Z. 11/12. Kleinste Klasse
  des Batches (6 `[Fact]`).

## Commit

- **Code-Commit-Hash:** `57f7f03`
- **Message:**
  ```
  test: Web-Tests Kategorie-taggen [flaky-and-test-performance]

  Refs: tasks/flaky-and-test-performance/step-004
  ```
- **Branch:** `main`
- **Push:** nein (lokal)
- **Doku-Commit:** separater, zweiter Commit (Hash steht nicht hier
  drin — Selbstbezug, siehe `git log`).

**Subject-Länge:** 61 Zeichen (inkl. `[flaky-and-test-performance]`-Suffix),
exakt wie im DoD vorgegeben — verifiziert per
`('test: Web-Tests Kategorie-taggen [flaky-and-test-performance]').Length`
→ `61`. 11 Zeichen Sicherheitsabstand zur 72-Zeichen-Grenze.

## Build-/Test-Output

```
dotnet build → grün (0 Warnungen, 0 Fehler, ~7s)
dotnet test (voll)                → grün (1325/1325, 0 Fehler, 1 m 54 s)
dotnet test --filter "Category=Unit"        → grün (278/278, 0 Fehler, 15 s)
dotnet test --filter "Category=Integration" → grün im 3. Anlauf (113/113)
                                              1./2. Anlauf flaky mit bekanntem
                                              pre-existing Test (EPIC-06)
dotnet run --project src/AiNetLinter -- --config rules.json --path . → OK
```

## Numerische Plausibilitätsprüfung

Regex-basierte Zählung (gemäß step-003-Review NITPICK "regex statt manuell
zählen") per `Select-String -Pattern "\[Fact\]"` über die 5 Dateien:

| Datei                              | `[Fact]`-Count |
|------------------------------------|---------------:|
| `CssAnalyzerTests.cs`              | 15             |
| `JsAnalyzerTests.cs`               | 20             |
| `RazorAnalyzerTests.cs`            | 15             |
| `RazorAnalyzerTests.Extended.cs`   | 18             |
| `WebSuppressionDetectorTests.cs`   |  6             |
| **Summe**                          | **74**         |

(Alle 74 sind `[Fact]`; `[Theory]`-Count = 0 in allen 5 Dateien.)

| Lauf                                          | Total | Passed | Failed | Skipped | Dauer     |
|-----------------------------------------------|------:|-------:|-------:|--------:|-----------|
| `dotnet test --no-build` (voll)               | 1325  | 1325   | 0      | 0       | 1 m 54 s  |
| `dotnet test --no-build --filter "Category=Unit"`       |  278 |   278  | 0      | 0       |   15 s    |
| `dotnet test --no-build --filter "Category=Integration"`|  113 |   113  | 0      | 0       | 1 m 47 s (3. Anlauf) |

- **Erwartet nach step-003:** Unit=204, Integration=113, Total=1325
- **Erwartet nach step-004:** Unit=204+74=278, Integration=113, Total=1325
- **Tatsächlich:** Unit=**278**, Integration=**113**, Total=**1325** → **Delta 74** ✅
- Differenz (74−74=0): keine — die Trait-Filter-Zahl deckt sich exakt mit
  der regex-basierten Methoden-Zählung. Damit ist die Klassifikations-
  Korrektheit aller 5 Klassen (alle = `Unit`) durch zwei unabhängige
  Methoden (statische Grep-Zählung + dynamische Filter-Läufe) bestätigt.

## Build-Output

```
Der Buildvorgang wurde erfolgreich ausgeführt.
    0 Warnung(en)
    0 Fehler
Verstrichene Zeit 00:00:06.75
```

Sauber, 0 Warnungen, 0 Fehler. Trait-Attribute folgen der etablierten
Konvention (`[Trait("Category", "Unit")]`, CamelCase-Großbuchstabe),
verifiziert per Grep über die 100+ bestehenden Trait-Vorkommen im Projekt.

## Test-Output (Auszüge)

- **Voll:** `Bestanden! Fehler: 0, erfolgreich: 1325, übersprungen: 0, gesamt: 1325, Dauer: 1 m 54 s`
- **Unit-Filter:** `Bestanden! Fehler: 0, erfolgreich: 278, übersprungen: 0, gesamt: 278, Dauer: 15 s`
- **Integration-Filter (3. Anlauf, grün):** `Bestanden! Fehler: 0, erfolgreich: 113, übersprungen: 0, gesamt: 113, Dauer: 1 m 47 s`

## Self-Lint-Output

```
# Run: 2026-08-07 11:06:47
OK
```

TD-001-konform, semantisch identisch zu `--self-lint`. Sauber.

## Abweichungen vom Plan

**Keine.** Plan-DoD vollständig erfüllt:

- [x] Alle 5 Items umgesetzt (je 1 `[Trait("Category", "Unit")]`-Zeile
      zwischen `</summary>` und `public sealed class`)
- [x] Bestehende Traits respektiert: nach Diff 5 Klassen mit
      Trait-Attribut, 0 ohne
- [x] `dotnet build` grün
- [x] `dotnet test` (voller Lauf) grün
- [x] `dotnet test --filter "Category=Unit"` grün
- [x] `dotnet test --filter "Category=Integration"` best-effort
      grün (3. Anlauf)
- [x] Self-Lint `OK`
- [x] Code-Commit auf `main` mit Conventional-Commit-Format (61 Zeichen
      Subject, exakt wie im DoD vorgegeben)
- [x] `step-plan.md` Status auf `done (pending audit)` gesetzt
- [x] `codemap.md` aktualisiert (Web/-Eintrag: „Korrektur in step-004"
      entfernt, Endstand "zuletzt: step-004")
- [ ] **Audit** durch planer/reviewer — dieser Step wartet noch auf
      Audit-Freigabe (Status `pending audit`)

**Implementierungs-Detail:** Auf diesem System ist `core.autocrlf=true`
global gesetzt (`C:\Program Files\Git\etc\gitconfig`), und die `Web/`-
Testdateien haben **gemischte** EOL- und Trailing-Newline-Status im HEAD
(`RazorAnalyzerTests.Extended.cs` = CRLF + kein Trailing-NL,
`RazorAnalyzerTests.cs` = LF + kein Trailing-NL, die übrigen 3 = LF +
Trailing-NL). Beim ersten Edit-Versuch konvertierte das Edit-Tool alle
Dateien auf CRLF + Trailing-NL, was den Git-Diff für `Extended.cs` zu
einer 577-Zeilen-Diff-Statistik aufblähte (zwar semantisch korrekt, aber
stark irreführend im Review). Die finale Implementation nutzte einen
kleinen Python-Helper (`_add_traits.py`, temporär, untracked, nicht
committet), der die Original-EOL und Trailing-NL-Status byte-für-byte
bewahrt — die resultierende Diff-Statistik ist `5 files changed, 5
insertions(+)`, exakt wie geplant. Der Helper wurde nach dem Edit
wieder entfernt (über `mavis-trash`).

## Beobachtungen

1. **Pre-Existing Flaky Test reproduziert (EPIC-06-relevant, nicht
   step-004-verursacht):** Der Integration-Filter-Lauf flake-te in den
   ersten zwei Anläufen mit dem bekannten Test
   `McpServerCommandLoadingStateTests.LoadState_LoadFuncCompletesSynchronouslyWithCatalog_ReportsLoadedImmediately`.
   Im dritten Anlauf grün. Konsistent mit der step-002/step-003-
   Beobachtung und der im Plan dokumentierten "best-effort, ein Lauf
   grün"-Klausel. Dieser Step berührt weder Test-Logik noch
   Parallelität, ist also nicht ursächlich — die Wurzel liegt in EPIC-06.

2. **Heuristik-Bestätigung `null!` als Edge-Input (kein Subprozess):**
   `RazorAnalyzerExtendedTests.cs:30` enthält
   `RazorAnalyzer.Analyze(null!, ...)` — bewusster Edge-Input-Test, kein
   Hinweis auf Subprozess. Passt zur bestehenden Heuristik und zur im
   Plan dokumentierten Negativ-Abgrenzung "in-process API-Edge-Test"
   (analog zu step-002's `DisableAllCliTests` mit Subprozess — dort aber
   Integration). Die Heuristik "Subprozess-Marker = Integration" bleibt
   unverändert; keine Erweiterung nötig.

3. **Homogenster Batch der EPIC-02-Serie:** 5/5 Klassen mit XML-Doc
   (step-002: 7/8, step-003: 3/7). Damit ist die Trait-Platzierung
   `"zwischen </summary> und public sealed class"` in diesem Batch die
   einzige vorkommende Variante — keine Mixed-Doc-Sonderbehandlung
   nötig. Folgt der in step-002 etablierten `IgnoreSuppressionsFilter`-
   Konvention.

4. **CodeMap-Korrektur in diesem Step umgesetzt:** Der in step-002
   angelegte `codemap.md`-Eintrag für `Web/` listete nur 4 Klassen und
   hatte `RazorAnalyzerExtendedTests` vergessen. Diese Korrektur ist im
   selben Step (step-004) umgesetzt worden, der die 5. Klasse nun
   nachzieht — der Eintrag ist jetzt vollständig und mit "zuletzt:
   step-004" abgeschlossen. Kein neues Anti-Loop-Risiko für
   Folge-Planer.

5. **EOL-/Trailing-NL-Konservierung (siehe „Abweichungen vom Plan"):**
   Bei Dateien mit gemischtem EOL/Trailing-NL-Status im HEAD wäre ein
   naiver Edit-Tool-Einsatz in einem riesigen Diff-Stat-Churn geendet
   (577 Zeilen für eine 1-Zeilen-Änderung). Der byte-genaue Python-
   Helper war hier notwendig; der Helper selbst ist untracked und
   wurde nach dem Edit wieder entfernt. Falls in einem Folge-Step
   erneut ein Datei-Set mit gemischtem EOL-Status im HEAD angetroffen
   wird (z. B. `Core/Checkers/`), wäre eine vergleichbare Vorab-Prüfung
   sinnvoll. **Nicht** als Tech-Debt-Eintrag deklariert (kein
   wiederholbarer struktureller Mangel, sondern einmaliger
   implementationsspezifischer Workaround) — Hinweis an den Kritiker im
   Beobachtungen-Abschnitt.

6. **Long-Running Test in der vollen Suite:** Der Test
   `McpTestClientParallelTests.ConnectAsync_SixteenParallelCalls_AllSucceedOrFailCleanly`
   lief im Volllauf 1 m 38 s — wie in step-002/003 erwartet (MCP-
   Parallelitäts-Stresstest), kein Hinweis auf Schritt-Verursachung.

## Modell-Info

- **Modell:** MiniMax-M3
- **Knowledge Cutoff:** 2026-01
- **Coder-Agent:** Standard-Drift-Loop-Coder (Mavis / mavis-runtime)
- **Workspace:** `C:\Daten\Entwicklung\Ralf\AiNetLinter`
- **Branch:** `main` (kein Push durchgeführt)
- **Datum:** 2026-08-07
