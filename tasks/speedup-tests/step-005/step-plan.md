---
status: done (pending audit)
type: step-plan
task: speedup-tests
step: 005
corrects: step-004
title: "Korrektur: AiNetLinterRichtlinien.mdc §4 an Quarantäne-Entscheidung anpassen"
epic: EPIC-1
estimated_risk: low
step_type: single
items: []
created_by: orchestrator
created_by_model: claude-sonnet-5
created_by_model_knowledge_cutoff: 2026-01
created_at: 2026-08-12
related_to: ["tasks/speedup-tests/step-004/step-review.md"]
---

# Step 005: Korrektur — AiNetLinterRichtlinien.mdc §4 an Quarantäne-Entscheidung anpassen

## Bezug

- **Task:** `speedup-tests`
- **Epic:** `EPIC-1` aus `roadmap.md` — dieser Step korrigiert ausschliesslich
  eine Rules-Konsistenzlücke aus step-004, ändert nichts am Epic-Fortschritt.
- **Konzept-Referenz:** Leitplanke 8 (Quarantäne) — dieser Step behebt einen
  Widerspruch zwischen der in step-004 vollzogenen Quarantäne-Entscheidung
  und einer unveränderten Regel-Textstelle.

## Aktueller Projektzustand (JIT-Kontext)

Mechanisches Korrektur-Transkript aus `tasks/speedup-tests/step-004/step-review.md`
(Finding 1, MAJOR, Ebene Konzept-Treue/Rules-Konformität) — keine eigene
Interpretation. Die Zeile ist Datei+Zeile-genau identifiziert, die Fix-Formulierung
liegt im Review bereits vollständig vor.

## Intention

`.agents/rules/AiNetLinterRichtlinien.mdc` §4 „MCP & Dogfood Testing" soll nicht
mehr behaupten, MCP-Tests würden ausschließlich über `AiNetLinter.Tests`/
`McpTestClient` geprüft — das widerspricht der step-004-Quarantäne-Entscheidung
und dem Umstand, dass `McpHandshakeToolRegistrationTests` bereits in
`AiNetLinter.IntegrationTests` lebt.

## Konkrete Änderungen

### Datei 1: `.agents/rules/AiNetLinterRichtlinien.mdc` (Zeile 94)

- **Was:** Ersetze die Zeile

  ```
  - **MCP & Dogfood Testing:** MCP-Funktionalitäten und Live-Verifikationen (Dogfooding gegen das eigene Repo) werden ausschließlich über die C#-Testinfrastruktur (`McpLiveRepositoryTests` und `McpTestClient` in `AiNetLinter.Tests`) in `dotnet test` umgesetzt. Das Anlegen von ad-hoc Python-Skripten (z. B. im `.todos/`-Ordner) ist verboten.
  ```

  durch (Wortlaut aus `step-004/step-review.md` Finding 1, unverändert übernommen):

  ```
  - **MCP & Dogfood Testing:** MCP-Funktionalitäten und Live-Verifikationen (Dogfooding gegen das eigene Repo) werden ausschließlich über die C#-Testinfrastruktur umgesetzt (aktuell u. a. `McpHandshakeToolRegistrationTests` in `AiNetLinter.IntegrationTests`; die verbleibenden `pending`-MCP-Verträge liegen bis zu ihrer Migration in `McpLiveRepositoryTests`/`McpTestClient`, `AiNetLinter.Tests`). Das Anlegen von Ad-hoc-Skripten (z. B. im `.todos/`-Ordner) ist verboten.
  ```

- **Warum:** Behebt den in `step-004/step-review.md` Finding 1 dokumentierten
  Widerspruch zur eigenen Quarantäne-Entscheidung dieses Steps.

## Tests

- [ ] Keine — reine Textänderung in einer Regel-Markdown-Datei, keine Code-/Testauswirkung.

## Definition of Done

- [ ] Zeile 94 in `.agents/rules/AiNetLinterRichtlinien.mdc` wie oben ersetzt
- [ ] Kein sonstiger Text in der Datei verändert
- [ ] Commit auf aktuellem Branch (Conventional Commit), Subject-Suffix `[speedup-tests]`
- [ ] `step-005/step-result.md` geschrieben
- [ ] `status` in `step-plan.md` von `in_progress` auf `done (pending audit)` gesetzt

## Rules-Refs

- `.agents/rules/AiNetLinterRichtlinien.mdc#4-updates--tests` — die zu korrigierende Datei selbst.

## Bekannte Ausnahmen

- Keine.

## Notes

Scope-Disziplin (siehe `../../spec.md` §6.2.1): ausschliesslich das eine
MAJOR-Finding aus `step-004/step-review.md` beheben. Keine weiteren
Änderungen an `AiNetLinterRichtlinien.mdc` oder anderen Dateien. TD-001..
TD-003 aus `tech-debt.md` bleiben explizit außen vor.
