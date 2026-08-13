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
    [Trait("Category", "Unit")]
    public void ResolveSolutionPathOrError_TwoSlnxFiles_ReportsAmbiguousSolution()
    {
        var tempDir = CreateTempDir();
        try
        {
            File.WriteAllText(Path.Combine(tempDir, "First.slnx"), "");
            File.WriteAllText(Path.Combine(tempDir, "Second.slnx"), "");

            var console = new RecordingLintConsole();
            var result = McpServerCommand.ResolveSolutionPathOrError(tempDir, console);

            Assert.Null(result);
            var error = Assert.Single(console.ErrorLines);
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
            var console = new RecordingLintConsole();
            var result = McpServerCommand.ResolveSolutionPathOrError(tempDir, console);

            Assert.Null(result);
            var error = Assert.Single(console.ErrorLines);
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

            var console = new RecordingLintConsole();
            var result = McpServerCommand.ResolveSolutionPathOrError(tempDir, console);

            Assert.Equal(sln, result);
            Assert.Empty(console.ErrorLines);
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

            var console = new RecordingLintConsole();
            var result = McpServerCommand.ResolveSolutionPathOrError("", console);

            Assert.Equal(sln, result);
            Assert.Empty(console.ErrorLines);
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDir);
            Directory.Delete(tempDir, recursive: true);
        }
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

    private static string CreateTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"ainetlinter-mcp-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        return dir;
    }
}
