#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using AiNetLinter.Configuration;
using AiNetLinter.Mcp.Assemblies;
using AiNetLinter.TestKit;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Assemblies;

[Trait("Category", "Unit")]
public sealed class AssemblySourceMatchResolverTests
{
    [Fact]
    public void Resolve_MatchesConfiguredDllAliasToProjectAssemblyName()
    {
        var mapping = CreateMapping("https://gitea.example/shared.git", "src/Shared.slnx", ["Shared"]);
        using var snapshot = CreateSnapshot(
            mapping,
            "revision-1",
            new ProjectSpec("DifferentProject", "  Shared  ", "shared"));
        using var registry = new SourceSnapshotRegistry();
        using var lease = registry.Acquire(snapshot);

        var result = AssemblySourceMatchResolver.Resolve(lease, mapping, "  SHARED.dll  ");

        var candidate = Assert.Single(result.Candidates);
        Assert.Equal(ExternalSourceMatchState.Matched, result.State);
        Assert.Equal(ExternalSourceMatchConfidence.High, result.Confidence);
        Assert.Equal("SHARED", result.RequestedAssemblyAlias);
        Assert.Same(snapshot.Identity, result.SourceSnapshotIdentity);
        Assert.Same(candidate, result.MatchedCandidate);
        Assert.Equal("DifferentProject", candidate.ProjectName);
        Assert.Equal("  Shared  ", candidate.AssemblyName);
        Assert.Equal(
            [
                "snapshot-identity-matched",
                "explicit-assembly-alias-matched",
                "project-assembly-name-matched",
                "unique-project-matched"
            ],
            result.Evidence);
        Assert.False(snapshot.IsDisposed);
        Assert.Equal(1, registry.ResidentCount);
    }

    [Fact]
    public void Resolve_NormalizesConfiguredAliasesAndProjectAssemblyNamesCaseInsensitively()
    {
        var mapping = CreateMapping(
            " HTTPS://GITEA.EXAMPLE/shared.git ",
            @".\src\..\src/Shared.slnx",
            ["  Shared.DLL  "]);
        using var snapshot = CreateSnapshot(mapping, "revision-1", new ProjectSpec("Project", " shared ", "project"));
        using var registry = new SourceSnapshotRegistry();
        using var lease = registry.Acquire(snapshot);

        var result = AssemblySourceMatchResolver.Resolve(lease, mapping, " SHARED.dll ");

        Assert.Equal(ExternalSourceMatchState.Matched, result.State);
        Assert.Equal("SHARED", result.RequestedAssemblyAlias);
        Assert.Equal(" shared ", Assert.Single(result.Candidates).AssemblyName);
    }

    [Fact]
    public void Resolve_DoesNotUseProjectNameAsAssemblyFallback()
    {
        var mapping = CreateMapping("https://gitea.example/shared.git", "src/Shared.slnx", ["Mapped"]);
        using var snapshot = CreateSnapshot(mapping, "revision-1", new ProjectSpec("Mapped", "Other", "mapped"));
        using var registry = new SourceSnapshotRegistry();
        using var lease = registry.Acquire(snapshot);

        var result = AssemblySourceMatchResolver.Resolve(lease, mapping, "Mapped");

        AssertNoMatch(result, "project-assembly-name-not-matched");
    }

    [Fact]
    public void Resolve_ReturnsNoMatchForUnconfiguredAlias()
    {
        var mapping = CreateMapping("https://gitea.example/shared.git", "src/Shared.slnx", ["Configured"]);
        using var snapshot = CreateSnapshot(mapping, "revision-1", new ProjectSpec("Project", "Requested", "project"));
        using var registry = new SourceSnapshotRegistry();
        using var lease = registry.Acquire(snapshot);

        var result = AssemblySourceMatchResolver.Resolve(lease, mapping, "Requested.dll");

        AssertNoMatch(result, "explicit-assembly-alias-not-configured");
    }

    [Fact]
    public void Resolve_ReturnsNoMatchForForeignSnapshotIdentity()
    {
        var mapping = CreateMapping("https://gitea.example/shared.git", "src/Shared.slnx", ["Shared"]);
        var foreignMapping = CreateMapping("https://gitea.example/other.git", "src/Shared.slnx", ["Shared"]);
        using var snapshot = CreateSnapshot(foreignMapping, "revision-1", new ProjectSpec("Project", "Shared", "project"));
        using var registry = new SourceSnapshotRegistry();
        using var lease = registry.Acquire(snapshot);

        var result = AssemblySourceMatchResolver.Resolve(lease, mapping, "Shared");

        AssertNoMatch(result, "snapshot-identity-mismatched");
        Assert.Empty(result.Candidates);
        Assert.False(snapshot.IsDisposed);
    }

    [Fact]
    public void Resolve_ReturnsNoMatchForInvalidMappingIdentityWithoutThrowing()
    {
        var mapping = CreateMapping("https://gitea.example/shared.git", "src/Shared.slnx", ["Shared"]);
        var invalidMapping = CreateMapping("not-a-url", "src/Shared.slnx", ["Shared"]);
        using var snapshot = CreateSnapshot(mapping, "revision-1", new ProjectSpec("Project", "Shared", "project"));
        using var registry = new SourceSnapshotRegistry();
        using var lease = registry.Acquire(snapshot);

        var result = AssemblySourceMatchResolver.Resolve(lease, invalidMapping, "Shared");

        AssertNoMatch(result, "mapping-identity-invalid");
    }

    [Fact]
    public void Resolve_ReturnsAmbiguousForDuplicateProjectAssemblyNamesInStableOrder()
    {
        var mapping = CreateMapping("https://gitea.example/shared.git", "src/Shared.slnx", ["Shared"]);
        using var snapshot = CreateSnapshot(
            mapping,
            "revision-1",
            new ProjectSpec("Zeta", "Shared", "z"),
            new ProjectSpec("Alpha", " shared ", "a"));
        using var registry = new SourceSnapshotRegistry();
        using var lease = registry.Acquire(snapshot);

        var first = AssemblySourceMatchResolver.Resolve(lease, mapping, "Shared.dll");
        var second = AssemblySourceMatchResolver.Resolve(lease, mapping, "Shared.dll");

        Assert.Equal(ExternalSourceMatchState.Ambiguous, first.State);
        Assert.Equal(ExternalSourceMatchConfidence.None, first.Confidence);
        Assert.Null(first.MatchedCandidate);
        Assert.Equal(["Alpha", "Zeta"], first.Candidates.Select(candidate => candidate.ProjectName));
        Assert.Equal(first.Candidates, second.Candidates);
        Assert.Equal(first.Evidence, second.Evidence);
        Assert.Equal(
            [
                "snapshot-identity-matched",
                "explicit-assembly-alias-matched",
                "project-assembly-name-matched",
                "duplicate-project-assembly-name"
            ],
            first.Evidence);
    }

    [Fact]
    public void Resolve_IgnoresEmptyAndMissingProjectAssemblyNames()
    {
        var mapping = CreateMapping("https://gitea.example/shared.git", "src/Shared.slnx", ["Shared"]);
        using var snapshot = CreateSnapshot(
            mapping,
            "revision-1",
            new ProjectSpec("EmptyAssembly", "   ", "empty"),
            new ProjectSpec("MissingAssembly", string.Empty, "missing"));
        using var registry = new SourceSnapshotRegistry();
        using var lease = registry.Acquire(snapshot);

        var result = AssemblySourceMatchResolver.Resolve(lease, mapping, "Shared");

        AssertNoMatch(result, "project-assembly-name-not-matched");
        Assert.Empty(result.Candidates);
    }

    [Fact]
    public void Resolve_ReturnsNoMatchForDisposedSnapshotWithoutReleasingLease()
    {
        var mapping = CreateMapping("https://gitea.example/shared.git", "src/Shared.slnx", ["Shared"]);
        using var snapshot = CreateSnapshot(mapping, "revision-1", new ProjectSpec("Project", "Shared", "project"));
        using var registry = new SourceSnapshotRegistry();
        using var lease = registry.Acquire(snapshot);
        snapshot.Dispose();

        var result = AssemblySourceMatchResolver.Resolve(lease, mapping, "Shared");

        AssertNoMatch(result, "snapshot-unavailable");
        Assert.Equal(1, registry.ResidentCount);
    }

    private static ExternalSourceMatchResult AssertNoMatch(
        ExternalSourceMatchResult result,
        string finalEvidence)
    {
        Assert.Equal(ExternalSourceMatchState.NoMatch, result.State);
        Assert.Equal(ExternalSourceMatchConfidence.None, result.Confidence);
        Assert.Null(result.MatchedCandidate);
        Assert.Empty(result.Candidates);
        Assert.NotEmpty(result.Evidence);
        Assert.Equal(finalEvidence, result.Evidence[^1]);
        return result;
    }

    private static ExternalSourceMapping CreateMapping(
        string url,
        string solutionPath,
        IReadOnlyList<string> assemblies) =>
        new(url, solutionPath, assemblies);

    private static ExternalSourceSnapshot CreateSnapshot(
        ExternalSourceMapping mapping,
        string revision,
        params ProjectSpec[] projectSpecs)
    {
        var workspace = new AdhocWorkspace();
        var solutionPath = Path.GetFullPath(mapping.SolutionPath);
        var solution = workspace.AddSolution(SolutionInfo.Create(
            SolutionId.CreateNewId(),
            VersionStamp.Create(),
            filePath: solutionPath));
        var solutionDirectory = Path.GetDirectoryName(solutionPath)!;

        foreach (var spec in projectSpecs)
        {
            var projectId = ProjectId.CreateNewId(spec.Name);
            var projectDirectory = Path.Combine(solutionDirectory, spec.DirectoryName);
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
                "namespace MatchTests; public sealed class SourceType { }",
                filePath: Path.Combine(projectDirectory, "Source.cs"));
        }

        return new ExternalSourceSnapshot(
            SourceSnapshotIdentity.Create(mapping, revision),
            solution,
            workspace);
    }

    private sealed record ProjectSpec(string Name, string AssemblyName, string DirectoryName);
}
