#nullable enable

using System;
using System.Threading.Tasks;
using AiNetLinter.Configuration;
using AiNetLinter.FastTests.Mcp.Tools.AssemblyAnalysis;
using AiNetLinter.Mcp.Assemblies.Analysis;
using AiNetLinter.Mcp.Assemblies.Analysis.Factories;
using AiNetLinter.Mcp.Tools.AssemblyAnalysis;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Assemblies;

[Trait("Category", "Unit")]
// @covers AssemblyAnalysisEntry
// @covers AssemblyAnalysisRegistryIdentity
public sealed class AssemblyAnalysisRegistryFreshnessTests
{
    [Fact]
    public async Task Entry_Matches_SourceSnapshotIdentityIsPartOfReuseIdentity()
    {
        using var solution = RoslynTestSolutionFactory.CreateSolution(
            "namespace EntryTest; public sealed class Value { }");
        var identity = SourceSnapshotIdentity.Create(
            new ExternalSourceMapping(
                "https://gitea.example/entry-test.git",
                "src/EntryTest.slnx",
                ["EntryTest"]),
            "revision-1");
        var baseContext = await AssemblyAnalysisRegistryTestContextFactory.CreateAsync(solution.Solution);
        var context = baseContext with
        {
            Origin = baseContext.Origin with { SourceSnapshotIdentity = identity },
        };
        await using var entry = AssemblyAnalysisEntryFactory.Create(
            new AssemblyAnalysisEntryCreateParameters(
                "entry-test.dll",
                solution.Solution,
                context,
                Lifetime: null));
        var fingerprint = new AssemblyFingerprint(
            "entry-test.dll",
            1,
            DateTime.UtcNow,
            context.Origin.ContentHash);

        Assert.True(entry.Matches(fingerprint, identity.StableValue, compareSourceSnapshotIdentity: true));
        Assert.False(entry.Matches(fingerprint, "changed-source-snapshot", compareSourceSnapshotIdentity: true));
    }
}
