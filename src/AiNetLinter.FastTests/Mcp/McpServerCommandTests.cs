#nullable enable

using System;
using System.IO;
using AiNetLinter.Cli;
using AiNetLinter.Commands;
using AiNetLinter.TestKit;
using Xunit;

namespace AiNetLinter.FastTests.Mcp;

[Trait("Category", "Unit")]
public sealed class McpServerCommandTests
{
    // Harter Cut: im MCP-Modus traegt jeder Aufruf seinen Projektbezug selbst (projectRoot +
    // Definitionsdatei); --path/--config sind harte Startfehler.

    [Fact]
    public void Validate_McpServerWithPath_IsHardError()
    {
        var args = new LinterArgs { McpServer = true, TargetPath = @"C:\repos\proj\App.slnx", Verbose = false };

        var error = args.Validate();

        Assert.NotNull(error);
        Assert.Contains("--path", error, StringComparison.Ordinal);
        Assert.Contains("nicht zulaessig", error, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_McpServerWithConfig_IsHardError()
    {
        var args = new LinterArgs { McpServer = true, ConfigPath = "rules.json", TargetPath = string.Empty, Verbose = false };

        var error = args.Validate();

        Assert.NotNull(error);
        Assert.Contains("--config", error, StringComparison.Ordinal);
        Assert.Contains("nicht zulaessig", error, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_McpServerWithoutProjectFlags_Passes()
    {
        var args = new LinterArgs { McpServer = true, TargetPath = string.Empty, Verbose = false };

        Assert.Null(args.Validate());
    }

    [Fact]
    public void Validate_McpServer_NonPositiveTtlMinutes_IsHardError()
    {
        var args = new LinterArgs { McpServer = true, McpProjectTtlMinutes = 0m, TargetPath = string.Empty, Verbose = false };

        var error = args.Validate();

        Assert.NotNull(error);
        Assert.Contains("--mcp-project-ttl-minutes", error, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_McpServer_FractionalTtlAndPositiveMaxProjects_Pass()
    {
        var args = new LinterArgs
        {
            McpServer = true,
            TargetPath = string.Empty,
            McpProjectTtlMinutes = 0.05m,
            McpMaxProjects = 4,
            Verbose = false,
        };

        Assert.Null(args.Validate());
    }

    [Fact]
    public void Validate_McpServer_NonPositiveMaxProjects_IsHardError()
    {
        var args = new LinterArgs { McpServer = true, McpMaxProjects = -1, TargetPath = string.Empty, Verbose = false };

        var error = args.Validate();

        Assert.NotNull(error);
        Assert.Contains("--mcp-max-projects", error, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_BatchWithoutPath_StillRequiresPath()
    {
        var args = new LinterArgs { TargetPath = string.Empty, Verbose = false };

        var error = args.Validate();

        Assert.NotNull(error);
        Assert.Contains("--path ist erforderlich", error, StringComparison.Ordinal);
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
    public void ResolveConfig_BatchResolutionStaysExplicitOverGivenResolvedPath()
    {
        using var explicitDir = TestTempDirectory.Create("ainetlinter-mcp-exp-");

        var explicitConfigPath = explicitDir.CreateFile("rules.json", """{ "Global": {}, "Metrics": { "MaxLineCount": 5 } }""");

        var args = new LinterArgs { ConfigPath = explicitConfigPath, TargetPath = "", Verbose = false };

        var config = McpServerCommand.ResolveConfig(args, explicitConfigPath);
        Assert.NotNull(config);
        Assert.Equal(5, config.Metrics.MaxLineCount);
    }
}
