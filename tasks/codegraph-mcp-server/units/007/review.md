---
unit: 007
task: codegraph-mcp-server
workflow: dynamic-loop
type: review
created_by: kritiker
created_at: 2026-08-02
reviewed_commits:
  - 49feb65  fix(baseline): sourcefilecatalog registermsbuild thread-safe (TD-003) [codegraph-mcp-server]
  - 3b29d72  feat(tests): EPIC-07 tests-ausbau (6 dod-bereiche abgesichert) [codegraph-mcp-server]
  - bb0544d  chore(task): unit 007 result, EPIC-07 tests-ausbau + TD-003 race-fix + TD-015/TD-016 cleanup [codegraph-mcp-server]
  - acb8ee4  chore(task): unit 007 result, commit-hashes ergaenzt
---

# Review Einheit 007 — EPIC-07 Tests-Ausbau + TD-003 Race-Fix + TD-015 / TD-016 Cleanup

## Verdict

**`approved`**

Keine CRITICAL- oder MAJOR-Findings. Alle sechs EPIC-07-DoD-Bereiche sind
abgesichert, TD-003 ist strukturell korrekt gefixt, TD-015 sauber entfernt,
TD-016 transparent als Teilschluss dokumentiert. A3-Disziplin eingehalten
(strukturelle A3-Nachweise vorhanden; eine ehrliche Selbstaussage des Coders
maskiert einen funktionalen A3 als Symptom-Maskierung, der strukturelle A3
reicht aber aus). Konzepttreue: keine Scope-Überschreitung, keine
Konzept-/Regel-/Kernel-Edits.

---

## Plan-Erfüllung

| Plan-Punkt | Status | Beleg |
|---|---|---|
| (a) Integrationstest je Tool (9/9) | dokumentiert als „Lücke nicht-existent" | `result.md` Abschnitt „What changed" / Plan-Check 1 |
| (b) Staleness-Invalidierung E2E | ✓ 1 Test | `McpServerCommandStalenessTests.cs:31` |
| (c) Miss-Hint komplett E2E | ✓ 1 Test | `McpServerCommandMissHintTests.cs:22` |
| (d) Mehrdeutigkeits-Abbruch E2E | ✓ 1 Test | `McpServerCommandAmbiguityE2ETests.cs:32` |
| (e-i) Cache-Filename-Isolation | ✓ 1 Test | `AnalysisCacheManagerIsolationTests.cs:29` |
| (e-ii) Cache-Filename-Gleichheit | ✓ 1 Test | `AnalysisCacheManagerIsolationTests.cs:49` |
| (e-iii) MCP-Disk-Cache-Bypass | ✓ 1 Test (Reflection) | `McpServerCommandCacheBypassTests.cs:30` |
| Bonus: Cache `rules.json`-Variante | +1 Test | `AnalysisCacheManagerIsolationTests.cs:67` |
| Bonus: Cache Case-Insensitive | +1 Test | `AnalysisCacheManagerIsolationTests.cs:87` |
| (f) CLI-Regression Mini-Fixture | ✓ 1 Test | `CliBatchRegressionTests.cs:32` |
| TD-003 strukturell + Test | ✓ | `49feb65` + `SourceFileCatalogRegisterMSBuildTests.cs` (3 Tests) |
| TD-015 Dead Code weg | ✓ | `McpToolResults.cs` 134 → 122 Z., `rg "WarningsSection"` → 0 Treffer |
| TD-016 Fixture-Refactor (Teil) | ✓ Teilschluss dokumentiert | 2/4 Fixtures refaktoriert, Folge-Refactor in `result.md` benannt |
| 1 internal `CachePath` für Tests | ✓ | `AnalysisCacheManager.cs:31` |
| 2 Code-Commits (TD-003 + EPIC-07) | ✓ (4 Commits gesamt, davon 2 für Code + 2 für `result.md`-Lifecycle) | siehe „Commit-Disziplin" |
| `McpServerCommandTests.cs` unangetastet | ✓ | `git diff 83f414c 3b29d72` = 0 Zeilen |
| Kein Push, kein Amend, kein `-A` | ✓ | Working-Tree clean, `main`, 3 ahead of `origin/main` (alle von Ralf nach 007) |

**Test-Anzahl:** 12 neue Tests (8 unit + 4 E2E-Integration). Volllauf
1161/1161 in 5:55 min am 2026-08-02 17:10 als offizielle Abschluss-Verifikation
gemäß AGENTS.md §2.

**Build/Test-Verifikation durch Coder:** `dotnet build` 0/0,
`dotnet test --filter Category=Unit` 80/80 in 22 s, gezielter
E2E-Slice 8/8 in 29 s, Sanity-Slice 9/9 in 30 s.

---

## Findings

### CRITICAL

_keine_

### MAJOR

_keine_

### MINOR

- **`src/AiNetLinter.Tests/Commands/CliBatchRegressionTests.cs:32` —
  irreführender Test-Methodenname.** Methode heißt
  `RunLinterCli_OnSymbolGraphMiniFixture_ReportsViolationAndExitsZero`,
  assertiert aber `process.ExitCode == 1` (Z. 65-68). Der Coder hat im
  `result.md` (A3-8-Anmerkung) korrekt erkannt, dass der Plan-Widerspruch
  „Exit 0 + ViolationTrigger" unhaltbar war und stattdessen Exit 1 +
  Violation-Trigger (korrektes CLI-Verhalten bei Verletzungen)
  implementiert — nur den Methodennamen hat er nicht mitgezogen. Kosmetik,
  aber wer den Test-Code später liest, wird unnötig verwirrt. Kein
  Build- oder Test-Impact. Sollte bei nächster Gelegenheit umbenannt
  werden (z. B. `..._ExitsNonZero_WithViolationTriggerInOutput`).

---

## Sonstige Beobachtungen (MINOR / informativ)

- **A3-2-Maskierungs-Hinweis des Coders ist korrekt und wichtig.** Der
  funktionale Test `LoadAsync_TwentyParallelCallsAcrossFixtures_AllSucceed`
  (`SourceFileCatalogRegisterMSBuildTests.cs:51`) wäre auch **vor** dem
  TD-003-Fix grün, weil das bestehende `try/catch` in
  `RegisterMSBuild` (`SourceFileCatalog.cs:243-246`) die
  `InvalidOperationException` schluckt. Der Coder hat das in
  `result.md` A3-2 explizit dokumentiert und A3-1 (Reflection auf
  `_msbuildRegistrationLock`) als den eigentlichen A3 markiert. A3-1 ist
  valide: das Lock-Feld existiert oder existiert nicht, das ist ein
  strukturell scharfer Test. Die funktionalen Tests A3-2 / A3-3 sind
  Smoke-Tests, die das Lock-Feld **voraussetzen** und nur die korrekte
  Lock-Semantik mitprüfen. Kein Finding — ausdrücklich als saubere
  Selbst-Aussage gewürdigt. (Auf die Frage aus dem Auftrag: „reichen 20
  parallele Calls als Race-Beweis?" — nein, das wäre Symptom-Maskierung.
  Der A3-1 strukturelle Test ist der eigentliche Race-Beweis.)

- **Test-Infrastruktur `McpTestClient` / `SymbolGraphMcpFixture` /
  `TestTempDirectory` wurde bereits in `3b29d72` von der Coder-Session
  benutzt** (steht auch im `result.md` A3-4 / A3-5 implizit). Per
  Commit-Git-Log wurde sie ursprünglich in `0e27af4`
  (`test(mcp): C# MCP Test-Harness ...`) angelegt. Die Commit-Reihenfolge
  zeigt, dass die EPIC-07-Tests von Anfang an die später „offiziell"
  benannten Fixtures genutzt haben — kein Konflikt mit der
  Ralf-Anmerkung im Auftrag („NEU in 3b315c2"), die Ralf-Fixtures selbst
  betrifft. Kein 007-Finding.

- **Plan-Disziplin (a): „Integrationstest je Tool" als abgeschlossen
  markiert ohne neuen Test** ist sauber: der Plan hat das in
  `units/007/plan.md` Check 1 explizit als Entscheidung begründet
  (zentrale Datei 499/500 Z., 9/9 Tools bereits in 001/004/005
  abgedeckt). Coder hat das so dokumentiert. Konsistent.

- **Commit-Aufteilung 2 → 4 Commits** ist kein Verstoß gegen A4. Der
  Plan nannte 2 Code-Commits, daraus wurden 2 Code-Commits (`49feb65` +
  `3b29d72`) + 2 Task-Artefakt-Commits (`bb0544d` + `acb8ee4`,
  beide `result.md` betreffend). Task-Commits werden im Repo seit
  Einheit 001 analog ausgesondert. `bb0544d` ist `result.md`-Initial,
  `acb8ee4` ergänzt Commit-Hashes (offensichtlich, weil die Commits
  zeitlich nach dem `result.md`-Schreiben finalisiert wurden). Kein
  Amend, kein Push, kein `-A`, kein `.` — A4 sauber erfüllt.

- **`McpServerCommandTests.cs` ist 359 Z. im aktuellen Working-Tree** —
  die Reduktion von 426 → 359 (gegenüber dem im `result.md`
  dokumentierten Stand) erfolgte in den Ralf-Commits `3b315c2` /
  `4f6fa6f` durch Umzug von Test-Logik in Class-Fixtures. **Nicht** Teil
  der 007-Einheit, kein 007-Befund. In `3b29d72` selbst ist die Datei
  unangetastet (Diff = 0 Zeilen) — das ist die einzige 007-relevante
  Aussage, und die stimmt.

- **Konzepttreue „Dogfooding pro Tool-Step" und „Python-Skripte verboten"**
  ist nicht betroffen: 007 hat kein neues Tool eingeführt, und kein
  Python-Skript wurde angelegt. `rg` über das `.todos/`-Verzeichnis
  wäre der Sanity-Check, aber er ist hier entbehrlich, weil 007 rein
  Test-Erweiterung + Dead-Code-Removal + ein Bugfix ist.

- **A4-Konformität der vier Commits:** Conventional Commits auf Englisch,
  Imperativ, `[codegraph-mcp-server]`-Suffix konsistent zu 001-006.
  `49feb65` ist `fix(baseline)`, `3b29d72` ist `feat(tests)`, beide
  Task-Commits sind `chore(task)`. Sauber.

---

## Tech-Debt-Aktionen

### 1. TD-003 — Status-Update (vom Coder in `result.md` explizit angefragt)

Coder hat TD-003 strukturell + testseitig geschlossen, den `tech-debt.md`-
Eintrag aber bewusst dem Kritiker überlassen (A6/A2). **Vorgeschlagener
Volltext** (vom Orchestrator nach diesem Review in `tech-debt.md`
einzupflegen, Index-Zeile + Eintrag-Body):

**Index-Zeile** (ersetzen):

```
| TD-003 | `src/AiNetLinter/Baseline/SourceFileCatalog.cs` (`RegisterMSBuild`) | mittel | ~~Nicht-thread-sicherer Check-then-Act führt bei parallel laufenden Testklassen intermittierend zu `InvalidOperationException`.~~ **Geschlossen durch Einheit 007** (Commit `49feb65`): statisches Lock + Check-Lock-Check-Pattern + 3 Tests (Reflection + 20 parallele `LoadAsync`-Calls + Idempotenz). |
```

**Eintrag-Body** (Status-Block ersetzen):

```
- **Status:** **geschlossen** durch Einheit 007 (Commit `49feb65`): `SourceFileCatalog.RegisterMSBuild` mit `private static readonly object _msbuildRegistrationLock` + Check-Lock-Check-Pattern abgesichert. Struktureller A3: `RegisterMSBuild_HasStaticLockField_ForThreadSafeRegistration` (Reflection auf das Feld). Funktionale Verifikation: `LoadAsync_TwentyParallelCallsAcrossFixtures_AllSucceed` (smoke) + `LoadAsync_SecondSequentialCall_DoesNotRepatchBuildHost` (Idempotenz). Klasse von 286 auf 302 Z. gewachsen (vor 007: 286; +Lock-Feld + Kommentar; gut innerhalb `MaxLineCount: 500`). Workaround 006 (`ConsoleTestCollection`) bleibt als zusätzliche Schicht bestehen, ist aber nicht mehr die einzige Absicherung.
```

### 2. TD-016a — Neuer Eintrag (Vorschlag des Coders aufgegriffen)

Coder hat in `result.md` Abschnitt „TD-016 — geschlossen (mit
Teilschluss-Anmerkung)" explizit auf die Lücke hingewiesen: der
Refactor in `6c872e4` hat nur 2 von 4 Fixture-Workspace-Klassen
abgedeckt. `CompileErrorMiniFixtureWorkspace` (71 Z.) und
`GitImpactMiniFixtureWorkspace` (166 Z.) duplizieren weiterhin
`CopyFixture` / `IsGeneratedPath` / `FindSolutionRoot` 1:1 (per
`grep` auf den beiden Dateien bestätigt: jede der drei Helper
kommt wortgleich vor, jeweils als `private static` in der jeweiligen
Klasse). Der Coder schlägt TD-016a oder inline-Mitnahme beim nächsten
Fixture-Block vor. Als Kritiker werte ich das als tauglichen Eintrag.

**Vorgeschlagener Volltext** für `tech-debt.md` (Index-Zeile +
Eintrag-Body):

**Neue Index-Zeile** (am Ende der Tabelle einfügen, nach TD-016):

```
| TD-016a | `src/AiNetLinter.Tests/Fixtures/{CompileErrorMini,GitImpactMini}FixtureWorkspace.cs` | niedrig | Folge-Refactor aus TD-016: zwei der vier Fixture-Workspace-Klassen wurden in `6c872e4` nicht auf `FixtureWorkspaceBase` umgestellt und duplizieren weiterhin `CopyFixture`/`IsGeneratedPath`/`FindSolutionRoot`. |
```

**Neuer Eintrag-Body** (nach TD-016 einfügen):

```
### TD-016a — TD-016-Folge: 2 verbleibende Fixture-Klassen noch nicht refaktoriert [Priorität: niedrig]

- **Ort:** `src/AiNetLinter.Tests/Fixtures/CompileErrorMiniFixtureWorkspace.cs` (71 Z.) und `src/AiNetLinter.Tests/Fixtures/GitImpactMiniFixtureWorkspace.cs` (166 Z.).
- **Befund:** Beim TD-016-Refactor in `6c872e4` wurden `BaselineMiniFixtureWorkspace` (20 Z.) und `SymbolGraphMiniFixtureWorkspace` (20 Z.) auf `FixtureWorkspaceBase` (73 Z.) umgestellt — die beiden Klassen mit Zusatzlogik (`CompileErrorMini`: Compile-Fehler-spezifische Helper, `GitImpactMini`: `InitializeGitRepoWithInitialCommit`) wurden **nicht** migriert. `grep` bestätigt: `CopyFixture` / `IsGeneratedPath` / `FindSolutionRoot` kommen in beiden Klassen weiterhin wortgleich als `private static`-Methoden vor, parallel zur identischen Implementierung in `FixtureWorkspaceBase`. **Erkannt im Review von Einheit 007** (Coder-Beobachtung in `result.md` Abschnitt „TD-016 — geschlossen (mit Teilschluss-Anmerkung)").
- **Vorschlag:** **Inline** beim nächsten Fixture-Block (z. B. wenn EPIC-08 Last-Fixture-Generierung aus P1-6 eine weitere Fixture braucht). Planer entscheidet, ob ein eigenständiger Refactor (TD-016a-Einheit, ~1-2 h) oder inline-Mitnahme sinnvoller ist. Risikofaktor bei `GitImpactMiniFixtureWorkspace`: die Git-Init-Logik muss beim Umbau auf eine gemeinsame `TestTempDirectory` mit-konsolidiert werden, sonst gehen Initial-Commits verloren.
- **Status:** offen
```

### 3. Keine neuen Findings im 007-Scope

Alle Beobachtungen, die nicht in den Tech-Debt-Vorschlag oben passen,
sind unter „Sonstige Beobachtungen" als MINOR-Informativ vermerkt —
kein Bedarf für einen weiteren TD-Eintrag.

---

## Zusammenfassung (für Orchestrator)

- **Verdict:** `approved` — keine Folge-Runde nötig.
- **Empfohlene Commits durch Orchestrator** (auf Basis dieses Reviews,
  gemäß A4):
  1. `chore(task): unit 007 review, approved [codegraph-mcp-server]`
     → `tasks/codegraph-mcp-server/units/007/review.md` (diese Datei).
  2. `chore(task): TD-003 status geschlossen durch 007 + TD-016a neu
     [codegraph-mcp-server]` → `tasks/codegraph-mcp-server/tech-debt.md`
     (Volltext oben).
- **Nächste Arbeitseinheit (vom Planer, nicht von mir):** TD-016a ist
  ein naheliegender Folge-Refactor (2-4 h, niedrige Priorität) — kann
  standalone oder inline beim nächsten Fixture-Block laufen. EPIC-08
  (Doku) und P0/P1-Rest-Erweiterungen aus `konzept.md` Z. 207-324
  bleiben davon unabhängig.
