#nullable enable

using System;
using System.Threading.Tasks;
using AiNetLinter.Tests.Mcp;
using Xunit;

namespace AiNetLinter.Tests.Fixtures;

/// <summary>
/// Erstellt einmalig pro Testklasse ein temporaeres <see cref="SymbolGraphMiniFixtureWorkspace"/>
/// und verbindet einen <see cref="McpTestClient"/>.
/// Wird in Read-Only E2E-Tests via <see cref="CollectionAttribute"/> auf
/// <see cref="SymbolGraphMcpCollection"/> geteilt verwendet.
/// </summary>
public sealed class SymbolGraphMcpFixture : IAsyncLifetime
{
    public SymbolGraphMiniFixtureWorkspace Workspace { get; private set; } = null!;
    public McpTestClient Client { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        Workspace = new SymbolGraphMiniFixtureWorkspace();
        Client = await McpTestClient.ConnectAsync(Workspace.RootPath, timeoutSeconds: 60,
            retryOptions: new McpTestClientRetryOptions(MaxRetries: 5, BaseDelayMs: 1000, BackoffFactor: 2.0));
    }

    public async ValueTask DisposeAsync()
    {
        if (Client is not null)
        {
            await Client.DisposeAsync();
        }

        Workspace?.Dispose();
    }
}
