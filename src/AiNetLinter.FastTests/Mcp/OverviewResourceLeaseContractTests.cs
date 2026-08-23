#nullable enable

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Baseline;
using AiNetLinter.Configuration;
using AiNetLinter.FastTests.Fixtures;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Projects;
using AiNetLinter.Output;
using AiNetLinter.TestKit;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.FastTests.Mcp;

[Trait("Category", "Unit")]
public sealed class OverviewResourceLeaseContractTests
{
    [Fact]
    public async Task LoadFailed_UsesToolContractAndReleasesAfterExplicitToolResponse()
    {
        using var tempDir = TestTempDirectory.Create("overview-failed-");
        var root = ProjectRegistryFixture.CreateProjectRoot(tempDir, "proj");
        var load = new TaskCompletionSource<SourceFileCatalog?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstServer = new McpCodeGraphServer(new McpCodeGraphServerOptions
        {
            Catalog = null,
            Console = LinterConsole.Instance,
            Config = new Config { Global = new GlobalConfig(), Metrics = new MetricsConfig() },
            UsedDefaultConfig = false,
            LoadFunc = _ => load.Task,
        });
        var factoryCalls = 0;
        await using var registry = ProjectRegistryFixture.Create(_ =>
            Interlocked.Increment(ref factoryCalls) == 1
                ? ProjectInstanceCreation.Resident(firstServer)
                : ProjectInstanceCreation.Resident(OverviewTestServers.FaultingLoadServer(LinterConsole.Instance)));

        var initial = registry.Lease(root);
        initial.Lease!.Dispose();
        load.SetException(new InvalidOperationException("Overview-Kalt-Load-Fehler"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => firstServer.LoadTask!);

        var exception = Assert.Throws<McpException>(
            () => OverviewResourceRegistration.BuildTemplatedResult(registry, root));

        Assert.Contains(ProjectErrorCodes.ProjectLoadFailed, exception.Message, StringComparison.Ordinal);
        Assert.Contains("Overview-Kalt-Load-Fehler", exception.Message, StringComparison.Ordinal);
        Assert.Contains(Path.Combine(root, "app.slnx"), exception.Message, StringComparison.Ordinal);
        Assert.Contains("automatisch neu", exception.Message, StringComparison.Ordinal);

        await ProjectToolCall.ExecuteAsync(
            registry,
            root,
            _ => Task.FromResult(McpToolResults.Text("sollte nie erreicht werden")));

        var retry = registry.Lease(root);
        using var retryLease = retry.Lease;
        Assert.Equal(2, Volatile.Read(ref factoryCalls));
        Assert.NotSame(firstServer, retryLease!.Server);
    }

    [Fact]
    public async Task RenderingLeaseProtectsServerFromEvictionUntilRenderingCompletes()
    {
        using var tempDir = TestTempDirectory.Create("overview-lease-");
        var root = ProjectRegistryFixture.CreateProjectRoot(tempDir, "proj");
        var otherRoot = ProjectRegistryFixture.CreateProjectRoot(tempDir, "other");
        var replacementRoot = ProjectRegistryFixture.CreateProjectRoot(tempDir, "replacement");
        await using var registry = ProjectRegistryFixture.Create(
            _ => ProjectInstanceCreation.Resident(OverviewTestServers.PendingLoadServer()),
            maxProjects: 1);

        var initial = registry.Lease(root);
        var renderingServer = initial.Lease!.Server;
        initial.Lease.Dispose();
        using var renderEntered = new ManualResetEventSlim(false);
        using var releaseRender = new ManualResetEventSlim(false);
        var readTask = Task.Run(() => OverviewResourceRegistration.BuildTemplatedResult(
            registry,
            root,
            snapshot =>
            {
                renderEntered.Set();
                releaseRender.Wait(TimeSpan.FromSeconds(15));
                return new ReadResourceResult
                {
                    Contents =
                    [
                        new TextResourceContents
                        {
                            Uri = "test://overview",
                            MimeType = "text/markdown",
                            Text = OverviewResourceRegistration.BuildOverviewText(snapshot),
                        },
                    ],
                };
            }));

        Assert.True(renderEntered.Wait(TimeSpan.FromSeconds(15)));
        var other = registry.Lease(otherRoot);
        using var otherLease = other.Lease;
        await registry.RunEvictionTickAsync();

        Assert.Contains(registry.Snapshots(), snapshot => ReferenceEquals(snapshot.Server, renderingServer));
        Assert.False(renderingServer.LoadTask!.IsCanceled);

        releaseRender.Set();
        await readTask.WaitAsync(TimeSpan.FromSeconds(15));
        var replacement = registry.Lease(replacementRoot);
        using var replacementLease = replacement.Lease;

        Assert.True(replacement.Succeeded);
        Assert.True(renderingServer.LoadTask!.IsCanceled);
    }

}
