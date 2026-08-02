#nullable enable

using System;
using System.Threading.Tasks;
using AiNetLinter.Tests.Mcp;
using Xunit;

namespace AiNetLinter.Tests.Fixtures;

/// <summary>
/// Erstellt einmalig pro Testklasse ein temporaeres <see cref="BaselineMiniFixtureWorkspace"/>
/// und verbindet einen <see cref="McpTestClient"/>.
/// Wird in Read-Only E2E-Tests via <see cref="IClassFixture{BaselineMcpFixture}"/> verwendet.
/// </summary>
public sealed class BaselineMcpFixture : IAsyncLifetime
{
    public BaselineMiniFixtureWorkspace Workspace { get; private set; } = null!;
    public McpTestClient Client { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        Workspace = new BaselineMiniFixtureWorkspace();
        Client = await McpTestClient.ConnectAsync(Workspace.RootPath);
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
