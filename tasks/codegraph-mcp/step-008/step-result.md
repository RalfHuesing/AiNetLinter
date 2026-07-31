---
status: done
type: step-result
task: codegraph-mcp
step: 008
epic: EPIC-04
step_type: single
coded_by: coder
coded_by_model: claude-sonnet-5
coded_by_model_knowledge_cutoff: 2026-01
coded_at: 2026-07-31T16:32:00Z
code_commit_hash: 6624312
status_after: done
blocker_category: n/a
---

# Result Step 008: get_index_scope Tool (Dateityp-Aufschlüsselung der Solution)

## Zusammenfassung

`get_index_scope` implementiert: parameterloses Tool, liefert eine Zeile
pro Dateityp (`.cs`/`.css`/`.html`/`.js`/`.razor`/`.xaml`) mit Anzahl und
Abdeckungshinweis. `.cs` über `SourceFileCatalog.IsValidDocument`,
`.css`/`.js`/`.razor` über `WebFileCatalog.Collect`, `.xaml`/`.html` über
einen neuen, minimalen Verzeichnis-Scan auf Basis der jetzt `internal`
gemachten `WebFileCatalog.GetProjectDirectories`. In
`FileStructureToolRegistrations` registriert.

## Geänderte Dateien

- `src/AiNetLinter/Web/WebFileCatalog.cs` — `GetProjectDirectories` von
  `private` auf `internal` angehoben (reine Sichtbarkeitsänderung, wie
  im Plan vorgesehen).
- `src/AiNetLinter/Mcp/Tools/GetIndexScopeTool.cs` (neu) — dünner
  Dispatch: löst Solution auf, delegiert an `GetIndexScopeScanner`.
- `src/AiNetLinter/Mcp/Tools/GetIndexScopeScanner.cs` (neu, **nicht im
  ursprünglichen Plan-Dateiplan** — siehe „Abweichungen") — die eigentliche
  Zähl-/Formatierungslogik, ohne Abhängigkeit auf `McpCodeGraphServer`.
- `src/AiNetLinter/Mcp/FileStructureToolRegistrations.cs` — Registrierung
  von `get_index_scope`, Klassenkommentar aktualisiert.
- `tests/Fixtures/SymbolGraphMini/src/SymbolGraphMini/wwwroot/` (neu) —
  `site.js`, `Component.razor`, `styles.css`, `Page.xaml`, `index.html`,
  je eine Datei pro bislang fehlendem Typ.
- `src/AiNetLinter.Tests/Mcp/Tools/GetIndexScopeToolTests.cs` (neu) —
  5 Tests: Fehlerpfad, `.cs`-Zählung, `.css`/`.js`/`.razor`-Zählung,
  `.xaml`/`.html`-Zählung, `obj`/`bin`-Ausschluss-Regression.
- `src/AiNetLinter.Tests/Commands/McpServerCommandTests.cs` — Test auf 6
  Tools umbenannt/erweitert, neuer E2E-Test
  `RunAsync_ValidFixture_GetIndexScopeReturnsFileTypeBreakdown`.

## Commit

- **Code-Commit-Hash:** `6624312`
- **Message:**
  ```
  feat(mcp): add get_index_scope tool [codegraph-mcp]

  Adds the first EPIC-04 tool: a file-type breakdown of the loaded
  solution (.cs fully covered by the symbol graph; .css/.js/.razor via
  the existing WebFileCatalog; .xaml/.html via a new minimal filesystem
  scan) so an agent can orient itself before calling find_symbol or
  search_pattern. Scan/formatting logic lives in a separate
  GetIndexScopeScanner class (no McpCodeGraphServer dependency) to keep
  GetIndexScopeTool's own AIContextFootprint under the 2500 limit.

  Refs: tasks/codegraph-mcp/step-008
  ```
- **Branch:** main
- **Push:** nein (lokal)
- **Doku-Commit:** separater, zweiter Commit (Hash steht nicht hier
  drin — Selbstbezug, siehe `git log`).

## Build-/Test-Output

```
dotnet build AiNetLinter.slnx → grün, 0 Warnungen
dotnet test AiNetLinter.slnx  → grün (1071 Tests, 0 Fehler)
ainetlinter --config rules.json --path . → OK (0 Violations)
```

## Abweichungen vom Plan

- **Neue Datei `GetIndexScopeScanner.cs` nicht im ursprünglichen
  Dateiplan.** Der Plan sah die gesamte Zähl-/Formatierungslogik direkt
  in `GetIndexScopeTool.cs` vor (Datei 2), mit dem DoD-Hinweis: "Reißt
  eine der beiden Klassen das Limit: Formatier-/Scan-Logik in eine
  separate, `McpCodeGraphServer`-unabhängige Datei auslagern." Genau das
  ist eingetreten: die erste Implementierung (alles in
  `GetIndexScopeTool.cs`, ~119 Zeilen) hatte einen Footprint von 2503
  (> 2500) — Ursache war nicht die neu importierte `Configuration`-
  Namespace-Abhängigkeit (die pullt über `McpCodeGraphServer` ohnehin
  schon jedes Tool, siehe `GetFileSkeletonTool` bei 2428), sondern die
  schiere Zeilenzahl der eigenen Klasse selbst (der
  `AIContextFootprintCalculator` zählt die deklarierende Datei der
  gemessenen Klasse mit). Behoben durch Auslagerung der Zähl-/
  Formatierungslogik in `GetIndexScopeScanner` (analog
  `GetTypeHierarchyFormatter`-Muster) — `GetIndexScopeTool` selbst ist
  jetzt nur noch dünner Dispatch (~20 Zeilen). Footprint danach: 2413
  (`GetIndexScopeTool`) bzw. 2434 (`FileStructureToolRegistrations`),
  beide dokumentiert unten unter „Selbst-Lint-Footprint-Kontrolle".
- Ansonsten 1:1 wie geplant umgesetzt (Sichtbarkeitsänderung,
  Registrierung, Fixture-Erweiterung, Tests).

## Selbst-Lint-Footprint-Kontrolle (DoD-Pflicht)

```
--footprint FileStructureToolRegistrations → 2434 (< 2500)
--footprint GetIndexScopeTool              → 2413 (< 2500)
```

## Dogfooding

Ad-hoc-Aufruf von `get_index_scope` gegen die reale
`AiNetLinter.slnx` über den MCP-Server (Subprozess, JSON-RPC über
stdio, `--mcp-server --path .`):

```
.cs: 294 Dateien (voll vom Symbolgraph abgedeckt)
.css: 0 Dateien (nicht vom Symbolgraph abgedeckt)
.html: 0 Dateien (nicht vom Symbolgraph abgedeckt)
.js: 0 Dateien (nicht vom Symbolgraph abgedeckt)
.razor: 0 Dateien (nicht vom Symbolgraph abgedeckt)
.xaml: 0 Dateien (nicht vom Symbolgraph abgedeckt)
```

Plausibilitätsprüfung: `.css`/`.html`/`.js`/`.razor`/`.xaml` bei `0` ist
korrekt — wie im Step-Plan vorhergesagt enthält das eigene Repo (außerhalb
von `obj`/`bin`) keine Dateien dieser Typen. Verifiziert per
Dateisystem-Zählung: `src/` (die von `AiNetLinter.slnx` tatsächlich
referenzierten Projekte) enthält exakt 294 `.cs`-Dateien außerhalb von
`obj`/`bin` — deckungsgleich mit der Tool-Ausgabe. Die zusätzlichen 7
`.cs`-Dateien im Repo liegen unter `tests/Fixtures/*` (eigene,
unabhängige Fixture-Solutions, nicht Teil von `AiNetLinter.slnx`) und
werden zu Recht nicht mitgezählt. Der positive Nachweis für
`.css`/`.html`/`.js`/`.razor`/`.xaml` (jeweils > 0, korrekt gezählt)
kommt wie geplant aus den Unit-/E2E-Tests gegen die erweiterte
`SymbolGraphMini`-Fixture, nicht aus diesem Dogfooding-Lauf.

## Beobachtungen

- Der `AIContextFootprintCalculator` zählt die deklarierende
  Syntax-Datei der gemessenen Klasse selbst als Teil ihres eigenen
  Footprints mit (nicht nur transitive Fremdabhängigkeiten) — das war
  in dieser Deutlichkeit aus dem Plan-Text nicht ersichtlich (der Plan
  vermutete die `Configuration`-Namespace-Abhängigkeit als
  Hauptverdächtigen). Für künftige EPIC-04-Tools (`get_hotspots`,
  `get_violations`) relevant: eigene Tool-Klassen sollten von Anfang an
  als dünner Dispatch angelegt werden (Muster aus step-004/007), nicht
  erst nachträglich aufgeteilt werden, wenn absehbar ist, dass die
  Zähl-/Scan-Logik mehr als ein paar Zeilen braucht.
- `FileFiltersConfig()`-Default-Konstruktor wie im Plan vorgesehen ohne
  weiteren Kontext nutzbar — keine Überraschungen dort.

## Bekannte Unschärfen

- Die in „Bekannte Ausnahmen" des Plans dokumentierten Einschränkungen
  (keine `FileFiltersConfig`-Anbindung für `.xaml`/`.html`, `.cs`-Zählung
  ignoriert Test-vs-Produktion-Unterscheidung) wurden 1:1 übernommen,
  keine Abweichung.
- Der Kritiker sollte den neuen `GetIndexScopeScanner`-Split gegen
  TD-005-Konventionen prüfen (Namensmuster `*Formatter` vs. hier
  `*Scanner` — bewusst anders benannt, weil die Klasse sowohl scannt als
  auch formatiert; falls das nicht passt, ist eine Umbenennung unkritisch).
