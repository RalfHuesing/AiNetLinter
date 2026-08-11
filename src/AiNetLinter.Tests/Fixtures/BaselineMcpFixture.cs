#nullable enable

namespace AiNetLinter.Tests.Fixtures;

/// <summary>
/// Erstellt einmalig pro Testklasse ein temporaeres <see cref="BaselineMiniFixtureWorkspace"/>
/// und verbindet einen <see cref="Mcp.McpTestClient"/>.
/// Wird in Read-Only E2E-Tests via <see cref="Xunit.IClassFixture{BaselineMcpFixture}"/> verwendet.
/// </summary>
public sealed class BaselineMcpFixture : McpMiniFixtureBase<BaselineMiniFixtureWorkspace>;
