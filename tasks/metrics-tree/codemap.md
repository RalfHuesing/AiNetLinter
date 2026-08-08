---
task: metrics-tree
type: codemap
maintained_by: planer, coder, kritiker
last_updated: 2026-08-08
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
  (`get_file_skeleton`, `get_index_scope`, `get_hotspots`, `metrics_tree`
  — Step 001, EPIC-01 umgesetzt); `AddMetricsTree` reiht sich als viertes
  Tool ein (Delegate-Closure auf `McpCodeGraphServer`, kein DI-Container,
  identisches Muster zu `AddGetHotspots`). **Achtung:** `get_violations`
  meldet nach Step 001 eine `AIContextFootprint`-Warnung auf dieser Klasse
  (2894 > 2890, knapp über dem Limit) — Top-Treiber sind bereits vor
  Step 001 vorhandene Config-Ketten, nicht die neuen `MetricsTree*`-Typen
  selbst, siehe `step-001/step-result.md` „Beobachtungen". Baut nicht rot
  (Warnung), aber bei weiteren Tool-Ergänzungen hier im Auge behalten.
- **`src/AiNetLinter/Mcp/Tools/SolutionFileWalker.cs`** (Step 001, neu) —
  generalisierter Datei-Walk-Kern (`CollectFiles`/`MatchesScope`/
  `TryReadAllLines`), extrahiert aus `GetHotspotsScanner`. Gemeinsame
  Datenquelle für `GetHotspotsScanner` UND `MetricsTreeScanner` — keine
  zweite unabhängige Walk-Implementierung mehr im Projekt. Zusätzlich zum
  ursprünglichen `scopeFilter` jetzt ein optionaler Regex-`fileFilter` auf
  den relativen Pfad.
- **`src/AiNetLinter/Mcp/Tools/GetHotspotsTool.cs` +
  `GetHotspotsScanner.cs`** — Referenz-Pattern für Tool/Scanner-Split:
  Tool ist dünner Dispatch (Loading-/Solution-Checks + Aufruf), Scanner
  trägt die eigentliche Walk-/Formatierungslogik, keine Abhängigkeit auf
  `McpCodeGraphServer` (direkt unit-testbar). `GetHotspotsScanner` nutzt
  seit Step 001 `SolutionFileWalker` statt eigener `CollectFiles`/
  `MatchesScope`/`TryCountLines` (Verhalten unverändert, Regression über
  bestehende `GetHotspotsToolTests` abgesichert).
- **`src/AiNetLinter/Mcp/Tools/MetricsTreeTool.cs` +
  `MetricsTreeScanner.cs` + `MetricsTreeRenderer.cs` +
  `MetricsTreeMode.cs`** (Step 001, alle neu) — MCP-Tool `metrics_tree`:
  Tool = dünner Dispatch (Validierung `mode`/`depth`/`top_n`/
  `file_filter`, analog `FindSymbolTool`); Scanner = Walk über
  `SolutionFileWalker` + Verzeichnis-Aggregation bis `depth` für die
  Datei-Modi `code_size`/`comment_density`; Renderer = modus-agnostischer
  ASCII-Tree-Formatierer über `MetricsTreeNode` (kennt weder Solution
  noch Modus-Herkunft — von EPIC-02s Roslyn-Modi ohne Änderung
  wiederverwendbar); Mode = Enum + Parser, aktuell nur die zwei
  Datei-Modi, EPIC-02 erweitert um `violation_density`/`complexity`.
- **`src/AiNetLinter/Mcp/McpDrillDownHints.cs`** (Step 001, neu) —
  Gegenstück zu `McpSufficiencyHints`: Hinweistext für
  `metrics_tree`-Output, der per Definition nie vollständig ist (immer
  Top-N, nie alle Kinder).
- **`src/AiNetLinter/Mcp/Tools/GetViolationsScanner.cs` +
  `GetViolationsTool.cs`** — Referenz-Pattern für den
  `violation_density`-Modus (EPIC-02): ruft `LinterEngine.RunAsync(...,
  noCache: true, ...)` auf, filtert/aggregiert `RuleViolation`s nach
  Scope, unterscheidet `IsMalfunction` (echter Engine-Fehler, IsError=true)
  von normalen Leer-Ergebnissen. In eigener `AnalysisToolRegistrations`
  registriert (nicht `FileStructureToolRegistrations`), weil der
  `LinterEngine`-Pull-in den Footprint sonst über das Limit treibt —
  relevant, falls `metrics_tree`s `violation_density`-Pfad denselben
  Footprint-Druck erzeugt.
- **`src/AiNetLinter/Metrics/ComplexityCalculator.cs`** — statische
  `GetCyclomaticComplexity`/`GetCognitiveComplexity` pro
  `MethodDeclarationSyntax`; Quelle für Ø CC/max CC/max CogC im
  `complexity`-Modus (EPIC-02). Reine Berechnungslogik ohne
  Solution-/Tool-Abhängigkeit.
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
