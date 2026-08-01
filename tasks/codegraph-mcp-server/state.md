---
task: codegraph-mcp-server
workflow: dynamic-loop
started_at: 2026-08-01
orchestrator_session: mvs_9bacea56e2a54bcab43a08aa6be14c16
rules_dir: .agents/rules
case: 1 (frischer Task-Verzeichnis-Stand, aber inhaltlich Konsolidierung aus
  `tasks/codegraph-mcp` (drift-loop, gelöscht) und `tasks/codegraph-mcp-next`
  (Konzept-Verfeinerung, P2 entschlackt); siehe `konzept.md` "Bereits umgesetzt"
  und Konsolidierungs-Commit `7b4e467`)
---

# State: codegraph-mcp-server

## Phase 0 — Befund

- `konzept.md` vorhanden, umfangreich (668 Zeilen), mit vollständigem
  DoD, explizitem "Bereits umgesetzt"-Block und konkretem nächsten
  Arbeitsschritt ("Kritiker-Review für `get_violations`, Commit
  `e63176d`, nachholen — kein Neu-Code").
- `tech-debt.md` vorhanden, 8 Einträge (TD-001 bis TD-008),
  unverändert gültig, alle aus `drift-loop`-Vorgänger übernommen.
- Projektregeln: `.agents/rules/AiNetLinter.mdc` (alwaysApply, mit
  C#-Codequalitäts-Limits) + `.agents/rules/AiNetLinterRichtlinien.mdc`
  (alwaysApply, mit Architektur-/Workflow-Leitplanken). Genau ein
  Verzeichnis → übernommen.
- Working-Tree: **clean**, Branch `main` 3 Commits ahead of
  `origin/main` (Konsolidierungs-Commit `7b4e467` heute 10:15
  lokal committed, **nicht gepusht**).
- Keine uncommitteten Änderungen. ✓

### Realität vs. Doku (Fall 2/3-Verifikation)

- **Konsolidierungs-Commit `7b4e467`**: löscht `tasks/codegraph-mcp/`
  (drift-loop-Artefakte: `step-001/`..`step-010/`, `roadmap.md`,
  `task-state.md`, `tech-debt.md`) und `tasks/codegraph-mcp-next/`
  (bis auf P2-Backlog in `Konzept.md`); legt `tasks/codegraph-mcp-server/`
  mit `konzept.md` und `tech-debt.md` neu an. Git-Historie bleibt
  vollständig. ✓
- **`get_violations`-Code-Commit `e63176d`** (Konzept-Aussage):
  real existierend, Commit-Message ist `tasks: codegraph-mcp-next
  verfeinert` (extern durch Nutzer zusammengeführt, conventional-
  Format mit `[codegraph-mcp]`-Suffix nicht erfüllt). Inhalt
  umfasst `GetViolationsTool.cs`/`GetViolationsScanner.cs`/
  `GetViolationsToolTests.cs`/`AnalysisToolRegistrations.cs`
  (`+`-Commits), `McpServerCommand.cs`/`McpCodeGraphServer.cs`/
  `rules.json` (Modifikationen) — exakt der im step-010-Plan
  vorgesehene Scope. ✓
- **Build/Test-Stand**: 1088/1088 grün, 0 Warnungen, 0 Fehler.
  Cache-Files existieren in `src/AiNetLinter/bin/Debug/net10.0/cache/`
  — strukturell durch pre-existing `LinterEngineCacheTests`/
  `StaticTestSentinelExemptionTests` verursacht, dokumentiert in
  `step-010/step-result.md` (vor Konsolidierung via
  `git show 7474226:tasks/codegraph-mcp/step-010/step-result.md`),
  kein Step-Regress. ✓
- **`tasks/codegraph-mcp-next/Konzept.md`**: existiert noch
  (Konsolidierung hat es auf P2-Backlog entschlackt), aber ist
  **außerhalb** dieses Task-Scopes (explizit "Bewusst außerhalb
  dieses Tasks" in `konzept.md`). Werde ich nicht antippen.

### Vorbefund-Zusammenfassung (für Ralf)

- Sechs EPICs sind real umgesetzt und via `drift-loop` approved:
  EPIC-01 (CLI-Flag), EPIC-02 (Resident-Server), EPIC-03
  (5/9 Symbolgraph-Tools), EPIC-04 (2/4 Struktur-/Qualitäts-Tools
  — `get_index_scope` + `get_hotspots`).
- Ein Tool ist codiert, aber Review nicht abgeschlossen:
  `get_violations` (EPIC-04, 3/4). Code-Commit `e63176d` ist real,
  Tests grün, Build sauber, Dogfooding dokumentiert (0 Violations
  gegen reale `AiNetLinter.slnx`, konsistent mit CLI). Was fehlt:
  die zweite Hälfte der festen Drei-Rollen-Schleife — der
  Kritiker-Review.
- Offen: `search_pattern` (EPIC-04, 4/4), EPIC-05 (Scope-
  Kommunikation + Miss-Hint), EPIC-06 (Robustheit bei
  Compile-Fehlern), EPIC-07 (Tests), EPIC-08 (Doku).

## Phase 1 — Baseline (gemessen)

| Befehl | Ergebnis |
| :--- | :--- |
| `dotnet build AiNetLinter.slnx` | grün, 0 Warnungen, 0 Fehler, 10.66 s |
| `dotnet test AiNetLinter.slnx --no-build` | grün, 1088/1088, 0 Fehler, 0 übersprungen, 7:51 min |

Cache-Files in `bin/.../cache/`: 6 (5× BaselineMini, 1× AiNetLinter),
alle mit heutigem Datum, alle aus pre-existing Tests (siehe oben).
**Nicht** Step-Regress.

Baseline ist **grün**. Kein bekannter roter Stand, also voller
Count gegen Coder.

## Zähler (A1, Default-Werte aus `kernel.md`)

| Größe | Default | Verbraucht | Verbleibend |
| :--- | :---: | :---: | :---: |
| `max_aufrufe` | 40 | 0 | 40 |
| `max_fix_pro_einheit` | 3 | — | — |
| `max_fix_gesamt` | 12 | 0 | 12 |

Kein `konfig.md` vorhanden → keine User-Overrides. Defaults aktiv.

## Geplante Einheiten (Vorschau)

`konzept.md` nennt den nächsten konkreten Arbeitsschritt explizit
(Kritiker-Review für `get_violations`). Diese Einheit **muss**
als erste laufen — alles andere baut auf dem Konsens über den
bereits vorhandenen Code auf. Konkrete Schnitt-Entscheidung
(Einheiten 002..n für `search_pattern` / EPIC-05 / EPIC-06 /
EPIC-07 / EPIC-08) trifft der Planer **JIT** auf Basis des
tatsächlichen Codestands, nicht hier — siehe Kernel Teil B
"Drift".

## Nächste Aktion

→ Planer für Einheit 001 aufrufen (Kritiker-Review für
`get_violations`).
