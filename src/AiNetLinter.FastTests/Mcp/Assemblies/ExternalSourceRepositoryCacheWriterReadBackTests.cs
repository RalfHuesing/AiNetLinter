#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp.Assemblies;
using AiNetLinter.TestKit;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Assemblies;

// @covers LocalExternalSourceRepositoryCacheWriter
public sealed partial class ExternalSourceRepositoryCacheWriterTests
{
    private static readonly TimeSpan TestSynchronizationTimeout = TimeSpan.FromSeconds(15);

    public static IEnumerable<object[]> BoundedMalformedInputs =>
    [
        new object[] { "current", "oversize" },
        new object[] { "current", "invalid-utf8" },
        new object[] { "current", "truncated" },
        new object[] { "current", "growth" },
        new object[] { "current", "unknown-field" },
        new object[] { "current", "duplicate-field" },
        new object[] { "manifest", "oversize" },
        new object[] { "manifest", "invalid-utf8" },
        new object[] { "manifest", "truncated" },
        new object[] { "manifest", "growth" },
        new object[] { "manifest", "unknown-field" },
        new object[] { "manifest", "duplicate-field" },
        new object[] { "manifest", "unknown-file-field" },
        new object[] { "manifest", "duplicate-file-field" },
        new object[] { "inventory", "oversize" },
        new object[] { "inventory", "invalid-utf8" },
        new object[] { "inventory", "truncated" },
        new object[] { "inventory", "growth" },
        new object[] { "inventory", "unknown-field" },
        new object[] { "inventory", "duplicate-field" },
        new object[] { "inventory", "unknown-file-field" },
        new object[] { "inventory", "duplicate-file-field" },
    ];

    public static IEnumerable<object[]> InventoryLimitInputs =>
    [
        new object[] { "entry-count" },
        new object[] { "declared-total-bytes" },
        new object[] { "cumulative-total-bytes" },
        new object[] { "file-length" },
        new object[] { "path-length" },
        new object[] { "file-count-mismatch" },
    ];

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task PublishAsync_CancellationAfterPointerPublishDoesNotRollbackConcurrentPublish(
        bool hasPreviousCurrent)
    {
        using var previousSource = SourceFixture.Create(Revision);
        using var failedSource = SourceFixture.Create(OtherRevision);
        using var successfulSource = SourceFixture.Create(Revision);
        using var cache = TestTempDirectory.Create("external-source-cache-cancel-race-");
        using var cancellation = new CancellationTokenSource();
        using var timeout = new CancellationTokenSource(TestSynchronizationTimeout);
        using var allowBPointer = new SemaphoreSlim(0, 1);
        var aPointerPublished = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var bPointerPublished = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        Task<ExternalSourceRepositoryCachePublishResult>? concurrentPublish = null;
        LocalExternalSourceRepositoryCacheWriter writer = null!;

        var bSeam = new ExternalSourceRepositoryCachePublishTestSeam
        {
            BeforePointerPublishedAsync = async () =>
            {
                await allowBPointer.WaitAsync(timeout.Token).ConfigureAwait(false);
            },
            AfterPointerPublishedAsync = () =>
            {
                bPointerPublished.TrySetResult(true);
                return Task.CompletedTask;
            },
        };
        var aSeam = new ExternalSourceRepositoryCachePublishTestSeam
        {
            AfterPointerPublishedAsync = () =>
            {
                cancellation.Cancel();
                concurrentPublish = writer.PublishAsync(
                    successfulSource.Request,
                    CancellationToken.None,
                    bSeam);
                Assert.True(aPointerPublished.TrySetResult(true));
                return Task.CompletedTask;
            },
            AfterLeaseReleasedAsync = async () =>
            {
                Assert.Equal(
                    hasPreviousCurrent ? 1 : 0,
                    ExternalSourceRepositoryCacheReadBackTestSupport.CountGenerations(
                        writer.GetEntryDirectory(successfulSource.Key)));
                allowBPointer.Release();
                await bPointerPublished.Task
                    .WaitAsync(timeout.Token)
                    .ConfigureAwait(false);
            },
        };

        if (hasPreviousCurrent)
        {
            var seedWriter = new LocalExternalSourceRepositoryCacheWriter(cache.DirectoryPath);
            var seed = await seedWriter.PublishAsync(previousSource.Request);
            Assert.True(seed.Succeeded);
        }

        writer = new LocalExternalSourceRepositoryCacheWriter(cache.DirectoryPath);
        var failedTask = writer.PublishAsync(
            failedSource.Request,
            cancellation.Token,
            aSeam);
        await aPointerPublished.Task.WaitAsync(timeout.Token);

        var failed = await failedTask.WaitAsync(timeout.Token);
        Assert.False(failed.Succeeded);
        Assert.Equal(
            ExternalSourceRepositoryCachePublishFailureKind.Cancelled,
            failed.FailureKind);
        var successful = await concurrentPublish!.WaitAsync(timeout.Token);
        Assert.True(successful.Succeeded);

        Assert.True(writer.TryReadCurrent(
            successfulSource.Key,
            out var current,
            out var diagnostic));
        Assert.Null(diagnostic);
        Assert.Equal(successful.GenerationName, current!.Manifest.GenerationName);
        Assert.Equal(Revision, current.Manifest.LoadedRevision);

        var generations = Directory.EnumerateDirectories(
                writer.GetEntryDirectory(successfulSource.Key),
                ExternalSourceRepositoryCacheContract.GenerationDirectoryPrefix + "*",
                SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileName)
            .ToArray();
        Assert.DoesNotContain(failed.GenerationName, generations);
        Assert.Contains(successful.GenerationName, generations);
        Assert.Equal(hasPreviousCurrent ? 2 : 1, generations.Length);
        if (hasPreviousCurrent)
        {
            Assert.Contains(
                generations,
                generation => !string.Equals(
                    generation,
                    successful.GenerationName,
                    StringComparison.Ordinal));
        }
    }

    [Fact]
    public async Task ReadBack_RejectsPairedManifestAndContentTruncation()
    {
        using var source = SourceFixture.Create(Revision);
        using var cache = TestTempDirectory.Create("external-source-cache-paired-truncation-");
        var writer = new LocalExternalSourceRepositoryCacheWriter(cache.DirectoryPath);
        var published = await writer.PublishAsync(source.Request);
        Assert.True(published.Succeeded);

        var manifestPath = Path.Combine(
            published.GenerationPath!,
            ExternalSourceRepositoryCacheContract.ManifestFileName);
        var manifest = JsonNode.Parse(File.ReadAllText(manifestPath));
        Assert.NotNull(manifest);
        manifest!["files"] = new JsonArray();
        File.WriteAllText(
            manifestPath,
            manifest.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        var contentPath = Path.Combine(
            published.GenerationPath!,
            ExternalSourceRepositoryCacheContract.ContentDirectoryName);
        Directory.Delete(contentPath, recursive: true);
        Directory.CreateDirectory(contentPath);

        Assert.False(writer.TryReadCurrent(source.Key, out _, out var diagnostic));
        Assert.NotNull(diagnostic);
    }

    [Fact]
    public async Task ReadBack_RejectsMissingExpectedSolutionPathFromContent()
    {
        using var source = SourceFixture.Create(Revision);
        using var cache = TestTempDirectory.Create("external-source-cache-solution-anchor-");
        var writer = new LocalExternalSourceRepositoryCacheWriter(cache.DirectoryPath);
        var published = await writer.PublishAsync(source.Request);
        Assert.True(published.Succeeded);

        File.Delete(Path.Combine(
            published.GenerationPath!,
            ExternalSourceRepositoryCacheContract.ContentDirectoryName,
            SolutionPath.Replace('/', Path.DirectorySeparatorChar)));

        Assert.False(writer.TryReadCurrent(source.Key, out _, out var diagnostic));
        Assert.NotNull(diagnostic);
    }

    [Theory]
    [MemberData(nameof(BoundedMalformedInputs))]
    public async Task ReadBack_RejectsBoundedMalformedInputs(string artifact, string mutation)
    {
        using var source = SourceFixture.Create(Revision);
        using var cache = TestTempDirectory.Create("external-source-cache-bounded-matrix-");
        var writer = new LocalExternalSourceRepositoryCacheWriter(cache.DirectoryPath);
        var published = await writer.PublishAsync(source.Request);
        Assert.True(published.Succeeded);

        var pointerPath = Path.Combine(
            writer.GetEntryDirectory(source.Key),
            ExternalSourceRepositoryCacheContract.CurrentPointerFileName);
        var pointerBefore = File.ReadAllBytes(pointerPath);
        var targetPath = GetMetadataPath(writer, source, published, artifact);
        var original = File.ReadAllBytes(targetPath);
        var maxBytes = GetMetadataLimit(artifact);
        Func<string, Stream>? openReadStream = null;
        if (mutation is "oversize" or "growth")
        {
            openReadStream = ExternalSourceRepositoryCacheReadBackTestSupport.CreateLengthControlledReadStream(
                targetPath,
                original,
                maxBytes,
                mutation == "oversize");
        }
        else
        {
            File.WriteAllBytes(
                targetPath,
                ExternalSourceRepositoryCacheReadBackTestSupport.CreateMalformedBytes(
                    artifact,
                    mutation,
                    original,
                    published.GenerationName!,
                    source.Key,
                    SolutionPath));
        }

        try
        {
            Assert.False(writer.TryReadCurrent(
                CreateReadRequest(writer, source, openReadStream),
                out var result,
                out var diagnostic));
            Assert.Null(result);
            Assert.NotNull(diagnostic);
            Assert.Equal(
                ExternalSourceRepositoryCacheContract.PublishFailedDiagnosticCode,
                diagnostic!.Code);
            if (!string.Equals(artifact, "current", StringComparison.Ordinal))
            {
                Assert.Equal(pointerBefore, File.ReadAllBytes(pointerPath));
            }
        }
        finally
        {
            if (openReadStream is null)
            {
                File.WriteAllBytes(targetPath, original);
            }
        }

        Assert.True(writer.TryReadCurrent(source.Key, out var current, out var readDiagnostic));
        Assert.Null(readDiagnostic);
        Assert.Equal(published.GenerationName, current!.Manifest.GenerationName);
        Assert.Equal(pointerBefore, File.ReadAllBytes(pointerPath));
    }

    [Theory]
    [MemberData(nameof(InventoryLimitInputs))]
    public async Task ReadBack_RejectsInventoryLimits(string mutation)
    {
        using var source = SourceFixture.Create(Revision);
        using var cache = TestTempDirectory.Create("external-source-cache-inventory-limits-");
        var writer = new LocalExternalSourceRepositoryCacheWriter(cache.DirectoryPath);
        var published = await writer.PublishAsync(source.Request);
        Assert.True(published.Succeeded);

        var pointerPath = Path.Combine(
            writer.GetEntryDirectory(source.Key),
            ExternalSourceRepositoryCacheContract.CurrentPointerFileName);
        var pointerBefore = File.ReadAllBytes(pointerPath);
        var inventoryPath = Path.Combine(
            published.GenerationPath!,
            ExternalSourceRepositoryCacheContract.InventoryFileName);
        var original = File.ReadAllBytes(inventoryPath);
        var validCurrent = Assert.IsType<ExternalSourceRepositoryCacheReadResult>(
            ReadCurrent(writer, source.Key));
        File.WriteAllBytes(
            inventoryPath,
            ExternalSourceRepositoryCacheReadBackTestSupport.CreateInventoryLimitBytes(
                mutation,
                Encoding.UTF8.GetString(original),
                validCurrent.Manifest.Files.Count));

        try
        {
            Assert.False(writer.TryReadCurrent(
                source.Key,
                out var result,
                out var diagnostic));
            Assert.Null(result);
            Assert.NotNull(diagnostic);
            Assert.Equal(
                ExternalSourceRepositoryCacheContract.PublishFailedDiagnosticCode,
                diagnostic!.Code);
            Assert.Equal(pointerBefore, File.ReadAllBytes(pointerPath));
        }
        finally
        {
            File.WriteAllBytes(inventoryPath, original);
        }

        Assert.True(writer.TryReadCurrent(source.Key, out var current, out var readDiagnostic));
        Assert.Null(readDiagnostic);
        Assert.Equal(published.GenerationName, current!.Manifest.GenerationName);
        Assert.Equal(pointerBefore, File.ReadAllBytes(pointerPath));
    }

    [Fact]
    public async Task ReadBack_RejectsContentGrowthAndTruncationWithBoundedHashing()
    {
        using var source = SourceFixture.Create(Revision);
        using var cache = TestTempDirectory.Create("external-source-cache-bounded-content-");
        var writer = new LocalExternalSourceRepositoryCacheWriter(cache.DirectoryPath);
        var published = await writer.PublishAsync(source.Request);
        Assert.True(published.Succeeded);
        var contentPath = Path.Combine(
            published.GenerationPath!,
            ExternalSourceRepositoryCacheContract.ContentDirectoryName,
            SolutionPath.Replace('/', Path.DirectorySeparatorChar));
        File.AppendAllText(contentPath, "growth");
        Assert.False(writer.TryReadCurrent(source.Key, out _, out _));

        File.WriteAllText(contentPath, "solutio");
        Assert.False(writer.TryReadCurrent(source.Key, out _, out _));
    }

    private static ExternalSourceRepositoryCacheReadRequest CreateReadRequest(
        LocalExternalSourceRepositoryCacheWriter writer,
        SourceFixture source,
        Func<string, Stream>? openReadStream) =>
        new()
        {
            Key = source.Key,
            EntryDirectory = writer.GetEntryDirectory(source.Key),
            ExpectedRevision = Revision,
            ExpectedSolutionPath = SolutionPath,
            OpenReadStream = openReadStream,
        };

    private static string GetMetadataPath(
        LocalExternalSourceRepositoryCacheWriter writer,
        SourceFixture source,
        ExternalSourceRepositoryCachePublishResult published,
        string artifact) =>
        artifact switch
        {
            "current" => Path.Combine(
                writer.GetEntryDirectory(source.Key),
                ExternalSourceRepositoryCacheContract.CurrentPointerFileName),
            "manifest" => Path.Combine(
                published.GenerationPath!,
                ExternalSourceRepositoryCacheContract.ManifestFileName),
            "inventory" => Path.Combine(
                published.GenerationPath!,
                ExternalSourceRepositoryCacheContract.InventoryFileName),
            _ => throw new ArgumentException("Unbekanntes Cachemetadaten-Artefakt.", nameof(artifact)),
        };

    private static int GetMetadataLimit(string artifact) =>
        artifact switch
        {
            "current" => ExternalSourceRepositoryCacheContract.MaxPointerJsonBytes,
            "manifest" => ExternalSourceRepositoryCacheContract.MaxManifestJsonBytes,
            "inventory" => ExternalSourceRepositoryCacheContract.MaxInventoryJsonBytes,
            _ => throw new ArgumentException("Unbekanntes Cachemetadaten-Artefakt.", nameof(artifact)),
        };

}
