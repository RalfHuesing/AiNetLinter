---
task: flaky-and-test-performance
completed_at: 2026-08-08T23:59:00+02:00
final_status: done
total_iterations: 20
total_commits: 101
total_epics: 8
total_tech_debt_entries: 12
---

# Task Summary: flaky-and-test-performance

## Ergebnis

Der Task hat beide in `konzept.md` formulierten Kernprobleme strukturell
adressiert: Ein dokumentierter, verifizierter Fast-Path (`dotnet test
--filter Category=Unit`, alle 1193 Unit-Tests, EPIC-04) existiert und
deckt den täglichen Feedback-Loop ab; alle ~1087 Testmethoden tragen jetzt
lückenlos Category-Traits (1193 Unit + 132 Integration = 1325, EPIC-02,
strukturell vom Kritiker verifiziert); Fixture-Sharing für die am
stärksten duplizierten Fixtures wurde umgesetzt (EPIC-03); der bekannte
Flaky Test wurde strukturell (Event-/`Task.WhenAny`-basiert statt
Poll-Loop mit fixer Deadline) gefixt und in kumulativ 14 vollen
Testläufen ohne Fehlschlag verifiziert (EPIC-06). Kein Verlust an
Testabdeckung (1325 Tests durchgehend, keine gestrichenen Assertions).
Einzig der quantitative Vorher/Nachher-Vergleich (DoD-Punkt 2, "spürbar
kürzer als ~90s-Baseline") ist am Ende **nicht belastbar** verifiziert —
die Abschlussmessung (step-020, 200s) lief auf anderer Hardware
(Notebook) als alle Referenzwerte (Arbeits-PC), macht den Vergleich
methodisch untauglich. Der Nutzer hat dies akzeptiert und explizit keine
weitere Messung mehr gewünscht. Das Ergebnis passt insgesamt zur
ursprünglichen Intention aus `konzept.md`, mit diesem einen ehrlich
dokumentierten, nicht auflösbaren Restpunkt.

## Roadmap-Status

Alle 8 Epics in `roadmap.md` sind abgehakt — 6 davon vollständig
umgesetzt und verifiziert (EPIC-01 Spike, EPIC-02 Traits, EPIC-03
Fixture-Sharing, EPIC-04 Fast-Path, EPIC-06 Flaky-Fix, EPIC-08
Abschluss-Validierung), 1 bewusst als Nice-to-Have **obsolet** markiert
mit nachvollziehbarer fachlicher Begründung (EPIC-05 — Muss-Haben bereits
durch EPIC-03/04 erfüllt, verbleibendes Potenzial spekulativ gegenüber
Produktionscode-Risiko), 1 als **nicht umsetzbar verworfen** (EPIC-07 —
`ConsoleTestCollection` ist zwingend erforderlich für 5 `Console.Out`-
umleitende Testklassen, in step-018 korrekt wiederhergestellt). Kein
offener, unbearbeiteter Epic-Rest. Details siehe `roadmap.md`.

## Steps-Übersicht

| Step | Epic | Status | Title | Commit | Notiz |
|------|------|--------|-------|--------|-------|
| step-001 | EPIC-01 | done | Spike — SymbolGraphMcpFixture auf ICollectionFixture | `bf5de7e`/`cc395d0` | approved |
| step-002 | EPIC-02 | done | Traits Suppression/ (Batch 1) | `3ae94c2`/`79d3d6d` | approved |
| step-003 | EPIC-02 | done | Traits Metrics/ (Batch 2) | `67fb86b`/`03b04f4` | approved |
| step-004 | EPIC-02 | done | Traits Web/ (Batch 3) | `57f7f03`/`ecd9dfa` | approved |
| step-005 | EPIC-02 | done | Traits Arch/Diag/FP/Cache (Batch 4) | `b15a198`/`fe95a08` | approved |
| step-006 | EPIC-02 | done | Traits Evals/ (Batch 5) | `f88c223`/`5d7df9b` | approved |
| step-007 | EPIC-02 | done | Traits Output/ Teil 1 (Batch 6a) | `9c4269f`/`a2e9b3f` | approved; TD-003 beobachtet |
| step-008 | EPIC-02 | done | Traits Output/ Teil 2 (Batch 6b) | `95ab4d5`/`b23a4cf` | approved |
| step-009 | EPIC-02 | done | Traits Configuration/ (Batch 7) | `b484627`/`b4a8c59` | approved; TD-005 eleviert |
| step-010 | EPIC-02 | done | Traits Core/Checkers/ Teil 1 (Batch 8a) | `44956b7`/`2674a46` | approved; TD-006 beobachtet |
| step-011 | EPIC-02 | done | Traits Core/Checkers/+Core/ (Mega-Batch 20) | `bb39619`/`2a4067a`/`daad777` | approved; TD-002 (Subject-Länge) |
| step-012 | EPIC-02 | done | Traits Core/+Maps/ (Mega-Batch 17) | `b2477f5`/`7deeff1` | approved (Review: Cursor Grok 4.5) |
| step-013 | EPIC-02 | done | Traits Mcp/Tools/ + TD-007-Löschung | `0d5cee2`/`5c4600c` | approved; TD-007 erledigt |
| step-014 | EPIC-02 | done | Rest-Batch Traits (Mega-Batch 20) | `c46d839`/`98e2e9a` | approved |
| step-015 | EPIC-02 | done | Traits McpServerCommandTests.cs (letzter EPIC-02-Step) | `2cf236f`/`e1d316b` | approved; EPIC-02 vollständig |
| step-016 | EPIC-03 | done | Fixture-Sharing SymbolGraphCatalog(18×)+McpLiveRepository(2×) | `6dfd588`/`39991a2` | approved, gegengeprüft (3 eigene Läufe) |
| step-017 | EPIC-04 | done | Fast-Path-Befehl etablieren + Doku | local | kein Kritiker-Review-File (Gemini-Session) |
| step-018 | EPIC-07 | reverted | ConsoleTestCollection entfernen → wiederhergestellt | local | Selbst-Korrektur, kein Kritiker-Review-File |
| step-019 | EPIC-06 | done | Flaky-Test strukturell fixen | `6ee3bbe`/`a47774f` | approved |
| step-020 | EPIC-08 | done | Abschluss-Validierung | `a41d910` | approved (nach Revision, s. u.) |

## Globale Audit-Befunde (Kritiker, Modus `global`)

### Konzept erfüllt?

Alle Muss-Haben-Punkte aus `konzept.md` sind adressiert:
- Fast-Path-Feedback-Loop existiert und ist dokumentiert (EPIC-04).
- Category-Traits auf 100 % der Tests (1193 Unit + 132 Integration =
  1325, kein Rest — projektweit vom Kritiker in step-015 strukturell über
  alle 202 Testdateien verifiziert, nicht nur numerisch).
- Fixture-Sharing für die identifizierten Duplikate (`SymbolGraphCatalogFixture`
  18×, `McpLiveRepositoryFixture` 2×) umgesetzt, Dispose-Risiko dabei
  aktiv behoben.
- Flaky Test strukturell gefixt (Event-/`Task.WhenAny`-Warten statt
  Poll-Loop), 14 kumulierte volle Läufe ohne Fehlschlag der Zieltests.
- Kein Testabdeckungsverlust (1325 Tests durchgehend).
- Kein Non-Goal verletzt (kein Framework-Wechsel, kein sichtbares
  CLI-/MCP-Verhalten geändert — durchgehend in den Step-Reviews geprüft).

**Einzige echte Lücke:** DoD-Punkt 2 ("Voller Testlauf spürbar kürzer als
~90s-Baseline") ist am Task-Ende **nicht belastbar verifiziert** — nicht
weil er nachweislich verfehlt wurde, sondern weil die Abschlussmessung
(step-020, 200s Median) auf einer anderen Maschine (Notebook) lief als
alle drei Referenzwerte (Arbeits-PC, 32 Kerne @ 5,5 GHz: ~90s
Konzept-Baseline, 97,75s step-016, ~175s step-019). Der step-020-Kritiker
hatte das zunächst als `MAJOR`-Regressions-Finding gewertet (`issues`),
nach Nutzer-Klarstellung zur Hardware-Diskrepanz aber korrekt auf
"nicht abschließend vergleichbar gemessen" revidiert und auf `approved`
zurückgesetzt (Revisions-Historie vollständig in `step-020/step-review.md`
dokumentiert, keine stille Korrektur). Diese Revision ist nachvollziehbar
und sauber begründet — kein Kritikpunkt an der Vorgehensweise selbst.
Unabhängig von der Hardware-Frage bleibt zusätzlich eine ungeklärte
same-hardware-Diskrepanz `step-016` (97,75s) → `step-019` (~175s, beide
Arbeits-PC, +79 %) offen — dokumentiert in TD-012, auf Nutzer-Wunsch ohne
weitere Messung abgeschlossen.

### Seiteneffekte / Regressionen

- `dotnet build` (selbst ausgeführt, Solution-Root): **grün, 0 Fehler**.
  Ein erster Lauf schlug mit `MSB3027`/`MSB3026` (DLL-Lock durch
  verwaiste `testhost`/`AiNetLinter.Tests`-Prozesse) fehl — ein zweiter
  Lauf nach kurzer Wartezeit war grün. Dies ist eine **direkte,
  selbst beobachtete Live-Reproduktion von TD-009** (verwaiste
  `AiNetLinter.exe`/Testprozesse sterben unter Windows nicht zuverlässig
  mit dem Eltern-Testprozess und blockieren nachfolgende Builds) —
  bestätigt die dort dokumentierte Einschätzung, kein neuer Fund.
- Kein voller `dotnet test`-Lauf in diesem Review durchgeführt
  (explizite Auftragsvorgabe: keine weiteren Messungen mehr). Als
  Testevidenz dienen die bereits in den Step-Results/-Reviews
  dokumentierten Ergebnisse (durchgehend 1325/1325 grün in allen
  validen Läufen von step-016, step-019, step-020).
- Keine Regression in Produktionscode-Verhalten festgestellt: alle
  Produktionscode-Änderungen (`McpCodeGraphServer.cs` `LoadTask`-Property,
  Dispose-Kette) sind additiv bzw. strukturell lokal begrenzt, in den
  jeweiligen Step-Reviews unabhängig nachvollzogen (u. a. eigene
  Diff-Verifikation, eigene Wiederholungsläufe).

### Rules-Konformität (Stichproben)

Drei Steps quergeprüft (step-013 EPIC-02 Batch, step-016 EPIC-03
Fixture-Sharing, step-019 EPIC-06 Flaky-Fix) — in allen drei Fällen
bestätigen die jeweiligen Kritiker-Reviews konkrete, nachvollziehbare
Rules-Prüfungen (nicht nur Behauptungen): Commit-Subject-Längen einzeln
nachgezählt, `BanBlockingTaskAccess`/"Symptom-Fixing verboten" explizit
gegen den Diff geprüft, Parallelitäts-Erhalt (`parallelizeTestCollections`)
verifiziert, BOM/EOL-Byte-Scans durchgeführt. Keine Regelverletzung in
den drei Stichproben-Steps. Einzige wiederkehrende, bekannte und bereits
dokumentierte Abweichung ist die Commit-Subject-72-Zeichen-Grenze
(TD-002, Priorität niedrig, mehrfach vom Kritiker konsistent erkannt und
nie als Blocker gewertet).

**Beobachtung außerhalb der Stichprobe (kein neues Finding):** `step-017`
und `step-018` haben kein `step-review.md` — beide liefen offenbar in
einer Session außerhalb des regulären Planer→Coder→Kritiker-Zyklus
(`coded_by`/`reviewed_by` beide "gemini-3.6-flash", Commits als "local"
im `task-state.md` vermerkt). `step-018` hat sich dabei selbst korrekt
revertiert (Wiederherstellung von `ConsoleTestCollection`), inhaltlich
nachvollziehbar und mit den nachfolgenden Steps (step-019, step-020)
konsistent — aber ohne unabhängige Kritiker-Prüfung durchgelaufen. Da
beide Epics (EPIC-04, EPIC-07) durch nachfolgende Steps (step-019/020)
indirekt mitverifiziert wurden (voller Testlauf weiterhin grün, Self-Lint
`OK`) und keine Funktionsänderung mehr offen ist, ist das kein Blocker,
aber ein Prozess-Lückenhinweis für künftige Tasks (siehe Empfehlungen).

## Tech-Debt-Zusammenfassung

12 Einträge insgesamt erzeugt (TD-001 bis TD-012), davon 4 bereits
erledigt und aus dem Index entfernt (TD-001, TD-003, TD-004, TD-007 —
siehe `task-state.md`-Historie), **8 aktuell offen:**

- **Hoch:** 1 Eintrag — `TD-010` (reproduzierbare MSBuildWorkspace-/
  Subprozess-Hänger bei unabhängigen MCP-Integrationstests unter Last;
  Häufigkeit im Projektverlauf von 2/10 auf 5/8 Läufen gestiegen)
- **Mittel:** 3 Einträge — `TD-008` (kein Headroom beim
  `AIContextFootprint`-Limit in `AnalysisToolRegistrations.cs`), `TD-011`
  (Fast-Path-Zeitangabe in `AGENTS.md` mit aktueller Messung nicht
  reproduzierbar), `TD-012` (Task-Abschluss-Performance-Vergleich
  methodisch durch Hardware-Wechsel + ungeklärte same-hardware-Diskrepanz
  beeinträchtigt)
- **Niedrig:** 4 Einträge — `TD-002` (Commit-Subject-Längen-Disziplin),
  `TD-005`/`TD-006` (UTF-8-BOM-Inhomogenität in `Configuration/` bzw.
  `Core/Checkers/`+`Core/`), `TD-009` (verwaiste `AiNetLinter.exe`-/
  Testprozesse sterben nicht mit dem Eltern-Testprozess)

**Worauf sich ein Blick lohnt:** TD-010 (hoch) und TD-009 (niedrig, aber
in diesem Review selbst live reproduziert — siehe "Seiteneffekte" oben)
hängen wahrscheinlich mit demselben Grundmuster zusammen
(Subprozess-/Workspace-Kontention bzw. -Nachwirkung unter Last) und
könnten in einem Root-Cause-fokussierten Folge-Task gemeinsam betrachtet
werden. TD-011/TD-012 sind eng an TD-010 gekoppelt (alle drei betreffen
die Frage, wie belastbar Performance-Messungen in diesem Projekt aktuell
sind) und lösen sich möglicherweise mit auf, sobald TD-010 verstanden
ist.

## Offene Punkte

- [ ] DoD-Punkt 2 (voller Testlauf spürbar kürzer als ~90s-Baseline) ist
      nicht belastbar verifiziert — methodisch durch Hardware-Wechsel
      der Abschlussmessung beeinträchtigt (TD-012). Auf Nutzer-Wunsch
      keine weitere Messung in diesem Task.
- [ ] TD-010 (hoch): Root Cause der MCP-Integrationstest-Hänger unter
      Last nicht lokalisiert, nur symptomatisch dokumentiert.
- [ ] `AGENTS.md`-Fast-Path-Zeitangabe (~23-24s) veraltet/nicht
      reproduzierbar (TD-011) — bewusst nicht korrigiert, da die genaue
      Zahl von der noch offenen TD-010/TD-012-Klärung abhängt.
- [ ] `step-017`/`step-018` liefen ohne eigenständiges Kritiker-Review
      (siehe „Rules-Konformität" oben) — kein Blocker, da inhaltlich
      durch Folge-Steps mitverifiziert, aber ein Prozess-Lücken-Hinweis.

## Empfehlungen

- Falls die Performance-Frage später erneut relevant wird: eigener,
  fokussierter Folge-Task für TD-010 (Root-Cause MCP-Timeout-/
  Subprozess-Hänger) — vermutlich der wertvollste nächste Schritt, weil
  er sowohl TD-009 als auch TD-011/TD-012 indirekt mit klärt. Jede
  künftige Vergleichsmessung konsequent auf derselben Hardware wie die
  Baseline durchführen und die Hardware im Messprotokoll dokumentieren.
- TD-005/TD-006 (BOM-Inhomogenität) und TD-002 (Commit-Subject-Länge)
  sind niedrig priorisiert und unkritisch — können bei Gelegenheit
  (z. B. als kleines Aufräum-Item in einem ohnehin laufenden Test-Task)
  mit erledigt werden, kein eigener Task nötig.
- TD-008 (kein Headroom beim `AIContextFootprint`-Limit) vor dem
  nächsten Feature-Schritt an `McpCodeGraphServer.cs`/
  `AnalysisToolRegistrations.cs` klären (Override moderat anheben oder
  Registrierungsklasse aufteilen), sonst blockiert das Limit die nächste
  sinnvolle Änderung dort sofort.
- `AGENTS.md`-Fast-Path-Zeitangabe erst korrigieren, wenn TD-010/TD-012
  geklärt sind — eine vorschnelle Zahlenkorrektur würde sonst kurzfristig
  wieder veralten.

## Statistik

- **Anzahl Epics:** 8, davon abgehakt: 8 (6 vollständig umgesetzt, 1
  obsolet, 1 verworfen — alle nachvollziehbar begründet)
- **Anzahl Steps:** 20 (step-001 bis step-020)
- **Davon approved:** 18 (step-017/018 ohne separates Kritiker-Review,
  step-018 selbst-revertiert und inhaltlich konsistent)
- **Davon blocked:** 0
- **Anzahl Commits:** 101 (mit `[flaky-and-test-performance]`-Suffix bzw.
  Task-Bezug im Log)
- **Anzahl Tech-Debt-Einträge:** 12 insgesamt (8 offen, 4 erledigt/
  entfernt), davon `auto_fixable: ja`: 0 der aktuell offenen
- **Davon Korrektur-Steps:** 0 echte `corrects`-Ketten (step-018 ist ein
  Selbst-Revert desselben Steps, keine `corrects: step-NNN`-Kette)
- **Laufzeit:** 2026-08-07T08:55 bis 2026-08-08T23:59 (ca. 2 Kalendertage,
  mit einer bewussten Nutzer-Pause nach EPIC-03)
