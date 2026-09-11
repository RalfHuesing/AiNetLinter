#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Composition;
using AiNetLinter.IntegrationTests.Fixtures;
using AiNetLinter.IntegrationTests.Mcp.Platform;
using Xunit;

namespace AiNetLinter.IntegrationTests.Mcp;

/// <summary>
/// First-Principles-E2E-Test fuer das JSON-RPC-Framing des MCP-Servers: spawnt einen
/// <c>AiNetLinter.exe --mcp-server</c>-Subprozess, schreibt <c>initialize</c> oder
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
    private const int PreSlice20InstructionsUtf8Bytes = 1872;
    private const int PreSlice20ToolsListUtf8Bytes = 52694;
    private const int PreSlice20ToolDescriptionsUtf8Bytes = 32242;
    private const int PreSlice20InputSchemasUtf8Bytes = 13809;
    private const string InputSchemaFingerprint = "3062E4D76629034A473F8C32F9E92F08FB725F8EA6A4ADC1A652D82B2C90F9A2";
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

        var observedLines = await McpRawWireTestHarness.RunAndCollectStdoutAsync(fixture.SolutionPath, frames);

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

        var observedLines = await McpRawWireTestHarness.RunAndCollectStdoutAsync(fixture.SolutionPath, frames);

        Assert.NotEmpty(observedLines);
        foreach (var line in observedLines)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            using var doc = JsonDocument.Parse(line);
            Assert.Equal("2.0", doc.RootElement.GetProperty("jsonrpc").GetString());
        }
    }

    [Fact]
    public async Task SearchPatternCall_RawStructuredContentIsObjectAndTextRemains()
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
            fixture.SolutionPath,
            frames.ToArray(),
            new McpRawWireRunOptions { InterFrameDelay = TimeSpan.FromSeconds(5) });
        JsonElement? result = null;
        foreach (var line in observedLines)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            using var document = JsonDocument.Parse(line);
            if (!document.RootElement.TryGetProperty("result", out var candidate)
                || !candidate.TryGetProperty("structuredContent", out var candidateStructured)
                || candidateStructured.ValueKind != JsonValueKind.Object
                || !candidateStructured.TryGetProperty("matches", out _)) continue;
            result = candidate.Clone();
            break;
        }

        Assert.True(result.HasValue, "Kein structured search_pattern-Result auf dem Raw-Wire gefunden.");
        var structured = result.Value.GetProperty("structuredContent");
        Assert.Equal(JsonValueKind.Object, structured.ValueKind);
        Assert.True(structured.TryGetProperty("matches", out var matches), structured.GetRawText());
        Assert.Equal(JsonValueKind.Array, matches.ValueKind);
        Assert.Equal(JsonValueKind.Object, structured.GetProperty("completeness").ValueKind);
        Assert.True(structured.GetProperty("completeness").GetProperty("totalCount").GetInt32()
            >= structured.GetProperty("completeness").GetProperty("returnedCount").GetInt32());
        Assert.Equal(JsonValueKind.Object, structured.GetProperty("next").ValueKind);
        Assert.Contains(
            matches.EnumerateArray(),
            match => match.TryGetProperty("semantic", out var semantic)
                && semantic.ValueKind == JsonValueKind.Object);
        Assert.Contains("Greeter", result.Value.GetProperty("content")[0].GetProperty("text").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task DiscoveryTools_RawWireExposeRoutingAndBoundedFollowUpMetadata()
    {
        using var fixture = new SymbolGraphMiniFixtureWorkspace();
        var frames = new List<string>
        {
            "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"initialize\",\"params\":{" +
                "\"protocolVersion\":\"" + ProtocolVersion + "\",\"capabilities\":{}," +
                "\"clientInfo\":{\"name\":\"" + ClientName + "\",\"version\":\"" + ClientVersion + "\"}}}",
            "{\"jsonrpc\":\"2.0\",\"method\":\"notifications/initialized\"}",
        };
        for (var id = 2; id <= 5; id++)
        {
            frames.Add(JsonSerializer.Serialize(new
            {
                jsonrpc = "2.0",
                id,
                method = "tools/call",
                @params = new
                {
                    name = "get_file_tree",
                    arguments = new { view = "files", maxResults = 1 },
                },
            }));
        }
        for (var id = 6; id <= 9; id++)
        {
            frames.Add(JsonSerializer.Serialize(new
            {
                jsonrpc = "2.0",
                id,
                method = "tools/call",
                @params = new { name = "get_index_scope", arguments = new { } },
            }));
        }
        for (var id = 10; id <= 13; id++)
        {
            frames.Add(JsonSerializer.Serialize(new
            {
                jsonrpc = "2.0",
                id,
                method = "tools/call",
                @params = new
                {
                    name = "search_pattern",
                    arguments = new { pattern = "Greeter", maxResults = 1 },
                },
            }));
        }

        var lines = await McpRawWireTestHarness.RunAndCollectStdoutAsync(
            fixture.SolutionPath,
            frames.ToArray(),
            new McpRawWireRunOptions { InterFrameDelay = TimeSpan.FromSeconds(5) });

        var fileTreeResponse = McpRawWireTestHarness.FindResponseWithStructuredProperty(lines, 5, "fileTree").GetProperty("result");
        var fileTree = fileTreeResponse.GetProperty("structuredContent").GetProperty("fileTree");
        Assert.Contains(
            "maxResults",
            fileTree.GetProperty("completeness").GetProperty("truncatedBy").EnumerateArray()
                .Select(item => item.GetString()));
        Assert.Equal(JsonValueKind.Object, fileTree.GetProperty("next").ValueKind);

        var indexScope = McpRawWireTestHarness.FindResponseWithStructuredProperty(lines, 9, "routing").GetProperty("result")
            .GetProperty("structuredContent");
        Assert.Equal("ok", indexScope.GetProperty("status").GetString());
        Assert.Equal("find_symbol", indexScope.GetProperty("routing").GetProperty("cSharp").GetProperty("tool").GetString());
        Assert.Equal("search_pattern", indexScope.GetProperty("routing").GetProperty("nonCSharp").GetProperty("tool").GetString());

        var search = McpRawWireTestHarness.FindResponseWithStructuredProperty(lines, 13, "next").GetProperty("result");
        var searchStructured = search.GetProperty("structuredContent");
        Assert.Equal(JsonValueKind.Object, searchStructured.GetProperty("next").ValueKind);
        Assert.True(searchStructured.GetProperty("completeness").GetProperty("totalCount").GetInt32()
            >= searchStructured.GetProperty("completeness").GetProperty("returnedCount").GetInt32());
        Assert.Contains("[NEXT:", search.GetProperty("content")[0].GetProperty("text").GetString(), StringComparison.Ordinal);
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
            fixture.SolutionPath,
            frames.ToArray(),
            new McpRawWireRunOptions { InterFrameDelay = TimeSpan.FromSeconds(1) });
        JsonElement? response = null;
        foreach (var line in observedLines)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            using var document = JsonDocument.Parse(line);
            if (!document.RootElement.TryGetProperty("result", out var result) ||
                !result.TryGetProperty("structuredContent", out var candidateStructured) ||
                candidateStructured.ValueKind != JsonValueKind.Object ||
                !candidateStructured.TryGetProperty("callSites", out _)) continue;
            response = document.RootElement.Clone();
            break;
        }

        Assert.True(response.HasValue, "Kein erfolgreicher find_references-Response mit structuredContent gefunden.");
        var structuredContent = response.Value.GetProperty("result").GetProperty("structuredContent");

        Assert.Equal(JsonValueKind.Object, structuredContent.ValueKind);
        Assert.True(structuredContent.TryGetProperty("callSites", out var callSites), structuredContent.GetRawText());
        Assert.Equal(JsonValueKind.Array, callSites.ValueKind);
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

        var observedLines = await McpRawWireTestHarness.RunAndCollectStdoutAsync(fixture.SolutionPath, frameList.ToArray());

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

    [Fact]
    public async Task InitializeAndModernDiscovery_ExposeSameInstructionsAndRegisteredToolsWithinBudget()
    {
        using var initializeFixture = new SymbolGraphMiniFixtureWorkspace();
        using var modernFixture = new SymbolGraphMiniFixtureWorkspace();
        var expectedToolNames = await GetRegisteredToolNames();
        var initialize = await ReadDiscoverySnapshotAsync(initializeFixture.SolutionPath, modern: false);
        var modern = await ReadDiscoverySnapshotAsync(modernFixture.SolutionPath, modern: true);

        Assert.Equal(initialize.Instructions, modern.Instructions);
        Assert.Equal(initialize.InstructionsSize, modern.InstructionsSize);
        Assert.True(expectedToolNames.SetEquals(initialize.ToolNames));
        Assert.True(expectedToolNames.SetEquals(modern.ToolNames));
        Assert.True(initialize.ToolNames.SetEquals(modern.ToolNames));
        Assert.True(
           initialize.InstructionsSize.Utf8Bytes <= ServerInstructions.MaxUtf8Bytes,
           $"ServerInstructions: {initialize.InstructionsSize.Utf8Bytes} Bytes, " +
           $"Budget: {ServerInstructions.MaxUtf8Bytes} Bytes.");
        Assert.True(modern.InstructionsSize.Utf8Bytes <= ServerInstructions.MaxUtf8Bytes);
        Assert.True(
            modern.InstructionsSize.Utf8Bytes <= 1_200,
            $"Instructions: {modern.InstructionsSize.Utf8Bytes} Bytes.");
        Assert.True(
            modern.ToolsListPayload.Utf8Bytes <= PreSlice20ToolsListUtf8Bytes * 80 / 100,
            $"tools/list wurde nicht um mindestens 20 % reduziert: {modern.ToolsListPayload.Utf8Bytes} > {PreSlice20ToolsListUtf8Bytes * 80 / 100}.");
        Assert.True(
            initialize.ToolDescriptionsSize.Utf8Bytes <= PreSlice20ToolDescriptionsUtf8Bytes * 70 / 100,
            $"Toolbeschreibungen wurden nicht um mindestens 30 % reduziert: {initialize.ToolDescriptionsSize.Utf8Bytes} > {PreSlice20ToolDescriptionsUtf8Bytes * 70 / 100}.");
        Assert.Equal(PreSlice20InputSchemasUtf8Bytes, initialize.InputSchemasSize.Utf8Bytes);
        Assert.Equal(InputSchemaFingerprint, initialize.InputSchemaFingerprint);

        output.WriteLine($"initialize discovery: {initialize.DiscoveryPayload}");
        output.WriteLine($"initialize tools/list: {initialize.ToolsListPayload}");
        output.WriteLine($"Modern discovery: {modern.DiscoveryPayload}");
        output.WriteLine($"Modern tools/list: {modern.ToolsListPayload}");
        output.WriteLine($"Instructions: {initialize.InstructionsSize}");
        output.WriteLine($"Toolbeschreibungen: {initialize.ToolDescriptionsSize}; Inputschemas: {initialize.InputSchemasSize}; Schemafingerprint: {initialize.InputSchemaFingerprint}");
        output.WriteLine(
            $"Slice-20-Messung: Instructions {PreSlice20InstructionsUtf8Bytes} -> {initialize.InstructionsSize.Utf8Bytes}; " +
            $"Beschreibungen {PreSlice20ToolDescriptionsUtf8Bytes} -> {initialize.ToolDescriptionsSize.Utf8Bytes}; " +
            $"Inputschemas {PreSlice20InputSchemasUtf8Bytes} -> {initialize.InputSchemasSize.Utf8Bytes}; " +
            $"tools/list {PreSlice20ToolsListUtf8Bytes} -> {initialize.ToolsListPayload.Utf8Bytes} UTF-8-Bytes.");
    }

    private static async Task<McpWireDiscoverySnapshot> ReadDiscoverySnapshotAsync(
        string targetPath, bool modern)
    {
        var lines = await McpRawWireTestHarness.RunAndCollectStdoutAsync(
            targetPath,
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
        var tools = toolsResult.GetProperty("tools").EnumerateArray().ToArray();
        var descriptions = string.Concat(tools.Select(tool => tool.GetProperty("description").GetString()));
        var inputSchemas = string.Concat(tools.Select(tool => tool.GetProperty("inputSchema").GetRawText()));
        var schemaFingerprintInput = string.Join(
            "\n",
            tools.OrderBy(tool => tool.GetProperty("name").GetString(), StringComparer.Ordinal)
                .Select(tool => $"{tool.GetProperty("name").GetString()}:{tool.GetProperty("inputSchema").GetRawText()}"));
        var inputSchemaFingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(schemaFingerprintInput)));

        return new McpWireDiscoverySnapshot(
            instructions!,
            toolNames.ToHashSet(StringComparer.Ordinal),
            instructionSize,
            McpPayloadMeasurement.MeasureJson(discoveryResponse),
            McpPayloadMeasurement.MeasureJson(toolsResponse),
            McpPayloadMeasurement.Measure(descriptions),
            McpPayloadMeasurement.Measure(inputSchemas),
            inputSchemaFingerprint);
    }

    private static async Task<IReadOnlySet<string>> GetRegisteredToolNames()
    {
        await using var registry = ProjectRegistryFixture.CreateInspectionRegistry();
        return McpServerToolCollectionFactory.Build(
                registry,
                AnalysisToolCall.CreateTargetRoute(
                    ProjectAnalysisDispatcher.CreateRoute(registry),
                    AssemblyAnalysisDispatcher.CreateRoute(null)))
            .Select(tool => tool.ProtocolTool.Name)
            .ToHashSet(StringComparer.Ordinal);
    }

    private sealed record McpWireDiscoverySnapshot(
        string Instructions,
        IReadOnlySet<string> ToolNames,
        McpPayloadSize InstructionsSize,
        McpPayloadSize DiscoveryPayload,
        McpPayloadSize ToolsListPayload,
        McpPayloadSize ToolDescriptionsSize,
        McpPayloadSize InputSchemasSize,
        string InputSchemaFingerprint);
}
