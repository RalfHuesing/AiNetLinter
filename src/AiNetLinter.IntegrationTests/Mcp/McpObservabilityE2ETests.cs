#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.IntegrationTests.Fixtures;
using AiNetLinter.IntegrationTests.Mcp.Platform;
using ModelContextProtocol.Client;
using Xunit;

namespace AiNetLinter.IntegrationTests.Mcp;

/// <summary>
/// E2E-Tests fuer MCP-Server Observability und Agent-Feedback: startet <c>AiNetLinter.exe --mcp-server</c>
/// mit `--mcp-log <pfad>` als Subprozess, fuehrt Tool-Calls und Feedback-Reports aus und prueft die
/// erzeugte JSONL-Datei.
/// </summary>
[Trait("Category", "Integration")]
public sealed class McpObservabilityE2ETests
{
    [Fact]
    public async Task Server_WithMcpLogFlag_LogsToolCallsAndFeedbackToJsonl()
    {
        var exePath = Path.Combine(AppContext.BaseDirectory, "AiNetLinter.exe");
        Assert.True(File.Exists(exePath), $"AiNetLinter.exe nicht gefunden: {exePath}");

        using var fixture = new BaselineMiniFixtureWorkspace();
        var fixtureRoot = fixture.RootPath;
        McpFixtureProjectDefinition.Ensure(fixtureRoot);
        using var tempDir = TestTempDirectory.Create("mcp-obs-process-");
        var logDir = tempDir.DirectoryPath;

        var transport = new StdioClientTransport(new StdioClientTransportOptions
        {
            Name = "ainetlinter-observability-e2e-test",
            Command = exePath,
            Arguments = ["--mcp-server", "--mcp-log", logDir],
            WorkingDirectory = fixtureRoot,
            EnvironmentVariables = new Dictionary<string, string?>
            {
                ["AINETLINTER_NO_DAEMON"] = "1",
            },
        });

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await using var client = await McpClient.CreateAsync(transport, cancellationToken: cts.Token);

        // 1. tools/list pruefen
        var tools = await client.ListToolsAsync(cancellationToken: cts.Token);
        Assert.Contains(tools, t => t.Name == "report_observability_feedback");
        Assert.Contains(tools, t => t.Name == "find_symbol");

        // 2. Regulären Tool-Call ausführen
        var result = await client.CallToolAsync(
            "get_index_scope",
            new Dictionary<string, object?> { ["projectRoot"] = fixtureRoot },
            cancellationToken: cts.Token);
        Assert.NotNull(result);

        // 3. Feedback-Tool aufrufen
        var feedbackResult = await client.CallToolAsync(
            "report_observability_feedback",
            new Dictionary<string, object?>
            {
                ["feedbackType"] = "feature_request",
                ["title"] = "Support für zusätzliche Sprachmuster",
                ["description"] = "Erkennung von weiteren C# 14 Konstrukten gewünscht.",
                ["relatedTool"] = "pattern_detect",
                ["severity"] = "low",
                ["projectRoot"] = fixtureRoot,
            },
            cancellationToken: cts.Token);

        Assert.NotNull(feedbackResult);
        Assert.False(feedbackResult.IsError == true);

        // 4. Client sauber schliessen, damit der Serverprozess flushed und beendet
        await client.DisposeAsync();

        var logFiles = Directory.GetFiles(logDir, "*.jsonl", SearchOption.AllDirectories);
        Assert.NotEmpty(logFiles);

        var logLines = logFiles.SelectMany(f =>
        {
            using var stream = new FileStream(f, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(stream);
            var lines = new List<string>();
            while (reader.ReadLine() is { } line)
            {
                lines.Add(line);
            }
            return lines;
        }).ToArray();
        Assert.True(logLines.Length >= 2, "Erwartet mindestens 2 JSONL Zeilen");

        var foundToolCall = false;
        var foundFeedback = false;

        foreach (var line in logLines)
        {
            using var doc = JsonDocument.Parse(line);
            var root = doc.RootElement;
            var recordType = root.GetProperty("recordType").GetString();
            if (recordType == "tool_call")
            {
                foundToolCall = true;
                Assert.Equal("ainetlinter", root.GetProperty("serverName").GetString());
                Assert.True(root.TryGetProperty("response", out var responseProp));
                Assert.False(string.IsNullOrEmpty(responseProp.GetString()));
                Assert.True(root.GetProperty("responseLength").GetInt32() > 0);
                Assert.True(root.GetProperty("responseLines").GetInt32() >= 1);
                Assert.False(root.GetProperty("responseTruncated").GetBoolean());
                Assert.Equal(0, root.GetProperty("nonTextContentBlocks").GetInt32());
            }
            else if (recordType == "feedback")
            {
                foundFeedback = true;
                Assert.Equal("feature_request", root.GetProperty("feedbackType").GetString());
                Assert.Equal("Support für zusätzliche Sprachmuster", root.GetProperty("title").GetString());
                Assert.Equal("pattern_detect", root.GetProperty("relatedTool").GetString());
            }
        }

        Assert.True(foundToolCall, "tool_call Record muss in JSONL vorhanden sein");
        Assert.True(foundFeedback, "feedback Record muss in JSONL vorhanden sein");
    }
}
