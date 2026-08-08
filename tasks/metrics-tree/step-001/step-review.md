---
status: done
type: step-review
task: metrics-tree
step: 001
epic: EPIC-01
step_type: single
reviewed_by: kritiker
reviewed_by_model: claude-sonnet-5
reviewed_by_model_knowledge_cutoff: 2026-01
reviewed_at: 2026-08-08
verdict: issues
tech_debt_ids: [TD-001, TD-002]
---

# Review Step 001: metrics_tree — Datei-Walk-Modi (EPIC-01)

## Verdict

- [ ] **approved** — alle vier Prüfebenen ok
- [x] **issues** — Korrektur-Step `step-002` anlegen (`corrects: step-001`)
- [ ] **blocked** — Nutzer-Entscheidung nötig (siehe Frage unten)

## Geprüft

- [x] Plan-Erfüllung: alle im `step-plan.md` genannten Änderungen erfolgt
- [x] Rules-Konformität: `<rules_dir>/**` (kuratierte Rules-Refs-Auswahl) eingehalten — bis auf 2 Findings
- [x] Logische Korrektheit: Code macht was er soll, nicht nur „grün"
- [x] Konzept-Treue: passt die Umsetzung zu `konzept.md` (Scope, Non-Goals, Muss-Haben)
- [x] Build: selbst nachgeprüft, grün
- [x] Tests: selbst nachgeprüft, grün (gefilterter Lauf, gemäß abweichendem Gate)

## Befund

### Plan-Erfüllung

Alle 8 „Konkrete Änderungen" (Dateien 1-8) sind wie geplant umgesetzt, inkl. der bewusst dokumentierten
Abweichungen (`OverviewResourceRegistration`-Parität, `TryBuildFileFilter`-Extraktion,
`FileMetric`-Aggregationsfelder, Bytes in `code_size`-Display) — alle vier nachvollziehbar begründet,
keine davon eine unangekündigte Scope-Erweiterung. Alle geplanten Tests existieren (13 Tool-Tests inkl.
2 Theory-Fälle, 4 Renderer-Tests) und decken die im Plan gelisteten Fälle 1:1 ab. `codemap.md` wurde
korrekt und mit Substanz aktualisiert (inkl. dem Footprint-Hinweis). `GetHotspotsScanner`-Umstellung ist
verhaltensidentisch (Diff geprüft: `MatchesScope`/`TryCountLines` 1:1 nach `SolutionFileWalker` verschoben,
keine Logikänderung), Regression über bestehende `GetHotspotsToolTests` abgesichert (im gefilterten Lauf
mitgelaufen, grün). Status-Feld in `step-plan.md` korrekt auf `done (pending audit)` gesetzt.

### Rules-Konformität

Selbst nachgeprüft per `ainetlinter`-MCP (`get_violations`, Scope auf die 8 geänderten Dateien):

- `sealed` auf allen konkreten Record-Typen (`MetricsTreeNode`, `FileMetric`, `BuilderNode`) korrekt
  gesetzt, `WalkedFile` als `record struct` sealed-äquivalent — eingehalten.
- `#nullable enable` in allen 6 neuen Dateien vorhanden — eingehalten.
- `MaxLineCount` 500: größte neue Datei `MetricsTreeScanner.cs` mit 238 Zeilen, weit im Limit —
  eingehalten.
- `MaxCyclomaticComplexity`/`MaxCognitiveComplexity`: keine Verstöße gemeldet — eingehalten.
- `AIContextFootprint` 2500: **verletzt**, aber Warnung (nicht Fehler) und bereits vom Coder für
  `FileStructureToolRegistrations` (2894>2890) benannt und begründet; eine zweite, bisher unbenannte
  Instanz auf `MetricsTreeTool` (2532>2500) mit identischer Ursache gefunden — beides zusammen als
  `TD-001` dokumentiert (kein Blocker: Warnung, `dotnet build` bleibt grün, Ursache bereits vor diesem
  Step angelegt).
- Kommentarsparsamkeit/Why-Kommentar zur `comment_density`-Sortierrichtung: im Code vorhanden
  (`MetricsTreeScanner.cs`, `ComputeSortValue`) — wie vom Plan gefordert, eingehalten.

**2 MAJOR-Verstöße gegen `MaxMethodParameterCount` (4)** — explizit Teil des zitierten
`§„Grenzwerte"`-Abschnitts in `AiNetLinter.mdc`, siehe Findings unten. Zusätzlich ein `BanPublicNestedTypes`-
Fehler auf `WalkedFile`, der **außerhalb** der zitierten Rules-Refs-Auswahl liegt (taucht in der
aktuellen `AiNetLinter.mdc`-Kurzfassung gar nicht auf) — daher kein Ebene-2-Finding, sondern als
`TD-002` dokumentiert.

### Logische Korrektheit

Baum-Aggregation (`BuildNode`/`GroupByNextSegment`) korrekt: Verzeichnis-Segmentierung relativ zum
Root-Knoten, Aggregation jenseits `depth` in den letzten sichtbaren Knoten (kein stillschweigendes
Abschneiden, wie vom Plan gefordert). `comment_density`-Aggregation nutzt korrekt eine gewichtete Ratio
(Summe Kommentarzeilen / Summe Gesamtzeilen) statt eines fehlerhaften Mittelwerts von Einzel-Ratios —
positive, im `step-result.md` selbst dokumentierte Abweichung vom Plan-Pseudocode, die einen sonst
entstehenden Bug vermeidet. `MetricsTreeRenderer` ist rein formatierend, Top-N + Rest-Zeile funktioniert
(durch eigene Tests + Nachvollzug bestätigt). Die zwei vom Coder selbst dokumentierten „Bekannten
Unschärfen" (Tie-Breaking bei Gleichstand, keine Roslyn-genaue Kommentarzählung bei Trailing-`//`) sind
beide explizit vom Plan als „kein vollständiger Tokenizer"/kein spezifiziertes Tie-Breaking gedeckt —
kein Fund gegen die Plan-Vorgabe. `root`-Präfix-Matching (`StartsWith` statt Pfadsegment-Grenze) ist eine
im Plan explizit getroffene, bewusste Design-Entscheidung (nicht eigenständig vom Coder erfunden) — kein
Fund.

### Konzept-Treue (Ebene 4)

Deckt exakt die für dieses Epic vorgesehenen Muss-Haben-Punkte ab (Datei-Walk-Modi `code_size`/
`comment_density`, `root`/`mode`/`depth`/`top_n`/`file_filter`, ASCII-Tree, Drill-down-Hinweis,
Tool-Registrierung, Walk-Kern-Extraktion). Kein Non-Goal umgesetzt (kein Mermaid, keine neue
Lint-Regel). `violation_density`/`complexity` bewusst nicht implementiert — laut `konzept.md`/Roadmap
EPIC-02, kein Scope-Fehler. CLI-`--map`-Subcommands unangetastet (Diff geprüft: keine Änderung an
`Commands/MapCommand.cs` oder `Maps/**`). Scope entspricht der Intention des Step-Plans, weder größer
noch kleiner.

### Build-/Test-Status

```
dotnet build AiNetLinter.slnx                                                   → grün (0 Warnungen, 0 Fehler)
dotnet test --filter "FullyQualifiedName~MetricsTree|FullyQualifiedName~GetHotspots" → grün (28 Tests, 0 Fehler)
```

Beide Läufe selbst reproduziert (nicht nur aus `step-result.md` übernommen). Gemäß abweichendem
Test-Gate für diesen Task (`roadmap.md` Tech-Stack-Notiz) ist für step-001 nur der gezielte Lauf
Pflicht — erfüllt. Kein Volllauf verlangt/durchgeführt.

## Findings (nur bei `issues`)

1. `src/AiNetLinter/Mcp/Tools/MetricsTreeScanner.cs:21` — **[MAJOR]** **[Rules-Konformität]**
   `BuildTree(Solution solution, string? root, MetricsTreeMode mode, int depth, int topN, Regex?
   fileFilter)` hat 6 Parameter, `MaxMethodParameterCount` (4, `AiNetLinter.mdc` §„Grenzwerte", Teil der
   im Step-Plan zitierten Rules-Refs) ist verletzt. Verifiziert per `get_violations` (Warnung, aber
   explizite Regelverletzung in Produktionscode, kein Einzelfall im Projekt-Vergleich — `FindSymbolTool`
   und `GetHotspotsTool`, die als Referenzmuster zitiert wurden, haben laut `get_violations` null
   Verstöße dieser Art). **Fix:** `root`/`mode`/`depth`/`topN`/`fileFilter` in einen Parameter-Record
   bündeln (z. B. `internal sealed record MetricsTreeQuery(string? Root, MetricsTreeMode Mode, int
   Depth, int TopN, Regex? FileFilter)`), Signatur auf `BuildTree(Solution solution, MetricsTreeQuery
   query)` reduzieren, Aufrufstelle in `MetricsTreeTool.ExecuteAsync` entsprechend anpassen.
2. `src/AiNetLinter/Mcp/Tools/MetricsTreeTool.cs:23` — **[MAJOR]** **[Rules-Konformität]**
   `ExecuteAsync(McpCodeGraphServer state, string? root, string mode, int depth, int topN, string?
   fileFilter, CancellationToken ct)` hat 7 Parameter (6 gewertet, `CancellationToken` ausgenommen) —
   gleicher Regelverstoß wie Finding 1. **Fix:** entweder denselben `MetricsTreeQuery`-Record (rohe,
   ungeparste Werte: `string? Root, string Mode, int Depth, int TopN, string? FileFilter`) für die
   Tool-Ebene verwenden und erst intern in den validierten `MetricsTreeQuery` aus Finding 1 überführen,
   oder einen separaten `MetricsTreeToolArgs`-Record einführen — Signatur auf
   `ExecuteAsync(McpCodeGraphServer state, MetricsTreeToolArgs args, CancellationToken ct)` reduzieren.
   Die Registrierungs-Lambda in `FileStructureToolRegistrations.AddMetricsTree` (MCP-Tool-Schema-Bindung)
   bleibt mit benannten Einzelparametern bestehen — nur der interne Aufruf von
   `MetricsTreeTool.ExecuteAsync` baut daraus den Record.

## Sonstige Beobachtungen / MINOR / NITPICK

- `step-result.md` „Beobachtungen" nennt nur eine der beiden `AIContextFootprint`-Warnungen
  (`FileStructureToolRegistrations`), nicht die zweite auf `MetricsTreeTool` selbst — inhaltlich
  dieselbe Ursache, daher kein eigener Fund, aber die Beobachtung war unvollständig. Für den nächsten
  Step-Plan relevant (siehe `TD-001`).

## Tech-Debt-Einträge aus diesem Review

- `TD-001` (siehe `tech-debt.md`) — `AIContextFootprint`-Druck auf `FileStructureToolRegistrations` UND
  `MetricsTreeTool`, gemeinsame Ursache in den Config-Override-Typen über `McpCodeGraphServer`; Facade
  vor EPIC-02 erwägen.
- `TD-002` (siehe `tech-debt.md`) — `SolutionFileWalker.WalkedFile` verletzt `BanPublicNestedTypes`
  (Error-Severity im eigenen Linter, aber außerhalb der zitierten Rules-Refs); mechanische Extraktion
  in eigene Datei, `auto_fixable: ja`.
