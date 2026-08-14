#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.IntegrationTests.Fixtures;
using AiNetLinter.IntegrationTests.Mcp.Platform;
using AiNetLinter.IntegrationTests.Platform;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.IntegrationTests.Mcp;

/// <summary>
/// E2E-Tests fuer den
/// Server-Crash, sondern jeder Tool-Call liefert eine strukturierte [ERROR]-Antwort; eine
/// valide Solution mit Compile-Fehlern in einzelnen Dateien liefert fuer die betroffene
/// Datei den
/// 006-Erweiterung.
/// </summary>
[Trait("Category", "Integration")]
public sealed class McpServerCommandErrorHandlingTests
{
    private const string LoadingMessagePrefix = "[INFO]: Server laedt die Solution noch.";

    [Fact]
    public async Task RunAsync_BrokenSlnx_ToolCallReturnsSolutionNotLoadedError()
    {
        // Bewusst kaputte .slnx analog McpServerCommandTests.TryLoadSolutionAsync_BrokenSlnx_...
        // Der Server muss starten (kein Crash beim Load-Fehler), jeder Tool-Call liefert dann
        // [ERROR]: SOLUTION_NOT_LOADED statt einer unbehandelten Exception.
        var tempDir = CreateTempDir();
        try
        {
            var brokenSln = Path.Combine(tempDir, "Broken.slnx");
            File.WriteAllText(brokenSln, "<this-is-not-a-valid-slnx-document>");

            var exePath = Path.Combine(AppContext.BaseDirectory, "AiNetLinter.exe");
            Assert.True(File.Exists(exePath), $"Erwartete AiNetLinter.exe nicht gefunden: {exePath}");

            var transport = new StdioClientTransport(new StdioClientTransportOptions
            {
                Name = "ainetlinter-mcp-broken-test-client",
                Command = exePath,
                Arguments = ["--mcp-server", "--path", brokenSln],
            });

            // 60s statt 30s: das Budget deckt Gate-Wartezeit + echten Subprozess-Start +
            // MCP-Handshake + bis zu 30 Tool-Call-Retries a 500ms ab — unter Volllauf-Last
            // (viele parallele Threads/Subprozesse gleichzeitig) reichten 30s nicht immer,
            // beobachtet als TaskCanceledException in SendRequestAsync.
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            using var lease = await SubprocessLifetimeBudget.Shared.AcquireAsync(cts.Token);
            await using var client = await McpClient.CreateAsync(transport, cancellationToken: cts.Token);
            var result = await CallToolWithLoadingRetryAsync(
                client,
                "find_symbol",
                new Dictionary<string, object?> { ["namePattern"] = "Anything" },
                cts.Token);

            Assert.True(result.IsError);
            var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
            Assert.Contains("[ERROR]: SOLUTION_NOT_LOADED", textContent.Text, StringComparison.Ordinal);
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { /* ignore */ }
        }
    }

    [Fact]
    public async Task RunAsync_CompileErrorMini_GetFileSkeleton_ReturnsFileSpecificCompileErrorHint()
    {
        // 006-Erweiterung: GetFileSkeleton auf einer datei-spezifisch fehlerhaften
        // Datei muss den
        // unstrukturierter Output.
        using var fixture = new CompileErrorMiniFixtureWorkspace();
        var exePath = Path.Combine(AppContext.BaseDirectory, "AiNetLinter.exe");
        Assert.True(File.Exists(exePath), $"Erwartete AiNetLinter.exe nicht gefunden: {exePath}");

        var transport = new StdioClientTransport(new StdioClientTransportOptions
        {
            Name = "ainetlinter-mcp-compile-error-test-client",
            Command = exePath,
            Arguments = ["--mcp-server", "--path", fixture.RootPath],
        });

        // 60s statt 30s, siehe Begruendung in RunAsync_BrokenSlnx_ToolCallReturnsSolutionNotLoadedError.
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        using var lease = await SubprocessLifetimeBudget.Shared.AcquireAsync(cts.Token);
        await using var client = await McpClient.CreateAsync(transport, cancellationToken: cts.Token);
        var result = await CallToolWithLoadingRetryAsync(
            client,
            "get_file_skeleton",
            new Dictionary<string, object?> { ["filePath"] = "src/CompileErrorMini/BrokenClassA.cs" },
            cts.Token);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        // Datei-spezifischer Hinweis (NICHT Aggregate-Format).
        Assert.Contains("Diese Datei hat", textContent.Text, StringComparison.Ordinal);
        Assert.Contains("Compile-Fehler", textContent.Text, StringComparison.Ordinal);
    }

    /// <summary>
    /// Ruft ein MCP-Tool auf und retryt, solange die Antwort den Loading-Info-Text enthaelt
    /// (Hintergrund-Load des Servers ist noch nicht abgeschlossen).
    /// </summary>
    private static async Task<CallToolResult> CallToolWithLoadingRetryAsync(
        McpClient client,
        string toolName,
        IReadOnlyDictionary<string, object?>? arguments,
        CancellationToken ct)
    {
        for (var attempt = 0; attempt < 30; attempt++)
        {
            var result = await client.CallToolAsync(toolName, arguments, cancellationToken: ct);
            if (result.Content?.Count > 0
                && result.Content[0] is TextContentBlock text
                && (text.Text?.StartsWith(LoadingMessagePrefix, StringComparison.Ordinal) != true))
            {
                return result;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(500), ct);
        }

        return await client.CallToolAsync(toolName, arguments, cancellationToken: ct);
    }

    private static string CreateTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"ainetlinter-mcp-error-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        return dir;
    }
}
