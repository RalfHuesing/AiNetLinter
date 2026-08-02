#nullable enable

using System;
using System.Threading.Tasks;
using AiNetLinter.Tests.Mcp;
using Xunit;

namespace AiNetLinter.Tests.Fixtures;

public sealed class BaselineMcpFixture : IAsyncLifetime
{
    public BaselineMiniFixtureWorkspace Workspace { get; private set; } = null!;
    public McpTestClient Client { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        Workspace = new BaselineMiniFixtureWorkspace();
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
