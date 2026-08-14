#nullable enable

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AiNetLinter.IntegrationTests.Fixtures;

namespace AiNetLinter.IntegrationTests.Mcp.Platform;

public sealed class ReadOnlyMcpHostFixture : IAsyncLifetime
{
    private readonly Lazy<Task<McpProcessHost>> host;
    private readonly ReadOnlyMcpHostClient client;

    public ReadOnlyMcpHostFixture()
    {
        host = new Lazy<Task<McpProcessHost>>(CreateAsync, true);
        client = new ReadOnlyMcpHostClient(host);
    }

    internal Task<McpProcessHost> GetHostAsync() => host.Value;
    internal ReadOnlyMcpHostClient Client => client;

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    // ainetlinter-disable DuplicateCode
    public async ValueTask DisposeAsync()
    {
        if (host.IsValueCreated) await (await host.Value.ConfigureAwait(false)).DisposeAsync().ConfigureAwait(false);
    }

    private static Task<McpProcessHost> CreateAsync() => McpProcessHost.StartAsync(
        new SymbolGraphMiniFixtureWorkspace(), TimeSpan.FromSeconds(60));
}

internal sealed class ReadOnlyMcpHostClient
{
    private readonly Lazy<Task<McpProcessHost>> host;

    public ReadOnlyMcpHostClient(Lazy<Task<McpProcessHost>> host) => this.host = host;

    public async Task<ModelContextProtocol.Protocol.CallToolResult> CallToolAsync(
        string toolName, IReadOnlyDictionary<string, object?>? arguments = null) =>
        await (await host.Value.ConfigureAwait(false)).CallToolAsync(toolName, arguments).ConfigureAwait(false);

    public async Task<string> CallToolGetTextAsync(
        string toolName, IReadOnlyDictionary<string, object?>? arguments = null) =>
        await (await host.Value.ConfigureAwait(false)).CallToolGetTextAsync(toolName, arguments).ConfigureAwait(false);
}
