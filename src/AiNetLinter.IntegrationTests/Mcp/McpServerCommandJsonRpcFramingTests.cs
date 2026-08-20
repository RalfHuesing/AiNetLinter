#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp;
using AiNetLinter.IntegrationTests.Fixtures;
using AiNetLinter.IntegrationTests.Mcp.Platform;
using AiNetLinter.IntegrationTests.Platform;
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

        var observedLines = await RunAndCollectStdoutAsync(fixture.RootPath, frames);

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
                "\"name\":\"find_symbol\",\"arguments\":{\"namePattern\":\"Greeter\"}}}",
        };

        var observedLines = await RunAndCollectStdoutAsync(fixture.RootPath, frames);

        Assert.NotEmpty(observedLines);
        foreach (var line in observedLines)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            using var doc = JsonDocument.Parse(line);
            Assert.Equal("2.0", doc.RootElement.GetProperty("jsonrpc").GetString());
        }
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
                "find_symbol" => "{\"namePattern\":\"Greeter\"}",
                "find_references" => "{\"symbolIdentifier\":\"Greeter\"}",
                "get_impact" => "{\"symbolIdentifier\":\"Greeter\"}",
                "get_type_hierarchy" => "{\"symbolIdentifier\":\"Greeter\"}",
                "get_file_skeleton" => "{\"filePath\":\"src/SymbolGraphMini/Greeter.cs\"}",
                "search_pattern" => "{\"pattern\":\"Greeter\"}",
                _ => "{}",
            };
            frameList.Add(
                "{\"jsonrpc\":\"2.0\",\"id\":" + id + ",\"method\":\"tools/call\",\"params\":{" +
                    "\"name\":\"" + toolName + "\",\"arguments\":" + args + "}}");
            id++;
        }

        var observedLines = await RunAndCollectStdoutAsync(fixture.RootPath, frameList.ToArray());

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

        var observedLines = await RunAndCollectStdoutAsync(fixture.RootPath, frames);

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
        var expectedToolNames = GetRegisteredToolNames();
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
    }

    private static async Task<McpWireDiscoverySnapshot> ReadDiscoverySnapshotAsync(
        string targetDirectory, bool modern)
    {
        var lines = await RunAndCollectStdoutAsync(targetDirectory, BuildDiscoveryFrames(modern));
        var discoveryResponse = FindResponse(lines, 1);
        var toolsResponse = FindResponse(lines, 2);
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

    private static string[] BuildDiscoveryFrames(bool modern)
    {
        var meta = new Dictionary<string, object?>
        {
            ["io.modelcontextprotocol/protocolVersion"] = ModernProtocolVersion,
            ["io.modelcontextprotocol/clientInfo"] = new { name = ClientName, version = ClientVersion },
            ["io.modelcontextprotocol/clientCapabilities"] = new { },
        };
        var discoveryFrame = modern
            ? JsonSerializer.Serialize(new
            {
                jsonrpc = "2.0",
                id = 1,
                method = "server/discover",
                @params = new { _meta = meta },
            })
            : JsonSerializer.Serialize(new
            {
                jsonrpc = "2.0",
                id = 1,
                method = "initialize",
                @params = new
                {
                    protocolVersion = ProtocolVersion,
                    capabilities = new { },
                    clientInfo = new { name = ClientName, version = ClientVersion },
                },
            });
        var toolsListFrame = modern
            ? JsonSerializer.Serialize(new
            {
                jsonrpc = "2.0",
                id = 2,
                method = "tools/list",
                @params = new { _meta = meta },
            })
            : JsonSerializer.Serialize(new { jsonrpc = "2.0", id = 2, method = "tools/list" });
        var initializedFrame = JsonSerializer.Serialize(new { jsonrpc = "2.0", method = "notifications/initialized" });
        return modern
            ? new[] { discoveryFrame, toolsListFrame }
            : new[] { discoveryFrame, initializedFrame, toolsListFrame };
    }

    private static IReadOnlySet<string> GetRegisteredToolNames()
    {
        using var state = new McpCodeGraphServer(
            McpCodeGraphServerOptions.From(new McpCodeGraphServerOptionsFromParameters(null)));
        return McpServerOptionsFactory.BuildToolCollection(state)
            .Select(tool => tool.ProtocolTool.Name)
            .ToHashSet(StringComparer.Ordinal);
    }

    private static JsonElement FindResponse(IEnumerable<string> lines, int id)
    {
        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            using var document = JsonDocument.Parse(line);
            if (document.RootElement.TryGetProperty("id", out var responseId) &&
                responseId.ValueKind == JsonValueKind.Number && responseId.GetInt32() == id)
            {
                return document.RootElement.Clone();
            }
        }

        throw new InvalidOperationException($"Keine JSON-RPC-Antwort fuer id={id} gefunden.");
    }

    private sealed record McpWireDiscoverySnapshot(
        string Instructions,
        IReadOnlySet<string> ToolNames,
        McpPayloadSize InstructionsSize,
        McpPayloadSize DiscoveryPayload,
        McpPayloadSize ToolsListPayload);

    private static async Task<System.Collections.Generic.List<string>> RunAndCollectStdoutAsync(
        string targetDirectory, string[] frames)
    {
        using var lease = await SubprocessLifetimeBudget.Shared.AcquireAsync(CancellationToken.None);
        var exePath = Path.Combine(AppContext.BaseDirectory, "AiNetLinter.exe");
        if (!File.Exists(exePath))
        {
            throw new FileNotFoundException(
                $"Erwartete AiNetLinter.exe nicht in BaseDirectory gefunden: {exePath}. " +
                "Test laeuft nur nach dotnet build.");
        }

        var psi = new ProcessStartInfo
        {
            FileName = exePath,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = targetDirectory,
        };
        psi.ArgumentList.Add("--mcp-server");
        psi.ArgumentList.Add("--path");
        psi.ArgumentList.Add(targetDirectory);

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException("AiNetLinter-Subprozess konnte nicht gestartet werden.");

        // stderr asynchron in einen Puffer mitlesen, damit der Subprozess sich nicht an einem
        // vollen stderr-Puffer aufhaengt. Wird im Test-Output nicht direkt geprueft, der
        // eigentliche Assert liegt auf stdout-Disziplin.
        var stderrTask = process.StandardError.ReadToEndAsync();
        var expectedResponses = CountExpectedResponses(frames);

        // Producer: schreibt die Frames auf stdin. Stdin bleibt offen, solange noch auf die
        // erwarteten Antworten gewartet wird, damit der Server unter Volllauf-Last nicht
        // vorzeitig durch ein vorzeitiges EOF abgebrochen wird.
        var writer = process.StandardInput;
        var writerTask = Task.Run(async () =>
        {
            foreach (var frame in frames)
            {
                await writer.WriteLineAsync(frame);
                await writer.FlushAsync();
            }
        });

        // stdout zeilenweise lesen, bis mindestens alle erwarteten Responses eingetroffen sind,
        // der Prozess beendet oder der Timeout greift.
        var observed = new System.Collections.Generic.List<string>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(90));
        try
        {
            await ReadStdoutFramesAsync(process.StandardOutput, observed, expectedResponses, cts.Token);
        }
        catch (OperationCanceledException)
        {
            // Server hat zu lange gebraucht; Frames bis hierhin lesen und Assertion laufen lassen.
        }

        // Producer-Task sauber beenden und stdin schliessen, damit der Server graceful beendet.
        try { await writerTask; } catch { /* Pipe-Close-Fehler ist hier OK */ }
        try { writer.Close(); } catch { /* Pipe evtl. schon zu */ }

        // Verbleibende Ausgaben bis zum Prozessende aufnehmen (z. B. nachlaufende stdout-Frames).
        try
        {
            await DrainRemainingStdoutAsync(process.StandardOutput, observed, cts.Token);
        }
        catch (OperationCanceledException)
        {
            // Timeout beim Drain
        }

        await EnsureProcessTerminatedAsync(process, stderrTask);

        return observed;
    }

    private static int CountExpectedResponses(string[] frames)
    {
        var count = 0;
        foreach (var frame in frames)
        {
            if (frame.Contains("\"id\":", StringComparison.Ordinal))
            {
                count++;
            }
        }
        return count;
    }

    private static async Task ReadStdoutFramesAsync(
        StreamReader stdout,
        System.Collections.Generic.List<string> observed,
        int expectedResponses,
        CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var line = await stdout.ReadLineAsync(ct);
            if (line is null) break;
            observed.Add(line);
            if (expectedResponses > 0 && observed.Count >= expectedResponses)
            {
                break;
            }
        }
    }

    private static async Task DrainRemainingStdoutAsync(
        StreamReader stdout,
        System.Collections.Generic.List<string> observed,
        CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var line = await stdout.ReadLineAsync(ct);
            if (line is null) break;
            observed.Add(line);
        }
    }

    /// <summary>
    /// Wartet auf graceful exit (kurzer Timeout), erzwingt danach einen Kill statt den Test
    /// unbegrenzt haengen zu lassen, und deckelt anschliessend das Warten auf <paramref
    /// name="stderrTask"/> (blockiert sonst bis stderr-EOF = Prozessende) mit einem eigenen
    /// Timeout. Ausgelagert aus <see cref="RunAndCollectStdoutAsync"/>, damit dessen eigene
    /// Komplexitaet unter <c>MaxCyclomaticComplexity</c>/<c>MaxCognitiveComplexity</c> bleibt —
    /// vorher fehlte dieser gesamte Force-Kill-Pfad, was einen nie beendeten Subprozess unter
    /// Last (konkurrierende Solution-Loads anderer parallel laufender Tests/Prozesse) den ganzen
    /// Testlauf unbegrenzt blockieren liess, empirisch beobachtet als Volllauf-Haenger
    /// 2026-08-11.
    /// </summary>
    private static async Task EnsureProcessTerminatedAsync(Process process, Task stderrTask)
    {
        if (!process.HasExited)
        {
            TryWaitOrKill(process);
        }

        // stderrTask kann nach einem erzwungenen Kill kurzzeitig offen bleiben (Pipe-Handle-
        // Teardown ist nicht synchron mit dem Kill) — eigener Timeout statt unbegrenztem await,
        // damit ein Edge-Case hier nie wieder den ganzen Testlauf blockieren kann.
        await Task.WhenAny(stderrTask, Task.Delay(TimeSpan.FromSeconds(10)));
    }

    private static void TryWaitOrKill(Process process)
    {
        try
        {
            if (!process.WaitForExit(TimeSpan.FromSeconds(10)) && !process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            // Best-effort-Cleanup.
        }
    }
}
