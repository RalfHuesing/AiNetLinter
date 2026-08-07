---
status: done (pending audit)
type: step-result
task: flaky-and-test-performance
step: 005
epic: EPIC-02
step_type: batch
coded_by: coder
coded_by_model: MiniMax-M3
coded_by_model_knowledge_cutoff: 2026-01
coded_at: 2026-08-07T11:30:00+02:00
code_commit_hash: b15a198
status_after: done
blocker_category: n/a
---

# Result Step 005: Category-Traits für 4 kleine Unit-Ordner (Arch/Diag/FP/Cache, 7 Klassen, Batch 4)

## Zusammenfassung

Alle 7 Testklassen in den 4 Ordnern `Architecture/`, `Diagnostics/`,
`FalsePositives/`, `Cache/` wurden mit `[Trait("Category", "Unit")]` auf
Klassen-Ebene versehen. Trait-Platzierung in 4 Varianten umgesetzt
(alle aus dem Plan):
**3×** direkt über `public sealed class` (kein XML-Doc), **2×** zwischen
`</summary>` und Klasse (XML-Doc), **1×** zwischen `// @covers`-Block
und Klasse, **1×** zwischen `</summary>` und Klasse bei
`AnalysisCacheManagerIsolationTests` (Kombination XML-Doc +
`: IDisposable` + 4 bestehende method-level Traits additiv ergänzt).
Gesamt-Diff: **7 files changed, 7 insertions(+)** — exakt wie im Plan
vorgegeben.

Klassifikations-Heuristik: alle 7 Klassen sind homogen `Unit` (kein
Subprozess-Marker im 4-Ordner-Set — verifiziert per
`McpTestClient`/`CliProcessRunner`/`Program.Main`/
`IClassFixture<McpLiveRepositoryFixture>`-Grep durch den Planer, 0/0/0/0
Treffer). Heuristik-Fortschreibung Punkt 4 (neu): Klassen-Trait additiv
zu bestehenden method-level Traits bei homogenen Klassen — die 4
vorhandenen `[Trait("Category", "Unit")]` auf Methoden-Ebene in
`AnalysisCacheManagerIsolationTests.cs` (Z. 28, 48, 66, 86) bleiben
**unverändert** erhalten; der Klassen-Trait ist rein additiv (xUnit-Trait-
Filter wertet Klassen-Oder-Methoden-Trait, also keine Doppelt-Zählung).

## Geänderte Dateien

```
src/AiNetLinter.Tests/Architecture/ArchitectureTests.cs                 | 1 +
src/AiNetLinter.Tests/Cache/AnalysisCacheManagerIsolationTests.cs       | 1 +
src/AiNetLinter.Tests/Cache/AnalysisCacheManagerTests.cs                | 1 +
src/AiNetLinter.Tests/Cache/CacheEntryMapperTests.cs                    | 1 +
src/AiNetLinter.Tests/Diagnostics/PerformanceProfilerTests.cs           | 1 +
src/AiNetLinter.Tests/FalsePositives/FalsePositiveExtensionsTests.cs    | 1 +
src/AiNetLinter.Tests/FalsePositives/FalsePositiveTests.cs              | 1 +
7 files changed, 7 insertions(+)
```

Pro Item hinzugefügt: 1 Trait-Zeile. Gesamt-Diff: 7 Zeilen.

- **item-01** (`ArchitectureTests`): `[Trait("Category", "Unit")]` direkt
  über `public sealed class ArchitectureTests` (Z. 9, kein XML-Doc).
- **item-02** (`PerformanceProfilerTests`): Trait zwischen letztem
  `// @covers ProfilerSummary` (Z. 15) und `public sealed class` (Z. 16);
  `// @covers`-Marker bleiben **direkt** am Symbol. Datei mit UTF-8-BOM
  (verifiziert nach Edit: erster Byte-Trippel `EF BB BF`).
- **item-03** (`FalsePositiveTests`): Trait zwischen `</summary>` (Z. 15)
  und `public sealed class` (Z. 16). Datei mit UTF-8-BOM (verifiziert).
- **item-04** (`FalsePositiveExtensionsTests`): dito, `</summary>` (Z. 17)
  → `public sealed class` (Z. 18). Datei mit UTF-8-BOM (verifiziert).
- **item-05** (`AnalysisCacheManagerTests`): Trait direkt über
  `public sealed class AnalysisCacheManagerTests : IDisposable` (Z. 14,
  kein XML-Doc; `IDisposable` ändert nichts an Unit-Klassifikation —
  analog zu `MaxDirectoryChildrenTests` aus step-003).
- **item-06** (`AnalysisCacheManagerIsolationTests`): Trait zwischen
  `</summary>` (Z. 20) und `public sealed class … : IDisposable` (Z. 21).
  **Spezialfall:** 4 bestehende method-level `[Trait("Category", "Unit")]`
  (Z. 28, 48, 66, 86) bleiben **unverändert** erhalten (verifiziert per
  `Select-String` nach Edit: 4 method-level + 1 Klassen-Level = 5
  `[Trait(`-Vorkommen in der Datei, exakt wie erwartet).
- **item-07** (`CacheEntryMapperTests`): Trait direkt über
  `public sealed class` (Z. 14, kein XML-Doc).

## Commit

- **Code-Commit-Hash:** `b15a198` (full: `b15a1985c1f67b6794e45c004e3a3c4737686655`)
- **Message:**
  ```
  test: 4 Unit-Ordner Kategorie-taggen [flaky-and-test-performance]

  Refs: tasks/flaky-and-test-performance/step-005
  ```
- **Branch:** `main`
- **Push:** nein (lokal)
- **Doku-Commit:** separater, zweiter Commit (Hash steht nicht hier
  drin — Selbstbezug, siehe `git log`).

**Subject-Länge:** 65 Zeichen (inkl. `[flaky-and-test-performance]`-Suffix),
**exakt wie im DoD vorgegeben** — verifiziert per
`('test: 4 Unit-Ordner Kategorie-taggen [flaky-and-test-performance]').Length`
→ `65`. 7 Zeichen Sicherheitsabstand zur 72-Zeichen-Grenze. Subject wurde
**unverändert** aus dem Plan übernommen (TD-002-Disziplin-Variante (a)
"Planer gibt Subject konkret vor" — analog zu step-004).

## Build-/Test-Output

```
dotnet build                                                                                       → grün (0 Warnungen, 0 Fehler, 4.79 s)
dotnet test --no-build (voll)                                                                       → grün (1325/1325, 0 Fehler, 1 m 49 s)
dotnet test --no-build --filter "Category=Unit"                                                    → grün (332/332, 0 Fehler, 14 s)
dotnet test --no-build --filter "Category=Integration" (1. Anlauf)                                  → grün (113/113, 0 Fehler, 1 m 58 s)
dotnet run --project src/AiNetLinter -- --config rules.json --path .                                → OK
```

## Numerische Plausibilitätsprüfung

Regex-basierte Zählung (gemäß step-003-Review NITPICK "regex statt manuell
zählen") per `Select-String -Pattern '\[Fact\]'` über die 7 Dateien:

| Datei                                          | `[Fact]`-Count | `[Theory]`-Count |
|------------------------------------------------|---------------:|-----------------:|
| `Architecture/ArchitectureTests.cs`            |             13 |                0 |
| `Diagnostics/PerformanceProfilerTests.cs`      |              3 |                0 |
| `FalsePositives/FalsePositiveTests.cs`         |             15 |                0 |
| `FalsePositives/FalsePositiveExtensionsTests.cs` |           12 |                0 |
| `Cache/AnalysisCacheManagerTests.cs`           |              7 |                0 |
| `Cache/AnalysisCacheManagerIsolationTests.cs`  |              4 |                0 |
| `Cache/CacheEntryMapperTests.cs`               |              4 |                0 |
| **Summe**                                      |        **58**  |           **0**  |

Regex-basierte Trait-Zählung in `AnalysisCacheManagerIsolationTests.cs`:
`Select-String -Pattern '\[Trait\('` → **5** Treffer = 4 method-level
(unverändert) + 1 Klassen-Level (neu in step-005).

| Lauf                                          | Total | Passed | Failed | Skipped | Dauer     |
|-----------------------------------------------|------:|-------:|-------:|--------:|-----------|
| `dotnet test --no-build` (voll)               |  1325 |  1325  | 0      | 0       | 1 m 49 s  |
| `dotnet test --no-build --filter "Category=Unit"`       |   332 |   332  | 0      | 0       |   14 s    |
| `dotnet test --no-build --filter "Category=Integration"` |   113 |   113  | 0      | 0       | 1 m 58 s  |

- **Erwartet nach step-004:** Unit=278, Integration=113, Total=1325
- **Erwartet nach step-005:** Unit=278+54=332, Integration=113, Total=1325
  (delta=+54, weil 58 Methoden hinzukommen, davon 4 in
  `AnalysisCacheManagerIsolationTests` schon method-level getaggt → 54
  neu für den Unit-Filter)
- **Tatsächlich:** Unit=**332**, Integration=**113**, Total=**1325** → **Delta 54** ✅
- Differenz (54−54=0): keine — die Trait-Filter-Zahl deckt sich exakt mit
  der regex-basierten Methoden-Zählung (58 − 4 = 54). Damit ist die
  Klassifikations-Korrektheit aller 7 Klassen (alle = `Unit`) durch zwei
  unabhängige Methoden (statische Grep-Zählung + dynamische Filter-Läufe)
  bestätigt.

## Build-Output (Auszug)

```
AiNetLinter -> C:\Daten\Entwicklung\Ralf\AiNetLinter\src\AiNetLinter\bin\Debug\net10.0\AiNetLinter.dll
AiNetLinter.Tests -> C:\Daten\Entwicklung\Ralf\AiNetLinter\src\AiNetLinter.Tests\bin\Debug\net10.0\AiNetLinter.Tests.dll

Der Buildvorgang wurde erfolgreich ausgeführt.
    0 Warnung(en)
    0 Fehler
Verstrichene Zeit 00:00:04.79
```

Sauber, 0 Warnungen, 0 Fehler. Trait-Attribute folgen der etablierten
Konvention (`[Trait("Category", "Unit")]`, CamelCase-Großbuchstabe),
verifiziert per Grep über die 100+ bestehenden Trait-Vorkommen im Projekt.

## Test-Output (Auszüge)

- **Voll:** `Bestanden! Fehler: 0, erfolgreich: 1325, übersprungen: 0, gesamt: 1325, Dauer: 1 m 49 s`
- **Unit-Filter:** `Bestanden! Fehler: 0, erfolgreich: 332, übersprungen: 0, gesamt: 332, Dauer: 14 s`
- **Integration-Filter (1. Anlauf, grün):** `Bestanden! Fehler: 0, erfolgreich: 113, übersprungen: 0, gesamt: 113, Dauer: 1 m 58 s`

## Self-Lint-Output

```
# Run: 2026-08-07 11:29:30
OK
```

TD-001-konform, semantisch identisch zu `--self-lint`. Sauber.

## EOL-/BOM-/Trailing-NL-Verifikation

Vorab durch den Planer verifiziert: alle 7 Dateien uniform CRLF +
Trailing-NL (3 mit BOM, 4 ohne BOM). **Nach dem Edit erneut per
PowerShell-Byte-Check verifiziert:**

| Datei                                          | BOM  | CRLF | Solo-LF | Trailing-NL | Größe (B) |
|------------------------------------------------|------|------|---------|-------------|----------:|
| `Architecture/ArchitectureTests.cs`            |  ✗   |  ✓   |   ✗     |     ✓       |  10641    |
| `Diagnostics/PerformanceProfilerTests.cs`      |  ✓   |  ✓   |   ✗     |     ✓       |   3832    |
| `FalsePositives/FalsePositiveTests.cs`         |  ✓   |  ✓   |   ✗     |     ✓       |  17661    |
| `FalsePositives/FalsePositiveExtensionsTests.cs` |  ✓  |  ✓   |   ✗     |     ✓       |  15205    |
| `Cache/AnalysisCacheManagerTests.cs`           |  ✗   |  ✓   |   ✗     |     ✓       |   7101    |
| `Cache/AnalysisCacheManagerIsolationTests.cs`  |  ✗   |  ✓   |   ✗     |     ✓       |   5056    |
| `Cache/CacheEntryMapperTests.cs`               |  ✗   |  ✓   |   ✗     |     ✓       |   5555    |

**BOM erhalten** in den 3 BOM-Dateien (erste 3 Bytes `EF BB BF`).
**EOL uniform CRLF, Trailing-NL überall.** Diff-Statistik
`7 files changed, 7 insertions(+)` — exakt 1 Zeile pro Datei, keine
Zeilenende-Drift-Aufblähung (anders als in step-004, wo `Web/`-Dateien
gemischte EOL-Status hatten und ein byte-genauer Python-Helper nötig war).

## Abweichungen vom Plan

**Keine.** Plan-DoD vollständig erfüllt:

- [x] Alle 7 Items umgesetzt (je 1 `[Trait("Category", "Unit")]`-Zeile
      in der vorgegebenen Platzierungs-Variante)
- [x] Bestehende Traits respektiert: 4 method-level Traits in
      `AnalysisCacheManagerIsolationTests` (Z. 28, 48, 66, 86) sind
      **unverändert** erhalten (verifiziert per `Select-String`:
      5 `[Trait(`-Treffer in der Datei = 4 alte + 1 neuer Klassen-Trait)
- [x] BOM-Erhaltung in den 3 BOM-Dateien
      (`PerformanceProfilerTests`, `FalsePositiveTests`,
      `FalsePositiveExtensionsTests`) — verifiziert per
      Byte-Trippel-Check `EF BB BF` nach Edit
- [x] EOL/Trailing-NL-Konservierung: alle 7 Dateien uniform CRLF +
      Trailing-NL (kein `core.autocrlf`-CRLF-Drift)
- [x] `dotnet build` grün (0 Warnungen, 0 Fehler, 4.79 s)
- [x] `dotnet test` (voller Lauf) grün (1325/1325)
- [x] `dotnet test --filter "Category=Unit"` grün (332/332,
      **erwartet 332, exakt**)
- [x] `dotnet test --filter "Category=Integration"` best-effort grün
      (113/113, **1. Anlauf** — kein Flake in diesem Step)
- [x] Self-Lint `OK`
- [x] Numerische Plausibilitätsprüfung: regex-basiert 58 `[Fact]`,
      0 `[Theory]`; Filter-Delta +54 (Unit 278→332) exakt wie erwartet
- [x] Code-Commit auf `main` mit Conventional-Commit-Format
      (Subject 65 Zeichen, exakt wie im DoD vorgegeben — TD-002 erfüllt)
- [x] `step-plan.md` Status auf `done (pending audit)` gesetzt
- [x] `codemap.md` aktualisiert (4 Verzeichnis-Einträge auf
      "zuletzt: step-005", `last_updated` aktualisiert)
- [ ] **Audit** durch planer/reviewer — dieser Step wartet noch auf
      Audit-Freigabe (Status `pending audit`)

## Beobachtungen

1. **EOL-Homogenität erleichtert Edit (im Gegensatz zu step-004):** Der
   Planer hat im Plan-DoD korrekt verifiziert, dass alle 7 Dateien
   uniform CRLF + Trailing-NL haben (3 mit BOM, 4 ohne). Das hat sich
   bewährt — alle 7 Edits gingen mit dem Standard-Edit-Tool durch,
   ohne dass ein byte-genauer Python-Helper nötig war. Diff-Statistik
   exakt `7 files changed, 7 insertions(+)`. **Lehre für Planer**
   (nicht als TD-Eintrag deklariert, sondern als Hinweis im Beobachtungs-
   Kanal): die EOL-Vorab-Prüfung im Planer-Schritt 2 zahlt sich aus und
   ist ein lohnender Schritt für jeden Batch mit ≥ 5 Dateien.

2. **Heuristik-Fortschreibung Punkt 4 praktisch umgesetzt:** Die 4
   bestehenden method-level Traits in `AnalysisCacheManagerIsolationTests`
   (alle `Unit`) wurden unangetastet gelassen; der Klassen-Trait wurde
   additiv hinzugefügt. Filter-Lauf bestätigt: keine Doppelt-Zählung
   (Unit steigt exakt um 54, nicht um 58). Heuristik-Regel etabliert:
   bei homogenen Klassen mit teilweise getaggten Methoden → Klassen-Trait
   zusätzlich setzen, nicht ersetzen. **Relevant für Folge-Batches** mit
   ähnlichen Mustern (z. B. `Commands/McpServerCommandTests.cs` mit 5
   Unit-Methoden + 18 Integration-Methoden in einer Klasse — dort aber
   nicht als Klassen-Trait, sondern als `step_type: single`-Step mit
   pro-Methode-Tagging, da die Klasse **nicht** homogen ist).

3. **BOM-Edit-Sicherheit:** Die 3 BOM-Dateien (`PerformanceProfilerTests`,
   `FalsePositiveTests`, `FalsePositiveExtensionsTests`) haben ihren
   UTF-8-BOM nach dem Edit **erhalten** — verifiziert per
   `[System.IO.File]::ReadAllBytes` und Trippel-Check `EF BB BF` nach
   dem Edit. Das Standard-Edit-Tool erhält den BOM (anders als naive
   `Set-Content -Encoding UTF8` ohne expliziten BOM-Schutz, was den BOM
   strippen würde). Wichtig für die Folge-Batches, weil viele Test-
   Dateien in älteren Ordnern BOM tragen (siehe CodeMap-Hinweise).

4. **Drei neue Trait-Platzierungs-Varianten demonstriert:** Dieser Batch
   zeigt drei neue Lokal-Varianten, die in step-002/003/004 noch nicht
   vorkamen:
   (a) `IDisposable` + XML-Doc auf Klassen-Ebene
       (`AnalysisCacheManagerIsolationTests`) — kombiniert
       `MaxDirectoryChildrenTests`-`IDisposable`-Variante aus step-003
       mit der XML-Doc-Variante;
   (b) Klassen-Trait additiv zu existierenden method-level Traits
       (`AnalysisCacheManagerIsolationTests`) — Heuristik-Punkt 4;
   (c) `// @covers`-Block-Plus-Trait (`PerformanceProfilerTests`) —
       erweitert die step-002-`IgnoreSuppressionsFilter`-Konvention auf
       einen mehrzeiligen `// @covers`-Block (4 Zeilen statt 1).
   Damit ist die Trait-Platzierungs-Bibliothek in der EPIC-02-Serie
   jetzt vollständig dokumentiert: einfach-direkt, XML-Doc, `// @covers`,
   `IDisposable`, `IDisposable+XML-Doc`, `IDisposable+XML-Doc+method-level-Traits`,
   `// @covers`-Block.

5. **Reiner-Unit-Ordner-Block abgeschlossen:** Mit diesem Step ist der in
   step-002 angelegte "Reine-Unit-Ordner, klein"-Block der
   CodeMap-Sektion (4 Ordner: `Architecture/`, `Diagnostics/`,
   `FalsePositives/`, `Cache/`) vollständig abgehakt. Übrig sind die in
   step-002/004/005 dokumentierten "mittel"- und "groß"-Unit-Ordner
   (`Evals/`, `Output/`, `Configuration/`, `Core/`, `Core/Checkers/`,
   `Maps/`, `Mcp/Tools/`) sowie die gemischten Ordner (`Mcp/`, `Baseline/`,
   `Commands/`, `Cli/`). Die CodeMap reflektiert den neuen Stand mit
   aktualisiertem `last_updated`-Datum.

6. **Integration-Filter im 1. Anlauf grün (positiv):** Im Gegensatz zu
   step-002/003/004 (wo der Integration-Filter im 1./2. Anlauf flaky war
   mit dem bekannten pre-existing Test) war der Integration-Filter in
   diesem Step **im 1. Anlauf grün** (113/113). Das ist konsistent mit
   dem nicht-step-005-verursachten Charakter des Flake (EPIC-06-Fix
   ausstehend) — der Flake zeigt sich je nach Systemlast mal früher mal
   später. Kein Hinweis auf step-005-Verursachung.

7. **Long-Running Test in der vollen Suite:** Der Test
   `McpTestClientParallelTests.ConnectAsync_SixteenParallelCalls_AllSucceedOrFailCleanly`
   lief im Volllauf ~1:36 und im Integration-Lauf 1:33 — wie in
   step-002/003/004 erwartet (MCP-Parallelitäts-Stresstest), kein Hinweis
   auf Schritt-Verursachung.

## Modell-Info

- **Modell:** MiniMax-M3
- **Knowledge Cutoff:** 2026-01
- **Coder-Agent:** Standard-Drift-Loop-Coder (Mavis / mavis-runtime)
- **Workspace:** `C:\Daten\Entwicklung\Ralf\AiNetLinter`
- **Branch:** `main` (kein Push durchgeführt)
- **Datum:** 2026-08-07
