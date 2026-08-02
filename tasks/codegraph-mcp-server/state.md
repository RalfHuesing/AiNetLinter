---
task: codegraph-mcp-server
workflow: dynamic-loop
started_at: 2026-08-01
orchestrator_session: mvs_9bacea56e2a54bcab43a08aa6be14c16
resumed_at: 2026-08-02
resumed_by: mvs_287875e0a9f74becbf96dca3b88b20fb
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
| `max_aufrufe` | 40 | 21 | 19 |
| `max_fix_pro_einheit` | 3 | 0 (in 006) | 3 |
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
- 1× Planer für 004 (Trunkierung in `find_symbol` + TD-012/013 inline)
- 1× Coder für 004 (Commit `c6261ea`, 1108/1108 grün, 7 neue + 8 modifizierte Tests, A3 dokumentiert)
- 1× Kritiker für 004 (Verdict: `approved`, 0/0/3, 3 Plan-Abweichungen begründet, **TD-012/013 geschlossen bestätigt**)
- 1× Planer für 005 (Trunkierung in `find_references` + `get_impact`)
- 1× Coder für 005 (Commit `3eb13bf`, 1114/1114 grün, 6 neue Tests, A3 dokumentiert)
- 1× Kritiker für 005 (Verdict: `approved`, 0/0/2, P0/P1-Trunkierung in 4/4 Listen-Tools erfüllt, **TD-011-Stand aktualisiert**)
- 1× Planer für 006 (EPIC-06 Robustheit)
- 1× Coder für 006 (Commit `de47034`, 1127/1127 grün, 13 neue Tests, A3 dokumentiert)
- 1× Kritiker für 006 (Verdict: `approved`, 0/1/5, **TD-015/016 als Vorschläge** — 1 MAJOR Dead Code, aber kein `issues` weil 8/9 Tools alternativen Pfad nutzen)
- 1× Coder für 007 (Commits `49feb65`+`3b29d72`+`bb0544d`+`acb8ee4`, 9 neue Tests in 6 Dateien + TD-003 strukturell gefixt + TD-015 inline gelöst + TD-016 **teil**geschlossen)
- **0× Kritiker für 007** — User-Stopp bevor Review gestartet werden konnte (siehe 007-Block unten).

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

## Workflow-Hinweis: Subagenten-Workspace-Anchor

**Beobachtung (Ralf, 2026-08-01, ~14:14):** Subagenten
orientieren sich nicht zuverlässig am tatsächlichen Workspace
und biegen in andere Projektverzeichnisse ab (User-Memory
„current project: SqlToAi" verfängt stärker als der
tatsächliche Auftrag). Workaround: jedem Subagenten den
Arbeitsordner als Anchor im Prompt mitgeben, strukturiert
(YAML/Bullet-Liste mit absoluten Pfaden), als erster Block
vor allen anderen Anweisungen. Vor jedem `cd`/`git`-Befehl
eigenständig `Get-Location`/`pwd` verifizieren.

**Wurde ab Einheit 004 angewendet.** Siehe Aufruf-Prompts
in dieser Session ab dem 004-Planer-Aufruf.

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

### Einheit 004 — Trunkierung + Scanner-Split + Miss-Hint-Trunkierung in `find_symbol`

- **Plan:** `units/004/plan.md` (Commit `5950645`) — 5 Vor-der-
  Planung-Checks, 11 Schritte, 3 vom Planer vorgegebene
  Entscheidungen (`DescribeKind`/`FormatSymbolLocations` im
  Tool, eigene E2E-Datei statt `McpServerCommandTests.cs`).
- **Result:** `units/004/result.md` (Commit `72704c7`) —
  Code-Commit `c6261ea` (`feat(mcp): find_symbol trunkierung
  + scanner-split (TD-012, TD-013)`). 4 neue Dateien
  (`FindSymbolScanner.cs` 94 Z., `FindSymbolScannerTests.cs`,
  `McpServerCommandFindSymbolTests.cs`, 2 Fixture-Dateien
  `Component.razor`+`Page.xaml`) + mehrere Modifikationen.
  7 neue Tests + 8 modifizierte, alle mit A3. Volllauf
  1108/1108 grün. Build 0/0.
- **Review:** `units/004/review.md` (siehe aktueller Commit) —
  Verdict **`approved`**, 0/0/3, 3 Plan-Abweichungen alle
  begründet. **TD-012 + TD-013 geschlossen bestätigt.**
- **Aufrufe:** Planer + Coder + Kritiker = 3 für 004, plus
  11 aus 001+002+002/fix-01+003 = 14/40.
- **Footprint:** `FindSymbolTool` 2529 → 2491 (-38 Z.,
  Scanner-Split hat Logik rausgezogen — schöner
  Nebeneffekt), `FindSymbolScanner` neu 94 Z.,
  `SymbolGraphToolRegistrations` 2488 → 2490 (+2 Z.,
  Puffer 10 Z. knapp), `McpTruncation` 44 → 70 (+26 Z.
  für `TruncateFileList`).
- **Tech-Debt:** TD-012 + TD-013 in `tech-debt.md` auf
  **geschlossen** gesetzt (Status-Feld + Index-Zeile
  aktualisiert, Body bleibt für die Historie). 14 → 12
  offene Einträge.
- **Status:** **`approved`**. 004 ist **komplett
  abgeschlossen**.
- **Nächste Einheit:** offen für Planer-Aufruf.
  Wahrscheinlichste Kandidaten:
  1. **005 = Trunkierungs-Einbau in `find_references` +
     `get_impact`** (analog 004, sinnvoll weil
     `McpTruncation` jetzt etabliert ist).
  2. **EPIC-06** (Robustheit bei Compile-/Solution-
     Fehlern).
  3. **EPIC-07** (Tests-Ausbau).
  4. **EPIC-08** (Doku — inkl. Trunkierungs-Format-Regel
     in `Docs/agent-api.md`).
  Konkrete Wahl trifft der Planer JIT.

### Einheit 005 — Trunkierung in `find_references` + `get_impact`

- **Plan:** `units/005/plan.md` (Commit `9d2dd99`) — 5 Vor-der-
  Planung-Checks, 11 Schritte, 1 mögliche Plan-Abweichung
  (Symbol-Branch-Delegation, vom Coder **nicht** genommen).
- **Result:** `units/005/result.md` (Commit `d6023e8`) —
  Code-Commit `3eb13bf` (`feat(mcp): find_references +
  get_impact trunkierung (P0/P1)`). 9 files changed, 6 neue
  Tests (3 Unit + 3 E2E, 2 neue E2E-Dateien analog 004),
  Fixture-Erweiterung (`RunTwice`/`RunThrice` in
  `Caller.cs` + `CalculatorCaller.cs`). Volllauf 1114/1114
  grün. Build 0/0. A3 für 3 Unit-Tests dokumentiert.
- **Review:** `units/005/review.md` (siehe aktueller Commit)
  — Verdict **`approved`**, 0/0/2. Plan-Erfüllung 100 %,
  keine Plan-Abweichung ausgelöst, Konzept-Treue erfüllt.
  Korrektur des Kritikers: `McpServerCommandTests.cs` ist
  426/500 (nicht 499/500 wie Planer angenommen), 74 Z.
  Puffer.
- **Aufrufe:** Planer + Coder + Kritiker = 3 für 005, plus
  14 aus 001+002+002/fix-01+003+004 = 17/40.
- **Footprint TD-011:** `FindReferencesTool` 2519 → 2522
  (+3, Puffer 178), `GetImpactTool` 2490 → 2495 (+5, Puffer
  5 knapp), `SymbolGraphToolRegistrations` 2490 → 2494 (+4,
  Puffer **6 knapp**). **TD-011 in `tech-debt.md`
  verschärft** (Puffer-Schrumpfung dokumentiert, „5. Klasse
  zwingend" statt „wahrscheinlich").
- **Status:** **`approved`**. 005 ist **komplett
  abgeschlossen**. **P0/P1-Trunkierung in allen 4
  Listen-Tools erfüllt** (`search_pattern` 002, `find_symbol`
  004, `find_references`+`get_impact` 005).
- **Nächste Einheit:** offen für Planer-Aufruf. Kandidaten
  (in Reihenfolge der Konzept-Logik):
  1. **EPIC-06** (Robustheit bei Compile-/Solution-Fehlern,
     Konzept Z. 146-153, DoD Z. 609-611): MCP-Server soll
     auch bei Compile-Fehlern antworten, statt abzustürzen.
  2. **EPIC-07** (Tests-Ausbau): Staleness-Invalidierung,
     Mehrdeutigkeits-Abbruch, Cache-Isolation, etc.
  3. **EPIC-08** (Doku): `Docs/agent-api.md` mit MCP-Modus,
     `Docs/integration.md` mit Registrierung, `Docs/ROADMAP.md`
     + `README.md`.
  4. P0/P1-Rest-Erweiterungen: Kaltstart entkoppeln,
     `rules.json`-Auto-Discovery, Staleness-Sweep mit
     Verzeichnis-`mtime`, `--mcp-log` Call-Log, RefreshStale-
     Documents-Verzeichnis-Sweep (neu/gelöschte Dateien),
     `ILintConsole` für MCP (stdout-Schutz).
  Konkrete Wahl trifft der Planer JIT.

### Einheit 006 — EPIC-06 Robustheit (Compile-Fehler-Warnhinweis + Server-Lifecycle)

- **Plan:** `units/006/plan.md` (Commit `25c1800`) — 6 Vor-der-
  Planung-Checks, 10 Schritte (Schritt 4 gestrichen wegen
  besserer Footprint-Bilanz), 12 A3-erforderliche Tests, neue
  Fixture `CompileErrorMiniFixture`.
- **Result:** `units/006/result.md` (Commit `a8234e3`) —
  Code-Commit `de47034` (`feat(mcp): compile-fehler-warnhinweis
  in allen 9 tools + server-lifecycle (EPIC-06)`). 4 neue
  Dateien (`McpCompileDiagnostics.cs` als statischer Helper,
  `McpServerCommandErrorHandlingTests.cs` als neue E2E-Datei,
  `CompileErrorMiniFixtureWorkspace.cs`, 8 Fixture-Dateien)
  + Modifikationen an 9 Tools + 10 Test-Dateien. 13 neue
  Tests (12 geplant + 1 Helper-Test). Volllauf 1127/1127
  grün. Build 0/0. A3 für 5 Tests wortwörtlich dokumentiert
  + 7 transitiv abgesichert. Schritt-1-Befund: MSBuildWorkspace
  lädt kaputte Solution, Plan-B entfällt.
- **Review:** `units/006/review.md` (siehe aktueller Commit) —
  Verdict **`approved`**, 0/1/5. 1 MAJOR: `McpToolResults.
  WarningsSection` ist Dead Code (kein Production-Caller),
  aber alle 8 Tools nutzen den alternativen
  `BuildAggregateWarningAsync` + `PrependWarning` — also
  kein `issues`-Verdict. T9 (`get_violations` Negativtest)
  am Code verifiziert. TD-003-Umgehung ausreichend.
- **Aufrufe:** Planer + Coder + Kritiker = 3 für 006, plus
  17 aus 001+002+002/fix-01+003+004+005 = 20/40.
- **Footprint:** alle 4 betroffenen Klassen unter Limit
  (kein PathOverride-Eingriff nötig).
- **Tech-Debt:** TD-015 (`WarningsSection` Dead Code) +
  TD-016 (Fixture-Code-Duplikation in 4 Workspace-Klassen)
  in `tech-debt.md` aufgenommen (Kritiker-Vorschläge). 12 →
  14 offene Einträge.
- **Status:** **`approved`**. 006 ist **komplett
  abgeschlossen**. **EPIC-06 vollständig** (Compile-Fehler-
  Warnhinweis in 8/9 Tools, `get_violations` mit Negativtest,
  Server-Lifecycle mit E2E-Tests).
- **Nächste Einheit:** offen für Planer-Aufruf.
  Wahrscheinlichste Kandidaten:
  1. **EPIC-07** (Tests-Ausbau, Konzept Z. 104-107, 624):
     Staleness-Invalidierung, Integrationstests je Tool,
     Miss-Hint, Mehrdeutigkeits-Abbruch, Cache-Isolation,
     CLI-Regression. **TD-003-Fix** (`RegisterMSBuild`
     Race) sollte hier inline addressiert werden (Kritiker
     hat die Umgehung als „ausreichend" bewertet, aber
     struktureller Fix ist sauberer).
  2. **EPIC-08** (Doku, Konzept Z. 107-108, 623):
     `Docs/agent-api.md` mit MCP-Modus,
     `Docs/integration.md` mit Registrierung, `Docs/ROADMAP.md`
     + `README.md`.
  3. P0/P1-Rest-Erweiterungen: Kaltstart entkoppeln,
     `rules.json`-Auto-Discovery, Staleness-Sweep mit
     Verzeichnis-`mtime`, `--mcp-log` Call-Log, RefreshStale-
     Documents-Verzeichnis-Sweep (neu/gelöschte Dateien),
     `ILintConsole` für MCP (stdout-Schutz).
  Konkrete Wahl trifft der Planer JIT.

---

## ⚠️ SESSION-STOPP (2026-08-02, 00:25)

**Stopp-Grund:** User-Abbruch ("warte bis der coder fertig ist dann
stoppen wir alles"). Coder für Einheit 007 ist erfolgreich
durchgelaufen, danach wurde der Workflow auf User-Wunsch gestoppt —
**kein Kritiker-Aufruf, kein Volllauf-`dotnet test`**.

### Stand bei Stopp

- **Build:** grün, 0 Warnungen (Coder-Bericht).
- **Tests:** 80/80 Unit-Tests grün (Coder-Bericht, vor 007: 72).
  Zusätzlich 12/12 gezielter E2E-Slice auf alle 007-neuen Tests
  grün (Coder-Bericht). **Volllauf `dotnet test AiNetLinter.slnx`
  wurde NICHT durchgeführt** — laut `AGENTS.md` §2 Pflicht vor
  Task-Beendigung, also formaler Stand: **nicht abschließend
  verifiziert**. Gezielter Slice deckt aber alle neuen Tests ab.
- **Working tree:** clean, alle 007-Änderungen lokal committed
  (kein Push per A4).
- **4 Commits lokal** (Reihenfolge):
  - `49feb65` `fix(baseline): sourcefilecatalog registermsbuild thread-safe (TD-003)`
  - `3b29d72` `feat(tests): EPIC-07 tests-ausbau (6 dod-bereiche abgesichert)`
  - `bb0544d` `chore(task): unit 007 result`
  - `acb8ee4` `chore(task): unit 007 result, commit-hashes ergaenzt`
- **Commit-Disziplin:** Abweichung vom Plan (2 geplante Commits → 4
  tatsächliche, weil result.md und Hash-Ergänzung als eigene Commits
  statt im Test-Commit mitgenommen). Inhaltlich unkritisch, formal
  ein bisschen zerfasert. Nächste Session sollte das bei der
  Bewertung berücksichtigen.
- **TD-Status nach 007:**
  - **TD-003** — **geschlossen** (strukturell gefixt, Lock +
    Check-Lock-Check + 3 Tests in
    `SourceFileCatalogRegisterMSBuildTests.cs` mit A3 via
    Reflection).
  - **TD-015** — **geschlossen** (WarningsSection-Methode +
    XML-Doc + tautologischer Test entfernt.
    `McpToolResults.cs` 134 → 122 Z.).
  - **TD-016** — **TEILGESCHLOSSEN**: Commit `6c872e4` hat nur
    `BaselineMiniFixtureWorkspace` und `SymbolGraphMiniFixtureWorkspace`
    auf `FixtureWorkspaceBase` umgestellt. `CompileErrorMiniFixtureWorkspace`
    und `GitImpactMiniFixtureWorkspace` enthalten weiterhin die
    duplizierten `CopyFixture`/`IsGeneratedPath`/`FindSolutionRoot`-
    Methoden. Coder hat das transparent in `result.md` und
    `tech-debt.md` (TD-016-Teilschluss-Anmerkung) dokumentiert und
    Folge-Refactor als **TD-016a** für künftigen Cycle empfohlen.
  - Offene TD-Einträge jetzt: TD-001, TD-002, TD-003✅, TD-004,
    TD-005, TD-006, TD-007, TD-008, TD-009, TD-010, TD-011, TD-012✅,
    TD-013✅, TD-014, TD-015✅, TD-016(teil)+TD-016a neu — exakte
    Zählung in `tech-debt.md`-Index prüfen.
- **Plan-Abweichung:** `CliBatchRegressionTests` testet Exit-Code 1
  (nicht 0 wie im Plan angedeutet) — der Plan hatte einen
  inneren Widerspruch ("Exit-Code 0" + "ViolationTrigger im
  Output"). Coder hat das korrekt zu Exit-Code 1 + ViolationTrigger
  aufgelöst und in `result.md` dokumentiert. **Muss vom Kritiker
  in der nächsten Session bestätigt werden**.

### Nächste Aktion (für nächste Session)

1. **Prüfen, ob die 4 Commits den Erwartungen entsprechen**
   (ggf. squashen oder fehlende Struktur ergänzen — nach
   `AGENTS.md` §2 ist `dotnet test` Pflicht).
2. **Volllauf `dotnet test AiNetLinter.slnx --no-build`**
   ausführen — wenn grün, ist EPIC-07 formal verifiziert.
   Bei rotem Test: `McpServerCommand*Tests.cs` evtl. von den
   neuen E2E-Tests beeinflusst (selbe `[Collection]`-Race-
   Risiken trotz TD-003-Fix, A3 ist verifiziert aber Last noch
   nicht).
3. **Kritiker-Aufruf für 007** (war zum Stopp-Zeitpunkt
   ausstehend) — Verdict zu 9 neuen Tests, 4 Commits, 2
   TD-Schließungen (TD-003 + TD-015) + 1 TD-Teilschluss (TD-016)
   + 1 Plan-Abweichung (CliBatchRegression Exit-Code).
4. **TD-016a-Eintrag in `tech-debt.md`** ergänzen, falls
   bestätigt wird, dass CompileErrorMini- und GitImpactMini-
   Workspace noch die duplizierten Helper tragen
   (Coder-Bericht prüfen, dann Index-Zeile hinzufügen).
5. **Push der 4 Commits** nach erfolgreichem Kritiker-`approved`
   (per A4 erlaubt: Push ja, Amend nein).
6. **Nächste Einheit (008):** Planer-Aufruf. Kandidaten:
   EPIC-08 (Doku: `Docs/agent-api.md`, `Docs/integration.md`,
   `Docs/ROADMAP.md`, `README.md`) — letzte offene P0-Säule
   nach EPIC-06+EPIC-07. Oder die P0/P1-Rest-Erweiterungen
   (Kaltstart, Auto-Discovery, Staleness-Sweep-`mtime`,
   `--mcp-log`, Verzeichnis-Sweep, `ILintConsole`). Planer
   entscheidet JIT.

### Tech-Debt-Stand zum Mitnehmen (Kurzfassung)

- Offene Punkte struktureller Natur: TD-001 (ungenutzte
  transitive Dep), TD-002 (Subprozess-E2E), TD-004
  (Footprint-Druck Registrar), TD-005 (Server-Param-Pull-in),
  TD-006 (Dateiscan-Duplikation), TD-007 (5-Param-Methode
  in McpCodeGraphServer), TD-008 (`PathOverrides`-Pragmatik
  vs. `ILinterEngineConfig`-Refactor), TD-009 (5/5
  Konstruktor-Deps am Limit), TD-010 (SearchPatternTool
  Footprint knapp), TD-011 (5. Registrar-Klasse beim nächsten
  Symbolgraph-Tool zwingend), TD-014 (McpServerOptionsFactory
  knapp).
- Plus **TD-016a** neu (2 von 4 Fixtures noch nicht refaktoriert).
- Plus die P0/P1-Rest-Erweiterungen aus `konzept.md` Z. 207-324
  (alle noch offen).
- Plus `get_symbol_body` + stabile Symbol-IDs (P2-Backlog
  aus `tasks/codegraph-mcp-next/Konzept.md`, explizit
  außerhalb dieses Tasks).

### Resümee

Coder hat die Aufgabe formal sauber umgesetzt (3/4 Commits sind
geplant, der 4. ist ein Hash-Fixup; alle A3-Nachweise dokumentiert;
alle 6 EPIC-07-DoD-Bereiche mit jeweils mindestens 1 Test
abgesichert; 2 TD-Einträge sauber geschlossen, 1 teilsgeschlossen
mit dokumentierter Folge-Aufgabe). Was fehlt für den formalen
Abschluss: **Volllauf-Verifikation + Kritiker-Review + Push**.

---

## Phase 2 — Fortsetzung (2026-08-02, ~17:00)

### Eingang des neuen Orchestrator-Laufs

**User-Hinweis (Ralf, 2026-08-02, ~16:59):**

- Tests wurden umstrukturiert — MCP-Tools jetzt **direkt per C#** testbar
  (keine Python-Skripte mehr nötig).
- Explizite **Test-Kategorien** (`Category=Unit` / `Category=Integration`)
  verwenden, weil die Tests teils ewig dauern.
- Build ist grün.

### Verifikation der neuen Test-Infrastruktur (gelesen, 2026-08-02, 17:01)

Commits `3b315c2` + `4f6fa6f` (von Ralf nach 007 manuell eingespielt):

- **`src/AiNetLinter.Tests/Mcp/McpTestClient.cs`** (NEU, 114 Z.):
  Sauberer C#-Harness für E2E-Tests via `StdioClientTransport` zum
  kompilierten `AiNetLinter.exe --mcp-server`. Methoden:
  `ConnectAsync(targetDir)` / `CallToolAsync(tool, args)` /
  `CallToolGetTextAsync(tool, args)` / `ListToolsAsync()` /
  `DisposeAsync()`. Ersetzt die ad-hoc Python-Dogfooding-Skripte.
- **`src/AiNetLinter.Tests/Fixtures/BaselineMcpFixture.cs`** (NEU, 34 Z.):
  `IClassFixture<BaselineMcpFixture>` — verbindet einmalig pro Testklasse
  einen `McpTestClient` gegen `BaselineMiniFixtureWorkspace`.
- **`src/AiNetLinter.Tests/Fixtures/SymbolGraphMcpFixture.cs`** (NEU, 34 Z.):
  Pendant für `SymbolGraphMiniFixtureWorkspace`.
- **`src/AiNetLinter.Tests/Fixtures/McpLiveRepositoryFixture.cs`** (NEU,
  47 Z.): Pendant gegen das echte `AiNetLinter.slnx` (findet Repo-Root
  via `AppContext.BaseDirectory`-Walk).
- **`src/AiNetLinter.Tests/Fixtures/BaselineCatalogFixture.cs`** (NEU,
  30 Z.) + **`SymbolGraphCatalogFixture.cs`** (NEU, 30 Z.):
  Pendant für **Unit-Tests** — `IClassFixture<*>` liefert einmal pro
  Testklasse einen geladenen `SourceFileCatalog`, kein Server-Subprozess
  nötig.
- **`src/AiNetLinter.Tests/Mcp/McpServerAllToolsE2ETests.cs`** (NEU,
  182 Z., 14 Tests): alle 9 Tools via `SymbolGraphMcpFixture` E2E.
  `[Trait("Category", "Integration")]`.
- **`src/AiNetLinter.Tests/Mcp/McpLiveRepositoryTests.cs`** (NEU, 145 Z.,
  9 Tests): Live-Dogfooding-Tests gegen `AiNetLinter.slnx`.
  `[Trait("Category", "Integration")]`.
- **Test-Kategorien:** 9 Test-Klassen markiert mit `Category=Integration`,
  18 Test-Klassen mit `Category=Unit`. Subagenten können
  `dotnet test --filter Category=Unit` für schnelle Iterationen nutzen
  (80 Tests in ~21s statt 7+ min im Volllauf).
- **AGENTS.md §2 aktualisiert** (Commit `d3c4da8`): Test-Kategorien
  als verpflichtender Workflow dokumentiert. Subagenten sind angewiesen,
  `Category=Unit` während der Entwicklung zu nutzen.

### Aktuelle Lage (2026-08-02, 17:03)

- **Working tree:** clean.
- **Branch:** `main`, 3 commits ahead of `origin/main`:
  - `3b315c2` test(infra): introduce shared class fixtures (Ralf)
  - `4f6fa6f` perf(tests): enable multi-core parallelization (Ralf)
  - `ed58ba0` chore(task): state update nach unit 007 (letzte Session)
- **Hinweis Commit-Disziplin:** die 3 neuen Commits haben **kein**
  `[codegraph-mcp-server]`-Suffix — User hat sie manuell eingespielt,
  nicht der Coder-Agent. Außerhalb des Orchestrator-Workflows
  entstanden, deshalb kein Verstoß gegen A4. Suffix ist
  Konventionssache, kein Hard-Requirement für extern hinzugefügte Commits.
- **Build:** grün, 0 Warnungen, 0 Fehler (User-Bestätigung).
- **Tests gezielt (Unit-Slice):** 80/80 grün in 21s, gemessen
  2026-08-02 17:03 (`dotnet test --no-build --filter "Category=Unit"`).
- **Volllauf:** läuft im Hintergrund (gestartet 17:03, erwartet
  ~8-10 min wegen MCP-Subprozess-Starts, 1130+ Tests).

### Konsequenz für den Orchestrator-Lauf

**Was sich ändert vs. 001-007:**

1. **Subagenten-Workspace-Anchor** (siehe oben) bleibt
   Pflicht-Bestandteil jedes Subagenten-Prompts — die Hinweise aus 004+
   werden fortgeführt.
2. **Test-Kategorie-Hinweis wird ergänzt:** Subagenten bekommen den
   expliziten Hinweis, `Category=Unit` während der Entwicklung zu
   nutzen und nur vor Task-Beendigung (bzw. für A3-Nachweis neuer
   Integration-Tests) den Volllauf zu fahren. AGENTS.md §2 ist
   verbindlich.
3. **MCP-Tool-Tests jetzt in C#:** Statt Python-Skripte für
   Tool-Smoke-Tests können Planer/Coder/Kritiker den
   `McpTestClient` + `*McpFixture` direkt in xUnit-Tests verwenden.
   Das ist die bevorzugte Test-Form für künftige Tool-Schritte.
4. **`McpLiveRepositoryFixture` ersetzt Python-Dogfooding:** Das
   im `konzept.md` Z. 193-204 geforderte Dogfooding pro Tool-Step
   gegen die echte `AiNetLinter.slnx` ist jetzt ein xUnit-Test
   (`McpLiveRepositoryTests` als Vorlage), kein manuelles Skript
   mehr nötig. Coder können diese Tests als Vorlage kopieren
   und für Tool-spezifische Live-Assertions erweitern.

### Einheit 007 — Kritiker-Review (abgeschlossen 2026-08-02, ~17:18)

- **Verdict:** **`approved`** (0 CRITICAL, 0 MAJOR, 1 MINOR).
- **Review:** `units/007/review.md` (229 Z.) — Plan-Erfüllung 100 %,
  alle 6 EPIC-07-DoD-Bereiche (a–f) abgesichert + 2 Bonus-Tests.
  TD-003 strukturell korrekt (Check-Lock-Check), TD-015 sauber weg,
  TD-016 transparent als Teilschluss dokumentiert. `McpServerCommand
  Tests.cs` 0 Zeilen Diff in `3b29d72`. 4 Commits A4-konform
  (kein Push, kein Amend, kein `-A`).
- **MINOR:** Methodenname `..._ExitsZero` in
  `CliBatchRegressionTests.cs:32` bei tatsächlich assertiertem
  Exit 1 — kosmetisch, kein Build-/Test-Impact. Folge-Rename
  bei nächster Gelegenheit.
- **Volllauf-Verifikation (AGENTS.md §2):** 1161/1161 grün in
  5:55 min, gemessen 2026-08-02 17:10. AGENTS.md-Pflicht vor
  Task-Beendigung erfüllt.
- **TD-003 Status-Update** in `tech-debt.md`:
  Index-Zeile + Status-Block auf „geschlossen durch 007
  (Commit `49feb65`)" gesetzt.
- **TD-016a neu** in `tech-debt.md` aufgenommen: Folge-Refactor
  für `CompileErrorMiniFixtureWorkspace` (71 Z.) und
  `GitImpactMiniFixtureWorkspace` (166 Z.) — duplizieren
  weiterhin `CopyFixture`/`IsGeneratedPath`/`FindSolutionRoot`.
  Vorschlag: standalone (~1-2h) oder inline beim nächsten
  Fixture-Block. Risiko Git-Init-Logik in GitImpactMini.
- **Aufrufe:** 1× Kritiker (für 007) = jetzt 22/40.

### Einheit 008 — EPIC-08 Doku (abgeschlossen 2026-08-02, ~18:25)

- **Plan:** `units/008/plan.md` (880 Z., Commit `9247951`) — Wahl
  fiel auf EPIC-08 (Doku, einzige noch offene Muss-Have-Säule aus
  `konzept.md` Z. 105-107). 2 Konzept-Diskrepanzen vom Planer
  dokumentiert (A7-konform).
- **Coder-Result:** `units/008/result.md` — 4 Doku-Dateien erweitert
  (+232 Z. gesamt) + 1 neue A3-Verifikations-Test-Datei
  (`McpDocumentationSmokeTests.cs`, 73 Z., 3 Smoke-Tests gegen den
  laufenden MCP-Server). 7 Commits, alle A4-konform. Volllauf
  1164/1164 grün in 6:50 min (gemessen 2026-08-02 ~18:18). 3
  Konzept-Diskrepanzen dokumentiert (2 vom Planer + 1 vom Coder
  selbst entdeckt, Z. 564).
- **Kritiker-Review:** `units/008/review.md` (Commit `5196623`) —
  Verdict **`issues`**: 1 MAJOR F-001 (`Docs/agent-api.md:238`
  zählte „7 Tools sind C#-only", Tabelle listet aber 6), 3 MINOR
  (F-002 A3-Asymmetrie, F-003 Self-Lint-Pfad-Differenz, F-004
  Klammer-Inkonsistenz als Wurzel von F-001). Sonst alles sauber:
  1164/1164 grün, Doku-Treue 1:1 zum Code, A3-Tests mit
  dokumentiertem Pfad, A7 eingehalten.
- **Aufrufe:** 1× Planer (008) + 1× Coder (008) + 1× Kritiker (008)
  = 25/40.

### Einheit 008/fix-01 — Doku-Drift F-001 (abgeschlossen 2026-08-02, ~19:06)

- **Plan:** `units/008/fix-01/plan.md` (446 Z., Commit `96f1029`) —
  Pflicht-Fix F-001 (`agent-api.md:238` wortwörtlich 1:1 nach
  Kritiker-Korrekturtext), optional F-002 (A3-Symmetrie in
  `result.md`) + optional 4. A3-Wortlaut-Test.
- **Coder-Result:** `units/008/fix-01/result.md` — 5 Commits:
  1× `docs(mcp): agent-api C#-only-zaehlung korrigiert`
  (`700eb4e`), 1× `test(mcp): doku-zaehlung-vs-agent-api-md-test`
  (`5593c91`), 1× `chore(task): a3-block-symmetrie` (`28d2c5e`),
  2× `chore(task): result + hash-nachtrag` (`669b07d`, `77e5ebf`).
  F-001 + F-002 + A3-4 alle umgesetzt. Volllauf 1165/1165 grün in
  4:57 min (gemessen 2026-08-02 ~19:00). Plan-Abweichung: File-Read
  im 4. Test statt hartkodierter String (methodisch korrekter, im
  Test-Kommentar Z. 82-85 und im result.md begründet).
- **Kritiker-Review:** `units/008/fix-01/review.md` (Commit
  `31d11ac`) — Verdict **`approved`**: 0/0/1, MINOR kritisiert den
  Plan (Plan widerspricht sich intern zwischen hartkodiertem String
  und A3-Methode), Coder hat methodisch korrekte Wahl getroffen.
- **MINOR F-003** (Self-Lint-Pfad-Differenz Plan `tests/Fixtures/...`
  vs. Result `src/BaselineMini/...`): Inhaltlich konsistent (1
  gewollte Fixture-Violation, kein Regress), nur Pfad-Hinweis
  inkonsistent. **Hinweis für künftige Planer** (ab 009+): wenn
  Self-Lint gegen `BaselineMini` geplant wird, ist der reale
  Pfad **`src/BaselineMini/ViolatingClass.cs`** (1 erwartete
  Violation in `EnforceSealedClasses`), nicht `tests/Fixtures/...`.
- **Push:** 17 Commits (4× 007, 3× Ralf, 7× 008, 3× Orchestrator
  007/008) am 2026-08-02 ~19:03 nach `origin/main` gepusht.
- **Aufrufe:** 1× Planer + 1× Coder + 1× Kritiker = 28/40.

### Einheit 009 — TD-016a Folge-Refactor (abgeschlossen 2026-08-02, ~19:50)

- **Plan:** `units/009/plan.md` (574 Z., Commit `39c4caa`) — Wahl
  fiel auf TD-016a, weil Konzept-DoD met ist (EPIC-01..08 alle
  approved) und TD-016a die kleinste echte Coder-pflichtige Arbeit
  ist. Konzept-Diskrepanzen aus 008 sind explizit User-pflichtig
  (A7), P0/P1-Rest sind alle "optional", keine P0-Pflicht offen.
- **Coder-Result:** `units/009/result.md` — 4 Commits:
  1× `refactor(tests): CompileErrorMini + GitImpactMini auf
  FixtureWorkspaceBase umstellen` (`b0c2283`), 1× `test(tests):
  TD-016a fixture-base refactor regression-schutz
  (reflection-tests)` (`8f0427e`), 1× `chore(debt): TD-016a
  geschlossen durch 009` (`0535660`), 1× `chore(task): unit 009
  result` (`5ea191e`). CompileErrorMini 71→21 Z.,
  GitImpactMini 166→118 Z. (besser als Plan-Schätzung).
  Volllauf **1173/1173** grün in 6:20 min (vor 009: 1165, +8
  Reflection-Invokationen). **A3 echt gefahren mit 3 Schichten**:
  A3-1 Vererbungs-Reflection, A3-2 Helper-Entfernungs-Reflection
  (CS0108-Compiler-Bonus entdeckt und dokumentiert), funktionale
  A3 über 14+ bestehende Tests.
- **Kritiker-Review:** `units/009/review.md` (Commit `0b4e323`) —
  Verdict **`approved`**: 0/0/1, MINOR M1 = Zeilenzahlen
  25/114→21/118 in `tech-debt.md` (mit dem Review-Commit in
  `0b4e323` inline korrigiert). Sonst alles sauber.
- **TD-016a Status-Update** in `tech-debt.md`:
  Index-Zeile + Eintrag-Body auf „geschlossen durch 009
  (Commits `b0c2283` + `8f0427e`)" gesetzt, CS0108-Beobachtung
  im Body dokumentiert.
- **Hinweis vom Kritiker:** Zwischen 009-Ende und Review-Commit
  hat Ralf einen `894be8b docs(rules)` Commit beigetragen
  (AGENTS.md auf Pointer eindampfen, Regeln in `.agents/rules`
  konsolidieren). Berührt 009 nicht, soll separat gepusht werden.
- **Push:** folgt jetzt (siehe nächste Aktion).
- **Aufrufe:** 1× Planer + 1× Coder + 1× Kritiker = 31/40.

### Nächste Aktion (für 010)

1. **Push** der 6 009-Commits + Ralf's `894be8b` nach
   `origin/main` — A4 erlaubt Push, kein Amend. (Ralf-Commit
   separat behandelt, wie vom Kritiker empfohlen, weil er
   außerhalb des 009-Scopes liegt und vom User committed wurde.)
2. **Planer für Einheit 010** aufrufen. Kandidaten (in Reihenfolge
   der Kritiker-Empfehlung in 009):
   - **(A1) `rules.json`-Auto-Discovery** (P0, Konzept Z. 257-264):
     Ohne `--config` neben der Solution nach `rules.json` suchen,
     `[WARN]` auf stderr + Vermerk in `get_violations`-Antwort.
     ~2-3h, kleinste P0, risikoarm, vor A4 (Kaltstart) weil
     A4 TD-009 triggert.
   - **Konzept-Pflege-Einheit** (3 veraltete Stellen aus 008
     angeregt: `konzept.md` Z. 539-552, 550, 564) — ~1h, sauber,
     niedrigste Risiko. **VOR** A1 sinnvoll, damit A1 nicht auf
     veralteten Konzept-Annahmen implementiert wird.
   - **(A4) Kaltstart entkoppeln** (P0, Konzept Z. 265-275) — ~4-6h,
     triggert TD-009 (McpCodeGraphServer-Konstruktor am Limit).
     **Doppeleinheit-Setup** mit TD-009-Refactor nötig.
   - **(A2) Verzeichnis-Sweep** (P1) + **(A3) Staleness-mtime**
     (P1) — gekoppelt, gleiche Sweep-Mechanik.
   - **(A5) `--mcp-log`** (P1) + **(A6) `ILintConsole`** (P1) +
     **(A7) Last-Fixture** (P1, braucht A4 als Voraussetzung) —
     später.
   - Andere Tech-Debt-Refactors (TD-008/010, TD-014) — inline
     in den jeweiligen P0/P1-Erweiterungen, nicht eigenständig.
   Konkrete Wahl trifft der Planer JIT (Kernel Teil B "Drift"),
   aber Strategie-Empfehlung: **Konzept-Pflege zuerst, dann A1**.

### Tech-Debt-Stand zum Mitnehmen (Kurzfassung)

Nach 007 + Review:

- **TD-003** geschlossen (Commit `49feb65`, 007) — Status aktualisiert
- **TD-012** geschlossen (Commit `c6261ea`, 004)
- **TD-013** geschlossen (Commit `c6261ea`, 004)
- **TD-015** geschlossen (Commit `3b29d72`, 007)
- **TD-016** geschlossen mit Teilschluss-Anmerkung (Commit `6c872e4`,
  vor 007) — TD-016a ist Folge-Refactor für die 2 verbleibenden
  Fixtures
- **TD-016a** NEU aufgenommen aus 007-Review
- Alle anderen Einträge TD-001, TD-002, TD-004, TD-005, TD-006,
  TD-007, TD-008, TD-009, TD-010, TD-011, TD-014 weiterhin offen.

### Einheit 010 — Konzept-Pflege (abgeschlossen 2026-08-02, ~20:35)

- **Plan:** `units/010/plan.md` (778 Z., Commit `9368011`) — Wahl
  fiel auf Konzept-Pflege, weil der 009-Kritiker explizit
  empfohlen hat, vor A1-Erweiterungen die 3 veralteten Konzept-
  Stellen aus 008 zu beheben. A7-Aufhebung: Planer hat die
  wortwörtlichen Korrekturen vorgegeben, Coder durfte `konzept.md`
  editieren.
- **Coder-Result:** `units/010/result.md` — 4 Commits:
  1× `docs(mcp): konzept tool-status-tabelle + server-betrieb
  an code-stand angepasst` (`84f4dc3`), 1× `test(mcp):
  konzept-reflection-tests gegen code-drift` (`f913bda`),
  2× `chore(task): result + hash-nachtrag` (`62e58c0`,
  `a4bc708`). 4/4 Korrekturen wortwörtlich 1:1 (Z. 546/550/551/
  559-560), 5/5 Reflection-Tests, alle A3 echt. Volllauf
  1178/1178 grün (Lauf 2 nach Flake in Lauf 1; 1. Lauf hatte
  `SymbolGraphMcpFixture`-Timeout bei 16 parallelen
  Test-Collections, Re-Run in 1s grün, **kein 010-Regress**).
  3 begründete Plan-Abweichungen (separate Test-Datei, Regex statt
  `Assert.Contains` für Markdown-Bold, voll-qualifizierter
  `Regex`-Typ).
- **Kritiker-Review:** `units/010/review.md` (Commit `b2a88b4`) —
  Verdict **`approved`**: 0/0/5, alle MINOR. Volllauf-Flake
  als **pre-existing** gewertet, **kein 010-Regress**.
- **TD-019 neu** in `tech-debt.md`: parallele MCP-Server-Init-
  Stabilität in `SymbolGraphMcpFixture`. Niedrig.
- **Aufrufe:** 1× Planer + 1× Coder + 1× Kritiker = 34/40.

### Strategie für 011 (Tech-Debt-Bündel)

**User-Anweisung (Ralf, 2026-08-02, ~20:45):** "Ok, schließe das
dann komplett ab - gebündelt." — keine Mini-Steps mehr, sondern
einen echten Brocken mit mehreren Tech-Debt-Aktionen in einer
Einheit.

**Bündel für 011 (vom Orchestrator vorgeschlagen):**

- **TD-009** (Pflicht): `McpCodeGraphServer`-Konstruktor auf
  Input-`record` umstellen (`internal sealed record
  McpCodeGraphServerOptions`). Löst 5/5-Limit-Reserve-Problem
  für nachfolgende P0/P1-Erweiterungen (Kaltstart, Auto-Discovery).
- **TD-014** (Pflicht): `McpServerOptionsFactory` aufteilen in
  `McpServerOptionsBuilder` + Factory. Footprint-Reserve für
  `--mcp-log` (A5), Auto-Discovery (A1) und Konzept-
  Erweiterungen.
- **TD-019** (Pflicht, falls Zeit): parallele MCP-Init-Stabilität
  in `SymbolGraphMcpFixture` — Lock-Pattern oder sequenzielle
  Init-Phase.
- **Optional TD-008/TD-010** (`ILinterEngineConfig`-Interface):
  größerer Refactor (4-6h), kann in 011 mitgenommen werden,
  wenn die Coder-Kapazität reicht.

**Achtung:** Volllauf muss am Ende grün bleiben (1178+mind =
1178). Budget nach 011: 34/40 + 3 (Planer/Coder/Kritiker) = 37/40,
3/40 verbleibend — genug für genau eine weitere Einheit oder
Task-Abschluss mit `summary.md`.

### Nächste Aktion

1. **Push** der 5 lokalen Commits nach `origin/main` (4× 010
   + 1× review) — A4 erlaubt.
2. **Planer für Einheit 011 = TD-Bündel** aufrufen.

### Verbrauchtes Aufruf-Budget (aktualisiert 20:45)

| Größe | Default | Verbraucht | Verbleibend |
| :--- | :---: | :---: | :---: |
| `max_aufrufe` | 40 | 34 (zuzügl. 011-Planer/Coder/Kritiker = 37 nach 011) | 6 (3 nach 011) |
| `max_fix_pro_einheit` | 3 | 0 (in 006) | 3 |
| `max_fix_gesamt` | 12 | 1 (002/fix-01) | 11 |
| `max_fix_gesamt` | 12 | 1 (002/fix-01) | 11 |
