#nullable enable

using Xunit;

namespace AiNetLinter.Tests.Fixtures;

// Eine geteilte McpLiveRepositoryFixture-Instanz pro Collection; reduziert zwei
// unabhaengige MCP-Subprozess-Starts gegen das echte Repository auf einen.
[CollectionDefinition("McpLiveRepository")]
public sealed class McpLiveRepositoryCollection : ICollectionFixture<McpLiveRepositoryFixture>
{
}
