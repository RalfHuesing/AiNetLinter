---
status: done
type: step-result
task: flaky-and-test-performance
step: 012
epic: EPIC-02
step_type: batch
coded_by: coder
coded_by_model: MiniMax-M3
coded_by_model_knowledge_cutoff: 2026-01
coded_at: 2026-08-08T11:00:00+02:00
code_commit_hash: b2477f5
status_after: done
blocker_category: n/a
---

# Result Step 012: Category-Traits für Core-Rest (11 Klassen) und Maps/+Maps/Skeleton/ (6 Klassen) nachziehen (Mega-Batch 2/2 für Core+Maps)

**Wer das liest:** Kritiker (prüft gegen Plan) und Planer (nächster Step).

## Zusammenfassung

17 Testklassen mit `[Trait("Category", "Unit")]` auf Klassen-Ebene versehen
(15 Standard-Insert via `edit`-Tool + 1 XML-Doc-Variante 1b in
`PlaybookGeneratorRound2Tests.cs` + 1 Python-Helper für die LF-only-Datei
`SkeletonStableIdTests.cs`). 11 `Core/`-Klassen (LinterEngineTests bis
ViolationDescriptionTests) komplettieren den `Core/`-Ordner auf 19/19;
6 `Maps/`+`Maps/Skeleton/`-Klassen komplettieren den `Maps/`-Ordner auf
6/6. Filter-Delta wie prognostiziert: Unit 882 → 984 (+102), Integration
113 → 113 (±0), Total 1325 → 1325 (±0).

## Geänderte Dateien

- `src/AiNetLinter.Tests/Core/LinterEngineTests.cs` (item-01) — `[Trait("Category", "Unit")]` über `public sealed class LinterEngineTests` (Z. 9 → 10); Helper-Klasse `HighlyRelevantServiceTests` auf Z. 269 (innerhalb eines `@"..."`-String-Literals) bleibt **unverändert** (Heuristik-Punkt 6 — Helper-Klassen ohne `[Fact]`/`[Theory]` werden nicht getaggt).
- `src/AiNetLinter.Tests/Core/NamespaceFilterTests.cs` (item-02) — Standard-Insert, classLine 8 → 9.
- `src/AiNetLinter.Tests/Core/NullCoalescingInitializerClassifierTests.cs` (item-03) — Standard-Insert, classLine 14 → 15; **BOM-tragend** (`EF BB BF`) — Post-Edit-Verify: erste 3 Bytes = `EF BB BF` ✓, CRLF erhalten.
- `src/AiNetLinter.Tests/Core/PlaybookGeneratorRound2Tests.cs` (item-04) — **XML-Doc-Variante 1b (2-fach `// @covers`)** — Trait-Zeile zwischen `</summary>` (Z. 21) und `public sealed class` (Z. 22); classLine 22 → 23. **Variante 1b in step-012 etabliert** für Folge-Planer wiederverwendbar.
- `src/AiNetLinter.Tests/Core/ResultPatternNamespaceTests.cs` (item-05) — Standard-Insert, classLine 10 → 11; **BOM-tragend** — erste 3 Bytes = `EF BB BF` ✓, CRLF erhalten.
- `src/AiNetLinter.Tests/Core/RuleRegistryTests.cs` (item-06) — Standard-Insert, classLine 13 → 14.
- `src/AiNetLinter.Tests/Core/ScopeImmutabilityTests.cs` (item-07) — Standard-Insert, classLine 10 → 11; **BOM-tragend** — erste 3 Bytes = `EF BB BF` ✓, CRLF erhalten.
- `src/AiNetLinter.Tests/Core/StaticTestSentinelExemptionTests.cs` (item-08) — Standard-Insert, classLine 13 → 14; **BOM-tragend** — erste 3 Bytes = `EF BB BF` ✓, CRLF erhalten.
- `src/AiNetLinter.Tests/Core/TestCoverageResolverTests.cs` (item-09) — Standard-Insert, classLine 6 → 7 (kleinste `Core/`-Datei im Batch, 1378 Bytes).
- `src/AiNetLinter.Tests/Core/TestProjectDetectorSuffixTests.cs` (item-10) — Standard-Insert, classLine 8 → 9; 3 Facts + 2 Theories + 11 InlineData = **14 Test-Cases** zur Laufzeit.
- `src/AiNetLinter.Tests/Core/ViolationDescriptionTests.cs` (item-11) — Standard-Insert, classLine 9 → 10 (kleinste Datei im Batch, 804 Bytes).
- `src/AiNetLinter.Tests/Maps/HotspotMapBuilderTests.cs` (item-12) — Standard-Insert vor `public sealed class HotspotMapBuilderTests : IDisposable` (Z. 12 → 13); Interface-Deklaration unangetastet.
- `src/AiNetLinter.Tests/Maps/Skeleton/SkeletonMapBuilderTests.cs` (item-13) — Standard-Insert, classLine 12 → 13.
- `src/AiNetLinter.Tests/Maps/Skeleton/SkeletonStableIdTests.cs` (item-14) — **Python-Helper** (LF-only, byte-genau); `[Trait("Category", "Unit")]\n` (28 Bytes) eingefügt; CR=0, LF=42→43 (+1), BOM=N, TrNL=ja — EOL LF-only bleibt LF-only.
- `src/AiNetLinter.Tests/Maps/Skeleton/SkeletonSyntaxWalkerTests.cs` (item-15) — Standard-Insert, classLine 10 → 11.
- `src/AiNetLinter.Tests/Maps/StructureMapBuilderTests.cs` (item-16) — Standard-Insert vor `public sealed class StructureMapBuilderTests : IDisposable` (Z. 12 → 13).
- `src/AiNetLinter.Tests/Maps/VocabularyMapBuilderTests.cs` (item-17) — Standard-Insert vor `public sealed class VocabularyMapBuilderTests : IDisposable` (Z. 12 → 13).

**Diff-Statistik:** 17 Dateien × +1 Trait-Zeile = **+17 Zeilen** in 17 Dateien. Bytes-Diff:
16 CRLF-Dateien: +29 Bytes/Datei = +464 Bytes; 1 LF-only-Datei: +28 Bytes = +28 Bytes. **Gesamt: +492 Bytes** (Planer-Schätzung +491 Bytes; +1 Byte messtechnisch).

## Commit

- **Code-Commit-Hash:** `b2477f5`
- **Message:**
  ```
  test: Core+Maps-Tests Kategorie-taggen [flaky-and-test-performance]

  Refs: tasks/flaky-and-test-performance/step-012
  ```
- **Branch:** `main`
- **Push:** nein (lokal)
- **Subject-Länge:** 67 Zeichen (Planer-Vorgabe exakt 5 Zeichen Reserve zur 72-Grenze eingehalten).
- **Doku-Commit:** separater, zweiter Commit — siehe `git log` nach step-Abschluss.

## Build-/Test-Output

```
dotnet build                                                       → grün (0 Warnungen, 0 Fehler)
dotnet run --project src/AiNetLinter -- --config rules.json --path . → OK (TD-001-konform)
dotnet test --no-build --filter "Category=Unit"                    → grün (984 Tests, 0 Fehler) [882 + 102 = Planer-Prognose exakt]
dotnet test --no-build --filter "Category=Integration"              → 112 erfolgreich, 1 fehlgeschlagen (pre-existing Flaky-Test McpServerCommandLoadingStateTests.LoadState_LoadFuncCompletesSynchronouslyWithCatalog_ReportsLoadedImmediately — siehe "Beobachtungen")
dotnet test (Voll)                                                 → grün (1325 Tests, 0 Fehler, 0 übersprungen, 2m 15s) [Total-Stand step-011 = 1325; step-012 = 1325; Planer-Prognose exakt]
```

### EOL-Konservierungstabelle (Post-Edit Vollscan)

| Item | Datei | BOM (vor/nach) | CR (vor/nach) | LF (vor/nach) | TrNL | EOL (vor/nach) | Diff-Bemerkung |
|---|---|---|---|---|---|---|---|
| 01 | LinterEngineTests.cs | ✗/✗ | 333/334 | 333/334 | ✓/✓ | CRLF/CRLF | +1 Zeile (Trait) |
| 02 | NamespaceFilterTests.cs | ✗/✗ | 30/31 | 30/31 | ✓/✓ | CRLF/CRLF | +1 Zeile |
| 03 | NullCoalescingInitializerClassifierTests.cs | ✓/✓ | 206/207 | 206/207 | ✓/✓ | CRLF/CRLF | +1 Zeile, **BOM intakt** (erste 3 Bytes = `EF BB BF`) |
| 04 | PlaybookGeneratorRound2Tests.cs | ✗/✗ | 229/230 | 229/230 | ✓/✓ | CRLF/CRLF | +1 Zeile (XML-Doc-Variante 1b) |
| 05 | ResultPatternNamespaceTests.cs | ✓/✓ | 192/193 | 192/193 | ✓/✓ | CRLF/CRLF | +1 Zeile, **BOM intakt** |
| 06 | RuleRegistryTests.cs | ✗/✗ | 149/150 | 149/150 | ✓/✓ | CRLF/CRLF | +1 Zeile |
| 07 | ScopeImmutabilityTests.cs | ✓/✓ | 209/210 | 209/210 | ✓/✓ | CRLF/CRLF | +1 Zeile, **BOM intakt** |
| 08 | StaticTestSentinelExemptionTests.cs | ✓/✓ | 313/314 | 313/314 | ✓/✓ | CRLF/CRLF | +1 Zeile, **BOM intakt** |
| 09 | TestCoverageResolverTests.cs | ✗/✗ | 46/47 | 46/47 | ✓/✓ | CRLF/CRLF | +1 Zeile |
| 10 | TestProjectDetectorSuffixTests.cs | ✗/✗ | 69/70 | 69/70 | ✓/✓ | CRLF/CRLF | +1 Zeile |
| 11 | ViolationDescriptionTests.cs | ✗/✗ | 26/27 | 26/27 | ✓/✓ | CRLF/CRLF | +1 Zeile |
| 12 | HotspotMapBuilderTests.cs | ✗/✗ | 76/77 | 76/77 | ✓/✓ | CRLF/CRLF | +1 Zeile |
| 13 | SkeletonMapBuilderTests.cs | ✗/✗ | 61/62 | 61/62 | ✓/✓ | CRLF/CRLF | +1 Zeile |
| 14 | **SkeletonStableIdTests.cs** | ✗/✗ | 0/0 | 42/**43** | ✓/✓ | **LF-only/LF-only** | +28 Bytes via Python-Helper; `git diff` rein Trait-Zeile ohne EOL-Wechsel |
| 15 | SkeletonSyntaxWalkerTests.cs | ✗/✗ | 214/215 | 214/215 | ✓/✓ | CRLF/CRLF | +1 Zeile |
| 16 | StructureMapBuilderTests.cs | ✗/✗ | 86/87 | 86/87 | ✓/✓ | CRLF/CRLF | +1 Zeile |
| 17 | VocabularyMapBuilderTests.cs | ✗/✗ | 94/95 | 94/95 | ✓/✓ | CRLF/CRLF | +1 Zeile |

**EOL-Vollscan-Ergebnis:** 16/17 Dateien uniform CRLF erhalten (CR-Zahl = LF-Zahl vor und nach Edit), 1/17 Datei LF-only erhalten (CR=0, LF+1), 4/4 BOM-Dateien mit intakter BOM (`EF BB BF` weiterhin erste 3 Bytes). Git-Warnung "LF will be replaced by CRLF the next time Git touches it" für `SkeletonStableIdTests.cs` ist `core.autocrlf=true`-bezogen beim nächsten Ein/Aus-Checken — der **Commit** enthält die LF-Bytes unverändert (HEAD hatte ebenfalls LF-only, Blob wird mit aktuellen Working-Tree-Bytes gespeichert, `git diff --staged` zeigt nur +1 Trait-Zeile ohne EOL-Wechsel).

### Numerische Plausibilität (NITPICK-Linie aus step-009)

- **Brutto Methoden:** 91 `[Fact]` + 2 `[Theory]` = **93 Methoden** (Planer-Erwartung 93 ✓)
- **Netto Methoden (ohne String-Literal-`[Fact]`):** 91 + 2 = **93** (0/17 Dateien mit String-Literal-`[Fact]`-Verschachtelung)
- **`[InlineData]`-Expansionen:** 11 (ausschließlich in `TestProjectDetectorSuffixTests`: Theory#1 = 7, Theory#2 = 4)
- **Test-Cases-Inventar (xUnit-Zählung, was `dotnet test` zählt):** 91 Facts + 11 InlineData-Expansions = **102** (Planer-Erwartung 102 ✓)
- **Filter-Delta (gemessen via `dotnet test`):** Unit 882 → **984** (+102 ✓), Integration 113 → 113 (±0 ✓), Total 1325 → 1325 (±0 ✓)

## Abweichungen vom Plan

Keine — Plan 1:1 umgesetzt. Die einzige Mini-Abweichung war ein PowerShell-Pfad-Parsing-Bug in meinem **internen** Verifikations-Skript (Backslash-Continuation verursachte einen Item-Loss bei `VocabularyMapBuilderTests.cs`); das war kein Datei-Edit-Bug, das Coder-Skript nutzte absolute Pfade und lieferte korrekte 93/91/11-Zahlen. Der Plan-Workflow selbst ist sauber durchgelaufen.

## Beobachtungen

- **Integration-Filter-Lauf mit 1 fehlgeschlagenem Test:** Der einzige Fehler im `dotnet test --filter "Category=Integration"`-Lauf ist `AiNetLinter.Tests.Commands.McpServerCommandLoadingStateTests.LoadState_LoadFuncCompletesSynchronouslyWithCatalog_ReportsLoadedImmediately` — ein im `codemap.md` §72 explizit als "pre-existing Flaky-Test (Z. 112-150, Poll-Loop mit fixer 5s-Deadline, Thread-Pool-abhängig); Ziel von EPIC-06" dokumentierter Test. **Er steht nicht im step-012-Scope** (Commands/-Ordner ist Folge-Step-Material laut step-012 §"Schnitt-Wahl-Begründung" und step-plan Notes "Folge-Steps"). Der volle `dotnet test`-Lauf war grün (0 Fehler) — die Inkonsistenz erklärt sich durch Test-Lauf-Timing (Long-Running-Tests in paralleler Ausführung; im Voll-Lauf lief der Test grün, im Filter-Lauf traf er den 5s-Poll-Loop-Timing-Window nicht). Empfehlung an Kritiker: bleibt EPIC-06-Problem (TD-008 oder vergleichbar), kein step-012-Defekt.
- **XML-Doc-Variante 1b in step-012 etabliert:** die Variante für 2-fach `// @covers` vor XML-Doc-Block wurde in `PlaybookGeneratorRound2Tests.cs` zum ersten Mal angewendet. Mechanik identisch zu Variante 1a aus step-011. Für Folge-Planer mit ähnlicher Klassen-Deklaration wiederverwendbar.
- **BOM-Inhomogenität in `Core/` (4/17 = 24 %) vs. `Maps/` (0/6 = 0 %):** bestätigt die step-010/011-Hypothese ("30 % mit BOM" für `Core/Checkers/`+`Core/`-A-Teil). Alle 4 BOM-Dateien sind in `Core/`, keine in `Maps/`. Heuristik-Punkt 8 (TD-006) bleibt offen — Konsolidierung out of scope.
- **EOL-Inhomogenität `Maps/` (1/6 = 17 % LF-only):** `SkeletonStableIdTests.cs` ist die einzige `Maps/`-Datei mit LF-only (analog `McpLintConsoleTests.cs` LF-only in `Output/`). Python-Helper analog step-007 hat byte-genau funktioniert.
- **`#nullable enable`-Disziplin:** 7/17 Dateien ohne Direktive (`LinterEngineTests`, `NamespaceFilterTests`, `ResultPatternNamespaceTests`, `ScopeImmutabilityTests`, `TestCoverageResolverTests`, `TestProjectDetectorSuffixTests`, `SkeletonStableIdTests`); Trait-Insertion hat **keine** der Direktiven hinzugefügt. `dotnet build` läuft grün (0 Warnungen) — Tests-Profil hebt `EnforceNullableEnable` wahrscheinlich auf.
- **Git-Attribute und `core.autocrlf=true`:** die `core.autocrlf=true`-Einstellung führt zu Warnungen für LF-only-Dateien beim Ein/Aus-Checken. Der Commit selbst erhält die Bytes unverändert; die LF-only-EOL-Status ist im Repo-BLOB korrekt. **Kein Auto-CRLF-Fix nötig** (out of scope step-012; Planer hat LF-only bewusst als `Maps/`-TD-003-Analogon dokumentiert).
- **Filter-Delta stimmt exakt mit Planer-Prognose überein:** +102 Unit, ±0 Integration, ±0 Total. Die +102 = 91 Facts + 11 InlineData-Expansions (7 aus Theory#1 + 4 aus Theory#2 in `TestProjectDetectorSuffixTests`).

## Bekannte Unschärfen

- **`McpServerCommandLoadingStateTests` Flaky-Test:** siehe "Beobachtungen" oben. Der Integration-Filter-Lauf war nicht grün (112/113), aber im vollen Lauf war er grün. Falls Kritiker den Integration-Filter-Lauf als "nicht best-effort-grün" wertet: liegt außerhalb step-012-Scope, ist EPIC-06-Problem.
- **PowerShell `Get-Content` + Backslash-Continuation:** bei meinem ersten Verifikations-Skript crashte `Get-Content` für den letzten Eintrag einer 17-Element-Liste (Backslash-Interpretation als Escape statt Zeilen-Continuation). Mit absoluten Pfaden war es robust. **Kein** Datei-Problem, nur internes Test-Skript.
- **Git-Autocrlf-Warnung für `SkeletonStableIdTests.cs`:** siehe "Beobachtungen" — Commit ist sauber, aber zukünftige Git-Touches (z. B. `git checkout` auf den Branch) würden die Datei auf CRLF normalisieren. Empfehlung an Folge-Planer: bei zukünftigen Edits an dieser Datei Python-Helper verwenden, sonst unbeabsichtigter EOL-Wechsel.

## Falls Status `blocked`

Nicht zutreffend — Status ist `done`.
