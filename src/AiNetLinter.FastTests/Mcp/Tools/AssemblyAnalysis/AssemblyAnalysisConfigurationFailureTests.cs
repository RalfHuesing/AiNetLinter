#nullable enable

using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using AiNetLinter.Configuration;
using AiNetLinter.Mcp.Assemblies;
using AiNetLinter.Mcp.Tools.AssemblyAnalysis;
using AiNetLinter.TestKit;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Tools.AssemblyAnalysis;

[Trait("Category", "Component")]
public sealed class AssemblyAnalysisConfigurationFailureTests
{
    [Fact]
    public async Task ExecuteAsync_InvalidLoadedCacheRootStopsBeforeProviderAndDecompilation()
    {
        using var temp = TestTempDirectory.Create("assembly-source-config-failure-");
        var assemblyPath = AssemblyTestHelper.EmitAssembly(
            temp,
            "TargetAssembly",
            "namespace Target; public sealed class TargetOnly { }");
        temp.CreateFile("mappings.json", ValidMappings());
        const string rawCacheRoot = "https:/user:secret@example.invalid/cache";
        var settingsPath = temp.CreateFile(
            "appsettings.json",
            $$"""{ "ExternalSources": { "MappingsPath": "mappings.json", "CacheRoot": {{JsonSerializer.Serialize(rawCacheRoot)}} } }""");
        var provider = new AssemblyAnalysisRecordingProvider();
        using var registry = new SourceSnapshotRegistry();
        var orchestrator = AssemblySourceSelectionOrchestrator.CreateFromSettings(
            settingsPath,
            provider,
            registry);
        AssemblySourceSelectionScope? observedScope = null;
        var builderCalled = false;

        var result = await AssemblyAnalysisToolSupport.ExecuteAsync(
            new AssemblyToolExecutionParameters(
                null,
                assemblyPath,
                null,
                100,
                default,
                (_, _, _) =>
                {
                    builderCalled = true;
                    return new CallToolResult { Content = [] };
                }),
            orchestrator,
            scope => observedScope = scope);

        Assert.False(result.IsError);
        Assert.NotNull(observedScope);
        Assert.True(observedScope!.IsDisposed);
        Assert.Equal(AssemblySourceSelectionStatus.ConfigurationFailure, observedScope.Status);
        Assert.Null(observedScope.Selection);
        Assert.Equal(ExternalSourceProviderFailureKind.None, observedScope.ProviderFailureKind);
        Assert.Equal(0, provider.CallCount);
        Assert.Equal(0, registry.ResidentCount);
        Assert.False(builderCalled);

        var resultText = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.Contains(ExternalSourceConfigurationDiagnosticCodes.CacheRootInvalid, resultText, StringComparison.Ordinal);
        Assert.DoesNotContain(rawCacheRoot, resultText, StringComparison.Ordinal);
        Assert.DoesNotContain("secret", resultText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("decompiled", resultText, StringComparison.OrdinalIgnoreCase);
        Assert.Null(result.StructuredContent);
    }

    [Fact]
    public async Task ExecuteAsync_EmptyConfigurationFailureStopsBeforeProviderAndDecompilation()
    {
        using var temp = TestTempDirectory.Create("assembly-source-empty-config-failure-");
        var assemblyPath = AssemblyTestHelper.EmitAssembly(
            temp,
            "TargetAssembly",
            "namespace Target; public sealed class TargetOnly { }");
        var provider = new AssemblyAnalysisRecordingProvider(new ExternalSourceProviderResult(false, []));
        using var registry = new SourceSnapshotRegistry();
        var orchestrator = new AssemblySourceSelectionOrchestrator(
            ExternalSourceConfigurationLoadResult.Failure([]),
            provider,
            registry);
        AssemblySourceSelectionScope? observedScope = null;
        var builderCalled = false;

        var result = await AssemblyAnalysisToolSupport.ExecuteAsync(
            new AssemblyToolExecutionParameters(
                null,
                assemblyPath,
                null,
                100,
                default,
                (_, _, _) =>
                {
                    builderCalled = true;
                    return new CallToolResult { Content = [] };
                }),
            orchestrator,
            scope =>
            {
                observedScope = scope;
            });

        Assert.False(result.IsError);
        Assert.NotNull(observedScope);
        Assert.True(observedScope!.IsDisposed);
        Assert.Equal(AssemblySourceSelectionStatus.ConfigurationFailure, observedScope.Status);
        Assert.Null(observedScope.Selection);
        Assert.True(observedScope.LoaderDiagnostics.IsEmpty);
        Assert.True(observedScope.Diagnostics.IsEmpty);
        Assert.False(builderCalled);
        Assert.Equal(0, provider.CallCount);
        Assert.Equal(0, registry.ResidentCount);

        var resultText = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.Contains(ExternalSourceConfigurationDiagnosticCodes.ExternalSourcesSectionInvalid, resultText, StringComparison.Ordinal);
        Assert.Contains("ExternalSources-Konfiguration korrigieren", resultText, StringComparison.Ordinal);
        Assert.DoesNotContain("decompiled", resultText, StringComparison.OrdinalIgnoreCase);
        Assert.Null(result.StructuredContent);
    }

    private static string ValidMappings() =>
        "{ \"repositories\": [{ \"url\": \"https://gitea.example/shared.git\", "
        + "\"solutionPath\": \"src/Shared.slnx\", \"assemblies\": [\"TargetAssembly\"] }] }";
}
