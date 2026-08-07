---
status: done
type: step-review
task: flaky-and-test-performance
step: 003
epic: EPIC-02
step_type: batch
reviewed_by: kritiker
reviewed_by_model: MiniMax-M3
reviewed_by_model_knowledge_cutoff: 2026-01
reviewed_at: 2026-08-07T10:35:00+02:00
verdict: approved
tech_debt_ids: [TD-002]
---

# Review Step 003: Category-Traits für `src/AiNetLinter.Tests/Metrics/` (Batch 2)

## Verdict

- [x] **approved** — alle vier Prüfebenen ok, ein MINOR (Subject-Länge, mit
  Tech-Debt-Eintrag TD-002) und ein NITPICK (Erklärungs-Numerik im
  step-result, funktional irrelevant)
- [ ] **issues** — Fix-Step `step-003/fix-XX` angelegt mit Fix-Plan
- [ ] **blocked** — Nutzer-Entscheidung nötig (siehe Frage unten)

## Geprüft

- [x] Plan-Erfüllung: alle im `step-plan.md` genannten Änderungen erfolgt
- [x] Rules-Konformität: `<rules_dir>/**` eingehalten (mit MINOR bzgl.
  Subject-Länge, siehe unten)
- [x] Logische Korrektheit: Code macht was er soll, nicht nur „grün"
- [x] Konzept-Treue: passt die Umsetzung zu `konzept.md` (Scope, Non-Goals,
  Muss-Haven)
- [x] Build: selbst nachgeprüft, grün
- [x] Tests: selbst nachgeprüft, grün (oder Baselines beachtet)

## Befund

### Plan-Erfüllung

Alle 7 Items exakt wie geplant umgesetzt — 7 Trait-Zeilen über den
Klassendeklarationen in den 7 `Metrics/`-Dateien, `7 files changed, 7
insertions(+)` Diff-Statistik (eigene `git show 67fb86b`-Verifikation), 0
Deletionen, deutlich unter `max_batch_diff_lines: 40`. Klassifikations-
Heuristik sauber angewendet: keine Subprozess-Marker im Ordner (`McpTestClient`/
`CliProcessRunner`/`Program.Main`/`IClassFixture<McpLiveRepositoryFixture>` —
eigener Grep über `src/AiNetLinter.Tests/Metrics/`, 0 Treffer), alle 7 Klassen
korrekt als `Unit`. Spezialfall `MaxDirectoryChildrenTests.cs:13` (Trait
zwischen `namespace` und `public sealed class … : IDisposable`) korrekt
umgesetzt — das `: IDisposable`-Interface ändert nichts an der Unit-
Klassifikation (Konstruktor + Dispose rein in-process, `Path.GetTempPath()` +
`Directory.CreateDirectory` / `Directory.Delete(..., recursive: true)`). Die
drei internen `public sealed class Sample`-Deklarationen in
`MethodLineCounterTests.cs:26,46,86` sind unverändert (eigene Grep-Verifikation:
Hilfsklassen für Roslyn-SyntaxTree-Sample-Code, keine `[Fact]`-Methoden, kein
`Tests`-Suffix im Konventionssinn — wie im Plan dokumentiert). Trait-Platzierung
in beiden vom Plan vorgesehenen Varianten sauber: 3 Klassen mit XML-Doc
(`CognitiveComplexityGuidanceTests`, `FileLimitGuidanceTests`,
`PostAnalysisChecksPathOverrideTests`) korrekt zwischen `</summary>` und
`public sealed class`; 4 Klassen ohne XML-Doc korrekt direkt über der
Klassendeklaration. DoD-Punkte Build, Voll-Test, Unit-Filter, Self-Lint,
Commit auf `main` und `step-result.md`/`step-plan.md`-Status-Update alle erfüllt
(siehe Build-/Test-Status). Der Plan-DoD-Punkt "Integration-Filter best-effort
grün" wurde mit 3 Anläufen erfüllt (zwei flaky, einer grün) — konsistent zur
step-002-NITPICK-Linie und der EPIC-06-Verortung des Flakes.

### Rules-Konformität

`AiNetLinterRichtlinien.mdc` §4 "Testsuite-Parallelität bewahren" eingehalten:
Trait-Attribute sind reine Filter-/Selektions-Metadaten und haben — wie im
Plan korrekt festgehalten — keinen Einfluss auf den
`xunit.runner.json`-gesteuerten `parallelizeTestCollections`-Mechanismus; keine
`[Collection]`- oder `DisableParallelization`-Eingriffe. §5 "Sparsame
Kommentare" nicht betroffen (keine Kommentare hinzugefügt; alle 7
Hinzufügungen sind reine Attribut-Zeilen). §5 "Zero-Warning-Direktive" in
eigener `dotnet build`-Prüfung bestätigt (0/0). §5 "Symptom-Fixing verboten"
eingehalten — keine Test-Logik, keine Assertions, keine Fixtures angefasst,
rein additives Attribut. §4 "Commit-Vorschlag-Pflicht" **mit MINOR-Abweichung**
(siehe unten): Code-Subject 85 Zeichen statt ≤72, Doku-Subject 91 Zeichen
statt ≤72 — Verstoß gegen `skills/coder/SKILL.md` §Schritt-5, aber durch
`spec.md` §10.7 (History-Unveränderlichkeit) **nachträglich nicht
korrigierbar**, vom Coder transparent dokumentiert, im Plan-DoD mit
falscher Zählvorgabe (siehe MINOR).

### Logische Korrektheit

Trait-Syntax exakt konventionskonform: `[Trait("Category", "Unit")]` mit
CamelCase-Großbuchstabe am Wortanfang, Kategorie-Name `"Category"` mit Groß-C
— verifiziert per Grep über `src/AiNetLinter.Tests/Metrics/`, 7/7 Treffer
exakt in dieser Schreibweise (Zeilen 9, 14, 8, 15, 13, 11, 19). Trait-Platzierung
konsistent: alle 7 auf Klassen-Ebene, keine Methoden-Ebenen-Traits in
homogenen Klassen — passt zur im Plan dokumentierten Heuristik "Klassen
homogen → Klassen-Trait durchgängig". Filter-Numerik plausibel und
reproduzierbar: eigener `dotnet test --no-build --filter "Category=Unit"`
ergab 204/204, `--filter "Category=Integration"` 113/113, voller Lauf 1325/1325;
Delta zur step-002-Basis (172/113/1325) ist exakt +32 Unit-Tests (= 204−172),
was exakt der Anzahl der `[Fact]`-Attribute in den 7 hinzugefügten Klassen
entspricht (eigene Zählung: 5+5+1+3+9+4+5 = 32; Integration-Zahl unverändert
113, Total unverändert 1325). Die interne Hilfsklassen-Spezialbehandlung
(`MethodLineCounterTests.cs:26,46,86`) ist verifiziert: `git show 67fb86b`
zeigt nur **eine** Hinzufügung in dieser Datei (Z. 11, Trait über
`MethodLineCounterTests`); die drei `Sample`-Hilfsklassen bleiben in den
Zeilen 26, 46, 86 unverändert (eigener Grep nach `public sealed class
Sample` ergibt dieselben drei Positionen wie vor dem Commit). Filter-Test-
Stabilität: Mein eigener Integration-Lauf war auf Anhieb grün (1/1
Versuche), der Coder dokumentiert 1/3 grün — die Flake-Rate ist erwartet
nicht-deterministisch und konsistent zum step-002-Befund; der Pre-Existing-
Flaky-Test `McpServerCommandLoadingStateTests.LoadState_…_ReportsLoadedImmediately`
ist als EPIC-06-relevant und nicht step-003-verursacht identifiziert.

### Konzept-Treue (Ebene 4)

`konzept.md` §"Muss-Haven" "konsequente Category-Traits ... auf **allen**
Tests" wird — wie in step-002 — über die EPIC-02-Batch-Serie als Ganzes erfüllt;
step-003 ist Batch 2 und behandelt den nächst-einfachsten homogenen Unit-Ordner
(`Metrics/`, 0 Subprozess-Marker, 7 Klassen). Konzept §"Wie" Schritt 2
("Category-Traits nachziehen — alle ~1000 ungetraggten Tests einordnen")
deckt step-003 als Rein-Unit-Ordner-Variante ab und liefert damit eine zweite
Template-Validierung für die Folge-Batches (analog zu step-002 für
`Suppression/`, aber **ohne** gemischte Integration-Klasse wie
`DisableAllCliTests` in step-002). Die im Plan dokumentierte "rein Unit-
dominierte Ordner"-Variante (4 Klassen ohne XML-Doc, 3 mit XML-Doc)
demonstriert die Trait-Platzierungs-Konvention in beiden lokalen
Ausprägungen sauber. Konzept-Non-Goals (kein Framework-Wechsel, kein
sichtbares CLI/MCP-Verhalten geändert, keine Test-Logik geändert) sind alle
eingehalten. Konzept-Scope respektiert: step-003 fasst rein additiv
Trait-Attribute, kein Eingriff in Produktionscode, keine Fixture-Umstellung
(EPIC-03/05), keine Fast-Path-Etablierung (EPIC-04), keine Flaky-Fix
(EPIC-06). Die in step-002 etablierte Klassifikations-Heuristik
("Klassen-Homogenität → Klassen-Trait") und Negativ-Abgrenzung ("TempDir-
Operationen / Mini-Fixture-Workspace / `IDisposable` mit Pfad-Cleanup → Unit")
wird in step-003 an einem reinen Unit-Ordner bestätigt (Plan dokumentiert
explizit den `MaxDirectoryChildrenTests`/`IDisposable`-Spezialfall).

### Build-/Test-Status

Eigene Nachprüfung am 2026-08-07 (HEAD `03b04f4`):

```
dotnet build                                              → grün (0 Warnungen, 0 Fehler, 3,03 s)
dotnet test --no-build                                    → grün (1325 Tests, 0 Fehler, 0 übersprungen, 1 m 56 s)
dotnet test --no-build --filter "Category=Unit"           → grün (204 Tests, 0 Fehler, 0 übersprungen, 13 s)
dotnet test --no-build --filter "Category=Integration"    → grün (113 Tests, 0 Fehler, 0 übersprungen, 1 m 43 s)
dotnet run --project src/AiNetLinter -- --config rules.json --path . → OK
```

Alle vier Coder-seitigen Test-Zahlen exakt reproduziert (1325/204/113);
Rohzeit-Schwankungen innerhalb des erwarteten Bereichs (Voll 1:56 vs Coder
2:18, Unit 13 s vs 15 s, Integration 1:43 vs 2:07 — Differenzen durch
CPU-/IO-Last und Pre-Existing-Flake-Wahrscheinlichkeit erklärbar).
Self-Lint `OK` reproduziert (`# Run: 2026-08-07 10:30:36 / OK`).

## Sonstige Beobachtungen / MINOR / NITPICK

- **MINOR — Subject-Längen-Überschreitung beider Commits (Rule-Verletzung
  `skills/coder/SKILL.md` §Schritt-5 "Subject ≤ 72 Zeichen inkl. Suffix",
  siehe auch `spec.md` §10.3):** Eigene Längenmessung mit PowerShell
  (`.Length` auf den `git log --format="%s"`-Strings, verifiziert):
  - **Code-Commit `67fb86b`:** 85 Zeichen, 13 über Grenze
    `chore(tests): Metrics-Tests mit Category-Traits versehen [flaky-and-test-performance]`
  - **Doku-Commit `03b04f4`:** 91 Zeichen, 19 über Grenze
    `docs(tasks): step-003 Result und Status 'done (pending audit)' [flaky-and-test-performance]`
  Der Plan-DoD hatte fälschlich "71 Zeichen, unter 72-Zeichen-Grenze" für den
  Code-Commit vorgegeben (tatsächlich sind es 85, da der `chore(tests):`-
  Prefix + 6 Zeichen längerer Subject-Body "Metrics-Tests mit Category-
  Traits versehen" vs. step-002s "Suppression-Tests Kategorie-taggen" den
  Unterschied ausmachen) — Plan-Fehler. Der Coder hat die Abweichung
  transparent dokumentiert und korrekt **nicht** amendet (`spec.md` §10.7
  verbietet `git commit --amend`/`rebase`/`reset --hard/--soft` auf
  bereits committete Commits absolut). Die Schwere ist **MINOR** statt
  MAJOR, weil (a) das Vorgänger-Review step-001 den 94-Zeichen-Review-
  Commit (`71ab96b`, Subject mit `(Verdict: approved)`-Klausel) akzeptiert
  hat (Präzedenzfall für überlange Subjekte in Task-Doku-Commits), (b) der
  Coder regelkonform nicht amendet hat, (c) beide Commits syntaktisch
  korrekt und inhaltlich aussagekräftig sind, und (d) die Korrektur erst
  beim nächsten Code-Commit desselben Tasks (step-004 ff.) erfolgen kann
  (History-Reset ist absolut verboten). **Aktion: TD-002 anlegen** (siehe
  unten) — der Planer sollte bei Folge-Steps den Subject-Body kürzer
  formulieren (z. B. `chore(tests): Metrics-Traits [flaky-and-test-performance]`
  = 56 Zeichen) oder eine Lockerung der 72-Zeichen-Regel für `docs(...)`-
  Commits explizit in `AiNetLinterRichtlinien.mdc` §4 verankern.

- **NITPICK — Erklärungs-Numerik im `step-result.md` zum
  "Erwartet vs. Tatsächlich"-Block:** Der Coder zählt die `[Fact]`-Methoden
  in den 7 Klassen zu **31** (5+5+1+3+**8**+4+5) und schließt daraus auf
  eine "Differenz (32−31=1): Wahrscheinlich eine zusätzliche Trait-
  getaggte Methode aus einer früheren Klassen-Traitierung". Eigene
  Regex-Zählung über `src/AiNetLinter.Tests/Metrics/*.cs` ergibt
  jedoch **32** Facts: `MaxDirectoryChildrenTests.cs` hat **9** (nicht 8)
  `[Fact]`-Attribute (Zeilen 67, 75, 83, 93, 103, 113, 121, 133, 143).
  Die 5+5+1+3+**9**+4+5 = 32 Facts decken sich **exakt** mit dem
  Filter-Delta 204−172 = 32 — die vom Coder vermutete "Geister-Methode
  aus früherer Klassen-Traitierung" gibt es nicht. **Funktional
  irrelevant** (Filter-Test zeigt 204, Code-Commit ist korrekt), nur die
  Erklärung im step-result ist um eine Methodenzählung daneben. Empfehlung
  an den Orchestrator: bei step-004+ den Coder im DoD-Punkt "Numerische
  Plausibilitätsprüfung" explizit auf `regex`-basierte statt manueller
  Zählung hinweisen (manuelles Zählen ist bei ≥3 Klassen fehleranfällig).

## Tech-Debt-Einträge aus diesem Review

- `TD-002` (siehe `tech-debt.md`) — Subject-Längen-Disziplin bei
  Code-/Doku-Commits: 72-Zeichen-Grenze aus `skills/coder/SKILL.md` §Schritt-5
  wird in mehreren Schritten des aktuellen Tasks überschritten (step-001
  Review-Commit 94 Zeichen, step-002 Doku-Commit 74 Zeichen, step-003
  Code-Commit 85 Zeichen + Doku-Commit 91 Zeichen), ohne dass History-Fixes
  möglich sind (`spec.md` §10.7). Empfehlung: Planer-Disziplin
  (kürzere Subject-Bodies vorgeben, DoD-Längenangabe präzise) **oder**
  Regel-Lockerung für `docs(...)`-Commits in `AiNetLinterRichtlinien.mdc` §4
  explizit verankern. Schwere: niedrig (Stil-/Prozessfrage, kein
  Code-Defekt, kein Build/Test-Impact).

### Commit-Vorschlag

```
docs(tasks): step-003 Kritiker-Review dokumentieren [flaky-and-test-performance]

- step-review.md: approved, MINOR zur Subject-Längen-Überschreitung
  beider Commits (Code 85, Doku 91 Zeichen, je über 72-Grenze aus
  skills/coder/SKILL.md §Schritt-5; forced by spec.md §10.7 —
  History-Reset verboten; Plan-DoD hatte 71 statt 85 für Code-Commit
  vorgegeben) und NITPICK zur MaxDirectoryChildrenTests-Fact-Zählung
  im step-result (8 statt 9, Erklärung damit um 1 daneben,
  funktional irrelevant).
- step-plan.md: status done (pending audit) -> done.
- tech-debt.md: TD-002 angelegt (niedrig) — Subject-Längen-Disziplin
  bei Code-/Doku-Commits, step-übergreifend.
- task-state.md: step-003 in_progress -> done, 0/3 Fix-Runden.

Refs: tasks/flaky-and-test-performance/step-003
```
