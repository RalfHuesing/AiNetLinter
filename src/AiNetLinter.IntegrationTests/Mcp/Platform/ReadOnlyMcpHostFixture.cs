#nullable enable

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AiNetLinter.IntegrationTests.Fixtures;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace AiNetLinter.IntegrationTests.Mcp.Platform;

public sealed class ReadOnlyMcpHostFixture : McpHostFixtureBase
{
    private protected override Task<McpProcessHost> CreateProcessHostAsync() => McpProcessHost.StartAsync(
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

    public async Task<IList<McpClientTool>> ListToolsAsync() =>
        await (await host.Value.ConfigureAwait(false)).ListToolsAsync().ConfigureAwait(false);

    public async Task<IList<McpClientResource>> ListResourcesAsync() =>
        await (await host.Value.ConfigureAwait(false)).ListResourcesAsync().ConfigureAwait(false);

    public async Task<IList<McpClientResourceTemplate>> ListResourceTemplatesAsync() =>
        await (await host.Value.ConfigureAwait(false)).ListResourceTemplatesAsync().ConfigureAwait(false);

    public async Task<ReadResourceResult> ReadResourceAsync(string uri) =>
        await (await host.Value.ConfigureAwait(false)).ReadResourceAsync(uri).ConfigureAwait(false);
}
