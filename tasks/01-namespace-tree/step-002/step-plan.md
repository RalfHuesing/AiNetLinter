---
status: done (pending audit)
type: step-plan
task: 01-namespace-tree
step: 002
corrects: null
title: "GetNamespaceTreeTool registrieren, MCP-Optionen & Server-Instructions synchronisieren"
epic: EPIC-02
estimated_risk: medium
step_type: single
items: []
created_by: planer
created_by_model: claude-3-7-sonnet
created_by_model_knowledge_cutoff: 2026-01
created_at: 2026-08-19T00:15:00+02:00
related_to: [step-001]
---

# Step 002: GetNamespaceTreeTool registrieren, MCP-Optionen & Server-Instructions synchronisieren

## Bezug

- **Task:** `01-namespace-tree`
- **Epic:** `EPIC-02` aus `roadmap.md` — Registrierung in `FileStructureToolRegistrations`, `OverviewResourceRegistration`, `ServerInstructions` und Verifikation via `McpServerOptionsFactory`
- **Konzept-Referenz:** `konzept.md` §Scope, Muss-Haben, §Edge Cases, §DoD #1-10

## Aktueller Projektzustand (JIT-Kontext)

- In `step-001` wurden `GetNamespaceTreeScanner`, `GetNamespaceTreeModels` und `ProjectTypeClassifier` implementiert und unit-getestet.
- Bisher sind 22 MCP-Tools registriert. Mit `get_namespace_tree` steigt die Tool-Anzahl auf **23**.
- Zu aktualisieren:
  - `src/AiNetLinter/Mcp/Tools/FileStructure/GetNamespaceTreeTool.cs` (neu) — Tool-Einstiegspunkt mit Parameter-Validierung (`kind`, `depth`, `maxResults`), Solution/Project-Dispatch und Aggregate Compile-Warnings.
  - `src/AiNetLinter/Mcp/FileStructureToolRegistrations.cs` — `AddGetNamespaceTree` registrieren.
  - `src/AiNetLinter/Mcp/ServerInstructions.cs` — Text-Doctrine um `get_namespace_tree`, C#-Only-Grenze und Workflow-Erkundung erweitern.
  - `src/AiNetLinter/Mcp/OverviewResourceRegistration.cs` — Tool-Summary eintragen.
  - Bestehende Tool-Count-Tests in FastTests & IntegrationTests von 22 auf 23 anheben.

## Intention

Das Tool `get_namespace_tree` als residenten MCP-Endpunkt freischalten, alle Server-Metadaten konsistent synchronisieren und über Tool-Komponententests absichern.

## Konkrete Änderungen

### Datei 1: `src/AiNetLinter/Mcp/Tools/FileStructure/GetNamespaceTreeTool.cs` (neu)

- **Was:** Implementieren von `GetNamespaceTreeTool.ExecuteAsync(McpCodeGraphServer state, string? project, string? namespacePrefix, int depth, bool includeTypes, string? kind, int maxResults, CancellationToken ct)`:
  - Prüft `state.LoadState` und `state.GetCurrentSolution()`.
  - Validiert `kind` via `GetNamespaceTreeScanner.IsValidKind`.
  - Clampt `depth` (1 bis 3) und `maxResults` (1 bis 200, Default 50).
  - Wenn `project` null: delegiert an `GetNamespaceTreeScanner.ScanSolutionProjectsAsync`.
  - Wenn `project` angegeben: sucht Projekt im Workspace (case-insensitive Substring).
    - Bei 0 Treffern: `McpToolResults.InvalidArgument($"Projekt '{project}' wurde nicht gefunden...", hint: "Verfuegbare Projekte: ...")`.
    - Bei >1 Treffern: `McpToolResults.AmbiguousSymbol(project, candidateProjectNames)`.
    - Bei 1 Treffer: ruft `GetNamespaceTreeScanner.ScanProjectNamespacesAsync` auf.
  - Hängt `FindSymbolTool.BuildAggregateWarningAsync` an und liefert `McpToolResults.Text(..., payload)` mit SufficiencyHint bei Unvollständigkeit/Vollständigkeit.

### Datei 2: `src/AiNetLinter/Mcp/FileStructureToolRegistrations.cs`

- **Was:** `AddGetNamespaceTree` hinzufügen, Beschreibung `GetNamespaceTreeDescription` definieren und in `Register` aufrufen.

### Datei 3: `src/AiNetLinter/Mcp/ServerInstructions.cs` & `src/AiNetLinter/Mcp/OverviewResourceRegistration.cs`

- **Was:** `get_namespace_tree` in Tool-Liste, C#-Only-Grenze und Empfohlene Workflows ("Code erkunden: get_index_scope -> get_namespace_tree -> ...") aufnehmen; Summary in `OverviewResourceRegistration.ToolSummaries` ergänzen.

### Datei 4: FastTests & IntegrationTests Tool-Count anpassen

- **Was:** `McpServerOptionsFactoryTests` (22 -> 23), `McpServerCommandContractTests` (22 -> 23) synchronisieren.

## Tests

- [ ] `src/AiNetLinter.FastTests/Mcp/Tools/GetNamespaceTreeToolTests.cs` (neu):
  - `ExecuteAsync_NoSolutionLoaded_ReturnsSolutionNotLoaded`
  - `ExecuteAsync_UnknownKind_ReturnsInvalidArgument`
  - `ExecuteAsync_UnknownProject_ReturnsInvalidArgumentWithAvailableProjects`
  - `ExecuteAsync_AmbiguousProject_ReturnsAmbiguousSymbol`
  - `ExecuteAsync_NoParameters_ReturnsSolutionOverview`
  - `ExecuteAsync_SpecificProject_ReturnsProjectTree`
  - `ExecuteAsync_SpecificNamespaceAndKind_ReturnsTypes`
- [ ] `McpServerOptionsFactoryTests` & `OverviewResourceRegistrationTests` laufen grün mit 23 Tools.

## Definition of Done

- [ ] Alle „Konkrete Änderungen" umgesetzt
- [ ] `dotnet build` mit 0 Warnungen
- [ ] `dotnet test src/AiNetLinter.FastTests --filter Category!=Stress` und `IntegrationTests` grün
- [ ] `step-002/step-result.md` geschrieben
- [ ] `status` in `step-plan.md` auf `done (pending audit)`

## Rules-Refs

- `.agents/rules/AiNetLinterRichtlinien.mdc` — Single Source of Truth in `ServerInstructions`, Dogfooding.
- `src/AiNetLinter/Mcp/IsErrorPolicy.md` — Fehler-Klassifikation (Recoverable für InvalidArgument / AmbiguousSymbol).
