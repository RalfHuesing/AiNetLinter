#nullable enable

using System;
using System.IO;
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
    public void TryCreate_ConfiguredNeighbor_MaterializesConfigFromSolutionDirectory()
    {
        using var tempDir = TestTempDirectory.Create("project-factory-");
        var solutionPath = tempDir.CreateFile("proj/app.slnx", "");
        tempDir.CreateFile(
            "proj/ainetlinter-rules.json",
            """{ "Global": {}, "Metrics": { "MaxLineCount": 42 } }""");
        var definition = LoadDefinition(solutionPath);

        var captured = CaptureOptions(definition);

        Assert.False(captured.Creation.Succeeded);
        Assert.Equal("TEST_CAPTURE", captured.Creation.ErrorCode);
        Assert.Equal(definition.RulesPath, captured.Options!.ResolvedConfigPath);
        Assert.False(captured.Options.UsedDefaultConfig);
        Assert.Equal(42, captured.Options.MaxLineCount);
        Assert.Equal(42, captured.Options.Config.Metrics.MaxLineCount);
    }

    [Fact]
    public void TryCreate_InvalidNeighborRules_FailsWithRulesInvalidInsteadOfDefaults()
    {
        using var tempDir = TestTempDirectory.Create("project-factory-invalid-");
        var solutionPath = tempDir.CreateFile("proj/app.slnx", "");
        var rulesPath = tempDir.CreateFile("proj/ainetlinter-rules.json", "{ this is not valid json ");
        var definition = LoadDefinition(solutionPath);

        var creation = ProjectInstanceFactory.TryCreate(
            definition,
            _ => throw new InvalidOperationException("Ungueltige Regeldatei darf keine Options erzeugen."));

        Assert.False(creation.Succeeded);
        Assert.Null(creation.Server);
        Assert.Equal(ProjectErrorCodes.RulesInvalid, creation.ErrorCode);
        Assert.Contains(rulesPath, creation.ErrorMessage, StringComparison.Ordinal);
        Assert.Contains("keine Default-Regeln geladen", creation.ErrorMessage, StringComparison.Ordinal);
        // Kopierfaehige Bauanleitung: minimales, gueltiges ainetlinter-rules.json-Skelett im Fehlertext.
        Assert.Contains("\"Global\": {},", creation.ErrorMessage, StringComparison.Ordinal);
        Assert.Contains("\"MaxLineCount\": 700", creation.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void TryCreate_NonexistentConfiguredRules_FailsWithRulesInvalid()
    {
        using var tempDir = TestTempDirectory.Create("project-factory-missing-");
        var solutionPath = tempDir.CreateFile("proj/app.slnx", "");
        var missingRulesPath = Path.Combine(tempDir.DirectoryPath, "proj", "ainetlinter-rules.json");
        var definition = new ProjectDefinition(
            solutionPath,
            missingRulesPath);

        var creation = ProjectInstanceFactory.TryCreate(
            definition,
            _ => throw new InvalidOperationException("Fehlende Regeldatei darf keine Options erzeugen."));

        Assert.False(creation.Succeeded);
        Assert.Equal(ProjectErrorCodes.RulesInvalid, creation.ErrorCode);
        Assert.Contains(missingRulesPath, creation.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void TryCreate_MissingNeighborRules_CreatesNavigationSessionWithNotConfiguredMetadata()
    {
        using var tempDir = TestTempDirectory.Create("project-factory-not-configured-");
        var solutionPath = tempDir.CreateFile("proj/app.slnx", "");
        var definition = LoadDefinition(solutionPath);

        var captured = CaptureOptions(definition);

        Assert.False(captured.Creation.Succeeded);
        Assert.Equal("TEST_CAPTURE", captured.Creation.ErrorCode);
        Assert.NotNull(captured.Options);
        Assert.True(captured.Options!.UsedDefaultConfig);
        Assert.Null(captured.Options.ResolvedConfigPath);
        Assert.Equal(new MetricsConfig().MaxLineCount, captured.Options.MaxLineCount);
        Assert.Equal(new MetricsConfig().MaxLineCount, captured.Options.Config.Metrics.MaxLineCount);
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

    private static ProjectDefinition LoadDefinition(string solutionPath)
    {
        var result = ProjectDefinitionLoader.LoadSolutionTarget(solutionPath);
        Assert.True(result.Succeeded, result.Message);
        return result.Definition!;
    }
}
