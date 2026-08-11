#nullable enable

namespace AiNetLinter.Tests.Fixtures;

/// <summary>
/// Erstellt einmalig pro Testklasse ein temporaeres <see cref="SymbolGraphMiniFixtureWorkspace"/>
/// und verbindet einen <see cref="Mcp.McpTestClient"/>.
/// Wird in Read-Only E2E-Tests via <see cref="Xunit.CollectionAttribute"/> auf
/// <see cref="SymbolGraphMcpCollection"/> geteilt verwendet.
/// </summary>
public sealed class SymbolGraphMcpFixture : McpMiniFixtureBase<SymbolGraphMiniFixtureWorkspace>;
