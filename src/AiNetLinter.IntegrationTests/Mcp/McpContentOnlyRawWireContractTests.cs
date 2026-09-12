#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using AiNetLinter.IntegrationTests.Fixtures;
using AiNetLinter.IntegrationTests.Mcp.Platform;

namespace AiNetLinter.IntegrationTests.Mcp;

[Trait("Category", "Integration")]
public sealed class McpContentOnlyRawWireContractTests
{
    [Fact]
    public async Task RepresentativeToolResponses_UseOnlyOneNonEmptyTextBlockOnTheRawWire()
    {
        using var fixture = new SymbolGraphMiniFixtureWorkspace();
        var frames = BuildFrames();
        var lines = await McpRawWireTestHarness.RunAndCollectStdoutAsync(
            fixture.SolutionPath,
            frames,
            new McpRawWireRunOptions { InterFrameDelay = TimeSpan.FromSeconds(2) });
        var responses = new[]
        {
            ReadResponse(lines, 6, "success/find_symbol"),
            ReadResponse(lines, 7, "empty/find_symbol"),
            ReadResponse(lines, 8, "truncated/get_file_tree"),
            ReadResponse(lines, 9, "error/find_symbol"),
        };

        AssertContentOnly(responses);
        AssertSmallerThanFormerDualPayload(responses);
    }

    private static string[] BuildFrames()
    {
        var frames = new List<string>
        {
            "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"initialize\",\"params\":{" +
                "\"protocolVersion\":\"2024-11-05\",\"capabilities\":{}," +
                "\"clientInfo\":{\"name\":\"ContentOnlyContract\",\"version\":\"1.0\"}}}",
            "{\"jsonrpc\":\"2.0\",\"method\":\"notifications/initialized\"}",
        };
        for (var id = 2; id <= 6; id++)
        {
            frames.Add(ToolCall(id, "find_symbol", new { namePatterns = new[] { "Greeter" } }));
        }

        frames.Add(ToolCall(7, "find_symbol", new { namePatterns = new[] { "DefinitelyAbsentSymbol" } }));
        frames.Add(ToolCall(8, "get_file_tree", new { view = "files", maxResults = 1 }));
        frames.Add(ToolCall(9, "find_symbol", new { namePatterns = new[] { "Greeter" }, maxResults = 0 }));
        return frames.ToArray();
    }

    private static string ToolCall(int id, string name, object arguments) =>
        JsonSerializer.Serialize(new
        {
            jsonrpc = "2.0",
            id,
            method = "tools/call",
            @params = new { name, arguments },
        });

    private static RawToolResponse ReadResponse(IReadOnlyList<string> lines, int id, string scenario)
    {
        var response = McpRawWireTestHarness.FindResponse(lines, id);
        Assert.True(response.TryGetProperty("result", out var result), response.GetRawText());
        return new RawToolResponse(scenario, result.Clone());
    }

    private static void AssertContentOnly(IEnumerable<RawToolResponse> responses)
    {
        var violations = new List<string>();
        foreach (var response in responses)
        {
            if (!response.Result.TryGetProperty("content", out var content)
                || content.ValueKind != JsonValueKind.Array
                || content.GetArrayLength() != 1)
            {
                violations.Add($"{response.Scenario}: expected exactly one content block.");
            }
            else
            {
                var block = content[0];
                if (!block.TryGetProperty("type", out var type) || type.GetString() != "text"
                    || !block.TryGetProperty("text", out var text) || string.IsNullOrWhiteSpace(text.GetString()))
                {
                    violations.Add($"{response.Scenario}: expected one non-empty text content block.");
                }
            }

            var unexpectedProperties = response.Result
                .EnumerateObject()
                .Select(property => property.Name)
                .Where(name => name is not ("content" or "isError"))
                .ToArray();
            if (unexpectedProperties.Length > 0)
            {
                violations.Add($"{response.Scenario}: raw result has unexpected properties: {string.Join(", ", unexpectedProperties)}.");
            }
        }

        var measurements = string.Join(
            Environment.NewLine,
            responses.Select(Measure));
        Assert.True(
            violations.Count == 0,
            "Raw-wire Content-only contract violations:" + Environment.NewLine
            + string.Join(Environment.NewLine, violations)
            + Environment.NewLine
            + "Current dual-output UTF-8 matrix:" + Environment.NewLine
            + measurements);
    }

    private static string Measure(RawToolResponse response)
    {
        var text = response.Result.GetProperty("content")[0].GetProperty("text").GetString()!;
        var textBytes = McpPayloadMeasurement.Measure(text).Utf8Bytes;
        return $"{response.Scenario}: content={textBytes}";
    }

    private static void AssertSmallerThanFormerDualPayload(IEnumerable<RawToolResponse> responses)
    {
        var formerDualPayloadBytes = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["success/find_symbol"] = 1_343,
            ["empty/find_symbol"] = 1_238,
            ["truncated/get_file_tree"] = 2_332,
            ["error/find_symbol"] = 1_117,
        };

        var violations = responses
            .Select(response =>
            {
                var text = response.Result.GetProperty("content")[0].GetProperty("text").GetString()!;
                var contentBytes = McpPayloadMeasurement.Measure(text).Utf8Bytes;
                var formerBytes = formerDualPayloadBytes[response.Scenario];
                return contentBytes < formerBytes
                    ? null
                    : $"{response.Scenario}: content={contentBytes}, former dual payload={formerBytes}.";
            })
            .Where(message => message is not null)
            .ToArray();

        Assert.True(
            violations.Length == 0,
            "Content-only responses must be smaller than their former dual payload:" + Environment.NewLine
            + string.Join(Environment.NewLine, violations));
    }

    private sealed record RawToolResponse(string Scenario, JsonElement Result);
}
