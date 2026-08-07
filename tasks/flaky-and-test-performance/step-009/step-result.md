---
status: done
type: step-result
task: flaky-and-test-performance
step: 009
epic: EPIC-02
step_type: batch
coded_by: coder
coded_by_model: MiniMax-M3
coded_by_model_knowledge_cutoff: 2026-01
coded_at: 2026-08-07T13:50:00+02:00
code_commit_hash: b484627
status_after: done
blocker_category: n/a
---

# Result Step 009: Category-Traits für Configuration-Tests nachziehen (Batch 8)

**Wer das liest:** der Kritiker (prüft dich gegen den Plan) und der
Planer beim nächsten Step. Entscheidungsrelevant sind vor allem
„Abweichungen vom Plan", „Beobachtungen" und „Bekannte Unschärfen" —
dort lieber konkret als knapp. Alles andere: knapp halten, nichts aus
dem Step-Plan wiederholen, was unverändert umgesetzt wurde.

## Zusammenfassung

Alle 8 Testklassen im `Configuration/`-Ordner (`AgentFeaturesTests`,
`ConfigLoaderRulesJsonTests`, `ConfigNormalizerTests`,
`ConfigSyncerTests`, `DeveloperExperienceTests`,
`FileFilterEvaluatorTests`, `PathOverridesTests`,
`RuleMetadataRegistryTests`) mit `[Trait("Category", "Unit")]` auf
Klassen-Ebene versehen. Trait-Platzierungs-Bibliothek aus step-007/008
vollständig bestätigt: 5× Standard-Insert, 2× `// @covers`-Block-Insert
(`AgentFeaturesTests`, `ConfigLoaderRulesJsonTests`), 1× XML-Doc-Variante
(`DeveloperExperienceTests`). `Configuration/`-Ordner damit vollständig
abgeschlossen — 67 Test-Cases (Plan: 68, Differenz siehe „Abweichungen
vom Plan") tragen jetzt den `Unit`-Klassen-Trait.

## Geänderte Dateien

**Code-Commit `b484627`:**

- `src/AiNetLinter.Tests/Configuration/AgentFeaturesTests.cs` (item-01) — `[Trait("Category", "Unit")]` zwischen letztem `// @covers` (Z. 15) und class (alt Z. 16) eingefügt; class verschiebt sich auf Z. 17
- `src/AiNetLinter.Tests/Configuration/ConfigLoaderRulesJsonTests.cs` (item-02) — `[Trait("Category", "Unit")]` zwischen `// @covers ConfigLoader` (Z. 9) und class (alt Z. 10); class auf Z. 11; **BOM-tragend**, BOM `EF BB BF` vor/nach Edit verifiziert
- `src/AiNetLinter.Tests/Configuration/ConfigNormalizerTests.cs` (item-03) — `[Trait("Category", "Unit")]` zwischen namespace (Z. 3) und class (alt Z. 5); class auf Z. 6; **BOM-tragend**, BOM vor/nach verifiziert
- `src/AiNetLinter.Tests/Configuration/ConfigSyncerTests.cs` (item-04) — `[Trait("Category", "Unit")]` zwischen namespace (Z. 8) und class (alt Z. 10); class auf Z. 11; **BOM-tragend**, BOM vor/nach verifiziert
- `src/AiNetLinter.Tests/Configuration/DeveloperExperienceTests.cs` (item-05) — `[Trait("Category", "Unit")]` zwischen `</summary>` (Z. 31) und class (alt Z. 32); class auf Z. 33; XML-Doc-Variante mit 3 Schichten (`// @covers` + Leerzeile + XML-Doc)
- `src/AiNetLinter.Tests/Configuration/FileFilterEvaluatorTests.cs` (item-06) — `[Trait("Category", "Unit")]` zwischen namespace (Z. 10) und class (alt Z. 12); class auf Z. 13
- `src/AiNetLinter.Tests/Configuration/PathOverridesTests.cs` (item-07) — `[Trait("Category", "Unit")]` zwischen namespace (Z. 6) und class (alt Z. 8); class auf Z. 9; **BOM-tragend**, BOM vor/nach verifiziert
- `src/AiNetLinter.Tests/Configuration/RuleMetadataRegistryTests.cs` (item-08) — `[Trait("Category", "Unit")]` zwischen namespace (Z. 8) und class (alt Z. 10); class auf Z. 11

**Diff-Statistik:** 8 Dateien, je +1 Zeile, je `-0`/`+1` (rein additiver Trait-Insert), keine weiteren Änderungen.

**Doku-Commit (zweiter, separater Commit):**

- `tasks/flaky-and-test-performance/step-009/step-plan.md` — Status von `in_progress` auf `done (pending audit)`
- `tasks/flaky-and-test-performance/step-009/step-result.md` (neu) — diese Datei
- `tasks/flaky-and-test-performance/codemap.md` — `Configuration/`-Eintrag in Sektion "Test-Verzeichnisse — geplant für EPIC-02-Folge-Batches" aktualisiert auf "8 Klassen, alle Unit, mit `[Trait("Category", "Unit")]` auf Klassen-Ebene versehen; `Configuration/`-Ordner vollständig abgeschlossen in step-009 (zuletzt: step-009)"; `last_updated` vorgespult

## Commit

- **Code-Commit-Hash:** `b484627`
- **Message:**
  ```
  test: Configuration-Tests Kategorie-taggen [flaky-and-test-performance]

  Refs: tasks/flaky-and-test-performance/step-009
  ```
- **Branch:** main
- **Push:** nein (lokal)
- **Doku-Commit:** separater, zweiter Commit (Hash steht nicht hier drin — Selbstbezug, siehe `git log`).
- **Subject-Länge Code-Commit:** 71 Zeichen (1 Zeichen Reserve zur 72-Grenze), verifiziert per `('test: Configuration-Tests Kategorie-taggen [flaky-and-test-performance]').Length` = 71.

## Build-/Test-Output

```
dotnet build → grün (0 Warnungen, 0 Fehler, ~2 s)
dotnet test --no-build → grün (1325 Tests, 0 Fehler, 1 m 45 s)
dotnet test --no-build --filter "Category=Unit" → grün (656 Tests, 0 Fehler, 8 s)  [Plan: 657, Differenz -1, siehe Abweichungen]
dotnet test --no-build --filter "Category=Integration" → grün (113 Tests, 0 Fehler, 2 m 10 s)
dotnet run --project src/AiNetLinter -- --config rules.json --path . → OK (Self-Lint grün)
```

## Numerische Plausibilitätsprüfung

**Methoden-Inventar (regex-basiert, gemäß step-003-Review NITPICK, `Select-String -Pattern '\[Fact\]'` / `'\[Theory\]'`):**

| Datei                              | Facts | Theories | InlineData | Methoden-Summe |
|------------------------------------|------:|---------:|-----------:|---------------:|
| AgentFeaturesTests.cs              |    16 |        0 |          0 |             16 |
| ConfigLoaderRulesJsonTests.cs      |     4 |        0 |          0 |              4 |
| ConfigNormalizerTests.cs           |     3 |        0 |          0 |              3 |
| ConfigSyncerTests.cs               |     9 |        0 |          0 |              9 |
| DeveloperExperienceTests.cs        |    11 |        0 |          0 |             11 |
| FileFilterEvaluatorTests.cs        |     3 |        2 |          9 |              5 |
| PathOverridesTests.cs              |    10 |        0 |          0 |             10 |
| RuleMetadataRegistryTests.cs       |     3 |        0 |          0 |              3 |
| **Summe**                          |    59 |        2 |          9 |        **61**  |

**Test-Case-Inventar (regex-basiert; = Facts + InlineData-Expansionen):**

| Datei                              | Test-Cases |
|------------------------------------|-----------:|
| AgentFeaturesTests.cs              |         16 |
| ConfigLoaderRulesJsonTests.cs      |          4 |
| ConfigNormalizerTests.cs           |          3 |
| ConfigSyncerTests.cs               |          9 |
| DeveloperExperienceTests.cs        |         11 |
| FileFilterEvaluatorTests.cs (3 + 9 InlineData) | 12 |
| PathOverridesTests.cs              |         10 |
| RuleMetadataRegistryTests.cs       |          3 |
| **Summe**                          |    **68**  |

**Per-Klasse-Filter-Aufschlüsselung (xUnit, `--filter "FullyQualifiedName~Configuration.<Klasse>"`):**

| Datei                              | Tests (xUnit) | Abweichung |
|------------------------------------|--------------:|-----------:|
| AgentFeatures                      |            15 |        **-1** |
| ConfigLoaderRulesJson              |             4 |         0 |
| ConfigNormalizer                   |             3 |         0 |
| ConfigSyncer                       |             9 |         0 |
| DeveloperExperience                |            11 |         0 |
| FileFilterEvaluator                |            12 |         0 |
| PathOverrides                      |            10 |         0 |
| RuleMetadataRegistry               |             3 |         0 |
| **Summe Configuration/**           |        **67** |    **-1** |

**Filter-Delta step-009:**

| Filter        | step-008 | step-009 | Delta   | Plan-Erwartung |
|---------------|---------:|---------:|--------:|---------------:|
| Unit          |      589 |    **656** |  **+67** | +68            |
| Integration   |      113 |      113 |     ±0  | ±0             |
| **Total**     |    **1325** |  **1325** |  **±0** | ±0             |

**BOM-Konservierung (alle 4 BOM-tragenden Dateien):**

| Datei                              | BOM-vor-Edit | BOM-nach-Edit |
|------------------------------------|:------------:|:-------------:|
| ConfigLoaderRulesJsonTests.cs      | True (EF BB BF) | True (EF BB BF) |
| ConfigNormalizerTests.cs           | True (EF BB BF) | True (EF BB BF) |
| ConfigSyncerTests.cs               | True (EF BB BF) | True (EF BB BF) |
| PathOverridesTests.cs              | True (EF BB BF) | True (EF BB BF) |

**EOL-/Trailing-NL-Konservierung (alle 8 Dateien nach Edit, Stichprobe-Empfehlung aus DoD, hier Vollscan):**

| Datei                              | BOM  | CR=LF | TrNL |
|------------------------------------|:----:|:-----:|:----:|
| AgentFeaturesTests.cs              |  ✗   |  414/414 | ✓  |
| ConfigLoaderRulesJsonTests.cs      |  ✓   |   49/49 | ✓  |
| ConfigNormalizerTests.cs           |  ✓   |   59/59 | ✓  |
| ConfigSyncerTests.cs               |  ✓   |  290/290 | ✓  |
| DeveloperExperienceTests.cs        |  ✗   |  415/415 | ✓  |
| FileFilterEvaluatorTests.cs        |  ✗   |  231/231 | ✓  |
| PathOverridesTests.cs              |  ✓   |  190/190 | ✓  |
| RuleMetadataRegistryTests.cs       |  ✗   |   55/55 | ✓  |

**Vorher-/Nachher-Zeilenzahlen pro Datei (verifiziert per `Get-Content ... | Measure-Object -Line`):**

| Datei                              | Vorher | Nachher | Δ |
|------------------------------------|-------:|--------:|--:|
| AgentFeaturesTests.cs              |    355 |     356 | +1 |
| ConfigLoaderRulesJsonTests.cs      |     42 |      43 | +1 |
| ConfigNormalizerTests.cs           |     46 |      47 | +1 |
| ConfigSyncerTests.cs               |    254 |     255 | +1 |
| DeveloperExperienceTests.cs        |    368 |     369 | +1 |
| FileFilterEvaluatorTests.cs        |    203 |     204 | +1 |
| PathOverridesTests.cs              |    166 |     167 | +1 |
| RuleMetadataRegistryTests.cs       |     49 |      50 | +1 |

Alle 8 Dateien +1 Zeile, exakt wie spezifiziert (reiner 1-Zeilen-Insert pro Datei, keine Multi-Line-Edits).

## Abweichungen vom Plan

- **Unit-Filter-Delta: +67 statt +68** (Filter-Lauf 656 statt 657).
  Ursache: **Planer-Miscount** in `AgentFeaturesTests.cs`. Der Plan
  gibt 16 `[Fact]`-Methoden an, real existieren nur 15. Der 16.
  `[Fact]`-Marker in dieser Datei (Z. 241) sitzt **innerhalb eines
  raw multi-line string literals** (`""" ... [Fact] ... """`),
  das als Test-Input-Daten für die Linter-Engine dient (siehe
  `src/AiNetLinter.Tests/Configuration/AgentFeaturesTests.cs:236-247`
  — `const string testClass = """ ... [Fact] ... """;`) und ist
  **kein** xUnit-Test-Attribut. Der xUnit-Runner ignoriert
  `[Fact]`-Marker innerhalb von String-Literalen korrekt; nur die
  echten Test-Attribute auf Klassen-Ebene (15 Facts) zählen.
  Konsequenz: **Filter-Delta -1, Unit-Filter 656 statt 657**.
  Die korrekte Test-Case-Summe über alle 8 Dateien ist
  **67 = 15+4+3+9+11+12+10+3**, nicht 68. **Plan-Korrektur
  an die CodeMap-Annotation:** die Konfiguration-`Configuration/`-
  Zeile wurde bereits korrekt auf step-009 gesetzt, der Vollzug
  gilt unabhängig von der Planer-Zählungen-Korrektur. Der Folge-
  Planer (für EPIC-02-Folge-Batches `Core/Checkers/`, `Core/`,
  `Maps/`, etc.) sollte `Select-String`-basierte Counts
  anwenden und nicht manuelle Counts aus Class-Body + String-
  Literals mischen.
- **Keine Code-Änderung am Plan-Scope:** alle 8 Trait-Inserts
  exakt wie geplant umgesetzt (1 Zeile pro Datei, +1 Shift der
  class-Zeile). BOM, EOL, Trailing-NL, Nullable alle unverändert.

## Beobachtungen

- **BOM-Inhomogenität in `Configuration/` (4/8 mit BOM, 4/8 ohne
  BOM) ist auch nach step-009 unverändert** — Heuristik-Punkt 7
  (in spe, neu in step-009 dokumentiert) bleibt offen. Konsolidierung
  ist Nutzer-/Repo-Konvention-Entscheidung (analog TD-003 EOL und
  TD-004 Nullable); in `roadmap.md` EPIC-02-Zeile vermerkt, kein
  TD-Eintrag angelegt.
- **BOM-Konservierung perfekt:** alle 4 BOM-tragenden Dateien
  (`ConfigLoaderRulesJsonTests`, `ConfigNormalizerTests`,
  `ConfigSyncerTests`, `PathOverridesTests`) behalten ihre BOM
  `EF BB BF` über den Edit hinweg. Das `edit`-Tool arbeitet
  byte-präzise auf dem exakten `old_string`-Match und erhält
  die Datei-Header-Bytes. Kein byte-genauer Python-Helper
  nötig (anders als in step-007 für TD-003 LF-only).
- **Trait-Platzierungs-Bibliothek vollständig bestätigt:** alle
  3 in step-007/008 etablierten Varianten (Standard-Insert in
  5 Klassen, `// @covers`-Block-Insert in 2 Klassen, XML-Doc-
  Variante in 1 Klasse) sind in step-009 angewendet und ohne
  Sonderbehandlung bestätigt. Die Bibliothek ist für die
  EPIC-02-Folge-Batches (kleine Unit-Ordner `Maps/`, `Cli/`,
  ggf. Anfang von `Core/`) als abgeschlossen anzusehen.
- **`Configuration/`-Ordner vollständig abgeschlossen** in 1
  Schritt: 8 Test-Klassen, alle homogen Unit, exakt am
  8-Item-Deckel. Der Folge-Planer kann `Configuration/` als
  abgehakt voraussetzen und auf `Core/Checkers/` (27 Klassen),
  `Core/` (19 Klassen), `Maps/` (6 Klassen), `Mcp/` (19 Klassen,
  gemischt), `Commands/` (17 Klassen, stark gemischt), `Baseline/`
  (10 Klassen, gemischt) oder `Cli/` (6 Klassen, gemischt) als
  nächsten EPIC-02-Batch zielen.
- **Build/Test grün, Self-Lint OK:** alle DoD-Gates erfüllt.
  Volltest 1 m 45 s, Unit-Filter 8 s, Integration-Filter
  2 m 10 s. Der Integration-Filter (113 Tests) lief **ohne
  Re-Run** grün durch — der pre-existing Flaky-Test
  `McpServerCommandLoadingStateTests.LoadState_...ReportsLoaded
  Immediately` (EPIC-06-Ziel) hat diesmal nicht geflackt.
- **Subject-Länge exakt 71 Zeichen** (Plan: 71, verifiziert).
  Subject-Diff zur step-008-Konvention: `Output-Tests
  Kategorie-taggen 2/2` → `Configuration-Tests Kategorie-taggen`
  (kein `1/1`/`2/2`-Marker, weil Configuration/ ein einzelner
  Batch ist, kein Halb-Ordner-Schnitt).

## Bekannte Unschärfen

- **Planer-Miscount in `AgentFeaturesTests.cs` (16 statt 15 Facts):**
  String-Literal-Verschachtelung von `[Fact]`-Markern wurde
  nicht erkannt. Der Coder hat den Plan **1:1** umgesetzt (1
  Trait-Zeile pro Datei) und nicht „korrigierend" in den
  Methoden-Count eingegriffen. Der xUnit-Runner liefert
  die korrekte Test-Case-Zahl (15 für AgentFeatures), die
  Filter-Delta-Berechnung basiert auf der xUnit-Wahrheit
  (656 statt 657). **Der Kritiker sollte die korrekten Counts
  in zukünftige Pläne übernehmen** und idealerweise die
  `Select-String`-basierte Regex-Methode (mit Ausschluss
  von `"""`-String-Literal-Kontexten) für die Methoden-
  Inventur verwenden.
- **Per-Klasse-Filter nicht direkt verifizierbar** (xUnit-v3
  unterstützt keine `&`-Operator-Verknüpfung von `Category=Unit`
  und `FullyQualifiedName~Klasse` in einer einzelnen
  `--filter`-Expression) — die Per-Klasse-Zahlen wurden daher
  durch **separate** `FullyQualifiedName`-Filter-Läufe
  ermittelt (7 Läufe für die 7 Klassen außer `FileFilterEvaluator`,
  das via `~Configuration` mit-counted wurde). Die
  Summenprobe 15+4+3+9+11+12+10+3 = 67 stimmt mit dem
  Unit-Filter-Total 656 - 589 = +67 überein.
- **BOM-Inhomogenität in `Configuration/`** ist eine
  **Repository-Konsistenz-Frage**, nicht in step-009-Scope
  gelöst. Falls der Nutzer nach Konsolidierung (analog
  TD-003-Variante (a) `git add --renormalize .`) fragt:
  eigenständiger Folge-Schritt, nicht Teil von EPIC-02.

## Falls Status `blocked`

**Nicht zutreffend.** Status `done (pending audit)`.
