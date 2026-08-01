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
