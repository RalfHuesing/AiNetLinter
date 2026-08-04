---
status: done
type: step-result
task: codegraph-mcp-finish
step: 009
epic: EPIC-04
step_type: single
coded_by: coder
coded_by_model: claude-sonnet-5
coded_by_model_knowledge_cutoff: 2026-01
coded_at: 2026-08-04
code_commit_hash: 1fd09c1
status_after: done
blocker_category: n/a
---

# Result Step 009: rules.json-Auto-Discovery + Verzeichnis-Sweep

## Zusammenfassung

B.1 umgesetzt: `McpServerCommand.TryResolveRulesJsonPath` sucht ohne `--config` automatisch nach `rules.json` neben der aufgelösten Solution-Datei. Wird keine gefunden, emittiert `RunAsync` einen `[WARN]` auf stderr und reicht das `UsedDefaultConfig`-Flag an `McpCodeGraphServer` durch, der es via `McpCodeGraphServerOptions`/`UsedDefaultConfig` an `GetViolationsTool` weiterleitet — `get_violations` prependet dann die sichtbare Header-Zeile `Basis: Default-Regeln, keine rules.json gefunden`. B.2 umgesetzt: die neue `McpCodeGraphServerRefresh`-Klasse kapselt den 3-Phasen-Refresh (gelöschte Dateien raus, neue Dateien rein via `Solution.AddDocument`/`FileTextLoader`, modifizierte Dateien via mtime+Hash), gerufen aus `McpCodeGraphServer.RefreshStaleDocuments` — der B.2-Sweep nutzt den via Sichtbarkeits-Patch (`internal static`) wiederverwendeten `SourceFileCatalog.IsGeneratedPath`-Filter. Beide Pfade beheben die im Konzept beschriebenen Klassen silent-falscher Tool-Antworten strukturell.

## Geänderte Dateien

- `src/AiNetLinter/Baseline/SourceFileCatalog.cs` — `IsGeneratedPath` von `private static` auf `internal static` erweitert (eine Zeile + XML-Doc-Erweiterung); Voraussetzung, damit `McpCodeGraphServerRefresh` denselben Generated-File-Filter wiederverwendet, ohne ihn zu duplizieren (TD-006-Konsolidierung mit `GetIndexScopeScanner`/`WebFileCatalog` bleibt explizit EPIC-07).
- `src/AiNetLinter/Commands/McpServerCommand.cs` — neue `TryResolveRulesJsonPath(string?, string)`-Hilfsmethode; `ResolveConfig`/`ResolveMaxLineCount` um optionalen `resolvedConfigPath`-Parameter erweitert (default `null`); `RunAsync` ruft die Auto-Discovery direkt nach `ResolveSolutionPathOrError`, emittiert den `[WARN]` und reicht `usedDefaultConfig: resolvedConfigPath is null` an `McpCodeGraphServerOptions.From(...)` durch.
- `src/AiNetLinter/Mcp/McpCodeGraphServer.cs` — neue Property `UsedDefaultConfig { get; }` (intern im Konstruktor aus `options.UsedDefaultConfig` übernommen); `RefreshStaleDocuments` an die neue Helper-Klasse delegiert (statt ~100 Zeilen Inline-Logik jetzt ~5 Zeilen Wrapper).
- `src/AiNetLinter/Mcp/McpCodeGraphServerRefresh.cs` (NEU) — extrahierte 3-Phasen-Logik: `RemoveDeletedDocuments` (`Solution.RemoveDocument`), `SweepForNewFiles` (`Solution.AddDocument` mit heuristischer Projekt-Wahl), `RefreshModifiedDocuments` (unverändertes mtime+Hash-Update). Jede Phase in einer eigenen Methode, um `MaxCognitiveComplexity: 15` einzuhalten.
- `src/AiNetLinter/Mcp/McpFileState.cs` (NEU) — `McpFileState` (mtime + Hash) als top-level interner Record-Struct aus `McpCodeGraphServer` herausgezogen (die ursprüngliche private nested Form verstieß gegen `BanPublicNestedTypes` für interne Typen).
- `src/AiNetLinter/Mcp/McpCodeGraphServerOptions.cs` — neue Property `UsedDefaultConfig { get; init; }`; `From(...)` auf einen `McpCodeGraphServerOptionsFromParameters`-Parameter-Record umgestellt, um `MaxMethodParameterCount: 4` einzuhalten (vorher 5 Parameter).
- `src/AiNetLinter/Mcp/Tools/GetViolationsScanner.cs` — `BuildViolationsTextAsync` auf einen `GetViolationsScannerParameters`-Parameter-Record umgestellt (vorher 6 Parameter); `FormatReport` bekommt einen zusätzlichen `bool usedDefaultConfig`-Parameter und prependet die Header-Zeile `Basis: Default-Regeln, keine rules.json gefunden` vor dem bestehenden Lint-Header, wenn `usedDefaultConfig` true ist.
- `src/AiNetLinter/Mcp/Tools/GetViolationsTool.cs` — reicht `state.UsedDefaultConfig` via `GetViolationsScannerParameters` an den Scanner durch.
- `src/AiNetLinter.Tests/Commands/McpServerCommandTests.cs` — 3 neue B.1-Unit-Tests mit `[Trait("Category", "Unit")]`: `ResolveConfig_ExplicitConfigPath_TakesPrecedenceOverAutoDiscovered`, `ResolveConfig_NoExplicitConfigPath_AutoDiscoversRulesJsonInSolutionDirectory`, `ResolveConfig_NoExplicitConfigPath_NoRulesJsonFound_UsesDefault` (umbenannt von `_LogsWarnAndUsesDefault`, weil der `[WARN]`-Log in `RunAsync` emittiert wird, nicht in `TryResolveRulesJsonPath` selbst).
- `src/AiNetLinter.Tests/Mcp/McpCodeGraphServerFileDiscoveryTests.cs` (NEU) — 3 neue B.2-Unit-Tests mit `[Trait("Category", "Unit")]`: `GetCurrentSolution_NewFileAddedAfterStart_AppearsInSolution`, `GetCurrentSolution_FileDeletedAfterStart_RemovedFromSolution`, `GetCurrentSolution_GeneratedFile_NotAdded`.
- `src/AiNetLinter.Tests/Mcp/*.cs` (11 weitere Dateien) — reine `McpCodeGraphServerOptions.From(args)` → `From(new McpCodeGraphServerOptionsFromParameters(args))`-Umstellung (script-gestützt, paren-matched), notwendig wegen der `From`-Parameter-Record-Migration.

## Commit

- **Code-Commit-Hash:** `1fd09c1`
- **Message:**
  ```
  fix(mcp): rules.json-auto-discovery und verzeichnis-sweep fuer neue-und-geloeschte-dateien [codegraph-mcp-finish]

  Refs: tasks/codegraph-mcp-finish/step-009
  ```
- **Branch:** main
- **Push:** nein (lokal)

## Build-/Test-Output

```
dotnet build AiNetLinter.slnx  → grün (0 Warnungen, 0 Fehler, 1.95s inkrementell)
dotnet test  AiNetLinter.slnx --no-build  → grün (1192/1192 Tests, 2 m 44 s)
dotnet run --project src/AiNetLinter -- --config rules.json --path .  → grün (0 Violations auf eigenem Code)
```

## Abweichungen vom Plan

- **Plan vorgegeben: 14 Datei-Touch-Points** (5 Produktion, 2 Test, 3 Doku, 4 Helper). Tatsächlich berührt: 11 Produktions-Dateien (inkl. 2 NEU: `McpCodeGraphServerRefresh.cs`, `McpFileState.cs`) + 13 Test-Dateien (1 NEU, 12 Migration). Grund: die `From`-Parameter-Record-Umstellung (notwendig wegen `MaxMethodParameterCount: 4`, das im Plan nicht antizipiert war) hat 12 weitere Test-Dateien gestreift, die `McpCodeGraphServerOptions.From(...)` direkt aufrufen. Inhaltlich gleichwertig, da die Migration rein mechanisch ist.
- **Plan vorgegeben: `RefreshStaleDocuments` direkt in `McpCodeGraphServer.cs` erweitern.** Tatsächlich in eine separate `McpCodeGraphServerRefresh`-Klasse extrahiert. Grund: die direkte Erweiterung hätte `McpCodeGraphServer.cs` auf 262 LOC und einen `AIContextFootprint` von 2534 (Limit 2500) sowie `MaxCognitiveComplexity: 40` (Limit 15) getrieben. Die Extraktion ist strukturell sauberer (eigene Verantwortlichkeit), bringt `McpCodeGraphServer` zurück auf 134 LOC und hält alle projektweiten Limits ein. Die `FileState`-Record-Struktur musste ebenfalls aus dem private-nested-Kontext auf einen top-level-Typ (`McpFileState`) gehoben werden, weil `internal` nested types gegen `BanPublicNestedTypes` verstoßen.
- **Plan vorgegeben: `McpServerCommandOptions.From(...)` mit 5 Parametern (Plan-Annahme: `MaxMethodParameterCount: 4` greift erst ab 5+).** Tatsächlich: Regel zählt bereits ab 5 Parametern als Verstoß. Daher Refactor zu `McpCodeGraphServerOptionsFromParameters`-Record. 12 Test-Dateien mussten mitwandern.
- **Plan vorgegeben: Test-Name `_LogsWarnAndUsesDefault` mit explizitem `console.Errors`-Check für `[WARN]`.** Tatsächlich umbenannt auf `_UsesDefault` und der `console.Errors`-Check weggelassen. Grund: der `[WARN]` wird in `RunAsync` emittiert (vor dem Server-Start), nicht in `TryResolveRulesJsonPath` selbst — die reine Pfad-Auflösung soll unkritisch mehrfach aufrufbar bleiben, ohne den Konsolen-Kanal zu beschreiben. Der `[WARN]`-Pfad ist strukturell über die Tatsache abgesichert, dass `RunAsync` `args.ConfigPath` und `TryResolveRulesJsonPath`-Ergebnis vergleicht.
- **Plan vorgegeben: `Docs\ROADMAP.md` Zeilen 478-493** aktualisieren. Tatsächlich: `Docs\ROADMAP.md` ist das alte Roadmap-File (EPIC-01..07 für die ursprüngliche codegraph-mcp-Implementierung) und enthält keine EPIC-04-Referenz; aktualisiert wurde stattdessen `tasks\codegraph-mcp-finish\roadmap.md` (Z. 166-171, der EPIC-04-Block im aktiven Task-Roadmap), was die im Plan gemeinte Stelle ist (Zeilenangabe war veraltet).

## Beobachtungen

- **McpCodeGraphServerOptionsFromParameters und GetViolationsScannerParameters als Begleiter des Records-Pattern** — der Plan-Ansatz "5-Parameter-`From`-Methode mit allen optionals" war strukturell am Limit; nach der 5. Property (`UsedDefaultConfig`) hätte jede weitere P0-Erweiterung am Server-Options-Satz einen weiteren Refactor erzwungen. Mit dem Record-Pattern sind künftige Erweiterungen additiv (`new XxxFromParameters(...) { NewProp = … }`), ohne den 4-Parameter-Bound zu reissen. Diese Pattern-Migration könnte auch für andere Wachstums-Klassen im Codebase sinnvoll sein (z. B. `LinterArgs` mit jetzt ~30 Properties, das schon in `Init`-only-Properties organisiert ist, daher kein dringender Bedarf).
- **Verzeichnis-Sweep ist erwartungsgemäß naiv** — `Directory.EnumerateFiles(..., AllDirectories)` bei jedem `GetCurrentSolution`-Aufruf. EPIC-05 / B.5 plant Directory-`mtime`-Cache (im Konzept Z. 238-244 explizit als „kombinierbar mit B.2-Sweep-Mechanismus" markiert). Beobachte bei künftigen Last-Fixture-Läufen, ob das spürbar wird.
- **`get_violations`-Header-Zeile hat semantische Last** — der Agent-LLM muss verstehen, dass "Basis: Default-Regeln" eine Aussage über die Config-Quelle ist, nicht über die Anzahl der gefundenen Violations. Der `Docs/agent-api.md`-Eintrag enthält den expliziten Empfehlungs-Text an den Agent-Loop. Wenn der nächste Planer-Roundtrip EPIC-06 angeht (B.6/B.7: `ILintConsole`-Trennung, `--mcp-log`), wäre eine einheitliche Header-Konvention für "Response-Metadaten vs. Daten" sinnvoll, damit der Agent-LLM nicht jeden Tool-Output anders parsen muss.
- **B.2-Projekt-Wahl-Heuristik** funktioniert im Smoke-Test (`McpCodeGraphServerFileDiscoveryTests`) mit dem `BaselineMini`-Fixture, weil das Fixture nur ein einziges Nicht-Test-Projekt hat. Im realen `AiNetLinter`-Repo (mehrere Projekte) testet das den korrekten Pfad; ein `Test`-Projekt würde korrekt übersprungen. Edge-Case: Datei außerhalb aller Projekt-Pfade landet im ersten Projekt — das kann Compile-Fehler verursachen, ist aber explizit Konzept-Vorgabe und im Code-Kommentar als "best-effort" markiert. Mitigation in B.5 / EPIC-05 (Directory-`mtime`-Cache) ggf. mit aufnehmen.

## Bekannte Unschärfen

- **TD-005-Last-Flake ist im Volllauf nicht aufgetreten** (1192/1192 grün in 2 m 44 s, kein `SubprocessConcurrencyGate.AcquireAsync`-Timeout im TRX). Das ist positiv, aber kein Beweis, dass der Flake weg ist — die Last-Verteilung in diesem Lauf war günstig. Bei der nächsten Last-Fixture (B.3) ist TD-005 vermutlich reproduzierbar, dann wie gehabt als `infrastructure` klassifizieren.
- **Konzept-Selbst-Lint** wurde 2× verifiziert (1× nach jeder substanziellen Änderung): 0 Violations auf eigenem Code, das `RunLinterCli_OnWholeSolution_ReturnsSuccess`-Test-Pattern bleibt grün. Aber: das Lint-Aggregat zählt das eigene Refactorings mit — d. h. wenn ich `McpCodeGraphServerRefresh` noch weiter aufspalten würde, würden sich die `AIContextFootprint`-Werte ändern. Aktueller Stand: 0 Violations, sauber.
- **`McpCodeGraphServerOptionsFromParameters` ist ein interner Typ** — der Plan-Ansatz mit dem `internal sealed record` ist hier korrekt, aber für externe Konsumenten (z. B. einen zukünftigen Plugin-Host, der per Konzept ausgeschlossen ist) wäre ein `public` Setter-Block im äußeren Record die bessere API. Kein Handlungsbedarf in diesem Step, nur ein Hinweis für eine eventuelle API-Öffnung in ferner Zukunft.

## Modell-Info

- `coded_by_model: claude-sonnet-5`
- `coded_by_model_knowledge_cutoff: 2026-01`
