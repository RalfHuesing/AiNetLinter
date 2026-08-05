---
status: done
type: step-result
task: verbesserungen-mcp
step: 001
epic: EPIC-01
step_type: single
coded_by: coder
coded_by_model: claude-sonnet-5
coded_by_model_knowledge_cutoff: 2026-01
coded_at: 2026-08-05T06:50:45Z
code_commit_hash: fbc399fd052092a3150b219a533623987c979db0
status_after: done
blocker_category: n/a
---

# Result Step 001: Blazor-Partial-Fixture anlegen und Symbolgraph-Diskrepanz reproduzieren

## Zusammenfassung

Neue synthetische Mini-Solution `BlazorPartialMini` (Sdk.Razor) angelegt
sowie eine zugehörige `FixtureWorkspaceBase`-Ableitung und drei neue
Tests in `SourceFileCatalogBlazorPartialTests`. Alle drei Tests sind
grün und belegen den im Plan beschriebenen IST-Zustand: `CS0115` auf
beiden `override`-Lifecycle-Methoden, Aggregat-Hinweis in
`get_index_scope`, fehlender `: ComponentBase`-Basistyp in
`get_file_skeleton`. Genau die 6 im Plan spezifizierten Dateien
angelegt, keine Scope-Erweiterung.

## Geänderte Dateien

- `tests/Fixtures/BlazorPartialMini/BlazorPartialMini.slnx` (neu) — Solution-Datei nach CompileErrorMini-Muster.
- `tests/Fixtures/BlazorPartialMini/src/BlazorPartialMini/BlazorPartialMini.csproj` (neu) — Sdk.Razor-Projekt, net10.0, `FrameworkReference Microsoft.AspNetCore.App`.
- `tests/Fixtures/BlazorPartialMini/src/BlazorPartialMini/SiteView.razor` (neu) — Komponente ohne `@inherits`.
- `tests/Fixtures/BlazorPartialMini/src/BlazorPartialMini/SiteView.razor.cs` (neu) — Codebehind-Partial ohne expliziten Basistyp, `[Parameter]` + zwei `override`-Lifecycle-Methoden.
- `src/AiNetLinter.Tests/Fixtures/BlazorPartialMiniFixtureWorkspace.cs` (neu) — `FixtureWorkspaceBase`-Ableitung, `SiteViewCsPath`-Property.
- `src/AiNetLinter.Tests/Baseline/SourceFileCatalogBlazorPartialTests.cs` (neu) — die drei im Plan spezifizierten Tests.

## Commit

- **Code-Commit-Hash:** `fbc399fd052092a3150b219a533623987c979db0`
- **Message:**
  ```
  test(fixtures): Blazor-Partial-Fixture fuer Symbolgraph-Diskrepanz anlegen [verbesserungen-mcp]

  Neue synthetische Mini-Solution BlazorPartialMini (Sdk.Razor) mit
  .razor-Komponente ohne @inherits und .razor.cs-Codebehind-Partial mit
  override-Lifecycle-Methoden ohne expliziten Basistyp. Drei neue Tests
  belegen den aktuellen IST-Zustand: die vom Razor-Source-Generator
  erzeugte zweite Partial-Deklaration (mit ": ComponentBase") fliesst
  nicht in die von SourceFileCatalog.LoadAsync geladene Compilation ein,
  wodurch CS0115 auf den override-Methoden entsteht.

  Refs: tasks/verbesserungen-mcp/step-001
  ```
- **Branch:** main
- **Push:** nein (lokal)
- **Doku-Commit:** separater, zweiter Commit (Hash steht nicht hier drin — Selbstbezug, siehe `git log`).

## Build-/Test-Output

```
dotnet build AiNetLinter.slnx → grün (0 Warnung(en), 0 Fehler)
dotnet test AiNetLinter.slnx  → grün (1257 Tests, 0 Fehler, inkl. der 3 neuen)
```

## Abweichungen vom Plan

- **Regex in Test 2 angepasst:** Der Plan orientiert sich am Vorbild
  `GetIndexScopeToolTests.ExecuteAsync_CompileErrorFixture_OutputStartsWithAggregateWarning`,
  dessen Regex `\b\d+\s+Dateien?\s+haben\s+Compile-Fehler` lautet. Diese
  Fixture bricht nur eine einzige Datei (`SiteView.razor.cs`), daher
  liefert `FormatAggregateWarning` den Singular-Text „1 Datei haben
  Compile-Fehler". Das `?` in `Dateien?` bezieht sich nur auf das letzte
  „n" (macht aus „Dateien" optional „Dateie"/„Dateien"), matcht aber
  nicht „Datei" (5 Buchstaben) — der Test schlug beim ersten Lauf exakt
  daran fehl, nicht am CS0115-Verhalten selbst (das war bereits beim
  ersten Versuch korrekt reproduziert). Regex in der neuen Testklasse
  auf `\b\d+\s+Datei(en)?\s+haben\s+Compile-Fehler` korrigiert (matcht
  sowohl Singular als auch Plural). Kein Blocker, kein Fix-Versuch im
  Sinne von Schritt 4a verbraucht — reiner Assertion-Tippfehler im
  übernommenen Muster.

## Beobachtungen

- Der oben beschriebene Regex-Fehler (`Dateien?` statt `Datei(en)?`)
  steckt unverändert auch im bestehenden Vorbild-Test
  `GetIndexScopeToolTests.ExecuteAsync_CompileErrorFixture_OutputStartsWithAggregateWarning`
  sowie in mindestens den weiteren `*ExecuteAsync_CompileErrorFixture_OutputStartsWithAggregateWarning`-Tests
  (`FindReferencesToolTests`, `GetTypeHierarchyToolTests` u. a., laut
  Testlauf-Log gefunden). Dort fällt es aktuell nicht auf, weil die
  jeweilige Fixture immer mehrere kaputte Dateien hat (Plural-Fall). Der
  Regex würde aber bei einem Singular-Fall (1 Datei mit Compile-Fehler)
  denselben False-Negative erzeugen wie hier beim ersten Versuch. Kein
  Fix in diesem Step (Scope betrifft nur die neue Fixture/Testklasse) —
  möglicher Kandidat für einen Tech-Debt-Eintrag.
- `.agents/rules/AiNetLinter.mdc` und `tasks/verbesserungen-mcp/task-state.md`
  waren bereits vor Beginn dieses Coder-Laufs im Arbeitsverzeichnis
  verändert (Versionsbump 1.0.78→1.0.79 bzw. Fortschritts-Tabelle). Nicht
  von mir angefasst, nicht mitcommittet — beide liegen außerhalb des
  Step-Scopes.

## Bekannte Unschärfen

- Keine Verifikation gegen eine echte Blazor-Anwendung (San.smart.Planner.Platform)
  vorgenommen — wie im Konzept als „Verworfene Alternative" bewusst
  ausgeschlossen. Die Fixture ist synthetisch; ob das reale Projekt exakt
  dasselbe Muster (kein `@inherits`, `.razor.cs` ohne Basistyp) zeigt,
  bleibt eine Annahme aus dem Bug-Report.
- `dotnet build AiNetLinter.slnx` betrifft nur die Haupt-Solution; das neue
  `Microsoft.NET.Sdk.Razor`-Projekt der Fixture wird nie direkt mit
  `dotnet build`, sondern ausschließlich implizit über `MSBuildWorkspace`
  im Testlauf aufgelöst (wie geplant) — kein `dotnet restore`/`build`
  separat gegen die neue Fixture-Solution gefahren.
