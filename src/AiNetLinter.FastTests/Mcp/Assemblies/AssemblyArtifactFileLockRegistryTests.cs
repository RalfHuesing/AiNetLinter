#nullable enable

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp.Assemblies.Locking;
using AiNetLinter.TestKit;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Assemblies;

// @covers AssemblyArtifactFileLockRegistry
[Trait("Category", "Unit")]
public sealed class AssemblyArtifactFileLockRegistryTests
{
    [Fact]
    public async Task AcquireAsync_SingleCaller_AcquiresSuccessfullyAndReleases()
    {
        using var temp = TestTempDirectory.Create("artifact-file-lock-");
        var registry = new AssemblyArtifactFileLockRegistry("test.lock");

        var lease = await registry.AcquireAsync(temp.DirectoryPath, CancellationToken.None);
        Assert.False(lease.IsStalled);
        Assert.Equal(Path.GetFullPath(temp.DirectoryPath), lease.Key);

        lease.Dispose();
        // Erneuter Erwerb nach Freigabe muss sofort gelingen
        var secondLease = await registry.AcquireAsync(temp.DirectoryPath, CancellationToken.None);
        Assert.False(secondLease.IsStalled);
        secondLease.Dispose();
    }

    [Fact]
    public async Task AcquireAsync_WhenHeldAndThresholdExceeded_ReturnsStalled()
    {
        using var temp = TestTempDirectory.Create("artifact-file-lock-stall-");
        var registry = new AssemblyArtifactFileLockRegistry("test.lock");

        var firstLease = await registry.AcquireAsync(temp.DirectoryPath, CancellationToken.None);
        Assert.False(firstLease.IsStalled);

        try
        {
            // Zweiter Aufruf mit sehr kurzem StallThreshold (100ms)
            var secondLease = await registry.AcquireAsync(
                temp.DirectoryPath,
                CancellationToken.None,
                stallThreshold: TimeSpan.FromMilliseconds(100));

            Assert.True(secondLease.IsStalled);
            Assert.NotNull(secondLease.StallThreshold);
            secondLease.Dispose();
        }
        finally
        {
            firstLease.Dispose();
        }
    }

    [Fact]
    public async Task AcquireAsync_WhenCancelled_ThrowsOperationCanceledException()
    {
        using var temp = TestTempDirectory.Create("artifact-file-lock-cancel-");
        var registry = new AssemblyArtifactFileLockRegistry("test.lock");

        var firstLease = await registry.AcquireAsync(temp.DirectoryPath, CancellationToken.None);

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            {
                await registry.AcquireAsync(temp.DirectoryPath, cts.Token);
            });
        }
        finally
        {
            firstLease.Dispose();
        }
    }

    [Fact]
    public async Task AcquireAsync_ConcurrentCallers_SecondWaitsUntilFirstReleasesAndSucceeds()
    {
        using var temp = TestTempDirectory.Create("artifact-file-lock-wait-");
        var registry = new AssemblyArtifactFileLockRegistry("test.lock");

        var firstLease = await registry.AcquireAsync(temp.DirectoryPath, CancellationToken.None);
        var secondAcquired = false;

        var secondTask = Task.Run(async () =>
        {
            var secondLease = await registry.AcquireAsync(temp.DirectoryPath, CancellationToken.None);
            secondAcquired = true;
            secondLease.Dispose();
        });

        // Zweiter Aufruf darf noch nicht akquiriert haben, solange firstLease gehalten wird
        await Task.Delay(50);
        Assert.False(secondAcquired);

        firstLease.Dispose();
        await secondTask.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(secondAcquired);
    }
}
