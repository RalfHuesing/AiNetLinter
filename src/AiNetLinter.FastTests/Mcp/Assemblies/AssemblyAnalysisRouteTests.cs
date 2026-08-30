#nullable enable

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Configuration;
using AiNetLinter.FastTests.Fixtures;
using AiNetLinter.FastTests.Mcp.Tools.AssemblyAnalysis;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Assemblies.Analysis;
using AiNetLinter.Mcp.Assemblies.Analysis.References;
using AiNetLinter.Mcp.Assemblies.ExternalSource.Snapshots;
using AiNetLinter.Mcp.Tools.AssemblyAnalysis;
using AiNetLinter.TestKit;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Assemblies;

[Trait("Category", "Unit")]
// @covers AssemblyAnalysisRegistry
// @covers AssemblyAnalysisLease
// @covers AssemblyReferenceSessionExpander
// @covers AssemblyAnalysisSourceProjectEntryFactory
public sealed class AssemblyAnalysisRouteTests
{
    [Fact]
    public async Task AssemblyRoute_ResolvesRootReferenceAndAllowsLazyTransitiveTarget()
    {
        using var temp = TestTempDirectory.Create("assembly-route-reference-target-");
        var dependencyPath = AssemblyTestHelper.EmitAssembly(
            temp,
            "RoutedReferenceTarget",
            "namespace Probe; public sealed class DependencyType { public int Value => 1; }");
        var rootPath = AssemblyTestHelper.EmitAssembly(
            temp,
            "RoutedReferenceRoot",
            "namespace Probe; public sealed class Root { public DependencyType Value { get; } = new(); }",
            dependencyPath);
        await using var registry = new AssemblyAnalysisRegistry();

        var rootResult = await AnalysisToolCall.ExecuteRouted(
            AssemblyAnalysisDispatcher.CreateRoute(registry),
            new AnalysisToolCallRequest(
                new AnalysisTargetRequest("assembly", rootPath),
                new AnalysisToolDispatch(
                    AssemblySessionCall: lease => InspectAssemblyTool.ExecuteAsync(
                        lease,
                        new InspectAssemblyArguments(lease.CanonicalPath, null, null, null, true, 100))),
                CancellationToken.None));
        Assert.NotNull(rootResult.StructuredContent);
        var reference = Assert.Single(
            rootResult.StructuredContent!.Value.GetProperty("references").EnumerateArray(),
            item => item.GetProperty("name").GetString() == "RoutedReferenceTarget");
        var resolvedPath = reference.GetProperty("resolvedPath").GetString();
        Assert.Equal(Path.GetFullPath(dependencyPath), resolvedPath, StringComparer.OrdinalIgnoreCase);

        var dependencySession = Assert.Single(
            rootResult.StructuredContent.Value.GetProperty("referenceSessions").EnumerateArray(),
            item => item.GetProperty("reference").GetProperty("name").GetString() == "RoutedReferenceTarget");
        Assert.Equal("RoutedReferenceTarget", dependencySession.GetProperty("identity").GetProperty("name").GetString());
        Assert.Equal("complete", dependencySession.GetProperty("sessionStatus").GetString());
        Assert.Contains("RoutedReferenceTarget", Assert.IsType<ModelContextProtocol.Protocol.TextContentBlock>(Assert.Single(rootResult.Content)).Text, StringComparison.Ordinal);
        Assert.True(registry.ResidentCount >= 2);
    }

    [Fact]
    public async Task AssemblyRoute_ExpandsMappedSourceProjectReferenceThroughOneDispatcherCall()
    {
        using var temp = TestTempDirectory.Create("assembly-route-source-project-");
        temp.CreateSubdirectory("isolated");
        var emittedDependencyPath = AssemblyTestHelper.EmitAssembly(
            temp,
            "RoutedSourceDependency",
            "namespace Routed; public sealed class DependencyType { public int Value => 1; }");
        var dependencyPath = temp.GetPath("isolated/RoutedSourceDependency.dll");
        File.Move(emittedDependencyPath, dependencyPath);
        var rootPath = AssemblyTestHelper.EmitAssembly(
            temp,
            "RoutedSourceRoot",
            "namespace Routed; public sealed class Root { public DependencyType Value { get; } = new(); }",
            dependencyPath);
        var mapping = new ExternalSourceMapping(
            "https://gitea.example/routed-source.git",
            "src/Routed.slnx",
            ["RoutedSourceRoot"]);
        using var snapshot = ExternalSourceSnapshotTestFactory.CreateSnapshot(
            temp.DirectoryPath,
            mapping,
            new ExternalSourceProjectSpec(
                "SourceRoot",
                "RoutedSourceRoot",
                "namespace Routed; public sealed class Root { public DependencyType Value { get; } = new(); }",
                ["SourceDependency"]),
            new ExternalSourceProjectSpec(
                "SourceDependency",
                "RoutedSourceDependency",
                "namespace Routed; public sealed class DependencyType { public int Value => 1; }"));
        using var sourceRegistry = new SourceSnapshotRegistry();
        temp.CreateFile(
            "mappings.json",
            "{ \"repositories\": [{ \"url\": \"https://gitea.example/routed-source.git\", \"solutionPath\": \"src/Routed.slnx\", \"assemblies\": [\"RoutedSourceRoot\"] }] }");
        var settingsPath = temp.CreateFile(
            "appsettings.json",
            "{ \"ExternalSources\": { \"MappingsPath\": \"mappings.json\" } }");
        var provider = new AssemblyAnalysisRecordingProvider(
            new ExternalSourceProviderResult(true, [], snapshot));
        var orchestrator = AssemblySourceSelectionOrchestrator.CreateFromSettings(
            settingsPath,
            provider,
            sourceRegistry);
        await using var registry = new AssemblyAnalysisRegistry(orchestrator);

        var result = await AnalysisToolCall.ExecuteRouted(
            AssemblyAnalysisDispatcher.CreateRoute(registry),
            new AnalysisToolCallRequest(
                new AnalysisTargetRequest("assembly", rootPath),
                new AnalysisToolDispatch(
                    AssemblySessionCall: lease => InspectAssemblyTool.ExecuteAsync(
                        lease,
                        new InspectAssemblyArguments(lease.CanonicalPath, null, null, null, true, 100))),
                CancellationToken.None));

        Assert.NotEqual(true, result.IsError);
        var payload = result.StructuredContent!.Value;
        Assert.NotEqual("failed", payload.GetProperty("completeness").GetString());
        var reference = Assert.Single(
            payload.GetProperty("references").EnumerateArray(),
            item => item.GetProperty("name").GetString() == "RoutedSourceDependency");
        Assert.Equal("source_project", reference.GetProperty("resolutionState").GetString());
        Assert.False(reference.TryGetProperty("resolvedPath", out _));
        Assert.False(string.IsNullOrWhiteSpace(reference.GetProperty("sourceProjectPath").GetString()));

        var dependencySession = Assert.Single(
            payload.GetProperty("referenceSessions").EnumerateArray(),
            item => item.GetProperty("reference").GetProperty("name").GetString() == "RoutedSourceDependency");
        Assert.Equal("RoutedSourceDependency", dependencySession.GetProperty("identity").GetProperty("name").GetString());
        Assert.Equal("source-backed", dependencySession.GetProperty("origin").GetProperty("originKind").GetString());
        Assert.Equal("complete", dependencySession.GetProperty("sessionStatus").GetString());
        Assert.True(registry.ResidentCount >= 2);
    }
}
