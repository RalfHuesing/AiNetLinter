#nullable enable

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Commands;
using AiNetLinter.Mcp.Assemblies.ExternalSource.Repository;
using Xunit;

namespace AiNetLinter.IntegrationTests.Mcp.Assemblies;

[Trait("Category", "Integration")]
public sealed class ExternalSourceRepositoryCacheLeaseProcessTests
{
    private const string RepositoryUrl = "https://gitea.example/cache-lease-probe.git";
    private const string SolutionPath = "src/LeaseProbe.slnx";
    private const string Revision = "0123456789abcdef0123456789abcdef01234567";
    private const string OwnershipToken = "cache-lease-probe-owner";

    [Fact]
    public async Task SeparateProcesses_ReaderLeaseSurvivesRetentionAndCancellationReleasesLocks()
    {
        using var temp = TestTempDirectory.Create("external-source-cache-lease-process-");
        var cacheRoot = temp.CreateSubdirectory("cache");
        var sourceRoot = temp.CreateSubdirectory("source");
        var sourceCheckout = Path.Combine(sourceRoot, "checkout");
        Directory.CreateDirectory(sourceCheckout);
        var readerRoot = temp.CreateSubdirectory("reader");
        File.WriteAllText(
            Path.Combine(sourceCheckout, ExternalSourceCheckoutOwnership.OwnershipMarkerFileName),
            OwnershipToken);
        Directory.CreateDirectory(Path.Combine(sourceCheckout, ".git"));
        Directory.CreateDirectory(Path.Combine(sourceCheckout, "src"));
        File.WriteAllText(Path.Combine(sourceCheckout, ".git", "config"), "[core]\nrepositoryformatversion = 0\n");
        File.WriteAllText(Path.Combine(sourceCheckout, SolutionPath.Replace('/', Path.DirectorySeparatorChar)), "solution");
        File.WriteAllText(Path.Combine(sourceCheckout, "src", "Program.cs"), "class LeaseProbe { }");

        var initial = await RunWorkerAsync(
            CreateArguments(new WorkerRequest
            {
                Role = "writer",
                CacheRoot = cacheRoot,
                CheckoutPath = sourceCheckout,
                StagingRoot = sourceRoot,
                ReadyPath = temp.GetPath("initial-ready"),
                ContinuePath = temp.GetPath("initial-continue"),
                CancelPath = temp.GetPath("initial-cancel"),
                ResultPath = temp.GetPath("initial-result"),
                PublishCount = 1,
            }));
        AssertWorkerSucceeded(initial);

        var ready = temp.GetPath("reader-ready");
        var continuation = temp.GetPath("reader-continue");
        var cancellation = temp.GetPath("reader-cancel");
        var readerResult = temp.GetPath("reader-result");
        await using var reader = StartWorker(
            CreateArguments(new WorkerRequest
            {
                Role = "reader",
                CacheRoot = cacheRoot,
                CheckoutPath = temp.GetPath("unused-checkout"),
                StagingRoot = readerRoot,
                ReadyPath = ready,
                ContinuePath = continuation,
                CancelPath = cancellation,
                ResultPath = readerResult,
            }));

        await WaitForFileAsync(ready);
        var heldGeneration = File.ReadAllText(ready);
        var publishing = await RunWorkerAsync(
            CreateArguments(new WorkerRequest
            {
                Role = "writer",
                CacheRoot = cacheRoot,
                CheckoutPath = sourceCheckout,
                StagingRoot = sourceRoot,
                ReadyPath = temp.GetPath("writer-ready"),
                ContinuePath = temp.GetPath("writer-continue"),
                CancelPath = temp.GetPath("writer-cancel"),
                ResultPath = temp.GetPath("writer-result"),
                PublishCount = 2,
            }));
        AssertWorkerSucceeded(publishing);

        var entryDirectory = Path.Combine(cacheRoot, ExternalSourceRepositoryCacheContract.CreateStableValue(
            ExternalSourceRepositoryCacheContract.CacheSchemaVersion,
            RepositoryUrl,
            SolutionPath));
        var duringMaterialization = Directory.EnumerateDirectories(
                entryDirectory,
                ExternalSourceRepositoryCacheContract.GenerationDirectoryPrefix + "*",
                SearchOption.TopDirectoryOnly)
            .ToArray();
        Assert.Contains(duringMaterialization, path =>
            string.Equals(Path.GetFileName(path), heldGeneration, StringComparison.Ordinal));

        File.WriteAllText(continuation, "continue");
        var readerCompletion = await reader.WaitForExitAsync(TimeSpan.FromSeconds(30));
        AssertWorkerSucceeded(readerCompletion);
        Assert.Equal($"materialized:{heldGeneration}", File.ReadAllText(readerResult));

        var afterRelease = await RunWorkerAsync(
            CreateArguments(new WorkerRequest
            {
                Role = "writer",
                CacheRoot = cacheRoot,
                CheckoutPath = sourceCheckout,
                StagingRoot = sourceRoot,
                ReadyPath = temp.GetPath("release-ready"),
                ContinuePath = temp.GetPath("release-continue"),
                CancelPath = temp.GetPath("release-cancel"),
                ResultPath = temp.GetPath("release-result"),
                PublishCount = 1,
            }));
        AssertWorkerSucceeded(afterRelease);
        Assert.False(Directory.Exists(Path.Combine(entryDirectory, heldGeneration)));
        Assert.False(File.Exists(Path.Combine(
            entryDirectory,
            heldGeneration + ".reader.lock")));

        await RunCancellationScenarioAsync(
            temp,
            cacheRoot,
            sourceCheckout,
            sourceRoot,
            readerRoot,
            entryDirectory);
    }

    private static async Task RunCancellationScenarioAsync(
        TestTempDirectory temp,
        string cacheRoot,
        string sourceCheckout,
        string sourceRoot,
        string readerRoot,
        string entryDirectory)
    {
        var cancelReady = temp.GetPath("cancel-reader-ready");
        var cancelSignal = temp.GetPath("cancel-reader-continue");
        var cancelMarker = temp.GetPath("cancel-reader-cancel");
        var cancelResult = temp.GetPath("cancel-reader-result");
        await using var cancelledReader = StartWorker(
            CreateArguments(new WorkerRequest
            {
                Role = "reader",
                CacheRoot = cacheRoot,
                CheckoutPath = temp.GetPath("unused-cancel-checkout"),
                StagingRoot = readerRoot,
                ReadyPath = cancelReady,
                ContinuePath = cancelSignal,
                CancelPath = cancelMarker,
                ResultPath = cancelResult,
            }));
        await WaitForFileAsync(cancelReady);
        var cancelledGeneration = File.ReadAllText(cancelReady);

        var retentionWhileCancelled = await RunWorkerAsync(
            CreateArguments(new WorkerRequest
            {
                Role = "writer",
                CacheRoot = cacheRoot,
                CheckoutPath = sourceCheckout,
                StagingRoot = sourceRoot,
                ReadyPath = temp.GetPath("cancel-writer-ready"),
                ContinuePath = temp.GetPath("cancel-writer-continue"),
                CancelPath = temp.GetPath("cancel-writer-cancel"),
                ResultPath = temp.GetPath("cancel-writer-result"),
                PublishCount = 2,
            }));
        AssertWorkerSucceeded(retentionWhileCancelled);
        Assert.True(Directory.Exists(Path.Combine(entryDirectory, cancelledGeneration)));

        File.WriteAllText(cancelMarker, "cancel");
        var cancelledCompletion = await cancelledReader.WaitForExitAsync(TimeSpan.FromSeconds(30));
        AssertWorkerSucceeded(cancelledCompletion);
        Assert.Equal("cancelled", File.ReadAllText(cancelResult));

        var cleanupAfterCancellation = await RunWorkerAsync(
            CreateArguments(new WorkerRequest
            {
                Role = "writer",
                CacheRoot = cacheRoot,
                CheckoutPath = sourceCheckout,
                StagingRoot = sourceRoot,
                ReadyPath = temp.GetPath("cancel-release-ready"),
                ContinuePath = temp.GetPath("cancel-release-continue"),
                CancelPath = temp.GetPath("cancel-release-cancel"),
                ResultPath = temp.GetPath("cancel-release-result"),
                PublishCount = 1,
            }));
        AssertWorkerSucceeded(cleanupAfterCancellation);
        Assert.False(Directory.Exists(Path.Combine(entryDirectory, cancelledGeneration)));
        Assert.False(File.Exists(Path.Combine(
            entryDirectory,
            cancelledGeneration + ".reader.lock")));
    }

    private static string[] CreateArguments(WorkerRequest request) =>
        [
            ExternalSourceCacheLeaseProbeCommand.Option,
            request.Role,
            request.CacheRoot,
            request.CheckoutPath,
            request.StagingRoot,
            RepositoryUrl,
            SolutionPath,
            Revision,
            OwnershipToken,
            request.ReadyPath,
            request.ContinuePath,
            request.CancelPath,
            request.ResultPath,
            request.PublishCount.ToString(),
        ];

    private static ProcessHandle StartWorker(string[] arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = Path.Combine(AppContext.BaseDirectory, "AiNetLinter.exe"),
            WorkingDirectory = AppContext.BaseDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        var process = new Process { StartInfo = startInfo };
        Assert.True(process.Start());
        return new(process);
    }

    private static async Task<WorkerResult> RunWorkerAsync(string[] arguments)
    {
        await using var process = StartWorker(arguments);
        return await process.WaitForExitAsync(TimeSpan.FromSeconds(30));
    }

    private static async Task WaitForFileAsync(string path)
    {
        await TestWaiter.WaitForConditionAsync(
            () => File.Exists(path),
            TimeSpan.FromSeconds(30));
    }

    private static void AssertWorkerSucceeded(WorkerResult result)
    {
        Assert.False(result.TimedOut, result.Error);
        Assert.True(result.ExitCode == 0, result.Error);
    }

    private sealed class ProcessHandle : IAsyncDisposable
    {
        private readonly Process process;
        private readonly Task<string> output;
        private readonly Task<string> error;
        private int disposed;

        internal ProcessHandle(Process process)
        {
            this.process = process;
            output = process.StandardOutput.ReadToEndAsync();
            error = process.StandardError.ReadToEndAsync();
        }

        internal async Task<WorkerResult> WaitForExitAsync(TimeSpan timeout)
        {
            var exit = process.WaitForExitAsync();
            var completed = await Task.WhenAny(exit, Task.Delay(timeout));
            if (completed != exit)
            {
                if (!process.HasExited) process.Kill(entireProcessTree: true);
                await exit;
                return await CreateResultAsync(timedOut: true);
            }

            return await CreateResultAsync(timedOut: false);
        }

        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref disposed, 1) != 0) return;
            try
            {
                if (!process.HasExited) process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync();
                await Task.WhenAll(output, error);
            }
            finally
            {
                process.Dispose();
            }
        }

        private async Task<WorkerResult> CreateResultAsync(bool timedOut)
        {
            await Task.WhenAll(output, error);
            return new(
                process.ExitCode,
                await output,
                await error,
                timedOut);
        }
    }

    private sealed record WorkerResult(
        int ExitCode,
        string Output,
        string Error,
        bool TimedOut);

    private sealed class WorkerRequest
    {
        internal string Role { get; init; } = string.Empty;
        internal string CacheRoot { get; init; } = string.Empty;
        internal string CheckoutPath { get; init; } = string.Empty;
        internal string StagingRoot { get; init; } = string.Empty;
        internal string ReadyPath { get; init; } = string.Empty;
        internal string ContinuePath { get; init; } = string.Empty;
        internal string CancelPath { get; init; } = string.Empty;
        internal string ResultPath { get; init; } = string.Empty;
        internal int PublishCount { get; init; }
    }
}
