#nullable enable

using System;
using System.IO;
using AiNetLinter.Mcp.Projects;
using AiNetLinter.TestKit;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Projects;

[Trait("Category", "Unit")]
public sealed class ProjectDefinitionLoaderTests
{
    [Fact]
    public void Load_BothFieldsPresent_ResolvesRelativePathsAgainstDefinitionDirectory()
    {
        using var tempDir = TestTempDirectory.Create("project-def-loader-");
        tempDir.CreateFile("proj/app.slnx", "");
        tempDir.CreateFile("proj/config/rules.json", "{}");
        WriteDefinition(tempDir, """{ "solution": "app.slnx", "rules": "config/rules.json" }""");

        var result = ProjectDefinitionLoader.Load(ProjectRoot(tempDir));

        var loaded = AsLoaded(result);
        Assert.Equal(ResolveExpected(tempDir, "app.slnx"), loaded.SolutionPath);
        Assert.Equal(ResolveExpected(tempDir, "config/rules.json"), loaded.RulesPath);
    }

    [Fact]
    public void Load_AbsolutePaths_TakenUnchanged()
    {
        using var tempDir = TestTempDirectory.Create("project-def-loader-abs-");
        var solutionPath = tempDir.CreateFile("proj/app.slnx", "");
        var rulesPath = tempDir.CreateFile("proj/config/rules.json", "{}");
        WriteDefinition(
            tempDir,
            $$"""{ "solution": "{{JsonEscape(solutionPath)}}", "rules": "{{JsonEscape(rulesPath)}}" }""");

        var result = ProjectDefinitionLoader.Load(ProjectRoot(tempDir));

        var loaded = AsLoaded(result);
        Assert.Equal(solutionPath, loaded.SolutionPath);
        Assert.Equal(rulesPath, loaded.RulesPath);
    }

    [Fact]
    public void Load_MissingSolutionField_ReportsProjectDefinitionInvalid()
    {
        using var tempDir = TestTempDirectory.Create("project-def-loader-sol-");
        var definitionPath = WriteDefinition(tempDir, "{ }");

        var failed = AsFailed(ProjectDefinitionLoader.Load(ProjectRoot(tempDir)));

        Assert.Equal(ProjectErrorCodes.ProjectDefinitionInvalid, failed.ErrorCode);
        Assert.Contains("'solution'", failed.Message);
        Assert.Contains(definitionPath, failed.Message);
    }

    [Fact]
    public void Load_MissingRulesField_ReportsProjectDefinitionInvalid()
    {
        using var tempDir = TestTempDirectory.Create("project-def-loader-rul-");
        var definitionPath = WriteDefinition(tempDir, """{ "solution": "app.slnx" }""");

        var failed = AsFailed(ProjectDefinitionLoader.Load(ProjectRoot(tempDir)));

        Assert.Equal(ProjectErrorCodes.ProjectDefinitionInvalid, failed.ErrorCode);
        Assert.Contains("'rules'", failed.Message);
        Assert.Contains(definitionPath, failed.Message);
    }

    [Fact]
    public void Load_NonStringSolutionField_ReportsProjectDefinitionInvalid()
    {
        using var tempDir = TestTempDirectory.Create("project-def-loader-type-");
        WriteDefinition(tempDir, """{ "solution": 42, "rules": "config/rules.json" }""");

        var failed = AsFailed(ProjectDefinitionLoader.Load(ProjectRoot(tempDir)));

        Assert.Equal(ProjectErrorCodes.ProjectDefinitionInvalid, failed.ErrorCode);
        Assert.Contains("'solution'", failed.Message);
    }

    [Fact]
    public void Load_DefectiveJson_ReportsProjectDefinitionInvalid()
    {
        using var tempDir = TestTempDirectory.Create("project-def-loader-json-");
        var definitionPath = WriteDefinition(tempDir, """{ "solution": "app.slnx", """);

        var failed = AsFailed(ProjectDefinitionLoader.Load(ProjectRoot(tempDir)));

        Assert.Equal(ProjectErrorCodes.ProjectDefinitionInvalid, failed.ErrorCode);
        Assert.Contains(definitionPath, failed.Message);
    }

    [Fact]
    public void Load_MissingDefinitionFile_ReportsNotInitializedWithVerbatimTemplate()
    {
        using var tempDir = TestTempDirectory.Create("project-def-loader-miss-");
        var projectRoot = Path.Combine(tempDir.DirectoryPath, "uninitialized");
        var definitionPath = Path.Combine(projectRoot, "ainetlinter.project.json");

        var failed = AsFailed(ProjectDefinitionLoader.Load(projectRoot));

        Assert.Equal(ProjectErrorCodes.ProjectNotInitialized, failed.ErrorCode);
        Assert.Contains(BuildExpectedTemplate(definitionPath), failed.Message);
    }

    [Fact]
    public void Load_MissingRootDirectory_ReportsNotInitializedWithVerbatimTemplate()
    {
        using var tempDir = TestTempDirectory.Create("project-def-loader-root-");
        var projectRoot = Path.Combine(tempDir.DirectoryPath, "gone");
        var definitionPath = Path.Combine(projectRoot, "ainetlinter.project.json");

        var failed = AsFailed(ProjectDefinitionLoader.Load(projectRoot));

        Assert.Equal(ProjectErrorCodes.ProjectNotInitialized, failed.ErrorCode);
        Assert.Contains(BuildExpectedTemplate(definitionPath), failed.Message);
    }

    [Fact]
    public void Load_MissingSolutionTarget_ReportsSolutionNotFoundWithResolvedPath()
    {
        using var tempDir = TestTempDirectory.Create("project-def-loader-solmiss-");
        tempDir.CreateFile("proj/config/rules.json", "{}");
        WriteDefinition(tempDir, """{ "solution": "fehlt.slnx", "rules": "config/rules.json" }""");

        var failed = AsFailed(ProjectDefinitionLoader.Load(ProjectRoot(tempDir)));

        Assert.Equal(ProjectErrorCodes.SolutionNotFound, failed.ErrorCode);
        Assert.Contains(ResolveExpected(tempDir, "fehlt.slnx"), failed.Message);
    }

    [Fact]
    public void Load_MissingRulesTarget_ReportsRulesNotFoundWithResolvedPath()
    {
        using var tempDir = TestTempDirectory.Create("project-def-loader-rulmiss-");
        tempDir.CreateFile("proj/app.slnx", "");
        WriteDefinition(tempDir, """{ "solution": "app.slnx", "rules": "config/fehlt.json" }""");

        var failed = AsFailed(ProjectDefinitionLoader.Load(ProjectRoot(tempDir)));

        Assert.Equal(ProjectErrorCodes.RulesNotFound, failed.ErrorCode);
        Assert.Contains(ResolveExpected(tempDir, "config/fehlt.json"), failed.Message);
    }

    [Fact]
    public void Load_MissingRulesTarget_IgnoresNeighboringRulesJson()
    {
        using var tempDir = TestTempDirectory.Create("project-def-loader-fallback-");
        tempDir.CreateFile("proj/app.slnx", "");
        tempDir.CreateFile("proj/rules.json", "{}");
        tempDir.CreateFile("proj/config/rules.json", "{}");
        WriteDefinition(tempDir, """{ "solution": "app.slnx", "rules": "config/fehlt.json" }""");

        var failed = AsFailed(ProjectDefinitionLoader.Load(ProjectRoot(tempDir)));

        Assert.Equal(ProjectErrorCodes.RulesNotFound, failed.ErrorCode);
        Assert.Contains(ResolveExpected(tempDir, "config/fehlt.json"), failed.Message);
        Assert.DoesNotContain(ResolveExpected(tempDir, "config/rules.json"), failed.Message);
        Assert.DoesNotContain(ResolveExpected(tempDir, "rules.json"), failed.Message);
    }

    private static string ProjectRoot(TestTempDirectory tempDir) => Path.Combine(tempDir.DirectoryPath, "proj");

    private static string WriteDefinition(TestTempDirectory tempDir, string content) =>
        tempDir.CreateFile(Path.Combine("proj", "ainetlinter.project.json"), content);

    private static string ResolveExpected(TestTempDirectory tempDir, string relativeToDefinition) =>
        Path.GetFullPath(Path.Combine(tempDir.DirectoryPath, "proj", relativeToDefinition));

    private static ProjectDefinition AsLoaded(ProjectDefinitionLoadResult result)
    {
        Assert.True(result.Succeeded, result.Message);
        return result.Definition!;
    }

    private static ProjectDefinitionLoadResult AsFailed(ProjectDefinitionLoadResult result)
    {
        Assert.False(result.Succeeded, $"expected failure, got definition '{result.Definition?.SolutionPath}'");
        return result;
    }

    private static string JsonEscape(string path) => path.Replace("\\", "\\\\");

    private static string BuildExpectedTemplate(string definitionPath)
    {
        var nl = Environment.NewLine;
        return
            $"Create {definitionPath} with:" + nl +
            "{" + nl +
            "  \"solution\": \"<path/to/your.slnx or .sln>\",  // relative to this file, or absolute" + nl +
            "  \"rules\":    \"<path/to/rules.json>\"          // relative to this file, or absolute; MUST exist" + nl +
            "}" + nl +
            "Then retry the call with the same projectRoot.";
    }
}
