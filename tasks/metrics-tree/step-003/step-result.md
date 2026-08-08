---
status: done
type: step-result
task: metrics-tree
step: 003
epic: EPIC-02
step_type: single
coded_by: coder
coded_by_model: claude-sonnet-5
coded_by_model_knowledge_cutoff: 2026-01
coded_at: 2026-08-08
code_commit_hash: 58a6aa5
status_after: done
blocker_category: n/a
---

# Result Step 003: Roslyn-Modi violation_density/complexity + Doku-Updates + Roadmap-Abschluss

## Zusammenfassung

Alle 11 Dateien aus „Konkrete Änderungen" umgesetzt: `metrics_tree` deckt jetzt alle 4
Konzept-Modi ab (`code_size`/`comment_density` aus EPIC-01, neu `violation_density` über
`LinterEngine` und `complexity` über `ComplexityCalculator`). Registrierung nach
`AnalysisToolRegistrations` verschoben. Zusätzlich zwei nicht im Plan vorgesehene, aber vom
verpflichtenden Abschluss-Volllauf erzwungene Korrekturen: `FileMetric`/`BuilderNode` mussten
top-level statt `internal nested` werden (`BanPublicNestedTypes` verbietet auch `internal
nested`), und alle fünf `MetricsTree*.cs`-Dateien wurden in ein eigenes Unterverzeichnis
`Mcp/Tools/MetricsTree/` verschoben (`MaxDirectoryChildren`, war bereits vor diesem Step bei 31/30
Einträgen). Alle 4 Modi decken jetzt auch die Definition-of-Done des gesamten Tasks ab.

## Geänderte Dateien

- `src/AiNetLinter/Mcp/Tools/MetricsTree/MetricsTreeMode.cs` — Enum um `ViolationDensity`/
  `Complexity` erweitert, Parser entsprechend, XML-Doc aktualisiert.
- `src/AiNetLinter/Mcp/Tools/MetricsTree/MetricsTreeScanner.cs` — `FileMetric`/`BuilderNode` als
  Namespace-Ebene-Records (siehe „Abweichungen") um die Violation-/Complexity-Felder erweitert;
  `AggregateLeaf`/`AggregateWithChildren` mit `Math.Max` für `MaxCyclomatic`/`MaxCognitive`,
  restliche Felder weiter `Sum`; `ComputeSortValue`/`FormatDisplayLine` um die zwei neuen Modi
  erweitert (auf `BuilderNode`-Parameter statt Einzelwerte umgestellt, um die Signaturen nicht
  weiter aufzublähen); Bau-Helfer (`BuildNode`, `ToMetricsTreeNode`, `NormalizeRoot`,
  `ComputeRootName`) von `private` auf `internal` angehoben.
- `src/AiNetLinter/Mcp/Tools/MetricsTree/MetricsTreeRoslynScanner.cs` (neu) —
  `MetricsTreeRoslynScanParameters`-Record + `MetricsTreeRoslynScanner.BuildTreeAsync`:
  `violation_density` über `LinterEngine.RunAsync(noCache: true, ...)` + Gruppierung nach
  `FilePath`; `complexity` über `SolutionFileWalker.CollectFiles` + Datei→Document-Map +
  `ComplexityCalculator.GetCyclomaticComplexity`/`GetCognitiveComplexity` pro
  `MethodDeclarationSyntax`. Beide bauen dieselben `FileMetric`/`BuilderNode`-Typen und rufen
  `MetricsTreeScanner`s (jetzt internen) Aggregations-Kern.
- `src/AiNetLinter/Mcp/Tools/MetricsTree/MetricsTreeTool.cs` — Dispatch auf
  `MetricsTreeScanner.BuildTree` (Datei-Modi) vs. `MetricsTreeRoslynScanner.BuildTreeAsync`
  (Roslyn-Modi, inkl. `state.GetConfigSnapshot()`); `MetricsTreeDescription` auf alle 4 Modi
  aktualisiert.
- `src/AiNetLinter/Mcp/FileStructureToolRegistrations.cs` — `AddMetricsTree` + Konstante entfernt,
  Klassen-Doku angepasst (3 statt 4 Tools).
- `src/AiNetLinter/Mcp/AnalysisToolRegistrations.cs` — `AddMetricsTree` übernommen, Klassen-Doku
  erweitert; zusätzliches `using AiNetLinter.Mcp.Tools.MetricsTree;` (siehe „Abweichungen").
- `src/AiNetLinter.Tests/Commands/McpServerCommandTests.cs` — Test auf „FourteenTools“ umbenannt,
  `metrics_tree`-Assertion ergänzt, eine Leerzeile entfernt (`MaxLineCount`, siehe „Abweichungen“).
- `src/AiNetLinter.Tests/Mcp/McpLiveRepositoryTests.cs` — 2 Dogfooding-Tests
  (`violation_density`/`complexity` gegen das eigene Repo).
- `src/AiNetLinter.Tests/Mcp/Tools/MetricsTreeRoslynScannerTests.cs` (neu) — 8 Tests: sortierte
  Bäume für beide neue Modi, leerer/Single-File-Root, `depth=5`, methodenlose Datei
  (0-Methoden-Edge-Case). Zwei Tests nutzen eine isolierte
  `SymbolGraphMiniFixtureWorkspace`-Kopie + zusätzlich geschriebene `.cs`-Dateien statt der
  geteilten `SymbolGraphCatalogFixture`, um die von `GetIndexScopeToolTests`/`GetHotspotsToolTests`
  exakt geprüften Datei-/Methodenzahlen des geteilten Fixtures nicht zu verändern.
- `src/AiNetLinter.Tests/Mcp/Tools/MetricsTreeRendererTests.cs`,
  `MetricsTreeToolTests.cs` — `using AiNetLinter.Mcp.Tools.MetricsTree;` ergänzt (Namespace-Wechsel).
- `rules.json` — `PathOverrides.AnalysisToolRegistrations.cs` 2870→2910; neuer Eintrag
  `Mcp/Tools/MetricsTree/MetricsTreeTool.cs` → 2910.
- `Docs/agent-api.md`, `README.md`, `Docs/ROADMAP.md`, `tasks/features/05-roadmap.md` —
  Doku-Updates (14 statt 13 Tools, `metrics_tree`-Zeile/-Abschnitt, S2.5 abgehakt mit Anmerkung zu
  4 statt 5 Modi).

## Commit

- **Code-Commit-Hash:** `58a6aa5`
- **Message:**
  ```
  feat(mcp): Roslyn-Modi violation_density/complexity fuer metrics_tree [metrics-tree]

  Ergaenzt die zwei Roslyn-basierten Modi (violation_density ueber LinterEngine,
  complexity ueber ComplexityCalculator) um die zwei Datei-Walk-Modi aus EPIC-01 -
  metrics_tree deckt damit alle vier im Konzept vorgesehenen Modi ab.
  metrics_tree-Registrierung nach AnalysisToolRegistrations verschoben (gleicher
  Grund wie bei get_violations: LinterEngine-Pull-in). MetricsTree*.cs in eigenes
  Unterverzeichnis verschoben (MaxDirectoryChildren), PathOverrides fuer
  AIContextFootprint entsprechend angepasst.

  Refs: tasks/metrics-tree/step-003
  ```
- **Branch:** main
- **Push:** nein (lokal)
- **Doku-Commit:** separater, zweiter Commit (Hash steht nicht hier drin).

## Build-/Test-Output

```
dotnet build AiNetLinter.slnx                                                → grün (0 Warnungen, 0 Fehler)
dotnet test --filter "FullyQualifiedName~MetricsTree|~McpServerCommand"      → grün (102 Tests, 0 Fehler)
dotnet test --filter Category!=Stress  (Abschluss-Volllauf, GESAMTER Task)   → grün (1349 Tests, 0 Fehler)
get_violations (voller Scope, resident geladene Solution)                    → 0 Warnungen/Fehler
                                                                                 im Produktionscode
                                                                                 (3 Fehler in
                                                                                 tests/Fixtures/
                                                                                 DiRegistrationMini,
                                                                                 vorbestehende
                                                                                 absichtliche
                                                                                 Fixture-Verstöße,
                                                                                 unverändert)
```

`AIContextFootprint` Vorher/Nachher (für TD-001-Fortschreibung durch den Kritiker):
- `AnalysisToolRegistrations.cs`: 2870 (PathOverride, unverändert seit früherem Step) →
  tatsächlicher Footprint jetzt 2905 durch `metrics_tree`-Zuzug; PathOverride auf 2910 angehoben.
- `MetricsTreeTool.cs` (jetzt `Mcp/Tools/MetricsTree/MetricsTreeTool.cs`): vorher 2536 (kein
  Override, Default 2500), jetzt 2897 durch den Roslyn-Dispatch; neuer PathOverride 2910.
- `FileStructureToolRegistrations.cs`: 2895 (PathOverride 2890) → durch Entfernen von
  `metrics_tree` jetzt unter dem Default-Limit, kein Override mehr nötig (nicht mehr in den
  Violations gelistet).

## Abweichungen vom Plan

- **`FileMetric`/`BuilderNode` als Namespace-Ebene-Records statt `internal nested` in
  `MetricsTreeScanner`:** Der Plan sah `internal nested` Typen vor („Sichtbarkeit … von `private`
  auf `internal` anheben"). `get_violations` meldete dafür zwei **Fehler**-Verstöße
  (`BanPublicNestedTypes` — die Regel verbietet auch `internal nested` Typen,
  `BanPublicNestedTypesAllowPrivate` erlaubt nur `private nested`). Beide Records liegen jetzt auf
  Datei-Namespace-Ebene (analog `MetricsTreeQuery` aus Step 002) — reine
  Sichtbarkeits-Platzierung, kein Verhaltensunterschied. `MetricsTreeScanner.BuildNode` trägt
  zusätzlich eine `ainetlinter-disable MaxMethodParameterCount`-Suppression (5 Parameter; die
  relaxierte Nicht-Public-Grenze — 6 statt 4 — greift bei `internal` Methoden nicht, nur bei
  `private`/`protected`; `BuildNode` muss aber `internal` sein, damit
  `MetricsTreeRoslynScanner` sie aufrufen kann).
- **MetricsTree-Dateien in eigenes Unterverzeichnis `Mcp/Tools/MetricsTree/` verschoben, nicht im
  Plan:** Der verpflichtende Abschluss-Volllauf (`dotnet test --filter Category!=Stress`) deckte
  auf, dass `src/AiNetLinter/Mcp/Tools/` bereits **vor** diesem Step 31 Einträge hatte
  (`MaxDirectoryChildren`-Limit: 30) — durch die neue `MetricsTreeRoslynScanner.cs` stieg das auf
  32. Das ließ 4 `CliIntegrationTests` (u. a. `RunLinterCli_OnWholeSolution_ReturnsSuccess`, die
  einen buchstäblich violation-freien Lint-Lauf verlangen) fehlschlagen — laut Tech-Stack-Notiz war
  dies der erste Volllauf seit step-001, die Pre-Existenz wurde also erst jetzt sichtbar. Fix:
  alle fünf `MetricsTree*.cs` in `Mcp/Tools/MetricsTree/` verschoben (Tools/ jetzt 27 statt 32
  Einträge), Namespace auf `AiNetLinter.Mcp.Tools.MetricsTree` umgestellt
  (`EnforceNamespaceDirectoryMapping` erzwingt das bei einer Verzeichnis-Verschiebung), 4
  Referenzstellen (`AnalysisToolRegistrations.cs` + 3 Testdateien) um ein zusätzliches `using`
  ergänzt. `WalkedFile.cs`/`SolutionFileWalker.cs` blieben unverändert in `Mcp/Tools/` (nicht
  betroffen).
- **Zwei `AIContextFootprint`-`PathOverride`-Anpassungen in `rules.json`, nicht im Plan:** Aus
  demselben Volllauf — `AnalysisToolRegistrations.cs` (2905 > vorherigem Override 2870) und die neue
  `MetricsTreeTool.cs`-Position (2897 > Default 2500) blockierten denselben violation-freien
  Lint-Lauf. Fix folgt der bereits 10-fach etablierten Projekt-Konvention (`PathOverride` knapp
  über dem tatsächlichen Footprint) — keine neue Architekturentscheidung, sondern Anwendung des
  bestehenden Mechanismus. TD-001 (Facade-Extraktion) bleibt davon unberührt und offen für den
  Kritiker.
- **`McpServerCommandTests.cs` um eine Leerzeile gekürzt:** nach der mechanischen
  Tool-Count-Korrektur hatte die Datei 501 Zeilen (`MaxLineCount`: 500, **Fehler**-Severity) —
  eine Leerzeile zwischen Arrange und Assert entfernt, keine Verhaltensänderung.
- Ansonsten alle 11 geplanten Dateien 1:1 wie im Plan umgesetzt (inkl. Code-Skizze für
  `FileMetric`/`ComputeSortValue`/`FormatDisplayLine`, nur mit `BuilderNode`- statt
  Einzelwert-Parametern für weniger Signatur-Aufblähung).
- Doku-Datei 10 (`tasks/features/05-roadmap.md`): zwei Akzeptanzkriterien-Zeilen bewusst **nicht**
  abgehakt (`[ ]` belassen) mit Anmerkung, weil nicht 1:1 erfüllt — „5+ Unit-Tests (1 pro Mode)“
  (21 Tests, aber nur 4 statt 5 Modi) und „Doku mit Beispielen pro Mode“ (nur eine Tabellenzeile,
  keine dedizierten Beispiel-Blöcke) —, siehe Objektivitätsregel.

## Beobachtungen

- **`ServerInstructions.cs`** (`initialize`-Handshake-Text) listet `metrics_tree` **nicht** in
  seiner Tools-Aufzählung — vorbestehende Lücke seit EPIC-01 (nicht durch diesen Step verursacht,
  nicht im Plan als zu ändernde Datei genannt). `OverviewResourceRegistration.cs`
  (`ainetlinter://overview`-Resource) ist dagegen bereits korrekt (14 Tools, Step 001/002). Nicht
  behoben, da außerhalb des explizit benannten Datei-Scopes dieses Steps — potenzieller
  Tech-Debt-Kandidat für den Kritiker.
- **`FormatBytes` in `MetricsTreeScanner.cs`** (aus EPIC-01, unverändert) formatiert mit `:F1` ohne
  `CultureInfo.InvariantCulture` — auf einem System mit Komma-Dezimaltrenner (wie hier beim
  Testlauf beobachtet) würde das z. B. „1,5 MB“ statt „1.5 MB“ liefern. Der neue `complexity`-Modus
  wurde bewusst mit `InvariantCulture` implementiert (siehe Testfehler während der Iteration),
  `FormatBytes` selbst wurde nicht angefasst (nicht Teil dieses Steps' Scope) — latenter,
  vorbestehender Lokalisierungs-Bug, potenzieller Tech-Debt-Kandidat.
- Der Abschluss-Volllauf lief nur, weil `git stash` versehentlich kurzzeitig alle getrackten
  Änderungen entfernte (zur Prüfung des HEAD-Zustands) — sofort per `git stash pop` wiederhergestellt,
  keine Daten verloren, aber dadurch wurde die bereits vor diesem Step bestehende
  `MaxDirectoryChildren`-Verletzung (31/30) am reinen HEAD-Zustand sichtbar bestätigt.

## Bekannte Unschärfen

- **`violation_density`-Sortierung ist ungewichtet** (Summe aller Severities, kein Fehler zählt
  doppelt) — bewusste Vereinfachung laut Plan „Bekannte Ausnahmen", keine Fehlimplementierung.
- **`complexity`-Modus deckt nur `MethodDeclarationSyntax`** ab (keine Properties, Konstruktoren,
  lokale Funktionen) — bewusste Scope-Grenze laut Plan, konsistent mit der bestehenden
  `ComplexityCalculator`-Signatur.
- Die zwei neuen Roslyn-Modi laufen bei jedem Aufruf einen vollen `LinterEngine`-Durchlauf bzw.
  eine Syntax-Baum-Traversierung über den gesamten Scope — kein Caching zwischen aufeinanderfolgenden
  `metrics_tree`-Aufrufen (identisch zu `get_violations`, kein neues Problem, aber bei sehr großen
  Solutions relevant für die Tool-Latenz — nicht separat gemessen in diesem Step).
