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
    public async Task FilesystemDispatch_MissingOrRelativeProjectRoot_ReturnsArgumentErrorWithoutLease()
    {
        await using var registry = ProjectRegistryFixture.CreateInspectionRegistry();
        foreach (var root in new string?[] { null, "   ", "relativ/projekt" })
        {
            var result = await ProjectAnalysisDispatcher.ExecuteFilesystemAsync(
                registry,
                new AnalysisTargetRequest("project", root),
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
            new AnalysisTargetRequest("assembly", assemblyPath),
            ThrowingFilesystemCallback);

        Assert.NotEqual(true, result.IsError);
        var text = TextOf(result);
        Assert.Contains("ASSEMBLY_TARGET_UNSUPPORTED", text, StringComparison.Ordinal);
        Assert.Contains(canonicalPath, text, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(registry.Snapshots());
    }

    [Fact]
    public async Task FilesystemDispatch_InvokesCallbackWhileServerIsLoading()
    {
        using var tempDir = TestTempDirectory.Create("wiring-filesystem-loading-");
        var root = ProjectRegistryFixture.CreateProjectRoot(tempDir, "proj");
        var pendingServer = OverviewTestServers.PendingLoadServer();
        await using var registry = ProjectRegistryFixture.Create(_ => ProjectInstanceCreation.Resident(pendingServer));
        var result = await ProjectAnalysisDispatcher.ExecuteFilesystemAsync(
            registry,
            new AnalysisTargetRequest("project", root),
            lease => AssertFilesystemCallback(lease, ServerLoadState.Loading, root));

        Assert.NotEqual(true, result.IsError);
        Assert.Equal("physisch", TextOf(result));
    }

    [Fact]
    public async Task FilesystemDispatch_InvokesCallbackAfterLoadFailure()
    {
        using var tempDir = TestTempDirectory.Create("wiring-filesystem-failed-");
        var root = ProjectRegistryFixture.CreateProjectRoot(tempDir, "proj");
        var console = new RecordingLintConsole();
        var faultingServer = OverviewTestServers.FaultingLoadServer(console);
        await using var registry = ProjectRegistryFixture.Create(_ => ProjectInstanceCreation.Resident(faultingServer));
        await TestWaiter.WaitForConditionAsync(() => faultingServer.LoadState == ServerLoadState.LoadFailed, TimeSpan.FromSeconds(15));
        var result = await ProjectAnalysisDispatcher.ExecuteFilesystemAsync(
            registry,
            new AnalysisTargetRequest("project", root),
            lease => AssertFilesystemCallback(lease, ServerLoadState.LoadFailed));

        Assert.NotEqual(true, result.IsError);
        Assert.Equal("physisch", TextOf(result));
        Assert.DoesNotContain(ProjectErrorCodes.ProjectLoadFailed, TextOf(result), StringComparison.Ordinal);
    }

    [Fact]
    public async Task FilesystemDispatch_HoldsLeaseUntilCallbackCompletes()
    {
        using var tempDir = TestTempDirectory.Create("wiring-filesystem-lease-");
        var root = ProjectRegistryFixture.CreateProjectRoot(tempDir, "proj");
        var clock = new FakeClock();
        await using var registry = ProjectWiringFixtures.CreateLoadedRegistry(clock);
        var result = await ProjectAnalysisDispatcher.ExecuteFilesystemAsync(
            registry,
            new AnalysisTargetRequest("project", root),
            _ => HoldFilesystemLeaseAsync(registry, root, clock)).WaitAsync(TimeSpan.FromSeconds(15));

        Assert.Equal("ok", TextOf(result));
        clock.AdvanceMinutes(60);
        await registry.RunEvictionTickAsync();
        Assert.Null(registry.FindSnapshot(root));
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
