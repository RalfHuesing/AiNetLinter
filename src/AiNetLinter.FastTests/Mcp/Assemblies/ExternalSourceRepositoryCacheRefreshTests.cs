#nullable enable

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Configuration;
using AiNetLinter.Mcp.Assemblies;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Assemblies;

[Trait("Category", "Component")]
public sealed class ExternalSourceRepositoryCacheRefreshTests
{
    [Fact]
    public void Policy_UsesSixtyMinuteUtcBoundaryAndFailsClosedForUnsafeTimes()
    {
        var now = new DateTimeOffset(2026, 8, 29, 12, 0, 0, TimeSpan.Zero);
        var policy = new ExternalSourceRepositoryCacheRefreshPolicy(
            new FixedTimeProvider(now));

        Assert.Equal(TimeSpan.FromMinutes(60), policy.RefreshInterval);
        Assert.False(policy.IsStale(CreateManifest(now.UtcDateTime.AddMinutes(-59))));
        Assert.True(policy.IsStale(CreateManifest(now.UtcDateTime.AddMinutes(-60))));
        Assert.True(policy.IsStale(CreateManifest(now.UtcDateTime.AddMinutes(1))));
        Assert.True(policy.IsStale(CreateManifest(
            DateTime.SpecifyKind(now.UtcDateTime.AddMinutes(-1), DateTimeKind.Unspecified))));
    }

    [Fact]
    public async Task FreshCurrent_ReusesCheckoutWithoutFetchOrPublish()
    {
        using var source = SourceFixture.Create(ExternalSourceRepositoryCacheTestData.Revision);
        using var cacheRoot = TestTempDirectory.Create("external-source-refresh-fresh-cache-");
        using var stagingRoot = TestTempDirectory.Create("external-source-refresh-fresh-staging-");
        var localWriter = new LocalExternalSourceRepositoryCacheWriter(cacheRoot.DirectoryPath);
        var initial = await localWriter.PublishAsync(source.Request);
        Assert.True(initial.Succeeded);
        var currentGeneration = ExternalSourceRepositoryCacheTestAssertions.ReadCurrentGenerationName(
            localWriter,
            source.Key);
        var recordingWriter = new RecordingCacheWriter();
        var transport = CreateUnexpectedTransport();
        var acquirer = new ExternalSourceRepositoryAcquirer(
            transport,
            stagingRoot.DirectoryPath,
            cacheWriter: recordingWriter,
            cacheReader: localWriter,
            refreshPolicy: new ExternalSourceRepositoryCacheRefreshPolicy(
                new FixedTimeProvider(DateTimeOffset.UtcNow)));

        var result = await acquirer.AcquireAsync(ExternalSourceRepositoryCacheTestData.CreateMapping());

        Assert.True(result.IsAvailable);
        Assert.Equal(ExternalSourceRepositoryCacheTestData.Revision, result.LoadedRevision);
        Assert.Equal(0, transport.CallCount);
        Assert.Equal(0, transport.FetchCallCount);
        Assert.Null(recordingWriter.Request);
        Assert.Equal(currentGeneration, ExternalSourceRepositoryCacheTestAssertions.ReadCurrentGenerationName(
            localWriter,
            source.Key));
        ExternalSourceRepositoryCacheTestAssertions.AssertRequestOwnedCheckout(
            result.Checkout!,
            initial.GenerationPath);
        result.Checkout!.Dispose();
    }

    [Fact]
    public async Task StaleCurrent_FetchesMaterializedCheckoutAndPublishesNewGeneration()
    {
        using var source = SourceFixture.Create(ExternalSourceRepositoryCacheTestData.Revision);
        using var cacheRoot = TestTempDirectory.Create("external-source-refresh-stale-cache-");
        using var stagingRoot = TestTempDirectory.Create("external-source-refresh-stale-staging-");
        var writer = new LocalExternalSourceRepositoryCacheWriter(cacheRoot.DirectoryPath);
        var initial = await writer.PublishAsync(source.Request);
        Assert.True(initial.Succeeded);
        var oldGeneration = ExternalSourceRepositoryCacheTestAssertions.ReadCurrentGenerationName(
            writer,
            source.Key);
        var now = DateTimeOffset.UtcNow.AddHours(2);
        var transport = new ExternalSourceRecordingTransport(
            (_, _, _) => throw new InvalidOperationException("Clone darf beim Refresh nicht aufgerufen werden."),
            (_, destination, _) =>
            {
                File.WriteAllText(
                    Path.Combine(destination, "src", "Program.cs"),
                    "class Program { static int Refreshed => 1; }");
                return ExternalSourceRepositoryTestTransportResults.Success(destination, ExternalSourceRepositoryCacheTestData.OtherRevision);
            });
        var acquirer = new ExternalSourceRepositoryAcquirer(
            transport,
            stagingRoot.DirectoryPath,
            cacheWriter: writer,
            cacheReader: writer,
            refreshPolicy: new ExternalSourceRepositoryCacheRefreshPolicy(
                new FixedTimeProvider(now)));

        var result = await acquirer.AcquireAsync(ExternalSourceRepositoryCacheTestData.CreateMapping());

        Assert.True(result.IsAvailable);
        Assert.Equal(ExternalSourceRepositoryCacheTestData.OtherRevision, result.LoadedRevision);
        Assert.Equal(0, transport.CallCount);
        Assert.Equal(1, transport.FetchCallCount);
        var current = ExternalSourceRepositoryCacheTestAssertions.ReadCurrent(writer, source.Key)!;
        Assert.NotEqual(oldGeneration, current.Manifest.GenerationName);
        Assert.Equal(ExternalSourceRepositoryCacheTestData.OtherRevision, current.Manifest.LoadedRevision);
        Assert.Equal(2, ExternalSourceRepositoryCacheReadBackTestSupport.CountGenerations(
            Path.GetDirectoryName(current.GenerationPath)!));
        Assert.Equal(
            "solution",
            File.ReadAllText(Path.Combine(
                initial.GenerationPath!,
                ExternalSourceRepositoryCacheContract.ContentDirectoryName,
                "src",
                "BaselineMini.slnx")));
        Assert.Equal(
            "class Program { }",
            File.ReadAllText(Path.Combine(
                initial.GenerationPath!,
                ExternalSourceRepositoryCacheContract.ContentDirectoryName,
                "src",
                "Program.cs")));
        ExternalSourceRepositoryCacheTestAssertions.AssertRequestOwnedCheckout(
            result.Checkout!,
            current.GenerationPath);
        result.Checkout!.Dispose();
        Assert.Empty(Directory.EnumerateDirectories(stagingRoot.DirectoryPath, "checkout-*"));
    }

    [Fact]
    public async Task StaleCurrent_FetchFailurePreservesCurrentAndCleansCheckout()
    {
        using var source = SourceFixture.Create(ExternalSourceRepositoryCacheTestData.Revision);
        using var cacheRoot = TestTempDirectory.Create("external-source-refresh-failure-cache-");
        using var stagingRoot = TestTempDirectory.Create("external-source-refresh-failure-staging-");
        var writer = new LocalExternalSourceRepositoryCacheWriter(cacheRoot.DirectoryPath);
        var initial = await writer.PublishAsync(source.Request);
        Assert.True(initial.Succeeded);
        var oldGeneration = ExternalSourceRepositoryCacheTestAssertions.ReadCurrentGenerationName(
            writer,
            source.Key);
        var transport = new ExternalSourceRecordingTransport(
            (_, _, _) => throw new InvalidOperationException("Clone darf beim Refresh nicht aufgerufen werden."),
            (_, _, _) => new ExternalSourceRepositoryTransportResult(
                isAvailable: false,
                loadedRevision: null,
                diagnostics: [ExternalSourceConfigurationDiagnostic.CreateError(
                    ExternalSourceConfigurationDiagnosticCodes.NetworkUnavailable,
                    "Netzwerkfehler", "Test", "$repository")],
                state: ExternalSourceRepositoryResultState.Create(
                    ExternalSourceProviderFailureKind.NetworkUnavailable)));
        var acquirer = CreateStaleAcquirer(
            transport,
            writer,
            stagingRoot,
            DateTimeOffset.UtcNow.AddHours(2));

        var result = await acquirer.AcquireAsync(ExternalSourceRepositoryCacheTestData.CreateMapping());

        Assert.False(result.IsAvailable);
        Assert.Equal(ExternalSourceProviderFailureKind.NetworkUnavailable, result.FailureKind);
        Assert.Equal(ExternalSourceRepositoryHealth.Degraded, result.Health);
        Assert.Equal(ExternalSourceRepositoryCacheTestData.Revision, result.LastGoodRevision);
        Assert.Contains(
            result.Diagnostics,
            diagnostic => diagnostic.Code == ExternalSourceConfigurationDiagnosticCodes.RepositoryRefreshDegraded);
        Assert.Equal(oldGeneration, ExternalSourceRepositoryCacheTestAssertions.ReadCurrentGenerationName(
            writer,
            source.Key));
        Assert.Equal(1, ExternalSourceRepositoryCacheReadBackTestSupport.CountGenerations(
            Path.GetDirectoryName(initial.GenerationPath)!));
        Assert.Empty(Directory.EnumerateDirectories(stagingRoot.DirectoryPath, "checkout-*"));
    }

    [Fact]
    public async Task StaleCurrent_PublishFailurePreservesCurrentAndCleansCheckout()
    {
        using var source = SourceFixture.Create(ExternalSourceRepositoryCacheTestData.Revision);
        using var cacheRoot = TestTempDirectory.Create("external-source-refresh-publish-cache-");
        using var stagingRoot = TestTempDirectory.Create("external-source-refresh-publish-staging-");
        var localWriter = new LocalExternalSourceRepositoryCacheWriter(cacheRoot.DirectoryPath);
        var initial = await localWriter.PublishAsync(source.Request);
        Assert.True(initial.Succeeded);
        var oldGeneration = ExternalSourceRepositoryCacheTestAssertions.ReadCurrentGenerationName(
            localWriter,
            source.Key);
        var recordingWriter = new RecordingCacheWriter { ReturnFailure = true };
        var transport = new ExternalSourceRecordingTransport(
            (_, _, _) => throw new InvalidOperationException("Clone darf beim Refresh nicht aufgerufen werden."),
            (_, destination, _) => ExternalSourceRepositoryTestTransportResults.Success(destination, ExternalSourceRepositoryCacheTestData.OtherRevision));
        var acquirer = CreateStaleAcquirer(
            transport,
            recordingWriter,
            stagingRoot,
            DateTimeOffset.UtcNow.AddHours(2),
            localWriter);

        var result = await acquirer.AcquireAsync(ExternalSourceRepositoryCacheTestData.CreateMapping());

        Assert.False(result.IsAvailable);
        Assert.Equal(ExternalSourceProviderFailureKind.InvalidResponse, result.FailureKind);
        Assert.Equal(ExternalSourceRepositoryHealth.Degraded, result.Health);
        Assert.Equal(ExternalSourceRepositoryCacheTestData.Revision, result.LastGoodRevision);
        Assert.Contains(
            result.Diagnostics,
            diagnostic => diagnostic.Code == ExternalSourceConfigurationDiagnosticCodes.RepositoryRefreshDegraded);
        Assert.Equal(
            ExternalSourceRepositoryCacheTestData.OtherRevision,
            recordingWriter.Request!.LoadedRevision);
        Assert.Equal(oldGeneration, recordingWriter.Request.ExpectedCurrentGeneration);
        Assert.Equal(oldGeneration, ExternalSourceRepositoryCacheTestAssertions.ReadCurrentGenerationName(
            localWriter,
            source.Key));
        Assert.Equal(1, ExternalSourceRepositoryCacheReadBackTestSupport.CountGenerations(
            Path.GetDirectoryName(initial.GenerationPath)!));
        Assert.Empty(Directory.EnumerateDirectories(stagingRoot.DirectoryPath, "checkout-*"));
    }

    [Fact]
    public async Task StaleCurrent_IntegrityFailurePreservesCurrentAndCleansCheckout()
    {
        using var source = SourceFixture.Create(ExternalSourceRepositoryCacheTestData.Revision);
        using var cacheRoot = TestTempDirectory.Create("external-source-refresh-integrity-cache-");
        using var stagingRoot = TestTempDirectory.Create("external-source-refresh-integrity-staging-");
        var writer = new LocalExternalSourceRepositoryCacheWriter(cacheRoot.DirectoryPath);
        var initial = await writer.PublishAsync(source.Request);
        Assert.True(initial.Succeeded);
        var oldGeneration = ExternalSourceRepositoryCacheTestAssertions.ReadCurrentGenerationName(
            writer,
            source.Key);
        var transport = new ExternalSourceRecordingTransport(
            (_, _, _) => throw new InvalidOperationException("Clone darf beim Refresh nicht aufgerufen werden."),
            (_, destination, _) =>
            {
                File.Delete(Path.Combine(destination, "src", "BaselineMini.slnx"));
                return ExternalSourceRepositoryTestTransportResults.Success(destination, ExternalSourceRepositoryCacheTestData.OtherRevision);
            });
        var acquirer = CreateStaleAcquirer(
            transport,
            writer,
            stagingRoot,
            DateTimeOffset.UtcNow.AddHours(2));

        var result = await acquirer.AcquireAsync(ExternalSourceRepositoryCacheTestData.CreateMapping());

        Assert.False(result.IsAvailable);
        Assert.Equal(ExternalSourceProviderFailureKind.InvalidResponse, result.FailureKind);
        Assert.Equal(ExternalSourceRepositoryHealth.Degraded, result.Health);
        Assert.Equal(ExternalSourceRepositoryCacheTestData.Revision, result.LastGoodRevision);
        Assert.Contains(
            result.Diagnostics,
            diagnostic => diagnostic.Code == ExternalSourceConfigurationDiagnosticCodes.RepositoryRefreshDegraded);
        Assert.Equal(oldGeneration, ExternalSourceRepositoryCacheTestAssertions.ReadCurrentGenerationName(
            writer,
            source.Key));
        Assert.Equal(1, ExternalSourceRepositoryCacheReadBackTestSupport.CountGenerations(
            Path.GetDirectoryName(initial.GenerationPath)!));
        Assert.Empty(Directory.EnumerateDirectories(stagingRoot.DirectoryPath, "checkout-*"));
    }

    [Fact]
    public async Task StaleCurrent_CancellationPreservesCurrentAndCleansCheckout()
    {
        using var source = SourceFixture.Create(ExternalSourceRepositoryCacheTestData.Revision);
        using var cacheRoot = TestTempDirectory.Create("external-source-refresh-cancel-cache-");
        using var stagingRoot = TestTempDirectory.Create("external-source-refresh-cancel-staging-");
        var writer = new LocalExternalSourceRepositoryCacheWriter(cacheRoot.DirectoryPath);
        var initial = await writer.PublishAsync(source.Request);
        Assert.True(initial.Succeeded);
        var oldGeneration = ExternalSourceRepositoryCacheTestAssertions.ReadCurrentGenerationName(
            writer,
            source.Key);
        using var cancellation = new CancellationTokenSource();
        var transport = new CancellingRefreshTransport();
        var acquirer = CreateStaleAcquirer(
            transport,
            writer,
            stagingRoot,
            DateTimeOffset.UtcNow.AddHours(2));
        var operation = acquirer.AcquireAsync(
            ExternalSourceRepositoryCacheTestData.CreateMapping(),
            cancellation.Token);
        await transport.FetchStarted.Task.WaitAsync(TimeSpan.FromSeconds(15));
        cancellation.Cancel();

        var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => operation);

        Assert.Equal(cancellation.Token, exception.CancellationToken);
        Assert.Equal(1, transport.FetchCallCount);
        Assert.Equal(oldGeneration, ExternalSourceRepositoryCacheTestAssertions.ReadCurrentGenerationName(
            writer,
            source.Key));
        Assert.Equal(1, ExternalSourceRepositoryCacheReadBackTestSupport.CountGenerations(
            Path.GetDirectoryName(initial.GenerationPath)!));
        Assert.Empty(Directory.EnumerateDirectories(stagingRoot.DirectoryPath, "checkout-*"));
    }

    [Fact]
    public async Task StaleCurrent_CurrentRaceCannotOverwriteNewerGenerationOrFetchAgain()
    {
        using var source = SourceFixture.Create(ExternalSourceRepositoryCacheTestData.Revision);
        using var cacheRoot = TestTempDirectory.Create("external-source-refresh-race-cache-");
        using var stagingRoot = TestTempDirectory.Create("external-source-refresh-race-staging-");
        var writer = new LocalExternalSourceRepositoryCacheWriter(cacheRoot.DirectoryPath);
        var initial = await writer.PublishAsync(source.Request);
        Assert.True(initial.Succeeded);
        var oldGeneration = ExternalSourceRepositoryCacheTestAssertions.ReadCurrentGenerationName(
            writer,
            source.Key);
        var transport = new RaceRefreshTransport(writer);
        var acquirer = CreateStaleAcquirer(
            transport,
            writer,
            stagingRoot,
            DateTimeOffset.UtcNow.AddHours(2));

        var result = await acquirer.AcquireAsync(ExternalSourceRepositoryCacheTestData.CreateMapping());

        Assert.False(result.IsAvailable);
        Assert.Equal(ExternalSourceProviderFailureKind.InvalidResponse, result.FailureKind);
        Assert.Equal(ExternalSourceRepositoryHealth.Degraded, result.Health);
        Assert.Equal(ExternalSourceRepositoryCacheTestData.Revision, result.LastGoodRevision);
        Assert.Contains(
            result.Diagnostics,
            diagnostic => diagnostic.Code == ExternalSourceConfigurationDiagnosticCodes.RepositoryRefreshDegraded);
        Assert.Contains(
            result.Diagnostics,
            diagnostic => diagnostic.Code == ExternalSourceRepositoryCacheContract.CurrentChangedDiagnosticCode);
        Assert.Equal(1, transport.FetchCallCount);
        Assert.Equal(ExternalSourceRepositoryCacheTestData.OtherRevision, transport.RaceRevision);
        var current = ExternalSourceRepositoryCacheTestAssertions.ReadCurrent(writer, source.Key)!;
        Assert.NotEqual(oldGeneration, current.Manifest.GenerationName);
        Assert.Equal(ExternalSourceRepositoryCacheTestData.OtherRevision, current.Manifest.LoadedRevision);
        Assert.Equal(2, ExternalSourceRepositoryCacheReadBackTestSupport.CountGenerations(
            Path.GetDirectoryName(initial.GenerationPath)!));
        Assert.Empty(Directory.EnumerateDirectories(stagingRoot.DirectoryPath, "checkout-*"));
    }

    [Fact]
    public async Task StaleCurrent_CurrentChangedRaceReusesFreshGeneration()
    {
        using var source = SourceFixture.Create(ExternalSourceRepositoryCacheTestData.Revision);
        using var cacheRoot = TestTempDirectory.Create("external-source-refresh-race-reuse-cache-");
        using var stagingRoot = TestTempDirectory.Create("external-source-refresh-race-reuse-staging-");
        var writer = new LocalExternalSourceRepositoryCacheWriter(cacheRoot.DirectoryPath);
        var initial = await writer.PublishAsync(source.Request);
        Assert.True(initial.Succeeded);
        var transport = new RaceRefreshTransport(writer);
        var timeProvider = new SequenceTimeProvider(
            DateTimeOffset.UtcNow.AddHours(2),
            DateTimeOffset.UtcNow.AddMinutes(1));
        var acquirer = CreateStaleAcquirer(
            transport,
            writer,
            stagingRoot,
            DateTimeOffset.UtcNow,
            writer,
            timeProvider);

        var result = await acquirer.AcquireAsync(ExternalSourceRepositoryCacheTestData.CreateMapping());

        Assert.True(result.IsAvailable);
        Assert.Equal(ExternalSourceRepositoryCacheTestData.OtherRevision, result.LoadedRevision);
        Assert.Equal(ExternalSourceRepositoryHealth.Verified, result.Health);
        Assert.Equal(1, transport.FetchCallCount);
        var current = ExternalSourceRepositoryCacheTestAssertions.ReadCurrent(writer, source.Key)!;
        Assert.Equal(2, ExternalSourceRepositoryCacheReadBackTestSupport.CountGenerations(
            Path.GetDirectoryName(current.GenerationPath)!));
        result.Checkout!.Dispose();
        Assert.Empty(Directory.EnumerateDirectories(stagingRoot.DirectoryPath, "checkout-*"));
    }

    private static ExternalSourceRecordingTransport CreateUnexpectedTransport() =>
        new(
            (_, _, _) => throw new InvalidOperationException("Der Transport darf beim Current-Reuse nicht aufgerufen werden."));

    private static ExternalSourceRepositoryAcquirer CreateStaleAcquirer(
        IGiteaRepositoryTransport transport,
        IExternalSourceRepositoryCacheWriter cacheWriter,
        TestTempDirectory stagingRoot,
        DateTimeOffset now,
        IExternalSourceRepositoryCacheReader? cacheReader = null,
        TimeProvider? timeProvider = null) =>
        new(
            transport,
            stagingRoot.DirectoryPath,
            cacheWriter: cacheWriter,
            cacheReader: cacheReader ?? cacheWriter as IExternalSourceRepositoryCacheReader,
            refreshPolicy: new ExternalSourceRepositoryCacheRefreshPolicy(
                timeProvider ?? new FixedTimeProvider(now)));

    private static ExternalSourceRepositoryCacheManifest CreateManifest(DateTime createdUtc) =>
        new(
            ExternalSourceRepositoryCacheContract.CacheSchemaVersion,
            new string('a', 64),
            ExternalSourceRepositoryCacheTestData.RepositoryUrl,
            ExternalSourceRepositoryCacheTestData.SolutionPath,
            ExternalSourceRepositoryCacheTestData.Revision,
            "generation-00000000000000000000000000000000",
            createdUtc,
            []);

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset utcNow;

        internal FixedTimeProvider(DateTimeOffset utcNow)
        {
            this.utcNow = utcNow;
        }

        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed class SequenceTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset[] values;
        private int index;

        internal SequenceTimeProvider(params DateTimeOffset[] values)
        {
            this.values = values;
        }

        public override DateTimeOffset GetUtcNow()
        {
            var currentIndex = Interlocked.Increment(ref index) - 1;
            return values[Math.Min(currentIndex, values.Length - 1)];
        }
    }

    private sealed class CancellingRefreshTransport : IGiteaRepositoryTransport
    {
        internal TaskCompletionSource<bool> FetchStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal int FetchCallCount { get; private set; }

        public ValueTask<ExternalSourceRepositoryTransportResult> CloneDefaultBranchAsync(
            ExternalSourceMapping mapping,
            string destinationPath,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Clone darf beim Refresh nicht aufgerufen werden.");

        public async ValueTask<ExternalSourceRepositoryTransportResult> FetchDefaultBranchAsync(
            ExternalSourceMapping mapping,
            string destinationPath,
            CancellationToken cancellationToken = default)
        {
            FetchCallCount++;
            FetchStarted.TrySetResult(true);
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return ExternalSourceRepositoryTestTransportResults.Success(
                destinationPath,
                ExternalSourceRepositoryCacheTestData.OtherRevision);
        }
    }

    private sealed class RaceRefreshTransport : IGiteaRepositoryTransport
    {
        private readonly LocalExternalSourceRepositoryCacheWriter writer;

        internal RaceRefreshTransport(LocalExternalSourceRepositoryCacheWriter writer)
        {
            this.writer = writer;
        }

        internal int FetchCallCount { get; private set; }

        internal string? RaceRevision { get; private set; }

        public ValueTask<ExternalSourceRepositoryTransportResult> CloneDefaultBranchAsync(
            ExternalSourceMapping mapping,
            string destinationPath,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Clone darf beim Refresh nicht aufgerufen werden.");

        public async ValueTask<ExternalSourceRepositoryTransportResult> FetchDefaultBranchAsync(
            ExternalSourceMapping mapping,
            string destinationPath,
            CancellationToken cancellationToken = default)
        {
            FetchCallCount++;
            using var competingSource = SourceFixture.Create(
                ExternalSourceRepositoryCacheTestData.OtherRevision);
            var publish = await writer.PublishAsync(competingSource.Request, cancellationToken);
            Assert.True(publish.Succeeded);
            RaceRevision = competingSource.Request.LoadedRevision;
            return ExternalSourceRepositoryTestTransportResults.Success(
                destinationPath,
                ExternalSourceRepositoryCacheTestData.OtherRevision);
        }
    }
}
