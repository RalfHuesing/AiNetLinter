#nullable enable

using AiNetLinter.Baseline;
using AiNetLinter.Cli;
using AiNetLinter.Configuration;
using Microsoft.CodeAnalysis;

namespace AiNetLinter.FastTests.Baseline;

[Trait("Category", "Component")]
public sealed class SourceFileCatalogPolicyTests
{
    [Fact]
    public void ShouldIncludeProject_FiltersCorrectly()
    {
        using var workspace = new AdhocWorkspace();
        var solution = workspace.CurrentSolution;
        var coreId = ProjectId.CreateNewId();
        solution = solution.AddProject(coreId, "MyProject.Core", "MyProject.Core", LanguageNames.CSharp);
        var testId = ProjectId.CreateNewId();
        solution = solution.AddProject(testId, "MyProject.Tests", "MyProject.Tests", LanguageNames.CSharp);
        var core = solution.GetProject(coreId)!;
        var tests = solution.GetProject(testId)!;
        var config = TestHelper.CreateDefaultConfig() with { TestSentinel = new TestSentinelConfig { TestProjectNameSuffixes = ["Tests"] } };

        Assert.True(SourceFileCatalog.ShouldIncludeProject(core, new LinterArgs { TargetPath = "", Verbose = false, ExcludeTests = true }, config));
        Assert.False(SourceFileCatalog.ShouldIncludeProject(tests, new LinterArgs { TargetPath = "", Verbose = false, ExcludeTests = true }, config));
        Assert.False(SourceFileCatalog.ShouldIncludeProject(core, new LinterArgs { TargetPath = "", Verbose = false, TestsOnly = true }, config));
        Assert.True(SourceFileCatalog.ShouldIncludeProject(tests, new LinterArgs { TargetPath = "", Verbose = false, TestsOnly = true }, config));
        Assert.True(SourceFileCatalog.ShouldIncludeProject(core, new LinterArgs { TargetPath = "", Verbose = false, IncludeProjects = ["*.Core"] }, config));
        Assert.False(SourceFileCatalog.ShouldIncludeProject(tests, new LinterArgs { TargetPath = "", Verbose = false, ExcludeProjects = ["*.Tests"] }, config));
    }

    [Theory]
    [InlineData("obj")]
    [InlineData("bin")]
    [InlineData("worktrees")]
    [InlineData(".worktrees")]
    public void IsGeneratedPath_ExcludedSubdir_ReturnsTrue(string excludedSegment) => Assert.True(SourceFileCatalog.IsGeneratedPath(Path.Combine("repo", "src", excludedSegment, "agent-x", "Foo.cs")));

    [Fact]
    public void IsGeneratedPath_NormalPath_ReturnsFalse() => Assert.False(SourceFileCatalog.IsGeneratedPath(Path.Combine("repo", "src", "Project", "Foo.cs")));
}
