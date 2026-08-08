---
task: metrics-tree
completed_at: 2026-08-08T20:30:00Z
final_status: done
total_iterations: 3
total_commits: 6
total_epics: 2
total_tech_debt_entries: 2
---

# Task Summary: metrics-tree

## Ergebnis

Das neue MCP-Tool `metrics_tree` ist vollständig umgesetzt und über MCP aufrufbar: alle 4
Konzept-Modi (`code_size`, `comment_density`, `violation_density`, `complexity`), gemeinsamer
ASCII-Tree-Renderer, Input-Parameter `root`/`mode`/`depth`/`top_n`/`file_filter`, Drill-Down-Hinweis
(`McpDrillDownHints` statt `McpSufficiencyHints` — bewusste, begründete Namensabweichung, inhaltlich
identisch zur Konzept-Anforderung). Der Walk-Kern wurde wie gefordert aus `GetHotspotsScanner` in
`SolutionFileWalker` extrahiert und für beide Datei-Walk-Modi wiederverwendet statt einer zweiten
Implementierung. Ergebnis passt zur ursprünglichen Intention aus `konzept.md`; die einzige
Muss-Haben-Abweichung (`method_count`-Modus) war von Anfang an bewusst aus dem Scope genommen
(„Verworfene Alternativen").

## Roadmap-Status

Beide Epics in `tasks/metrics-tree/roadmap.md` sind `[x]` abgehakt (EPIC-01 in step-001+step-002,
EPIC-02 in step-003) — selbst verifiziert, keine offenen oder als obsolet markierten Epics. Epic S2.5
in `tasks/features/05-roadmap.md` ist ebenfalls `[x]` (Übersichtstabelle Zeile 103), Detail-Akzeptanz-
kriterien Zeile 271ff. objektiv gepflegt: 6 von 8 Kriterien `[x]`, 2 bewusst `[ ]` mit Begründung
(„5+ Tests, 1 pro Mode“ — 21 Tests vorhanden, aber nur 4 statt 5 Modi, da `method_count` verworfen;
„Doku mit Beispielen pro Mode“ — nur Tabellenzeile, keine Pro-Mode-Beispiele in `agent-api.md`). Kein
stillschweigendes Abhaken, transparent dokumentiert.

## Steps-Übersicht

| Step | Epic | Status | Title | Commit | Notiz |
|------|------|--------|-------|--------|-------|
| step-001 | EPIC-01 | done | metrics_tree: Walk-Kern-Extraktion + code_size/comment_density-Modi + ASCII-Renderer + Tool | `92251cb`/`8cfddc6` | issues (2× MaxMethodParameterCount) |
| step-002 | EPIC-01 | done | Korrektur: MaxMethodParameterCount + TD-002 (WalkedFile-Extraktion) | `2cdaa7f`/`bc5cb01` | approved, corrects: step-001 |
| step-003 | EPIC-02 | done | Roslyn-Modi violation_density/complexity + Doku-Updates + Roadmap-Abschluss | `58a6aa5`/`a292aab` | approved |

## Globale Audit-Befunde (Kritiker, Modus `global`)

### Konzept erfüllt?

Ja. Alle Muss-Haben-Punkte aus `konzept.md` sind adressiert: 4 Modi, Input-Parameter (`root`/`mode`/
`depth`/`top_n`/`file_filter`), ASCII-Tree mit aggregierten Werten + Top-N-Kindern, Drill-Down-/
Sufficiency-Hinweis, Walk-Kern-Wiederverwendung (`SolutionFileWalker` statt zweiter Implementierung),
Doku-Updates (`Docs/agent-api.md`, `Docs/ROADMAP.md`, `README.md` — alle drei bestätigt geändert),
Epic S2.5 abgehakt. Kein Non-Goal umgesetzt (kein Mermaid/Graph-Output, kein `method_count`, keine
neue Linter-Regel).

### Seiteneffekte / Regressionen

`dotnet build AiNetLinter.slnx` selbst reproduziert: grün, 0 Warnungen, 0 Fehler. `dotnet test
--filter Category=Unit` selbst reproduziert: **1197/1197 grün** (55s). Der vollständige
Abschluss-Volllauf (`--filter Category!=Stress`, 1349/1349 grün) wurde bereits vom Coder in step-003
durchgeführt und vom Step-Kritiker per `--list-tests`-Abgleich verifiziert (dieselbe Testanzahl) —
kein erneuter Volllauf nötig, keine Auffälligkeiten, die einen Re-Run rechtfertigen würden.
`get_violations` (voller Solution-Scope, selbst ausgeführt): **3 Verstöße**, alle drei vorbestehende,
absichtliche `AllowDynamic`-Fixture-Fehler in `tests/Fixtures/DiRegistrationMini/` — unverändert seit
vor diesem Task, keine neuen Verstöße. Insbesondere keine `AIContextFootprint`-Warnungen mehr (TD-001
war zeitweise als Warnung sichtbar, ist durch `rules.json`-`PathOverride`-Anpassungen aktuell grün,
siehe Tech-Debt unten). CLI-`--map`-Subcommands unverändert: `git log`/`git diff` bestätigen keine
Commits dieses Tasks berühren `src/AiNetLinter/Commands/MapCommand.cs` oder `src/AiNetLinter/Maps/**`.

### Rules-Konformität (Stichproben)

Alle drei Steps stichprobenartig gegengeprüft (nicht nur aus den Step-Reviews übernommen):
- **step-001/002:** `MetricsTreeScanner.cs`/`MetricsTreeTool.cs` selbst gelesen — `MetricsTreeQuery`
  ist ein `internal sealed record` auf Namespace-Ebene (Zeile 14), löst den ursprünglichen
  `MaxMethodParameterCount`-Verstoß wie im Korrektur-Step beschrieben. `BuildNode` trägt eine
  begründete `ainetlinter-disable`-Suppression mit Why-Kommentar (kein Task-ID-Bezug) — konform mit
  `AiNetLinterRichtlinien.mdc` §5.
- **step-003:** `get_violations` (voller Scope) bestätigt aktuell 0 Verstöße im Produktionscode.

Keine neuen Abweichungen gegenüber den bereits in den Step-Reviews dokumentierten Findings gefunden.

## Tech-Debt-Zusammenfassung

- **Hoch:** 0 Einträge
- **Mittel:** 1 Eintrag — `TD-001`
- **Niedrig:** 1 Eintrag — `TD-002` (Status: erledigt seit step-002)

`TD-001` (`AIContextFootprint`-Druck durch die drei Config-Override-Typen über
`McpCodeGraphServer`, betrifft aktuell `AnalysisToolRegistrations.cs` +
`Mcp/Tools/MetricsTree/MetricsTreeTool.cs`) bleibt offen und ist der einzige aus Nutzersicht
relevante Punkt: der Druck wurde über den Task-Verlauf nicht behoben, sondern durch
`PathOverride`-Anhebungen kaschiert — beide betroffenen Klassen liegen laut letztem Review nur noch
ca. 5-13 Zeilen unter ihrem jeweiligen Override, praktisch keine Reserve mehr für künftiges Wachstum
in diesem Codebereich. Kein Blocker für diesen Task, aber ein Kandidat für einen eigenen Facade-Task,
bevor der nächste Sprint (S2.2/S2.3) weiteren Code in diesen Bereich zieht.

## Offene Punkte

- [ ] Zwei Akzeptanzkriterien in `tasks/features/05-roadmap.md` (Zeile 282, 286) bleiben bewusst
  `[ ]` — kein echter Mangel, sondern objektive Nichterfüllung der ursprünglichen (5-Modi-)Formulierung
  nach der bewussten Reduktion auf 4 Modi. Keine Handlung nötig, nur zur Kenntnis.
- [ ] `ServerInstructions.cs` listet `metrics_tree` nicht im `initialize`-Handshake-Text (vorbestehende
  Lücke seit EPIC-01, in step-003 als außerhalb des Datei-Scopes benannt, kein eigener Tech-Debt-Eintrag
  angelegt) — minimal, kein Blocker.

## Empfehlungen

- `TD-001` vor dem nächsten größeren MCP-Tool-Task (S2.2 `pattern_detect` oder S2.3
  `metrics_lookup`) als eigenes kleines Refactoring-Epic aufgreifen — die Facade-Idee für
  `GlobalConfigOverride`/`MetricsConfigOverride`/`TestSentinelConfigOverride` ist bereits skizziert
  (`tech-debt.md` TD-001), reduziert Footprint-Druck an mehreren Stellen gleichzeitig statt ihn erneut
  nur zu verschieben.
- Optional, geringe Priorität: `metrics_tree` in `ServerInstructions.cs`-Handshake-Text ergänzen.

## Statistik

- **Anzahl Epics:** 2, davon abgehakt: 2
- **Anzahl Steps:** 3 (2 regulär + 1 Korrektur)
- **Davon approved:** 2 (step-002, step-003)
- **Davon issues → korrigiert:** 1 (step-001)
- **Davon blocked:** 0
- **Anzahl Commits:** 6 (je Code- + Doku-Commit pro Step)
- **Anzahl Tech-Debt-Einträge:** 2 (davon `auto_fixable: ja`: 1, TD-002, erledigt)
- **Davon Korrektur-Steps:** 1 (`step-002` corrects `step-001`, Kettenlänge 1 / 3)
- **Laufzeit:** 2026-08-08T17:31:45Z bis 2026-08-08T20:30:00Z (ca. 3h)
