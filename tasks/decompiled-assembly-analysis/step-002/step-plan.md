---
status: done
type: step-plan
task: decompiled-assembly-analysis
step: 002
corrects: step-001
title: "MCP-Workflow-Regel auf den neuen Target-Vertrag synchronisieren"
epic: EPIC-01
estimated_risk: low
step_type: single
items: []
created_by: orchestrator
created_by_model: gpt-5
created_by_model_knowledge_cutoff: nicht angegeben
created_at: 2026-08-28T12:22:00+02:00
related_to:
  - step-001/step-review.md
---

# Step 002: MCP-Workflow-Regel auf den neuen Target-Vertrag synchronisieren

## Bezug

- **Task:** `decompiled-assembly-analysis`
- **Epic:** `EPIC-01` — gemeinsamer Target-Vertrag und Dispatch-Grenze
- **Korrektur von:** `step-001/step-review.md`, Finding 1

## Aktueller Projektzustand (JIT-Kontext)

`step-001` ist implementiert und getestet, aber wegen des MAJOR-Funds zur
veralteten ausgelieferten MCP-Workflow-Regel nicht freigegeben.

## Intention

Die dauerhaft anzuwendende und ausgelieferte MCP-Regel muss denselben
`targetType`/`targetPath`-Vertrag beschreiben wie die implementierten
Registrierungen, damit Agenten daraus gültige Tool-Aufrufe ableiten.

## Konkrete Änderungen

### MCP-Workflow-Regel — `.agents/rules/AiNetLinter-McpWorkflow.mdc:4,24-25,56-60`

- **Was:** Die Regel auf den neuen Vertrag synchronisieren: `targetType` /
  absolutes `targetPath` für target-gebundene Tools, paarweise optionale
  Targets nur für `get_server_health`, keine Targets für Feedback sowie die
  weiterhin projektbezogenen `projectRoot`-Parameter ausschließlich bei
  Resource-URIs dokumentieren.
- **Was:** Den Assembly-Abschnitt auf `targetType: "assembly"` /
  `targetPath` und den metadata-only Spezialtool-Scope ohne Consumer-Projekt
  umstellen.
- **Was:** Die eingebettete Auslieferung und eine Bootstrap-/Regel-
  Vertragsteststrecke müssen denselben Inhalt prüfen.

### Konfigurationsdokumentation — `Docs/configuration.md:35`

- **Was:** Den veralteten optionalen Consumer-Kontext bereinigen.

## Tests

- [ ] Die Bootstrap-/Regel-Vertragsteststrecke prüft denselben Inhalt wie die
  eingebettete Auslieferung.
- [ ] `dotnet build`
- [ ] `dotnet test src/AiNetLinter.FastTests --filter Category!=Stress`
- [ ] `dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress`

## Definition of Done

- [ ] `.agents/rules/AiNetLinter-McpWorkflow.mdc` beschreibt für
  target-gebundene Tools `targetType`/absolutes `targetPath`.
- [ ] Nur `get_server_health` verwendet paarweise optionale Targets.
- [ ] Feedback bleibt ohne Targets; `projectRoot` bleibt nur bei
  Resource-URIs dokumentiert.
- [ ] Der Assembly-Abschnitt verwendet `targetType: "assembly"`/`targetPath`
  und beschreibt den metadata-only Scope ohne Consumer-Projekt.
- [ ] Eingebettete Auslieferung und Bootstrap-/Regel-Vertragsteststrecke
  prüfen denselben Inhalt.
- [ ] `Docs/configuration.md:35` enthält keinen veralteten optionalen
  Consumer-Kontext mehr.

## Rules-Refs

- `.agents/rules/AiNetLinter-McpWorkflow.mdc:4,24-25,56-60` — auf den
  implementierten `targetType`/`targetPath`-Vertrag synchronisieren.

## Bekannte Ausnahmen

- Keine.
