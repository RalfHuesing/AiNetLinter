#nullable enable

using System;
using System.IO;
using System.Threading;
using AiNetLinter.Configuration;
using AiNetLinter.Mcp.Assemblies;
using AiNetLinter.Mcp.Tools.AssemblyAnalysis;
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
        using var composition = AssemblyAnalysisHostComposition.Create(settingsPath);

        var inspect = await InspectAssemblyTool.ExecuteAsync(
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
        var extensions = await FindAssemblyExtensionsTool.ExecuteAsync(
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
    public void Dispose_ReleasesTheHostOwnedRegistryOnceAndClosesOrchestratorAccess()
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
        using var composition = AssemblyAnalysisHostComposition.Create(
            temp.GetPath("missing-appsettings.json"));
        using var lease = composition.Registry.Acquire(snapshot);

        Assert.Equal(1, composition.Registry.ResidentCount);
        lease.Dispose();
        Assert.True(lease.IsDisposed);
        Assert.Equal(1, composition.Registry.ResidentCount);

        composition.Dispose();
        composition.Dispose();

        Assert.True(composition.IsDisposed);
        Assert.Equal(0, composition.Registry.ResidentCount);
        Assert.True(snapshot.IsDisposed);
        Assert.Throws<ObjectDisposedException>(() => _ = composition.Orchestrator);
    }

    private static string TextOf(CallToolResult result) =>
        Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
}
