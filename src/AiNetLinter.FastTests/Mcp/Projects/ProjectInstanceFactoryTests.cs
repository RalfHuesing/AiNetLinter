#nullable enable

using System.IO;
using AiNetLinter.Cli;
using AiNetLinter.Commands;
using AiNetLinter.Configuration;
using AiNetLinter.Mcp.Projects;
using AiNetLinter.TestKit;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Projects;

[Trait("Category", "Unit")]
public sealed class ProjectInstanceFactoryTests
{
    [Fact]
    public void Create_FromDefinition_LoadsConfigFromDefinitionRulesPath()
    {
        using var tempDir = TestTempDirectory.Create("project-factory-");
        tempDir.CreateFile("proj/app.slnx", "");
        tempDir.CreateFile(
            "proj/config/rules.json",
            """{ "Global": {}, "Metrics": { "MaxLineCount": 42 } }""");
        WriteDefinition(tempDir);
        var definition = LoadDefinition(tempDir);

        var options = ProjectInstanceFactory.Create(definition);

        Assert.Equal(definition.RulesPath, options.ResolvedConfigPath);
        Assert.False(options.UsedDefaultConfig);
        Assert.Equal(42, options.MaxLineCount);
        Assert.Equal(42, options.Config.Metrics.MaxLineCount);
    }

    [Fact]
    public void Create_MaxLineCount_MatchesLegacyBatchPipeline()
    {
        using var tempDir = TestTempDirectory.Create("project-factory-legacy-");
        tempDir.CreateFile("proj/app.slnx", "");
        var rulesPath = tempDir.CreateFile(
            "proj/config/rules.json",
            """{ "Global": {}, "Metrics": { "MaxLineCount": 7 } }""");
        WriteDefinition(tempDir);

        var options = ProjectInstanceFactory.Create(LoadDefinition(tempDir));
        var legacy = McpServerCommand.ResolveMaxLineCount(
            new LinterArgs { TargetPath = tempDir.DirectoryPath, Verbose = false },
            rulesPath);

        Assert.Equal(legacy, options.MaxLineCount);
    }

    [Fact]
    public void MaterializeRules_MissingPath_ReturnsMetricsDefaults()
    {
        var result = ProjectInstanceFactory.MaterializeRules(rulesPath: null, isRequired: false);

        Assert.Equal(new MetricsConfig().MaxLineCount, result.MaxLineCount);
        Assert.Equal(new MetricsConfig().MaxLineCount, result.Config.Metrics.MaxLineCount);
    }

    private static ProjectDefinition LoadDefinition(TestTempDirectory tempDir)
    {
        var result = ProjectDefinitionLoader.Load(Path.Combine(tempDir.DirectoryPath, "proj"));
        Assert.True(result.Succeeded, result.Message);
        return result.Definition!;
    }

    private static void WriteDefinition(TestTempDirectory tempDir) =>
        tempDir.CreateFile(
            Path.Combine("proj", "ainetlinter.project.json"),
            """{ "solution": "app.slnx", "rules": "config/rules.json" }""");
}
