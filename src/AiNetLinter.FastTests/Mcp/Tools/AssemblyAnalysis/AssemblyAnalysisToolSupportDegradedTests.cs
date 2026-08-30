#nullable enable

using System;
using System.Threading.Tasks;
using AiNetLinter.Configuration;
using AiNetLinter.FastTests.Fixtures;
using AiNetLinter.Mcp.Assemblies;
using AiNetLinter.Mcp.Tools.AssemblyAnalysis;
using AiNetLinter.TestKit;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Tools.AssemblyAnalysis;

[Trait("Category", "Component")]
public sealed class AssemblyAnalysisToolSupportDegradedTests
{
    [Fact]
    public async Task ExecuteAsync_DegradedProviderShowsLastGoodAndUsesDecompilationFallback()
    {
        using var temp = TestTempDirectory.Create("assembly-source-degraded-");
        var assemblyPath = AssemblyTestHelper.EmitAssembly(
            temp,
            "TargetAssembly",
            "namespace Target; public sealed class TargetOnly { }");
        const string lastGoodRevision = "0123456789abcdef0123456789abcdef01234567";
        var provider = new AssemblyAnalysisRecordingProvider(new ExternalSourceProviderResult(
            isAvailable: false,
            diagnostics: [new ExternalSourceConfigurationDiagnostic(
                ExternalSourceConfigurationDiagnosticCodes.RepositoryRefreshDegraded,
                "Der aktuelle Repository-Refresh ist degraded.",
                "warning",
                "$repository")],
            state: ExternalSourceRepositoryResultState.Create(
                ExternalSourceProviderFailureKind.NetworkUnavailable,
                ExternalSourceRepositoryHealth.Degraded,
                lastGoodRevision)));
        using var registry = new SourceSnapshotRegistry();
        var mappingsPath = temp.CreateFile(
            "mappings.json",
            """{ "repositories": [{ "url": "https://gitea.example/shared.git", "solutionPath": "src/Shared.slnx", "assemblies": ["TargetAssembly"] }] }""");
        var settingsPath = temp.CreateFile(
            "appsettings.json",
            """{ "ExternalSources": { "MappingsPath": "mappings.json" } }""");
        var configuration = ExternalSourceConfigurationLoader.Load(settingsPath);
        Assert.True(configuration.Succeeded);
        var orchestrator = new AssemblySourceSelectionOrchestrator(configuration, provider, registry);
        AssemblyContext? context = null;
        AssemblySourceSelectionScope? scope = null;

        var result = await AssemblyAnalysisToolSupport.ExecuteAsync(
            new AssemblyToolExecutionParameters(
                null,
                assemblyPath,
                null,
                100,
                default,
                (_, observed, _) =>
                {
                    context = observed;
                    return new CallToolResult { Content = [] };
                }),
            orchestrator,
            observed => scope = observed);

        Assert.NotEqual(true, result.IsError);
        Assert.NotNull(context);
        Assert.Equal("decompiled", context!.Origin.OriginKind);
        Assert.NotNull(scope);
        Assert.Equal(AssemblySourceSelectionStatus.ProviderDegraded, scope!.Status);
        Assert.Equal(ExternalSourceRepositoryHealth.Degraded, scope.ProviderHealth);
        Assert.Equal(lastGoodRevision, scope.LastGoodRevision);
        Assert.Null(scope.Selection);
        Assert.Contains(
            context.Diagnostics,
            message => message.Contains(
                ExternalSourceConfigurationDiagnosticCodes.RepositoryRefreshDegraded,
                StringComparison.Ordinal));
        Assert.Equal(0, registry.ResidentCount);
    }
}
