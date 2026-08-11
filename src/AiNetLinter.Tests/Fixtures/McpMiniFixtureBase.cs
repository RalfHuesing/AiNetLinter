#nullable enable

using System;
using System.Threading.Tasks;
using AiNetLinter.Tests.Mcp;
using Xunit;

namespace AiNetLinter.Tests.Fixtures;

/// <summary>
/// Erstellt einmalig pro Testklasse/-Collection ein <typeparamref name="TWorkspace"/> und verbindet
/// einen <see cref="McpTestClient"/> darauf — gemeinsame Basis fuer <see cref="BaselineMcpFixture"/>
/// und <see cref="SymbolGraphMcpFixture"/>, die sich nur im konkreten <see cref="FixtureWorkspaceBase"/>-
/// Subtyp unterscheiden.
/// </summary>
public abstract class McpMiniFixtureBase<TWorkspace> : IAsyncLifetime
    where TWorkspace : FixtureWorkspaceBase, new()
{
    public TWorkspace Workspace { get; private set; } = null!;
    public McpTestClient Client { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        Workspace = new TWorkspace();
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
