#nullable enable

using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Configuration;
using AiNetLinter.Mcp.Assemblies;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Assemblies;

[Trait("Category", "Component")]
public sealed class ExternalSourceRepositoryCacheConfigurationTests
{
    [Fact]
    public async Task LoadedCacheOptions_UseSourceRootAndConfiguredRefreshInterval()
    {
        using var tempDir = TestTempDirectory.Create("external-source-cache-options-");
        var mappingsPath = tempDir.CreateFile(
            "mappings.json",
            $$"""{ "repositories": [{ "url": "https://gitea.example/shared.git", "solutionPath": "src/Shared.slnx", "assemblies": ["Shared"] }] }""");
        var settingsPath = tempDir.CreateFile(
            "appsettings.json",
            $$"""{ "ExternalSources": { "MappingsPath": {{JsonSerializer.Serialize(mappingsPath)}}, "CacheRoot": "configured-cache", "RefreshIntervalMinutes": 1 } }""");

        var loadResult = ExternalSourceConfigurationLoader.Load(settingsPath);

        Assert.True(loadResult.Succeeded);
        var options = loadResult.Configuration!.CacheOptions;
        var construction = ExternalSourceRepositoryCacheOptionsFactory.Create(options);
        var expectedRoot = Path.GetFullPath(Path.Combine(tempDir.DirectoryPath, "configured-cache"));
        Assert.Equal(expectedRoot, construction.CacheRoot);
        Assert.Equal(
            Path.Combine(expectedRoot, ExternalSourceRepositoryCacheContract.SourceDirectoryName),
            construction.SourceRoot);
        Assert.Equal(TimeSpan.FromMinutes(1), construction.RefreshInterval);

        using var source = SourceFixture.Create(ExternalSourceRepositoryCacheTestData.Revision);
        var writer = new LocalExternalSourceRepositoryCacheWriter(construction.SourceRoot);
        var initial = await writer.PublishAsync(source.Request);
        Assert.True(initial.Succeeded);
        var transport = new ExternalSourceRecordingTransport(
            (_, _, _) => throw new InvalidOperationException("Clone darf beim Refresh nicht aufgerufen werden."),
            (_, _, _) => ExternalSourceRepositoryTransportResult.Success(
                ExternalSourceRepositoryCacheTestData.OtherRevision));
        using var staging = TestTempDirectory.Create("external-source-cache-options-staging-");
        var acquirer = ExternalSourceRepositoryAcquirerFactory.CreateConfigured(
            transport,
            staging.DirectoryPath,
            cacheOptions: options,
            refreshPolicy: construction.CreateRefreshPolicy(
                new FixedTimeProvider(DateTimeOffset.UtcNow.AddMinutes(2))));

        var result = await acquirer.AcquireAsync(ExternalSourceRepositoryCacheTestData.CreateMapping());

        Assert.True(result.IsAvailable);
        Assert.Equal(0, transport.CallCount);
        Assert.Equal(1, transport.FetchCallCount);
        Assert.NotEqual(
            initial.GenerationName,
            ExternalSourceRepositoryCacheTestAssertions.ReadCurrentGenerationName(writer, source.Key));
        result.Checkout!.Dispose();
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset utcNow;

        internal FixedTimeProvider(DateTimeOffset utcNow)
        {
            this.utcNow = utcNow;
        }

        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
