#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace AiNetLinter.IntegrationTests.Platform;

[Trait("Category", "Integration")]
public sealed class LoadedFixtureTests
{
    [Fact]
    public async Task ExecuteWithinLoadBudgetAsync_AllowsExactlyTwoDelegatesBeforeRelease()
    {
        var entered = 0;
        var active = 0;
        var maximum = 0;
        var firstTwoEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        async Task<int> EnterAsync(CancellationToken cancellationToken)
        {
            var current = Interlocked.Increment(ref active);
            RecordMaximum(ref maximum, current);
            if (Interlocked.Increment(ref entered) == LoadedFixture.MaxConcurrentLoads)
            {
                firstTwoEntered.TrySetResult();
            }

            await release.Task.WaitAsync(cancellationToken);
            Interlocked.Decrement(ref active);
            return current;
        }

        var gate = new LoadBudgetGate(LoadedFixture.MaxConcurrentLoads);
        var tasks = new Task<int>[]
        {
            gate.ExecuteAsync(EnterAsync),
            gate.ExecuteAsync(EnterAsync),
        };

        try
        {
            await firstTwoEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var third = gate.ExecuteAsync(EnterAsync);
            tasks = [.. tasks, third];

            Assert.Equal(LoadedFixture.MaxConcurrentLoads, Volatile.Read(ref entered));
            Assert.False(third.IsCompleted);
        }
        finally
        {
            release.TrySetResult();
            await Task.WhenAll(tasks);
        }

        Assert.Equal(LoadedFixture.MaxConcurrentLoads, Volatile.Read(ref maximum));
    }

    [Fact]
    public async Task ExecuteWithinLoadBudgetAsync_ReleasesPermitAfterException()
    {
        var gate = new LoadBudgetGate(LoadedFixture.MaxConcurrentLoads);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            gate.ExecuteAsync<int>(_ => Task.FromException<int>(new InvalidOperationException())));

        await AssertTwoDelegatesCanEnterAsync(gate);
    }

    [Fact]
    public async Task ExecuteWithinLoadBudgetAsync_ReleasesPermitAfterCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        var gate = new LoadBudgetGate(LoadedFixture.MaxConcurrentLoads);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            gate.ExecuteAsync<int>(_ =>
            {
                cancellation.Cancel();
                return Task.FromCanceled<int>(cancellation.Token);
            }));

        await AssertTwoDelegatesCanEnterAsync(gate);
    }

    [Fact]
    public void SourceFileCatalogLoads_UseLoadedFixtureAsOnlyIntegrationTestCallsite()
    {
        var rootPath = SolutionRootLocator.Find();
        var integrationTestPath = Path.Combine(rootPath, "src", "AiNetLinter.IntegrationTests");
        const string LoadCall = "SourceFileCatalog." + "LoadAsync(";

        var callers = Directory.EnumerateFiles(integrationTestPath, "*.cs", SearchOption.AllDirectories)
            .Where(path => File.ReadAllText(path).Contains(LoadCall, StringComparison.Ordinal))
            .Select(path => Path.GetRelativePath(integrationTestPath, path).Replace('\\', '/'))
            .ToArray();

        Assert.Equal(["Platform/LoadedFixture.cs"], callers);
    }

    [Fact]
    public void LoadedFixture_UsesSingleGateWithConfiguredCapacity() =>
        Assert.Equal(LoadedFixture.MaxConcurrentLoads, LoadedFixture.LoadBudgetCapacity);

    private static async Task AssertTwoDelegatesCanEnterAsync(LoadBudgetGate gate)
    {
        var entered = 0;
        var bothEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        async Task<int> EnterAsync(CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref entered) == LoadedFixture.MaxConcurrentLoads)
            {
                bothEntered.TrySetResult();
            }

            await release.Task.WaitAsync(cancellationToken);
            return 0;
        }

        var tasks = new[] { gate.ExecuteAsync(EnterAsync), gate.ExecuteAsync(EnterAsync) };
        try
        {
            await bothEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }
        finally
        {
            release.TrySetResult();
            await Task.WhenAll(tasks);
        }
    }

    private static void RecordMaximum(ref int maximum, int candidate)
    {
        var observed = Volatile.Read(ref maximum);
        while (candidate > observed)
        {
            var replacement = Interlocked.CompareExchange(ref maximum, candidate, observed);
            if (replacement == observed)
            {
                return;
            }

            observed = replacement;
        }
    }
}
