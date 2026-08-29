#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Configuration;
using AiNetLinter.Mcp.Assemblies;
using AiNetLinter.TestKit;
using Xunit;
using static AiNetLinter.FastTests.Mcp.Assemblies.ExternalSourceRepositoryCacheTestAssertions;
using static AiNetLinter.FastTests.Mcp.Assemblies.ExternalSourceRepositoryCacheTestData;

namespace AiNetLinter.FastTests.Mcp.Assemblies;

// @covers ExternalSourceRepositoryCacheKey
// @covers LocalExternalSourceRepositoryCacheWriter
[Trait("Category", "Component")]
public sealed partial class ExternalSourceRepositoryCacheWriterTests
{
    [Fact]
    public void CacheKey_IsDeterministicAndCredentialFree()
    {
        Assert.True(ExternalSourceRepositoryCacheKey.TryCreate(
            RepositoryUrl,
            "src\\.\\BaselineMini.slnx",
            out var first));
        Assert.True(ExternalSourceRepositoryCacheKey.TryCreate(
            RepositoryUrl,
            SolutionPath,
            out var second));

        Assert.Equal(first, second);
        Assert.Equal(64, first!.StableValue.Length);
        Assert.Equal(first.StableValue, first.StableValue.ToLowerInvariant());
        Assert.DoesNotContain("gitea", first.StableValue, StringComparison.OrdinalIgnoreCase);
        Assert.False(ExternalSourceRepositoryCacheKey.TryCreate(
            "https://build-user:blocked@gitea.example/shared.git",
            SolutionPath,
            out _));

        using var temp = TestTempDirectory.Create("external-source-cache-key-");
        var writer = new LocalExternalSourceRepositoryCacheWriter(temp.DirectoryPath);
        var entryPath = writer.GetEntryDirectory(first);
        Assert.StartsWith(
            Path.GetFullPath(temp.DirectoryPath) + Path.DirectorySeparatorChar,
            entryPath,
            StringComparison.OrdinalIgnoreCase);
        Assert.Equal(first.StableValue, Path.GetFileName(entryPath));
    }

    [Fact]
    public async Task PublishAsync_WritesCompleteManifestAndKeepsGenerationAfterHandleDispose()
    {
        using var source = SourceFixture.Create(Revision);
        using var cache = TestTempDirectory.Create("external-source-cache-publish-");
        var writer = new LocalExternalSourceRepositoryCacheWriter(cache.DirectoryPath);

        var result = await writer.PublishAsync(source.Request);

        Assert.True(result.Succeeded);
        Assert.Equal(ExternalSourceRepositoryCachePublishFailureKind.None, result.FailureKind);
        Assert.Equal(source.Key, result.CacheKey);
        Assert.NotNull(result.GenerationName);
        Assert.NotNull(result.GenerationPath);
        Assert.True(Directory.Exists(result.GenerationPath));
        Assert.True(File.Exists(Path.Combine(
            result.GenerationPath!,
            ExternalSourceRepositoryCacheContract.ManifestFileName)));
        Assert.True(File.Exists(Path.Combine(
            result.GenerationPath!,
            ExternalSourceRepositoryCacheContract.InventoryFileName)));

        Assert.True(writer.TryReadCurrent(
            new ExternalSourceRepositoryCacheReadRequest
            {
                Key = source.Key,
                EntryDirectory = writer.GetEntryDirectory(source.Key),
                ExpectedRevision = Revision,
                ExpectedSolutionPath = SolutionPath,
            },
            out var read,
            out var diagnostic));
        Assert.Null(diagnostic);
        Assert.NotNull(read);
        Assert.Equal(source.Key.StableValue, read!.Manifest.CacheKey);
        Assert.Equal(RepositoryUrl, read.Manifest.CanonicalRepositoryUrl);
        Assert.Equal(SolutionPath, read.Manifest.SolutionPath);
        Assert.Equal(Revision, read.Manifest.LoadedRevision);
        Assert.Equal(result.GenerationName, read.Manifest.GenerationName);
        Assert.NotEqual(default, read.Manifest.CreatedUtc);
        Assert.Contains(
            read.Manifest.Files,
            file => file.RelativePath == SolutionPath && file.Length > 0);
        Assert.Contains(
            read.Manifest.Files,
            file => file.RelativePath == ".git/config");
        Assert.DoesNotContain(
            read.Manifest.Files,
            file => string.Equals(
                file.RelativePath,
                ExternalSourceCheckoutOwnership.OwnershipMarkerFileName,
                StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(
            Directory.EnumerateFiles(read.GenerationPath, "*", SearchOption.AllDirectories),
            path => string.Equals(
                Path.GetFileName(path),
                ExternalSourceCheckoutOwnership.OwnershipMarkerFileName,
                StringComparison.OrdinalIgnoreCase));

        source.Handle.Dispose();
        Assert.True(File.Exists(Path.Combine(
            read.GenerationPath,
            ExternalSourceRepositoryCacheContract.ContentDirectoryName,
            SolutionPath.Replace('/', Path.DirectorySeparatorChar))));
    }

    [Fact]
    public async Task PublishAsync_LeavesPreviousCurrentWhenSourceValidationFails()
    {
        using var firstSource = SourceFixture.Create(Revision);
        using var secondSource = SourceFixture.Create(OtherRevision);
        using var cache = TestTempDirectory.Create("external-source-cache-atomic-");
        var writer = new LocalExternalSourceRepositoryCacheWriter(cache.DirectoryPath);

        var first = await writer.PublishAsync(firstSource.Request);
        Assert.True(first.Succeeded);
        var currentBefore = Assert.IsType<ExternalSourceRepositoryCacheReadResult>(
            ReadCurrent(writer, firstSource.Key)).Manifest.GenerationName;
        File.Delete(Path.Combine(
            secondSource.CheckoutPath,
            SolutionPath.Replace('/', Path.DirectorySeparatorChar)));

        var failed = await writer.PublishAsync(secondSource.Request);

        Assert.False(failed.Succeeded);
        Assert.Equal(ExternalSourceRepositoryCachePublishFailureKind.UnsafeSource, failed.FailureKind);
        var currentAfter = Assert.IsType<ExternalSourceRepositoryCacheReadResult>(
            ReadCurrent(writer, firstSource.Key));
        Assert.Equal(currentBefore, currentAfter.Manifest.GenerationName);
        Assert.Equal(Revision, currentAfter.Manifest.LoadedRevision);
        Assert.Equal(1, Directory.EnumerateDirectories(
            writer.GetEntryDirectory(firstSource.Key),
            ExternalSourceRepositoryCacheContract.GenerationDirectoryPrefix + "*",
            SearchOption.TopDirectoryOnly).Count());
    }

    [Fact]
    public async Task PublishAsync_RejectsManifestHashTamperingOnReadBack()
    {
        using var source = SourceFixture.Create(Revision);
        using var cache = TestTempDirectory.Create("external-source-cache-manifest-");
        var writer = new LocalExternalSourceRepositoryCacheWriter(cache.DirectoryPath);
        var published = await writer.PublishAsync(source.Request);
        Assert.True(published.Succeeded);

        var manifestPath = Path.Combine(
            published.GenerationPath!,
            ExternalSourceRepositoryCacheContract.ManifestFileName);
        var manifest = File.ReadAllText(manifestPath);
        var originalHash = Assert.IsType<ExternalSourceRepositoryCacheReadResult>(
                ReadCurrent(writer, source.Key))
            .Manifest.Files[0].ContentHash;
        File.WriteAllText(
            manifestPath,
            manifest.Replace(
                originalHash,
                new string('0', originalHash.Length),
                StringComparison.Ordinal));

        Assert.False(writer.TryReadCurrent(
            source.Key,
            out _,
            out var diagnostic));
        Assert.NotNull(diagnostic);
        Assert.Equal(
            ExternalSourceRepositoryCacheContract.PublishFailedDiagnosticCode,
            diagnostic!.Code);
    }

    [Fact]
    public async Task ReadBack_RejectsManifestIdentityRevisionAndFileSetTampering()
    {
        using var source = SourceFixture.Create(Revision);
        using var cache = TestTempDirectory.Create("external-source-cache-integrity-");
        var writer = new LocalExternalSourceRepositoryCacheWriter(cache.DirectoryPath);
        var published = await writer.PublishAsync(source.Request);
        Assert.True(published.Succeeded);
        var manifestPath = Path.Combine(
            published.GenerationPath!,
            ExternalSourceRepositoryCacheContract.ManifestFileName);
        var originalManifest = File.ReadAllText(manifestPath);

        foreach (var mutation in new Func<string, string>[]
        {
            value => value.Replace(source.Key.StableValue, new string('a', 64), StringComparison.Ordinal),
            value => value.Replace(Revision, OtherRevision, StringComparison.Ordinal),
            value => value.Replace(SolutionPath, "other.slnx", StringComparison.Ordinal),
            value => value.Replace(
                ExternalSourceRepositoryCacheContract.CacheSchemaVersion,
                "other-cache-v1",
                StringComparison.Ordinal),
        })
        {
            File.WriteAllText(manifestPath, mutation(originalManifest));
            Assert.False(writer.TryReadCurrent(
                new ExternalSourceRepositoryCacheReadRequest
                {
                    Key = source.Key,
                    EntryDirectory = writer.GetEntryDirectory(source.Key),
                    ExpectedRevision = Revision,
                    ExpectedSolutionPath = SolutionPath,
                },
                out _,
                out var diagnostic));
            Assert.NotNull(diagnostic);
            File.WriteAllText(manifestPath, originalManifest);
        }

        var contentPath = Path.Combine(
            published.GenerationPath!,
            ExternalSourceRepositoryCacheContract.ContentDirectoryName,
            "src",
            "Program.cs");
        File.Delete(contentPath);
        Assert.False(writer.TryReadCurrent(
            new ExternalSourceRepositoryCacheReadRequest
            {
                Key = source.Key,
                EntryDirectory = writer.GetEntryDirectory(source.Key),
                ExpectedRevision = Revision,
                ExpectedSolutionPath = SolutionPath,
            },
            out _,
            out _));
        File.WriteAllText(contentPath, "class Program { }");
        File.WriteAllText(
            Path.Combine(
                published.GenerationPath!,
                ExternalSourceRepositoryCacheContract.ContentDirectoryName,
                "extra.txt"),
            "extra");
        Assert.False(writer.TryReadCurrent(
            new ExternalSourceRepositoryCacheReadRequest
            {
                Key = source.Key,
                EntryDirectory = writer.GetEntryDirectory(source.Key),
                ExpectedRevision = Revision,
                ExpectedSolutionPath = SolutionPath,
            },
            out _,
            out _));
    }

    [Fact]
    public async Task PublishAsync_CancelledBeforePublishKeepsPreviousCurrentAndCleansStaging()
    {
        using var source = SourceFixture.Create(Revision);
        using var replacement = SourceFixture.Create(OtherRevision);
        using var cache = TestTempDirectory.Create("external-source-cache-cancel-");
        var writer = new LocalExternalSourceRepositoryCacheWriter(cache.DirectoryPath);
        var first = await writer.PublishAsync(source.Request);
        Assert.True(first.Succeeded);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var result = await writer.PublishAsync(replacement.Request, cancellation.Token);

        Assert.False(result.Succeeded);
        Assert.Equal(ExternalSourceRepositoryCachePublishFailureKind.Cancelled, result.FailureKind);
        Assert.True(writer.TryReadCurrent(source.Key, out var current, out _));
        Assert.Equal(Revision, current!.Manifest.LoadedRevision);
        Assert.Single(Directory.EnumerateDirectories(
            writer.GetEntryDirectory(source.Key),
            ExternalSourceRepositoryCacheContract.GenerationDirectoryPrefix + "*",
            SearchOption.TopDirectoryOnly));
    }

    [Fact]
    public async Task PublishAsync_PointerPathFailureDoesNotReplaceExistingPath()
    {
        using var source = SourceFixture.Create(Revision);
        using var cache = TestTempDirectory.Create("external-source-cache-pointer-");
        var writer = new LocalExternalSourceRepositoryCacheWriter(cache.DirectoryPath);
        var key = source.Key;
        var entryDirectory = writer.GetEntryDirectory(key);
        Directory.CreateDirectory(entryDirectory);
        var pointerPath = Path.Combine(
            entryDirectory,
            ExternalSourceRepositoryCacheContract.CurrentPointerFileName);
        Directory.CreateDirectory(pointerPath);

        var result = await writer.PublishAsync(source.Request);

        Assert.False(result.Succeeded);
        Assert.Equal(ExternalSourceRepositoryCachePublishFailureKind.PointerPublishFailed, result.FailureKind);
        Assert.True(Directory.Exists(pointerPath));
        Assert.Empty(Directory.EnumerateDirectories(
            entryDirectory,
            ExternalSourceRepositoryCacheContract.GenerationDirectoryPrefix + "*",
            SearchOption.TopDirectoryOnly));
    }

    [Fact]
    public async Task ReadBack_RejectsUnsafeCurrentPointerWithoutExposingGenerationPath()
    {
        using var source = SourceFixture.Create(Revision);
        using var cache = TestTempDirectory.Create("external-source-cache-pointer-read-");
        var writer = new LocalExternalSourceRepositoryCacheWriter(cache.DirectoryPath);
        var published = await writer.PublishAsync(source.Request);
        Assert.True(published.Succeeded);
        var pointerPath = Path.Combine(
            writer.GetEntryDirectory(source.Key),
            ExternalSourceRepositoryCacheContract.CurrentPointerFileName);
        File.WriteAllText(pointerPath, "{\"generation\":\"../outside\"}");

        Assert.False(writer.TryReadCurrent(source.Key, out _, out var diagnostic));
        Assert.NotNull(diagnostic);
        Assert.DoesNotContain(
            "outside",
            diagnostic!.Message,
            StringComparison.OrdinalIgnoreCase);
        Assert.True(Directory.Exists(published.GenerationPath));
    }

    [Fact]
    public async Task PublishAsync_SerializesSameKeyAndLeavesConsistentCurrent()
    {
        using var firstSource = SourceFixture.Create(Revision);
        using var secondSource = SourceFixture.Create(OtherRevision);
        using var cache = TestTempDirectory.Create("external-source-cache-concurrent-");
        var writer = new LocalExternalSourceRepositoryCacheWriter(cache.DirectoryPath);

        var results = await Task.WhenAll(
            writer.PublishAsync(firstSource.Request),
            writer.PublishAsync(secondSource.Request));

        Assert.All(results, result => Assert.True(result.Succeeded));
        Assert.True(writer.TryReadCurrent(
            firstSource.Key,
            out var current,
            out var diagnostic));
        Assert.Null(diagnostic);
        Assert.Contains(
            current!.Manifest.LoadedRevision,
            new[] { Revision, OtherRevision });
        Assert.Equal(
            current.Manifest.GenerationName,
            Path.GetFileName(current.GenerationPath));
        Assert.Equal(2, Directory.EnumerateDirectories(
            writer.GetEntryDirectory(firstSource.Key),
            ExternalSourceRepositoryCacheContract.GenerationDirectoryPrefix + "*",
            SearchOption.TopDirectoryOnly).Count());
    }

    [Fact]
    public async Task PublishAsync_ActualReparseEntryFailsClosed()
    {
        WindowsReparseCapabilityGate.Require();
        using var source = SourceFixture.Create(Revision);
        using var cache = TestTempDirectory.Create("external-source-cache-reparse-");
        var target = TestTempDirectory.Create("external-source-cache-reparse-target-");
        try
        {
            Directory.CreateSymbolicLink(
                Path.Combine(source.CheckoutPath, "unsafe-link"),
                target.DirectoryPath);
            var writer = new LocalExternalSourceRepositoryCacheWriter(cache.DirectoryPath);

            var result = await writer.PublishAsync(source.Request);

            Assert.False(result.Succeeded);
            Assert.Equal(ExternalSourceRepositoryCachePublishFailureKind.UnsafeSource, result.FailureKind);
            Assert.False(Directory.Exists(writer.GetEntryDirectory(source.Key)));
            Assert.True(Directory.Exists(target.DirectoryPath));
        }
        finally
        {
            var linkPath = Path.Combine(source.CheckoutPath, "unsafe-link");
            if (Directory.Exists(linkPath))
            {
                Directory.Delete(linkPath);
            }

            target.Dispose();
        }
    }

}
