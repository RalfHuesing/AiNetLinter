---
status: open
type: step-plan
task: 01-namespace-tree
step: 001
corrects: null
title: "Core Models, ProjectTypeClassifier & GetNamespaceTreeScanner implementieren"
epic: EPIC-01
estimated_risk: medium
step_type: single
items: []
created_by: planer
created_by_model: claude-3-7-sonnet
created_by_model_knowledge_cutoff: 2026-01
created_at: 2026-08-18T23:45:00+02:00
related_to: []
---

# Step 001: Core Models, ProjectTypeClassifier & GetNamespaceTreeScanner implementieren

## Bezug

- **Task:** `01-namespace-tree`
- **Epic:** `EPIC-01` aus `roadmap.md` — Roslyn-basierte Extraktion für 3 Zoom-Stufen (Projekte, Namespaces, Typen) inkl. Filterung (Source-Only, Compiler-Generated) und Truncation
- **Konzept-Referenz:** `konzept.md` §Scope, Muss-Haben, §Technische Umsetzung

## Aktueller Projektzustand (JIT-Kontext)

- In `src/AiNetLinter/Mcp/Tools/FileStructure/` existieren bereits `GetClassStructureModels.cs`, `GetClassStructureTool.cs`, `GetIndexScopeScanner.cs`, `SolutionFileWalker.cs` etc.
- `PathNormalizer.IsTestFile` steht bereit für die Erkennung von Test-Dateien/Projekten.
- `McpTruncation.TruncateLines`, `McpSufficiencyHints.Append`, `McpToolResults` und `McpJsonOptions.Default` sind als einheitliche MCP-Infrastruktur etabliert.
- Compiler-generierte Symbole werden über `symbol.IsImplicitlyDeclared`, Name-Checks (`<`, `$`, `<Clone>$`, `EqualityContract`) und `CompilerGeneratedAttribute` identifiziert.
- Source-Only-Isolation erfolgt strikt über `type.Locations.Any(l => l.IsInSource)` / `type.DeclaringSyntaxReferences.Length > 0`.

## Intention

Implementierung der Datenstrukturen (`NamespaceTreePayload`, `NamespaceTreeNode`, `TypeNodeEntry`, `ProjectOverviewEntry`), des `ProjectTypeClassifier` (`Exe`, `Test`, `Lib`) und des `GetNamespaceTreeScanner` mit den drei Zoom-Stufen (Stufe 1: Solution/Projects, Stufe 2: Project/Namespaces, Stufe 3: Namespace/Types) inkl. Filterung, Formatierung und Truncation.

## Konkrete Änderungen

### Datei 1: `src/AiNetLinter/Mcp/Tools/FileStructure/GetNamespaceTreeModels.cs` (neu)

- **Was:** Erstellen der DTOs/Payloads für `StructuredContent`:
  - `ProjectOverviewEntry(string ProjectName, string ProjectType, int NamespaceCount, int TypeCount)`
  - `TypeNodeEntry(string Name, string Kind, string FilePath, int Line, string Visibility)`
  - `NamespaceTreeNode(string Namespace, int TypeCount, IReadOnlyList<TypeNodeEntry>? Types, IReadOnlyList<NamespaceTreeNode>? SubNamespaces)`
  - `NamespaceTreePayload(string? SolutionName, string? Project, string? NamespacePrefix, string? KindFilter, int Depth, bool IncludeTypes, int TotalCount, int ShownCount, bool Truncated, IReadOnlyList<ProjectOverviewEntry>? Projects, IReadOnlyList<NamespaceTreeNode>? Namespaces, IReadOnlyList<TypeNodeEntry>? Types)`
- **Warum:** Typisierte, saubere JSON-Serialisierung für MCP-Clients.

### Datei 2: `src/AiNetLinter/Mcp/Tools/FileStructure/ProjectTypeClassifier.cs` (neu)

- **Was:** Implementieren von `Classify(Project project)`:
  - Prüft ob Test-Projekt (Name enthält `.Tests`, `.FastTests`, `.IntegrationTests`, `.TestKit`, `.Specs` oder `PathNormalizer.IsTestFile` matcht auf Projektdateipfad) -> `"Test"`.
  - Prüft `CompilationOptions.OutputKind` == `OutputKind.ConsoleApplication` / `WindowsApplication` -> `"Exe"`.
  - Sonst -> `"Lib"`.
- **Warum:** Einheitliche Projektklassifizierung für Stufe 1.

### Datei 3: `src/AiNetLinter/Mcp/Tools/FileStructure/GetNamespaceTreeScanner.cs` (neu)

- **Was:** Implementieren des Scanners mit Methoden:
  - `ScanSolutionProjectsAsync(Solution solution, CancellationToken ct)`: Stufe 1 (Projekte mit Klassifizierung, Namespace-Count und Source-Type-Count).
  - `ScanProjectNamespacesAsync(Project project, string? namespacePrefix, int depth, bool includeTypes, string? kindFilter, int maxResults, CancellationToken ct)`: Stufe 2 & Stufe 3.
  - Traversierung von `compilation.GlobalNamespace.GetNamespaceMembers()`, rekursiv bis `depth` (Cap 3).
  - Filterung: Ignorieren von Compiler-generierten Typen (`<`, `$`, etc.) und externen BCL/NuGet-Typen (`!l.IsInSource`). Leere Parent-Namespaces anzeigen wenn Sub-Namespaces Typen haben, sonst weglassen.
  - Markdown-Rendering für alle 3 Stufen mit Einrückungen, Baumstruktur, Hinweisen und `McpTruncation`.
- **Warum:** Entkoppelte Scan- und Formatierungslogik ohne direkte Server-Abhängigkeit, ideal unit-testbar.

## Tests

- [ ] `src/AiNetLinter.FastTests/Mcp/Tools/GetNamespaceTreeScannerTests.cs` (neu):
  - `ScanSolutionProjects_ReturnsAllProjectsWithCorrectClassificationAndCounts`
  - `ScanProjectNamespaces_Level2_ReturnsNamespaceTreeWithoutTypesWhenIncludeTypesFalse`
  - `ScanProjectNamespaces_Level3_ReturnsTypesWithKindFilter`
  - `ScanProjectNamespaces_ExcludesCompilerGeneratedAndSyntheticTypes`
  - `ScanProjectNamespaces_ExcludesExternalBclTypes`
  - `ScanProjectNamespaces_TruncatesWhenExceedingMaxResults`

## Definition of Done

- [ ] Alle „Konkrete Änderungen" umgesetzt
- [ ] Build-Command aus Tech-Stack-Notiz (`dotnet build`) grün
- [ ] Test-Command `dotnet test src/AiNetLinter.FastTests --filter Category!=Stress` grün
- [ ] Commit auf aktuellem Branch (Conventional Commit)
- [ ] `step-001/step-result.md` geschrieben
- [ ] `status` in `step-plan.md` von `in_progress` auf `done (pending audit)` gesetzt

## Rules-Refs

- `.agents/rules/AiNetLinter.mdc#Kurz-Stil` — sealed Klassen, flache Methoden, Footprint < 2500 Zeilen.
- `.agents/rules/AiNetLinterRichtlinien.mdc#1. Grundprinzipien` — Immutability, direkte Lösungen, MCP-Sufficiency-Doctrine.

## Bekannte Ausnahmen

- Keine.
