#nullable enable

using System;
using System.IO;
using System.Threading.Tasks;
using AiNetLinter.Tests.Mcp;
using Xunit;

namespace AiNetLinter.Tests.Fixtures;

/// <summary>
/// Startet einmalig pro Testklasse den MCP-Server-Prozess gegen das echte Repository (AiNetLinter.slnx).
/// Wird in Read-Only Integrationstests via <see cref="IClassFixture{McpLiveRepositoryFixture}"/> verwendet.
/// </summary>
public sealed class McpLiveRepositoryFixture : IAsyncLifetime
{
    public McpTestClient Client { get; private set; } = null!;
    public string RepositoryRoot { get; private set; } = string.Empty;

    public async ValueTask InitializeAsync()
    {
        RepositoryRoot = FindRepositoryRoot();
        Client = await McpTestClient.ConnectAsync(RepositoryRoot, timeoutSeconds: 60,
            retryOptions: new McpTestClientRetryOptions(MaxRetries: 5, BaseDelayMs: 1000, BackoffFactor: 2.0));
    }

    public async ValueTask DisposeAsync()
    {
        if (Client is not null)
        {
            await Client.DisposeAsync();
        }
    }

    private static string FindRepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "AiNetLinter.slnx")))
            {
                return dir.FullName;
            }
            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("AiNetLinter.slnx konnte im Elternverzeichnispfad nicht gefunden werden.");
    }
}
