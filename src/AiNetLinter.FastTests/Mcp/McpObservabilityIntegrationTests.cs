#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp;
using AiNetLinter.TestKit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using RalfHuesing.Mcp.Observability;
using Xunit;

namespace AiNetLinter.FastTests.Mcp;

/// <summary>
/// Unit- und Integrationstests fuer die Anbindung von <see cref="RalfHuesing.Mcp.Observability"/>:
/// verifiziert Tool-Registrierung von <c>report_observability_feedback</c>, automatisches
/// Tool-Call-Logging aller Aufrufe und Speicherung im konfigurierten Log-Verzeichnis.
/// </summary>
[Trait("Category", "Unit")]
public sealed class McpObservabilityIntegrationTests
{
    [Fact]
    public void McpServerOptionsFactory_RegistersAllExpectedTools()
    {
        var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(new McpCodeGraphServerOptionsFromParameters(null)));
        var options = McpServerOptionsFactory.Create(state);

        Assert.NotNull(options.ToolCollection);
        Assert.True(options.ToolCollection.Count >= 20);
        Assert.Contains(options.ToolCollection, t => t.ProtocolTool.Name == "find_symbol");
        Assert.Contains(options.ToolCollection, t => t.ProtocolTool.Name == "get_violations");
        Assert.Contains(options.ToolCollection, t => t.ProtocolTool.Name == "get_server_health");
        Assert.Contains(options.ToolCollection, t => t.ProtocolTool.Name == "report_observability_feedback");
    }

    [Fact]
    public void McpObservabilityOptions_DefaultValues_AreSensible()
    {
        var options = new McpObservabilityOptions();

        Assert.True(options.Enabled);
        Assert.True(options.EnableToolCallLogging);
        Assert.True(options.EnableFeedbackTool);
        Assert.Null(options.LogDirectory);
    }

    [Fact]
    public async Task EndToEnd_ToolCallAndFeedback_WritesJsonlLogs()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "mcp-obs-e2e-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var clientToServer = new System.IO.Pipelines.Pipe();
            var serverToClient = new System.IO.Pipelines.Pipe();

            var clientTransport = new StreamClientTransport(
                clientToServer.Writer.AsStream(),
                serverToClient.Reader.AsStream());

            var serverTransport = new StreamServerTransport(
                clientToServer.Reader.AsStream(),
                serverToClient.Writer.AsStream());

            var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(new McpCodeGraphServerOptionsFromParameters(null)));

            var services = new ServiceCollection();
            var builder = services.AddMcpServer();

            var obsOptions = new McpObservabilityOptions
            {
                Enabled = true,
                EnableToolCallLogging = true,
                EnableFeedbackTool = true,
                LogDirectory = tempDir,
            };
            builder.WithObservability(obsOptions);

            var sp = services.BuildServiceProvider();
            var serverOptions = sp.GetRequiredService<IOptions<McpServerOptions>>().Value;
            serverOptions.ServerInfo = new Implementation
            {
                Name = "ainetlinter",
                Version = "1.0.95",
            };
            serverOptions.ToolCollection = McpServerOptionsFactory.BuildToolCollection(state, sp);

            await using var server = McpServer.Create(serverTransport, serverOptions, serviceProvider: sp);
            using var cts = new CancellationTokenSource();
            var serverTask = server.RunAsync(cts.Token);

            await using var client = await McpClient.CreateAsync(clientTransport);

            // 1. Tool auflisten
            var tools = await client.ListToolsAsync(cancellationToken: cts.Token);
            Assert.Contains(tools, t => t.Name == "report_observability_feedback");
            Assert.Contains(tools, t => t.Name == "get_index_scope");

            // 2. Reguläres Tool aufrufen (wird als tool_call geloggt)
            var indexResult = await client.CallToolAsync("get_index_scope", new Dictionary<string, object?>(), cancellationToken: cts.Token);
            Assert.NotNull(indexResult);

            // 3. Feedback-Tool aufrufen (wird als tool_call und als feedback geloggt)
            var feedbackResult = await client.CallToolAsync("report_observability_feedback", new Dictionary<string, object?>
            {
                ["feedbackType"] = "issue",
                ["title"] = "Falsch-Positiv bei Nullable-Check",
                ["description"] = "Die Regel meldet einen Verstoß, obwohl der Typ nicht nullable ist.",
                ["relatedTool"] = "get_violations",
                ["severity"] = "medium"
            }, cancellationToken: cts.Token);

            Assert.NotNull(feedbackResult);
            await client.DisposeAsync();
            await cts.CancelAsync();
            try { await serverTask; } catch (OperationCanceledException) { }
            await server.DisposeAsync();
            if (sp is IAsyncDisposable asyncSp) await asyncSp.DisposeAsync();
            else sp.Dispose();

            // 4. Log-Datei validieren
            var logFiles = Directory.GetFiles(tempDir, "*.jsonl", SearchOption.AllDirectories);
            Assert.Single(logFiles);

            string[] lines;
            using (var stream = new FileStream(logFiles[0], FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var reader = new StreamReader(stream))
            {
                var content = await reader.ReadToEndAsync();
                lines = content.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
            }
            Assert.True(lines.Length >= 2);

            var hasToolCall = false;
            var hasFeedback = false;

            foreach (var line in lines)
            {
                using var doc = JsonDocument.Parse(line);
                var root = doc.RootElement;
                var recordType = root.GetProperty("recordType").GetString();
                if (recordType == "tool_call")
                {
                    hasToolCall = true;
                    Assert.Equal("ainetlinter", root.GetProperty("serverName").GetString());
                }
                else if (recordType == "feedback")
                {
                    hasFeedback = true;
                    Assert.Equal("issue", root.GetProperty("feedbackType").GetString());
                    Assert.Equal("Falsch-Positiv bei Nullable-Check", root.GetProperty("title").GetString());
                    Assert.Equal("get_violations", root.GetProperty("relatedTool").GetString());
                }
            }

            Assert.True(hasToolCall, "Erwartet mindestens einen tool_call Record");
            Assert.True(hasFeedback, "Erwartet einen feedback Record");
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }
    }
}
