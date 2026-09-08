#nullable enable

using System;
using System.IO;
using System.Threading.Tasks;
using AiNetLinter.Baseline;
using AiNetLinter.Cli;
using AiNetLinter.Commands;
using AiNetLinter.Mcp.Projects;
using AiNetLinter.Output;
using AiNetLinter.TestKit;
using Xunit;

namespace AiNetLinter.FastTests.Mcp;

[Trait("Category", "Unit")]
public sealed class McpServerCommandTests
{
    // Harter Cut: im MCP-Modus traegt jeder zielgebundene Aufruf seinen absoluten targetPath
    // selbst; --path/--config sind harte Startfehler.

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
        var args = new LinterArgs { McpServer = true, ConfigPath = "ainetlinter-rules.json", TargetPath = string.Empty, Verbose = false };

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
    public async Task CreateResidentInstance_MissingNeighborRules_PropagatesDefaultConfigStatus()
    {
        using var tempDir = TestTempDirectory.Create("mcp-server-factory-default-status-");
        var solutionPath = tempDir.CreateFile("app.slnx", string.Empty);
        var definition = new ProjectDefinition(solutionPath, string.Empty);

        var creation = McpServerCommand.CreateResidentInstance(
            definition,
            LinterConsole.Instance,
            static _ => Task.FromResult<SourceFileCatalog?>(null));
        using var server = Assert.IsType<AiNetLinter.Mcp.McpCodeGraphServer>(creation.Server);
        await server.LoadTask!.WaitAsync(TimeSpan.FromSeconds(30));

        Assert.True(server.UsedDefaultConfig);
        Assert.Null(server.ResolvedConfigPath);
    }

    [Fact]
    public async Task CreateResidentInstance_ConfiguredNeighborRules_PropagatesResolvedConfigStatus()
    {
        using var tempDir = TestTempDirectory.Create("mcp-server-factory-config-status-");
        var solutionPath = tempDir.CreateFile("app.slnx", string.Empty);
        var rulesPath = tempDir.CreateFile(
            "ainetlinter-rules.json",
            "{ \"Global\": {}, \"Metrics\": { \"MaxLineCount\": 42 } }");
        var definition = new ProjectDefinition(solutionPath, rulesPath);

        var creation = McpServerCommand.CreateResidentInstance(
            definition,
            LinterConsole.Instance,
            static _ => Task.FromResult<SourceFileCatalog?>(null));
        using var server = Assert.IsType<AiNetLinter.Mcp.McpCodeGraphServer>(creation.Server);
        await server.LoadTask!.WaitAsync(TimeSpan.FromSeconds(30));

        Assert.False(server.UsedDefaultConfig);
        Assert.Equal(rulesPath, server.ResolvedConfigPath);
    }

}
