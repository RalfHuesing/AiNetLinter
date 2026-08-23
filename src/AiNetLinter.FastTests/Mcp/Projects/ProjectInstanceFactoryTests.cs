#nullable enable

using System;
using System.IO;
using AiNetLinter.Cli;
using AiNetLinter.Commands;
using AiNetLinter.Configuration;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Projects;
using AiNetLinter.TestKit;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Projects;

[Trait("Category", "Unit")]
public sealed class ProjectInstanceFactoryTests
{
    [Fact]
    public void TryCreate_FromDefinition_MaterializesConfigFromDefinitionRulesPath()
    {
        using var tempDir = TestTempDirectory.Create("project-factory-");
        tempDir.CreateFile("proj/app.slnx", "");
        tempDir.CreateFile(
            "proj/config/rules.json",
            """{ "Global": {}, "Metrics": { "MaxLineCount": 42 } }""");
        WriteDefinition(tempDir);
        var definition = LoadDefinition(tempDir);

        var captured = CaptureOptions(definition);

        Assert.False(captured.Creation.Succeeded);
        Assert.Equal("TEST_CAPTURE", captured.Creation.ErrorCode);
        Assert.Equal(definition.RulesPath, captured.Options!.ResolvedConfigPath);
        Assert.False(captured.Options.UsedDefaultConfig);
        Assert.Equal(42, captured.Options.MaxLineCount);
        Assert.Equal(42, captured.Options.Config.Metrics.MaxLineCount);
    }

    [Fact]
    public void TryCreate_MaxLineCount_MatchesLegacyBatchPipeline()
    {
        using var tempDir = TestTempDirectory.Create("project-factory-legacy-");
        tempDir.CreateFile("proj/app.slnx", "");
        var rulesPath = tempDir.CreateFile(
            "proj/config/rules.json",
            """{ "Global": {}, "Metrics": { "MaxLineCount": 7 } }""");
        WriteDefinition(tempDir);

        var captured = CaptureOptions(LoadDefinition(tempDir));
        var legacy = McpServerCommand.ResolveMaxLineCount(
            new LinterArgs { TargetPath = tempDir.DirectoryPath, Verbose = false },
            rulesPath);

        Assert.Equal(legacy, captured.Options!.MaxLineCount);
    }

    [Fact]
    public void TryCreate_ReadableButInvalidRules_FailsWithRulesInvalidInsteadOfDefaults()
    {
        using var tempDir = TestTempDirectory.Create("project-factory-invalid-");
        tempDir.CreateFile("proj/app.slnx", "");
        tempDir.CreateFile("proj/config/rules.json", "{ this is not valid json ");
        WriteDefinition(tempDir);
        var definition = LoadDefinition(tempDir);

        var creation = ProjectInstanceFactory.TryCreate(
            definition,
            _ => throw new InvalidOperationException("Ungueltige Regeldatei darf keine Options erzeugen."));

        Assert.False(creation.Succeeded);
        Assert.Null(creation.Server);
        Assert.Equal(ProjectErrorCodes.RulesInvalid, creation.ErrorCode);
        Assert.Contains(definition.RulesPath, creation.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void TryCreate_MissingRulesFile_FailsWithRulesNotFound()
    {
        using var tempDir = TestTempDirectory.Create("project-factory-missing-");
        tempDir.CreateFile("proj/app.slnx", "");
        WriteDefinition(tempDir, rulesRelative: "config/fehlt.json");
        var definition = new ProjectDefinition(
            Path.Combine(tempDir.DirectoryPath, "proj", "app.slnx"),
            Path.Combine(tempDir.DirectoryPath, "proj", "config", "fehlt.json"));

        var creation = ProjectInstanceFactory.TryCreate(
            definition,
            _ => throw new InvalidOperationException("Fehlende Regeldatei darf keine Options erzeugen."));

        Assert.False(creation.Succeeded);
        Assert.Equal(ProjectErrorCodes.RulesNotFound, creation.ErrorCode);
    }

    [Fact]
    public void MaterializeRules_MissingPath_ReturnsMetricsDefaults()
    {
        var result = ProjectInstanceFactory.MaterializeRules(rulesPath: null, isRequired: false);

        Assert.Equal(new MetricsConfig().MaxLineCount, result.MaxLineCount);
        Assert.Equal(new MetricsConfig().MaxLineCount, result.Config.Metrics.MaxLineCount);
    }

    /// <summary>
    /// Materialisiert die Options fuer einen Definitionssatz, ohne eine Serverinstanz zu bauen:
    /// der Callback fängt die Options ab und meldet einen Test-Marker statt einer Instanz.
    /// </summary>
    private static (ProjectInstanceCreation Creation, McpCodeGraphServerOptions? Options) CaptureOptions(
        ProjectDefinition definition)
    {
        McpCodeGraphServerOptions? captured = null;
        var creation = ProjectInstanceFactory.TryCreate(definition, options =>
        {
            captured = options;
            return ProjectInstanceCreation.Failed("TEST_CAPTURE", "Test materialisiert nur die Options.");
        });
        return (creation, captured);
    }

    private static ProjectDefinition LoadDefinition(TestTempDirectory tempDir)
    {
        var result = ProjectDefinitionLoader.Load(Path.Combine(tempDir.DirectoryPath, "proj"));
        Assert.True(result.Succeeded, result.Message);
        return result.Definition!;
    }

    private static void WriteDefinition(TestTempDirectory tempDir, string rulesRelative = "config/rules.json") =>
        tempDir.CreateFile(
            Path.Combine("proj", "ainetlinter.project.json"),
            $$"""{ "solution": "app.slnx", "rules": "{{rulesRelative}}" }""");
}
