---
status: done
type: step-plan
task: 11_epic-projektregistry-und-daemon
step: 008
corrects: null
title: "EPIC-A-Abschluss: Drift-Audit, Overview-Liveprüfung und Meilenstein-Doku"
epic: EPIC-A
estimated_risk: medium
step_type: single
items: []
created_by: planer
created_by_model: GPT-5
created_by_model_knowledge_cutoff: nicht deklariert
created_at: 2026-08-24T00:48:56+02:00
related_to: ["step-007/step-result.md", "step-007/step-review.md", "step-003/step-plan.md"]
---

# Step 008: EPIC-A-Abschluss: Drift-Audit, Overview-Liveprüfung und Meilenstein-Doku

## Bezug

- **Task:** `11_epic-projektregistry-und-daemon`
- **Epic:** `EPIC-A` aus `roadmap.md` — fachlich implementiert und bis
  step-007 abgenommen; dieser Step schließt ausschließlich die verbleibenden
  Epic-DoD- und Entscheidungsregister-Punkte.
- **Konzept-Referenzen:** `tasks/mcp-server-weiterentwicklung/11_epic-projektregistry-und-daemon/Konzept.md`
  (A.4/A.8/A.9 und Review-5-Rückfallplan) sowie
  `tasks/mcp-server-weiterentwicklung/90_bewusst-nicht-umsetzen/Konzept.md` §D.4.
- **Scope-Grenze:** Genau ein flacher regulärer Abschluss-Step für EPIC-A;
  EPIC-B wird erst nach EPIC-A-Abnahme separat und JIT geplant.

## Aktueller Projektzustand (JIT-Kontext)

- Step-007 ist laut `step-007/step-review.md` `approved`. `step-result.md`
  dokumentiert einen grünen Build, den vollständigen Nicht-Stress-Nachweis und
  MCP-Gates für Registry, Wiring, Test-Seam und Fehlervertrag. Diese Nachweise
  werden nicht blind wiederholt; der Coder ergänzt nur den neuen Abschluss-
  Nachweis.
- Die semantische MCP-Prüfung des aktuellen C#-Stands bestätigt:
  `McpServerOptionsFactory` registriert sechs Tool-Gruppen und die
  `OverviewResourceRegistration`; `Register` verwendet das URI-Template
  `ainetlinter://overview?projectRoot=<url-encoded>`, und der Template-Handler
  holt pro Read einen Registry-Snapshot/Lease. `McpServerCommand.RunAsync`
  startet die Registry und den stdio-MCP-Server. Die geprüften Produktions-
  symbole (`OverviewResourceRegistration`, `McpServerOptionsFactory`,
  `ProjectRegistry`, `McpServerCommand`, `ServerInstructions`) haben aktuell
  keine Violations und liegen innerhalb der MCP-Metrikbudgets.
- Die bestehende Live-Testinfrastruktur ist wiederzuverwenden:
  `RepositoryMcpHostFixture` startet den echten Server gegen das Repo,
  `McpProcessHost` ergänzt bei Tool-Calls automatisch den absoluten
  `projectRoot`; der Wrapper bietet bisher jedoch nur Tool-Calls, keinen
  Resource-Read. Es existieren In-Memory-/Wiring-Tests und Raw-Wire-Discovery-
  Tests, aber kein echter Repository-Live-Test für den Overview-Resource-Read.
- Die Migration des eigenen Repos ist bereits vorhanden: Root-
  `ainetlinter.project.json`, `.mcp.json` und die externe
  `C:\Users\Ralf\AppData\Local\hermes\config.yaml` enthalten für `ainetlinter`
  nur `command` plus `--mcp-server`. Diese Registrierungen sind im Step
  read-only zu verifizieren; die externe Hermes-Datei ist kein Git-Artefakt.
- Das allgemeine Entscheidungsregister führt §D.4 weiterhin als „zurückgestellt“.
  Die Epic-Konzeptdatei dokumentiert bereits die belegte Wiederöffnung wegen
  der Hermes-Host-Realität; der Registereintrag und der Meilensteinstatus sind
  noch nicht nachgezogen.
- **DoD-Transparenz vor dem Step:**
  - erledigt: Loader/Registry/Wiring/Hard-Cut, eigene Repo-Migration,
    Fehler-/Eviction-Verträge sowie Contract- und Regressionstests (step-001
    bis step-007, Review-Status `approved`);
  - offen: einmalige Audit-Triage/Dokumentation, sichere Overview-Liveprüfung,
    `Docs/ROADMAP.md`, `00_uebersicht-und-entscheidungen.md` Zeile 11 und der
    §D.4-Wiederöffnungsvermerk;
  - nicht Scope: EPIC-B, allgemeiner Tech-Debt-Abbau und Änderungen an den
    fertigen Registry-Verträgen.

## Intention

Nach diesem Step ist EPIC-A nicht nur technisch umgesetzt, sondern auch anhand
der verbleibenden DoD-Punkte nachvollziehbar abgeschlossen. Die Live-Prüfung
belegt die tatsächliche Resource-Registrierung und das URL-kodierte
`projectRoot`-Routing über die vorhandene C#-MCP-Testinfrastruktur; die
Meilenstein- und Entscheidungsdokumente spiegeln danach ausschließlich den
implementierten Stand wider.

## Konkrete Änderungen

### C#-MCP-Liveprüfung: bestehende Testinfrastruktur erweitern

- **Dateien:**
  `src/AiNetLinter.IntegrationTests/Mcp/Platform/McpProcessHost.cs`,
  `src/AiNetLinter.IntegrationTests/Mcp/Platform/ReadOnlyMcpHostFixture.cs`
  und die passende Live-Testdatei unter
  `src/AiNetLinter.IntegrationTests/Mcp/McpLiveRepositoryTests.cs`.
- **Was:** Einen minimalen, auf dem vorhandenen MCP-SDK-Client basierenden
  Resource-Read-/Template-Read-Zugriff ergänzen oder den bereits vorhandenen
  Raw-Wire-Testanker gezielt wiederverwenden. Einen echten Repository-Live-Test
  für `ainetlinter://overview?projectRoot=<Uri.EscapeDataString(repoRoot)>`
  ausführen und den erfolgreichen `text/markdown`-Snapshot, den adressierten
  Root sowie Solution-/Regelstatus assertieren. Zusätzlich die registrierte
  Resource-/Template-Liste und die sechs Toolgruppen gegen die bestehende
  Inventory-Prüfung abgleichen.
- **Warum:** Die bisherigen In-Memory- und Wiring-Tests beweisen die
  Implementierung, aber nicht die Host-nahe Expansion und den tatsächlichen
  Resource-Read des laufenden Servers. Der vorhandene Fixture-/Client-Pfad
  vermeidet Ad-hoc-Skripte und behält die projektweite `projectRoot`-Injektion.
- **Sicherheitsgrenze:** `.mcp.json` und Hermes werden nur gelesen. Falls ein
  tatsächlich erreichbarer Host das Query-Template ablehnt, ist die konkrete
  Host-Antwort im Task-Log/`step-result.md` festzuhalten und der in Konzept
  A.4 erlaubte Resource→Tool-Rückfall als separate Entscheidung zu markieren;
  ohne belegten Hostfehler wird kein Tool-Ersatz erfunden.

### Audit-Befunde dokumentieren, nicht ungefragt refactoren

- **Dateien:** `step-008/step-result.md` und bei Bedarf append-only
  `../tech-debt.md`.
- **Was:** Die bereits einmalig ausgeführte Audit-Runde nachvollziehbar
  festhalten: tokenbasierter Scan (`src`, `minTokens=20`) mit einem exact-
  Cluster (`TrackingServerFactory.MinimalConfig` /
  `TestConfigFactory.CreateEmpty`), near-Kandidaten u. a. in den
  Registry-Tests; struktureller Scan mit beabsichtigten Test-Helfer-
  Kandidaten; Magic-Value-Scan mit vielen heuristischen Treffern; Dead-Code-
  Scan mit zwei LOW-Hinweisen (`ServerInstructions.FitsBudget`,
  `LinterErrorCodes.AmbiguousSolution`). EPIC-A-nahe Befunde sind als
  Beobachtung/Tech-Debt zu dokumentieren, nicht als eigener Step.
- **Warum:** Die Nutzervorgabe verlangt genau einen Drift-Audit pro Epic und
  verbietet Tech-Debt-Overhead. Die semantische Sichtung ordnet die
  Registry-/Test-Helfer als überwiegend testbezogen bzw. absichtlich
  unterschiedlich ein; kein Befund autorisiert hier eigenständige
  Produkt-Refactorings.

### Meilenstein- und Entscheidungsregister nachziehen

- **Dateien:** `Docs/ROADMAP.md`,
  `tasks/mcp-server-weiterentwicklung/00_uebersicht-und-entscheidungen.md`
  (die vorgesehene Zeile 11) und
  `tasks/mcp-server-weiterentwicklung/90_bewusst-nicht-umsetzen/Konzept.md` §D.4.
- **Was:** In `Docs/ROADMAP.md` einen sachlichen EPIC-A-Meilenstein mit
  Abschlussreferenz auf `step-008` ergänzen; in der Übersicht Zeile 11 den
  Meilenstein-/Statushinweis ergänzen; §D.4 vom alten „zurückgestellt“-Stand
  auf einen belegten Wiederöffnungs-/Umsetzungsvermerk für die
  transportneutrale Multi-Solution-Registry aktualisieren. Nur implementierte
  Tatsachen dokumentieren; keine EPIC-B-Ergebnisse vorwegnehmen.
- **Warum:** A.9 und die Sammelpflicht nennen diese drei Dokumentstellen
  ausdrücklich. Der §D.4-Vermerk muss die vorhandene Evidenz und die nun
  implementierte EPIC-A-Entscheidung widerspruchsfrei zum Register machen.

## Tests

- [ ] Gezielter Unit-/Component-Slice für die betroffenen Test-Harness- und
  Overview-Verträge während der Iteration; kein Stress-Test.
- [ ] Gezielter Integration-Slice für den neuen Repository-Live-Overview-Read
  sowie vorhandene Discovery-/Registration-Tests.
- [ ] `dotnet build` einmal als Abschluss-Gate.
- [ ] `dotnet test src/AiNetLinter.FastTests --filter Category!=Stress` genau
  einmal als vollständiger Nicht-Stress-Lauf.
- [ ] `dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress`
  genau einmal als vollständiger Nicht-Stress-Lauf.
- [ ] Vor jedem zukünftigen Commit: MCP-Quality-Gates für die geänderten
  C#-Scopes (`get_feature_context`/`get_impact` gezielt, anschließend
  `get_violations`, `safeguard`, `metrics_lookup`).
- [ ] Kritiker prüft `step-result.md`, die Doku-Diffs, Registrierungsdateien
  und die neuen Live-Assertions stichprobenartig; er wiederholt den
  vollständigen Nicht-Stress-Stack nicht.

## Definition of Done

- [ ] Die vorhandene EPIC-A-Fachlichkeit bleibt unverändert und die
  Abschlussprüfung des laufenden MCP-Servers liest die Overview über das
  URL-kodierte `projectRoot`-Template erfolgreich oder dokumentiert einen
  belegten Host-Rückfall nach Konzeptregel.
- [ ] `.mcp.json`, `ainetlinter.project.json` und die externe Hermes-
  Registrierung sind read-only verifiziert; keine veralteten `--path`- oder
  `--config`-Argumente im MCP-Registrierungsweg.
- [ ] Der einmalige Drift-Audit ist mit Triage und ohne ungefragten
  Tech-Debt-Fix in `step-result.md`/`tech-debt.md` nachvollziehbar.
- [ ] `Docs/ROADMAP.md`, `00_uebersicht-und-entscheidungen.md` Zeile 11 und
  §D.4 dokumentieren den implementierten EPIC-A-Stand sachlich.
- [ ] Build und beide Nicht-Stress-Testprojekte sind genau einmal als
  Abschlusslauf grün; Stress bleibt unberührt.
- [ ] `step-008/step-result.md` dokumentiert Nachweise, Abweichungen,
  Live-Grenzen, Auditbefunde und MCP-Gates; `codemap.md` ist bei neuen
  Test-Harness-Symbolen gepflegt.
- [ ] Nach erfolgreicher Abnahme steht dieser Plan auf `done (pending audit)`;
  erst danach darf der Orchestrator EPIC-B JIT planen.

## Rules-Refs

- `.agents/rules/AiNetLinterRichtlinien.mdc#1` — MCP-first-Semantikprüfung,
  Doku-Objektivität und Verifikation gegen den aktuellen Code.
- `.agents/rules/AiNetLinterRichtlinien.mdc#3` — Windows-/Test-Workflow,
  bestehende C#-Testinfrastruktur statt Ad-hoc-MCP-Skripte.
- `.agents/rules/AiNetLinterRichtlinien.mdc#4` — gezielte Iteration, genau ein
  vollständiger Nicht-Stress-Abschlusslauf und Erhalt der Parallelität.
- `.agents/rules/AiNetLinterRichtlinien.mdc#5` — einmaliger Drift-Audit,
  Zero-Warning- und MCP-Quality-Gates, Tech-Debt nicht als automatischer
  neuer Scope.
- `.agents/rules/AiNetLinter.mdc#Kurz-Stil` — falls die minimale
  Test-Harness-Erweiterung C# berührt: Nullable, kurze Methoden, keine
  blockierenden Task-Zugriffe und keine unaufgelösten Abhängigkeiten.

## Bekannte Ausnahmen

- Die Hermes-Konfiguration liegt außerhalb des Repositories und kann nicht
  Bestandteil des Git-Diffs werden; ihr aktuell bereits migrierter Inhalt wird
  nur read-only geprüft.
- `find_magic_values` und `find_dead_code` liefern heuristische, teils LOW-
  Confidence-Ergebnisse; sie werden dokumentiert, nicht automatisch gelöscht
  oder in ein neues Epic überführt.
- Die Live-Prüfung darf keine externen Host-Zustände mutieren. Fehlt ein
  erreichbarer Host mit echter Resource-Expansion, wird genau diese Grenze mit
  dem vorhandenen C#-Raw-Wire-/Fixture-Nachweis dokumentiert.

## Notes

- Der Drift-Audit wurde in diesem Planerlauf bereits einmalig ausgeführt:
  `find_duplicates(scopeDir="src", minTokens=20)`,
  `find_duplicates(mode="structural", scopeDir="src", minTokens=10)`,
  `find_magic_values(scopeFilter="src")` und
  `find_dead_code(scopeFilter="src")`. Coder und Kritiker wiederholen diese
  Epic-Aktivität nicht.
- Nutzer-Overrides: `max_fix_rounds_per_step=6`,
  `soft_step_checkin_interval=80`, `max_batch_items=16`,
  `max_batch_diff_lines=80`. Dieser Step bleibt trotzdem `single`, weil die
  vier Abschlussbereiche fachlich einen gemeinsamen EPIC-A-Abschluss bilden.
- Keine Codeänderungen oder Commits wurden beim Planen vorgenommen; dieser
  Turn schreibt nur den Step-Plan und die Task-Roadmap.
