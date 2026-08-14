#nullable enable

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
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

        var fixtureRoot = Path.Combine(SolutionRootLocator.Find(), "tests", "Fixtures", "BaselineMini");

        var transport = new StdioClientTransport(new StdioClientTransportOptions
        {
            Name = "ainetlinter-mse-mcp-handshake-test",
            Command = exePath,
            Arguments = ["--mcp-server", "--path", fixtureRoot],
        });

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await using var client = await McpClient.CreateAsync(transport, cancellationToken: cts.Token);

        var tools = await client.ListToolsAsync(cancellationToken: cts.Token);

        Assert.Contains(tools, t => t.Name == "find_symbol");
        Assert.Contains(tools, t => t.Name == "get_violations");
    }
}
