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

        var first = LoadedFixture.ExecuteWithinLoadBudgetAsync(EnterAsync);
        var second = LoadedFixture.ExecuteWithinLoadBudgetAsync(EnterAsync);
        await firstTwoEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var third = LoadedFixture.ExecuteWithinLoadBudgetAsync(EnterAsync);

        Assert.Equal(LoadedFixture.MaxConcurrentLoads, Volatile.Read(ref entered));
        Assert.False(third.IsCompleted);

        release.TrySetResult();
        await Task.WhenAll(first, second, third);

        Assert.Equal(LoadedFixture.MaxConcurrentLoads, Volatile.Read(ref maximum));
    }

    [Fact]
    public async Task ExecuteWithinLoadBudgetAsync_ReleasesPermitAfterException()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            LoadedFixture.ExecuteWithinLoadBudgetAsync<int>(_ => Task.FromException<int>(new InvalidOperationException())));

        await AssertTwoDelegatesCanEnterAsync();
    }

    [Fact]
    public async Task ExecuteWithinLoadBudgetAsync_ReleasesPermitAfterCancellation()
    {
        using var cancellation = new CancellationTokenSource();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            LoadedFixture.ExecuteWithinLoadBudgetAsync<int>(_ =>
            {
                cancellation.Cancel();
                return Task.FromCanceled<int>(cancellation.Token);
            }));

        await AssertTwoDelegatesCanEnterAsync();
    }

    [Fact]
    public void SourceFileCatalogLoads_UseLoadedFixtureAsOnlyIntegrationTestCallsite()
    {
        var rootPath = FindSolutionRoot();
        var integrationTestPath = Path.Combine(rootPath, "src", "AiNetLinter.IntegrationTests");
        const string LoadCall = "SourceFileCatalog." + "LoadAsync(";

        var callers = Directory.EnumerateFiles(integrationTestPath, "*.cs", SearchOption.AllDirectories)
            .Where(path => File.ReadAllText(path).Contains(LoadCall, StringComparison.Ordinal))
            .Select(path => Path.GetRelativePath(integrationTestPath, path).Replace('\\', '/'))
            .ToArray();

        Assert.Equal(["Platform/LoadedFixture.cs"], callers);
    }

    private static async Task AssertTwoDelegatesCanEnterAsync()
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

        var first = LoadedFixture.ExecuteWithinLoadBudgetAsync(EnterAsync);
        var second = LoadedFixture.ExecuteWithinLoadBudgetAsync(EnterAsync);
        await bothEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));

        release.TrySetResult();
        await Task.WhenAll(first, second);
    }

    private static string FindSolutionRoot()
    {
        var currentDirectory = new DirectoryInfo(AppContext.BaseDirectory);
        while (currentDirectory is not null)
        {
            if (File.Exists(Path.Combine(currentDirectory.FullName, "AiNetLinter.slnx")))
            {
                return currentDirectory.FullName;
            }

            currentDirectory = currentDirectory.Parent;
        }

        throw new DirectoryNotFoundException("Das Root-Verzeichnis mit der Projektmappe 'AiNetLinter.slnx' wurde nicht gefunden.");
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
