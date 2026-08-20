#nullable enable

using System.IO;
using AiNetLinter.Mcp;
using AiNetLinter.TestKit;
using Xunit;

namespace AiNetLinter.FastTests.Mcp;

[Trait("Category", "Unit")]
public sealed class McpCodeGraphServerRefreshTests
{
    private static readonly string SolutionRoot = @"C:\Daten\Entwicklung\Ralf\AiNetLinter";
    private static readonly string SolutionPath = Path.Combine(SolutionRoot, "AiNetLinter.slnx");

    [Fact]
    public void PickProjectForNewFile_TestFileInFastTests_PicksFastTestsProject()
    {
        using var testSolution = CreateMultiProjectSolution();
        var newTestFile = Path.Combine(SolutionRoot, "src", "AiNetLinter.FastTests", "Mcp", "ParentProcessWatchdogTests.cs");

        var pickedProjectId = McpCodeGraphServerRefresh.PickProjectForNewFile(testSolution.Solution, newTestFile);

        Assert.NotNull(pickedProjectId);
        var project = testSolution.Solution.GetProject(pickedProjectId);
        Assert.NotNull(project);
        Assert.Equal("AiNetLinter.FastTests", project!.Name);
    }

    [Fact]
    public void PickProjectForNewFile_ProductionFileInMainProject_PicksMainProject()
    {
        using var testSolution = CreateMultiProjectSolution();
        var newSourceFile = Path.Combine(SolutionRoot, "src", "AiNetLinter", "Commands", "NewCommand.cs");

        var pickedProjectId = McpCodeGraphServerRefresh.PickProjectForNewFile(testSolution.Solution, newSourceFile);

        Assert.NotNull(pickedProjectId);
        var project = testSolution.Solution.GetProject(pickedProjectId);
        Assert.NotNull(project);
        Assert.Equal("AiNetLinter", project!.Name);
    }

    [Fact]
    public void PickProjectForNewFile_TestFileInIntegrationTests_PicksIntegrationTestsProject()
    {
        using var testSolution = CreateMultiProjectSolution();
        var newIntegrationFile = Path.Combine(SolutionRoot, "src", "AiNetLinter.IntegrationTests", "Mcp", "NewIntegrationTests.cs");

        var pickedProjectId = McpCodeGraphServerRefresh.PickProjectForNewFile(testSolution.Solution, newIntegrationFile);

        Assert.NotNull(pickedProjectId);
        var project = testSolution.Solution.GetProject(pickedProjectId);
        Assert.NotNull(project);
        Assert.Equal("AiNetLinter.IntegrationTests", project!.Name);
    }

    [Fact]
    public void PickProjectForNewFile_FileOutsideAllProjectFolders_ReturnsNull()
    {
        using var testSolution = CreateMultiProjectSolution();
        var outsiderFile = Path.Combine(SolutionRoot, "tests", "Fixtures", "CompileErrorMini", "src", "BrokenClassA.cs");

        var pickedProjectId = McpCodeGraphServerRefresh.PickProjectForNewFile(testSolution.Solution, outsiderFile);

        Assert.Null(pickedProjectId);
    }

    [Fact]
    public void PickProjectForNewFile_NestedProjectDirectories_PicksMostSpecificSubProject()
    {
        var rootDir = @"C:\virtual\NestedApp";
        using var testSolution = RoslynTestSolutionFactory.CreateSolution(
            Path.Combine(rootDir, "App.slnx"),
            new ProjectSpec("ParentApp", [("Parent.cs", "class Parent {}")], VirtualProjectDirectory: "src/ParentApp"),
            new ProjectSpec("ChildModule", [("Child.cs", "class Child {}")], VirtualProjectDirectory: "src/ParentApp/ChildModule"));

        var childFile = Path.Combine(rootDir, "src", "ParentApp", "ChildModule", "SubComponent.cs");
        var picked = McpCodeGraphServerRefresh.PickProjectForNewFile(testSolution.Solution, childFile);

        Assert.NotNull(picked);
        Assert.Equal("ChildModule", testSolution.Solution.GetProject(picked)!.Name);
    }

    [Fact]
    public void PickProjectForNewFile_MixedSlashes_MatchesCorrectly()
    {
        using var testSolution = CreateMultiProjectSolution();
        var mixedSlashFile = $"{SolutionRoot.Replace('\\', '/')}/src/AiNetLinter.FastTests/Sub/TestProbe.cs";

        var pickedProjectId = McpCodeGraphServerRefresh.PickProjectForNewFile(testSolution.Solution, mixedSlashFile);

        Assert.NotNull(pickedProjectId);
        Assert.Equal("AiNetLinter.FastTests", testSolution.Solution.GetProject(pickedProjectId)!.Name);
    }

    private static RoslynTestSolution CreateMultiProjectSolution()
    {
        return RoslynTestSolutionFactory.CreateSolution(
            SolutionPath,
            new ProjectSpec("AiNetLinter", [("Program.cs", "class Program {}")], VirtualProjectDirectory: "src/AiNetLinter"),
            new ProjectSpec("AiNetLinter.FastTests", [("UnitTest1.cs", "class UnitTest1 {}")], VirtualProjectDirectory: "src/AiNetLinter.FastTests"),
            new ProjectSpec("AiNetLinter.IntegrationTests", [("IntegTest1.cs", "class IntegTest1 {}")], VirtualProjectDirectory: "src/AiNetLinter.IntegrationTests"),
            new ProjectSpec("AiNetLinter.TestKit", [("Kit1.cs", "class Kit1 {}")], VirtualProjectDirectory: "src/AiNetLinter.TestKit"));
    }
}
