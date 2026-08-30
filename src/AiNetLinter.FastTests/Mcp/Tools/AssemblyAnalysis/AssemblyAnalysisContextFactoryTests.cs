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
using Microsoft.CodeAnalysis;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Tools.AssemblyAnalysis;

[Trait("Category", "Component")]
public sealed class AssemblyAnalysisContextFactoryTests
{
    [Fact]
    public async Task CreateAsync_UsesMatchedReadOnlySourceProjectWithoutDecompilation()
    {
        using var temp = TestTempDirectory.Create("assembly-context-source-");
        var assemblyPath = AssemblyTestHelper.EmitAssembly(
            temp,
            "TargetAssembly",
            "namespace Target; public sealed class TargetOnly { }");
        var mapping = CreateMapping("https://gitea.example/source.git", "src/Source.slnx", ["TargetAssembly"]);
        using var snapshot = ExternalSourceSnapshotTestFactory.CreateSnapshot(
            temp.DirectoryPath,
            mapping,
            new ExternalSourceProjectSpec("SourceProject", "TargetAssembly", "namespace Source; public sealed class SourceOnly { }"));
        using var registry = new SourceSnapshotRegistry();
        using var lease = registry.Acquire(snapshot);

        var match = AssemblySourceMatchResolver.Resolve(lease, mapping, "TargetAssembly.dll");
        var selection = AssemblySourceSelection.Create(new(lease, match));
        Assert.NotNull(selection);

        var result = await AssemblyAnalysisContextFactory.CreateAsync(new AssemblyAnalysisContextRequest(
            assemblyPath,
            null,
            null,
            selection,
            CancellationToken.None));

        var context = AssertContext(result);
        Assert.Equal("TargetAssembly", context.Identity?.Name);
        Assert.NotNull(context.Compilation.GetTypeByMetadataName("Source.SourceOnly"));
        Assert.Null(context.Compilation.GetTypeByMetadataName("Target.TargetOnly"));
        Assert.Equal("source-backed", context.Origin.OriginKind);
        Assert.False(context.Origin.IsDecompiled);
        Assert.Equal(0, context.Generation);
        Assert.Equal("high", context.Origin.Confidence);
        Assert.Empty(context.Origin.GeneratedDocumentPath);
        Assert.Same(snapshot.Identity, context.Origin.SourceSnapshotIdentity);
        Assert.Equal(
            snapshot.Solution.GetProject(match.MatchedCandidate!.ProjectId)!.FilePath,
            context.Origin.SourceProjectPath);
        Assert.Empty(context.Diagnostics);
        Assert.False(lease.IsDisposed);
        Assert.False(snapshot.IsDisposed);
        Assert.Equal(1, registry.ResidentCount);
    }

    [Fact]
    public async Task CreateAsync_NoMatchAndAmbiguousUseExistingDecompilationFallback()
    {
        using var temp = TestTempDirectory.Create("assembly-context-fallback-");
        var assemblyPath = AssemblyTestHelper.EmitAssembly(
            temp,
            "TargetAssembly",
            "namespace Target; public sealed class TargetOnly { }");
        var noMatchMapping = CreateMapping("https://gitea.example/no-match.git", "src/NoMatch.slnx", ["OtherAssembly"]);
        using var noMatchSnapshot = ExternalSourceSnapshotTestFactory.CreateSnapshot(
            temp.DirectoryPath,
            noMatchMapping,
            new ExternalSourceProjectSpec("NoMatchProject", "TargetAssembly", "namespace Source; public sealed class NoMatchOnly { }"));
        using var noMatchRegistry = new SourceSnapshotRegistry();
        using var noMatchLease = noMatchRegistry.Acquire(noMatchSnapshot);
        var noMatch = AssemblySourceMatchResolver.Resolve(noMatchLease, noMatchMapping, "TargetAssembly");
        var noMatchSelection = AssemblySourceSelection.Create(new(noMatchLease, noMatch));
        Assert.NotNull(noMatchSelection);

        var noMatchResult = await AssemblyAnalysisContextFactory.CreateAsync(new AssemblyAnalysisContextRequest(
            assemblyPath,
            null,
            null,
            noMatchSelection,
            CancellationToken.None));
        AssertDecompiledFallback(noMatchResult, "Source.NoMatchOnly");

        var ambiguousMapping = CreateMapping("https://gitea.example/ambiguous.git", "src/Ambiguous.slnx", ["TargetAssembly"]);
        using var ambiguousSnapshot = ExternalSourceSnapshotTestFactory.CreateSnapshot(
            temp.DirectoryPath,
            ambiguousMapping,
            new ExternalSourceProjectSpec("Zeta", "TargetAssembly", "namespace Source; public sealed class ZetaOnly { }"),
            new ExternalSourceProjectSpec("Alpha", "TargetAssembly", "namespace Source; public sealed class AlphaOnly { }"));
        using var ambiguousRegistry = new SourceSnapshotRegistry();
        using var ambiguousLease = ambiguousRegistry.Acquire(ambiguousSnapshot);
        var ambiguous = AssemblySourceMatchResolver.Resolve(ambiguousLease, ambiguousMapping, "TargetAssembly");
        var ambiguousSelection = AssemblySourceSelection.Create(new(ambiguousLease, ambiguous));
        Assert.NotNull(ambiguousSelection);

        var ambiguousResult = await AssemblyAnalysisContextFactory.CreateAsync(new AssemblyAnalysisContextRequest(
            assemblyPath,
            null,
            null,
            ambiguousSelection,
            CancellationToken.None));
        AssertDecompiledFallback(ambiguousResult, "Source.AlphaOnly");
    }

    [Fact]
    public async Task CreateAsync_WithoutSourceSelectionRepresentsUnavailableFallback()
    {
        using var temp = TestTempDirectory.Create("assembly-context-unavailable-");
        var assemblyPath = AssemblyTestHelper.EmitAssembly(
            temp,
            "TargetAssembly",
            "namespace Target; public sealed class TargetOnly { }");

        var result = await AssemblyAnalysisContextFactory.CreateAsync(
            assemblyPath,
            null,
            null,
            CancellationToken.None);

        AssertDecompiledFallback(result, "Source.SourceOnly");
    }

    [Fact]
    public async Task CreateAsync_UsesDecompilationForIdentityMismatchDisposedLeaseAndMissingProject()
    {
        using var temp = TestTempDirectory.Create("assembly-context-invalid-selection-");
        var assemblyPath = AssemblyTestHelper.EmitAssembly(
            temp,
            "TargetAssembly",
            "namespace Target; public sealed class TargetOnly { }");
        var mapping = CreateMapping("https://gitea.example/source.git", "src/Source.slnx", ["TargetAssembly"]);
        var foreignMapping = CreateMapping("https://gitea.example/foreign.git", "src/Source.slnx", ["TargetAssembly"]);
        using var sourceSnapshot = ExternalSourceSnapshotTestFactory.CreateSnapshot(
            temp.DirectoryPath,
            mapping,
            new ExternalSourceProjectSpec("SourceProject", "TargetAssembly", "namespace Source; public sealed class SourceOnly { }"));
        using var foreignSnapshot = ExternalSourceSnapshotTestFactory.CreateSnapshot(
            temp.DirectoryPath,
            foreignMapping,
            new ExternalSourceProjectSpec("ForeignProject", "TargetAssembly", "namespace Foreign; public sealed class ForeignOnly { }"));
        using var sourceRegistry = new SourceSnapshotRegistry();
        using var foreignRegistry = new SourceSnapshotRegistry();
        using var sourceLease = sourceRegistry.Acquire(sourceSnapshot);
        using var foreignLease = foreignRegistry.Acquire(foreignSnapshot);

        var sourceMatch = AssemblySourceMatchResolver.Resolve(sourceLease, mapping, "TargetAssembly");
        Assert.Null(AssemblySourceSelection.Create(new(foreignLease, sourceMatch)));

        var disposedLeaseSelection = AssemblySourceSelection.Create(new(sourceLease, sourceMatch));
        Assert.NotNull(disposedLeaseSelection);
        sourceLease.Dispose();
        var disposedLeaseResult = await AssemblyAnalysisContextFactory.CreateAsync(new AssemblyAnalysisContextRequest(
            assemblyPath,
            null,
            null,
            disposedLeaseSelection,
            CancellationToken.None));
        AssertDecompiledFallback(disposedLeaseResult, "Source.SourceOnly");

        using var secondSourceSnapshot = ExternalSourceSnapshotTestFactory.CreateSnapshot(
            temp.DirectoryPath,
            mapping,
            new ExternalSourceProjectSpec("SecondSourceProject", "TargetAssembly", "namespace Source; public sealed class SecondSourceOnly { }"));
        using var secondRegistry = new SourceSnapshotRegistry();
        using var secondLease = secondRegistry.Acquire(secondSourceSnapshot);
        var secondMatch = AssemblySourceMatchResolver.Resolve(secondLease, mapping, "TargetAssembly");
        var missingProjectMatch = secondMatch with
        {
            MatchedCandidate = secondMatch.MatchedCandidate! with { ProjectId = ProjectId.CreateNewId("MissingProject") }
        };
        var missingProjectSelection = AssemblySourceSelection.Create(new(secondLease, missingProjectMatch));
        Assert.NotNull(missingProjectSelection);

        var missingProjectResult = await AssemblyAnalysisContextFactory.CreateAsync(new AssemblyAnalysisContextRequest(
            assemblyPath,
            null,
            null,
            missingProjectSelection,
            CancellationToken.None));
        AssertDecompiledFallback(missingProjectResult, "Source.SecondSourceOnly");
    }

    [Fact]
    public async Task CreateAsync_DoesNotReleaseSourceLeaseOrSnapshotOwnership()
    {
        using var temp = TestTempDirectory.Create("assembly-context-ownership-");
        var assemblyPath = AssemblyTestHelper.EmitAssembly(
            temp,
            "TargetAssembly",
            "namespace Target; public sealed class TargetOnly { }");
        var mapping = CreateMapping("https://gitea.example/source.git", "src/Source.slnx", ["TargetAssembly"]);
        using var snapshot = ExternalSourceSnapshotTestFactory.CreateSnapshot(
            temp.DirectoryPath,
            mapping,
            new ExternalSourceProjectSpec("SourceProject", "TargetAssembly", "namespace Source; public sealed class SourceOnly { }"));
        var registry = new SourceSnapshotRegistry();
        var lease = registry.Acquire(snapshot);

        var match = AssemblySourceMatchResolver.Resolve(lease, mapping, "TargetAssembly");
        var selection = AssemblySourceSelection.Create(new(lease, match));
        Assert.NotNull(selection);
        var sourceResult = await AssemblyAnalysisContextFactory.CreateAsync(new AssemblyAnalysisContextRequest(
            assemblyPath,
            null,
            null,
            selection,
            CancellationToken.None));
        var sourceContext = AssertContext(sourceResult);
        Assert.Equal("source-backed", sourceContext.Origin.OriginKind);

        var noMatchMapping = CreateMapping("https://gitea.example/source.git", "src/Source.slnx", ["OtherAssembly"]);
        var noMatch = AssemblySourceMatchResolver.Resolve(lease, noMatchMapping, "TargetAssembly");
        var noMatchSelection = AssemblySourceSelection.Create(new(lease, noMatch));
        Assert.NotNull(noMatchSelection);
        var fallbackResult = await AssemblyAnalysisContextFactory.CreateAsync(new AssemblyAnalysisContextRequest(
            assemblyPath,
            null,
            null,
            noMatchSelection,
            CancellationToken.None));
        AssertDecompiledFallback(fallbackResult, "Source.SourceOnly");

        Assert.False(lease.IsDisposed);
        Assert.False(snapshot.IsDisposed);
        Assert.Equal(1, registry.ResidentCount);

        registry.Dispose();
        registry.Dispose();
        Assert.False(snapshot.IsDisposed);
        lease.Dispose();
        Assert.True(snapshot.IsDisposed);
    }

    private static AssemblyContext AssertContext((AssemblyContext? Context, string? Error) result)
    {
        Assert.Null(result.Error);
        Assert.NotNull(result.Context);
        return result.Context!;
    }

    private static void AssertDecompiledFallback(
        (AssemblyContext? Context, string? Error) result,
        string sourceOnlyType)
    {
        var context = AssertContext(result);
        Assert.Equal("decompiled", context.Origin.OriginKind);
        Assert.True(context.Origin.IsDecompiled);
        Assert.NotEqual(0, context.Generation);
        Assert.NotNull(context.Compilation.GetTypeByMetadataName("Target.TargetOnly"));
        Assert.Null(context.Compilation.GetTypeByMetadataName(sourceOnlyType));
        Assert.Null(context.Origin.SourceSnapshotIdentity);
        Assert.Null(context.Origin.SourceProjectPath);
    }

    private static ExternalSourceMapping CreateMapping(
        string url,
        string solutionPath,
        IReadOnlyList<string> assemblies) =>
        new(url, solutionPath, assemblies);

}
