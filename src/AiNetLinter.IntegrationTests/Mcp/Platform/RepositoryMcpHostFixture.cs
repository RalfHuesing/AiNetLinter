#nullable enable

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AiNetLinter.IntegrationTests.Platform;

namespace AiNetLinter.IntegrationTests.Mcp.Platform;

public sealed class RepositoryMcpHostFixture : IAsyncLifetime
{
    private readonly Lazy<Task<McpProcessHost>> host;
    private readonly ReadOnlyMcpHostClient client;

    public RepositoryMcpHostFixture()
    {
        host = new Lazy<Task<McpProcessHost>>(CreateAsync, true);
        client = new ReadOnlyMcpHostClient(host);
    }

    internal Task<McpProcessHost> GetHostAsync() => host.Value;
    internal ReadOnlyMcpHostClient Client => client;

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public async ValueTask DisposeAsync()
    {
        if (host.IsValueCreated) await (await host.Value.ConfigureAwait(false)).DisposeAsync().ConfigureAwait(false);
    }

    private static Task<McpProcessHost> CreateAsync() => McpProcessHost.StartAsync(
        new McpProcessTarget(SolutionRootLocator.Find(), null), TimeSpan.FromSeconds(60));
}
