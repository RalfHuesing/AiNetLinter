---
status: done
type: step-review
task: decompiled-assembly-analysis
step: 001
epic: EPIC-01
step_type: single
reviewed_by: kritiker
reviewed_by_model: gpt-5
reviewed_by_model_knowledge_cutoff: nicht angegeben
reviewed_at: 2026-08-28T12:18:22+02:00
verdict: issues
tech_debt_ids: []
---

# Review Step 001: Einheitlichen Analysis-Target-Vertrag und Dispatch umstellen

## Verdict

- [ ] **approved**
- [x] **issues**
- [ ] **blocked**

Der Step ist wegen eines MAJOR-Verstoßes gegen die referenzierte, dauerhaft
anzuwendende MCP-Workflow-Regel nicht freigabefähig. Die Implementierung und
die Gates sind ansonsten plausibel und grün.

## Geprüfte Bereiche

- [x] Plan-Erfüllung
- [x] Rules-Konformität
- [x] Logische Korrektheit
- [x] Konzept-Treue
- [x] Build-/Test-Gates

## Befund: Plan-Erfüllung

Der Commit migriert den Target-Vertrag, den gemeinsamen Dispatcher, die 29
Tool-Registrierungen, Test-Fixtures, Regressionstests und die im Plan genannten
Dokumente; die ausgelieferte Workflow-Regel bleibt jedoch trotz ihrer direkten
Relevanz für diesen Vertrag unaufgelöst widersprüchlich.

## Befund: Rules-Konformität

Die C#-Qualitäts-, Immutability-, Sicherheits- und Architekturregeln werden
eingehalten, aber `.agents/rules/AiNetLinter-McpWorkflow.mdc` ist als
`alwaysApply`-Regel weiterhin auf `projectRoot` sowie `assemblyPath` mit
optionalem Consumer-Projekt festgelegt und widerspricht damit dem neuen
`targetType`/`targetPath`-Vertrag.

## Befund: Logische Korrektheit

Resolver, Projekt-Lease-Dispatch, Assembly-Metadata-Adapter, Health-Varianten
und die negativen Vertragsfälle verhalten sich wie geplant; es wurde keine
Assembly-Lade- oder Ausführungsroute eingeführt.

## Befund: Konzept-Treue

Der Step bleibt innerhalb der Konzept-Phase 1: ein harter gemeinsamer Target-
Vertrag, Projektregressionen und metadata-only Assembly-Spezialtools ohne
Dekompilierung, Session, Reflection-Laden oder versteckten Consumer-Kontext.

## Findings

### 1. MCP-Workflow-Regel liefert weiterhin den veralteten Wire-Vertrag

- **Severity:** MAJOR
- **Ebene:** Rules-Konformität
- **Ort:** `.agents/rules/AiNetLinter-McpWorkflow.mdc:4,24-25,56-60`; ausgelieferte Einbettung über `src/AiNetLinter/AiNetLinter.csproj:30` und `src/AiNetLinter/Mcp/Registration/McpAgentGuideRegistration.cs:20`; neuer Vertrag in `src/AiNetLinter/Mcp/Registration/AssemblyAnalysisToolRegistrations.cs:28-47,70-87`
- **Was:** Die im Plan ausdrücklich referenzierte und mit `alwaysApply: true` ausgelieferte MCP-Regel verlangt weiterhin für projektbezogene Tool-Aufrufe `projectRoot` und beschreibt für beide Assembly-Tools `assemblyPath` sowie ein optionales Consumer-`projectRoot`. Der Step registriert dieselben Assembly-Tools dagegen ausschließlich mit erforderlichem `targetType: "assembly"` und `targetPath` und verwirft den Consumer-Kontext; auch die übrigen target-gebundenen Tools erwarten den neuen Vertrag. Ein Agent, der die mit dem Produkt ausgelieferte Regel befolgt, erzeugt damit nach diesem Step ungültige Tool-Aufrufe.
- **Konkreter Fix:** `.agents/rules/AiNetLinter-McpWorkflow.mdc` auf den neuen Vertrag synchronisieren: `targetType`/absolutes `targetPath` für target-gebundene Tools, paarweise optionale Targets nur für `get_server_health`, keine Targets für Feedback sowie die weiterhin projektbezogenen `projectRoot`-Parameter ausschließlich bei Resource-URIs dokumentieren. Den Assembly-Abschnitt auf `targetType: "assembly"`/`targetPath` und den metadata-only Spezialtool-Scope ohne Consumer-Projekt umstellen. Die eingebettete Auslieferung und eine Bootstrap-/Regel-Vertragsteststrecke müssen denselben Inhalt prüfen; `Docs/configuration.md:35` ist dabei ebenfalls vom veralteten optionalen Consumer-Kontext zu bereinigen.

## Build-/Test-Status

- `dotnet build` — **grün** (0 Warnungen, 0 Fehler)
- `dotnet test src/AiNetLinter.FastTests --filter Category!=Stress` — **grün** (1857 Tests, 0 Fehler)
- `dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress` — **grün** (360 Tests, 0 Fehler)
