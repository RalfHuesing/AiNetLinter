#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using AiNetLinter.Cli;
using AiNetLinter.Commands;
using AiNetLinter.TestKit;
using Xunit;

namespace AiNetLinter.FastTests.Mcp;

[Trait("Category", "Unit")]
public sealed class McpServerCommandTests
{
    [Fact]
    public void ResolveSolutionPathOrError_TwoSlnxFiles_ReportsAmbiguousSolution()
    {
        using var tempDir = TestTempDirectory.Create("ainetlinter-mcp-test-");
        tempDir.CreateFile("First.slnx", "");
        tempDir.CreateFile("Second.slnx", "");

        var console = new RecordingLintConsole();
        var result = McpServerCommand.ResolveSolutionPathOrError(tempDir.DirectoryPath, console);

        Assert.Null(result);
        var error = Assert.Single(console.ErrorLines);
        Assert.Contains("AMBIGUOUS_SOLUTION", error);
        Assert.Contains("First.slnx", error);
        Assert.Contains("Second.slnx", error);
    }

    [Fact]
    public void ResolveSolutionPathOrError_NoSolutionFound_ReportsResourceNotFound()
    {
        using var tempDir = TestTempDirectory.Create("ainetlinter-mcp-test-");
        var console = new RecordingLintConsole();
        var result = McpServerCommand.ResolveSolutionPathOrError(tempDir.DirectoryPath, console);

        Assert.Null(result);
        var error = Assert.Single(console.ErrorLines);
        Assert.Contains("RESOURCE_NOT_FOUND", error);
    }

    [Fact]
    public void ResolveSolutionPathOrError_SingleCandidate_ReturnsIt()
    {
        using var tempDir = TestTempDirectory.Create("ainetlinter-mcp-test-");
        var sln = tempDir.CreateFile("Only.slnx", "");

        var console = new RecordingLintConsole();
        var result = McpServerCommand.ResolveSolutionPathOrError(tempDir.DirectoryPath, console);

        Assert.Equal(sln, result);
        Assert.Empty(console.ErrorLines);
    }

    [Fact]
    public void ResolveSolutionPathOrError_MissingPath_UsesCurrentDirectory()
    {
        using var tempDir = TestTempDirectory.Create("ainetlinter-mcp-test-");
        var originalDir = Directory.GetCurrentDirectory();
        try
        {
            var sln = tempDir.CreateFile("Only.slnx", "");
            Directory.SetCurrentDirectory(tempDir.DirectoryPath);

            var console = new RecordingLintConsole();
            var result = McpServerCommand.ResolveSolutionPathOrError("", console);

            Assert.Equal(sln, result);
            Assert.Empty(console.ErrorLines);
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDir);
        }
    }

    [Fact]
    public void ResolveMaxLineCount_ConfigWithCustomMaxLineCount_ReturnsConfiguredValue()
    {
        using var tempDir = TestTempDirectory.Create("ainetlinter-mcp-test-");
        var configPath = tempDir.CreateFile("rules.json", """{ "Global": {}, "Metrics": { "MaxLineCount": 5 } }""");
        var args = new LinterArgs { ConfigPath = configPath, TargetPath = tempDir.DirectoryPath, Verbose = false };

        var result = McpServerCommand.ResolveMaxLineCount(args);

        Assert.Equal(5, result);
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
        using var tempDir = TestTempDirectory.Create("ainetlinter-mcp-test-");
        var configPath = tempDir.CreateFile("rules.json", """{ "Global": {}, "Metrics": { "MaxLineCount": 5 } }""");
        var args = new LinterArgs { ConfigPath = configPath, TargetPath = tempDir.DirectoryPath, Verbose = false };

        var result = McpServerCommand.ResolveConfig(args);

        Assert.NotNull(result);
        Assert.Equal(5, result.Metrics.MaxLineCount);
    }

    [Fact]
    public void ResolveConfig_NoConfigPath_ReturnsDefaultConfig()
    {
        var args = new LinterArgs { ConfigPath = null, TargetPath = "", Verbose = false };

        var result = McpServerCommand.ResolveConfig(args);

        Assert.NotNull(result);
        Assert.Equal(new AiNetLinter.Configuration.MetricsConfig().MaxLineCount, result.Metrics.MaxLineCount);
    }

    [Fact]
    public void ResolveConfig_ExplicitConfigPath_TakesPrecedenceOverAutoDiscovered()
    {
        using var solutionDir = TestTempDirectory.Create("ainetlinter-mcp-sol-");
        using var explicitDir = TestTempDirectory.Create("ainetlinter-mcp-exp-");

        var slnxPath = solutionDir.CreateFile("Only.slnx", "");

        // Auto-discovered rules.json (next to the solution) with MaxLineCount: 7
        solutionDir.CreateFile("rules.json", """{ "Global": {}, "Metrics": { "MaxLineCount": 7 } }""");

        // Explicit rules.json in a separate dir with MaxLineCount: 5
        var explicitConfigPath = explicitDir.CreateFile("rules.json", """{ "Global": {}, "Metrics": { "MaxLineCount": 5 } }""");

        var args = new LinterArgs { ConfigPath = explicitConfigPath, TargetPath = slnxPath, Verbose = false };

        // TryResolveRulesJsonPath returns the explicit path, not the auto-discovered one
        var resolved = McpServerCommand.TryResolveRulesJsonPath(args.ConfigPath, slnxPath);
        Assert.Equal(explicitConfigPath, resolved);

        // ResolveConfig with the resolved path uses the explicit config (MaxLineCount: 5)
        var config = McpServerCommand.ResolveConfig(args, resolved);
        Assert.NotNull(config);
        Assert.Equal(5, config.Metrics.MaxLineCount);
    }

    [Fact]
    public void ResolveConfig_NoExplicitConfigPath_AutoDiscoversRulesJsonInSolutionDirectory()
    {
        using var tempDir = TestTempDirectory.Create("ainetlinter-mcp-test-");
        var slnxPath = tempDir.CreateFile("Only.slnx", "");
        var rulesJsonPath = tempDir.CreateFile("rules.json", """{ "Global": {}, "Metrics": { "MaxLineCount": 11 } }""");

        var args = new LinterArgs { ConfigPath = null, TargetPath = tempDir.DirectoryPath, Verbose = false };

        // TryResolveRulesJsonPath auto-discovers the rules.json next to the solution
        var resolved = McpServerCommand.TryResolveRulesJsonPath(null, slnxPath);
        Assert.Equal(rulesJsonPath, resolved);

        // ResolveConfig with the resolved path uses the auto-discovered config
        var config = McpServerCommand.ResolveConfig(args, resolved);
        Assert.NotNull(config);
        Assert.Equal(11, config.Metrics.MaxLineCount);
    }
}
