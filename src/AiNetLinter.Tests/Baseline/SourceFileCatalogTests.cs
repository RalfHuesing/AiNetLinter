using AiNetLinter.Baseline;
using Xunit;
using Microsoft.CodeAnalysis;
using AiNetLinter.Cli;
using AiNetLinter.Configuration;

namespace AiNetLinter.Tests.Baseline;

public sealed class SourceFileCatalogTests
{
    [Fact]
    public async Task LoadAsync_MiniFixture_ReturnsSourceFiles()
    {
        var fixtureRoot = Path.Combine(FindSolutionRoot(), "tests", "Fixtures", "BaselineMini");

        using var catalog = await SourceFileCatalog.LoadAsync(fixtureRoot);
        var files = catalog.GetSourceFiles(fixtureRoot);

        Assert.Contains(files, f => f.RelativePath.EndsWith("ViolatingClass.cs", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ShouldIncludeProject_FiltersCorrectly()
    {
        using var workspace = new AdhocWorkspace();
        var solution = workspace.CurrentSolution;
        var projectId1 = ProjectId.CreateNewId();
        solution = solution.AddProject(projectId1, "MyProject.Core", "MyProject.Core", LanguageNames.CSharp);
        var project1 = solution.GetProject(projectId1)!;

        var projectId2 = ProjectId.CreateNewId();
        solution = solution.AddProject(projectId2, "MyProject.Tests", "MyProject.Tests", LanguageNames.CSharp);
        var project2 = solution.GetProject(projectId2)!;

        var config = new Config
        {
            Global = new GlobalConfig(),
            Metrics = new MetricsConfig(),
            TestSentinel = new TestSentinelConfig
            {
                TestProjectNameSuffixes = new[] { "Tests" }
            }
        };

        var argsExcludeTests = new LinterArgs { TargetPath = "", Verbose = false, ExcludeTests = true };
        Assert.True(SourceFileCatalog.ShouldIncludeProject(project1, argsExcludeTests, config));
        Assert.False(SourceFileCatalog.ShouldIncludeProject(project2, argsExcludeTests, config));

        var argsTestsOnly = new LinterArgs { TargetPath = "", Verbose = false, TestsOnly = true };
        Assert.False(SourceFileCatalog.ShouldIncludeProject(project1, argsTestsOnly, config));
        Assert.True(SourceFileCatalog.ShouldIncludeProject(project2, argsTestsOnly, config));

        var argsIncludeProject = new LinterArgs { TargetPath = "", Verbose = false, IncludeProjects = new[] { "*.Core" } };
        Assert.True(SourceFileCatalog.ShouldIncludeProject(project1, argsIncludeProject, config));
        Assert.False(SourceFileCatalog.ShouldIncludeProject(project2, argsIncludeProject, config));

        var argsExcludeProject = new LinterArgs { TargetPath = "", Verbose = false, ExcludeProjects = new[] { "*.Tests" } };
        Assert.True(SourceFileCatalog.ShouldIncludeProject(project1, argsExcludeProject, config));
        Assert.False(SourceFileCatalog.ShouldIncludeProject(project2, argsExcludeProject, config));
    }

    private static string FindSolutionRoot()
    {
        var currentDir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
        while (currentDir != null)
        {
            if (File.Exists(Path.Combine(currentDir.FullName, "AiNetLinter.slnx")))
            {
                return currentDir.FullName;
            }

            currentDir = currentDir.Parent;
        }

        throw new DirectoryNotFoundException("Solution root not found.");
    }
}
