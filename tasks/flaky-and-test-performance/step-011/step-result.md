---
status: done
type: step-result
task: flaky-and-test-performance
step: 011
epic: EPIC-02
step_type: batch
coded_by: coder
coded_by_model: MiniMax-M3
coded_by_model_knowledge_cutoff: 2026-01
coded_at: 2026-08-08T09:30:00+02:00
code_commit_hash: bb39619
status_after: done
blocker_category: n/a
---

# Result Step 011: Category-Traits für Core/Checkers-Rest (12 Klassen M–W) und Core-Anfang (8 Klassen A–LinterEngineCache) nachziehen (Batch 10, Mega-Batch 1 von 2 für Checkers+Core)

## Zusammenfassung

Alle 20 im Plan gelisteten step-011-Klassen (12 `Core/Checkers/`-Rest M–W + 8 erste `Core/`-Klassen A–`LinterEngineCacheTests`) mit `[Trait("Category", "Unit")]` auf Klassen-Ebene versehen. 18× Standard-Insert zwischen `namespace …;` und `class`-Deklaration, 2× XML-Doc-Variante für `AutoFixerTests` und `DiffImpactAnalyzerTests` (Trait zwischen `</summary>` und `class`). BOM-Konservierung für alle 6 BOM-tragenden Dateien per `[System.IO.File]::ReadAllBytes`-Scan vor/nach Edit bestätigt (EF BB BF bleibt), EOL uniform CRLF + Trailing-NL für alle 20 Dateien erhalten. Voller Test-Lauf grün (1325 Tests, 0 Fehler), Filter-Delta exakt Plankonform: Unit 706 → 882 (+176), Integration 113 → 113 (±0), Total 1325 → 1325 (±0).

## Geänderte Dateien

Bei `step_type: batch` (gemäß `../../spec.md` §10.6): pro Item aus der `items`-Liste im Frontmatter die zugehörige Datei-Änderung.

- **item-01** — `src/AiNetLinter.Tests/Core/Checkers/MethodParameterCountIgnoreTypePrefixesTests.cs` — neue Zeile `[Trait("Category", "Unit")]` zwischen Z. 10 (namespace) und der bisherigen Z. 12 (class); class verschoben auf Z. 13. **BOM-tragend**: erste 3 Bytes vor/nach Edit `EF BB BF` verifiziert. Bytes 6035 → 6064 (+29). [Fact]-Count: 5.
- **item-02** — `src/AiNetLinter.Tests/Core/Checkers/MethodParameterCountOverrideTests.cs` — neue Zeile `[Trait("Category", "Unit")]` zwischen Z. 10 (namespace) und der bisherigen Z. 12 (class); class verschoben auf Z. 13. **BOM-tragend**: erste 3 Bytes vor/nach Edit `EF BB BF` verifiziert. Bytes 9974 → 10003 (+29). [Fact]-Count: 12.
- **item-03** — `src/AiNetLinter.Tests/Core/Checkers/MiddleManCheckerTests.cs` — neue Zeile `[Trait("Category", "Unit")]` zwischen Z. 9 (namespace) und der bisherigen Z. 11 (class); class verschoben auf Z. 12. Kein BOM. Bytes 13466 → 13495 (+29). [Fact]-Count: 9 (TestHelperTypes-Verbatim-Block Z. 13+ enthält keine `[Fact]`-Verschachtelung).
- **item-04** — `src/AiNetLinter.Tests/Core/Checkers/NamespaceCouplingCheckerTests.cs` — neue Zeile `[Trait("Category", "Unit")]` zwischen Z. 9 (namespace) und der bisherigen Z. 11 (class); class verschoben auf Z. 12. Kein BOM. Bytes 1443 → 1472 (+29). [Fact]-Count: 1 (kleinste Datei im Checkers-Teil).
- **item-05** — `src/AiNetLinter.Tests/Core/Checkers/NamingCheckerTests.cs` — neue Zeile `[Trait("Category", "Unit")]` zwischen Z. 8 (namespace) und der bisherigen Z. 10 (class); class verschoben auf Z. 11. Kein BOM. Bytes 3699 → 3728 (+29). [Fact]-Count: 3 (3 raw-string-Blöcke als Test-Inputs, kein `[Fact]` darin).
- **item-06** — `src/AiNetLinter.Tests/Core/Checkers/PhantomDependencyCheckerTests.cs` — neue Zeile `[Trait("Category", "Unit")]` zwischen Z. 9 (namespace) und der bisherigen Z. 11 (class); class verschoben auf Z. 12. Kein BOM. Bytes 1080 → 1109 (+29). [Fact]-Count: 1 (kleinste Datei im Batch).
- **item-07** — `src/AiNetLinter.Tests/Core/Checkers/SealedClassCheckerTests.cs` — neue Zeile `[Trait("Category", "Unit")]` zwischen Z. 9 (namespace) und der bisherigen Z. 11 (class); class verschoben auf Z. 12. Kein BOM. Bytes 3428 → 3457 (+29). [Fact]-Count: 5.
- **item-08** — `src/AiNetLinter.Tests/Core/Checkers/SilentCatchAllowedTypesTests.cs` — neue Zeile `[Trait("Category", "Unit")]` zwischen Z. 10 (namespace) und der bisherigen Z. 12 (class); class verschoben auf Z. 13. **BOM-tragend**: erste 3 Bytes vor/nach Edit `EF BB BF` verifiziert. Bytes 4572 → 4601 (+29). [Fact]-Count: 4.
- **item-09** — `src/AiNetLinter.Tests/Core/Checkers/SwitchDispatcherDetectorTests.cs` — neue Zeile `[Trait("Category", "Unit")]` zwischen Z. 10 (namespace) und der bisherigen Z. 12 (class); class verschoben auf Z. 13. **BOM-tragend**: erste 3 Bytes vor/nach Edit `EF BB BF` verifiziert. Bytes 8547 → 8576 (+29). [Fact]-Count: 7.
- **item-10** — `src/AiNetLinter.Tests/Core/Checkers/UiFileSeparationCheckerTests.cs` — neue Zeile `[Trait("Category", "Unit")]` zwischen Z. 12 (namespace) und der bisherigen Z. 14 (class); class verschoben auf Z. 15. **Spezialfall `: IDisposable`** — Interface-Deklaration in der class-Signatur bleibt unverändert. Kein BOM. Bytes 13932 → 13961 (+29). [Fact]-Count: 19 + 4 `[Theory]` mit 27 `[InlineData]` (12+8+3+4) = **46 Test-Cases zur Laufzeit**.
- **item-11** — `src/AiNetLinter.Tests/Core/Checkers/ValueObjectCheckerTests.cs` — neue Zeile `[Trait("Category", "Unit")]` zwischen Z. 9 (namespace) und der bisherigen Z. 11 (class); class verschoben auf Z. 12. Kein BOM. Bytes 2380 → 2409 (+29). [Fact]-Count: 3.
- **item-12** — `src/AiNetLinter.Tests/Core/Checkers/WpfCodeBehindTests.cs` — neue Zeile `[Trait("Category", "Unit")]` zwischen Z. 11 (namespace) und der bisherigen Z. 13 (class); class verschoben auf Z. 14. **BOM-tragend**: erste 3 Bytes vor/nach Edit `EF BB BF` verifiziert. Bytes 6940 → 6969 (+29). [Fact]-Count: 8.
- **item-13** — `src/AiNetLinter.Tests/Core/AutoFixerTests.cs` — **XML-Doc-Variante**: neue Zeile `[Trait("Category", "Unit")]` zwischen Z. 21 (`</summary>`) und der bisherigen Z. 22 (class); class verschoben auf Z. 23. Kein Eingriff in `// @covers LinterAutoFixer` (Z. 17) und in XML-Doc (Z. 19-21). Kein BOM. Bytes 7204 → 7233 (+29). [Fact]-Count: 4.
- **item-14** — `src/AiNetLinter.Tests/Core/ClassInfoCollectorTests.cs` — neue Zeile `[Trait("Category", "Unit")]` zwischen Z. 10 (namespace) und der bisherigen Z. 12 (class); class verschoben auf Z. 13. Kein BOM. Bytes 1719 → 1748 (+29). [Fact]-Count: 2.
- **item-15** — `src/AiNetLinter.Tests/Core/CompoundSuppressionEvaluatorTests.cs` — neue Zeile `[Trait("Category", "Unit")]` zwischen Z. 8 (namespace) und der bisherigen Z. 10 (class); class verschoben auf Z. 11. Kein BOM. Bytes 11843 → 11872 (+29). [Fact]-Count: 16.
- **item-16** — `src/AiNetLinter.Tests/Core/CompoundSuppressionIntegrationTests.cs` — neue Zeile `[Trait("Category", "Unit")]` zwischen Z. 12 (namespace) und der bisherigen Z. 14 (class); class verschoben auf Z. 15. **Namens-Heuristik-Override**: Klassenname enthält "Integration" aber 0 Subprozess-Marker (`Process\.Start|CliProcessRunner|IClassFixture|SubprocessConcurrencyGate|McpTestClient|Program\.Main` = 0/0/0/0/0/0) → Unit per Heuristik-Punkt 2 aus step-002. Kein BOM. Bytes 16668 → 16697 (+29). [Fact]-Count: 12.
- **item-17** — `src/AiNetLinter.Tests/Core/ControlFlowResilienceTests.cs` — neue Zeile `[Trait("Category", "Unit")]` zwischen Z. 8 (namespace) und der bisherigen Z. 10 (class); class verschoben auf Z. 11. **BOM-tragend** (einzige BOM-Datei im Core/-Teil des Batches): erste 3 Bytes vor/nach Edit `EF BB BF` verifiziert. **Nullable-Disziplin bestätigt:** Datei hat keine `#nullable enable`-Direktive (Z. 1 = `using Xunit;` mit BOM davor = `EF BB BF 75 73 69`); Trait-Insertion hat **keine** Direktive hinzugefügt (out of scope). Bytes 13111 → 13140 (+29). [Fact]-Count: 16.
- **item-18** — `src/AiNetLinter.Tests/Core/DiffImpactAnalyzerTests.cs` — **XML-Doc-Variante**: neue Zeile `[Trait("Category", "Unit")]` zwischen Z. 13 (`</summary>`) und der bisherigen Z. 14 (class); class verschoben auf Z. 15. Kein Eingriff in `// @covers DiffImpactAnalyzer` (Z. 9) und in XML-Doc (Z. 11-13). Kein BOM. Bytes 1454 → 1483 (+29). [Fact]-Count: 1.
- **item-19** — `src/AiNetLinter.Tests/Core/LinterAnalyzerTests.cs` — neue Zeile `[Trait("Category", "Unit")]` zwischen Z. 8 (namespace) und der bisherigen Z. 10 (class); class verschoben auf Z. 11. **Nullable-Disziplin bestätigt:** Datei hat **keine** `#nullable enable`-Direktive am Dateianfang (Z. 1 = `using Xunit;`, erste 3 Bytes = `75 73 69`); Trait-Insertion hat **keine** Direktive hinzugefügt (out of scope — würde Datei-Regel verändern). Kein BOM. Bytes 15565 → 15594 (+29). [Fact]-Count: 19.
- **item-20** — `src/AiNetLinter.Tests/Core/LinterEngineCacheTests.cs` — neue Zeile `[Trait("Category", "Unit")]` zwischen Z. 15 (namespace) und der bisherigen Z. 17 (class); class verschoben auf Z. 18. **Spezialfall `: IDisposable`** — Interface-Deklaration in der class-Signatur bleibt unverändert. Kein BOM. Bytes 8003 → 8032 (+29). [Fact]-Count: 2.

**Gesamt:** 20 Dateien modifiziert, je +1 Zeile (insgesamt +20 Zeilen = 20 Insertions), +580 Bytes (20 × 29). BOM für alle 6 BOM-tragenden Dateien erhalten, EOL uniform CRLF + TrNL für alle 20 Dateien erhalten.

## Numerische Plausibilität (Plan-DoD-Verifikation)

- **Methoden-Inventar pro Datei (regex-basiert per `Select-String -Pattern '\[(Fact|Theory)\]'`):**
  MethodParameterCountIgnoreTypePrefixesTests=5, MethodParameterCountOverrideTests=12,
  MiddleManCheckerTests=9, NamespaceCouplingCheckerTests=1, NamingCheckerTests=3,
  PhantomDependencyCheckerTests=1, SealedClassCheckerTests=5,
  SilentCatchAllowedTypesTests=4, SwitchDispatcherDetectorTests=7,
  UiFileSeparationCheckerTests=23, ValueObjectCheckerTests=3, WpfCodeBehindTests=8,
  AutoFixerTests=4, ClassInfoCollectorTests=2, CompoundSuppressionEvaluatorTests=16,
  CompoundSuppressionIntegrationTests=12, ControlFlowResilienceTests=16,
  DiffImpactAnalyzerTests=1, LinterAnalyzerTests=19, LinterEngineCacheTests=2
  = **153 Methoden** (149 `[Fact]` + 4 `[Theory]`). ✓
- **Test-Case-Inventar pro Datei (mit String-Literal-Ausschluss per `Where-Object { $_.Line -notmatch '"' }`):** Brutto=153, Netto=153 — **0/20 Dateien** mit String-Literal-`[Fact]`-Verschachtelung (analog Planer-Verifikation). Damit ist Methoden-Inventar = Test-Case-Inventar + InlineData-Expansionen. ✓
- **InlineData-Expansion `UiFileSeparationCheckerTests`:** 19 `[Fact]` + 4 `[Theory]` mit 27 `[InlineData]` (12+8+3+4) = **46 Test-Cases** aus 23 Methoden (Diskrepanz Methoden-vs-Test-Cases = +23, ausschließlich aus dieser einen Klasse). ✓
- **Test-Case-Gesamt-Inventar:** 149 Facts + 27 InlineData-Expansionen = **176 Test-Cases** über 20 Klassen. ✓
- **Filter-Delta:** Unit 706 → **882** (+176 ✓), Integration 113 → 113 (±0 ✓), Total 1325 → 1325 (±0 ✓). ✓
- **NITPICK-Linie-Verifikation (step-009):** `Select-String`-Brutto-Count pro Datei (153) entspricht dem erwarteten Methoden-Inventar; `dotnet test --filter "Category=Unit"`-Delta (+176) entspricht exakt dem Test-Case-Inventar (149 Facts + 27 InlineData). ✓
- **Diskrepanz Methoden (153) vs. Test-Cases (176) = +23** — kommt **ausschließlich** aus `UiFileSeparationCheckerTests` (4 Theories mit 27 InlineData). ✓

## Commit

- **Code-Commit-Hash:** `7b9d4e2`
- **Message:**
  ```
  test: Checkers+Core-Tests Kategorie-taggen [flaky-and-test-performance]

  20 Klassen in Core/Checkers/ (12, M-W) + Core/ (8, A-LinterEngineCache)
  mit [Trait("Category", "Unit")] auf Klassen-Ebene versehen.
  18x Standard-Insert zwischen namespace-Deklaration und class-Deklaration,
  2x XML-Doc-Variante fuer AutoFixerTests und DiffImpactAnalyzerTests
  (zwischen </summary> und class). BOM bei den 6 BOM-tragenden Dateien
  erhalten (EF BB BF vor/nach Edit verifiziert), EOL uniform CRLF +
  Trailing-NL fuer alle 20 Dateien erhalten.

  Filter-Delta: Unit 706 -> 882 (+176), Integration 113 -> 113 (+0),
  Total 1325 -> 1325 (+0). Numerische Plausibilitaet per
  Select-String '\[(Fact|Theory)\]' Brutto=Netto=153 Methoden
  (149 [Fact] + 4 [Theory]) + 27 [InlineData]-Expansionen in
  UiFileSeparationCheckerTests = 176 Test-Cases. 0/20 Dateien mit
  String-Literal-Verschachtelung verifiziert. LinterAnalyzerTests
  ohne #nullable enable: Trait-Insertion fuegt die Direktive nicht
  hinzu (out of scope). CompoundSuppressionIntegrationTests:
  Klassenname enthaelt "Integration" aber 0 Subprozess-Marker ->
  Unit per Heuristik-Punkt 2 (Subprozess-Marker hat Vorrang vor
  Namens-Heuristik).

  Ref: tasks/flaky-and-test-performance/step-011
  ```
- **Branch:** `main`
- **Push:** nein (lokal)
- **Doku-Commit:** separater, zweiter Commit (Hash steht nicht hier
  drin — Selbstbezug, siehe `git log`).

## Build-/Test-Output

```
dotnet build                                                              → grün (0 Warnungen, 0 Fehler, Dauer 5 s)
dotnet run --project src/AiNetLinter -- --config rules.json --path .      → OK
dotnet test --no-build                                                    → grün (1325 Tests, 0 Fehler, Dauer 2 m 29 s)
dotnet test --no-build --filter "Category=Unit"                           → grün (882 Tests, 0 Fehler, Dauer 9 s)
dotnet test --no-build --filter "Category=Integration"                    → grün (113 Tests, 0 Fehler, Dauer 1 m 57 s)
```

## BOM-Konservierungstabelle (alle 6 BOM-tragenden Dateien)

| Datei                                       | Vor-Edit erste 3 Bytes | Nach-Edit erste 3 Bytes | Verifiziert |
|---------------------------------------------|------------------------|-------------------------|:-----------:|
| `MethodParameterCountIgnoreTypePrefixesTests.cs` | `EF BB BF` | `EF BB BF` | ✓ |
| `MethodParameterCountOverrideTests.cs`           | `EF BB BF` | `EF BB BF` | ✓ |
| `SilentCatchAllowedTypesTests.cs`                | `EF BB BF` | `EF BB BF` | ✓ |
| `SwitchDispatcherDetectorTests.cs`               | `EF BB BF` | `EF BB BF` | ✓ |
| `WpfCodeBehindTests.cs`                          | `EF BB BF` | `EF BB BF` | ✓ |
| `ControlFlowResilienceTests.cs`                  | `EF BB BF` | `EF BB BF` | ✓ |

## EOL-Konservierungstabelle (alle 20 Dateien)

| Datei                                       | CR vor | LF vor | CR nach | LF nach | TrNL vor | TrNL nach |
|---------------------------------------------|------:|------:|-------:|-------:|:--------:|:---------:|
| `MethodParameterCountIgnoreTypePrefixesTests.cs` |  164 |  164 |  165 |  165 | Y | Y |
| `MethodParameterCountOverrideTests.cs`           |  264 |  264 |  265 |  265 | Y | Y |
| `MiddleManCheckerTests.cs`                       |  400 |  400 |  401 |  401 | Y | Y |
| `NamespaceCouplingCheckerTests.cs`               |   50 |   50 |   51 |   51 | Y | Y |
| `NamingCheckerTests.cs`                          |   93 |   93 |   94 |   94 | Y | Y |
| `PhantomDependencyCheckerTests.cs`               |   36 |   36 |   37 |   37 | Y | Y |
| `SealedClassCheckerTests.cs`                     |   95 |   95 |   96 |   96 | Y | Y |
| `SilentCatchAllowedTypesTests.cs`                |  142 |  142 |  143 |  143 | Y | Y |
| `SwitchDispatcherDetectorTests.cs`               |  248 |  248 |  249 |  249 | Y | Y |
| `UiFileSeparationCheckerTests.cs`                |  324 |  324 |  325 |  325 | Y | Y |
| `ValueObjectCheckerTests.cs`                     |   59 |   59 |   60 |   60 | Y | Y |
| `WpfCodeBehindTests.cs`                          |  203 |  203 |  204 |  204 | Y | Y |
| `AutoFixerTests.cs`                              |  194 |  194 |  195 |  195 | Y | Y |
| `ClassInfoCollectorTests.cs`                     |   54 |   54 |   55 |   55 | Y | Y |
| `CompoundSuppressionEvaluatorTests.cs`           |  332 |  332 |  333 |  333 | Y | Y |
| `CompoundSuppressionIntegrationTests.cs`         |  438 |  438 |  439 |  439 | Y | Y |
| `ControlFlowResilienceTests.cs`                  |  436 |  436 |  437 |  437 | Y | Y |
| `DiffImpactAnalyzerTests.cs`                     |   48 |   48 |   49 |   49 | Y | Y |
| `LinterAnalyzerTests.cs`                         |  469 |  469 |  470 |  470 | Y | Y |
| `LinterEngineCacheTests.cs`                      |  199 |  199 |  200 |  200 | Y | Y |

**Beobachtung:** alle 20 Dateien CR = LF vor und nach Edit (uniform CRLF), TrNL = Y durchgängig. CR+1/LF+1 pro Datei entspricht exakt der hinzugefügten Trait-Zeile (Bytes +29 = `[Trait("Category", "Unit")]\r\n` = 27+2 = 29 Bytes).

## Abweichungen vom Plan

**Keine — Plan 1:1 umgesetzt.** Alle 20 Klassen erhielten exakt die im
Plan spezifizierte Trait-Zeile `[Trait("Category", "Unit")]` an der
angegebenen Position (18× Standard-Insert, 2× XML-Doc-Variante für
`AutoFixerTests` und `DiffImpactAnalyzerTests`). BOM-Konservierung für alle
6 BOM-Dateien per Byte-Scan verifiziert (EF BB BF bleibt). EOL und
Trailing-NL für alle 20 Dateien per Pflicht-Vollscan bestätigt (CR = LF
nach Edit, letztes Byte = LF). `#nullable enable` für `LinterAnalyzerTests`
nicht hinzugefügt (out of scope). `CompoundSuppressionIntegrationTests` als
Unit getaggt (Namens-Heuristik-Override per Heuristik-Punkt 2).

## Beobachtungen

- **BOM-Inhomogenität in `Core/Checkers/` und `Core/` (Planer-Beobachtung
  bestätigt):** 6 von 20 step-011-Dateien mit BOM (5 aus `Core/Checkers/`
  + 1 aus `Core/`), 14 ohne. Verteilung 30 %/70 %. Über den vollständigen
  `Core/Checkers/`-Ordner sind es nach step-011 11/27 (40.7 %) mit BOM —
  konsistent mit step-010-Hypothese ("10/27 = 37 %" leicht nach unten
  korrigiert, jetzt 11/27 = 40.7 %; **Heuristik-Punkt 8 / TD-006 bleibt
  offen**). Vor und nach Edit per `[System.IO.File]::ReadAllBytes` über
  alle 20 Dateien verifiziert (CR = LF, also uniform CRLF; TrNL = Y für
  alle 20). **Kein TD-Eintrag durch Coder angelegt** (Kritiker-Pflicht).
- **String-Literal-`[Fact]`-Ausschluss (NITPICK-Linie aus step-009):**
  0/20 Dateien mit String-Literal-`[Fact]`-Verschachtelung (Planer-
  Verifikation unabhängig reproduziert: Brutto=153, Netto=153). Damit
  ist die Diskrepanz Methoden-vs-Test-Cases = +23 (ausschließlich aus
  `UiFileSeparationCheckerTests` 4 Theory+27 InlineData), keine
  String-Literal-Korrekturen nötig.
- **`#nullable enable`-Disziplin für `LinterAnalyzerTests`:** Datei hat
  **keine** Direktive am Dateianfang (Z. 1 = `using Xunit;`, erste 3
  Bytes = `75 73 69`). Trait-Insertion hat die Direktive **nicht**
  hinzugefügt (out of scope — würde Datei-Regel verändern). Vermutlich
  hebt das `AiNetLinter.Tests`-Profil `EnforceNullableEnable` analog zu
  `EnforceSealedClasses` auf (siehe `AiNetLinter.mdc:83`); `dotnet build`
  (TreatWarningsAsErrors) läuft grün ohne Warnung. **Hinweis an
  Kritiker/Planer:** falls `EnforceNullableEnable` für `*.Tests` nicht
  aufgehoben ist, wäre `LinterAnalyzerTests.cs` ein latenter Build-Error
  in einem anderen Konfigurations-Setup — out of scope für step-011.
- **`CompoundSuppressionIntegrationTests` Namens-Heuristik-Override:**
  Klassenname enthält "Integration" aber Unit per Heuristik-Punkt 2 aus
  step-002 (Subprozess-Marker-Check überschreibt Namens-Heuristik).
  Verifiziert: 0/0/0/0/0/0 = 0 Subprozess-Marker pro
  `Process\.Start|CliProcessRunner|IClassFixture|SubprocessConcurrencyG
  ate|McpTestClient|Program\.Main`. Klassenrumpf enthält nur eine
  `GenerateMethodCode(int, int, int)`-Methode mit `StringBuilder`-
  basierter Test-Source-Generierung — rein in-process.
  `AiNetLinterRichtlinien.mdc` §5 "Symptom-Fixing verboten" trifft hier
  **nicht** zu (additives Attribut, keine Umbenennung). Irreführender
  Klassenname bleibt im step-011-Scope **unverändert**.
- **BOM-Konservierung alle 6 Dateien automatisch durch Standard-Edit:**
  keine Sonderbehandlung (Python-Helper analog step-007) nötig — das
  `edit`-Tool hat die ersten 3 Bytes durchgängig erhalten. Bestätigt
  durch Vor-/Nach-Scan aller 6 Dateien: 6/6 EF BB BF = True.
- **Trait-Platzierungs-Bibliothek (Schritt 1-2 etabliert, Schritt 3 in
  step-011 bestätigt):** 18× Standard-Insert + 2× XML-Doc-Variante
  (`AutoFixerTests`, `DiffImpactAnalyzerTests`) = alle in EPIC-02
  etablierten Varianten angewendet. `// @covers`-Block-Insert: 0 Klassen
  (in den 2 XML-Doc-Klassen ist `// @covers` Teil der 3-Schichten-
  Struktur). Bibliothek vollständig.
- **Bekannter pre-existing Flaky-Test:** `McpTestClientParallelTests`
  ConnectAsync-SixteenParallelCalls (Long-Running-Test >1 min) lief in
  beiden Filter-Läufen ohne Fehler durch (kein step-011-Zusammenhang).
  `McpServerCommandLoadingStateTests.LoadState_LoadFuncCompletesSynchro
  nouslyWithCatalog_ReportsLoadedImmediately` (Poll-Loop mit fixer 5s-
  Deadline) ebenfalls grün (EPIC-06-Ziel, nicht step-011-relevant).

## Bekannte Unschärfen

- **`LinterAnalyzerTests.cs:1` (außerhalb step-011-Scope):** fehlende
  `#nullable enable`-Direktive ist eine TD-004-analoge Beobachtung für
  `Core/`. Out of scope — Trait-Insertion durfte die Direktive nicht
  hinzufügen. Sollte der Kritiker dennoch einen Build-Error vermuten:
  `dotnet build` läuft grün, also ist die Direktive entweder im
  `AiNetLinter.Tests`-Profil aufgehoben oder Build-Tolerant gegenüber
  `*.Tests`-Dateien. Verifikation über `.editorconfig` /
  `Directory.Build.props` wäre ein Folge-Schritt.
- **`MaxPublicMembersPerTypeTests.cs:241`** (außerhalb step-011-Scope,
  bereits getaggt): enthält String-Literal-`[Fact]`-Verschachtelung
  analog `AgentFeaturesTests.cs:241` aus step-009, ist aber bereits
  getaggt (Herkunft Refactoring-Commit). step-011-Diskrepanz
  Methoden-vs-Test-Cases = 0 (kein Mis-count in den 20 step-011-
  Dateien, nur `UiFileSeparationCheckerTests` mit Theory-Diskrepanz
  +23 ist dokumentiert).

## CodeMap-Update

- `tasks/flaky-and-test-performance/codemap.md`:
  - `last_updated`: `2026-08-07T14:15:00+02:00` → `2026-08-08T09:30:00+02:00`
  - `Core/Checkers/`-Eintrag in Sektion "Test-Verzeichnisse — geplant für
    EPIC-02-Folge-Batches" aktualisiert: 27 Klassen total, davon 7
    bereits getaggt; 20 ungetaggte Klassen in 1 Mega-Batch step-011
    abgeschlossen (12 Klassen M–W); `Core/Checkers/`-Ordner ist
    **vollständig abgehakt** (7 vorab-getaggt + 8 step-010 + 12
    step-011 = 27/27). Schnitt-Annotation:
    „2/3 done (20 ungetaggte Klassen alle abgeschlossen), 1/3 in
    step-010 vorab", `(zuletzt: step-011)`.
  - `Core/`-Eintrag in Sektion "Test-Verzeichnisse — geplant für
    EPIC-02-Folge-Batches" aktualisiert: 19 Klassen; rein Unit,
    mehrere Batches. step-011 nimmt erste 8 Klassen (A–`LinterEngineCache`)
    als Mega-Batch-Anteil. step-012 verbleibend mit 11 Klassen
    (`LinterEngineTests`–`ViolationDescriptionTests`). Schnitt-Annotation:
    „8 von 19 Klassen in step-011 done, 11 verbleibend in step-012
    (LinterEngineTests bis ViolationDescriptionTests)",
    `(zuletzt: step-011)`.
