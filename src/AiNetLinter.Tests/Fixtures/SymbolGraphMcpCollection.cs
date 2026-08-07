#nullable enable

using Xunit;

namespace AiNetLinter.Tests.Fixtures;

// Eine geteilte SymbolGraphMcpFixture-Instanz pro Collection; xUnit v3 serialisiert die
// zugehoerigen Testklassen untereinander, instanziiert die Fixture aber nur einmal und
// spart so sechs eigenstaendige MCP-Subprozess-Starts ein.
[CollectionDefinition("SymbolGraphMcp")]
public sealed class SymbolGraphMcpCollection : ICollectionFixture<SymbolGraphMcpFixture>
{
}
