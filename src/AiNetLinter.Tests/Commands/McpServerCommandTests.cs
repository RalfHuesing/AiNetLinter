#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Cli;
using AiNetLinter.Commands;
using AiNetLinter.Tests.Fixtures;
using AiNetLinter.Tests.Mcp;
using AiNetLinter.Tests.Output;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.Tests.Commands;

public sealed class McpServerCommandTests : IClassFixture<SymbolGraphMcpFixture>, IClassFixture<BaselineMcpFixture>
{
    private readonly SymbolGraphMcpFixture _symbolGraphMcpFixture;
    private readonly BaselineMcpFixture _baselineMcpFixture;

    public McpServerCommandTests(
        SymbolGraphMcpFixture symbolGraphMcpFixture,
        BaselineMcpFixture baselineMcpFixture)
    {
        _symbolGraphMcpFixture = symbolGraphMcpFixture;
        _baselineMcpFixture = baselineMcpFixture;
    }

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
    public async Task RunAsync_ValidFixture_ServerRespondsWithNineTools()
    {
        var tools = await _baselineMcpFixture.Client.ListToolsAsync();

        Assert.Equal(9, tools.Count);
        Assert.Contains(tools, t => t.Name == "find_symbol");
        Assert.Contains(tools, t => t.Name == "find_references");
        Assert.Contains(tools, t => t.Name == "get_impact");
        Assert.Contains(tools, t => t.Name == "get_file_skeleton");
        Assert.Contains(tools, t => t.Name == "get_type_hierarchy");
        Assert.Contains(tools, t => t.Name == "get_index_scope");
        Assert.Contains(tools, t => t.Name == "get_hotspots");
        Assert.Contains(tools, t => t.Name == "get_violations");
        Assert.Contains(tools, t => t.Name == "search_pattern");
    }

    [Fact]
    public async Task RunAsync_ValidFixture_GetHotspotsReturnsAllGreenForSmallFixture()
    {
        var result = await _symbolGraphMcpFixture.Client.CallToolAsync(
            "get_hotspots",
            new Dictionary<string, object?>());

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("im gruenen Bereich", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_ValidFixture_GetIndexScopeReturnsFileTypeBreakdown()
    {
        var result = await _symbolGraphMcpFixture.Client.CallToolAsync(
            "get_index_scope",
            new Dictionary<string, object?>());

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains(".cs:", textContent.Text, StringComparison.Ordinal);
        Assert.Contains(".xaml:", textContent.Text, StringComparison.Ordinal);
        Assert.Contains("voll vom Symbolgraph abgedeckt", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_ValidFixture_GetViolationsReturnsAtLeastOneViolation()
    {
        var result = await _symbolGraphMcpFixture.Client.CallToolAsync(
            "get_violations",
            new Dictionary<string, object?>());

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("ViolationTrigger", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_ValidFixture_SearchPatternReturnsExpectedHit()
    {
        var result = await _symbolGraphMcpFixture.Client.CallToolAsync(
            "search_pattern",
            new Dictionary<string, object?> { ["pattern"] = "Greeter" });

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("Greeter.cs", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_ValidFixture_FindSymbolReturnsMatch()
    {
        var result = await _baselineMcpFixture.Client.CallToolAsync(
            "find_symbol",
            new Dictionary<string, object?> { ["namePattern"] = "Violating" });

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("ViolatingClass", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_ValidFixture_FindReferencesReturnsCallSite()
    {
        var result = await _symbolGraphMcpFixture.Client.CallToolAsync(
            "find_references",
            new Dictionary<string, object?> { ["symbolIdentifier"] = "Greeter.Greet" });

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("Caller.cs", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_ValidFixture_GetImpactWithGitRefReturnsCallSite()
    {
        using var fixture = new GitImpactMiniFixtureWorkspace();
        fixture.CommitCalculatorAddBodyChange();

        await using var client = await McpTestClient.ConnectAsync(fixture.RootPath);
        var result = await client.CallToolAsync(
            "get_impact",
            new Dictionary<string, object?> { ["gitRef"] = "HEAD~1" });

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("CalculatorCaller.cs", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_ValidFixture_GetImpactWithoutGitRefUncommittedReturnsCallSite()
    {
        using var fixture = new GitImpactMiniFixtureWorkspace();
        fixture.ChangeCalculatorAddBodyWithoutCommitting();

        await using var client = await McpTestClient.ConnectAsync(fixture.RootPath);
        var result = await client.CallToolAsync(
            "get_impact",
            new Dictionary<string, object?>());

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("CalculatorCaller.cs", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_ValidFixture_GetFileSkeletonReturnsGreeterSignature()
    {
        var result = await _symbolGraphMcpFixture.Client.CallToolAsync(
            "get_file_skeleton",
            new Dictionary<string, object?> { ["filePath"] = "src/SymbolGraphMini/Greeter.cs" });

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("Greet", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_ValidFixture_GetTypeHierarchyReturnsBaseGreetingHierarchy()
    {
        var result = await _symbolGraphMcpFixture.Client.CallToolAsync(
            "get_type_hierarchy",
            new Dictionary<string, object?> { ["typeIdentifier"] = "BaseGreeting" });

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("IGreeting", textContent.Text, StringComparison.Ordinal);
        Assert.Contains("SpecialGreeting", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void ResolveMaxLineCount_ConfigWithCustomMaxLineCount_ReturnsConfiguredValue()
    {
        var tempDir = CreateTempDir();
        try
        {
            var configPath = Path.Combine(tempDir, "rules.json");
            File.WriteAllText(configPath, """{ "Global": {}, "Metrics": { "MaxLineCount": 5 } }""");
            var args = new LinterArgs { ConfigPath = configPath, TargetPath = tempDir, Verbose = false };

            var result = McpServerCommand.ResolveMaxLineCount(args);

            Assert.Equal(5, result);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void ResolveMaxLineCount_NoConfigPath_ReturnsMetricsConfigDefault()
    {
        var args = new LinterArgs { ConfigPath = null, TargetPath = "", Verbose = false };

        var result = McpServerCommand.ResolveMaxLineCount(args);

        Assert.Equal(new AiNetLinter.Configuration.MetricsConfig().MaxLineCount, result);
    }

    [Fact]
    public void ResolveConfig_ConfigWithCustomMaxLineCount_UsesConfigFromArgs()
    {
        var tempDir = CreateTempDir();
        try
        {
            var configPath = Path.Combine(tempDir, "rules.json");
            File.WriteAllText(configPath, """{ "Global": {}, "Metrics": { "MaxLineCount": 5 } }""");
            var args = new LinterArgs { ConfigPath = configPath, TargetPath = tempDir, Verbose = false };

            var result = McpServerCommand.ResolveConfig(args);

            Assert.NotNull(result);
            Assert.Equal(5, result.Metrics.MaxLineCount);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void ResolveConfig_NoConfigPath_ReturnsDefaultConfig()
    {
        var args = new LinterArgs { ConfigPath = null, TargetPath = "", Verbose = false };

        var result = McpServerCommand.ResolveConfig(args);

        Assert.NotNull(result);
        Assert.Equal(new AiNetLinter.Configuration.MetricsConfig().MaxLineCount, result.Metrics.MaxLineCount);
    }

    private static string CreateTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"ainetlinter-mcp-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        return dir;
    }
}
