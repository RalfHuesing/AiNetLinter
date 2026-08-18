#nullable enable

using System;
using System.Threading.Tasks;
using Xunit;

namespace AiNetLinter.IntegrationTests.Mcp.Platform;

/// <summary>
/// Gemeinsame Basisklasse fuer MCP-Host-Fixtures mit Lazy-Lifecycle und Test-Client.
/// </summary>
public abstract class McpHostFixtureBase : IAsyncLifetime
{
    private readonly Lazy<Task<McpProcessHost>> host;
    private readonly ReadOnlyMcpHostClient client;

    protected McpHostFixtureBase()
    {
        host = new Lazy<Task<McpProcessHost>>(CreateProcessHostAsync, true);
        client = new ReadOnlyMcpHostClient(host);
    }

    internal Task<McpProcessHost> GetHostAsync() => host.Value;
    internal ReadOnlyMcpHostClient Client => client;

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public async ValueTask DisposeAsync()
    {
        if (host.IsValueCreated) await (await host.Value.ConfigureAwait(false)).DisposeAsync().ConfigureAwait(false);
    }

    private protected abstract Task<McpProcessHost> CreateProcessHostAsync();
}
