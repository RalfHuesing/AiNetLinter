#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Commands;
using AiNetLinter.Tests.Fixtures;
using AiNetLinter.Tests.Output;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.Tests.Commands;

[Collection("ConsoleTestCollection")]
public sealed class McpServerCommandTests
{
    [Fact]
    public void ResolveSolutionPathOrError_TwoSlnxFiles_ReportsAmbiguousSolution()
    {
        var tempDir = CreateTempDir();
        try
        {
            File.WriteAllText(Path.Combine(tempDir, "First.slnx"), "");
            File.WriteAllText(Path.Combine(tempDir, "Second.slnx"), "");

            var console = new TestLintConsole();
            var result = McpServerCommand.ResolveSolutionPathOrError(tempDir, console);

            Assert.Null(result);
            var error = Assert.Single(console.Errors);
            Assert.Contains("AMBIGUOUS_SOLUTION", error);
            Assert.Contains("First.slnx", error);
            Assert.Contains("Second.slnx", error);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void ResolveSolutionPathOrError_NoSolutionFound_ReportsResourceNotFound()
    {
        var tempDir = CreateTempDir();
        try
        {
            var console = new TestLintConsole();
            var result = McpServerCommand.ResolveSolutionPathOrError(tempDir, console);

            Assert.Null(result);
            var error = Assert.Single(console.Errors);
            Assert.Contains("RESOURCE_NOT_FOUND", error);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void ResolveSolutionPathOrError_SingleCandidate_ReturnsIt()
    {
        var tempDir = CreateTempDir();
        try
        {
            var sln = Path.Combine(tempDir, "Only.slnx");
            File.WriteAllText(sln, "");

            var console = new TestLintConsole();
            var result = McpServerCommand.ResolveSolutionPathOrError(tempDir, console);

            Assert.Equal(sln, result);
            Assert.Empty(console.Errors);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void ResolveSolutionPathOrError_MissingPath_UsesCurrentDirectory()
    {
        var tempDir = CreateTempDir();
        var originalDir = Directory.GetCurrentDirectory();
        try
        {
            var sln = Path.Combine(tempDir, "Only.slnx");
            File.WriteAllText(sln, "");
            Directory.SetCurrentDirectory(tempDir);

            var console = new TestLintConsole();
            var result = McpServerCommand.ResolveSolutionPathOrError("", console);

            Assert.Equal(sln, result);
            Assert.Empty(console.Errors);
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDir);
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task TryLoadSolutionAsync_BrokenSlnx_LogsWarningWithoutThrowing()
    {
        var tempDir = CreateTempDir();
        try
        {
            var brokenSln = Path.Combine(tempDir, "Broken.slnx");
            File.WriteAllText(brokenSln, "<this-is-not-a-valid-slnx-document>");

            var console = new TestLintConsole();
            AiNetLinter.Baseline.SourceFileCatalog? catalog = null;
            var exception = await Record.ExceptionAsync(
                async () => catalog = await McpServerCommand.TryLoadSolutionAsync(brokenSln, CancellationToken.None, console));

            Assert.Null(exception);
            Assert.Null(catalog);
            Assert.Contains(console.Errors, e => e.Contains("[WARN]", StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task RunAsync_ValidFixture_ServerRespondsWithBothTools()
    {
        using var fixture = new BaselineMiniFixtureWorkspace();
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
        var tools = await client.ListToolsAsync(cancellationToken: cts.Token);

        Assert.Equal(2, tools.Count);
        Assert.Contains(tools, t => t.Name == "find_symbol");
        Assert.Contains(tools, t => t.Name == "find_references");
    }

    [Fact]
    public async Task RunAsync_ValidFixture_FindSymbolReturnsMatch()
    {
        using var fixture = new BaselineMiniFixtureWorkspace();
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
            "find_symbol",
            new Dictionary<string, object?> { ["namePattern"] = "Violating" },
            cancellationToken: cts.Token);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("ViolatingClass", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_ValidFixture_FindReferencesReturnsCallSite()
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
            new Dictionary<string, object?> { ["symbolIdentifier"] = "Greeter.Greet" },
            cancellationToken: cts.Token);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("Caller.cs", textContent.Text, StringComparison.Ordinal);
    }

    private static string CreateTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"ainetlinter-mcp-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        return dir;
    }
}
