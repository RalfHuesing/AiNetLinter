---
status: done
type: step-result
task: flaky-and-test-performance
step: 010
epic: EPIC-02
step_type: batch
coded_by: coder
coded_by_model: MiniMax-M3
coded_by_model_knowledge_cutoff: 2026-01
coded_at: 2026-08-08T08:30:00+02:00
code_commit_hash: 44956b7
status_after: done
blocker_category: n/a
---

# Result Step 010: Category-Traits für Core/Checkers-Tests nachziehen (Batch 9, Core/Checkers Teil 1/3)

## Zusammenfassung

Alle 8 im Plan gelisteten step-010-Klassen in `Core/Checkers/` (A bis
`MethodParameterCountAccessibilityTests`) mit `[Trait("Category", "Unit")]`
auf Klassen-Ebene versehen (Standard-Insert zwischen `namespace`-Deklaration
und `class`-Deklaration). BOM-Konservierung für die 5 BOM-tragenden Dateien
per `[System.IO.File]::ReadAllBytes`-Scan vor/nach Edit bestätigt
(EF BB BF bleibt). EOL uniform CRLF, Trailing-NL erhalten. Voller Test-Lauf
grün (1325 Tests, 0 Fehler), Filter-Delta exakt Plankonform: Unit 656 → 706
(+50), Integration 113 → 113 (±0), Total 1325 → 1325 (±0).

## Geänderte Dateien

Bei `step_type: batch` (gemäß `../../spec.md` §10.6): pro Item aus der
`items`-Liste im Frontmatter die zugehörige Datei-Änderung.

- **item-01** — `src/AiNetLinter.Tests/Core/Checkers/AsciiIdentifiersTests.cs` — neue Zeile `[Trait("Category", "Unit")]` zwischen Z. 9 (namespace) und der bisherigen Z. 10 (class); class verschoben auf Z. 11. Kein BOM in der Datei (erste 3 Bytes `23 6E 69` = `#ni` = `#nullable enable`). Bytes 6255 → 6284 (+29). [Fact]-Count: 6.
- **item-02** — `src/AiNetLinter.Tests/Core/Checkers/AsyncVoidCheckerTests.cs` — neue Zeile `[Trait("Category", "Unit")]` zwischen Z. 10 (namespace) und der bisherigen Z. 11 (class); class verschoben auf Z. 12. **BOM-tragend**: erste 3 Bytes vor/nach Edit `EF BB BF` verifiziert. Bytes 5988 → 6017 (+29). [Fact]-Count: 8.
- **item-03** — `src/AiNetLinter.Tests/Core/Checkers/BlockingTaskCheckerTests.cs` — neue Zeile `[Trait("Category", "Unit")]` zwischen Z. 10 (namespace) und der bisherigen Z. 11 (class); class verschoben auf Z. 12. **BOM-tragend**: erste 3 Bytes vor/nach Edit `EF BB BF` verifiziert. Bytes 6790 → 6819 (+29). [Fact]-Count: 8.
- **item-04** — `src/AiNetLinter.Tests/Core/Checkers/CouplingSemanticTests.cs` — neue Zeile `[Trait("Category", "Unit")]` zwischen Z. 11 (namespace) und der bisherigen Z. 12 (class); class verschoben auf Z. 13. **BOM-tragend**: erste 3 Bytes vor/nach Edit `EF BB BF` verifiziert. Bytes 3102 → 3131 (+29). [Fact]-Count: 2.
- **item-05** — `src/AiNetLinter.Tests/Core/Checkers/DynamicTypeCheckerTests.cs` — neue Zeile `[Trait("Category", "Unit")]` zwischen Z. 11 (namespace) und der bisherigen Z. 12 (class); class verschoben auf Z. 13. Kein BOM. Bytes 1010 → 1039 (+29). [Fact]-Count: 1.
- **item-06** — `src/AiNetLinter.Tests/Core/Checkers/LinqChainLengthCheckerTests.cs` — neue Zeile `[Trait("Category", "Unit")]` zwischen Z. 11 (namespace) und der bisherigen Z. 12 (class); class verschoben auf Z. 13. **BOM-tragend**: erste 3 Bytes vor/nach Edit `EF BB BF` verifiziert. Bytes 7147 → 7176 (+29). [Fact]-Count: 7.
- **item-07** — `src/AiNetLinter.Tests/Core/Checkers/MaxPartialClassFilesTests.cs` — neue Zeile `[Trait("Category", "Unit")]` zwischen Z. 12 (namespace) und der bisherigen Z. 13 (class); class verschoben auf Z. 14. Kein BOM. Bytes 7333 → 7362 (+29). [Fact]-Count: 7.
- **item-08** — `src/AiNetLinter.Tests/Core/Checkers/MethodParameterCountAccessibilityTests.cs` — neue Zeile `[Trait("Category", "Unit")]` zwischen Z. 11 (namespace) und der bisherigen Z. 12 (class); class verschoben auf Z. 13. **BOM-tragend**: erste 3 Bytes vor/nach Edit `EF BB BF` verifiziert. Bytes 7595 → 7624 (+29). [Fact]-Count: 11.

**Gesamt:** 8 Dateien modifiziert, je +1 Zeile (insgesamt +8 Zeilen = 8 Insertions), +232 Bytes (8 × 29).

## Commit

- **Code-Commit-Hash:** `44956b7`
- **Message:**
  ```
  test: Checkers-Tests Kategorie-taggen 1/3 [flaky-and-test-performance]

  8 Klassen in Core/Checkers/ (A bis MethodParameterCountAccessibility)
  mit [Trait("Category", "Unit")] auf Klassen-Ebene versehen.
  Standard-Insert zwischen namespace-Deklaration und class-Deklaration,
  kein XML-Doc-Variante und kein // @covers-Block noetig (alle 8 Klassen
  ohne diese Marker). BOM bei den 5 BOM-tragenden Dateien erhalten
  (EF BB BF vor/nach Edit verifiziert), EOL uniform CRLF + Trailing-NL.

  Filter-Delta: Unit 656 -> 706 (+50), Integration 113 -> 113 (+0),
  Total 1325 -> 1325 (+0). Numerische Plausibilitaet per
  Select-String '\[(Fact|Theory)\]' Brutto=Netto=50 (0/8 Dateien mit
  String-Literal-Verschachtelung verifiziert).

  Ref: tasks/flaky-and-test-performance/step-010
  ```
- **Branch:** `main`
- **Push:** nein (lokal)
- **Doku-Commit:** separater, zweiter Commit (Hash steht nicht hier
  drin — Selbstbezug, siehe `git log`).

## Build-/Test-Output

```
dotnet build                                                              → grün (0 Warnungen, 0 Fehler)
dotnet run --project src/AiNetLinter -- --config rules.json --path .      → OK
dotnet test --no-build                                                    → grün (1325 Tests, 0 Fehler, Dauer 1 m 51 s)
dotnet test --no-build --filter "Category=Unit"                           → grün (706 Tests, 0 Fehler, Dauer 10 s)
dotnet test --no-build --filter "Category=Integration"                    → grün (113 Tests, 0 Fehler, Dauer 2 m 3 s)
```

## Numerische Plausibilität (Plan-DoD-Verifikation)

- **Methoden-Inventar pro Datei (regex-basiert per
  `Select-String -Pattern '\[(Fact|Theory)\]'`):**
  AsciiIdentifiersTests=6, AsyncVoidCheckerTests=8,
  BlockingTaskCheckerTests=8, CouplingSemanticTests=2,
  DynamicTypeCheckerTests=1, LinqChainLengthCheckerTests=7,
  MaxPartialClassFilesTests=7, MethodParameterCountAccessibilityTests=11
  = **50 Methoden** (50 `[Fact]` + 0 `[Theory]`). ✓
- **Test-Case-Inventar pro Datei (mit String-Literal-Ausschluss per
  `Where-Object { $_.Line -notmatch '"' }`):** Brutto=50, Netto=50 —
  **0/8 Dateien** mit String-Literal-`[Fact]`-Verschachtelung (analog
  Planer-Verifikation). Damit ist Methoden-Inventar = Test-Case-Inventar
  (kein Diskrepanz-Faktor, kein Mis-count analog step-009
  `AgentFeaturesTests.cs:241`). ✓
- **Filter-Delta:** Unit 656 → **706** (+50 ✓), Integration 113 → 113 (±0 ✓),
  Total 1325 → 1325 (±0 ✓). ✓
- **NITPICK-Linie-Verifikation (step-009):** `Select-String`-Brutto-Count
  pro Datei (50) entspricht exakt dem `dotnet test --filter
  "Category=Unit"`-Delta (+50). ✓
- **Diskrepanz Methoden (50) vs. Test-Cases (50) = 0** — keine
  `[Theory]+[InlineData]`-Expansion und keine String-Literal-Verschachtelung
  im step-010-Batch. ✓

## Abweichungen vom Plan

**Keine — Plan 1:1 umgesetzt.** Alle 8 Klassen erhielten exakt die im
Plan spezifizierte Trait-Zeile `[Trait("Category", "Unit")]` an der
angegebenen Position (Standard-Insert, kein `// @covers`-Block, kein
XML-Doc, kein `: IDisposable`). BOM-Konservierung für alle 5 BOM-Dateien
per Byte-Scan verifiziert (EF BB BF bleibt). EOL und Trailing-NL für alle
8 Dateien per Stichprobe (alle 8 gescannt) bestätigt: CR = LF nach Edit,
letztes Byte = LF.

## Beobachtungen

- **BOM-Inhomogenität in `Core/Checkers/` (Planer-Beobachtung bestätigt):**
  5 von 8 step-010-Dateien mit BOM, 3 ohne — Verteilung 62.5 %/37.5 %.
  Vor Edit verifiziert per `[System.IO.File]::ReadAllBytes` (CR = LF, also
  uniform CRLF; TrNL = Y für alle 8). Konsistent mit Planer-Erwartung.
  Drei verschiedene Ordner, drei verschiedene BOM-Verteilungen sind nun
  im Projekt dokumentiert: `Output/` (0/9 = 0 % mit BOM, einheitlich
  ohne — siehe step-007), `Configuration/` (4/8 = 50/50 — siehe step-009),
  `Core/Checkers/` (10/27 = 37 % mit BOM inkl. der 5 hier verifizierten
  step-010-Dateien, 17/27 = 63 % ohne). Wahrscheinlich gemeinsame Wurzel
  in `core.autocrlf` + Editor-Encoding-Defaults, aber **kein** step-010-
  Scope zur Klärung. **Heuristik-Punkt 8 (in spe, neu in step-010
  beobachtet):** BOM-Inhomogenität als **dritte** Inhomogenitäts-Dimension
  dokumentiert — analog TD-005-Elevation in step-009. **Kein TD-Eintrag
  durch Coder angelegt** (Kritiker-Pflicht); Planer-Hinweis im Plan
  dokumentiert.
- **String-Literal-`[Fact]`-Ausschluss (NITPICK-Linie aus step-009):**
  0/8 Dateien mit String-Literal-`[Fact]`-Verschachtelung (Planer-
  Verifikation unabhängig reproduziert: Brutto=50, Netto=50). Damit
  ist die Diskrepanz Methoden-vs-Test-Cases = 0 (kein Mis-count-Risiko
  analog `AgentFeaturesTests.cs:241`).
- **`MaxPublicMembersPerTypeTests.cs:241`** (außerhalb step-010-Scope):
  enthält laut Planer String-Literal-`[Fact]`-Verschachtelung analog
  `AgentFeaturesTests.cs:241`, ist aber bereits getaggt (Herkunft
  Refactoring-Commit `d744dc9` aus `[codegraph-mcp-finish]`). step-010
  braucht diese Diskrepanz nicht zu behandeln — sie ist für die
  numerische Plausibilität des step-010-Batches irrelevant (Diskrepanz
  liegt in **bereits getaggter** Klasse, nicht in den 8 step-010-Klassen).
- **BOM-Konservierung alle 5 Dateien automatisch durch Standard-Edit:**
  keine Sonderbehandlung (Python-Helper analog step-007) nötig — das
  `edit`-Tool hat die ersten 3 Bytes durchgängig erhalten. Bestätigt
  durch Vor-/Nach-Scan aller 5 Dateien: 5/5 EF BB BF = True.
- **Trait-Platzierungs-Bibliothek:** alle 3 in step-007/008 etablierten
  Varianten (Standard-Insert, `// @covers`-Block-Insert, XML-Doc-Variante)
  sind in den bisherigen 8 Batches angewendet; step-010 brauchte nur
  Standard-Insert. Die Bibliothek ist für die EPIC-02-Folge-Batches
  als abgeschlossen anzusehen.

## Bekannte Unschärfen

- **`MethodParameterCountAccessibilityTests.cs:241`** (außerhalb
  step-010-Scope): Planer-Hinweis im Plan §"Bekannte Ausnahmen", dass
  String-Literal-`[Fact]`-Verschachtelung existiert (analog
  `AgentFeaturesTests.cs:241` aus step-009). Klasse ist bereits getaggt,
  step-010-Scope ist nicht betroffen. **Sollte der Kritiker dennoch
  eine Diskrepanz vermuten:** die step-010-Diskrepanz wäre
  50/50 = 0, das `MaxPublicMembersPerTypeTests`-Problem ist in einem
  **früheren** Refactoring-Commit (nicht in EPIC-02) entstanden und
  außerhalb dieses Steps.

## CodeMap-Update

- `tasks/flaky-and-test-performance/codemap.md`:
  - `last_updated`: `2026-08-07T13:42:00+02:00` → `2026-08-07T14:15:00+02:00`
  - `Core/Checkers/`-Eintrag in Sektion "Test-Verzeichnisse — geplant für
    EPIC-02-Folge-Batches" von "27 Klassen; rein Unit, mehrere Batches
    (zuletzt: step-002)" auf ausführliche Form mit **Pre-Existing-Tag-
    Befund** aktualisiert: 27 Klassen total, davon 7 bereits getaggt
    (namentlich aufgelistet, Herkunfts-Verweis auf
    `[codegraph-mcp-finish]`-Refactoring-Commits im Plan §"Bekannte
    Ausnahmen"); 20 ungetaggte Klassen in 3 alphabetischen Batches
    8+8+4; step-010-Sub-Eintrag mit den 8 Klassen-Namen + `Unit`-Trait
    Vermerk; step-011/012 als ausstehend markiert; `(zuletzt: step-010)`.
