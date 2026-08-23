#nullable enable

using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Projects;
using AiNetLinter.TestKit;

namespace AiNetLinter.FastTests.Mcp.Projects;

[Trait("Category", "Unit")]
public sealed class ProjectRegistryPublishRaceTests
{
    [Fact]
    public async Task Lease_PublishCreationRace_DisposesLoserOnceOutsideRegistryLock()
    {
        await using var harness = new PublishRaceHarness();
        var targetCall = harness.StartTargetCall();
        await harness.WaitForCreationAsync(targetCall);

        var other = await harness.AcquireOtherRootAsync();
        using var otherLease = other.Lease;
        Assert.True(other.Succeeded);

        harness.ReleasePublish();
        var target = await targetCall.WaitAsync(TimeSpan.FromSeconds(15));
        await harness.AssertTargetAsync(target);
        await harness.DisposeRegistryAsync();
        harness.AssertDisposedAfterRegistryDispose();
    }

    private sealed class PublishRaceHarness : IAsyncDisposable
    {
        private readonly TestTempDirectory tempDir = TestTempDirectory.Create("project-registry-publish-race-");
        private readonly string root;
        private readonly string otherRoot;
        private readonly TrackingServerFactory factory = new();
        private readonly TrackingServerFactory otherFactory = new();
        private readonly TaskCompletionSource creationReady = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> disposalProbe = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly ManualResetEventSlim releasePublish = new(false);
        private readonly ManualResetEventSlim disposalProbeCompleted = new(false);
        private readonly ProjectRegistry registry;
        private McpCodeGraphServer? loserServer;
        private McpCodeGraphServer? winnerServer;

        internal PublishRaceHarness()
        {
            root = CreateProjectRoot("proj");
            otherRoot = CreateProjectRoot("other");
            registry = new ProjectRegistry(new ProjectRegistryOptions(
                definition => string.Equals(
                    Path.GetDirectoryName(definition.SolutionPath),
                    Path.GetFullPath(root),
                    StringComparison.OrdinalIgnoreCase)
                    ? factory.Factory(definition)
                    : otherFactory.Factory(definition),
                TimeProvider.System)
            {
                BeforePublishCreation = CreateWinnerAttempt,
            });
            factory.OnServerDisposed = ProbeRegistryLock;
        }

        internal Task<ProjectLeaseResult> StartTargetCall()
        {
            return Task.Run(() => registry.Lease(root));
        }

        internal async Task WaitForCreationAsync(Task<ProjectLeaseResult> targetCall)
        {
            var ready = creationReady.Task.WaitAsync(TimeSpan.FromSeconds(10));
            if (await Task.WhenAny(targetCall, ready) == targetCall)
            {
                await targetCall;
            }

            await ready;
        }

        internal Task<ProjectLeaseResult> AcquireOtherRootAsync()
        {
            return Task.Run(() => registry.Lease(otherRoot));
        }

        internal void ReleasePublish()
        {
            releasePublish.Set();
        }

        internal async Task AssertTargetAsync(ProjectLeaseResult target)
        {
            Assert.True(target.Succeeded);
            using var lease = target.Lease;
            Assert.NotNull(loserServer);
            Assert.NotNull(winnerServer);
            Assert.Same(winnerServer, lease!.Server);
            Assert.NotSame(loserServer, lease.Server);
            Assert.Equal(2, factory.InstancesCreated);
            Assert.Equal(1, factory.DisposalsFor(loserServer!));
            Assert.Equal(0, factory.DisposalsFor(winnerServer!));
            Assert.True(await disposalProbe.Task.WaitAsync(TimeSpan.FromSeconds(10)));
        }

        internal async Task DisposeRegistryAsync()
        {
            await registry.DisposeAsync();
        }

        internal void AssertDisposedAfterRegistryDispose()
        {
            Assert.Equal(1, factory.DisposalsFor(loserServer!));
            Assert.Equal(1, factory.DisposalsFor(winnerServer!));
        }

        public async ValueTask DisposeAsync()
        {
            await registry.DisposeAsync();
            disposalProbeCompleted.Dispose();
            releasePublish.Dispose();
            tempDir.Dispose();
        }

        private ProjectCreationAttempt? CreateWinnerAttempt(string key, ProjectCreationAttempt attempt)
        {
            if (!string.Equals(key, Path.GetFullPath(root), StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            loserServer = attempt.Creation.Server;
            winnerServer = factory.CreateServer(attempt.Definition!);
            creationReady.TrySetResult();
            Assert.True(releasePublish.Wait(TimeSpan.FromSeconds(30)));
            return new ProjectCreationAttempt(
                attempt.Definition,
                ProjectInstanceCreation.Resident(winnerServer!));
        }

        private void ProbeRegistryLock(McpCodeGraphServer disposedServer)
        {
            if (!ReferenceEquals(disposedServer, loserServer))
            {
                return;
            }

            _ = Task.Run(ProbeOtherRoot);
            Assert.True(disposalProbeCompleted.Wait(TimeSpan.FromSeconds(10)));
        }

        private void ProbeOtherRoot()
        {
            try
            {
                var result = registry.Lease(otherRoot);
                result.Lease?.Dispose();
                disposalProbe.TrySetResult(result.Succeeded);
            }
            catch
            {
                disposalProbe.TrySetResult(false);
            }
            finally
            {
                disposalProbeCompleted.Set();
            }
        }

        private string CreateProjectRoot(string name)
        {
            tempDir.CreateFile(Path.Combine(name, "app.slnx"), string.Empty);
            tempDir.CreateFile(Path.Combine(name, "rules.json"), "{}");
            tempDir.CreateFile(
                Path.Combine(name, "ainetlinter.project.json"),
                "{ \"solution\": \"app.slnx\", \"rules\": \"rules.json\" }");
            return Path.Combine(tempDir.DirectoryPath, name);
        }
    }
}
