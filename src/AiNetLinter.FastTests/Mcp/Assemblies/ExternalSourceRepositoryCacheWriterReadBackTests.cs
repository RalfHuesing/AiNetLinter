#nullable enable

using System;
using System.IO;
using System.Linq;
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
        Task<ExternalSourceRepositoryCachePublishResult>? concurrentPublish = null;
        string? previousGeneration = null;
        var callbackCalls = 0;
        LocalExternalSourceRepositoryCacheWriter writer = null!;
        writer = new LocalExternalSourceRepositoryCacheWriter(
            cache.DirectoryPath,
            () =>
            {
                if (Interlocked.Exchange(ref callbackCalls, 1) != 0)
                {
                    return;
                }

                cancellation.Cancel();
                concurrentPublish = writer.PublishAsync(successfulSource.Request);
            });

        if (hasPreviousCurrent)
        {
            var seedWriter = new LocalExternalSourceRepositoryCacheWriter(cache.DirectoryPath);
            var seed = await seedWriter.PublishAsync(previousSource.Request);
            Assert.True(seed.Succeeded);
            previousGeneration = seed.GenerationName;
        }

        var failed = await writer.PublishAsync(failedSource.Request, cancellation.Token);
        Assert.False(failed.Succeeded);
        Assert.Equal(
            ExternalSourceRepositoryCachePublishFailureKind.Cancelled,
            failed.FailureKind);
        Assert.NotNull(concurrentPublish);
        var successful = await concurrentPublish!;
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
            Assert.Contains(previousGeneration, generations);
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

    [Fact]
    public async Task ReadBack_RejectsOversizeInvalidUtf8AndTruncatedMetadata()
    {
        using var source = SourceFixture.Create(Revision);
        using var cache = TestTempDirectory.Create("external-source-cache-bounded-metadata-");
        var writer = new LocalExternalSourceRepositoryCacheWriter(cache.DirectoryPath);
        var published = await writer.PublishAsync(source.Request);
        Assert.True(published.Succeeded);
        var pointerPath = Path.Combine(
            writer.GetEntryDirectory(source.Key),
            ExternalSourceRepositoryCacheContract.CurrentPointerFileName);
        var validPointer = File.ReadAllBytes(pointerPath);

        File.WriteAllBytes(
            pointerPath,
            new byte[ExternalSourceRepositoryCacheContract.MaxPointerJsonBytes + 1]);
        Assert.False(writer.TryReadCurrent(source.Key, out _, out _));

        File.WriteAllBytes(pointerPath, new byte[] { 0xFF });
        Assert.False(writer.TryReadCurrent(source.Key, out _, out _));

        File.WriteAllBytes(pointerPath, validPointer);
        var manifestPath = Path.Combine(
            published.GenerationPath!,
            ExternalSourceRepositoryCacheContract.ManifestFileName);
        File.WriteAllText(manifestPath, "{");
        Assert.False(writer.TryReadCurrent(source.Key, out _, out _));
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
}
