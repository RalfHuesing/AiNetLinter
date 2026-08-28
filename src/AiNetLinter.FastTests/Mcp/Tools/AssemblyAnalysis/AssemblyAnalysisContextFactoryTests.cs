#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Configuration;
using AiNetLinter.Mcp.Assemblies;
using AiNetLinter.Mcp.Tools.AssemblyAnalysis;
using AiNetLinter.TestKit;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
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
        using var snapshot = CreateSnapshot(
            temp.DirectoryPath,
            mapping,
            new SourceProjectSpec("SourceProject", "TargetAssembly", "namespace Source; public sealed class SourceOnly { }"));
        using var registry = new SourceSnapshotRegistry();
        using var lease = registry.Acquire(snapshot);

        var match = AssemblySourceMatchResolver.Resolve(lease, mapping, "TargetAssembly.dll");
        var selection = AssemblySourceSelection.Create(lease, match);
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
        using var noMatchSnapshot = CreateSnapshot(
            temp.DirectoryPath,
            noMatchMapping,
            new SourceProjectSpec("NoMatchProject", "TargetAssembly", "namespace Source; public sealed class NoMatchOnly { }"));
        using var noMatchRegistry = new SourceSnapshotRegistry();
        using var noMatchLease = noMatchRegistry.Acquire(noMatchSnapshot);
        var noMatch = AssemblySourceMatchResolver.Resolve(noMatchLease, noMatchMapping, "TargetAssembly");
        var noMatchSelection = AssemblySourceSelection.Create(noMatchLease, noMatch);
        Assert.NotNull(noMatchSelection);

        var noMatchResult = await AssemblyAnalysisContextFactory.CreateAsync(new AssemblyAnalysisContextRequest(
            assemblyPath,
            null,
            null,
            noMatchSelection,
            CancellationToken.None));
        AssertDecompiledFallback(noMatchResult, "Source.NoMatchOnly");

        var ambiguousMapping = CreateMapping("https://gitea.example/ambiguous.git", "src/Ambiguous.slnx", ["TargetAssembly"]);
        using var ambiguousSnapshot = CreateSnapshot(
            temp.DirectoryPath,
            ambiguousMapping,
            new SourceProjectSpec("Zeta", "TargetAssembly", "namespace Source; public sealed class ZetaOnly { }"),
            new SourceProjectSpec("Alpha", "TargetAssembly", "namespace Source; public sealed class AlphaOnly { }"));
        using var ambiguousRegistry = new SourceSnapshotRegistry();
        using var ambiguousLease = ambiguousRegistry.Acquire(ambiguousSnapshot);
        var ambiguous = AssemblySourceMatchResolver.Resolve(ambiguousLease, ambiguousMapping, "TargetAssembly");
        var ambiguousSelection = AssemblySourceSelection.Create(ambiguousLease, ambiguous);
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
        using var sourceSnapshot = CreateSnapshot(
            temp.DirectoryPath,
            mapping,
            new SourceProjectSpec("SourceProject", "TargetAssembly", "namespace Source; public sealed class SourceOnly { }"));
        using var foreignSnapshot = CreateSnapshot(
            temp.DirectoryPath,
            foreignMapping,
            new SourceProjectSpec("ForeignProject", "TargetAssembly", "namespace Foreign; public sealed class ForeignOnly { }"));
        using var sourceRegistry = new SourceSnapshotRegistry();
        using var foreignRegistry = new SourceSnapshotRegistry();
        using var sourceLease = sourceRegistry.Acquire(sourceSnapshot);
        using var foreignLease = foreignRegistry.Acquire(foreignSnapshot);

        var sourceMatch = AssemblySourceMatchResolver.Resolve(sourceLease, mapping, "TargetAssembly");
        Assert.Null(AssemblySourceSelection.Create(foreignLease, sourceMatch));

        var disposedLeaseSelection = AssemblySourceSelection.Create(sourceLease, sourceMatch);
        Assert.NotNull(disposedLeaseSelection);
        sourceLease.Dispose();
        var disposedLeaseResult = await AssemblyAnalysisContextFactory.CreateAsync(new AssemblyAnalysisContextRequest(
            assemblyPath,
            null,
            null,
            disposedLeaseSelection,
            CancellationToken.None));
        AssertDecompiledFallback(disposedLeaseResult, "Source.SourceOnly");

        using var secondSourceSnapshot = CreateSnapshot(
            temp.DirectoryPath,
            mapping,
            new SourceProjectSpec("SecondSourceProject", "TargetAssembly", "namespace Source; public sealed class SecondSourceOnly { }"));
        using var secondRegistry = new SourceSnapshotRegistry();
        using var secondLease = secondRegistry.Acquire(secondSourceSnapshot);
        var secondMatch = AssemblySourceMatchResolver.Resolve(secondLease, mapping, "TargetAssembly");
        var missingProjectMatch = secondMatch with
        {
            MatchedCandidate = secondMatch.MatchedCandidate! with { ProjectId = ProjectId.CreateNewId("MissingProject") }
        };
        var missingProjectSelection = AssemblySourceSelection.Create(secondLease, missingProjectMatch);
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
        using var snapshot = CreateSnapshot(
            temp.DirectoryPath,
            mapping,
            new SourceProjectSpec("SourceProject", "TargetAssembly", "namespace Source; public sealed class SourceOnly { }"));
        var registry = new SourceSnapshotRegistry();
        var lease = registry.Acquire(snapshot);

        var match = AssemblySourceMatchResolver.Resolve(lease, mapping, "TargetAssembly");
        var selection = AssemblySourceSelection.Create(lease, match);
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
        var noMatchSelection = AssemblySourceSelection.Create(lease, noMatch);
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
        Assert.True(snapshot.IsDisposed);
        lease.Dispose();
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

    private static ExternalSourceSnapshot CreateSnapshot(
        string rootPath,
        ExternalSourceMapping mapping,
        params SourceProjectSpec[] projectSpecs)
    {
        var workspace = new AdhocWorkspace();
        var solutionPath = Path.Combine(rootPath, "ExternalSource.slnx");
        var solution = workspace.AddSolution(SolutionInfo.Create(
            SolutionId.CreateNewId(),
            VersionStamp.Create(),
            filePath: solutionPath));
        var solutionDirectory = Path.GetDirectoryName(solutionPath)!;

        foreach (var spec in projectSpecs)
        {
            var projectId = ProjectId.CreateNewId(spec.Name);
            var projectDirectory = Path.Combine(solutionDirectory, spec.Name);
            var projectPath = Path.Combine(projectDirectory, spec.Name + ".csproj");
            var projectInfo = ProjectInfo.Create(
                    projectId,
                    VersionStamp.Create(),
                    spec.Name,
                    spec.AssemblyName,
                    LanguageNames.CSharp,
                    filePath: projectPath)
                .WithMetadataReferences(RoslynTestSolutionFactory.CoreReferences)
                .WithCompilationOptions(new CSharpCompilationOptions(
                    OutputKind.DynamicallyLinkedLibrary,
                    nullableContextOptions: NullableContextOptions.Enable));
            solution = solution.AddProject(projectInfo);
            solution = solution.AddDocument(
                DocumentId.CreateNewId(projectId),
                "Source.cs",
                spec.Source,
                filePath: Path.Combine(projectDirectory, "Source.cs"));
        }

        return new ExternalSourceSnapshot(
            SourceSnapshotIdentity.Create(mapping, "revision-1"),
            solution,
            workspace);
    }

    private sealed record SourceProjectSpec(string Name, string AssemblyName, string Source);
}
