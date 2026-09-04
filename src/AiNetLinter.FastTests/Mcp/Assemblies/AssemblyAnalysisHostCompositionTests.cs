#nullable enable

using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Configuration;
using AiNetLinter.Mcp.Assemblies;
using AiNetLinter.Mcp.Assemblies.Analysis;
using AiNetLinter.TestKit;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Assemblies;

[Trait("Category", "Component")]
public sealed class AssemblyAnalysisHostCompositionTests
{
    [Fact]
    public async Task Composition_CanBeCreatedWithDefaultsAndDisposedAsync()
    {
        await using var composition = AssemblyAnalysisHostComposition.Create();

        Assert.NotNull(composition.Sessions);
        Assert.NotNull(composition.Resources);
        Assert.False(composition.IsDisposed);
        Assert.Equal(0, composition.Sessions.ResidentCount);

        await composition.DisposeAsync();
        Assert.True(composition.IsDisposed);
        Assert.Throws<ObjectDisposedException>(() => _ = composition.Sessions);
    }

    [Fact]
    public async Task Composition_WiresConfiguredExternalResourceLimits()
    {
        await using var composition = AssemblyAnalysisHostComposition.Create(
            resourceOverrides: new ExternalResourceRegistryOverrides(
                MaxDiskBytes: 300,
                MaxMemoryBytes: 400,
                MaxParallelOperations: 5,
                MaxResidentResources: 6,
                IdleTtlMinutes: 1.5m));

        Assert.Equal(300, composition.Resources.Health.MaxDiskBytes);
        Assert.Equal(400, composition.Resources.Health.MaxMemoryBytes);
        Assert.Equal(5, composition.Resources.Health.MaxParallelOperations);
        Assert.Equal(6, composition.Resources.Health.MaxResidentResources);
        Assert.Equal(TimeSpan.FromMinutes(1.5), composition.Resources.IdleTtl);
    }

    [Fact]
    public async Task Composition_WiresAssemblyCacheRootAndTimeoutIntoSessions()
    {
        using var temp = TestTempDirectory.Create("assembly-host-composition-settings-");
        var assemblyPath = AssemblyTestHelper.EmitAssembly(
            temp,
            "ConfiguredAssembly",
            "namespace Configured; public sealed class Value { public int Number => 1; }");
        var settingsPath = temp.CreateFile(
            "appsettings.json",
            """{ "AssemblyAnalysis": { "CacheRoot": "assembly-cache", "DecompilationTimeoutSeconds": 12 } }""");
        var cacheRoot = Path.Combine(temp.DirectoryPath, "assembly-cache");

        await using var composition = AssemblyAnalysisHostComposition.Create(settingsPath);
        var leaseResult = await composition.Sessions.LeaseAsync(assemblyPath);
        Assert.Null(leaseResult.Error);
        using var lease = leaseResult.Lease!;

        Assert.NotNull(composition.ConfigurationResult.AssemblyAnalysis);
        Assert.Equal(Path.GetFullPath(cacheRoot), composition.ConfigurationResult.AssemblyAnalysis!.CacheRoot);
        Assert.Equal(TimeSpan.FromSeconds(12), composition.ConfigurationResult.AssemblyAnalysis.DecompilationTimeout);
        Assert.NotEmpty(Directory.EnumerateFiles(cacheRoot, "*.csproj", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task Composition_IdempotentDisposalPreservesState()
    {
        var composition = AssemblyAnalysisHostComposition.Create();
        Assert.False(composition.IsDisposed);

        await composition.DisposeAsync();
        Assert.True(composition.IsDisposed);

        await composition.DisposeAsync();
        Assert.True(composition.IsDisposed);
        Assert.Throws<ObjectDisposedException>(() => _ = composition.Sessions);
    }
}
