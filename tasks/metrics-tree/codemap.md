---
task: metrics-tree
type: codemap
maintained_by: planer, coder, kritiker
last_updated: 2026-08-08 (Step 003)
---

# CodeMap: metrics-tree

Task-scoped Landkarte — existiert nur für diesen Task, wird mit
`<task-dir>` gelöscht, kein projektweites Artefakt. Enthält **nur**, was
für diesen Task relevant ist (Module/Dateien/Bereiche, die ein Step
tatsächlich berührt hat oder für die Planung des nächsten Steps
gebraucht wird) — kein Anspruch auf vollständige Projektabdeckung.

**Pointer-Prinzip — wie Regel-Index (`roadmap.md`) und Tech-Debt-Index
(`tech-debt.md`):** Jeder Eintrag ist Ort + **ein Satz**, was dort ist
und wozu es für diesen Task relevant ist — keine Verhaltensbeschreibung,
kein „wie funktioniert das im Detail". Verhaltensbehauptungen veralten,
Ortsangaben kaum. Wer mehr wissen muss, liest die Datei selbst nach —
das ersetzt die Map nie, sie beschleunigt nur das Finden.

**Warum das trotzdem verlässlich bleibt (anders als generische Doku):**
Der gesamte Loop läuft strikt seriell — genau ein Subagent gleichzeitig
(`../spec.md` §6). Zwischen einem Coder-Update und dem nächsten Lesezugriff
kann sich am Code strukturell nichts geändert haben, was hier nicht auch
eingetragen wurde. Die Map ist also, solange sie gepflegt wird, tatsächlich
aktuell — kein Snapshot mit Drift-Risiko. **Schritt 2 im Step-Modus des
Planers („tatsächlichen Projektzustand lesen", `../spec.md` §7.2) bleibt
trotzdem Pflicht** — die Map sagt *wo* nachschauen, ersetzt nie das
Nachschauen selbst.

## Pflege — wer trägt wann ein

- **Planer, Roadmap-Modus (einmalig):** befüllt die Map initial aus dem
  Grobüberblick, den er beim Ableiten der Epics ohnehin über den
  Bestandscode gewinnt (`../skills/planer/SKILL.md` Roadmap-Modus
  Schritt 1).
- **Coder (jeder Step):** ergänzt/aktualisiert Einträge für tatsächlich
  angelegte oder geänderte Module, **vor** dem Doku-Commit
  (`../skills/coder/SKILL.md` Schritt 6a).
- **Planer, Step-Modus (jeder Step):** liest die Map vor dem Planen,
  ergänzt neue Bereiche, die er beim Lesen des Ist-Zustands entdeckt.
  Zusätzlich Grundlage für den Anti-Loop-Check (siehe unten).
- **Kritiker:** prüft stichprobenartig, ob die Map dem tatsächlichen Diff
  entspricht (Teil von Ebene 1, Plan-Erfüllung) — schreibt selbst nur bei
  offensichtlicher Lücke/Fehler nach, ist aber nicht Haupt-Pfleger.

## Anti-Loop-Nutzen

Bevor der Planer im Step-Modus einen neuen Step plant, gleicht er sein
Vorhaben gegen die hier verzeichneten, bereits getroffenen Entscheidungen
ab. Widerspricht der neue Plan erkennbar einem hier festgehaltenen,
bereits umgesetzten Stand (z. B. Step-234 würde zurückdrehen, was Step-123
laut Map bewusst so gebaut hat): entweder im neuen Step-Plan explizit als
Erweiterung begründen, oder den alten Eintrag hier als „obsolet —
<Grund>" markieren (nicht löschen) — nie stillschweigend widersprechen.
Das verhindert kein Kreisen zu 100 %, macht ein Hin-und-Her aber
wenigstens sichtbar und begründungspflichtig statt stillschweigend.

## Karte

- **`src/AiNetLinter/Mcp/FileStructureToolRegistrations.cs`** —
  Registrierungspunkt der dateistruktur-orientierten Tools
  (`get_file_skeleton`, `get_index_scope`, `get_hotspots`). **Seit Step
  003:** `metrics_tree` ist hier **nicht mehr** registriert — nach
  `AnalysisToolRegistrations` verschoben, weil die zwei neuen Roslyn-Modi
  (`violation_density`/`complexity`) denselben `LinterEngine`-Pull-in
  haben wie `get_violations` (identischer Grund, aus dem `get_violations`
  dort statt hier registriert ist). Footprint dieser Klasse dadurch
  entlastet statt weiter erhoeht.
- **`src/AiNetLinter/Mcp/AnalysisToolRegistrations.cs`** — Registrierungspunkt
  der analyse-orientierten Tools (`get_violations`, `safeguard`,
  `search_pattern`). **Seit Step 003:** `metrics_tree` ist hierher
  gewandert (`AddMetricsTree`, Body 1:1 aus `FileStructureToolRegistrations`
  uebernommen). Tatsaechlicher Footprint stieg dadurch auf 2905 (vorheriges
  `PathOverride` 2870) — `PathOverride` in `rules.json` auf 2910 angehoben
  (etablierte Projekt-Konvention, identisches Muster zu den 10+
  bestehenden `PathOverrides`-Eintraegen; TD-001-relevant, siehe
  `step-003/step-result.md`).
- **`rules.json` → `PathOverrides`** (Step 003) — zwei Eintraege
  angepasst/ergaenzt: `AnalysisToolRegistrations.cs` 2870→2910,
  neu `Mcp/Tools/MetricsTree/MetricsTreeTool.cs` 2910 (vorher kein
  Override, Default 2500, tatsaechlicher Footprint 2897).
- **`src/AiNetLinter/Mcp/Tools/SolutionFileWalker.cs`** (Step 001, neu) —
  generalisierter Datei-Walk-Kern (`CollectFiles`/`MatchesScope`/
  `TryReadAllLines`), extrahiert aus `GetHotspotsScanner`. Gemeinsame
  Datenquelle für `GetHotspotsScanner` UND `MetricsTreeScanner` — keine
  zweite unabhängige Walk-Implementierung mehr im Projekt. Zusätzlich zum
  ursprünglichen `scopeFilter` jetzt ein optionaler Regex-`fileFilter` auf
  den relativen Pfad.
- **`src/AiNetLinter/Mcp/Tools/WalkedFile.cs`** (Step 002, neu, TD-002) —
  `internal readonly record struct WalkedFile(RelativePath, AbsolutePath)`,
  aus `SolutionFileWalker.cs` auf Namespace-Ebene extrahiert (war
  `internal` genestet, verletzte `BanPublicNestedTypes`); rein mechanische
  Verschiebung, keine Verhaltensänderung.
- **`src/AiNetLinter/Mcp/Tools/GetHotspotsTool.cs` +
  `GetHotspotsScanner.cs`** — Referenz-Pattern für Tool/Scanner-Split:
  Tool ist dünner Dispatch (Loading-/Solution-Checks + Aufruf), Scanner
  trägt die eigentliche Walk-/Formatierungslogik, keine Abhängigkeit auf
  `McpCodeGraphServer` (direkt unit-testbar). `GetHotspotsScanner` nutzt
  seit Step 001 `SolutionFileWalker` statt eigener `CollectFiles`/
  `MatchesScope`/`TryCountLines` (Verhalten unverändert, Regression über
  bestehende `GetHotspotsToolTests` abgesichert).
- **`src/AiNetLinter/Mcp/Tools/MetricsTree/MetricsTreeTool.cs` +
  `MetricsTreeScanner.cs` + `MetricsTreeRoslynScanner.cs` +
  `MetricsTreeRenderer.cs` + `MetricsTreeMode.cs`** (Step 003: aus
  `src/AiNetLinter/Mcp/Tools/` in eigenes Unterverzeichnis `MetricsTree/`
  verschoben, Namespace entsprechend auf `AiNetLinter.Mcp.Tools.MetricsTree`
  umgestellt — Grund: `MaxDirectoryChildren` (30) war in `Mcp/Tools/` durch
  die neue `MetricsTreeRoslynScanner.cs` auf 32 Eintraege gestiegen
  (31 bereits vor Step 003 vorhanden, siehe „Beobachtungen" in
  `step-003/step-result.md`); `EnforceNamespaceDirectoryMapping` erzwingt
  den Namespace-Wechsel bei einer Verzeichnis-Verschiebung. Referenzstellen
  (`AnalysisToolRegistrations.cs`, `MetricsTreeToolTests.cs`,
  `MetricsTreeRendererTests.cs`, `MetricsTreeRoslynScannerTests.cs`) haben
  je ein zusaetzliches `using AiNetLinter.Mcp.Tools.MetricsTree;` bekommen
  — Tests selbst liegen weiterhin unter `src/AiNetLinter.Tests/Mcp/Tools/`
  (dort noch Platz, keine Verschiebung noetig).) — MCP-Tool
  `metrics_tree`, jetzt alle 4 Konzept-Modi fertig: Tool = dünner Dispatch
  (Validierung `mode`/`depth`/`top_n`/`file_filter`, analog
  `FindSymbolTool`) + Modus-Dispatch (`code_size`/`comment_density`
  synchron über `MetricsTreeScanner`, `violation_density`/`complexity`
  async über `MetricsTreeRoslynScanner`, inkl. `state.GetConfigSnapshot()`
  nur fuer den Roslyn-Pfad); Renderer = modus-agnostischer ASCII-Tree-
  Formatierer über `MetricsTreeNode` (unveraendert seit Step 001, wie
  vorgesehen ohne Aenderung fuer EPIC-02 wiederverwendet); Mode = Enum +
  Parser, jetzt alle 4 Modi (`CodeSize`/`CommentDensity`/
  `ViolationDensity`/`Complexity`).
  **Seit Step 003:** `FileMetric`/`BuilderNode` sind aus
  `MetricsTreeScanner` als Namespace-Ebene-Records (nicht mehr `private
  nested`) nach `MetricsTreeScanner.cs` (Datei-Top-Level, gleiche Datei)
  verschoben und `internal`, ebenso der Aggregations-Kern
  (`BuildNode`/`ToMetricsTreeNode`/`NormalizeRoot`/`ComputeRootName`,
  vorher `private`) — `MetricsTreeRoslynScanner.cs` baut fuer
  `violation_density`/`complexity` dieselben `FileMetric`-Listen und ruft
  denselben Aggregations-Kern, keine zweite Baum-Implementierung.
  **Wichtig (Anti-Loop):** `FileMetric`/`BuilderNode` sind bewusst
  **top-level**, nicht als `internal nested` Typen in der Klasse
  belassen — `BanPublicNestedTypes` (siehe `AiNetLinter.mdc`) verbietet
  auch `internal nested` Typen (`BanPublicNestedTypesAllowPrivate`
  erlaubt nur `private nested`), das waere sonst ein Error-Verstoss.
  `MetricsTreeScanner.BuildNode` traegt eine `ainetlinter-disable
  MaxMethodParameterCount`-Suppression (5 Parameter; die relaxierte
  Nicht-Public-Grenze greift nicht, weil die Methode `internal` statt
  `private`/`protected` sein muss, damit `MetricsTreeRoslynScanner` sie
  aufrufen kann).
  **Seit Step 002:** `MetricsTreeScanner.BuildTree` nimmt statt 6
  Einzelparametern einen `MetricsTreeQuery`-Record (validierte Werte,
  auf Namespace-Ebene in `MetricsTreeScanner.cs`); `MetricsTreeTool.
  ExecuteAsync` analog einen `MetricsTreeToolArgs`-Record (rohe,
  ungeparste Werte, auf Namespace-Ebene in `MetricsTreeTool.cs`).
- **`src/AiNetLinter/Mcp/McpDrillDownHints.cs`** (Step 001, neu) —
  Gegenstück zu `McpSufficiencyHints`: Hinweistext für
  `metrics_tree`-Output, der per Definition nie vollständig ist (immer
  Top-N, nie alle Kinder).
- **`src/AiNetLinter/Mcp/Tools/GetViolationsScanner.cs` +
  `GetViolationsTool.cs`** — Referenz-Pattern, das `MetricsTreeRoslynScanner`
  für `violation_density` uebernommen hat: `LinterEngine.RunAsync(...,
  noCache: true, ...)` einmal aufrufen, `RuleViolation`s nach `FilePath`
  gruppieren, Severity über `EffectiveSeverity ?? RuleRegistry.TryResolve`.
- **`src/AiNetLinter/Metrics/ComplexityCalculator.cs`** — statische
  `GetCyclomaticComplexity`/`GetCognitiveComplexity` pro
  `MethodDeclarationSyntax`; Quelle für Ø CC/max CC/max CogC im
  `complexity`-Modus (Step 003). Reine Berechnungslogik ohne
  Solution-/Tool-Abhängigkeit, genutzt von `MetricsTreeRoslynScanner`
  (Muster fuer die Methoden-Enumeration: `SafeguardScanner.cs`
  `BuildScannedClass`, `root.DescendantNodes().OfType<MethodDeclarationSyntax>()`
  nach `document.GetSyntaxRootAsync`).
- **`src/AiNetLinter/Commands/MapCommand.cs`** + `src/AiNetLinter/Maps/**`
  (`HotspotMapBuilder`, `StructureMapBuilder`, `VocabularyMapBuilder`,
  `Skeleton/SkeletonMapBuilder`) — bestehende CLI-`--map`-Subcommands,
  bleiben laut `konzept.md`/Decision D6 unverändert; nur als
  Referenz-Aggregationslogik relevant, `metrics_tree` ist eigenständige
  MCP-Variante, kein Aufruf/Ersatz dieser Klassen.
- **`src/AiNetLinter/Mcp/McpSufficiencyHints.cs`** — gemeinsamer
  Hinweistext-Baustein für „Daten vollständig, kein weiteres Read/Grep
  nötig"; `metrics_tree` braucht das Gegenstück (Drill-down-Hinweis,
  „das ist Ebene 1, für Details tieferer `root`/`depth`") — eigener,
  neuer Hinweistyp, kein Wiederverwendung von `CompleteDataHint` selbst,
  da `metrics_tree`-Output per Definition nie „vollständig" im Sinne
  dieser Klasse ist (immer Top-N, nie alle Kinder).
- **`.agents/rules/AiNetLinter.mdc`** — `AIContextFootprint`-Limit
  (2500 transitive Zeilen) ist der wiederkehrende Grund für die
  Tool/Scanner/Renderer-Aufsplittung in diesem Bereich des Codes —
  bei der Größe des Renderers (gemeinsam für 4 Modi) im Auge behalten.
