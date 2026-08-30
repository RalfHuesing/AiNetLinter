#nullable enable

using System;
using System.IO;
using System.Threading.Tasks;
using AiNetLinter.FastTests.Fixtures;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Projects;
using AiNetLinter.TestKit;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.FastTests.Mcp;

[Trait("Category", "Unit")]
public sealed class AnalysisToolCallTests
{
    [Fact]
    public async Task ExecuteAsync_AssemblyTargetReturnsRecoverableUnsupportedWithoutProjectLease()
    {
        using var tempDir = TestTempDirectory.Create("analysis-dispatch-assembly-");
        var assemblyPath = Path.Combine(tempDir.DirectoryPath, "sample.dll");
        File.WriteAllBytes(assemblyPath, [0]);
        await using var registry = ProjectRegistryFixture.CreateInspectionRegistry();
        var projectCalled = false;

        var result = await ProjectAnalysisDispatcher.ExecuteAsync(
            registry,
            new AnalysisTargetRequest("assembly", assemblyPath),
            (new AnalysisToolDispatch(ProjectCall: _ =>
            {
                projectCalled = true;
                return Task.FromResult(McpToolResults.Text("unerwartet"));
            })).ProjectCall!);

        Assert.False(projectCalled);
        Assert.False(result.IsError);
        Assert.Contains("ASSEMBLY_TARGET_UNSUPPORTED", TextOf(result), StringComparison.Ordinal);
        Assert.Empty(registry.Snapshots());
    }

    [Fact]
    public async Task ExecuteAsync_ProjectTargetPassesCanonicalPathToExistingRegistryLease()
    {
        using var tempDir = TestTempDirectory.Create("analysis-dispatch-project-");
        var projectRoot = ProjectRegistryFixture.CreateProjectRoot(tempDir, "project");
        var requestPath = Path.Combine(projectRoot, ".", "sub", "..");
        await using var registry = ProjectWiringFixtures.CreateLoadedRegistry();

        var result = await ProjectAnalysisDispatcher.ExecuteAsync(
            registry,
            new AnalysisTargetRequest("project", requestPath),
            (new AnalysisToolDispatch(ProjectCall: lease =>
                Task.FromResult(McpToolResults.Text(lease.RootPath)))).ProjectCall!);

        Assert.Equal(Path.GetFullPath(projectRoot), TextOf(result));
    }

    [Fact]
    public async Task ExecuteAssemblyAsync_PassesCanonicalPathToSpecializedAdapter()
    {
        using var tempDir = TestTempDirectory.Create("analysis-dispatch-specialized-");
        var assemblyPath = Path.Combine(tempDir.DirectoryPath, ".", "sample.dll");
        File.WriteAllBytes(Path.GetFullPath(assemblyPath), [0]);
        await using var registry = ProjectRegistryFixture.CreateInspectionRegistry();
        string? receivedPath = null;

        var result = await ProjectAnalysisDispatcher.ExecuteAssemblyAsync(
            registry,
            "assembly",
            assemblyPath,
            path =>
            {
                receivedPath = path;
                return Task.FromResult(McpToolResults.Text("assembly"));
            });

        Assert.Equal(Path.GetFullPath(assemblyPath), receivedPath);
        Assert.Equal("assembly", TextOf(result));
    }

    private static string TextOf(CallToolResult result) =>
        Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
}
