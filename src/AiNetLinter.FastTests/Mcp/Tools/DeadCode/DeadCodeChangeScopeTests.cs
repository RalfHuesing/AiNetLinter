#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AiNetLinter.Mcp.Tools.Verify.DeadCode;
using Microsoft.CodeAnalysis.Text;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Tools.DeadCode;

[Trait("Category", "Component")]
public sealed class DeadCodeChangeScopeTests
{
    [Fact]
    public async Task MissingComparisonBasisReportsPartialCoverage()
    {
        using var fixture = RoslynTestSolutionFactory.CreateSolution(@"C:\ainetlinter-virtual\NoGit.slnx",
            new ProjectSpec("Host", [("Code.cs", "public sealed class Orphan { }")]));
        var document = fixture.Solution.Projects.Single().Documents.Single();
        var result = await DeadCodeAdvisoryScanner.ScanAsync(fixture.Solution, new(
            Accessibility: DeadCodeAccessibilityFilter.All,
            ScopeFiles: new HashSet<string> { document.FilePath! }));
        Assert.Equal("partial", result.Summary.Status);
        Assert.Equal("unavailable", result.Summary.Coverage!.ChangesBasis);
        Assert.Contains(result.DeadSymbols, entry => entry.SymbolName == "Orphan");
    }

    [Fact]
    public async Task RemovedLastCallFindsUnchangedTargetAndRanksItFirst()
    {
        using var fixture = RoslynTestSolutionFactory.CreateSolution(@"C:\ainetlinter-virtual\Changes.slnx",
            new ProjectSpec("Host", [
                ("Service.cs", "public static class Service { public static void Work() { } }"),
                ("Caller.cs", "public static class Caller { public static void Run() => Service.Work(); }")]));
        var caller = fixture.Solution.Projects.Single().Documents.Single(document => document.Name == "Caller.cs");
        var current = fixture.Solution.WithDocumentText(caller.Id,
            SourceText.From("public static class Caller { public static void Run() { } }"));
        var result = await DeadCodeAdvisoryScanner.ScanAsync(current, new(
            Accessibility: DeadCodeAccessibilityFilter.All, Kind: DeadCodeKindFilter.Method,
            ScopeFiles: new HashSet<string>(StringComparer.OrdinalIgnoreCase) { caller.FilePath! },
            PreviousSolution: fixture.Solution));
        var target = Assert.Single(result.DeadSymbols, entry => entry.SymbolName == "Work");
        Assert.Equal(0, target.Priority);
        Assert.Equal("available", result.Summary.Coverage!.ChangesBasis);
    }
}
