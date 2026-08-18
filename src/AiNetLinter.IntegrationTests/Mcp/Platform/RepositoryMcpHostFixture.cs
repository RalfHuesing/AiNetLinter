#nullable enable

using System;
using System.Threading.Tasks;
using AiNetLinter.IntegrationTests.Platform;

namespace AiNetLinter.IntegrationTests.Mcp.Platform;

public sealed class RepositoryMcpHostFixture : McpHostFixtureBase
{
    private protected override Task<McpProcessHost> CreateProcessHostAsync() => McpProcessHost.StartAsync(
        new McpProcessTarget(SolutionRootLocator.Find(), null), TimeSpan.FromSeconds(60));
}
