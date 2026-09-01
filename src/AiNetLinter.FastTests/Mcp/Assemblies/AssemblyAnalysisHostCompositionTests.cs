#nullable enable

using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using AiNetLinter.Configuration;
using AiNetLinter.Mcp.Assemblies;
using AiNetLinter.Mcp.Tools.AssemblyAnalysis;
using AiNetLinter.Mcp.Tools.AssemblyAnalysis.Dispatch;
using AiNetLinter.TestKit;
using Microsoft.CodeAnalysis;
using ModelContextProtocol.Protocol;

namespace AiNetLinter.FastTests.Mcp.Assemblies;

[Trait("Category", "Component")]
public sealed class AssemblyAnalysisHostCompositionTests
{
    [Fact]
    public async Task Composition_UsesOneHostContextForBothAssemblyToolsAndPreservesFallback()
    {
        using var temp = TestTempDirectory.Create("assembly-host-composition-tools-");
        var assemblyPath = AssemblyTestHelper.EmitAssembly(
            temp,
            "TargetAssembly",
            "namespace Target; public sealed class TargetOnly { }");
        var mappingsPath = temp.CreateFile(
            "mappings.json",
            "{ \"repositories\": [{ \"url\": \"https://gitea.example/shared.git\", \"solutionPath\": \"src/Shared.slnx\", \"assemblies\": [\"TargetAssembly\"] }] }");
        var settingsPath = temp.CreateFile(
            "appsettings.json",
            "{ \"ExternalSources\": { \"MappingsPath\": \"mappings.json\" } }");
        await using var composition = AssemblyAnalysisHostComposition.Create(
            settingsPath,
            new UnavailableExternalSourceProvider(),
            resourceOverrides: new ExternalResourceRegistryOverrides(MaxResidentResources: 6));

        var inspect = await InspectAssemblyToolDispatch.ExecuteAsync(
            null,
            new InspectAssemblyArguments(
                assemblyPath,
                null,
                "TargetOnly",
                null,
                true,
                10,
                true,
                null,
                10),
            CancellationToken.None,
            composition.Orchestrator);
        var extensions = await FindAssemblyExtensionsToolDispatch.ExecuteAsync(
            null,
            new FindAssemblyExtensionsArguments(assemblyPath, null, null, null, 10),
            CancellationToken.None,
            composition.Orchestrator);

        Assert.NotEqual(true, inspect.IsError);
        Assert.NotEqual(true, extensions.IsError);
        Assert.Contains("decompiled", TextOf(inspect), StringComparison.Ordinal);
        Assert.Contains(ExternalSourceConfigurationDiagnosticCodes.ProviderUnavailable, TextOf(inspect), StringComparison.Ordinal);
        Assert.Contains("decompiled", TextOf(extensions), StringComparison.Ordinal);
        Assert.Contains(ExternalSourceConfigurationDiagnosticCodes.ProviderUnavailable, TextOf(extensions), StringComparison.Ordinal);
        Assert.IsType<UnavailableExternalSourceProvider>(composition.Provider);
        Assert.True(composition.ConfigurationResult.Succeeded);
        Assert.Equal(0, composition.Registry.ResidentCount);
    }

    [Fact]
    public async Task Composition_DefaultProviderUsesConfiguredCacheWithoutNetworkAccess()
    {
        using var temp = TestTempDirectory.Create("assembly-host-composition-default-provider-");
        var mappingsPath = temp.CreateFile(
            "mappings.json",
            "{ \"repositories\": [{ \"url\": \"https://gitea.example/shared.git\", \"solutionPath\": \"src/Shared.slnx\", \"assemblies\": [\"Shared\"] }] }");
        var settingsPath = temp.CreateFile(
            "appsettings.json",
            "{ \"ExternalSources\": { \"MappingsPath\": "
                + JsonSerializer.Serialize(mappingsPath)
                + ", \"CacheRoot\": \"cache\" } }");

        await using var composition = AssemblyAnalysisHostComposition.Create(settingsPath);

        Assert.True(composition.ConfigurationResult.Succeeded);
        Assert.IsType<GiteaExternalSourceProvider>(composition.Provider);
        Assert.False(composition.IsDisposed);
    }

    [Fact]
    public async Task Composition_WiresConfiguredExternalResourceLimitsIntoBothRegistries()
    {
        using var temp = TestTempDirectory.Create("assembly-host-composition-resource-limits-");
        var mappingsPath = temp.CreateFile(
            "mappings.json",
            "{ \"repositories\": [{ \"url\": \"https://gitea.example/shared.git\", \"solutionPath\": \"src/Shared.slnx\", \"assemblies\": [\"Shared\"] }] }");
        var settingsPath = temp.CreateFile(
            "appsettings.json",
            "{ \"ExternalSources\": { \"MappingsPath\": "
                + JsonSerializer.Serialize(mappingsPath)
                + ", \"MaxDiskBytes\": 100, \"MaxMemoryBytes\": 200, \"MaxParallelOperations\": 3, \"MaxResidentResources\": 5, \"IdleTtlMinutes\": 0.5 } }");

        await using var composition = AssemblyAnalysisHostComposition.Create(
            settingsPath,
            new UnavailableExternalSourceProvider(),
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
        Assert.Equal(composition.Resources.Health.MaxDiskBytes, composition.SourceResources.Health.MaxDiskBytes);
        Assert.Equal(composition.Resources.Health.MaxMemoryBytes, composition.SourceResources.Health.MaxMemoryBytes);
        Assert.Equal(composition.Resources.Health.MaxParallelOperations, composition.SourceResources.Health.MaxParallelOperations);
        Assert.Equal(composition.Resources.Health.MaxResidentResources, composition.SourceResources.Health.MaxResidentResources);
        Assert.Equal(TimeSpan.FromMinutes(1.5), composition.Resources.IdleTtl);
    }

    [Fact]
    public async Task Dispose_ReleasesTheHostOwnedRegistryOnceAndClosesOrchestratorAccess()
    {
        using var temp = TestTempDirectory.Create("assembly-host-composition-lifetime-");
        var mapping = new ExternalSourceMapping(
            "https://gitea.example/shared.git",
            "src/Shared.slnx",
            ["Shared"]);
        using var workspace = new AdhocWorkspace();
        var solution = workspace.AddSolution(SolutionInfo.Create(
            SolutionId.CreateNewId(),
            VersionStamp.Create(),
            filePath: Path.Combine(temp.DirectoryPath, "Shared.slnx")));
        var snapshot = new ExternalSourceSnapshot(
            SourceSnapshotIdentity.Create(mapping, "revision-1"),
            solution,
            workspace);
        await using var composition = AssemblyAnalysisHostComposition.Create(
            temp.GetPath("missing-appsettings.json"));
        using var lease = composition.Registry.Acquire(snapshot);

        Assert.Equal(1, composition.Registry.ResidentCount);
        lease.Dispose();
        Assert.True(lease.IsDisposed);
        Assert.Equal(1, composition.Registry.ResidentCount);

        await composition.DisposeAsync();
        await composition.DisposeAsync();

        Assert.True(composition.IsDisposed);
        Assert.Equal(0, composition.Registry.ResidentCount);
        Assert.True(snapshot.IsDisposed);
        Assert.Throws<ObjectDisposedException>(() => _ = composition.Orchestrator);
    }

    private static string TextOf(CallToolResult result) =>
        Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
}
