#nullable enable

using System;
using System.IO;
using System.Threading.Tasks;
using AiNetLinter.FastTests.Fixtures;
using AiNetLinter.FastTests.Mcp.Projects;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Projects;
using AiNetLinter.Output;
using AiNetLinter.TestKit;
using static AiNetLinter.TestKit.McpTestResultText;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.FastTests.Mcp;

[Trait("Category", "Unit")]
public sealed class WiringFilesystemContractTests
{
    [Fact]
    public async Task FilesystemDispatch_MissingOrRelativeTargetPath_ReturnsArgumentErrorWithoutLease()
    {
        await using var registry = ProjectRegistryFixture.CreateInspectionRegistry();
        foreach (var root in new string?[] { null, "   ", "relativ/projekt" })
        {
            var result = await ProjectAnalysisDispatcher.ExecuteFilesystemAsync(
                registry,
                new AnalysisTargetRequest(root),
                ThrowingFilesystemCallback);
            Assert.NotEqual(true, result.IsError);
            Assert.Contains("[ERROR]: INVALID_ARGUMENT", TextOf(result), StringComparison.Ordinal);
        }

        Assert.Empty(registry.Snapshots());
    }

    [Fact]
    public async Task FilesystemDispatch_AssemblyTargetReturnsUnsupportedWithCanonicalPath()
    {
        using var tempDir = TestTempDirectory.Create("wiring-filesystem-assembly-");
        var assemblyPath = Path.Combine(tempDir.DirectoryPath, ".", "sample.dll");
        var canonicalPath = Path.GetFullPath(assemblyPath);
        File.WriteAllBytes(canonicalPath, [0]);
        await using var registry = ProjectRegistryFixture.CreateInspectionRegistry();

        var result = await ProjectAnalysisDispatcher.ExecuteFilesystemAsync(
            registry,
            new AnalysisTargetRequest(assemblyPath),
            ThrowingFilesystemCallback);

        Assert.True(result.IsError);
        var text = TextOf(result);
        Assert.Contains("ASSEMBLY_TARGET_UNSUPPORTED", text, StringComparison.Ordinal);
        Assert.Contains(canonicalPath, text, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(registry.Snapshots());
    }

    [Fact]
    public async Task FilesystemDispatch_InvokesCallbackWhileServerIsLoading()
    {
        using var fixture = IsolatedFixtureLease.CopyFixture(SolutionRootLocator.Find(), "SymbolGraphMini");
        var solutionPath = Path.Combine(fixture.RootPath, "SymbolGraphMini.slnx");
        var pendingServer = OverviewTestServers.PendingLoadServer();
        await using var registry = ProjectRegistryFixture.Create(_ => ProjectInstanceCreation.Resident(pendingServer));
        var result = await ProjectAnalysisDispatcher.ExecuteFilesystemAsync(
            registry,
            new AnalysisTargetRequest(Path.Combine(fixture.RootPath, ".", "SymbolGraphMini.slnx")),
            lease => AssertFilesystemCallback(lease, ServerLoadState.Loading, solutionPath));

        Assert.NotEqual(true, result.IsError);
        Assert.StartsWith("physisch", TextOf(result), StringComparison.Ordinal);
    }

    [Fact]
    public async Task FilesystemDispatch_InvokesCallbackAfterLoadFailure()
    {
        using var fixture = IsolatedFixtureLease.CopyFixture(SolutionRootLocator.Find(), "SymbolGraphMini");
        var solutionPath = Path.Combine(fixture.RootPath, "SymbolGraphMini.slnx");
        var console = new RecordingLintConsole();
        var faultingServer = OverviewTestServers.FaultingLoadServer(console);
        await using var registry = ProjectRegistryFixture.Create(_ => ProjectInstanceCreation.Resident(faultingServer));
        await TestWaiter.WaitForConditionAsync(() => faultingServer.LoadState == ServerLoadState.LoadFailed, TimeSpan.FromSeconds(15));
        var result = await ProjectAnalysisDispatcher.ExecuteFilesystemAsync(
            registry,
            new AnalysisTargetRequest(solutionPath),
            lease => AssertFilesystemCallback(lease, ServerLoadState.LoadFailed));

        Assert.NotEqual(true, result.IsError);
        Assert.StartsWith("physisch", TextOf(result), StringComparison.Ordinal);
        Assert.DoesNotContain(ProjectErrorCodes.ProjectLoadFailed, TextOf(result), StringComparison.Ordinal);
    }

    [Fact]
    public async Task FilesystemDispatch_HoldsLeaseUntilCallbackCompletes()
    {
        using var fixture = IsolatedFixtureLease.CopyFixture(SolutionRootLocator.Find(), "SymbolGraphMini");
        var solutionPath = Path.Combine(fixture.RootPath, "SymbolGraphMini.slnx");
        var clock = new FakeClock();
        await using var registry = ProjectWiringFixtures.CreateLoadedRegistry(clock);
        var result = await ProjectAnalysisDispatcher.ExecuteFilesystemAsync(
            registry,
            new AnalysisTargetRequest(solutionPath),
            _ => HoldFilesystemLeaseAsync(registry, solutionPath, clock)).WaitAsync(TimeSpan.FromSeconds(15));

        Assert.StartsWith("ok", TextOf(result), StringComparison.Ordinal);
        clock.AdvanceMinutes(60);
        await registry.RunEvictionTickAsync();
        Assert.Null(registry.FindSnapshot(solutionPath));
    }

    [Fact]
    public async Task PhysicalFilesystemDispatch_RoutesExistingSolutionTargetDirectly()
    {
        using var fixture = IsolatedFixtureLease.CopyFixture(SolutionRootLocator.Find(), "SymbolGraphMini");
        var solutionPath = Path.Combine(fixture.RootPath, "SymbolGraphMini.slnx");

        var result = await ProjectAnalysisDispatcher.ExecutePhysicalFilesystemAsync(
            new AnalysisTargetRequest(Path.Combine(fixture.RootPath, ".", "SymbolGraphMini.slnx")),
            canonicalPath =>
            {
                Assert.Equal(solutionPath, canonicalPath);
                return Task.FromResult(McpToolResults.Text("physisch"));
            });

        Assert.NotEqual(true, result.IsError);
        Assert.StartsWith("physisch", TextOf(result), StringComparison.Ordinal);
    }

    private static Task<CallToolResult> ThrowingFilesystemCallback(ProjectLease _) =>
        throw new InvalidOperationException("darf nicht erreicht werden");

    private static Task<CallToolResult> AssertFilesystemCallback(
        ProjectLease lease, ServerLoadState expectedState, string? expectedRoot = null)
    {
        Assert.Equal(expectedState, lease.Server.LoadState);
        if (expectedRoot is not null)
        {
            var canonicalRoot = Path.GetFullPath(expectedRoot)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            Assert.Equal(canonicalRoot, lease.RootPath);
        }

        if (expectedState == ServerLoadState.LoadFailed)
        {
            Assert.False(lease.LoadFailedResponseEmitted);
        }

        return Task.FromResult(McpToolResults.Text("physisch"));
    }

    private static async Task<CallToolResult> HoldFilesystemLeaseAsync(
        ProjectRegistry registry, string root, FakeClock clock)
    {
        clock.AdvanceMinutes(60);
        await registry.RunEvictionTickAsync();
        Assert.NotNull(registry.FindSnapshot(root));
        await Task.Delay(50);
        return McpToolResults.Text("ok");
    }

}
