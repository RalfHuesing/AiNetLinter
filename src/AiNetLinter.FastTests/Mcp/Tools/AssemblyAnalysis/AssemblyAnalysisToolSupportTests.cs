#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Configuration;
using AiNetLinter.FastTests.Fixtures;
using AiNetLinter.Mcp.Assemblies;
using AiNetLinter.Mcp.Tools.AssemblyAnalysis;
using AiNetLinter.TestKit;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Tools.AssemblyAnalysis;

// @covers AssemblySourceSelectionOrchestrator
[Trait("Category", "Component")]
public sealed class AssemblyAnalysisToolSupportTests
{
    [Fact]
    public async Task ExecuteAsync_WithConfiguredMappingPassesMatchedSelectionToFactory()
    {
        using var temp = TestTempDirectory.Create("assembly-source-support-");
        var assemblyPath = AssemblyTestHelper.EmitAssembly(
            temp,
            "TargetAssembly",
            "namespace Target; public sealed class TargetOnly { }");
        var mapping = CreateMapping(["TargetAssembly"]);
        using var snapshot = ExternalSourceSnapshotTestFactory.CreateSnapshot(
            temp.DirectoryPath,
            mapping,
            new ExternalSourceProjectSpec(
                "SourceProject",
                "TargetAssembly",
                "namespace Source; public sealed class SourceOnly { }"));
        using var registry = new SourceSnapshotRegistry();
        var provider = new AssemblyAnalysisRecordingProvider(new ExternalSourceProviderResult(
            true,
            [new ExternalSourceConfigurationDiagnostic("provider-info", "Quelle bereit", "info", "provider")],
            snapshot));
        var orchestrator = CreateConfiguredOrchestrator(temp, ["targetassembly.dll"], provider, registry);
        using var cancellation = new CancellationTokenSource();
        AssemblyContext? context = null;
        AssemblySourceSelectionScope? observedScope = null;
        var result = await AssemblyAnalysisToolSupport.ExecuteAsync(
            CreateParameters(
                assemblyPath,
                observed =>
                {
                    context = observed;
                    AssertLiveSelection(observedScope, ExternalSourceMatchState.Matched);
                },
                cancellation.Token),
            orchestrator,
            scope => observedScope = scope);
        Assert.NotEqual(true, result.IsError);
        Assert.NotNull(context);
        Assert.NotNull(observedScope);
        Assert.Equal(ExternalSourceMatchState.Matched, observedScope!.Selection!.MatchResult.State);
        Assert.Equal(ExternalSourceProviderFailureKind.None, observedScope.ProviderFailureKind);
        Assert.True(observedScope.Selection.SourceLease.IsDisposed);
        Assert.Equal("TargetAssembly", context!.Identity?.Name);
        Assert.NotNull(context.Compilation.GetTypeByMetadataName("Source.SourceOnly"));
        Assert.Null(context.Compilation.GetTypeByMetadataName("Target.TargetOnly"));
        Assert.Equal("source-backed", context.Origin.OriginKind);
        Assert.Contains(context.Diagnostics, diagnostic => diagnostic.Contains("provider-info", StringComparison.Ordinal));
        Assert.Equal("targetassembly", provider.Mapping!.Assemblies.Single());
        Assert.Equal(cancellation.Token, provider.CancellationToken);
        Assert.Equal(1, provider.CallCount);
    }

    [Fact]
    public async Task ExecuteAsync_HoldsSelectionLeaseThroughResultBuilderAndReleasesItOnce()
    {
        using var temp = TestTempDirectory.Create("assembly-source-lease-");
        var assemblyPath = AssemblyTestHelper.EmitAssembly(
            temp,
            "TargetAssembly",
            "namespace Target; public sealed class TargetOnly { }");
        var mapping = CreateMapping(["TargetAssembly"]);
        using var firstSnapshot = ExternalSourceSnapshotTestFactory.CreateSnapshot(
            temp.DirectoryPath,
            mapping,
            new ExternalSourceProjectSpec("SourceProject", "TargetAssembly", "namespace Source; public sealed class SourceOnly { }"));
        using var duplicateSnapshot = ExternalSourceSnapshotTestFactory.CreateSnapshot(
            temp.DirectoryPath,
            mapping,
            new ExternalSourceProjectSpec("SourceProject", "TargetAssembly", "namespace Source; public sealed class SourceOnly { }"));
        using var registry = new SourceSnapshotRegistry();
        var provider = new AssemblyAnalysisRecordingProvider(
            new ExternalSourceProviderResult(true, [], firstSnapshot),
            new ExternalSourceProviderResult(true, [], duplicateSnapshot));
        var orchestrator = CreateConfiguredOrchestrator(temp, ["TargetAssembly"], provider, registry);
        AssemblySourceSelectionScope? firstScope = null;
        var firstResult = await AssemblyAnalysisToolSupport.ExecuteAsync(
            CreateParameters(
                assemblyPath,
                _ => AssertLiveSelection(firstScope, ExternalSourceMatchState.Matched)),
            orchestrator,
            scope => firstScope = scope);
        Assert.NotEqual(true, firstResult.IsError);
        Assert.NotNull(firstScope);
        Assert.NotNull(firstScope!.Selection);
        var firstSelection = firstScope.Selection!;
        Assert.True(firstSelection.SourceLease.IsDisposed);
        Assert.Equal(1, registry.ResidentCount);
        AssemblySourceSelectionScope? secondScope = null;
        var secondResult = await AssemblyAnalysisToolSupport.ExecuteAsync(
            CreateParameters(
                assemblyPath,
                _ => AssertLiveSelection(secondScope, ExternalSourceMatchState.Matched)),
            orchestrator,
            scope => secondScope = scope);
        Assert.NotEqual(true, secondResult.IsError);
        Assert.NotNull(secondScope);
        Assert.NotNull(secondScope!.Selection);
        var secondSelection = secondScope.Selection!;
        Assert.Same(firstSnapshot, secondSelection.SourceLease.Snapshot);
        Assert.True(duplicateSnapshot.IsDisposed);
        Assert.Equal(1, registry.ResidentCount);
        firstScope.Dispose();
        Assert.True(firstSelection.SourceLease.IsDisposed);
        secondScope.Dispose();
        Assert.True(secondSelection.SourceLease.IsDisposed);
        Assert.False(firstSnapshot.IsDisposed);
        Assert.Equal(1, registry.ResidentCount);
    }

    [Fact]
    public async Task ExecuteAsync_WithoutMappingSkipsProviderAndUsesDecompilationFallback()
    {
        using var temp = TestTempDirectory.Create("assembly-source-no-mapping-");
        var assemblyPath = AssemblyTestHelper.EmitAssembly(
            temp,
            "TargetAssembly",
            "namespace Target; public sealed class TargetOnly { }");
        using var registry = new SourceSnapshotRegistry();
        var provider = new AssemblyAnalysisRecordingProvider(new ExternalSourceProviderResult(false, []));
        var orchestrator = CreateConfiguredOrchestrator(temp, ["OtherAssembly"], provider, registry);
        AssemblyContext? context = null;
        var result = await AssemblyAnalysisToolSupport.ExecuteAsync(
            CreateParameters(assemblyPath, observed => context = observed),
            orchestrator);
        Assert.NotEqual(true, result.IsError);
        Assert.NotNull(context);
        Assert.Equal("decompiled", context!.Origin.OriginKind);
        Assert.NotNull(context.Compilation.GetTypeByMetadataName("Target.TargetOnly"));
        Assert.Equal(0, provider.CallCount);
        Assert.Equal(0, registry.ResidentCount);
    }

    [Theory]
    [InlineData((int)ExternalSourceProviderFailureKind.ProviderUnavailable, ExternalSourceConfigurationDiagnosticCodes.ProviderUnavailable)]
    [InlineData((int)ExternalSourceProviderFailureKind.AuthenticationRequired, ExternalSourceConfigurationDiagnosticCodes.AuthenticationRequired)]
    [InlineData((int)ExternalSourceProviderFailureKind.AccessDenied, ExternalSourceConfigurationDiagnosticCodes.AccessDenied)]
    [InlineData((int)ExternalSourceProviderFailureKind.RepositoryNotFound, ExternalSourceConfigurationDiagnosticCodes.RepositoryNotFound)]
    [InlineData((int)ExternalSourceProviderFailureKind.NetworkUnavailable, ExternalSourceConfigurationDiagnosticCodes.NetworkUnavailable)]
    [InlineData((int)ExternalSourceProviderFailureKind.Timeout, ExternalSourceConfigurationDiagnosticCodes.Timeout)]
    [InlineData((int)ExternalSourceProviderFailureKind.InvalidResponse, ExternalSourceConfigurationDiagnosticCodes.InvalidResponse)]
    public async Task ExecuteAsync_TypedProviderFailurePreservesDiagnosticsAndFallsBack(
        int failureKindValue,
        string diagnosticCode)
    {
        var failureKind = (ExternalSourceProviderFailureKind)failureKindValue;
        using var temp = TestTempDirectory.Create("assembly-source-unavailable-");
        var assemblyPath = AssemblyTestHelper.EmitAssembly(
            temp,
            "TargetAssembly",
            "namespace Target; public sealed class TargetOnly { }");
        using var registry = new SourceSnapshotRegistry();
        var diagnostic = new ExternalSourceConfigurationDiagnostic(
            diagnosticCode,
            "Provider nicht verfügbar",
            "warning",
            "https://gitea.example/shared.git");
        var provider = new AssemblyAnalysisRecordingProvider(new ExternalSourceProviderResult(
            isAvailable: false,
            diagnostics: [diagnostic],
            state: ExternalSourceRepositoryResultState.Create(failureKind)));
        var orchestrator = CreateConfiguredOrchestrator(temp, ["TargetAssembly"], provider, registry);
        AssemblyContext? context = null;
        AssemblySourceSelectionScope? observedScope = null;
        var result = await AssemblyAnalysisToolSupport.ExecuteAsync(
            CreateParameters(assemblyPath, observed => context = observed),
            orchestrator,
            scope => observedScope = scope);
        Assert.NotEqual(true, result.IsError);
        Assert.NotNull(context);
        Assert.Equal("decompiled", context!.Origin.OriginKind);
        Assert.Contains(context.Diagnostics, message => message.Contains(diagnostic.Code, StringComparison.Ordinal));
        Assert.Contains(context.Diagnostics, message => message.Contains(diagnostic.Message, StringComparison.Ordinal));
        Assert.NotNull(observedScope);
        Assert.Equal(failureKind, observedScope!.ProviderFailureKind);
        Assert.Null(observedScope.Selection);
        Assert.Equal(1, provider.CallCount);
        Assert.Equal(0, registry.ResidentCount);
    }

    [Fact]
    public async Task ExecuteAsync_AcquisitionCapabilityFailureProjectsAndFallsBack()
    {
        using var temp = TestTempDirectory.Create("assembly-source-capability-failure-");
        var assemblyPath = AssemblyTestHelper.EmitAssembly(
            temp,
            "TargetAssembly",
            "namespace Target; public sealed class TargetOnly { }");
        var acquisition = ExternalSourceRepositoryAcquisitionResult.Failure(
            ExternalSourceProviderFailureKind.ProviderUnavailable,
            [new ExternalSourceConfigurationDiagnostic(
                ExternalSourceConfigurationDiagnosticCodes.RepositoryCapabilityUnavailable,
                "Lokale Capability nicht verfügbar.",
                "error",
                "$repository")]);
        using var registry = new SourceSnapshotRegistry();
        var provider = new AssemblyAnalysisAcquisitionFailureProvider(acquisition);
        var orchestrator = CreateConfiguredOrchestrator(temp, ["TargetAssembly"], provider, registry);
        AssemblyContext? context = null;
        AssemblySourceSelectionScope? scope = null;

        var result = await AssemblyAnalysisToolSupport.ExecuteAsync(
            CreateParameters(assemblyPath, observed => context = observed),
            orchestrator,
            observed => scope = observed);

        Assert.NotEqual(true, result.IsError);
        Assert.NotNull(context);
        Assert.Equal("decompiled", context!.Origin.OriginKind);
        Assert.NotNull(context.Compilation.GetTypeByMetadataName("Target.TargetOnly"));
        Assert.NotNull(scope);
        Assert.Null(scope!.Selection);
        Assert.Equal(
            ExternalSourceProviderFailureKind.ProviderUnavailable,
            scope.ProviderFailureKind);
        Assert.Contains(
            scope.Diagnostics,
            diagnostic => diagnostic.Code == ExternalSourceConfigurationDiagnosticCodes.RepositoryCapabilityUnavailable);
        Assert.Equal(0, registry.ResidentCount);
    }

    [Fact]
    public async Task ExecuteAsync_InvalidConfigurationStopsBeforeFallback_AndUnusableMatchFallsBackDeterministically()
    {
        using var temp = TestTempDirectory.Create("assembly-source-fallback-");
        var assemblyPath = AssemblyTestHelper.EmitAssembly(
            temp,
            "TargetAssembly",
            "namespace Target; public sealed class TargetOnly { }");
        var loaderDiagnostic = new ExternalSourceConfigurationDiagnostic(
            ExternalSourceConfigurationDiagnosticCodes.SettingsJsonInvalid,
            "Konfiguration ungültig",
            "error",
            "settings");
        using var invalidRegistry = new SourceSnapshotRegistry();
        var invalidProvider = new AssemblyAnalysisRecordingProvider(new ExternalSourceProviderResult(false, []));
        var invalidOrchestrator = new AssemblySourceSelectionOrchestrator(
            ExternalSourceConfigurationLoadResult.Failure([loaderDiagnostic]),
            invalidProvider,
            invalidRegistry);
        AssemblyContext? invalidContext = null;
        AssemblySourceSelectionScope? invalidScope = null;

        var invalidResult = await AssemblyAnalysisToolSupport.ExecuteAsync(
            CreateParameters(assemblyPath, observed => invalidContext = observed),
            invalidOrchestrator,
            scope => invalidScope = scope);
        Assert.False(invalidResult.IsError);
        Assert.Null(invalidContext);
        Assert.NotNull(invalidScope);
        Assert.True(invalidScope!.IsDisposed);
        Assert.Equal(AssemblySourceSelectionStatus.ConfigurationFailure, invalidScope.Status);
        var invalidText = Assert.IsType<TextContentBlock>(Assert.Single(invalidResult.Content)).Text;
        Assert.Contains(loaderDiagnostic.Code, invalidText, StringComparison.Ordinal);
        Assert.Equal(0, invalidProvider.CallCount);
        var mapping = CreateMapping(["TargetAssembly"]);
        using var noMatchSnapshot = ExternalSourceSnapshotTestFactory.CreateSnapshot(
            temp.DirectoryPath,
            mapping,
            new ExternalSourceProjectSpec("SourceProject", "OtherAssembly", "namespace Source; public sealed class OtherOnly { }"));
        using var noMatchRegistry = new SourceSnapshotRegistry();
        var noMatchProvider = new AssemblyAnalysisRecordingProvider(new ExternalSourceProviderResult(true, [], noMatchSnapshot));
        var noMatchOrchestrator = CreateConfiguredOrchestrator(temp, ["TargetAssembly"], noMatchProvider, noMatchRegistry);
        AssemblySourceSelectionScope? noMatchScope = null;
        AssemblyContext? noMatchContext = null;
        await AssemblyAnalysisToolSupport.ExecuteAsync(
            CreateParameters(
                assemblyPath,
                observed =>
                {
                    noMatchContext = observed;
                    AssertLiveSelection(noMatchScope, ExternalSourceMatchState.NoMatch);
                }),
            noMatchOrchestrator,
            scope => noMatchScope = scope);
        Assert.NotNull(noMatchContext);
        Assert.Equal("decompiled", noMatchContext!.Origin.OriginKind);
        Assert.NotNull(noMatchContext.Compilation.GetTypeByMetadataName("Target.TargetOnly"));
        Assert.Null(noMatchContext.Compilation.GetTypeByMetadataName("Source.OtherOnly"));
        Assert.NotNull(noMatchScope);
        Assert.True(noMatchScope!.Selection!.SourceLease.IsDisposed);
        noMatchScope.Dispose();
        using var ambiguousSnapshot = ExternalSourceSnapshotTestFactory.CreateSnapshot(
            temp.DirectoryPath,
            mapping,
            new ExternalSourceProjectSpec("Zeta", "TargetAssembly", "namespace Source; public sealed class ZetaOnly { }"),
            new ExternalSourceProjectSpec("Alpha", "TargetAssembly", "namespace Source; public sealed class AlphaOnly { }"));
        using var ambiguousRegistry = new SourceSnapshotRegistry();
        var ambiguousProvider = new AssemblyAnalysisRecordingProvider(new ExternalSourceProviderResult(true, [], ambiguousSnapshot));
        var ambiguousOrchestrator = CreateConfiguredOrchestrator(temp, ["TargetAssembly"], ambiguousProvider, ambiguousRegistry);
        AssemblySourceSelectionScope? ambiguousScope = null;
        AssemblyContext? ambiguousContext = null;
        await AssemblyAnalysisToolSupport.ExecuteAsync(
            CreateParameters(
                assemblyPath,
                observed =>
                {
                    ambiguousContext = observed;
                    AssertLiveSelection(ambiguousScope, ExternalSourceMatchState.Ambiguous);
                }),
            ambiguousOrchestrator,
            scope => ambiguousScope = scope);
        Assert.NotNull(ambiguousContext);
        Assert.Equal("decompiled", ambiguousContext!.Origin.OriginKind);
        Assert.NotNull(ambiguousContext.Compilation.GetTypeByMetadataName("Target.TargetOnly"));
        Assert.Null(ambiguousContext.Compilation.GetTypeByMetadataName("Source.ZetaOnly"));
        Assert.Null(ambiguousContext.Compilation.GetTypeByMetadataName("Source.AlphaOnly"));
        Assert.NotNull(ambiguousScope);
        Assert.True(ambiguousScope!.Selection!.SourceLease.IsDisposed);
        ambiguousScope.Dispose();
    }

    [Fact]
    public async Task ExecuteAsync_CancellationAfterProviderSnapshotReleasesSelectionLease()
    {
        using var temp = TestTempDirectory.Create("assembly-source-support-cancellation-");
        var assemblyPath = AssemblyTestHelper.EmitAssembly(
            temp,
            "TargetAssembly",
            "namespace Target; public sealed class TargetOnly { }");
        var mapping = CreateMapping(["TargetAssembly"]);
        using var snapshot = ExternalSourceSnapshotTestFactory.CreateSnapshot(
            temp.DirectoryPath,
            mapping,
            new ExternalSourceProjectSpec("SourceProject", "TargetAssembly", "namespace Source; public sealed class SourceOnly { }"));
        using var registry = new SourceSnapshotRegistry();
        using var cancellation = new CancellationTokenSource();
        var provider = new AssemblyAnalysisRecordingProvider((_, token) =>
        {
            var result = new ExternalSourceProviderResult(true, [], snapshot);
            cancellation.Cancel();
            return result;
        });
        var orchestrator = CreateConfiguredOrchestrator(temp, ["TargetAssembly"], provider, registry);
        AssemblySourceSelectionScope? observedScope = null;
        var builderCalled = false;
        var result = await AssemblyAnalysisToolSupport.ExecuteAsync(
            CreateParameters(assemblyPath, _ => builderCalled = true, cancellation.Token),
            orchestrator,
            scope => observedScope = scope);
        Assert.True(result.IsError);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("Assembly-Refresh wurde", text.Text, StringComparison.Ordinal);
        Assert.False(builderCalled);
        Assert.NotNull(observedScope);
        Assert.Equal(ExternalSourceMatchState.Matched, observedScope!.Selection!.MatchResult.State);
        Assert.True(observedScope.Selection.SourceLease.IsDisposed);
        observedScope.Dispose();
        Assert.Equal(cancellation.Token, provider.CancellationToken);
        Assert.Equal("TargetAssembly", provider.Mapping!.Assemblies.Single());
        Assert.Equal(1, provider.CallCount);
        Assert.Equal(1, registry.ResidentCount);
        Assert.False(snapshot.IsDisposed);
    }

    [Fact]
    public async Task ExecuteAsync_ResultBuilderFailureReleasesSelectionLease()
    {
        using var temp = TestTempDirectory.Create("assembly-source-support-builder-failure-");
        var assemblyPath = AssemblyTestHelper.EmitAssembly(
            temp,
            "TargetAssembly",
            "namespace Target; public sealed class TargetOnly { }");
        var mapping = CreateMapping(["TargetAssembly"]);
        using var snapshot = ExternalSourceSnapshotTestFactory.CreateSnapshot(
            temp.DirectoryPath,
            mapping,
            new ExternalSourceProjectSpec("SourceProject", "TargetAssembly", "namespace Source; public sealed class SourceOnly { }"));
        using var registry = new SourceSnapshotRegistry();
        var provider = new AssemblyAnalysisRecordingProvider(new ExternalSourceProviderResult(true, [], snapshot));
        var orchestrator = CreateConfiguredOrchestrator(temp, ["TargetAssembly"], provider, registry);
        AssemblySourceSelectionScope? observedScope = null;
        const string builderError = "Result-Builder fehlgeschlagen";
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            AssemblyAnalysisToolSupport.ExecuteAsync(
                CreateParameters(
                    assemblyPath,
                    _ => { },
                    buildResult: (_, _, _) =>
                    {
                        Assert.NotNull(observedScope);
                        Assert.NotNull(observedScope!.Selection);
                        Assert.False(observedScope.Selection!.SourceLease.IsDisposed);
                        throw new InvalidOperationException(builderError);
                    }),
                orchestrator,
                scope => observedScope = scope));

        Assert.Equal(builderError, exception.Message);
        Assert.NotNull(observedScope);
        Assert.Equal(ExternalSourceMatchState.Matched, observedScope!.Selection!.MatchResult.State);
        Assert.True(observedScope.Selection.SourceLease.IsDisposed);
        observedScope.Dispose();
        Assert.Equal(1, provider.CallCount);
        Assert.Equal(1, registry.ResidentCount);
        Assert.False(snapshot.IsDisposed);
    }
    [Fact]
    public async Task ResolveAsync_PropagatesProviderCancellationAndToken()
    {
        using var temp = TestTempDirectory.Create("assembly-source-cancellation-");
        var assemblyPath = AssemblyTestHelper.EmitAssembly(
            temp,
            "TargetAssembly",
            "namespace Target; public sealed class TargetOnly { }");
        using var registry = new SourceSnapshotRegistry();
        using var cancellation = new CancellationTokenSource();
        var provider = new AssemblyAnalysisRecordingProvider((_, token) => throw new OperationCanceledException(token));
        var orchestrator = CreateConfiguredOrchestrator(temp, ["TargetAssembly"], provider, registry);
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await orchestrator.ResolveAsync(assemblyPath, cancellation.Token));
        Assert.Equal(cancellation.Token, provider.CancellationToken);
        Assert.Equal(1, provider.CallCount);
    }

    private static AssemblySourceSelectionOrchestrator CreateConfiguredOrchestrator(
        TestTempDirectory temp,
        IReadOnlyList<string> assemblies,
        IExternalSourceProvider provider,
        SourceSnapshotRegistry registry)
    {
        var mappingsPath = temp.CreateFile(
            "mappings.json",
            $$"""{ "repositories": [{ "url": "https://gitea.example/shared.git", "solutionPath": "src/Shared.slnx", "assemblies": [{{string.Join(", ", assemblies.Select(assembly => $"\"{assembly}\""))}}] }] }""");
        var settingsPath = temp.CreateFile(
            "appsettings.json",
            $$"""{ "ExternalSources": { "MappingsPath": "mappings.json" } }""");
        var loadResult = ExternalSourceConfigurationLoader.Load(settingsPath);
        Assert.True(loadResult.Succeeded, string.Join(Environment.NewLine, loadResult.Diagnostics.Select(diagnostic => diagnostic.Message)));
        return new AssemblySourceSelectionOrchestrator(loadResult, provider, registry);
    }
    private static void AssertLiveSelection(
        AssemblySourceSelectionScope? scope,
        ExternalSourceMatchState expectedState)
    {
        Assert.NotNull(scope);
        Assert.NotNull(scope!.Selection);
        Assert.Equal(expectedState, scope.Selection!.MatchResult.State);
        Assert.False(scope.Selection.SourceLease.IsDisposed);
    }

    private static AssemblyToolExecutionParameters CreateParameters(
        string assemblyPath,
        Action<AssemblyContext> observe,
        CancellationToken cancellationToken = default,
        Func<string, AssemblyContext, int, CallToolResult>? buildResult = null) =>
        new(
            null,
            assemblyPath,
            null,
            100,
            cancellationToken,
            buildResult ?? ((_, context, _) =>
            {
                observe(context);
                return new CallToolResult { Content = [] };
            }));

    private static ExternalSourceMapping CreateMapping(IReadOnlyList<string> assemblies) =>
        new("https://gitea.example/shared.git", "src/Shared.slnx", assemblies);

}
