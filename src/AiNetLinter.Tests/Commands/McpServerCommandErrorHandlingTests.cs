#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Tests.Fixtures;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.Tests.Commands;

[Collection("ConsoleTestCollection")]
public sealed class McpServerCommandErrorHandlingTests
{
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

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await using var client = await McpClient.CreateAsync(transport, cancellationToken: cts.Token);
            var result = await client.CallToolAsync(
                "find_symbol",
                new Dictionary<string, object?> { ["namePattern"] = "Anything" },
                cancellationToken: cts.Token);

            Assert.True(result.IsError);
            var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
            Assert.Contains("SOLUTION_NOT_LOADED", textContent.Text);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Fact]
    public async Task RunAsync_ValidFixture_CompileErrorFileReturnsWarningSection()
    {
        // Valide Solution mit intentionalen Compile-Fehlern: get_file_skeleton auf eine kaputte
        using var fixture = new CompileErrorMiniFixtureWorkspace();
        var exePath = Path.Combine(AppContext.BaseDirectory, "AiNetLinter.exe");
        Assert.True(File.Exists(exePath), $"Erwartete AiNetLinter.exe nicht gefunden: {exePath}");

        var transport = new StdioClientTransport(new StdioClientTransportOptions
        {
            Name = "ainetlinter-mcp-compile-error-test-client",
            Command = exePath,
            Arguments = ["--mcp-server", "--path", fixture.RootPath],
        });

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await using var client = await McpClient.CreateAsync(transport, cancellationToken: cts.Token);
        var result = await client.CallToolAsync(
            "get_file_skeleton",
            new Dictionary<string, object?> { ["filePath"] = "src/CompileErrorMini/BrokenClassA.cs" },
            cancellationToken: cts.Token);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        // Datei-spezifischer Hinweis (NICHT Aggregate-Format).
        Assert.Contains("Diese Datei hat", textContent.Text, StringComparison.Ordinal);
        Assert.Contains("Compile-Fehler", textContent.Text, StringComparison.Ordinal);
    }

    private static string CreateTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"ainetlinter-mcp-error-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        return dir;
    }
}
