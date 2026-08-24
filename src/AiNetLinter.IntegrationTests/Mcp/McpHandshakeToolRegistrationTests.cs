#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.IntegrationTests.Fixtures;
using AiNetLinter.IntegrationTests.Mcp.Platform;
using ModelContextProtocol.Client;
using Xunit;

namespace AiNetLinter.IntegrationTests.Mcp;

/// <summary>
/// MSE-Baustein "MCP-Handshake/Toolregistrierung gegen eine Mini-Solution": startet
/// <c>AiNetLinter.exe --mcp-server</c> als echten Subprozess gegen die Mini-Fixture
/// <c>tests/Fixtures/BaselineMini</c>, fuehrt den JSON-RPC-<c>initialize</c>-Handshake ueber
/// <see cref="McpClient.CreateAsync(ModelContextProtocol.Client.IClientTransport, ModelContextProtocol.Client.McpClientOptions?, System.Threading.CancellationToken)"/>
/// durch und ruft <c>tools/list</c> auf.
/// </summary>
[Trait("Category", "Integration")]
public sealed class McpHandshakeToolRegistrationTests
{
    [Fact]
    public async Task ConnectAndListTools_AgainstMiniFixture_RegistersExpectedTools()
    {
        var exePath = Path.Combine(AppContext.BaseDirectory, "AiNetLinter.exe");
        Assert.True(File.Exists(exePath), $"Erwartete AiNetLinter.exe nicht in BaseDirectory gefunden: {exePath}");

        using var fixture = new BaselineMiniFixtureWorkspace();
        var fixtureRoot = fixture.RootPath;
        McpFixtureProjectDefinition.Ensure(fixtureRoot);

        var transport = new StdioClientTransport(new StdioClientTransportOptions
        {
            Name = "ainetlinter-mse-mcp-handshake-test",
            Command = exePath,
            Arguments = ["--mcp-server"],
            WorkingDirectory = fixtureRoot,
            EnvironmentVariables = new Dictionary<string, string?>
            {
                ["AINETLINTER_NO_DAEMON"] = "1",
            },
        });

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await using var client = await McpClient.CreateAsync(transport, cancellationToken: cts.Token);

        var tools = await client.ListToolsAsync(cancellationToken: cts.Token);

        Assert.Contains(tools, t => t.Name == "find_symbol");
        Assert.Contains(tools, t => t.Name == "get_violations");
        Assert.Contains(tools, t => t.Name == "get_test_context");
    }
}
