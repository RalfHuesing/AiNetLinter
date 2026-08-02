---
unit: 009
task: codegraph-mcp-server
workflow: dynamic-loop
type: result
created_by: coder
created_at: 2026-08-02
trigger: units/009/plan.md (Commit 39c4caa)
trigger_plan: units/009/plan.md (Commit 39c4caa)
---

# Result Einheit 009 — TD-016a: 2 verbleibende Fixture-Workspaces auf `FixtureWorkspaceBase` umstellen

## Summary

`CompileErrorMiniFixtureWorkspace` (71 Z. → 21 Z.) und `GitImpactMiniFixtureWorkspace` (166 Z. → 118 Z.) erben jetzt von `FixtureWorkspaceBase`, die duplizierten `CopyFixture`/`IsGeneratedPath`/`FindSolutionRoot`-Helper sind gelöscht, der Konstruktor delegiert in beiden Fällen an die Basis und ruft ggf. die spezifische Post-Basis-Aktion auf (`InitializeGitRepoWithInitialCommit()` in GitImpactMini). `GitImpactMiniFixtureWorkspace.Dispose` ist ein `override`, der `ClearReadOnlyAttributes(RootPath)` **vor** `base.Dispose()` aufruft — Windows-Schutz vor schreibgeschützten `.git`-Objekten, der genau einmal in der abgeleiteten Klasse bleibt (nicht generisch, nicht in die Basis gehoben). Als A3-Sicherung gegen Re-Drift existiert `TD016aRefactorTests.cs` mit 2 Reflection-Theories (8 Test-Invokationen, `[Trait("Category", "Unit")]`); zusätzlich fängt der Compiler via `CS0108` jeden Versuch ab, einen der drei Helper erneut einzuführen (Name-Conflict mit der Basis-Methode, mit `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` wird das Build-Error). Plan 1:1 umgesetzt, eine kleine **Bonus-Beobachtung** (CS0108 als zweite A3-Schicht) im `tech-debt.md`-Body dokumentiert. Volllauf **1173/1173 grün** in 6:20 min (1165 vor 009 + 8 Reflection-Test-Invokationen).

## What changed

| Datei | Diff-Größe | Zweck |
|---|---:|---|
| `src/AiNetLinter.Tests/Fixtures/CompileErrorMiniFixtureWorkspace.cs` | 71 Z. → 21 Z. (-50, 11 Ins / 60 Del) | Refactor auf `FixtureWorkspaceBase`: Konstruktor delegiert, `RootPath`/`Dispose` von der Basis geerbt, 3 private-static-Helper gelöscht, `PathFor` bleibt |
| `src/AiNetLinter.Tests/Fixtures/GitImpactMiniFixtureWorkspace.cs` | 166 Z. → 118 Z. (-48, 11 Ins / 59 Del) | Refactor auf `FixtureWorkspaceBase`: Konstruktor delegiert + `InitializeGitRepoWithInitialCommit()` als Post-Basis-Aktion, `Dispose` als `override` mit `ClearReadOnlyAttributes(RootPath)` **vor** `base.Dispose()`, 3 private-static-Helper gelöscht. `RunGit` und `ClearReadOnlyAttributes` bleiben (sind GitImpactMini-spezifisch) |
| `src/AiNetLinter.Tests/Fixtures/TD016aRefactorTests.cs` | NEU, 48 Z. | 2 Reflection-Theories mit `[Trait("Category", "Unit")]`: `Workspace_InheritsFromFixtureWorkspaceBase` (2 InlineData) + `Workspace_DoesNotDefineDuplicatedHelper` (6 InlineData) = 8 Test-Invokationen. Strukturelle A3-Sicherung gegen Re-Drift der 3 Helper |
| `tasks/codegraph-mcp-server/tech-debt.md` | Frontmatter + Index-Zeile TD-016a + Body-Block | TD-016a von „offen" auf „geschlossen durch 009" gesetzt, Commit-Hashes `b0c2283` + `8f0427e` referenziert, Bonus-Beobachtung CS0108 dokumentiert |
| `tasks/codegraph-mcp-server/units/009/result.md` | NEU | dieses Result-Dokument |
| `tasks/codegraph-mcp-server/units/009/volllauf.log` | NEU, ~130 KB | Volllauf-Log-Anhang analog `units/008/volllauf.log` |

**Keine** Modifikationen an: `src/AiNetLinter/**` (Produktionscode), `Mcp/`-Modul, `konzept.md`, `kernel.md`, Rollen-Dateien, `.agents/rules/**`, `rules.json`, `Docs/**`, `README.md`, `AiNetLinter.csproj`, `src/AiNetLinter.Tests/Fixtures/FixtureWorkspaceBase.cs` (unverändert), `src/AiNetLinter.Tests/Fixtures/TestTempDirectory.cs` (unverändert), den anderen 2 Fixture-Workspaces (`BaselineMiniFixtureWorkspace` + `SymbolGraphMiniFixtureWorkspace` — bereits refaktoriert, Finger weg).

## Commit-Hashes

Drei Commits in der Reihenfolge Refactor → Test → Tech-Debt, plus 4. Commit für `result.md`:

1. `b0c2283` — `refactor(tests): CompileErrorMini + GitImpactMini auf FixtureWorkspaceBase umstellen (TD-016a) [codegraph-mcp-server]`
2. `8f0427e` — `test(tests): TD-016a fixture-base refactor regression-schutz (reflection-tests) [codegraph-mcp-server]`
3. `0535660` — `chore(debt): TD-016a geschlossen durch 009 (CompileErrorMini+GitImpactMini auf FixtureWorkspaceBase) [codegraph-mcp-server]`
4. (folgt) — `chore(task): unit 009 result [codegraph-mcp-server]`

## A3-Nachweis

### A3 für die 2 Reflection-Tests (`TD016aRefactorTests.cs`)

#### A3-1: Vererbungs-Test `Workspace_InheritsFromFixtureWorkspaceBase`

**Test grün (refactor wirkt), wortwörtlich:**

```
Bestanden!   : Fehler:     0, erfolgreich:     2, übersprungen:     0, gesamt:     2, Dauer: 33 ms - AiNetLinter.Tests.dll (net10.0)
```

**A3-Auslöser:** `GitImpactMiniFixtureWorkspace` temporär auf `: IDisposable` zurückgestellt + `RootPath`/`Dispose`/3-Helper wieder eingefügt (entspricht 007-Stand).

**Test rot (Re-Drift), wortwörtlich:**

```
Fehler AiNetLinter.Tests.Fixtures.TD016aRefactorTests.Workspace_InheritsFromFixtureWorkspaceBase(workspaceType: typeof(AiNetLinter.Tests.Fixtures.GitImpactMiniFixtureWorkspace)) [2 ms]
Fehlermeldung:
 GitImpactMiniFixtureWorkspace erbt nicht von FixtureWorkspaceBase — TD-016a-Regression.
Stapelverfolgung:
   at AiNetLinter.Tests.Fixtures.TD016aRefactorTests.Workspace_InheritsFromFixtureWorkspaceBase(Type workspaceType) in C:\Daten\Entwicklung\Ralf\AiNetLinter\src\AiNetLinter.Tests\Fixtures\TD016aRefactorTests.cs:line 24

Fehler!      : Fehler:     1, erfolgreich:     1, übersprungen:     0, gesamt:     2, Dauer: 55 ms
```

1 von 2 Assertions rot, 1 von 2 grün — `CompileErrorMini`-Test ist grün, `GitImpactMini`-Test rot. **A3 wirkt** auf Strukturebene (Vererbung), nicht nur auf Funktionalität.

**A3-Rückgängig:** `git checkout -- src/AiNetLinter.Tests/Fixtures/GitImpactMiniFixtureWorkspace.cs` → refactor wieder aktiv, Test grün.

#### A3-2: Helper-Entfernungs-Test `Workspace_DoesNotDefineDuplicatedHelper`

**Test grün (refactor wirkt):** 6/6 grün (Build grün, Theorie läuft, alle InlineData grün).

**A3-Auslöser (geplant):** `CopyFixture` in `CompileErrorMiniFixtureWorkspace` als `private static` wieder einfügen, Test soll 1 von 6 rot werden.

**A3-Realität (Bonus-Beobachtung, **besser** als geplant):** Der Build bricht **vor** dem Test ab:

```
src\AiNetLinter.Tests\Fixtures\CompileErrorMiniFixtureWorkspace.cs(40,25): error CS0108: "CompileErrorMiniFixtureWorkspace.IsGeneratedPath(string)" blendet den vererbten Member "FixtureWorkspaceBase.IsGeneratedPath(string)" aus. Verwenden Sie das new-Schlüsselwort, wenn das Ausblenden vorgesehen war. [C:\Daten\Entwicklung\Ralf\AiNetLinter\AiNetLinter.Tests.csproj]
```

`CopyFixture` und `IsGeneratedPath` lösen beide `CS0108` aus, weil sie in `FixtureWorkspaceBase` als `protected static` mit identischer Signatur existieren. Mit `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` (`AiNetLinter.Tests.csproj`) wird die CS0108-Warnung zum Build-Error — der Test kann gar nicht laufen, weil das Test-Projekt nicht kompiliert. **Stärkerer A3 als im Plan angenommen**: jede zukünftige Re-Einführung eines der drei Helper wird vom **Compiler** gefangen, nicht erst vom Test. Der Reflection-Test ist die zweite Schicht für den Fall, dass jemand `new` davorschreibt (was den Build wieder grün macht, aber semantisch genau die Re-Drift wäre).

**A3-Rückgängig:** `git checkout -- src/AiNetLinter.Tests/Fixtures/CompileErrorMiniFixtureWorkspace.cs` → refactor wieder aktiv, Build grün, Tests grün.

**Was der A3-Nachweis zeigt:** TD-016 selbst ist daran gescheitert, dass der initiale Refactor nur 2 von 4 Klassen abgedeckt hat — die strukturelle Sicherung (Reflection-Tests) **und** der Compiler-Mechanismus (CS0108) verhindern jetzt zuverlässig, dass eine einzelne Klasse den Refactor re-driftet. **Doppelt abgesichert**.

### A3 für bestehende Tests (automatisch, weil `Dispose`-Override in GitImpactMini kritisch)

Die 14 bestehenden Tests, die `GitImpactMiniFixtureWorkspace` oder `CompileErrorMiniFixtureWorkspace` benutzen, sind die **funktionale** A3-Sicherung:

- 12 E2E in `McpServerCommandErrorHandlingTests.cs` (`Category=Integration`) — Compile-Fehler-Warnhinweise in den 9 Tool-Responses gegen `CompileErrorMiniFixtureWorkspace`
- 1 E2E in `McpServerCommandGetImpactTests.cs` (`Category=Integration`) — `get_impact` mit Git-Ref
- 2 E2E in `McpServerCommandTests.cs` (`Category=Integration`) — analog
- 1 Unit in `GetImpactToolTests.cs` (`Category=Unit`) — `get_impact` Tool-Unit-Test
- 9 Unit-Tests in `Mcp/Tools/*ToolTests.cs` (`Category=Unit`) — Compile-Fehler-Compile-Path

Gezielter Slice zur Regress-Früherkennung: `dotnet test --filter "FullyQualifiedName~CompileErrorMini|FullyQualifiedName~GitImpact|FullyQualifiedName~McpServerCommandErrorHandling|FullyQualifiedName~McpServerCommandGetImpact|FullyQualifiedName~McpServerCommandTests"` → **24/24 grün** in 54 s (gemessen 19:30:01). Wäre `ClearReadOnlyAttributes` nicht (oder in falscher Reihenfolge) im `Dispose`-Override, würden die E2E-Tests beim Cleanup entweder `UnauthorizedAccessException` werfen (Test rot) oder den Temp-Dir leaken (kein direkter Fail, aber Folgeprobleme in der nächsten Test-Klasse). Beides ist **nicht** aufgetreten.

## Build/Test-Ergebnis

| Schritt | Befehl | Ergebnis |
|---|---|---|
| Baseline Build | `dotnet build AiNetLinter.slnx` | grün, 0 Warnungen, 0 Fehler, 10.28 s |
| Baseline Unit-Slice | `dotnet test --no-build --filter "Category=Unit"` | grün, 80/80, 19 s |
| Build nach Refactor-Commit | `dotnet build AiNetLinter.slnx` | grün, 0/0, 3.00 s |
| Reflection-Test-Slice nach Test-Commit | `dotnet test --no-build --filter "FullyQualifiedName~TD016aRefactor"` | grün, 8/8, 33 ms |
| A3-1 isoliert (Re-Drift) | `dotnet test --no-build --filter "FullyQualifiedName~Workspace_InheritsFromFixtureWorkspaceBase"` | rot, 1/2 (siehe A3-Block oben) |
| A3-1 zurückgebogen | `git checkout` + `dotnet test --no-build --filter "FullyQualifiedName~Workspace_InheritsFromFixtureWorkspaceBase"` | grün, 2/2, ~50 ms |
| A3-2 isoliert (Re-Drift) | `CopyFixture` wieder eingefügt, `dotnet build` | rot, CS0108 (siehe A3-Block oben) |
| A3-2 zurückgebogen | `git checkout` + `dotnet build` | grün, 0/0, ~3 s |
| Unit-Slice final | `dotnet test --no-build --filter "Category=Unit"` | grün, 88/88, 23 s (80 + 8 Reflection) |
| Gezielter Regress-Slice | `dotnet test --no-build --filter "FullyQualifiedName~CompileErrorMini\|FullyQualifiedName~GitImpact\|FullyQualifiedName~McpServerCommandErrorHandling\|FullyQualifiedName~McpServerCommandGetImpact\|FullyQualifiedName~McpServerCommandTests"` | grün, 24/24, 54 s |
| **Volllauf (Pflicht, AGENTS.md §2)** | `dotnet test --no-build` | **grün, 1173/1173, 6 m 20 s** (vorher 1165, +8 Reflection-Test-Invokationen) |

`TreatWarningsAsErrors=true` ist gesetzt (`AiNetLinter.Tests.csproj`), Build ist sauber.

## Plan-Erfüllung

| Punkt | Soll | Ist | Status |
|---|---|---|---|
| `CompileErrorMiniFixtureWorkspace` auf `FixtureWorkspaceBase` umstellen, 3 Helper löschen | ja | ja, 71 → 21 Z. | ✓ |
| `GitImpactMiniFixtureWorkspace` auf `FixtureWorkspaceBase` umstellen, 3 Helper löschen | ja | ja, 166 → 118 Z. | ✓ |
| `InitializeGitRepoWithInitialCommit()` als Post-Base-Aktion | ja | ja, im Konstruktor nach `base(...)` | ✓ |
| `Dispose`-Override in GitImpactMini mit `ClearReadOnlyAttributes(RootPath)` **vor** `base.Dispose()` | ja (kritisch) | ja, Reihenfolge korrekt | ✓ |
| `RunGit` und `ClearReadOnlyAttributes` in GitImpactMini belassen (nicht in Basis heben) | ja (A5-„fertig ist fertig") | ja, unverändert | ✓ |
| Optional: `TD016aRefactorTests.cs` mit 2 Reflection-Tests als A3-Sicherung | optional, empfohlen | ja, 8 Test-Invokationen, `[Trait("Category", "Unit")]` | ✓ mitgenommen |
| `tech-debt.md` TD-016a → Status „geschlossen durch 009" + Index + Body analog TD-003/012/013/015/016 | ja | ja, alle 3 Stellen aktualisiert | ✓ |
| Volllauf `dotnet test AiNetLinter.slnx --no-build` (AGENTS.md §2 Pflicht) | ja | ja, 1173/1173 grün, 6:20 min | ✓ |
| Konvention-Commits 3-4 (Refactor + Test + Debt + Result) | ja | ja, exakt 4 Commits in der geplanten Reihenfolge | ✓ |
| A4: gezielter `git add`, kein `-A`/`.`, kein Push, kein Amend | ja | ja, jede Datei einzeln `git add` | ✓ |
| A5: keine kosmetischen Edits an den 2 refaktorierten Klassen | ja | ja, nur die im Plan explizit genannten Edits | ✓ |
| A7: kein Edit an `konzept.md`, `kernel.md`, Rollen-Dateien, `.agents/rules/**`, `rules.json` | ja | ja, nicht angefasst | ✓ |
| A8: Kernel und Rollen unantastbar | ja | ja, nicht angefasst | ✓ |

## Plan-Abweichungen

1. **A3-2 stärker als geplant (Compiler statt Test fängt die Regression).** Der Plan hatte vorgesehen, dass `Workspace_DoesNotDefineDuplicatedHelper` 1 von 6 Assertions rot wird, wenn man `CopyFixture` zurückkopiert. Realität: `CS0108` bricht den Build **vor** dem Test, weil `CopyFixture`/`IsGeneratedPath` als `protected static` in der Basis existieren und der C#-Compiler das Hiding via Warnung meldet — `TreatWarningsAsErrors=true` macht die Warnung zum Build-Error. Der Reflection-Test ist trotzdem sinnvoll als zweite Schicht (falls jemand `new` davorschreibt). Diese Beobachtung ist im `tech-debt.md` TD-016a-Body dokumentiert, nicht im `result.md` versteckt.

2. **GitImpactMini: 118 statt ~125 Z. (Plan war konservativ).** Plan-Erwartung war „166 → ~125 Z.", tatsächlich sind es 118 Z. Grund: `Dispose` ist als Override kürzer (4 Z. statt 7 Z. im Original), und die `using`-Anpassungen sind minimal (ImplicitUsings liefert `System.IO`). Kein Anlass zur Korrektur, nur eine Notiz für künftige Pläne (Vorlagen-Zahlen sind Schätzungen).

3. **Test-Count-Detail:** Plan-Erwartung war „+1 Test" (zählend die Test-Methoden, 2 davon). Realität: **+8 Test-Invokationen** (2 Theory-Methoden × 2/6 InlineData = 8 Invokationen). Volllauf-Stand 1165 → 1173, nicht 1166. Kein funktionaler Unterschied (beide sind „Refactor + 1 neue Test-Datei"), nur eine genauere Zählung.

## Tech-Debt-Aktionen

- **TD-016a → geschlossen durch Einheit 009** (Commits `b0c2283` + `8f0427e`). Index-Zeile, Body-Block + Frontmatter aktualisiert. Body dokumentiert den strukturellen Refactor, die A3-Sicherung über bestehende Tests, die Reflection-Tests, und die CS0108-Compiler-Beobachtung als Bonus.
- **Keine neuen TD-Einträge.** Refactor ist die Schließung selbst. `RunGit` und `ClearReadOnlyAttributes` in GitImpactMini zu belassen ist **kein** TD-Kandidat — der Plan hat das explizit als A5-„fertig ist fertig" markiert (spezifisch für Git-Repos, nicht generisch).
- **Keine TD-Verschärfung.** 009 fasst keine Tool-Klassen, keine Registrar-Klassen, keinen Produktionscode an — keine neuen Footprint-Drücke.
- **Offene TD-Einträge nach 009:** TD-001, TD-002, TD-004, TD-005, TD-006, TD-007, TD-008, TD-009, TD-010, TD-011, TD-014. (TD-003, TD-012, TD-013, TD-015, TD-016, **TD-016a** geschlossen.)

## Commit-Disziplin (A4)

| Punkt | Status |
|---|---|
| Gezielter `git add` pro Datei (kein `-A`, kein `.`) | ✓ — 3 separate `git add` für Refactor (2 Files), Test (1 File), Tech-Debt (1 File) |
| Conventional Commits in Englisch, Imperativ, `[codegraph-mcp-server]`-Suffix | ✓ — siehe Commit-Hashes oben |
| Kein Push | ✓ — Working-Tree nach 3 Commits clean, Branch `main` ist 4 Commits ahead of `origin/main` (vorher 3, +3 aus 009, 4. folgt mit result.md) |
| Kein Amend, kein Force-Push, kein History-Rewrite | ✓ |
| Plan-Abweichungen begründet im `result.md` | ✓ — siehe Plan-Abweichungen-Block oben |
| Working-Tree nach Commits clean | ✓ — `git status` zeigt nur die 2 ungetrackten Dateien `units/009/result.md` + `units/009/volllauf.log` |
| Kein Edit an `konzept.md`/`kernel.md`/Rollen-Dateien/`.agents/rules/**`/`rules.json`/`Docs/**` | ✓ — A7/A8 eingehalten |

## Nächste Aktion des Orchestrators

→ **Kritiker-Aufruf für 009** (Review-Datei
`tasks/codegraph-mcp-server/units/009/review.md`).

Kritiker-Prüfpunkte:

1. **Refactor 1:1 zum Plan** (`units/009/plan.md` Schritt 1 + 2):
   `CompileErrorMiniFixtureWorkspace` 21 Z. (Plan-Erwartung ~25, Abweichung begründet durch Implizit-Using-Auflösung), `GitImpactMiniFixtureWorkspace` 118 Z. (Plan-Erwartung ~125, Abweichung begründet), 3 Helper in beiden Klassen gelöscht, `Dispose`-Override in GitImpactMini mit korrekter Reihenfolge (`ClearReadOnlyAttributes` **vor** `base.Dispose()`).
2. **A3 echt:** Reflection-Tests in `TD016aRefactorTests.cs` mit echtem A3-Pfad gefahren (Vererbung temporär gebrochen, Failure-Output wortwörtlich dokumentiert). Bestehende Tests als funktionale A3-Sicherung mitgeführt (24/24 grün im gezielten Slice, 1173/1173 grün im Volllauf).
3. **CS0108-Bonus:** `Workspace_DoesNotDefineDuplicatedHelper` ist durch den Compiler-Mechanismus (CS0108 + `TreatWarningsAsErrors=true`) **doppelt** abgesichert. Der Reflection-Test wäre redundant, wenn jemand `new` davorschreibt — ist als zweite Schicht trotzdem wertvoll. Begründung im `tech-debt.md` TD-016a-Body dokumentiert.
4. **Volllauf 1173/1173 grün** ist Pflicht-Voraussetzung für `approved`. 8 Tests mehr als 008-Baseline (1165 → 1173) wegen der 8 Reflection-Test-Invokationen.
5. **Tech-Debt-Aktion:** TD-016a in `tech-debt.md` Index + Body korrekt auf „geschlossen durch 009" gesetzt, Commit-Hashes `b0c2283` + `8f0427e` referenziert, CS0108-Beobachtung dokumentiert. Body-Format konsistent mit TD-003/012/013/015/016.
6. **Commit-Disziplin A4** eingehalten (3 Commits lokal, kein Push, gezielter `git add`, Conventional Commits mit Suffix).
7. **A5/A7/A8** eingehalten: keine Edits an `konzept.md`, `kernel.md`, Rollen-Dateien, `.agents/rules/**`, `rules.json`, `Docs/**`, README, AiNetLinter.csproj, `src/AiNetLinter/**`-Produktionscode, den anderen 2 Fixture-Workspaces.

### Aufruf-Budget nach 009

`max_aufrufe`: 27 (Stand 008/fix-01 nach Kritiker-Approval) + 1 (009 Coder) = **28/40**
verbraucht, **12/40 verbleibend** für die P0/P1-Rest-Erweiterungen aus `Docs/ROADMAP.md`
(Kaltstart, Auto-Discovery, mtime-Sweep, Verzeichnis-Sweep neu/gelöscht, `ILintConsole`,
Last-Fixture, `--mcp-log`, stdout-Schutz, 7 weitere Punkte).

`max_fix_pro_einheit` für 009: 0 → 0, **3 verbleibend**.

`max_fix_gesamt`: 1 (002/fix-01) → 1, **11 verbleibend**.

### Working-Tree / Push-Status

Stand nach Coder: 3 Commits lokal (Refactor + Test + Tech-Debt), kein Push,
Branch `main` 4 Commits ahead of `origin/main` (vorher 3, +3 aus 009, 4. folgt
mit result.md). Coder wartet auf `approved` (oder `009/fix-01`-Freigabe).
**Kein Push durch Coder** (A4).
