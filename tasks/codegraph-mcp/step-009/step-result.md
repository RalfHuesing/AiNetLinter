---
status: done
type: step-result
task: codegraph-mcp
step: 009
epic: EPIC-04
step_type: single
coded_by: coder
coded_by_model: claude-sonnet-5
coded_by_model_knowledge_cutoff: 2026-01
coded_at: 2026-07-31T19:58:00Z
code_commit_hash: 995500e
status_after: done
blocker_category: n/a
---

# Result Step 009: get_hotspots Tool (Zeilen-Hotspot-Kennzahlen der Solution)

## Zusammenfassung

`get_hotspots` implementiert: liefert Dateien der geladenen Solution, die
sich ihrem konfigurierten `MaxLineCount`-Limit nähern/es überschreiten,
inkl. optionalem `scopeFilter` (Projekt-Name oder solution-relativer
Pfad). `McpCodeGraphServer` bekam einen additiven `maxLineCount`-
Konstruktor-Parameter/-Property; `McpServerCommand` lädt jetzt `rules.json`
über eine neue `ResolveMaxLineCount`-Hilfsmethode (1:1-Logik aus
`MapCommand`). Scan-/Formatierungslogik liegt in `GetHotspotsScanner`,
getrennt vom dünnen `GetHotspotsTool`-Dispatch (TD-005-Muster).

## Geänderte Dateien

- `src/AiNetLinter/Mcp/McpCodeGraphServer.cs` — additiver dritter
  Konstruktor-Parameter `int maxLineCount = 700` + public `MaxLineCount`-
  Property.
- `src/AiNetLinter/Commands/McpServerCommand.cs` — neue `internal static`
  Hilfsmethode `ResolveMaxLineCount(LinterArgs)` (1:1-Übernahme aus
  `MapCommand`), Aufruf vor der `McpCodeGraphServer`-Konstruktion.
- `src/AiNetLinter/Mcp/Tools/GetHotspotsTool.cs` (neu) — dünner Dispatch,
  delegiert an `GetHotspotsScanner`.
- `src/AiNetLinter/Mcp/Tools/GetHotspotsScanner.cs` (neu) — Sammel-/
  Filter-/Klassifikations-/Formatierungslogik, unabhängig von
  `McpCodeGraphServer`.
- `src/AiNetLinter/Mcp/FileStructureToolRegistrations.cs` — Registrierung
  von `get_hotspots`, Klassenkommentar aktualisiert.
- `src/AiNetLinter.Tests/Mcp/Tools/GetHotspotsToolTests.cs` (neu) — 6
  Tests: Fehlerpfad, kritische/Warnungs-Klassifikation, Default-Grün,
  Scope-Filter-Treffer, Scope-Filter-Leermenge.
- `src/AiNetLinter.Tests/Commands/McpServerCommandTests.cs` — Tool-Count-
  Test auf 7 erweitert/umbenannt, neuer E2E-Test für `get_hotspots`
  (alles grün bei kleiner Fixture), zwei neue Unit-Tests für
  `ResolveMaxLineCount` (Config-Wert vs. Default).

## Commit

- **Code-Commit-Hash:** `995500e`
- **Message:**
  ```
  feat(mcp): add get_hotspots tool [codegraph-mcp]

  Adds the second EPIC-04 tool: line-count hotspot metrics for .cs
  files in the loaded solution, gated by the configured MaxLineCount
  (rules.json or default), with an optional scope filter matching
  project name or solution-relative path. Reuses the SourceFileCatalog
  file inventory (same rationale as get_index_scope in step-008) instead
  of a second filesystem scan, so fixtures outside the solution are
  never miscounted.

  McpCodeGraphServer gains an additive maxLineCount constructor
  parameter/property (default 700). McpServerCommand now resolves the
  configured MaxLineCount from --config (same logic as
  MapCommand.ResolveMaxLineCount) before constructing the server. Scan/
  formatting logic lives in GetHotspotsScanner, separate from the thin
  GetHotspotsTool dispatch, to keep AIContextFootprint under the 2500
  limit (TD-005 pattern).

  Refs: tasks/codegraph-mcp/step-009
  ```
- **Branch:** main
- **Push:** nein (lokal)
- **Doku-Commit:** separater, zweiter Commit (Hash steht nicht hier drin —
  Selbstbezug, siehe `git log`).

## Build-/Test-Output

```
dotnet build AiNetLinter.slnx → grün, 0 Warnungen
dotnet test AiNetLinter.slnx  → grün (1080 Tests, 0 Fehler)
ainetlinter --config rules.json --path . → OK (0 Violations)
```

## Selbst-Lint-Footprint-Kontrolle (DoD-Pflicht)

```
--footprint GetHotspotsTool              → 2424 (< 2500)
--footprint FileStructureToolRegistrations → 2455 (< 2500)
```

Keine dritte Registrar-Klasse nötig — `FileStructureToolRegistrations`
bleibt mit 21 Zeilen Puffer knapp, aber unter dem Limit. Für den nächsten
EPIC-04-Tool-Step (`get_violations`) ist dieser Puffer voraussichtlich
nicht mehr ausreichend — sollte dort als Erstes geprüft werden (siehe
"Beobachtungen").

## Abweichungen vom Plan

Keine strukturellen Abweichungen — Dateiplan, Klassenaufteilung
(`GetHotspotsTool`/`GetHotspotsScanner`), Registrierungs- und
Testumfang 1:1 wie im Step-Plan beschrieben umgesetzt. Zwei
Implementierungsdetails, die der Plan nicht bis auf Zeilenebene
vorwegnahm und die beim ersten Testlauf auffielen:

- **Registrierung des `scopeFilter`-Parameters:** der Plan-Codeausschnitt
  für `GetHotspotsTool.ExecuteAsync` zeigt `string? scopeFilter` als
  Parameter, aber nicht explizit die Lambda-Signatur in
  `FileStructureToolRegistrations`. Ohne `= null`-Default im
  registrierten Lambda-Parameter (`(string? scopeFilter, ...)` statt
  `(string? scopeFilter = null, ...)`) lehnt das MCP-SDK den Aufruf ohne
  übergebenes Argument mit einem internen Fehler ab (E2E-Test schlug mit
  `IsError == true` fehl). Behoben nach dem bereits bestehenden Muster
  aus `SymbolGraphToolRegistrations` (`gitRef`/`symbolIdentifier` dort
  ebenfalls mit `= null`).
- **Testfixture für `ResolveMaxLineCount`:** minimales `rules.json` mit
  nur `{ "Metrics": { "MaxLineCount": 5 } }` schlägt beim Deserialisieren
  fehl, weil `Config.Global` `required` ist (C# `required`-Member) —
  `ConfigLoader.LoadConfig` fängt die resultierende Exception generisch
  ab und liefert `null` zurück (stiller Fallback auf Default, kein
  Absturz). Testfixture entsprechend um `"Global": {}` ergänzt.

## Beobachtungen

- `FileStructureToolRegistrations` liegt jetzt bei 2455/2500 — nur noch
  45 Zeilen Puffer, bei einem historischen Trend von ~11-15 Zeilen pro
  `tools.Add(...)`-Eintrag reicht das für höchstens einen weiteren Tool-
  Eintrag ähnlicher Größe. Der nächste EPIC-04-Step (`get_violations`,
  danach `search_pattern`) sollte den Footprint-Check als Erstes machen,
  nicht erst am Ende — die im aktuellen Plan bereits benannte Ausweich-
  Option (dritte Registrar-Klasse, z. B. `AnalysisToolRegistrations`) wird
  hier wahrscheinlich fällig.
- `GetHotspotsScanner` dupliziert wie geplant `WarnThreshold`/
  `CriticalThreshold` aus `HotspotMapBuilder`. Bewusst so — keine neue
  Beobachtung, nur Bestätigung, dass die Duplikation beim tatsächlichen
  Schreiben unauffällig blieb (zwei `const double`, keine Logik).
- Die vorab existierende, bereits im Code sichtbare TOCTOU-Lücke in
  `SourceFileCatalog.RegisterMSBuild()` (`if (!MSBuildLocator.IsRegistered)`
  ohne Lock) führte während des ersten vollen Testlaufs dieses Steps zu
  einem einmaligen, nicht reproduzierbaren Fehlschlag von
  `McpCodeGraphServerTests.GetCurrentSolution_FileTouchedWithoutContentChange_SkipsSolutionUpdate`
  (`MSBuildLocator.RegisterInstance was called, but MSBuild assemblies
  were already loaded`) bei parallelisierter Testausführung. Beim
  Wiederholungslauf (identischer Code) trat der Fehler nicht erneut auf —
  vorbestehende Race-Condition, nicht durch diesen Step verursacht, nicht
  in `SourceFileCatalog.cs` angefasst (außerhalb des Step-Scopes). Kandidat
  für einen Tech-Debt-Eintrag (Lock um die Registrierung), falls das
  künftig häufiger auftritt.

## Bekannte Unschärfen

- Der Scope-Filter matched wie im Plan dokumentiert gegen Projekt-Name
  und solution-relativen Dateipfad, nicht gegen die tatsächliche
  `namespace`-Deklaration — bewusste, dokumentierte Vereinfachung (siehe
  Step-Plan "Bekannte Ausnahmen"), im Dogfooding-Lauf nicht separat
  gegen die reale Solution verifiziert (nur die Fixture-Tests decken den
  Filter ab; die Fixture hat keine Namespace/Ordner-Abweichung, die den
  Unterschied sichtbar machen würde).
- Die Klassifikationsgrenzen (`WarnThreshold`/`CriticalThreshold`) wurden
  1:1 aus `HotspotMapBuilder` übernommen, aber nicht erneut fachlich
  hinterfragt — reine Übernahme, kein neuer Erkenntnisgewinn dazu.

## Dogfooding

Ad-hoc-Aufruf von `get_hotspots` gegen die reale `AiNetLinter.slnx` über
den MCP-Server (Subprozess, JSON-RPC über stdio, newline-delimited,
`--mcp-server --path . --config rules.json`, per Python-Skript
initialisiert und `tools/call` für `get_hotspots` ohne `scopeFilter`
aufgerufen):

```
Gescannt: 297 .cs-Dateien | MaxLineCount: 500

## Kritische Dateien (>=95% des Limits)

| Datei | Zeilen | Auslastung | Verbleibend |
|:---|---:|---:|---:|
| src/AiNetLinter.Tests/MaxConstructorDependenciesTests.cs | 495 | 99 % | 5 Zeilen |
| src/AiNetLinter.Tests/FalsePositives/FalsePositiveTests.cs | 475 | 95 % | 25 Zeilen |

## Warnungs-Dateien (>=80% des Limits)

| Datei | Zeilen | Auslastung | Verbleibend |
|:---|---:|---:|---:|
| src/AiNetLinter.Tests/Output/ViolationMarkdownFormatterTests.cs | 473 | 95 % | 27 Zeilen |
| src/AiNetLinter.Tests/LinterAnalyzerTests.cs | 469 | 94 % | 31 Zeilen |
| src/AiNetLinter/Core/RuleRegistry.cs | 459 | 92 % | 41 Zeilen |
| src/AiNetLinter/Core/RuleRegistry.General.cs | 448 | 90 % | 52 Zeilen |
| ... (7 weitere Zeilen) |

## Alle anderen Dateien: 284 Dateien im gruenen Bereich
```

Plausibilitätsprüfung (DoD-Pflicht): `rules.json` im Repo-Root setzt
`MaxLineCount: 500` — die Tool-Ausgabe übernimmt das korrekt (Kopfzeile
`MaxLineCount: 500`, nicht der Default `700`). Stichprobe gegen die
tatsächliche Dateigröße: `src/AiNetLinter/Core/RuleRegistry.cs` hat
`wc -l` zufolge 459 Zeilen; 459/500 = 91,8 % — liegt korrekt in der
Warnungs-Sektion (80-95 %), nicht in der Kritisch-Sektion (≥95 %), exakt
wie vom Tool ausgegeben (92 %, gerundet). Zweite Stichprobe:
`MaxConstructorDependenciesTests.cs` mit 495 Zeilen liegt bei 99 % —
korrekt in der Kritisch-Sektion. Beide Kategorisierungen sind konsistent
mit den tatsächlichen Dateigrößen und dem konfigurierten Grenzwert.
