---
status: done
type: step-plan
task: metrics-tree
step: 003
corrects: null
title: "Roslyn-Modi violation_density/complexity + Doku-Updates + Roadmap-Abschluss (EPIC-02)"
epic: EPIC-02
estimated_risk: medium
step_type: single
items: []
created_by: planer
created_by_model: claude-sonnet-5
created_by_model_knowledge_cutoff: 2026-01
created_at: 2026-08-08
related_to: [step-001, step-002]
---

# Step 003: Roslyn-Modi violation_density/complexity + Doku-Updates + Roadmap-Abschluss

## Bezug

- **Task:** `metrics-tree`
- **Epic:** `EPIC-02` aus `roadmap.md` — komplett offen, erster (und laut User-Vorgabe idealerweise
  einziger) Step dieses Epics: Roslyn-Modi `violation_density`/`complexity` auf dem in EPIC-01
  gebauten ASCII-Tree-Renderer, Tests, Doku-Updates, Abhaken von Epic S2.5 in
  `tasks/features/05-roadmap.md`.
- **Konzept-Referenz:** `konzept.md` Muss-Haben (4 Modi, `violation_density`/`complexity` über
  `LinterEngine`/`ComplexityCalculator`) + „Hinweis zur Umsetzungsgranularitaet" Block 2 +
  „Definition of Done".

## Aktueller Projektzustand (JIT-Kontext)

- `MetricsTreeMode.cs`: Enum mit aktuell nur `CodeSize`/`CommentDensity` + `TryParse`-Switch;
  Doku-Kommentar sagt explizit „kein Platzhalter für die Roslyn-Modi" — muss beim Erweitern
  mit angepasst werden.
- `MetricsTreeScanner.cs` (238 Zeilen): `BuildTree(Solution, MetricsTreeQuery)` ist synchron, walkt
  über `SolutionFileWalker.CollectFiles` + private `FileMetric`(RelativePath, CommentLines,
  CodeLines, Bytes)/`BuilderNode`(Name, RelativePath, FileCount, CommentLines, CodeLines, Bytes,
  Children) + `BuildNode`/`GroupByNextSegment`/`CombinePath`/`GetRemainder`/`AggregateLeaf`/
  `AggregateWithChildren`/`ToMetricsTreeNode`/`ComputeSortValue`/`FormatDisplayLine` — reine
  Additions-Aggregation (`Sum`), kein Modus braucht bisher `Max`-Aggregation. `MetricsTreeQuery`
  bündelt `Root`/`Mode`/`Depth`/`TopN`/`FileFilter` (seit step-002, wegen
  `MaxMethodParameterCount`).
- `MetricsTreeRenderer.cs`: bereits vollständig modus-agnostisch (`MetricsTreeNode` mit
  vorformatiertem `DisplayLine` + `SortValue` + `sortDescending`-Flag) — laut eigenem Doku-Kommentar
  „von EPIC-02s Roslyn-Modi ohne Änderung wiederverwendbar". Keine Änderung an dieser Datei
  vorgesehen.
- `MetricsTreeTool.cs`: dünner Dispatch, `MetricsTreeToolArgs` (rohe Werte, seit step-002) →
  Validierung → `MetricsTreeQuery` → `MetricsTreeScanner.BuildTree` (synchron aufgerufen aus
  async `ExecuteAsync`). Registriert in `FileStructureToolRegistrations.AddMetricsTree`
  (Delegate-Closure auf `McpCodeGraphServer`, wie alle Tools dort).
- **TD-001 (offen, mittel):** `get_violations` meldet `AIContextFootprint`-Warnungen auf
  `FileStructureToolRegistrations.cs` (2895>2890, eigenes `PathOverride` in `rules.json`) UND
  `MetricsTreeTool.cs` (2536>2500, **kein** `PathOverride` vorhanden) — beide über dieselbe
  Config-Override-Kette (`GlobalConfigOverride`/`MetricsConfigOverride`/
  `TestSentinelConfigOverride`, je 354 Zeilen) transitiv über `McpCodeGraphServer`. Selbst
  verifiziert per `get_violations` (aktueller Stand, s.u.). TD-001 selbst (Facade-Extraktion) ist
  **nicht** Teil dieses Steps (Architektur-Ermessen, User-Sache) — aber die Tech-Debt-Notiz
  benennt explizit, dass EPIC-02 den Footprint durch den zusätzlichen `LinterEngine`-Pull-in
  weiter erhöhen könnte, das wird unten aktiv gegengesteuert (Registrierung verschieben, siehe
  Änderung 5).
- **Referenzmuster `violation_density`:** `GetViolationsScanner.cs`/`GetViolationsTool.cs` —
  `LinterEngine.RunAsync(solution, noCache: true, cacheTtlMinutes: 0, ct)` einmal aufrufen,
  `RuleViolation`s nach `FilePath` filtern/aggregieren, Severity über
  `EffectiveSeverity ?? RuleRegistry.TryResolve(...).Severity`. Parameter-Bündelung als eigener
  Record (`GetViolationsScannerParameters`) wegen `MaxMethodParameterCount`. Registriert in
  `AnalysisToolRegistrations` statt `FileStructureToolRegistrations` — **explizit weil** der
  `LinterEngine`-Pull-in den Footprint von `FileStructureToolRegistrations`'
  Vorgänger-Zustand über das Limit getrieben hat (Doku-Kommentar in `AnalysisToolRegistrations.cs`
  Zeile 14-19). Identische Logik gilt jetzt für `metrics_tree`.
- **Referenzmuster `complexity`:** `ComplexityCalculator.GetCyclomaticComplexity`/
  `GetCognitiveComplexity(MethodDeclarationSyntax)` — reine, statische, Solution-unabhängige
  Berechnung. Enumerationsmuster für „alle Methoden einer Datei" bereits zweimal im Projekt
  etabliert: `SafeguardScanner.cs:454-461` (`classDecl.DescendantNodes().OfType<MethodDeclarationSyntax>()`
  nach `document.GetSyntaxTreeAsync(ct)` + `syntaxTree.GetRootAsync(ct)`) und
  `LinterAnalyzer.cs:325`. Kein neues Muster nötig, nur anwenden.
- Kein `rules.json`-`PathOverride` für `MetricsTreeScanner.cs`/die neue Roslyn-Scanner-Datei
  vorhanden — beide laufen (noch) unter dem Default-Limit 2500.
- **Vorgefundene Lücke (bei diesem Schritt 2 entdeckt, nicht Teil von EPIC-01/02-Scope
  ursprünglich, aber Definition-of-Done-relevant):**
  `McpServerCommandTests.RunAsync_ValidFixture_ServerRespondsWithThirteenTools` (Category
  `Integration`) zählt weiterhin **13** Tools und listet `metrics_tree` **nicht** — seit EPIC-01
  veraltet, aber nicht aufgefallen, weil Integration-Tests nicht Teil des für diesen Task
  reduzierten Test-Gates sind (`roadmap.md` Tech-Stack-Notiz: nur gezielte Läufe während der
  Iteration). `OverviewResourceRegistrationTests.BuildOverviewText_ListsAllFourteenTools` ist
  dagegen bereits korrekt (14). Diese Lücke würde den einen verpflichtenden Abschluss-Volllauf
  am Ende des gesamten Tasks (`dotnet test --filter Category!=Stress`) zum Scheitern bringen —
  daher unten als mechanische Korrektur mit aufgenommen (siehe Änderung 6), nicht als
  eigenständiger Tech-Debt-Eintrag, weil sie das eigene Definition-of-Done dieses Tasks direkt
  betrifft und trivial/eindeutig ist (kein Architektur-Ermessen).
- `Docs/agent-api.md`/`README.md` enthalten aktuell **noch keine** Erwähnung von `metrics_tree`
  (verifiziert per Grep) — konsistent mit der `konzept.md`-Entscheidung, Doku-Updates erst am
  Ende von EPIC-02 zu machen, nicht schon nach EPIC-01.
- `tasks/features/05-roadmap.md` S2.5-Detailabschnitt (Zeile 211ff.) beschreibt noch **5** Modi
  inkl. `method_count` (Zeilen 244, 257, 272, 277) — das ist der Stand vor der
  `konzept.md`-Entscheidung, `method_count` bewusst wegzulassen (siehe dort „Verworfene
  Alternativen"). Beim Abhaken muss das benannt werden, nicht stillschweigend als „erfüllt"
  markiert werden.

## Intention

Nach diesem Step deckt `metrics_tree` alle 4 im Konzept vorgesehenen Modi ab: die zwei neuen
Roslyn-Modi nutzen denselben Renderer und denselben Aggregations-Kern wie die Datei-Modi (durch
Erweiterung der bestehenden `FileMetric`/`BuilderNode`-Typen um zusätzliche, teils
Max-statt-Sum-aggregierbare Felder statt einer zweiten unabhängigen Baum-Implementierung). Die
Tool-Registrierung wandert nach `AnalysisToolRegistrations`, weil `metrics_tree` jetzt denselben
`LinterEngine`-Pull-in hat wie `get_violations` — das ist derselbe Grund, aus dem
`get_violations` dort und nicht in `FileStructureToolRegistrations` registriert ist, und
entschärft (nicht behebt) den in TD-001 dokumentierten Footprint-Druck auf
`FileStructureToolRegistrations`. Der Task endet danach fachlich vollständig: Doku und
`tasks/features/05-roadmap.md` werden aktualisiert, das Epic in `roadmap.md` wird abgehakt.

## Konkrete Änderungen

### Datei 1: `src/AiNetLinter/Mcp/Tools/MetricsTreeMode.cs`

- **Was:** `MetricsTreeMode`-Enum um `ViolationDensity`, `Complexity` erweitern;
  `MetricsTreeModeParser.TryParse` um `"violation_density"`/`"complexity"` erweitern; XML-Doc der
  Enum (die aktuell explizit sagt „kein Platzhalter für ... EPIC-02") auf den fertigen Stand
  aktualisieren.
- **Warum:** Muss-Haben aus `konzept.md`.

### Datei 2: `src/AiNetLinter/Mcp/Tools/MetricsTreeScanner.cs`

- **Was:** `FileMetric` und `BuilderNode` um die für die zwei neuen Modi nötigen Felder
  erweitern (z. B. `ViolationCount`/`ErrorCount`/`WarningCount` für `violation_density`,
  `MethodCount`/`SumCyclomatic`/`MaxCyclomatic`/`MaxCognitive` für `complexity`) — bestehende
  zwei Datei-Modi liefern für die neuen Felder einfach `0`. `AggregateLeaf`/
  `AggregateWithChildren` entsprechend erweitern: die meisten Felder bleiben `Sum`, `MaxCyclomatic`/
  `MaxCognitive` werden `Math.Max` über Kinder/Dateien aggregiert (nicht summiert). `ComputeSortValue`/
  `FormatDisplayLine` um die zwei neuen Modi erweitern (Sortierkriterium/Anzeige siehe „Code-Skizze"
  unten). Sichtbarkeit der bisher `private` Bau-Helfer (`BuildNode`, `AggregateLeaf`,
  `AggregateWithChildren`, `GroupByNextSegment`, `CombinePath`, `GetRemainder`, `ToMetricsTreeNode`,
  ggf. `BuilderNode`/`FileMetric` selbst) von `private` auf `internal` anheben, damit Datei 3 sie
  wiederverwenden kann, statt eine zweite Baum-Aggregation zu bauen.
- **Warum:** Wiederverwendung der bereits vorhandenen, getesteten Aggregations-Logik statt
  Duplikation — direkte Anwendung desselben Prinzips, das EPIC-01 schon für den Datei-Walk
  (`SolutionFileWalker`) etabliert hat.

### Datei 3 (neu): `src/AiNetLinter/Mcp/Tools/MetricsTreeRoslynScanner.cs`

- **Was:** Neue Datei mit `internal sealed record MetricsTreeRoslynScanParameters(Solution
  Solution, ILinterEngineConfig Config, ILintConsole Console, CancellationToken
  CancellationToken)` (analog `GetViolationsScannerParameters`, wegen `MaxMethodParameterCount`)
  und `internal static class MetricsTreeRoslynScanner` mit
  `internal static async Task<string> BuildTreeAsync(MetricsTreeRoslynScanParameters scan,
  MetricsTreeQuery query)`. Für `violation_density`: `LinterEngine.RunAsync(scan.Solution,
  noCache: true, cacheTtlMinutes: 0, scan.CancellationToken)` einmal aufrufen (Konstruktion wie
  in `GetViolationsScanner.BuildViolationsTextAsync`), Violations nach `FilePath` gruppieren,
  pro Datei Total/Error/Warning-Counts bilden. Für `complexity`: über die per
  `SolutionFileWalker.CollectFiles` gescopten `.cs`-Dateien iterieren, je Datei das zugehörige
  `Document` auflösen (Map `FilePath → Document`, analog `GetViolationsScanner.
  BuildFileToProjectMap`, aber auf `Document` statt Projekt-Name), `await
  document.GetSyntaxRootAsync(ct)`, `root.DescendantNodes().OfType<MethodDeclarationSyntax>()`
  (Muster: `SafeguardScanner.cs:458`), pro Methode `ComplexityCalculator.
  GetCyclomaticComplexity`/`GetCognitiveComplexity` aufrufen und aggregieren. Beide Pfade bauen
  aus den pro-Datei-Werten dieselben `FileMetric`/`BuilderNode`-Typen aus Datei 2 und rufen
  dessen (jetzt `internal`) `BuildNode`/`ToMetricsTreeNode` auf, dann
  `MetricsTreeRenderer.Render` (unverändert).
- **Warum:** Getrennt von `MetricsTreeScanner.cs`, damit diese Datei (mit dem neuen
  `LinterEngine`/Roslyn-Syntax-Pull-in) nicht zusätzlich zur ohnehin schon knappen
  `MaxLineCount`(500)-Reserve der Datei-Walk-Datei beiträgt und der reine, dependency-arme
  Datei-Walk-Pfad (`code_size`/`comment_density`, weiterhin synchron, kein `Solution`-Overhead)
  unangetastet bleibt — analoges Splitting-Prinzip wie `GetHotspotsScanner`/`GetViolationsScanner`
  als zwei getrennte Scanner-Dateien für zwei getrennte Datenquellen.

### Datei 4: `src/AiNetLinter/Mcp/Tools/MetricsTreeTool.cs`

- **Was:** In `ExecuteAsync` nach dem `TryParse`/Validierungsblock auf `parsedMode.Value`
  dispatchen: `CodeSize`/`CommentDensity` → weiterhin `MetricsTreeScanner.BuildTree(solution,
  query)` (synchron); `ViolationDensity`/`Complexity` → `await
  MetricsTreeRoslynScanner.BuildTreeAsync(new MetricsTreeRoslynScanParameters(solution,
  configSnapshot.Config, state.Console, ct), query)`, dafür `var configSnapshot =
  state.GetConfigSnapshot();` ergänzen (identischer Call wie in `GetViolationsTool.
  ExecuteAsync`). `MetricsTreeDescription`-Konstante aktualisieren: alle vier Modi nennen, „(weitere
  Modi folgen)" entfernen.
- **Warum:** Muss-Haben; Config/Console werden nur für den Roslyn-Pfad gebraucht, kein
  unnötiger Overhead für die Datei-Modi.

### Datei 5a: `src/AiNetLinter/Mcp/FileStructureToolRegistrations.cs`

- **Was:** `AddMetricsTree`-Methode + ihren Aufruf in `Register()` + die `MetricsTreeDescription`-
  Konstante entfernen (wandern nach Datei 5b); Klassen-Doku-Kommentar anpassen (nicht mehr
  „`get_file_skeleton`, `get_index_scope`, `get_hotspots`, `metrics_tree`" listen, sondern nur die
  drei verbleibenden; der bestehende Verweis auf `AnalysisToolRegistrations` als
  `LinterEngine`-Auslagerungsziel bleibt sinngemäß korrekt, kann leicht ergänzt werden).
- **Warum:** Siehe Intention — mitigiert TD-001-Footprint-Druck auf dieser Klasse, statt ihn
  durch den neuen `LinterEngine`-Pull-in weiter zu erhöhen.

### Datei 5b: `src/AiNetLinter/Mcp/AnalysisToolRegistrations.cs`

- **Was:** `AddMetricsTree` (Body 1:1 aus Datei 5a übernehmen) + `MetricsTreeDescription`-
  Konstante + Aufruf in `Register()` ergänzen; Klassen-Doku-Kommentar erweitern (analog zur
  bestehenden Begründung für `get_violations`/`search_pattern`, jetzt auch `metrics_tree`
  nennen).
- **Warum:** Siehe Intention/Datei 5a.

### Datei 6: `src/AiNetLinter.Tests/Commands/McpServerCommandTests.cs`

- **Was:** `RunAsync_ValidFixture_ServerRespondsWithThirteenTools` → umbenennen auf
  `...FourteenTools`, `Assert.Equal(13, tools.Count)` → `Assert.Equal(14, tools.Count)`,
  `Assert.Contains(tools, t => t.Name == "metrics_tree")` ergänzen.
- **Warum:** Siehe „Aktueller Projektzustand" — mechanische Korrektur einer seit EPIC-01
  veralteten Zählung, ohne die der verpflichtende Abschluss-Volllauf am Ende des Tasks fehlschlägt.
  Kein Architektur-Ermessen, direkt am Tool das dieser Step ändert.

### Datei 7: `Docs/agent-api.md`

- **Was:** Abschnitt „Die 13 Tools" (Zeile 249ff.) → „Die 14 Tools", neue Tabellenzeile für
  `metrics_tree` mit Input (`root?`, `mode`, `depth?`, `top_n?`, `file_filter?`), Output
  (ASCII-Tree, alle 4 Modi kurz benennen), C#-only (nein — zwei Modi sind reiner Datei-Walk),
  Trunkierung (ja, Top-N pro Ebene). Bestehende Beschreibung gegen den tatsächlichen Code
  verifizieren (Prüfpflicht laut `AiNetLinterRichtlinien.mdc` §1), nicht aus der Erinnerung
  übernehmen.
- **Warum:** Update-Pflicht (`AiNetLinterRichtlinien.mdc` §4) + `konzept.md`-DoD.

### Datei 8: `README.md`

- **Was:** Tool-Tabelle (Zeile ~77-92) um `metrics_tree`-Zeile ergänzen (kurzer Zweck-Satz,
  analog bestehenden Zeilen).
- **Warum:** Update-Pflicht + `konzept.md`-DoD.

### Datei 9: `Docs/ROADMAP.md`

- **Was:** Kurzen, eigenständigen Eintrag für `metrics_tree` ergänzen — **nicht** in den
  Abschnitt „MCP-Codegraph-Server (EPIC-01..08)" einsortieren, das ist ein anderer Task
  (`tasks/codegraph-mcp-server`) mit eigener EPIC-Zählung. Platzierung/exakte Formulierung liegt
  beim Coder; knapp halten (analog dem Stil der bestehenden `[x]`-Einträge), sachlich ohne
  Wertung (`AiNetLinterRichtlinien.mdc` §1 Dokumentations-Objektivität), Verweis auf beide Epics
  (EPIC-01 Datei-Modi, EPIC-02 Roslyn-Modi).
- **Warum:** Update-Pflicht + `konzept.md`-DoD.

### Datei 10: `tasks/features/05-roadmap.md`

- **Was:** Zeile 103 (Übersichtstabelle) `[ ]` → `[x]`. Detail-Abschnitt ab Zeile 211: die
  Akzeptanzkriterien-Zeilen 272 („Tool `metrics_tree` mit 5 Modi") und 277 („5+ Unit-Tests (1 pro
  Mode ...)") um eine kurze Anmerkung ergänzen, dass **4** Modi geliefert wurden — `method_count`
  bewusst verworfen laut `tasks/metrics-tree/konzept.md` „Verworfene Alternativen" — dann erst
  `[x]` setzen. Restliche Akzeptanzkriterien (Zeile 271-280) einzeln gegen den tatsächlichen
  Stand prüfen und `[x]` setzen.
- **Warum:** Explizit im `konzept.md`-Muss-Haben und in `roadmap.md` EPIC-02 gefordert;
  Objektivitätsregel verbietet stillschweigendes Abhaken einer nicht 1:1 erfüllten Zeile.

### Datei 11: `tasks/metrics-tree/codemap.md`

- **Was:** Coder ergänzt (vor dem Doku-Commit, wie in `codemap.md` „Pflege" beschrieben): neue
  Datei `MetricsTreeRoslynScanner.cs`, erweiterte `FileMetric`/`BuilderNode`-Felder, verschobene
  `AddMetricsTree`-Registrierung (jetzt `AnalysisToolRegistrations`), aktuelle
  `AIContextFootprint`-Zahlen für `MetricsTreeTool.cs`/`FileStructureToolRegistrations.cs`
  (Vergleich vorher/nachher, für Kritiker/TD-001-Fortschreibung).
- **Warum:** Pointer-Pflege-Pflicht laut `codemap.md`.

## Tests

- [ ] `MetricsTreeToolTests` (oder neue `MetricsTreeRoslynScannerTests`): `violation_density` —
  Baum sortiert absteigend nach Violation-Count, gegen eine Fixture mit bekannten Verstößen
  (z. B. `SymbolGraphMini` falls dort Verstöße existieren, sonst eine gezielt gewählte
  Mini-Fixture mit absichtlichem Regelverstoß).
- [ ] `complexity` — Baum liefert Ø CC/max CC/max CogC, gegen eine Fixture mit einer Methode
  bekannt hoher Komplexität (verzweigungsreiche Methode) vs. einer trivialen — Sortierung
  verifizieren.
- [ ] Edge-Case: leeres `root` (keine Dateien) für beide neuen Modi — analog bestehendem „Keine
  Dateien unter root"-Pfad in `MetricsTreeScanner.BuildTree`.
- [ ] Edge-Case: `depth=5` für mindestens einen der zwei neuen Modi.
- [ ] Edge-Case: single-File-`root` für mindestens einen der zwei neuen Modi.
- [ ] Unbekannte/leere Solution ohne C#-Dateien im Scope für `complexity` (0 Methoden gefunden) —
  darf nicht crashen, sinnvolle „keine Daten"-Meldung.
- [ ] 1 Integrationstest auf dem Live-Repo (Dogfooding) — `McpLiveRepositoryTests`-Pattern
  (`McpTestClient`, siehe `AiNetLinterRichtlinien.mdc` §4 „MCP & Dogfood Testing" — **kein**
  ad-hoc Skript), ruft `metrics_tree` mit `mode=violation_density` und `mode=complexity` gegen
  die echte `AiNetLinter`-Solution auf; nur grobe Plausibilität prüfen (kein Fehler, erwartete
  Struktur im Text), keine exakten Zahlen-Asserts (instabil über Zeit, da sich Solution/Violations
  ändern).
- [ ] `McpServerCommandTests.RunAsync_ValidFixture_ServerRespondsWithFourteenTools` (Datei 6).
- [ ] Bestehende `MetricsTreeToolTests`/`MetricsTreeRendererTests`/`GetHotspotsToolTests`
  unverändert grün (Regression, gezielter Lauf reicht laut Test-Strategie).

## Definition of Done

- [ ] Alle „Konkrete Änderungen" (Dateien 1-11) umgesetzt
- [ ] `dotnet build` (Solution) grün, 0 Warnungen, 0 Fehler (Zero-Warning-Direktive)
- [ ] Gezielter Testlauf grün, z. B. `dotnet test --filter
      "FullyQualifiedName~MetricsTree|FullyQualifiedName~McpServerCommand"` — **kein** Volllauf
      für diesen Step (abweichendes Test-Gate laut `roadmap.md`/`konzept.md`); da dies der letzte
      Step im letzten Epic ist, im Anschluss an diesen Step **zusätzlich** der eine verpflichtende
      Abschluss-Volllauf `dotnet test --filter Category!=Stress` (Definition of Done des
      *gesamten Tasks*, nicht dieses einzelnen Step-DoD — siehe `konzept.md` Test-Strategie)
- [ ] `get_violations` (Scope `MetricsTree`, `AnalysisToolRegistrations`,
      `FileStructureToolRegistrations`) selbst geprüft: keine neuen `error`-Severity-Verstöße;
      `AIContextFootprint`-Warnungen dokumentiert (Vorher/Nachher-Zahlen) für TD-001-Fortschreibung
      durch den Kritiker
- [ ] Commit auf aktuellem Branch (Conventional Commit)
- [ ] `tasks/metrics-tree/codemap.md` aktualisiert (Datei 11)
- [ ] `step-003/step-result.md` geschrieben
- [ ] `status` in `step-plan.md` von `in_progress` auf `done (pending audit)` gesetzt

## Rules-Refs

- `.agents/rules/AiNetLinter.mdc` §„Grenzwerte" — `MaxMethodParameterCount` (4, Grund für die
  zwei neuen Parameter-Records), `AIContextFootprint` (2500 Default/PathOverrides, Grund für die
  Registrierungs-Verschiebung), `MaxLineCount` (500, Grund für die neue eigene Scanner-Datei),
  `MaxCyclomaticComplexity`/`MaxCognitiveComplexity` (12/15, für den neuen Code selbst
  einzuhalten — die aggregierende Dispatch-Logik in `BuildTreeAsync` nicht zu verzweigungsreich
  bauen), Kurz-Stil (`sealed`, `#nullable enable`, kein leeres `catch`).
- `.agents/rules/AiNetLinterRichtlinien.mdc` §1 „Dokumentations-Objektivität" (Doku-Änderungen
  gegen Code verifizieren, sachlich ohne Wertung; relevant für Dateien 7-10) und §4
  „Updates & Tests" (Update-Pflicht `Docs/ROADMAP.md`/`README.md`; MCP & Dogfood Testing nur
  über C#-Testinfrastruktur) und §5 „Qualitätsdrift-Prävention" (Kommentar-Sparsamkeit, keine
  Task-ID-Referenzen im Code).

## Bekannte Ausnahmen

- `complexity`-Modus deckt ausschließlich `MethodDeclarationSyntax` ab (keine Properties,
  Konstruktoren, lokalen Funktionen) — konsistent mit der bestehenden Signatur von
  `ComplexityCalculator.GetCyclomaticComplexity`/`GetCognitiveComplexity`, die im gesamten
  Projekt nirgendwo anders erweitert wurde. Kein Bug, kein Testlücken-Fund, sondern bewusste
  Scope-Grenze — im Code kurz als *Why*-Kommentar festhalten, damit der Kritiker das nicht als
  Lücke moniert.
- `violation_density` zählt Verstöße pro Datei ungewichtet nach Severity in der
  Sortierung (Summe Total, nicht z. B. „Fehler zählen doppelt") — analog zur bewusst einfachen
  `code_size`-Sortierung (LoC, keine gewichtete Formel). Falls der Kritiker eine gewichtete
  Sortierung erwartet: das ist eine bewusste Vereinfachung, keine vom Plan abweichende
  Fehlimplementierung.

## Code-Skizze (optional)

```csharp
// MetricsTreeScanner.cs — erweiterte FileMetric (Beispiel, Feldnamen final beim Coder)
private sealed record FileMetric(
    string RelativePath, int CommentLines, int CodeLines, long Bytes,
    int ViolationCount, int ErrorCount, int WarningCount,
    int MethodCount, int SumCyclomatic, int MaxCyclomatic, int MaxCognitive);

// AggregateWithChildren: die meisten Felder weiter Sum, aber:
MaxCyclomatic: children.Count == 0 ? 0 : children.Max(c => c.MaxCyclomatic),
MaxCognitive:  children.Count == 0 ? 0 : children.Max(c => c.MaxCognitive),

// ComputeSortValue / FormatDisplayLine, neue Zweige:
// violation_density: sortDescending = true, SortValue = ViolationCount
//   DisplayLine: "{fileCount} Dateien | {violationCount} Violations ({errorCount} Fehler, {warningCount} Warnungen)"
// complexity: sortDescending = true, SortValue = MethodCount == 0 ? 0 : (double)SumCyclomatic / MethodCount
//   DisplayLine: "{fileCount} Dateien | Ø CC {avgCc:F1} | max CC {maxCyclomatic} | max CogC {maxCognitive}"

// MetricsTreeRoslynScanner.cs — Einstiegspunkt
internal sealed record MetricsTreeRoslynScanParameters(
    Solution Solution, ILinterEngineConfig Config, ILintConsole Console, CancellationToken CancellationToken);

internal static class MetricsTreeRoslynScanner
{
    internal static async Task<string> BuildTreeAsync(MetricsTreeRoslynScanParameters scan, MetricsTreeQuery query)
    {
        // violation_density: LinterEngine.RunAsync einmal, nach FilePath gruppieren
        // complexity: pro gescopter .cs-Datei Document auflösen, GetSyntaxRootAsync,
        //             DescendantNodes().OfType<MethodDeclarationSyntax>(), ComplexityCalculator
        // beide: MetricsTreeScanner.BuildNode(...)/ToMetricsTreeNode(...) wiederverwenden (jetzt internal)
    }
}
```

## Notes

- **Warum ein Step trotz Umfang (User-Vorgabe „idealerweise ein Step pro Epic"):** Jeder
  einzelne Baustein hat ein direktes, bereits im Projekt vorhandenes und getestetes
  Referenzmuster (`GetViolationsScanner` für LinterEngine-Integration inkl.
  Parameter-Record-Bündelung, `SafeguardScanner`/`LinterAnalyzer` für die
  Methoden-Enumeration/`ComplexityCalculator`-Nutzung, `MetricsTreeRenderer` bereits
  modus-agnostisch fertig, `AnalysisToolRegistrations` bereits die Ziel-Registrierungsklasse mit
  passendem Footprint-Override). Das eigentliche Risiko liegt nicht in unbekanntem Terrain,
  sondern in der Menge an Dateien — daher `estimated_risk: medium` statt `low`, aber kein Grund
  für eine künstliche Aufteilung laut der SKILL.md-Heuristik. Wird der `AIContextFootprint` auf
  `MetricsTreeTool.cs` nach der Umsetzung so stark ansteigen, dass `get_violations` eine
  `error`-Severity (statt `warning`) meldet, ist das ein echter Blocker für den gefilterten
  Test-/Build-Gate dieses Steps — in dem Fall wäre eine Facade-Extraktion (TD-001-Vorschlag)
  doch innerhalb dieses Steps nötig; aktuell (Stand `rules.json`) ist `AIContextFootprint`
  überall nur `warning`, dieser Fall wird als unwahrscheinlich eingeschätzt, aber der Coder soll
  ihn per `get_violations` aktiv prüfen, bevor er den Step als fertig meldet.
- Die Registrierungs-Verschiebung (Datei 5a/5b) ändert **nichts** an Tool-Namen, -Verhalten oder
  -Anzahl aus Sicht des MCP-Clients — reine interne Umorganisation, kein Breaking Change.
- `MetricsTreeRenderer.cs` bewusst **nicht** in den „Konkrete Änderungen" aufgeführt — laut
  eigenem Doku-Kommentar und Verifikation beim Lesen des Ist-Zustands bereits vollständig
  modus-agnostisch, keine Änderung nötig oder vorgesehen.
