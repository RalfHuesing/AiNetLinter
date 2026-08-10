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

[Collection("SymbolGraphMcp")]
public sealed class McpServerCommandTests : IClassFixture<BaselineMcpFixture>
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
    [Trait("Category", "Unit")]
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
    [Trait("Category", "Unit")]
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
    [Trait("Category", "Unit")]
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
    [Trait("Category", "Unit")]
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
    [Trait("Category", "Unit")]
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
    [Trait("Category", "Integration")]
    public async Task RunAsync_ValidFixture_ServerRespondsWithSixteenTools()
    {
        var tools = await _baselineMcpFixture.Client.ListToolsAsync();
        Assert.Equal(16, tools.Count);
        string[] expectedNames =
        [
            "find_symbol", "find_references", "get_call_tree", "get_impact", "get_file_skeleton",
            "get_type_hierarchy", "get_index_scope", "get_hotspots", "get_violations", "safeguard",
            "search_pattern", "get_symbol_body", "reload_config", "get_server_health", "metrics_tree",
            "pattern_detect",
        ];
        foreach (var name in expectedNames) Assert.Contains(tools, t => t.Name == name);
    }

    [Fact]
    [Trait("Category", "Integration")]
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
    [Trait("Category", "Integration")]
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
    [Trait("Category", "Integration")]
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
    [Trait("Category", "Integration")]
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
    [Trait("Category", "Integration")]
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
    [Trait("Category", "Integration")]
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
    [Trait("Category", "Integration")]
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
    [Trait("Category", "Integration")]
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
    [Trait("Category", "Integration")]
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
    [Trait("Category", "Integration")]
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
    [Trait("Category", "Unit")]
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
    [Trait("Category", "Unit")]
    public void ResolveMaxLineCount_NoConfigPath_ReturnsMetricsConfigDefault()
    {
        var args = new LinterArgs { ConfigPath = null, TargetPath = "", Verbose = false };

        var result = McpServerCommand.ResolveMaxLineCount(args);

        Assert.Equal(new AiNetLinter.Configuration.MetricsConfig().MaxLineCount, result);
    }

    [Fact]
    [Trait("Category", "Unit")]
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
    [Trait("Category", "Unit")]
    public void ResolveConfig_NoConfigPath_ReturnsDefaultConfig()
    {
        var args = new LinterArgs { ConfigPath = null, TargetPath = "", Verbose = false };

        var result = McpServerCommand.ResolveConfig(args);

        Assert.NotNull(result);
        Assert.Equal(new AiNetLinter.Configuration.MetricsConfig().MaxLineCount, result.Metrics.MaxLineCount);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void ResolveConfig_ExplicitConfigPath_TakesPrecedenceOverAutoDiscovered()
    {
        var solutionDir = CreateTempDir();
        var explicitDir = CreateTempDir();
        try
        {
            var slnxPath = Path.Combine(solutionDir, "Only.slnx");
            File.WriteAllText(slnxPath, "");

            // Auto-discovered rules.json (next to the solution) with MaxLineCount: 7
            var autoDiscoveredConfigPath = Path.Combine(solutionDir, "rules.json");
            File.WriteAllText(autoDiscoveredConfigPath, """{ "Global": {}, "Metrics": { "MaxLineCount": 7 } }""");

            // Explicit rules.json in a separate dir with MaxLineCount: 5
            var explicitConfigPath = Path.Combine(explicitDir, "rules.json");
            File.WriteAllText(explicitConfigPath, """{ "Global": {}, "Metrics": { "MaxLineCount": 5 } }""");

            var args = new LinterArgs { ConfigPath = explicitConfigPath, TargetPath = slnxPath, Verbose = false };

            // TryResolveRulesJsonPath returns the explicit path, not the auto-discovered one
            var resolved = McpServerCommand.TryResolveRulesJsonPath(args.ConfigPath, slnxPath);
            Assert.Equal(explicitConfigPath, resolved);

            // ResolveConfig with the resolved path uses the explicit config (MaxLineCount: 5)
            var config = McpServerCommand.ResolveConfig(args, resolved);
            Assert.NotNull(config);
            Assert.Equal(5, config.Metrics.MaxLineCount);
        }
        finally
        {
            Directory.Delete(solutionDir, recursive: true);
            Directory.Delete(explicitDir, recursive: true);
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void ResolveConfig_NoExplicitConfigPath_AutoDiscoversRulesJsonInSolutionDirectory()
    {
        var tempDir = CreateTempDir();
        try
        {
            var slnxPath = Path.Combine(tempDir, "Only.slnx");
            File.WriteAllText(slnxPath, "");

            var rulesJsonPath = Path.Combine(tempDir, "rules.json");
            File.WriteAllText(rulesJsonPath, """{ "Global": {}, "Metrics": { "MaxLineCount": 11 } }""");

            var args = new LinterArgs { ConfigPath = null, TargetPath = tempDir, Verbose = false };

            // TryResolveRulesJsonPath auto-discovers the rules.json next to the solution
            var resolved = McpServerCommand.TryResolveRulesJsonPath(null, slnxPath);
            Assert.Equal(rulesJsonPath, resolved);

            // ResolveConfig with the resolved path uses the auto-discovered config
            var config = McpServerCommand.ResolveConfig(args, resolved);
            Assert.NotNull(config);
            Assert.Equal(11, config.Metrics.MaxLineCount);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ResolveConfig_NoExplicitConfigPath_NoRulesJsonFound_UsesDefault()
    {
        var tempDir = CreateTempDir();
        try
        {
            var slnxPath = Path.Combine(tempDir, "Only.slnx");
            File.WriteAllText(slnxPath, "");

            var args = new LinterArgs { ConfigPath = null, TargetPath = tempDir, Verbose = false };
            var console = new TestLintConsole();
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            // (a) TryResolveRulesJsonPath returns null when no rules.json is found
            var resolved = McpServerCommand.TryResolveRulesJsonPath(null, slnxPath);
            Assert.Null(resolved);

            // (b) ResolveConfig with null returns the default config
            var config = McpServerCommand.ResolveConfig(args, resolved);
            Assert.NotNull(config);
            Assert.Equal(new AiNetLinter.Configuration.MetricsConfig().MaxLineCount, config.Metrics.MaxLineCount);

            // (c) RunAsync emits [WARN] to stderr before the solution load runs.
            // Die [WARN]-Zeile wird synchron emittiert, bevor die Solution geladen wird.
            // Im Test-Environment (kein stdin) kann RunAsync entweder normal returnen oder
            // die OperationCanceledException durchreichen — beides ist akzeptabel;
            // entscheidend ist, dass die [WARN]-Zeile bereits in console.Errors gelandet ist.
            try
            {
                await McpServerCommand.RunAsync(args, cts.Token, console);
            }
            catch (OperationCanceledException)
            {
                // Akzeptabel: pre-cancelled Token hat sich durch RunAsync propagiert.
            }

            Assert.Contains(console.Errors, e =>
                e.Contains("[WARN]", StringComparison.Ordinal) &&
                e.Contains("Keine rules.json neben der Solution gefunden", StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    private static string CreateTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"ainetlinter-mcp-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        return dir;
    }
}
