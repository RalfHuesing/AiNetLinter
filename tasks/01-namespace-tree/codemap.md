---
task: 01-namespace-tree
type: codemap
maintained_by: planer, coder, kritiker
last_updated: 2026-08-19T00:30:00+02:00
---

# CodeMap: 01-namespace-tree

Task-scoped Landkarte — existiert nur für diesen Task, wird mit
`tasks/01-namespace-tree` gelöscht.

## Karte

- **`src/AiNetLinter/Mcp/Tools/FileStructure/GetNamespaceTreeTool.cs`** — MCP-Tool-Einstiegspunkt für `get_namespace_tree` mit Input-Validierung und Solution/Project-Dispatch. (zuletzt: step-002)
- **`src/AiNetLinter/Mcp/Tools/FileStructure/GetNamespaceTreeModels.cs`** — DTOs, Payload-Records, Scan-Parameter und `GetNamespaceTreeInput`. (zuletzt: step-002)
- **`src/AiNetLinter/Mcp/Tools/FileStructure/ProjectTypeClassifier.cs`** — Heuristische Klassifizierung von Roslyn-Projekten nach `Exe`, `Test` oder `Lib`. (zuletzt: step-001)
- **`src/AiNetLinter/Mcp/Tools/FileStructure/GetNamespaceTreeScanner.cs`** — Scan- und Formatierungs-Engine für alle 3 Progressive-Disclosure-Stufen. (zuletzt: step-001)
- **`src/AiNetLinter/Mcp/Tools/FileStructure/SymbolVisibilityResolver.cs`** — Zentraler Helper für `DeclaredAccessibility -> string` Sichtbarkeits-Auflösung. (zuletzt: step-001)
- **`src/AiNetLinter/Mcp/Tools/FileStructure/GetClassStructureTool.cs`** — Member-/Zeilen-Übersicht eines Typs; nutzt jetzt `SymbolVisibilityResolver`. (zuletzt: step-001)
- **`src/AiNetLinter/Mcp/FileStructureToolRegistrations.cs`** — Registriert alle file-structure-orientierten Tools inkl. `get_namespace_tree`. (zuletzt: step-002)
- **`src/AiNetLinter/Mcp/ServerInstructions.cs`** — Single-Source-of-Truth für initialize-Handshake Instructions Doctrine und Tool-Auflistung (jetzt 23 Tools). (zuletzt: step-002)
- **`src/AiNetLinter/Mcp/OverviewResourceRegistration.cs`** — Registriert `ainetlinter://overview` Resource mit Tool-Kurzbeschreibungen inkl. `get_namespace_tree`. (zuletzt: step-002)
- **`src/AiNetLinter/Output/PathNormalizer.cs`** — Enthält Pfad- und Test-Erkennungsheuristiken (`IsTestFile`). (zuletzt: initial)
- **`src/AiNetLinter.FastTests/Mcp/Tools/GetNamespaceTreeToolTests.cs`** — In-Memory Komponententests für `GetNamespaceTreeTool` (Validierung, Ambiguous, Errors, 3 Stufen). (zuletzt: step-002)
- **`src/AiNetLinter.FastTests/Mcp/Tools/GetNamespaceTreeScannerTests.cs`** — In-Memory Unit-Tests für `GetNamespaceTreeScanner` und alle 3 Stufen. (zuletzt: step-001)
- **`src/AiNetLinter.IntegrationTests/Mcp/`** — Integrationstests (u. a. `McpServerCommandContractTests`, `McpHandshakeToolRegistrationTests`) synchronisiert auf 23 Tools. (zuletzt: step-002)


