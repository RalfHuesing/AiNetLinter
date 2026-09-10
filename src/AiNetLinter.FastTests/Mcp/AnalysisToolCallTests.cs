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
    public void ResolveTargetPathOnly_InfersSourceAndProjectsItsCapabilities()
    {
        using var fixture = IsolatedFixtureLease.CopyFixture(SolutionRootLocator.Find(), "SymbolGraphMini");
        var solutionPath = Path.Combine(fixture.RootPath, "SymbolGraphMini.slnx");

        var result = AnalysisTargetResolver.Resolve(new AnalysisTargetRequest(
            Path.Combine(fixture.RootPath, ".", "SymbolGraphMini.slnx")));

        Assert.Null(result.Error);
        Assert.NotNull(result.Target);
        var target = result.Target!;
        Assert.Equal(AnalysisTargetType.Project, target.TargetType);
        Assert.Equal(Path.GetFullPath(solutionPath), target.CanonicalPath);
        Assert.Equal(Path.GetDirectoryName(target.CanonicalPath), target.AnalysisRoot);
        Assert.Equal(AnalysisCapabilityStatus.Supported, target.Capabilities.Navigation);
        Assert.Equal(AnalysisCapabilityStatus.NotConfigured, target.Capabilities.Lint);
        Assert.NotEmpty(target.Fingerprint);
    }

    [Fact]
    public void ResolveTargetPathOnly_InfersAssemblyAndMarksLintUnsupported()
    {
        using var tempDir = TestTempDirectory.Create("analysis-target-assembly-capability-");
        var assemblyPath = Path.Combine(tempDir.DirectoryPath, "sample.exe");
        File.WriteAllBytes(assemblyPath, [0]);

        var result = AnalysisTargetResolver.Resolve(new AnalysisTargetRequest(assemblyPath));

        Assert.Null(result.Error);
        Assert.NotNull(result.Target);
        var target = result.Target!;
        Assert.Equal(AnalysisTargetType.Assembly, target.TargetType);
        Assert.Equal(AnalysisTargetOrigin.Decompiled, target.Origin);
        Assert.Equal(AnalysisCapabilityStatus.Supported, target.Capabilities.Navigation);
        Assert.Equal(AnalysisCapabilityStatus.Unsupported, target.Capabilities.Lint);
        Assert.Null(target.RulesPath);
    }

    [Fact]
    public async Task CreateTargetRoute_UsesCanonicalTargetPathAndInferredOrigin()
    {
        using var tempDir = TestTempDirectory.Create("analysis-target-route-");
        var assemblyPath = Path.Combine(tempDir.DirectoryPath, "sample.dll");
        File.WriteAllBytes(assemblyPath, [0]);
        var requestPath = Path.Combine(tempDir.DirectoryPath, ".", "sample.dll");
        string? receivedPath = null;

        var route = AnalysisToolCall.CreateTargetRoute(
            request =>
            {
                receivedPath = request.Target.TargetPath;
                return Task.FromResult(McpToolResults.Text("source"));
            },
            request =>
            {
                receivedPath = request.Target.TargetPath;
                return Task.FromResult(McpToolResults.Text("assembly"));
            });

        var result = await AnalysisToolCall.ExecuteRouted(
            route,
            new AnalysisToolCallRequest(
                new AnalysisTargetRequest(requestPath),
                new AnalysisToolDispatch()));

        Assert.Equal("assembly", TextOf(result));
        Assert.Equal(Path.GetFullPath(assemblyPath), receivedPath);
    }

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
            new AnalysisTargetRequest(assemblyPath),
            (new AnalysisToolDispatch(ProjectCall: _ =>
            {
                projectCalled = true;
                return Task.FromResult(McpToolResults.Text("unerwartet"));
            })).ProjectCall!);

        Assert.False(projectCalled);
        Assert.False(result.IsError ?? false);
        Assert.Contains("ASSEMBLY_TARGET_UNSUPPORTED", TextOf(result), StringComparison.Ordinal);
        Assert.Empty(registry.Snapshots());
    }

    [Fact]
    public async Task ExecuteAsync_ProjectTargetPassesCanonicalPathToExistingRegistryLease()
    {
        using var fixture = IsolatedFixtureLease.CopyFixture(SolutionRootLocator.Find(), "SymbolGraphMini");
        var solutionPath = Path.Combine(fixture.RootPath, "SymbolGraphMini.slnx");
        var requestPath = Path.Combine(fixture.RootPath, ".", "SymbolGraphMini.slnx");
        await using var registry = ProjectWiringFixtures.CreateLoadedRegistry();

        var result = await ProjectAnalysisDispatcher.ExecuteAsync(
            registry,
            new AnalysisTargetRequest(requestPath),
            (new AnalysisToolDispatch(ProjectCall: lease =>
                Task.FromResult(McpToolResults.Text(lease.RootPath)))).ProjectCall!);

        var text = TextOf(result);
        Assert.StartsWith(Path.GetFullPath(solutionPath), text, StringComparison.Ordinal);
        Assert.Contains("## Navigation", text, StringComparison.Ordinal);
        Assert.Contains("- operationStatus: `ok`", text, StringComparison.Ordinal);
        Assert.Contains("- completeness: `complete`", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_OptionsForwardBudgetAndDefaultsRemainUnbounded()
    {
        using var fixture = IsolatedFixtureLease.CopyFixture(SolutionRootLocator.Find(), "SymbolGraphMini");
        var solutionPath = Path.Combine(fixture.RootPath, "SymbolGraphMini.slnx");
        await using var registry = ProjectWiringFixtures.CreateLoadedRegistry();
        var callbackCount = 0;
        var observedBudget = 0;

        var result = await ProjectAnalysisDispatcher.ExecuteAsync(
            registry,
            new AnalysisTargetRequest(solutionPath),
            _ => Task.FromResult(McpToolResults.Text("payload")),
            new ProjectAnalysisExecutionOptions(
                MaxResponseBytes: 128,
                PostNavigationResponseBudget: (budgeted, maxBytes) =>
                {
                    callbackCount++;
                    observedBudget = maxBytes;
                    return budgeted;
                }));

        Assert.False(result.IsError ?? false);
        Assert.Equal(1, callbackCount);
        Assert.Equal(128, observedBudget);

        var defaults = new ProjectAnalysisExecutionOptions();
        Assert.Equal(0, defaults.MaxResponseBytes);
        Assert.Null(defaults.PostNavigationResponseBudget);
    }

    [Fact]
    public async Task CreateTargetPathRoute_PassesCanonicalPathToAssemblyAdapter()
    {
        using var tempDir = TestTempDirectory.Create("analysis-dispatch-specialized-");
        var assemblyPath = Path.Combine(tempDir.DirectoryPath, ".", "sample.dll");
        File.WriteAllBytes(Path.GetFullPath(assemblyPath), [0]);
        string? receivedPath = null;

        var result = await AnalysisToolCall.ExecuteRouted(
            AnalysisToolCall.CreateTargetPathRoute(
                _ => Task.FromResult(McpToolResults.Text("source")),
                request =>
                {
                    receivedPath = request.Target.TargetPath;
                    return Task.FromResult(McpToolResults.Text("assembly"));
                }),
            new AnalysisToolCallRequest(
                new AnalysisTargetRequest(assemblyPath),
                new AnalysisToolDispatch()));

        Assert.Equal(Path.GetFullPath(assemblyPath), receivedPath);
        Assert.Equal("assembly", TextOf(result));
    }

    private static string TextOf(CallToolResult result) =>
        Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
}
