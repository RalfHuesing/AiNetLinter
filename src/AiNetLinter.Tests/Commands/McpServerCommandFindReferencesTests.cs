#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using AiNetLinter.Tests.Fixtures;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.Tests.Commands;

/// <summary>
/// E2E-Tests fuer <c>find_references</c> ausgelagert aus <c>McpServerCommandTests.cs</c>, weil diese
/// Datei bereits am <c>MaxLineCount: 500</c>-Limit liegt. Thematisch fokussiert auf die 005-Erweiterung:
/// P0/P1-Trunkierung via <c>maxResults</c>-Parameter (Konzept Z. 215-225) im realen MCP-Subprozess.
/// </summary>
[Collection("ConsoleTestCollection")]
public sealed class McpServerCommandFindReferencesTests
{
    [Fact]
    public async Task RunAsync_ValidFixture_FindReferencesWithMaxResultsTruncates()
    {
        using var fixture = new SymbolGraphMiniFixtureWorkspace();
        var exePath = Path.Combine(AppContext.BaseDirectory, "AiNetLinter.exe");
        Assert.True(File.Exists(exePath), $"Erwartete AiNetLinter.exe nicht gefunden: {exePath}");

        var transport = new StdioClientTransport(new StdioClientTransportOptions
        {
            Name = "ainetlinter-mcp-test-client",
            Command = exePath,
            Arguments = ["--mcp-server", "--path", fixture.RootPath],
        });

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await using var client = await McpClient.CreateAsync(transport, cancellationToken: cts.Token);
        var result = await client.CallToolAsync(
            "find_references",
            new Dictionary<string, object?> { ["symbolIdentifier"] = "Greeter.Greet", ["maxResults"] = 2 },
            cancellationToken: cts.Token);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("Treffer gesamt", textContent.Text, StringComparison.Ordinal);
        Assert.Contains("2 gezeigt", textContent.Text, StringComparison.Ordinal);
    }
}
