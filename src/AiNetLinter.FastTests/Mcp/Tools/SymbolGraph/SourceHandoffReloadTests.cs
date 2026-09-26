#nullable enable

using System;
using System.Linq;
using System.Threading;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Tools.SymbolGraph;
using AiNetLinter.TestKit;
using Microsoft.CodeAnalysis;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Tools.SymbolGraph;

[Trait("Category", "Unit")]
public sealed class SourceHandoffReloadTests
{
    [Fact]
    public async Task SourceHandoffResolvesSameProjectAfterSolutionReload()
    {
        const string solutionPath = @"C:\current\workspace.slnx";
        var specs = new[]
        {
            new ProjectSpec("First", [("First.cs", "namespace Probe; public sealed class Current { }")]),
            new ProjectSpec("Second", [("Second.cs", "namespace Probe; public sealed class Current { }")]),
        };
        using var first = RoslynTestSolutionFactory.CreateSolution(solutionPath, specs);
        using var reloaded = RoslynTestSolutionFactory.CreateSolution(solutionPath, specs);
        var firstProject = first.Solution.Projects.Single(project => project.Name == "First");
        var symbol = (await firstProject.GetCompilationAsync())!.GetTypeByMetadataName("Probe.Current")!;
        var identity = AnalysisSymbolIdentity.ForSource(solutionPath, new string('b', 64), first.Solution);
        var reloadedIdentity = AnalysisSymbolIdentity.ForSource(solutionPath, new string('b', 64), reloaded.Solution);
        var handoff = identity.FormatHandoff(symbol, firstProject.Id)!;

        Assert.NotEqual(firstProject.Id, reloaded.Solution.Projects.Single(project => project.Name == "First").Id);
        var (resolved, error) = await SymbolIdentifierResolver.TryResolveByStableIdAsync(
            reloaded.Solution,
            handoff,
            CancellationToken.None,
            reloadedIdentity);

        Assert.Null(error);
        var reloadedFirst = reloaded.Solution.Projects.Single(project => project.Name == "First");
        var expected = (await reloadedFirst.GetCompilationAsync())!.GetTypeByMetadataName("Probe.Current");
        Assert.True(SymbolEqualityComparer.Default.Equals(expected, resolved));
    }
}
