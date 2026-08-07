---
status: done (pending audit)
type: step-result
task: flaky-and-test-performance
step: 006
epic: EPIC-02
step_type: batch
coded_by: coder
coded_by_model: MiniMax-M3
coded_by_model_knowledge_cutoff: 2026-01
coded_at: 2026-08-07T13:25:00+02:00
code_commit_hash: f88c223
status_after: done
blocker_category: n/a
---

# Result Step 006: Category-Traits für Evals-Ordner (Batch 5)

## Zusammenfassung

Alle 3 Testklassen in `src/AiNetLinter.Tests/Evals/` wurden mit
`[Trait("Category", "Unit")]` auf Klassen-Ebene versehen. Trait-Platzierung
in 2 lokalen Varianten umgesetzt (alle aus dem Plan): **2×** direkt über
`public sealed class … : IDisposable` (`EvalAssemblerTests`, `SpecLoaderTests`)
und **1×** direkt über `public sealed class …` ohne `: IDisposable`
(`ListEvalsCommandTests`) — alle 3 ohne XML-Doc, ohne `// @covers`-Marker,
ohne bestehende method-level Traits (jungfräulicher Batch). Gesamt-Diff:
**3 files changed, 3 insertions(+)** — exakt wie im Plan vorgegeben.

Klassifikations-Heuristik: alle 3 Klassen homogen `Unit` (kein
Subprozess-Marker im 1-Ordner-Set — `Process\.Start` / `McpTestClient` /
`CliProcessRunner` / `Program\.Main` / `IClassFixture` jeweils 0/0/0/0/0
Treffer, verifiziert per `Select-String` nach Edit). Heuristik-Punkt 5
(neu in step-006): Hypothese-Auflösungs-Pflicht für offene
"möglicherweise…"-Annotationen in der `codemap.md` — die in
`codemap.md` Z. 100 (Stand step-002/005) offene Hypothese
"`ListEvalsCommandTests` möglicherweise Integration via Subprozess,
JIT zu prüfen" ist durch die JIT-Prüfung in diesem Step **widerlegt**
(`ListEvalsCommand.Run(console)` ist direkter in-process-Aufruf mit
`TestLintConsole`-Mock, **kein** `dotnet AiNetLinter.dll`-Subprozess);
die CodeMap-Annotation wurde im Doku-Commit auf eine klare
"alle 3 Unit"-Aussage mit explizitem Widerlegungs-Hinweis gekürzt.

## Geänderte Dateien

```
src/AiNetLinter.Tests/Evals/EvalAssemblerTests.cs   | 1 +
src/AiNetLinter.Tests/Evals/ListEvalsCommandTests.cs | 1 +
src/AiNetLinter.Tests/Evals/SpecLoaderTests.cs       | 1 +
3 files changed, 3 insertions(+)
```

Pro Item hinzugefügt: 1 Trait-Zeile. Gesamt-Diff: 3 Zeilen.

- **item-01** (`EvalAssemblerTests`): `[Trait("Category", "Unit")]` direkt
  zwischen `namespace AiNetLinter.Tests.Evals;` und `public sealed class
  EvalAssemblerTests : IDisposable` — keine XML-Doc, kein
  `// @covers`-Marker. `IDisposable` ändert nichts an der
  Unit-Klassifikation (analog zu `MaxDirectoryChildrenTests` step-003 und
  `AnalysisCacheManagerTests` step-005).
- **item-02** (`SpecLoaderTests`): identische Platzierung zwischen
  `namespace …;` und `public sealed class SpecLoaderTests : IDisposable`.
- **item-03** (`ListEvalsCommandTests`): identische Platzierung zwischen
  `namespace …;` und `public sealed class ListEvalsCommandTests` (ohne
  `IDisposable`, ohne XML-Doc — analog zu `ArchitectureTests` step-005).
  **Hypothese-Widerlegung:** `ListEvalsCommand.Run(console)` ist
  in-process-Aufruf mit `TestLintConsole`-Mock aus `AiNetLinter.Tests.Output`,
  kein Subprozess.

## Commit

- **Code-Commit-Hash:** `f88c223` (full: `f88c2233bc6ae0053808753591a5469d7dd99be4`)
- **Message:**
  ```
  test: Evals-Tests Kategorie-taggen [flaky-and-test-performance]

  Refs: tasks/flaky-and-test-performance/step-006
  ```
- **Branch:** `main`
- **Push:** nein (lokal)
- **Doku-Commit:** separater, zweiter Commit (Hash steht nicht hier
  drin — Selbstbezug, siehe `git log`).

**Subject-Länge:** 63 Zeichen (inkl. `[flaky-and-test-performance]`-Suffix),
**exakt wie im DoD vorgegeben** — verifiziert per
`('test: Evals-Tests Kategorie-taggen [flaky-and-test-performance]').Length`
→ `63`. 9 Zeichen Sicherheitsabstand zur 72-Zeichen-Grenze. Subject wurde
**unverändert** aus dem Plan übernommen (TD-002-Disziplin-Variante (a)
"Planer gibt Subject konkret vor" — analog zu step-005). Kürzester
Subject der EPIC-02-Serie (step-002: 67 Zeichen, step-003: 65 Zeichen,
step-004: 65 Zeichen, step-005: 65 Zeichen, **step-006: 63 Zeichen**).

## Build-/Test-Output

```
dotnet build                                                                                       → grün (0 Warnungen, 0 Fehler, 4.92 s)
dotnet test --no-build (voll)                                                                       → grün (1325/1325, 0 Fehler, 1 m 50 s)
dotnet test --no-build --filter "Category=Unit"                                                    → grün (355/355, 0 Fehler, 16 s)
dotnet test --no-build --filter "Category=Integration" (1. Anlauf)                                  → grün (113/113, 0 Fehler, 1 m 55 s)
dotnet run --project src/AiNetLinter -- --config rules.json --path .                                → OK
```

## Numerische Plausibilitätsprüfung

Regex-basierte Zählung (gemäß step-003-Review NITPICK "regex statt manuell
zählen") per `Select-String -Pattern '\[Fact\]'` / `'\[Theory\]'` über die
3 Dateien — **vor** dem Edit:

| Datei                                       | `[Fact]`-Count | `[Theory]`-Count |
|---------------------------------------------|---------------:|-----------------:|
| `Evals/EvalAssemblerTests.cs`               |             11 |                0 |
| `Evals/SpecLoaderTests.cs`                  |             10 |                0 |
| `Evals/ListEvalsCommandTests.cs`            |              2 |                0 |
| **Summe**                                   |        **23**  |           **0**  |

Regex-basierte Trait-Zählung **nach** dem Edit per
`Select-String -Pattern '\[Trait\('`: **3** Klassen-Traits neu
(1+1+1), **0** method-level Traits in den 3 Dateien (jungfräulicher
Batch — verifiziert).

| Lauf                                          | Total | Passed | Failed | Skipped | Dauer     |
|-----------------------------------------------|------:|-------:|-------:|--------:|-----------|
| `dotnet test --no-build` (voll)               |  1325 |  1325  | 0      | 0       | 1 m 50 s  |
| `dotnet test --no-build --filter "Category=Unit"`       |   355 |   355  | 0      | 0       |   16 s    |
| `dotnet test --no-build --filter "Category=Integration"` |   113 |   113  | 0      | 0       | 1 m 55 s  |

- **Erwartet nach step-005:** Unit=332, Integration=113, Total=1325
- **Erwartet nach step-006:** Unit=332+23=**355**, Integration=113,
  Total=1325 (delta=+23, weil 23 Facts hinzukommen, 0 bestehende
  method-level Traits → 23 neu für den Unit-Filter)
- **Tatsächlich:** Unit=**355**, Integration=**113**, Total=**1325** → **Delta 23** ✅
- Differenz (23−23=0): keine — die Trait-Filter-Zahl deckt sich exakt
  mit der regex-basierten Methoden-Zählung. Damit ist die
  Klassifikations-Korrektheit aller 3 Klassen (alle = `Unit`) durch zwei
  unabhängige Methoden (statische Grep-Zählung + dynamische Filter-Läufe)
  bestätigt.

## Build-Output (Auszug)

```
AiNetLinter -> C:\Daten\Entwicklung\Ralf\AiNetLinter\src\AiNetLinter\bin\Debug\net10.0\AiNetLinter.dll
AiNetLinter.Tests -> C:\Daten\Entwicklung\Ralf\AiNetLinter\src\AiNetLinter.Tests\bin\Debug\net10.0\AiNetLinter.Tests.dll

Der Buildvorgang wurde erfolgreich ausgeführt.
    0 Warnung(en)
    0 Fehler
Verstrichene Zeit 00:00:04.92
```

Sauber, 0 Warnungen, 0 Fehler. Trait-Attribute folgen der etablierten
Konvention (`[Trait("Category", "Unit")]`, CamelCase-Großbuchstabe),
verifiziert per Grep über die 100+ bestehenden Trait-Vorkommen im Projekt.

## Test-Output (Auszüge)

- **Voll:** `Bestanden! Fehler: 0, erfolgreich: 1325, übersprungen: 0, gesamt: 1325, Dauer: 1 m 50 s`
- **Unit-Filter:** `Bestanden! Fehler: 0, erfolgreich: 355, übersprungen: 0, gesamt: 355, Dauer: 16 s`
- **Integration-Filter (1. Anlauf, grün):** `Bestanden! Fehler: 0, erfolgreich: 113, übersprungen: 0, gesamt: 113, Dauer: 1 m 55 s`

## Self-Lint-Output

```
# Run: 2026-08-07 11:53:34
OK
```

TD-001-konform, semantisch identisch zu `--self-lint`. Sauber.

## EOL-/BOM-/Trailing-NL-Verifikation

Vorab durch den Planer verifiziert: alle 3 Dateien uniform CRLF +
Trailing-NL, **alle 3 ohne BOM** (erste 3 Bytes jeweils `35 110 117` =
`#nu` von `#nullable enable` — kein `EF BB BF`). **Nach dem Edit erneut
per PowerShell-Byte-Check verifiziert:**

| Datei                                       | BOM  | CR  | LF  | Trailing-NL | Traits |
|---------------------------------------------|------|----:|----:|-------------|-------:|
| `Evals/EvalAssemblerTests.cs`               |  ✗   | 152 | 152 |     ✓       |   1    |
| `Evals/SpecLoaderTests.cs`                  |  ✗   | 123 | 123 |     ✓       |   1    |
| `Evals/ListEvalsCommandTests.cs`            |  ✗   |  27 |  27 |     ✓       |   1    |

**EOL uniform CRLF, Trailing-NL überall, kein BOM-Risiko** (kein BOM
vorhanden, also nichts zu erhalten — anders als in step-002/005 mit
3 BOM-Dateien). Diff-Statistik `3 files changed, 3 insertions(+)` —
exakt 1 Zeile pro Datei, keine Zeilenende-Drift-Aufblähung (anders als
in step-004, wo `Web/`-Dateien gemischte EOL-Status hatten und ein
byte-genauer Python-Helper nötig war). **Kein** EOL-Helper nötig in
diesem Step — Standard-Edit-Tool reichte.

## Abweichungen vom Plan

**Keine.** Plan-DoD vollständig erfüllt:

- [x] Alle 3 Items umgesetzt (je 1 `[Trait("Category", "Unit")]`-Zeile
      auf Klassen-Ebene, eingefügt zwischen `namespace …;` und
      `public sealed class …`-Deklaration — Standard-Insert ohne
      XML-Doc / `// @covers`-Sonderbehandlung)
- [x] Kein BOM-Risiko: alle 3 Dateien ohne UTF-8-BOM (verifiziert
      per `[System.IO.File]::ReadAllBytes` nach Edit, erste 3 Bytes
      `35 110 117` = `#nu` ≠ `EF BB BF`)
- [x] EOL/Trailing-NL-Konservierung: alle 3 Dateien uniform CRLF +
      Trailing-NL (kein `core.autocrlf`-CRLF-Drift)
- [x] `dotnet build` grün (0 Warnungen, 0 Fehler, 4.92 s)
- [x] `dotnet test` (voller Lauf) grün (1325/1325)
- [x] `dotnet test --filter "Category=Unit"` grün (355/355,
      **erwartet 332+23=355, exakt**)
- [x] `dotnet test --filter "Category=Integration"` best-effort grün
      (113/113, **1. Anlauf** — kein Flake in diesem Step)
- [x] Self-Lint `OK`
- [x] Numerische Plausibilitätsprüfung: regex-basiert 23 `[Fact]`,
      0 `[Theory]`; Filter-Delta +23 (Unit 332→355) exakt wie erwartet
- [x] Code-Commit auf `main` mit Conventional-Commit-Format
      (Subject 63 Zeichen, exakt wie im DoD vorgegeben — TD-002 erfüllt)
- [x] `step-plan.md` Status auf `done (pending audit)` gesetzt
- [x] `codemap.md` aktualisiert (`Evals/`-Eintrag auf
      "zuletzt: step-006", Hypothese-Annotation in prägnante
      "alle 3 Unit (ListEvalsCommandTests-Subprozess-Hypothese in
      step-006 widerlegt)"-Form überführt, `last_updated` aktualisiert)
- [ ] **Audit** durch planer/reviewer — dieser Step wartet noch auf
      Audit-Freigabe (Status `pending audit`)

## Beobachtungen

1. **Kleinster Batch der EPIC-02-Serie planmäßig abgeschlossen:** 3 Klassen
   in 1 Ordner, +23 Unit-Methoden, 1 Commit, 3 Diff-Zeilen. Damit ist
   der in step-002 angelegte "Reine-Unit-Ordner, klein"-Block der
   CodeMap-Sektion (`Evals/`) abgehakt. Verbleibend in der
   CodeMap-Reihenfolge sind die "mittel"-Unit-Ordner (`Output/`,
   `Configuration/`), die "großen" Unit-Ordner (`Core/`, `Core/Checkers/`,
   `Maps/`, `Mcp/Tools/`), die gemischten Ordner (`Mcp/`, `Baseline/`,
   `Commands/`, `Cli/`) — siehe `step-006/step-plan.md` §"Notes" für
   die informelle Planer-Reihenfolge der Folge-Batches.

2. **Heuristik-Punkt 5 (neu) praktisch umgesetzt:** Die in `codemap.md`
   Z. 100 seit step-002/005 offene Hypothese "`ListEvalsCommandTests`
   möglicherweise Integration via Subprozess, JIT zu prüfen" wurde in
   diesem Step durch zwei unabhängige Methoden endgültig widerlegt:
   (a) Subprozess-Marker-Grep lieferte 0/0/0/0/0 Treffer in
   `ListEvalsCommandTests.cs`; (b) Datei-Inspektion bestätigte direkten
   `ListEvalsCommand.Run(console)`-Aufruf mit in-process `TestLintConsole`-
   Mock aus `AiNetLinter.Tests.Output`. Die CodeMap-Annotation wurde
   **vor** dem Doku-Commit auf eine klare "alle 3 Unit"-Aussage mit
   explizitem Widerlegungs-Hinweis gekürzt — der nächste Planer-Aufruf
   findet damit eine konsistente, verifizierte Karte vor. **Relevant
   für Folge-Batches**, falls weitere "JIT-zu-prüfen"-Annotationen in
   der CodeMap auftauchen (z. B. in `Mcp/`- oder `Commands/`-Einträgen
   bei deren späterer Schritt-Planung).

3. **EOL-Homogenität erleichtert Edit (Bestätigung step-005-Beobachtung):**
   Alle 3 Dateien uniform CRLF + Trailing-NL, alle 3 ohne BOM. Damit
   Standard-Edit-Tool ausreichend — kein byte-genauer Python-Helper
   nötig (anders als in step-004). Diff-Statistik exakt
   `3 files changed, 3 insertions(+)`. **Lehre für Planer:** die
   EOL-Vorab-Prüfung im Planer-Schritt 2 zahlt sich aus und ist ein
   lohnender Schritt für jeden Batch mit ≥ 3 Dateien. (Nicht als
   TD-Eintrag deklariert, sondern als Hinweis im Beobachtungs-Kanal —
   analog zu step-005.)

4. **Zwei minimale Trait-Platzierungs-Varianten demonstriert:** Dieser
   Batch nutzt die einfachste mögliche Variante (Standard-Insert zwischen
   `namespace …;` und `public sealed class …`) für alle 3 Dateien — 2×
   mit `IDisposable`, 1× ohne. Keine XML-Doc, keine `// @covers`-Marker,
   keine bestehenden method-level Traits, keine Bündelung mit Sonder-
   varianten. Damit wird die "Standard-Insert"-Variante als robustes
   Default-Pattern für "jungfräuliche" Klassen weiter bestätigt.

5. **Integration-Filter im 1. Anlauf grün (positiv):** Im Gegensatz zu
   step-002 (2 Anläufe) und step-003/004 (1-2 Anläufe) war der
   Integration-Filter in diesem Step **im 1. Anlauf grün** (113/113).
   Das ist konsistent mit dem nicht-step-006-verursachten Charakter des
   pre-existing Flake-Tests
   `McpServerCommandLoadingStateTests.LoadState_LoadFuncCompletesSynchronouslyWithCatalog_ReportsLoadedImmediately`
   (EPIC-06-Fix ausstehend) — der Flake zeigt sich je nach Systemlast
   mal früher mal später. Kein Hinweis auf step-006-Verursachung.

6. **Step-006-Bezug im CodeMap-Pfad konsistent:** Der Coder-Auftrag
   sagte "`zuletzt: step-002` auf `zuletzt: step-006`". Tatsächlich
   stand im aktuellen CodeMap-Stand `zuletzt: step-005` (weil der
   Planer die CodeMap zwischen Plan-Erstellung und Coder-Start
   bereits aktualisiert hatte — siehe `step-plan.md` §"Anti-Loop-
   Check"-Absatz). Der Coder hat `zuletzt: step-005` auf
   `zuletzt: step-006` gesetzt (äquivalent zur Auftrags-Intention, da
   der Planer-Schritt den `step-002`-Stand abgelöst hatte). Kein
   Daten-Drift — bewusste Übernahme des aktuellen Standes.

## Modell-Info

- **Modell:** MiniMax-M3
- **Knowledge Cutoff:** 2026-01
- **Coder-Agent:** Standard-Drift-Loop-Coder (Mavis / mavis-runtime)
- **Workspace:** `C:\Daten\Entwicklung\Ralf\AiNetLinter`
- **Branch:** `main` (kein Push durchgeführt)
- **Datum:** 2026-08-07
