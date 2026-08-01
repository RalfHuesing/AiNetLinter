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
| `max_aufrufe` | 40 | 11 | 29 |
| `max_fix_pro_einheit` | 3 | 0 (in 003) | 3 |
| `max_fix_gesamt` | 12 | 1 (002/fix-01) | 11 |

**Aufruf-Log:**
- 1× Planer für 001 (Kritiker-Review `get_violations`)
- 1× Kritiker für 001 (Verdict: `approved`, 0/0/5, 1 TD-Vorschlag → TD-009 übernommen)
- 1× Planer für 002 (`search_pattern`-Tool, inkl. P0/P1)
- 1× Coder für 002 (Commit `28e6e58`, 1097/1097 grün, A3 für 10 Tests dokumentiert)
- 1× Kritiker für 002 (Verdict: `issues`, 0/1/6, 1 MAJOR + 2 TD-Vorschläge)
- 1× Planer für 002/fix-01 (M-1 Hint-Bug)
- 1× Coder für 002/fix-01 (Commit `bd9e6fd`, 1097/1097 grün, A3 dokumentiert)
- 1× Kritiker für 002/fix-01 (Verdict: `approved`, 0/0/0, keine echten Befunde)
- 1× Planer für 003 (EPIC-05 Miss-Hint + Scope-Kommunikation)
- 1× Coder für 003 (Commit `dd4b44e`, 1101/1101 grün, 4 neue Tests + 1 modifiziert, A3 dokumentiert)
- 1× Kritiker für 003 (Verdict: `approved`, 0/0/4, Plan-Abweichung begründet, 3 TD-Vorschläge → TD-012/013/014 übernommen)

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

---

## Phase 2 — Loop-Protokoll

### Einheit 001 — Kritiker-Review `get_violations` (Commit `e63176d`)

- **Plan:** `units/001/plan.md` (Commit `272db39`) — 4-Ebenen-Checkliste
  mit `file:line`-Belegen, A3-Nicht-Notwendigkeit begründet
  (Review-only).
- **Result:** `units/001/result.md` (Commit `272db39`) — historisches
  Coder-Resultat, gespiegelt aus `git show
  7474226:tasks/codegraph-mcp/step-010/step-result.md`. Read-only-
  Übernahme, kein Eingriff in den Original-Commit.
- **Review:** `units/001/review.md` (siehe aktueller Commit) —
  Verdict **`approved`**, 0 CRITICAL, 0 MAJOR, 5 MINOR
  (Stil/Struktur-Beobachtungen, alle "fertig, fertig").
- **Aufrufe:** Planer (1) + Kritiker (1) = 2/40.
- **Tech-Debt:** TD-009 übernommen (Vorschlag des Kritikers, kein
  direkter Edit durch den Kritiker — vom Orchestrator in
  `tech-debt.md` ergänzt). TD-008 als weiter gültig markiert
  (PathOverrides = Pragmatik, nicht struktur-fix; `ILinterEngineConfig`-
  Refactor bleibt offen).
- **Status:** **approved**. EPIC-04 in `konzept.md` Zeile 79 ist
  um eine reviewte Position reicher — die "fertig"-Verschiebung in
  der Tool-Set-Tabelle (Z. 550) ist Sache des Nutzers (A7), nicht
  dieses Commits.
- **Nächste Einheit:** offen für Planer-Aufruf — wahrscheinlichste
  Kandidaten laut `konzept.md` in Reihenfolge: `search_pattern`
  (EPIC-04, 4/4), EPIC-05 (Scope-Kommunikation + Miss-Hint),
  EPIC-06 (Robustheit), EPIC-07 (Tests), EPIC-08 (Doku), dann die
  P0/P1-Erweiterungen. Konkrete Wahl trifft der Planer JIT.

### Einheit 002 — `search_pattern` Tool (Commit `28e6e58`)

- **Plan:** `units/002/plan.md` (Commit `286233d`) — 4 Vor-der-
  Planung-Checks, 6 offene Fragen an Coder, A3-Pflicht pro Test.
- **Result:** `units/002/result.md` (Commit `91278ea`) — 4 neue
  Dateien + 2 Modifikationen, 9 neue Tests (8 Unit + 1 E2E) +
  1 modifizierter Tool-Count-Test, alle mit A3-Fehlschlag-Nachweis.
  Build: 0 Warnungen. Tests: 1097/1097 grün (vorher 1088, +9).
  Footprint TD-004 widerlegt: keine 4. Registrar-Klasse.
  `SearchPatternTool` 2482/2500 (18 Z. Puffer knapp), Coder
  dokumentiert.
- **Review:** `units/002/review.md` (siehe aktueller Commit) —
  Verdict **`issues`**, 0 CRITICAL, **1 MAJOR** (M-1: falscher
  `McpToolResults.InvalidArgument`-Helper an Z. 40 liefert
  irreführenden Hint für `search_pattern`, inkonsistent mit der
  korrekten Nutzung an Z. 57-60; Test 8 prüft nur Existenz von
  `INVALID_ARGUMENT`, nicht Hint-Korrektheit — deshalb A3-Pfad
  unentdeckt), 6 MINOR.
- **Aufrufe:** Planer (1) + Coder (1) + Kritiker (1) = 3 für 002,
  plus 2 aus 001 = 5/40.
- **Tech-Debt-Vorschläge im Review (kein direkter Edit durch
  Orchestrator, A7/A5):**
  - **TD-010** (mittel): `SearchPatternTool` 2482/2500 knapp,
    künftige Tools treiben das wahrscheinlich über 2500 (vom
    Kritiker vorgeschlagen).
  - **TD-011** (niedrig): `SymbolGraphToolRegistrations` 2487/2500
    knapp, 5. Registrar-Klasse beim nächsten Symbolgraph-Tool
    wahrscheinlich (vom Kritiker vorgeschlagen).
  - Nutzer entscheidet, ob TD-010/TD-011 in `tech-debt.md`
    übernommen werden.
- **Status:** **`issues`**. Fix-Runde `002/fix-01/` wird als
  nächstes eingeleitet (Planer → Coder → Kritiker).

### Einheit 002/fix-01 — M-1 Hint-Bug Fix

- **Plan:** `units/002/fix-01/plan.md` (Commit `517bebe`) — exakte
  Code-/Test-Diffs, A3-Methodik operationalisiert, harte Scope-
  Grenze.
- **Result:** `units/002/fix-01/result.md` (Commit `b1a08a3`) —
  Coder-Commit `bd9e6fd` (`fix(mcp): search_pattern leerer-
  pattern-Hint`). `SearchPatternTool.cs:40` nutzt jetzt
  `McpToolResults.Error(LinterErrorCodes.InvalidArgument, ...,
  hint: "Pattern angeben — leeres Pattern ist nicht erlaubt.")`
  analog Z. 57-60. Test 8 um 3 Assertions erweitert. A3: alle 6
  Schritte dokumentiert, Failure "Not found: 'Pattern angeben'"
  wortwörtlich. Volllauf 1097/1097 grün.
- **Review:** `units/002/fix-01/review.md` (siehe aktueller
  Commit) — Verdict **`approved`**, 0/0/0, keine echten
  Befunde, 2 B-Hinweise (außerhalb des Scopes) für künftige
  Planer.
- **Aufrufe:** Planer + Coder + Kritiker = 3, gesamt jetzt
  8/40.
- **Status:** **`approved`**. Einheit 002 ist **komplett
  abgeschlossen** (1 erfolgreiche Fix-Runde). EPIC-04 ist
  fertig: 4/4 Tools reviewt (`get_index_scope`, `get_hotspots`,
  `get_violations`, `search_pattern`).
- **Tech-Debt:** TD-010 (`SearchPatternTool`-Footprint knapp)
  und TD-011 (`SymbolGraphToolRegistrations`-Footprint knapp)
  in `tech-debt.md` übernommen (Kritiker-Vorschläge aus 002
  Review, gleiche Logik wie TD-009 in 001).
- **Nächste Einheit:** offen für Planer-Aufruf. Die zwei
  wahrscheinlichsten Kandidaten aus `konzept.md`:
  1. **EPIC-05 Trunkierungs-Einbau in `find_symbol`** (analog
     `search_pattern` in 002) — Konzept Z. 215-225 fordert
     Trunkierung für alle Listen-Tools; `McpTruncation.cs` ist
     wiederverwendbar.
  2. **EPIC-05 Miss-Hint in `find_symbol`** via
     `GetFilesWithHits`-API (aus 002 exportiert) — Konzept
     Z. 604-606.
  Konkrete Wahl trifft der Planer JIT (Kernel Teil B "Drift",
  keine Vorauswahl).

### Einheit 003 — EPIC-05 Miss-Hint + Scope-Kommunikation in `find_symbol`

- **Plan:** `units/003/plan.md` (Commit `45678a8`) — 5 Vor-der-
  Planung-Checks, 9 Schritte, 5 A3-erforderliche Tests, harte
  Scope-Grenze. Plan-Abweichung ermöglicht: 1 neue Test-Datei
  erlaubt.
- **Result:** `units/003/result.md` (Commit `2c46168`) —
  Code-Commit `dd4b44e` (`feat(mcp): find_symbol miss-hint +
  initialize instructions`). 5 modifizierte Dateien + 1 neue
  Test-Datei (`McpServerOptionsFactoryTests.cs`, 31 Z., aus
  Plan-Abweichung). 4 neue Tests + 1 modifizierter, alle mit
  A3-Fehlschlag-Nachweis. Volllauf 1101/1101 grün. Build 0/0.
  Self-Lint OK. Dogfooding dokumentiert.
- **Review:** `units/003/review.md` (siehe aktueller Commit) —
  Verdict **`approved`**, 0 CRITICAL, 0 MAJOR, 4 MINOR
  (voll-`McpServerCommandTests.cs`, pre-existing
  `#nullable enable`-Lücke in `FindSymbolToolTests.cs`,
  Konzept-Wortlaut vs. SDK-Property, PathOverride-Puffer).
  Plan-Abweichung **begründet** bewertet.
- **Aufrufe:** Planer + Coder + Kritiker = 3 für 003, plus
  8 aus 001+002+002/fix-01 = 11/40.
- **Tech-Debt-Vorschläge im Review (übernommen):**
  - **TD-012** (niedrig): `FindSymbolTool` ohne Scanner-Split
    (TD-005-Generalisierung). **Inline** beim nächsten
    `find_symbol`-Anlass (z. B. 004 Trunkierung).
  - **TD-013** (niedrig): `find_symbol`-Miss-Hint-Datei-Liste
    ohne Trunkierung. **Inline** beim nächsten `find_symbol`-
    Anlass oder Last-Fixture-Messlauf.
  - **TD-014** (niedrig): `McpServerOptionsFactory` 2484/2500
    (16 Z. Puffer). **Inline** beim nächsten Anlass (z. B.
    `--mcp-log`-Flag).
- **Status:** **`approved`**. EPIC-05 für `find_symbol`
  abgeschlossen. Konzept Z. 604-606 (Miss-Hint-DoD) und
  Z. 98-101 (Scope-Kommunikation) erfüllt.
- **Nächste Einheit:** offen für Planer-Aufruf. Kandidaten:
  1. **004 = Trunkierungs-Einbau in `find_symbol`** (analog
     `search_pattern` in 002) — würde TD-012 (Scanner-Split)
     und TD-013 (Miss-Hint-Trunkierung) **inline** mitnehmen
     können (Kritiker-Vorschlag in 003).
  2. **EPIC-06** (Robustheit bei Compile-/Solution-Fehlern).
  3. **EPIC-07** (Tests-Ausbau).
  4. **EPIC-08** (Doku).
  Konkrete Wahl trifft der Planer JIT.
