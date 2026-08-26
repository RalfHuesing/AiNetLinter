#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using AiNetLinter.Mcp;
using AiNetLinter.IntegrationTests.Fixtures;
using AiNetLinter.IntegrationTests.Mcp.Platform;
using Xunit;

namespace AiNetLinter.IntegrationTests.Mcp;

/// <summary>
/// First-Principles-E2E-Test fuer das JSON-RPC-Framing des MCP-Servers: spawnt einen
/// <c>AiNetLinter.exe --mcp-server</c>-Subprozess, schreibt Legacy-<c>initialize</c> oder
/// modernes <c>server/discover</c> sowie <c>tools/list</c> und <c>tools/call</c> manuell als
/// newline-delimited JSON auf stdin, liest stdout zeilenweise roh zurueck und verifiziert
/// <b>jede</b> Zeile als gueltigen JSON-RPC-Frame (<c>jsonrpc == "2.0"</c>).
/// Dieser Test umgeht bewusst den SDK-Parser zwischen Subprozess und Assertions - ein
/// einziger ungefilterter <c>Console.WriteLine</c>-Call aus irgendeiner zentralen
/// Hilfsklasse wuerde das JSON-RPC-Framing der gesamten Session zerstoeren und hier als
/// nicht-JSON-Zeile sichtbar werden. Regressions-Schutz fuer die strukturelle
/// stderr-Disziplin (alles Loggen ausschliesslich via Console.Error).
/// </summary>
[Trait("Category", "Integration")]
public sealed class McpServerCommandJsonRpcFramingTests
{
    private const string ProtocolVersion = "2024-11-05";
    private const string ModernProtocolVersion = "2026-07-28";
    private const int AnnotationPayloadBaselineUtf8Bytes = 20836;
    private const string ClientName = "FramingTestClient";
    private const string ClientVersion = "1.0.0";
    private readonly ITestOutputHelper output;

    public McpServerCommandJsonRpcFramingTests(ITestOutputHelper output) => this.output = output;

    [Fact]
    public async Task HandshakeOnly_AllStdoutLinesAreValidJsonRpcFrames()
    {
        using var fixture = new SymbolGraphMiniFixtureWorkspace();

        var frames = new[]
        {
            "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"initialize\",\"params\":{" +
                "\"protocolVersion\":\"" + ProtocolVersion + "\"," +
                "\"capabilities\":{}," +
                "\"clientInfo\":{\"name\":\"" + ClientName + "\",\"version\":\"" + ClientVersion + "\"}}}",
            "{\"jsonrpc\":\"2.0\",\"method\":\"notifications/initialized\"}",
            "{\"jsonrpc\":\"2.0\",\"id\":2,\"method\":\"tools/list\"}",
        };

        var observedLines = await McpRawWireTestHarness.RunAndCollectStdoutAsync(fixture.RootPath, frames);

        Assert.NotEmpty(observedLines);
        foreach (var line in observedLines)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            using var doc = JsonDocument.Parse(line);
            Assert.Equal("2.0", doc.RootElement.GetProperty("jsonrpc").GetString());
        }
    }

    [Fact]
    public async Task HandshakeAndSingleToolCall_AllStdoutLinesAreValidJsonRpcFrames()
    {
        using var fixture = new SymbolGraphMiniFixtureWorkspace();

        var frames = new[]
        {
            "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"initialize\",\"params\":{" +
                "\"protocolVersion\":\"" + ProtocolVersion + "\"," +
                "\"capabilities\":{}," +
                "\"clientInfo\":{\"name\":\"" + ClientName + "\",\"version\":\"" + ClientVersion + "\"}}}",
            "{\"jsonrpc\":\"2.0\",\"method\":\"notifications/initialized\"}",
            "{\"jsonrpc\":\"2.0\",\"id\":2,\"method\":\"tools/call\",\"params\":{" +
                "\"name\":\"find_symbol\",\"arguments\":{\"namePatterns\":[\"Greeter\"]}}}",
        };

        var observedLines = await McpRawWireTestHarness.RunAndCollectStdoutAsync(fixture.RootPath, frames);

        Assert.NotEmpty(observedLines);
        foreach (var line in observedLines)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            using var doc = JsonDocument.Parse(line);
            Assert.Equal("2.0", doc.RootElement.GetProperty("jsonrpc").GetString());
        }
    }

    [Fact]
    public async Task SearchPatternCall_RawStructuredContentIsObjectAndLegacyTextRemains()
    {
        using var fixture = new SymbolGraphMiniFixtureWorkspace();
        var frames = new List<string>
        {
            "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"initialize\",\"params\":{" +
                "\"protocolVersion\":\"" + ProtocolVersion + "\",\"capabilities\":{}," +
                "\"clientInfo\":{\"name\":\"" + ClientName + "\",\"version\":\"" + ClientVersion + "\"}}}",
            "{\"jsonrpc\":\"2.0\",\"method\":\"notifications/initialized\"}",
        };
        for (var id = 2; id <= 8; id++)
        {
            frames.Add(JsonSerializer.Serialize(new
            {
                jsonrpc = "2.0",
                id,
                method = "tools/call",
                @params = new
                {
                    name = "search_pattern",
                    arguments = new { pattern = "Greeter", contextLines = 1, maxFiles = 1, enrichCSharp = true },
                },
            }));
        }

        var observedLines = await McpRawWireTestHarness.RunAndCollectStdoutAsync(
            fixture.RootPath,
            frames.ToArray(),
            TimeSpan.FromSeconds(5));
        JsonElement? result = null;
        foreach (var line in observedLines)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            using var document = JsonDocument.Parse(line);
            if (!document.RootElement.TryGetProperty("result", out var candidate)
                || !candidate.TryGetProperty("structuredContent", out _)) continue;
            result = candidate.Clone();
            break;
        }

        Assert.True(result.HasValue, "Kein structured search_pattern-Result auf dem Raw-Wire gefunden.");
        var structured = result.Value.GetProperty("structuredContent");
        Assert.Equal(JsonValueKind.Object, structured.ValueKind);
        Assert.Equal(JsonValueKind.Array, structured.GetProperty("matches").ValueKind);
        Assert.Equal(JsonValueKind.Object, structured.GetProperty("completeness").ValueKind);
        Assert.Contains(
            structured.GetProperty("matches").EnumerateArray(),
            match => match.TryGetProperty("semantic", out var semantic)
                && semantic.ValueKind == JsonValueKind.Object);
        Assert.Contains("Greeter", result.Value.GetProperty("content")[0].GetProperty("text").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task SymbolGraphDepthToolCall_RawStructuredContentRemainsJsonObject()
    {
        using var fixture = new SymbolGraphMiniFixtureWorkspace();

        var frames = new List<string>
        {
            "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"initialize\",\"params\":{" +
                "\"protocolVersion\":\"" + ProtocolVersion + "\"," +
                "\"capabilities\":{},\"clientInfo\":{\"name\":\"" + ClientName + "\",\"version\":\"" + ClientVersion + "\"}}}",
            "{\"jsonrpc\":\"2.0\",\"method\":\"notifications/initialized\"}",
        };
        for (var id = 2; id <= 12; id++)
        {
            frames.Add(JsonSerializer.Serialize(new
            {
                jsonrpc = "2.0",
                id,
                method = "tools/call",
                @params = new
                {
                    name = "find_references",
                    arguments = new { symbolIdentifier = "Greeter.Greet", depth = 2 },
                },
            }));
        }

        var observedLines = await McpRawWireTestHarness.RunAndCollectStdoutAsync(
            fixture.RootPath, frames.ToArray(), TimeSpan.FromSeconds(1));
        JsonElement? response = null;
        foreach (var line in observedLines)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            using var document = JsonDocument.Parse(line);
            if (!document.RootElement.TryGetProperty("result", out var result) ||
                !result.TryGetProperty("structuredContent", out _)) continue;
            response = document.RootElement.Clone();
            break;
        }

        Assert.True(response.HasValue, "Kein erfolgreicher find_references-Response mit structuredContent gefunden.");
        var structuredContent = response.Value.GetProperty("result").GetProperty("structuredContent");

        Assert.Equal(JsonValueKind.Object, structuredContent.ValueKind);
        Assert.Equal(JsonValueKind.Array, structuredContent.GetProperty("callSites").ValueKind);
        Assert.Equal(JsonValueKind.Object, structuredContent.GetProperty("completeness").ValueKind);
        Assert.Equal(2, structuredContent.GetProperty("completeness").GetProperty("effectiveDepth").GetInt32());
    }

    [Fact]
    public async Task HandshakeAndTenToolCallsSequentially_AllStdoutLinesAreValidJsonRpcFrames()
    {
        using var fixture = new SymbolGraphMiniFixtureWorkspace();

        var toolNames = new[]
        {
            "find_symbol",
            "find_references",
            "get_impact",
            "get_type_hierarchy",
            "get_file_skeleton",
            "get_index_scope",
            "get_hotspots",
            "get_violations",
            "search_pattern",
            "metrics_tree",
        };

        var frameList = new System.Collections.Generic.List<string>
        {
            "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"initialize\",\"params\":{" +
                "\"protocolVersion\":\"" + ProtocolVersion + "\"," +
                "\"capabilities\":{}," +
                "\"clientInfo\":{\"name\":\"" + ClientName + "\",\"version\":\"" + ClientVersion + "\"}}}",
            "{\"jsonrpc\":\"2.0\",\"method\":\"notifications/initialized\"}",
        };

        var id = 2;
        foreach (var toolName in toolNames)
        {
            var args = toolName switch
            {
                "find_symbol" => "{\"namePatterns\":[\"Greeter\"]}",
                "find_references" => "{\"symbolIdentifier\":\"Greeter\"}",
                "get_impact" => "{\"symbolIdentifier\":\"Greeter\"}",
                "get_type_hierarchy" => "{\"symbolIdentifier\":\"Greeter\"}",
                "get_file_skeleton" => "{\"filePaths\":[\"src/SymbolGraphMini/Greeter.cs\"]}",
                "search_pattern" => "{\"pattern\":\"Greeter\"}",
                _ => "{}",
            };
            frameList.Add(
                "{\"jsonrpc\":\"2.0\",\"id\":" + id + ",\"method\":\"tools/call\",\"params\":{" +
                    "\"name\":\"" + toolName + "\",\"arguments\":" + args + "}}");
            id++;
        }

        var observedLines = await McpRawWireTestHarness.RunAndCollectStdoutAsync(fixture.RootPath, frameList.ToArray());

        Assert.NotEmpty(observedLines);
        foreach (var line in observedLines)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            using var doc = JsonDocument.Parse(line);
            Assert.Equal("2.0", doc.RootElement.GetProperty("jsonrpc").GetString());
        }
    }

    [Fact]
    public async Task Initialize_ResponseInstructionsField_ContainsServerInstructionsDoctrine()
    {
        // Die ServerInstructions.Text-Doctrine muss tatsaechlich im initialize-Response auf dem
        // Wire ankommen — nicht nur auf McpServerOptions-Ebene (siehe McpServerOptionsFactoryTests
        // fuer den Options-Ebenen-Test). Roher JSON-Parse gegen das "instructions"-Feld
        // (JSON-Property-Name laut ModelContextProtocol.Core InitializeResult), bewusst ohne
        // SDK-Client, analog zu den anderen Framing-Tests in dieser Klasse.
        using var fixture = new SymbolGraphMiniFixtureWorkspace();

        var frames = new[]
        {
            "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"initialize\",\"params\":{" +
                "\"protocolVersion\":\"" + ProtocolVersion + "\"," +
                "\"capabilities\":{}," +
                "\"clientInfo\":{\"name\":\"" + ClientName + "\",\"version\":\"" + ClientVersion + "\"}}}",
            "{\"jsonrpc\":\"2.0\",\"method\":\"notifications/initialized\"}",
        };

        var observedLines = await McpRawWireTestHarness.RunAndCollectStdoutAsync(fixture.RootPath, frames);

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
        Assert.Contains("Sufficiency", instructions, StringComparison.Ordinal);
        Assert.Contains("isError-Policy", instructions, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LegacyAndModernDiscovery_ExposeSameInstructionsAndRegisteredToolsWithinBudget()
    {
        using var legacyFixture = new SymbolGraphMiniFixtureWorkspace();
        using var modernFixture = new SymbolGraphMiniFixtureWorkspace();
        var expectedToolNames = await GetRegisteredToolNames();
        var legacy = await ReadDiscoverySnapshotAsync(legacyFixture.RootPath, modern: false);
        var modern = await ReadDiscoverySnapshotAsync(modernFixture.RootPath, modern: true);

        Assert.Equal(legacy.Instructions, modern.Instructions);
        Assert.Equal(legacy.InstructionsSize, modern.InstructionsSize);
        Assert.True(expectedToolNames.SetEquals(legacy.ToolNames));
        Assert.True(expectedToolNames.SetEquals(modern.ToolNames));
        Assert.True(legacy.ToolNames.SetEquals(modern.ToolNames));
        Assert.True(
           legacy.InstructionsSize.Utf8Bytes <= ServerInstructions.MaxUtf8Bytes,
           $"ServerInstructions: {legacy.InstructionsSize.Utf8Bytes} Bytes, " +
           $"Budget: {ServerInstructions.MaxUtf8Bytes} Bytes.");
        Assert.True(modern.InstructionsSize.Utf8Bytes <= ServerInstructions.MaxUtf8Bytes);

        output.WriteLine($"Legacy discovery: {legacy.DiscoveryPayload}");
        output.WriteLine($"Legacy tools/list: {legacy.ToolsListPayload}");
        output.WriteLine($"Modern discovery: {modern.DiscoveryPayload}");
        output.WriteLine($"Modern tools/list: {modern.ToolsListPayload}");
        output.WriteLine($"Instructions: {legacy.InstructionsSize}");
        output.WriteLine(
            $"Annotation payload delta (Legacy tools/list): " +
            $"{legacy.ToolsListPayload.Utf8Bytes - AnnotationPayloadBaselineUtf8Bytes:+#;-#;0} UTF-8-Bytes " +
            $"({AnnotationPayloadBaselineUtf8Bytes} -> {legacy.ToolsListPayload.Utf8Bytes}; " +
            "Baseline 2026-08-20)");
    }

    private static async Task<McpWireDiscoverySnapshot> ReadDiscoverySnapshotAsync(
        string targetDirectory, bool modern)
    {
        var lines = await McpRawWireTestHarness.RunAndCollectStdoutAsync(
            targetDirectory,
            McpRawWireTestHarness.BuildDiscoveryFrames(modern));
        var discoveryResponse = McpRawWireTestHarness.FindResponse(lines, 1);
        var toolsResponse = McpRawWireTestHarness.FindResponse(lines, 2);
        Assert.Equal("2.0", discoveryResponse.GetProperty("jsonrpc").GetString());
        Assert.Equal("2.0", toolsResponse.GetProperty("jsonrpc").GetString());
        if (!discoveryResponse.TryGetProperty("result", out var discoveryResult))
        {
            throw new InvalidOperationException(
                $"Discovery-Antwort ohne result (modern={modern}): {discoveryResponse.GetRawText()}");
        }
        if (modern)
        {
            var supportedVersions = discoveryResult.GetProperty("supportedVersions")
                .EnumerateArray()
                .Select(version => version.GetString())
                .ToHashSet(StringComparer.Ordinal);
            Assert.Contains(ModernProtocolVersion, supportedVersions);
        }

        var instructions = discoveryResult.GetProperty("instructions").GetString();
        Assert.False(string.IsNullOrEmpty(instructions));
        var instructionSize = McpPayloadMeasurement.Measure(instructions!);
        if (!toolsResponse.TryGetProperty("result", out var toolsResult))
        {
            throw new InvalidOperationException(
                $"tools/list-Antwort ohne result (modern={modern}): {toolsResponse.GetRawText()}");
        }

        var toolNames = toolsResult.GetProperty("tools")
            .EnumerateArray()
            .Select(tool => tool.GetProperty("name").GetString()!)
            .ToArray();
        Assert.Equal(toolNames.Length, toolNames.Distinct(StringComparer.Ordinal).Count());

        return new McpWireDiscoverySnapshot(
            instructions!,
            toolNames.ToHashSet(StringComparer.Ordinal),
            instructionSize,
            McpPayloadMeasurement.MeasureJson(discoveryResponse),
            McpPayloadMeasurement.MeasureJson(toolsResponse));
    }

    private static async Task<IReadOnlySet<string>> GetRegisteredToolNames()
    {
        await using var registry = ProjectRegistryFixture.CreateInspectionRegistry();
        return McpServerOptionsFactory.BuildToolCollection(registry)
            .Select(tool => tool.ProtocolTool.Name)
            .ToHashSet(StringComparer.Ordinal);
    }

    private sealed record McpWireDiscoverySnapshot(
        string Instructions,
        IReadOnlySet<string> ToolNames,
        McpPayloadSize InstructionsSize,
        McpPayloadSize DiscoveryPayload,
        McpPayloadSize ToolsListPayload);
}
