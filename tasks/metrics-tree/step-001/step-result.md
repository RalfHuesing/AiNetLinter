---
status: done
type: step-result
task: metrics-tree
step: 001
epic: EPIC-01
step_type: single
coded_by: coder
coded_by_model: claude-sonnet-5
coded_by_model_knowledge_cutoff: 2026-01
coded_at: 2026-08-08
code_commit_hash: 92251cb
status_after: done
blocker_category: n/a
---

# Result Step 001: metrics_tree — Datei-Walk-Modi (EPIC-01)

## Zusammenfassung

Walk-Kern (`CollectFiles`/`MatchesScope`/`TryCountLines`) aus `GetHotspotsScanner` nach
`SolutionFileWalker` extrahiert und generalisiert (Regex-`fileFilter`, `TryReadAllLines` statt
nur Zeilenzahl); `GetHotspotsScanner` nutzt den neuen Walker jetzt statt eigener Implementierung.
Neues MCP-Tool `metrics_tree` mit den zwei Datei-Walk-Modi `code_size` (LoC + Bytes, absteigend
sortiert) und `comment_density` (Kommentar-Ratio, aufsteigend sortiert) implementiert: modus-
agnostischer ASCII-Tree-Renderer (`MetricsTreeRenderer`/`MetricsTreeNode`), Scanner mit
Verzeichnis-Aggregation bis `depth`, dünner Tool-Dispatch mit Validierung, eigener Drill-down-
Hinweistyp (`McpDrillDownHints`) als Gegenstück zu `McpSufficiencyHints`, Registrierung in
`FileStructureToolRegistrations`.

## Geänderte Dateien

- `src/AiNetLinter/Mcp/Tools/SolutionFileWalker.cs` (neu) — extrahierter/generalisierter Walk-Kern.
- `src/AiNetLinter/Mcp/Tools/GetHotspotsScanner.cs` — nutzt jetzt `SolutionFileWalker` statt eigener
  `CollectFiles`/`MatchesScope`/`TryCountLines`; Verhalten unverändert (Regressionsabsicherung über
  bestehende `GetHotspotsToolTests`).
- `src/AiNetLinter/Mcp/Tools/MetricsTreeMode.cs` (neu) — `MetricsTreeMode`-Enum + Parser.
- `src/AiNetLinter/Mcp/Tools/MetricsTreeRenderer.cs` (neu) — `MetricsTreeNode`-Record +
  modus-agnostischer ASCII-Tree-Renderer.
- `src/AiNetLinter/Mcp/Tools/MetricsTreeScanner.cs` (neu) — Walk + Verzeichnis-Aggregation für
  `code_size`/`comment_density`, Kommentar-Zeilen-Heuristik.
- `src/AiNetLinter/Mcp/Tools/MetricsTreeTool.cs` (neu) — dünner Dispatch, Argument-Validierung.
- `src/AiNetLinter/Mcp/McpDrillDownHints.cs` (neu) — Drill-down-Hinweistext.
- `src/AiNetLinter/Mcp/FileStructureToolRegistrations.cs` — `AddMetricsTree` ergänzt, `Register(...)`
  ruft es auf, Klassen-Doc-Kommentar um `metrics_tree` erweitert.
- `src/AiNetLinter/Mcp/OverviewResourceRegistration.cs` — `metrics_tree`-Eintrag in `ToolSummaries`
  ergänzt (14 statt 13 Tools; nötig, sonst schlägt `OverviewResourceRegistrationTests.ToolSummaries_MatchesRegisteredToolNames`
  fehl — Namens-Paritätstest zwischen `ToolSummaries` und tatsächlich registrierten Tools).
- `src/AiNetLinter.Tests/Mcp/OverviewResourceRegistrationTests.cs` — `BuildOverviewText_ListsAllThirteenTools`
  in `BuildOverviewText_ListsAllFourteenTools` umbenannt, erwartete Tool-Anzahl auf 14 angepasst.
- `src/AiNetLinter.Tests/Mcp/Tools/MetricsTreeToolTests.cs` (neu) — 13 Tests: Validierung
  (Solution/mode/depth/topN/regex), beide Modi + Sortierrichtung, Edge-Cases (leerer Scope,
  Single-File-Root, depth=5, file_filter, Drill-down-Hinweis).
- `src/AiNetLinter.Tests/Mcp/Tools/MetricsTreeRendererTests.cs` (neu) — 4 reine Formatierungstests
  ohne Solution/Fixture (auf-/absteigende Sortierung, Top-N-Kürzung + Restzahl, Einrückung).

## Commit

- **Code-Commit-Hash:** `92251cb`
- **Message:**
  ```
  feat(mcp): metrics_tree-Tool mit code_size/comment_density-Modi [metrics-tree]

  Extrahiert den Datei-Walk-Kern aus GetHotspotsScanner nach SolutionFileWalker
  (wiederverwendet statt dupliziert) und baut darauf das neue MCP-Tool
  metrics_tree: ASCII-Tree-Renderer + Scanner fuer die zwei Datei-Walk-Modi
  code_size (LoC/Bytes) und comment_density (Kommentar-Ratio, aufsteigend
  sortiert), Registrierung in FileStructureToolRegistrations, eigener
  Drill-down-Hinweistyp (McpDrillDownHints) als Gegenstueck zu
  McpSufficiencyHints. OverviewResourceRegistration inkl. Tests auf 14 Tools
  aktualisiert.

  Refs: tasks/metrics-tree/step-001
  ```
- **Branch:** main
- **Push:** nein (lokal)
- **Doku-Commit:** separater, zweiter Commit (Hash steht nicht hier drin).

## Build-/Test-Output

```
dotnet build AiNetLinter.slnx                          → grün (0 Warnungen, 0 Fehler)
dotnet test --filter "FullyQualifiedName~MetricsTree|FullyQualifiedName~GetHotspots" → grün (28 Tests, 0 Fehler)
dotnet test --filter Category=Unit                     → grün (1191 Tests, 0 Fehler)
```

## Abweichungen vom Plan

- **`OverviewResourceRegistration.cs` + zugehöriger Test nicht im Plan genannt, aber notwendig
  geändert:** `OverviewResourceRegistrationTests.ToolSummaries_MatchesRegisteredToolNames` prüft
  Namens-Parität zwischen der dort gepflegten `ToolSummaries`-Liste und den tatsächlich
  registrierten MCP-Tools — nach der `metrics_tree`-Registrierung (Datei 8) schlug dieser Test
  im Category=Unit-Lauf fehl. Fix: `metrics_tree`-Eintrag ergänzt, Tool-Zähler-Test (13→14)
  aktualisiert. Notwendige Konsequenz aus der geplanten Registrierung, keine Scope-Erweiterung
  im Sinne einer eigenständigen Änderung.
- **`MetricsTreeTool.ExecuteAsync`-Regex-Validierung in eine private `TryBuildFileFilter`-Hilfsmethode
  ausgelagert** statt Inline-`try/catch` wie im Plan-Pseudocode: reduziert kognitive Komplexität der
  Hauptmethode, identisches Verhalten/identische Fehlermeldung.
- **`FileMetric` im Scanner** trägt `CommentLines`/`CodeLines`/`Bytes` statt eines einzelnen
  `SortValue`-Felds wie im Plan-Pseudocode skizziert: nötig, damit Verzeichnis-Aggregation bei
  `comment_density` eine gewichtete Ratio (Summe Kommentarzeilen / Summe Gesamtzeilen über alle
  Kind-Dateien) statt eines fehlerhaften Mittelwerts von Einzel-Ratios liefert. Die Aggregation
  läuft intern über ein privates `BuilderNode`-Record (mit denselben Rohwerten), erst am Ende wird
  in das plan-vorgegebene, modus-agnostische `MetricsTreeNode` (nur `SortValue`+`DisplayLine`)
  konvertiert — der öffentliche Vertrag aus Datei 4 ist exakt wie geplant.
- **`code_size`-Display-Zeile enthält Bytes** (`"{fileCount} Dateien | {loc} LoC | {bytes}"`), nicht
  nur Dateien+LoC wie im Plan-Beispieltext: `roadmap.md` nennt für `code_size` explizit
  "Dateien/LoC/Bytes", der `FileMetric`-Record im Plan hatte bereits ein `Bytes`-Feld — Display-Zeile
  entsprechend vervollständigt statt das Feld ungenutzt zu lassen.

## Beobachtungen

- **`FileStructureToolRegistrations` überschreitet nach dieser Änderung knapp das
  `AIContextFootprint`-Limit:** `get_violations` meldet eine `AIContextFootprint`-**Warnung**
  (nicht Fehler, baut nicht rot): `FileStructureToolRegistrations (2894 > 2890)`, Top-Treiber laut
  Tool-Hinweis sind `GlobalConfigOverride`/`MetricsConfigOverride`/`TestSentinelConfigOverride`
  (bereits vor diesem Step vorhandene Config-Ketten über `McpCodeGraphServer`, nicht die neuen
  `MetricsTree*`-Typen selbst) — die neue `metrics_tree`-Registrierung war offenbar der letzte
  Tropfen über eine bereits knapp am Limit liegende Klasse. Der Plan hatte genau dieses Risiko in
  „Aktueller Projektzustand" Punkt 6 vermerkt, aber keine Gegenmaßnahme (Facade o. ä.) vorgesehen.
  Baut aktuell nicht rot (Warnung, kein `TreatWarningsAsErrors`-Compiler-Diagnostic), aber der
  Kritiker sollte entscheiden, ob das einen Tech-Debt-Eintrag oder eine Facade-Extraktion in einem
  Folge-Step rechtfertigt.
- **`root`-Präfix-Filterung ist bewusst simpel (Design-Entscheidung im Plan):** `root="src/Foo"`
  matched fälschlich auch `src/FooBar/...`, weil die Filterung ein reiner String-Präfix-Vergleich
  ist (kein Pfadsegment-Grenzcheck). Das war im Plan explizit so vorgegeben ("Filterung erfolgt als
  Pfad-Präfix"), 1:1 umgesetzt, nicht selbst entdeckt — hier nur zur Sichtbarkeit nochmal genannt,
  falls ein Agent das beim Nutzen von `metrics_tree` überrascht.

## Bekannte Unschärfen

- **Sortierreihenfolge unter Gleichstand (identischer `SortValue`) ist nicht deterministisch
  spezifiziert:** `MetricsTreeRenderer.Render` nutzt `OrderBy`/`OrderByDescending` ohne
  Sekundärkriterium — bei exakt gleichem `SortValue` (z. B. mehrere Dateien mit 0 Kommentarzeilen
  bei `comment_density`) hängt die Reihenfolge von der (stabilen, aber nicht alphabetisch
  sortierten) Dictionary-Iterationsreihenfolge in `MetricsTreeScanner.GroupByNextSegment` ab. Die
  Tests dieses Steps umgehen das bewusst, indem sie nur Fälle mit eindeutig unterschiedlichen
  Werten prüfen (`ExecuteAsync_CommentDensityMode_ReturnsTreeSortedByRatioAscending` vergleicht
  `Greeter.cs` (Ratio 0) gegen `ViolationTrigger.cs` (Ratio > 0) statt zweier Ratio-0-Dateien
  untereinander). Kein Bug gegen die Plan-Vorgabe (die spezifiziert kein Tie-Breaking), aber ein
  Punkt, den der Kritiker im Hinterkopf behalten sollte, falls spätere Modi (EPIC-02) hier
  Determinismus erwarten.
- **`CountCommentLines`-Heuristik behandelt Zeilen mit Code+Trailing-`//`-Kommentar
  (`int x = 1; // foo`) als reine Code-Zeile**, nicht als teilweise Kommentar — Designentscheidung
  aus dem Plan ("kein vollständiger C#-Tokenizer"), hier nur als Verhaltensdetail dokumentiert, das
  bei Testdaten mit vielen Inline-Kommentaren zu einer niedrigeren Kommentar-Ratio führt als eine
  Token-genaue Zählung liefern würde.
