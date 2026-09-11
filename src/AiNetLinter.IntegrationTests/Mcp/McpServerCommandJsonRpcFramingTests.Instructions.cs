#nullable enable

using System;
using System.Text.Json;
using System.Threading.Tasks;
using AiNetLinter.IntegrationTests.Fixtures;
using AiNetLinter.IntegrationTests.Mcp.Platform;
using Xunit;

namespace AiNetLinter.IntegrationTests.Mcp;

public sealed partial class McpServerCommandJsonRpcFramingTests
{
    [Fact]
    public async Task Initialize_ResponseInstructionsField_ContainsServerInstructionsDoctrine()
    {
        using var fixture = new SymbolGraphMiniFixtureWorkspace();
        var frames = new[]
        {
            "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"initialize\",\"params\":{" +
                "\"protocolVersion\":\"" + ProtocolVersion + "\",\"capabilities\":{}," +
                "\"clientInfo\":{\"name\":\"" + ClientName + "\",\"version\":\"" + ClientVersion + "\"}}}",
            "{\"jsonrpc\":\"2.0\",\"method\":\"notifications/initialized\"}",
        };
        var observedLines = await McpRawWireTestHarness.RunAndCollectStdoutAsync(fixture.SolutionPath, frames);
        string? instructions = null;
        foreach (var line in observedLines)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            using var doc = JsonDocument.Parse(line);
            if (!doc.RootElement.TryGetProperty("id", out var id) || id.GetInt32() != 1) continue;
            instructions = doc.RootElement.GetProperty("result").GetProperty("instructions").GetString();
            break;
        }
        Assert.False(string.IsNullOrEmpty(instructions));
        Assert.Contains("search_pattern", instructions, StringComparison.Ordinal);
        Assert.Contains("structuredContent.navigation", instructions, StringComparison.Ordinal);
        Assert.Contains("tools/list", instructions, StringComparison.Ordinal);
    }
}
