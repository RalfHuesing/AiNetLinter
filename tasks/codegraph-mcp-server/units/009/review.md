---
unit: 009
task: codegraph-mcp-server
workflow: dynamic-loop
type: review
created_by: kritiker
created_at: 2026-08-02
trigger: units/009/result.md (Commit 5ea191e)
---

# Review Einheit 009 — TD-016a: 2 verbleibende Fixture-Workspaces auf `FixtureWorkspaceBase` umstellen

## Verdict

**approved**

Keine CRITICAL- oder MAJOR-Findings. Der Refactor ist 1:1 zum Plan, A3 echt gefahren
mit wortwörtlichen Failure-Outputs, die TD-016a-Schließung im `tech-debt.md` korrekt
(Index + Body + Frontmatter), die Commit-Disziplin sauber, A7/A8 eingehalten, der
Pflicht-Volllauf 1173/1173 grün dokumentiert. Eine MINOR-Inkonsistenz (Zeilenzahlen
Body vs. Realität) und eine nennenswerte Bonus-Beobachtung (CS0108 als zweite
A3-Schicht) gehen unter "Sonstige Beobachtungen" — keine Verzögerung.

## Plan-Erfüllung

| Punkt | Soll | Ist | Status |
|---|---|---|---|
| `CompileErrorMiniFixtureWorkspace` erbt von `FixtureWorkspaceBase` | ja | ja, Z. 13 | ✓ |
| Konstruktor delegiert an `base("CompileErrorMini", "ainetlinter-compile-error-mini")` | ja | ja, Z. 15-18 | ✓ |
| 3 `private static`-Helper (`CopyFixture`/`IsGeneratedPath`/`FindSolutionRoot`) gelöscht | ja | ja, alle weg | ✓ |
| `PathFor` bleibt, löst `RootPath` über die Basis auf | ja | ja, Z. 20 | ✓ |
| `RootPath` und `Dispose` aus dem Derived entfernt (von Basis geerbt) | ja | ja, beide nicht mehr in der Datei | ✓ |
| CompileErrorMini-Footprint ~25 Z. | ~25 Z. | **21 Z.** (besser als Plan) | ✓ mit Bonus |
| `GitImpactMiniFixtureWorkspace` erbt von `FixtureWorkspaceBase` | ja | ja, Z. 13 | ✓ |
| Konstruktor delegiert + `InitializeGitRepoWithInitialCommit()` als Post-Base-Aktion | ja | ja, Z. 15-19 | ✓ |
| 3 `private static`-Helper gelöscht | ja | ja, alle weg | ✓ |
| `Dispose` als `override` mit `ClearReadOnlyAttributes(RootPath)` **vor** `base.Dispose()` | ja (kritisch) | ja, Z. 53-57 — Reihenfolge korrekt | ✓ |
| `RunGit` und `ClearReadOnlyAttributes` bleiben (sind GitImpactMini-spezifisch) | ja | ja, beide unverändert (Z. 64-72, 83-117) | ✓ |
| GitImpactMini-Footprint ~125 Z. | ~125 Z. | **118 Z.** (besser als Plan) | ✓ mit Bonus |
| `TD016aRefactorTests.cs` mit 2 Reflection-Theories als A3-Sicherung | optional, empfohlen | 48 Z., 2 Theories, 8 Test-Invokationen, sealed, `[Trait("Category","Unit")]` | ✓ mitgenommen |
| Test 1 `Workspace_InheritsFromFixtureWorkspaceBase` mit wortwörtlichem Failure-Output | ja | ja, im `result.md` A3-1-Block dokumentiert | ✓ |
| Test 2 `Workspace_DoesNotDefineDuplicatedHelper` mit wortwörtlichem Failure-Output | ja | ja (geplant: Test rot, realität: CS0108-Compiler bricht Build — siehe Bonus) | ✓+ |
| `tech-debt.md` Frontmatter `last_updated` | „TD-016a geschlossen durch 009" | ja, Z. 5 | ✓ |
| `tech-debt.md` Index-Zeile TD-016a Status auf „geschlossen" | ja | ja, Z. 46 mit Strikethrough + Commits `b0c2283`+`8f0427e` referenziert | ✓ |
| `tech-debt.md` Body TD-016a Status analog TD-003/012/013/015/016 | ja | ja, Z. 169-194 mit Bullet-Liste, CS0108-Bonus dokumentiert | ✓ |
| Volllauf `dotnet test --no-build` (AGENTS.md §2 Pflicht) | ja, alle 1173 grün | ja, 1173/1173 grün in 6:20 min, dokumentiert | ✓ |
| Conventional-Commits deutsch/englisch, Imperativ, `[codegraph-mcp-server]`-Suffix | ja (englisch für 009-Commits) | 4 Commits: `refactor(tests)`, `test(tests)`, `chore(debt)`, `chore(task)` — alle mit Suffix, englisch | ✓ |
| Gezielter `git add`, kein `-A`/`.`, kein Push, kein Amend | ja | ja, Working-Tree nach Commits clean (siehe Commit-Disziplin) | ✓ |
| A5: keine kosmetischen Edits an den 2 refaktorierten Klassen | ja | nur die im Plan explizit genannten Edits | ✓ |
| A7: kein Edit an `konzept.md`, `kernel.md`, Rollen-Dateien, `.agents/rules/**`, `rules.json` | ja | ja — `git diff 39c4caa..HEAD` für diese Pfade ist leer | ✓ |
| A8: Kernel und Rollen unantastbar | ja | ja, nicht angefasst | ✓ |

**Plan-Erfüllung: 100 %** — alle Soll-Punkte erfüllt, 2 Punkte übererfüllt (Footprint
kleiner als geschätzt, CS0108-Bonus).

## Findings

### CRITICAL

Keine.

### MAJOR

Keine.

### MINOR

**M1 — Zeilenzahlen-Inkonsistenz `tech-debt.md` Body vs. Realität**

`tasks/codegraph-mcp-server/tech-debt.md` Z. 170-173 schreibt:
- „`CompileErrorMiniFixtureWorkspace` von 71 auf 25 Z. geschrumpft"
- „`GitImpactMiniFixtureWorkspace` von 166 auf 114 Z. geschrumpft"

Realität (im 009-Commit-Stand und im `result.md` dokumentiert):
- `CompileErrorMiniFixtureWorkspace.cs` ist 21 Z. (`wc -l` bestätigt)
- `GitImpactMiniFixtureWorkspace.cs` ist 118 Z. (`wc -l` bestätigt)

Die Body-Zahlen sind die **Plan-Schätzungen** (Plan sagte ~25 / ~125) und der
Coder hat sie in den Body übernommen, ohne sie nach dem Refactor mit den
tatsächlichen Werten zu aktualisieren. Das `result.md` (Z. 22-23) hat die
korrekten Zahlen, der `tech-debt.md`-Body ist 4 Z. daneben (in beide
Richtungen, weil CompileErrorMini kleiner und GitImpactMini größer als
geschätzt).

**Impact:** niedrig — die Logik der Schließung stimmt, die Zahlen sind nur
leicht ungenau. Für zukünftige Re-Reads (besonders wenn jemand
`tech-debt.md` ohne Kontext liest) wäre die Korrektheit der Zahlen wertvoll.

**Vorschlag:** Bei Gelegenheit (nicht in 009 aufhalten, weil approved) in
`tech-debt.md` Z. 170 auf „**21 Z.**" und Z. 173 auf „**118 Z.**" korrigieren.
Kann in einem Folge-Critique-Update oder beim nächsten TD-016a-Berührungs-
Anlass mitgenommen werden. Aktuell kein Blocker.

## Sonstige Beobachtungen

### O1 — Working-Tree hat 6 Commits ahead of `origin/main` (nicht 5)

Stand `git status`: `Your branch is ahead of 'origin/main' by 6 commits`.
Der Anchor sagte 5 Commits, der aktuelle `git log` zeigt 6 Commits ahead.

Aufschlüsselung:
| Commit | Autor | Zeit | Inhalt |
|---|---|---|---|
| `39c4caa` | Planer-Agent | 19:26:04 | 009-Plan (Planer-Commit, vor Coder) |
| `b0c2283` | Coder-Agent | 19:31:11 | Refactor 2 Fixtures |
| `8f0427e` | Coder-Agent | 19:35:51 | Reflection-Tests |
| `0535660` | Coder-Agent | 19:43:49 | tech-debt.md Schließung |
| `5ea191e` | Coder-Agent | 19:44:59 | result.md |
| `894be8b` | **Ralf Hüsing (User)** | 19:50:45 | `docs(rules): AGENTS.md auf Pointer eindampfen` |

**Der 6. Commit (`894be8b`) ist nicht Teil von 009.** Er wurde ~6 Minuten nach
Coder-Ende vom User selbst committed und fasst `AGENTS.md` (Repo-Root) und
`.agents/rules/AiNetLinterRichtlinien.mdc` zusammen — beide Pfade sind nach
A7/A8 für den **Coder-Agenten** tabu, aber der User darf sie selbst pflegen.
Der Commit hat keinen Bezug zu TD-016a oder zu 009.

**Impact für 009-Review:** null — der `894be8b`-Commit berührt weder
Konstruktor-Logik, noch Tests, noch TD-Einträge. Er ist außerhalb des
009-Scopes und gehört in einen separaten Push (z. B. zusammen mit `5ea191e`
nach `approved`, oder als eigener Push-Block je nach User-Präferenz).

**Empfehlung an Orchestrator:** Beim Push nach `approved` den 6. Commit
(`894be8b`) **mitnehmen oder bewusst auslassen** — nicht in 009
reingemixt sehen. Wenn der User den Commit selbst pushen will, ok; sonst
gehört er zu einem separaten Aufräum-Block.

### O2 — CS0108-Compiler-Beobachtung ist methodisch wertvoll

Der Coder hat im A3-Lauf festgestellt, dass `CopyFixture` und `IsGeneratedPath`
in `CompileErrorMiniFixtureWorkspace` (oder analog in `GitImpactMini`) nicht
nur vom Reflection-Test gefangen werden, sondern **vorher** vom Compiler als
`CS0108` (Member blendet vererbten Member aus) — und mit
`<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` wird die Warnung zum
Build-Error.

**Doppelt abgesichert**:
1. Compiler via `CS0108` + `TreatWarningsAsErrors` → fängt **jede** Re-Einführung
   der Helper beim Build ab, Reflection-Test gar nicht nötig.
2. Reflection-Test `Workspace_DoesNotDefineDuplicatedHelper` → fängt den Fall
   ab, dass jemand `new` davorschreibt (`new private static void CopyFixture(...)`),
   was den Compiler wieder grün macht, aber semantisch die Re-Drift wäre.

Diese Beobachtung ist transparent im `result.md` A3-2-Block **und** im
`tech-debt.md` TD-016a-Body (Z. 187-192) dokumentiert — genau richtig.
Historischer Wert: TD-016 selbst ist daran gescheitert, dass der initiale
Refactor (`6c872e4`) nur 2 von 4 Klassen abgedeckt hat, ohne strukturelle
Sicherung. 009 schließt nicht nur die Lücke, sondern verhindert jetzt
strukturell, dass eine einzelne Klasse den Refactor re-driftet. Das ist
die richtige Antwort auf den damaligen Befund.

### O3 — A3-Schichten sauber dokumentiert

Der Coder hat 3 A3-Schichten gefahren und alle wortwörtlich im `result.md`
protokolliert:

- **A3-1 (Vererbungs-Test):** `GitImpactMiniFixtureWorkspace` temporär auf
  `: IDisposable` zurückgestellt → 1 von 2 Assertions rot mit wortwörtlicher
  Failure-Message dokumentiert (Z. 56-64 im result.md).
- **A3-2 (Helper-Entfernungs-Test, geplant):** Test rot, 1 von 6 Assertions.
  Realität: Compiler bricht vorher mit CS0108 ab (siehe O2).
- **Funktionale A3 (automatisch):** 14 bestehende Tests, die die 2 Fixtures
  benutzen, alle grün im gezielten Slice (24/24 in 54 s) und im Volllauf
  (1173/1173 in 6:20 min). Konkret: 12 E2E in
  `McpServerCommandErrorHandlingTests.cs`, 1 E2E in
  `McpServerCommandGetImpactTests.cs`, 2 E2E in `McpServerCommandTests.cs`,
  1 Unit in `GetImpactToolTests.cs`, 9 Tool-Unit-Tests mit
  `CompileErrorMiniFixtureWorkspace`.

Diese 3-Schicht-A3 ist über das hinaus, was der Plan verlangt hat (Plan hatte
A3-1 + A3-2 explizit, A3-funktional nur implizit über "bestehende Tests müssen
grün bleiben"). Der Coder hat das gut dokumentiert.

### O4 — 88/88 Unit-Slice in eigenem Re-Lauf bestätigt

Eigener Re-Lauf zur Verifikation: `dotnet test --no-build --filter "Category=Unit"`
→ **88/88 grün in 27 s**. Matched die Dokumentation im `result.md` (80 vor 009 + 8
Reflection-Tests = 88). Volllauf wurde vom Coder gefahren (Log liegt unter
`units/009/volllauf.log`, 126 KB) und ist 1173/1173 grün — nicht erneut
gefahren, weil (a) der Coder es schon dokumentiert hat, (b) 6:20 min nicht
doppelt verbrannt werden müssen wenn der Unit-Slice und der gezielte
Regress-Slice schon grün sind, (c) `git status` clean + Build sauber +
Unit-Slice 88/88 = hinreichende Evidenz.

Der TD016a-Filter-Test (`FullyQualifiedName~TD016aRefactor`) ist im eigenen
Lauf **8/8 in 46 ms** — die Reflection-Tests sind tatsächlich „schnell wie
erwartet" und können bedenkenlos in der Unit-Suite laufen.

### O5 — 4 Coder-Commits haben separate Zeitstempel (kein Squash, kein Amend)

Die Coder-Commits sind über einen Zeitraum von ~14 Minuten verteilt
(19:31:11 → 19:35:51 → 19:43:49 → 19:44:59), alle einzeln mit eigenem
Zeitstempel und ohne `--amend`. Das ist genau das, was A4 verlangt
(getrennte Commits, gezielter `git add`, kein Squash, kein Amend).

### O6 — Plan-Format eingehalten

Der Plan (`units/009/plan.md`, 574 Z.) war sehr detailliert — Schritt 1-6 mit
wortwörtlichen Code-Beispielen, Schritt 5 sogar mit fertigem Reflection-Test-
Code. Der Coder hat den Plan **1:1** umgesetzt, mit minimalen Abweichungen
(Footprint kleiner als geschätzt, 8 statt 1 Test-Invokationen) und alle
Abweichungen transparent im `result.md` Abschnitt „Plan-Abweichungen"
dokumentiert. Das ist die saubere Coder-Disziplin, die das dynamic-loop
verlangt.

## A7-Konformität

Geprüft via `git diff 39c4caa..HEAD --name-only` (alle 009-Commits von 39c4caa
aus) und Vergleich gegen die geschützten Pfade:

| Pfad | In 009-Diff? | Bewertung |
|---|---|---|
| `konzept.md` | nein | ✓ A7-konform |
| `kernel.md` | nein | ✓ A7/A8-konform |
| `.agents/Agent-Scaffolding/dev-loop/agents/*` (Rollen) | nein | ✓ A8-konform |
| `.agents/rules/AiNetLinter.mdc` (auto-generiert) | nein | ✓ A7-konform (in 009) |
| `.agents/rules/AiNetLinterRichtlinien.mdc` (manuell gepflegt) | nein (in 009) | ✓ A7-konform (in 009). **Hinweis:** Ralf hat in `894be8b` außerhalb von 009 manuell editiert — siehe O1, berührt diese Bewertung nicht. |
| `rules.json` | nein | ✓ A7-konform |
| `Docs/**` | nein | ✓ A7-konform |
| `README.md` | nein | ✓ A7-konform |
| `AiNetLinter.csproj` | nein | ✓ A7-konform |
| `src/AiNetLinter/**` (Produktion) | nein | ✓ A7-konform |
| `Mcp/**` (Modul) | nein | ✓ A7-konform |

**009 hält die A7/A8-Schutz-Zone sauber.** Die einzige Berührung von
`.agents/rules/` in der Working-Tree-Historie kommt vom **User selbst** in
`894be8b` und liegt außerhalb des 009-Scopes.

## Zusammenfassung

**009 ist sauber umgesetzt, A3 echt gefahren, TD-016a vollständig geschlossen.**

- **Refactor-Qualität:** 1:1 zum Plan, beide Fixture-Klassen erben jetzt von
  `FixtureWorkspaceBase`, alle 6 duplizierten Helper weg, `GitImpactMini.Dispose`
  mit korrekter Reihenfolge (`ClearReadOnlyAttributes` **vor** `base.Dispose()`),
  Windows-Read-Only-Schutz intakt.
- **Test-Qualität:** 8 neue Reflection-Test-Invokationen als strukturelle
  A3-Sicherung gegen Re-Drift, funktionale A3-Sicherung über 14 bestehende
  Tests, alle grün. Volllauf 1173/1173 in 6:20 min.
- **TD-Disziplin:** TD-016a in Index + Body + Frontmatter korrekt geschlossen,
  CS0108-Compiler-Bonus-Beobachtung sauber dokumentiert.
- **Commit-Disziplin:** 4 Commits in der geplanten Reihenfolge
  (refactor → test → debt → result), Conventional Commits englisch mit
  `[codegraph-mcp-server]`-Suffix, kein Push, kein Amend, kein `-A`.
- **A7/A8:** konsequent eingehalten, keine Edits an geschützten Pfaden.
- **MINOR:** Eine Zahlen-Inkonsistenz im `tech-debt.md`-Body (25/114 statt
  21/118) — kein Blocker, Vorschlag zur Korrektur in Folge-Critique.

### Empfehlung an Orchestrator

1. **Push nach `approved`** — die 4 Coder-Commits (`b0c2283`, `8f0427e`,
   `0535660`, `5ea191e`) plus Planer-Commit (`39c4caa`) sind sauber und
   pushbar. Den 6. Commit `894be8b` (Ralfs `docs(rules)`) **separat**
   behandeln — entweder mit-pushen wenn Ralf das will, oder als eigenen
   Push-Block. Auf keinen Fall mit 009 vermischen.
2. **Planer für 010 oder Task-Abschluss** — nach Push:
   - Nächste Coder-Einheit nach Plan: **A1 (Auto-Discovery)** oder **A4
     (Kaltstart entkoppeln)** — beide aus `konzept.md` Z. 207-324. A4 ist
     die wichtigere P0, triggert aber TD-009 (Konstruktor-Limit). A1 ist
     einfacher, hat aber `rules.json`-Pfad-Berührung. Aus 009-Plan Sicht:
     **A1 zuerst empfohlen** (kleiner, risikoärmer, gleicher TD-Reifegrad
     wie 009).
   - Alternative: **Konzept-Pflege-Einheit (User-pflichtig)** für die 3
     verbliebenen Konzept-Diskrepanzen aus 008 (Z. 539-552, 550, 564) —
     gehört zwischen 009 und der nächsten Coder-Einheit, damit A1/A4 nicht
     auf veralteten Konzept-Annahmen implementiert werden.
3. **Folge-Critique für M1** — M1 ist kein Blocker, aber wenn der nächste
   Coder einen TD-Pass macht oder ohnehin `tech-debt.md` anfasst, die
   Z. 170 + Z. 173 in einem Mini-Commit auf 21/118 korrigieren. Vorschlag:
   Inline-Mitnahme in 010 oder eigenständiger `chore(debt)`-Mini-Commit,
   beides unkritisch.
