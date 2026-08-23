---
status: done (pending audit)
type: step-plan
task: 11_epic-projektregistry-und-daemon
step: 007
corrects: step-006
title: "Originalfehler und Creation-Loser im Testvertrag vollständig assertieren"
epic: EPIC-A
estimated_risk: medium
step_type: single
items: []
created_by: orchestrator
created_by_model: GPT-5
created_by_model_knowledge_cutoff: nicht deklariert
created_at: 2026-08-23T23:58:00+02:00
related_to: ["step-006/step-review.md", "step-006/step-plan.md", "step-006/step-result.md"]
---

# Step 007: Originalfehler und Creation-Loser im Testvertrag vollständig assertieren

## Bezug und Scope

- **Task:** `11_epic-projektregistry-und-daemon`
- **Epic:** EPIC-A
- **Korrekturquelle:** `step-006/step-review.md`, Findings 1–2.
- Reiner Test-/Test-Seam-Korrektur-Step. Die abgenommene Registry- und
  FAILED-Produktionslogik bleibt unverändert; keine Änderungen an Overview-,
  Loader- oder Health-Verträgen.

## Korrekturen

### 1. Vollständige Originalexception im PROJECT_LOAD_FAILED-Result

- Im Produktions-Kalt-Load-Contract-Test die Warnlog-Auswertung von der
  Vertragsassertion trennen. Die Warnung wird separat als Warnung geprüft.
- Das Fehlerresult muss direkt `originalException.Message` enthalten, mit
  ordinaler, nicht verkürzter Assertion; ein aus dem Log extrahierter Suffix
  darf nicht als Ersatz dienen.
- Den stabilen Restore-/Retry-Hint weiter direkt im Fehlerresult prüfen.

### 2. Expliziter, deterministischer Creation-Loser-Abnahmeanker

- Den bestehenden Lookup→Reservation-Test als Einmal-/Identity-Nachweis
  beibehalten: atomare Reservation führt zu exakt einem Factory-/Load-Pfad,
  gemeinsamer residenter Instanz und Other-Root-Servicefähigkeit.
- Ergänze für den im Review geforderten `PublishCreation`-Race-Zweig einen
  separaten, kontrollierten Test-Harness bzw. minimalen test-only Seam, der
  einen bereits erzeugten, nicht publizierten Creation-Attempt gegenüber
  einer kontrolliert publizierten Gewinnerinstanz sichtbar macht. Dieser
  Harness darf keine echte doppelte Erstzugriffs-Reservation im produktiven
  Pfad einführen und darf keine globale Testserialisierung verwenden.
- Assertiere am kontrollierten Race-Zweig, dass der tatsächlich nicht
  publizierte Server genau einmal außerhalb des Registry-Locks disposed wird,
  die publizierte Gewinnerinstanz unberührt bleibt und Other-Root-Aufrufe
  während der Barriere bedienbar sind. Der bestehende Einmal-/Identity-Test
  bleibt die Regression gegen die frühere getrennte Lookup→Reservation-
  Struktur; der neue Seam-Test deckt ausschließlich die Loser-Disposal-
  Garantie ab.

## Akzeptanzkriterien

1. `ProductionColdLoad_BrokenSlnx_ReturnsOriginalLoadFailedContract` prüft
   `originalException.Message` direkt im `PROJECT_LOAD_FAILED`-Text und
   separat Warnung, Solution-Pfad sowie Restore-/Retry-Hint.
2. Der Registry-Test beweist weiterhin exakt einen normalen Erstzugriffs-
   Factory-/Load-Pfad und gemeinsame Server-Identity.
3. Ein deterministischer Test für den kontrollierten Publish-Race-Zweig
   identifiziert den nicht publizierten Server, prüft seine genau einmalige
   Disposal außerhalb des Locks und verifiziert, dass der Gewinner nicht
   disposed wird, bevor die Registry beendet wird.
4. Keine globale Testserialisierung, kein Warten auf `LoadTask` und keine
   produktive Doppel-Reservation; test-only Seam minimal und ausdrücklich
   begründet.

## Tests und Verifikation

- Gezielte Testnamefilter/Unit- und Integration-Slices während der
  Entwicklung; kein Stresslauf.
- Abschluss genau einmal: `dotnet build`,
  `dotnet test src/AiNetLinter.FastTests --filter Category!=Stress`,
  `dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress`.
- Vor jedem Commit MCP-Quality-Gates für geänderte Scopes: `get_violations`,
  `safeguard`, `metrics_lookup` und gezielt `get_impact`/`get_feature_context`.
- Drift-Audit bleibt einmalige Epic-Abschlussaktivität und wird hier nicht
  ausgeführt.

## Definition of Done

- [ ] Beide Findings aus `step-006/step-review.md` sind mit direkten,
  regressionsstarken Assertions bzw. deterministischem Seam-Test abgedeckt.
- [ ] Build und beide Nicht-Stress-Testprojekte sind genau einmal als
  Abschlusslauf grün; Stress bleibt unberührt.
- [ ] `step-007/step-result.md` dokumentiert Nachweise, Abweichungen und
  MCP-Gates; `codemap.md` ist gepflegt; dieser Plan steht danach auf
  `done (pending audit)`.
- [ ] Coder erstellt zwei gezielte Commits: Code/Tests, danach
  Doku/Artefakte. Keine Historienmanipulation und kein Push.

## Rules-Refs

- `.agents/rules/AiNetLinterRichtlinien.mdc#1` — MCP-first-Symbol-/Impact-
  Prüfung für Test-/Seam-Änderungen.
- `.agents/rules/AiNetLinterRichtlinien.mdc#4` — deterministische xUnit-v3-
  Tests, gefilterte Iteration und genau ein Nicht-Stress-Abschlusslauf.
- `.agents/rules/AiNetLinterRichtlinien.mdc#5` — direkte Result-/Fehler-
  Assertions, Zero-Warning-Gate und keine globale Serialisierung.
