---
task: 01-namespace-tree
type: codemap
maintained_by: planer, coder, kritiker
last_updated: 2026-08-18T23:35:00+02:00
---

# CodeMap: 01-namespace-tree

Task-scoped Landkarte — existiert nur für diesen Task, wird mit
`tasks/01-namespace-tree` gelöscht.

## Karte

- **`src/AiNetLinter/Mcp/Tools/FileStructure/`** — Beherbergt Dateistruktur-Tools wie `GetClassStructureTool`, `GetIndexScopeTool`, `GetFileSkeletonTool` und künftig `GetNamespaceTreeTool` + `GetNamespaceTreeScanner` + Models. (zuletzt: initial)
- **`src/AiNetLinter/Mcp/FileStructureToolRegistrations.cs`** — Registriert alle file-structure-orientierten Tools an der MCP-Server-Tool-Collection. (zuletzt: initial)
- **`src/AiNetLinter/Mcp/ServerInstructions.cs`** — Single-Source-of-Truth für initialize-Handshake Instructions Doctrine und Tool-Auflistung. (zuletzt: initial)
- **`src/AiNetLinter/Mcp/OverviewResourceRegistration.cs`** — Registriert `ainetlinter://overview` Resource mit Tool-Kurzbeschreibungen. (zuletzt: initial)
- **`src/AiNetLinter/Output/PathNormalizer.cs`** — Enthält Pfad- und Test-Erkennungsheuristiken (`IsTestFile`). (zuletzt: initial)
- **`src/AiNetLinter.FastTests/Mcp/Tools/`** — Schnelle In-Memory Komponententests für MCP-Tools auf `McpInMemoryTestContext` / `RoslynTestSolutionFactory`. (zuletzt: initial)
- **`src/AiNetLinter.IntegrationTests/Mcp/`** — Integrationstests (u. a. `McpServerCommandContractTests`, `McpHandshakeToolRegistrationTests`) für Tool-Count und Framing. (zuletzt: initial)
