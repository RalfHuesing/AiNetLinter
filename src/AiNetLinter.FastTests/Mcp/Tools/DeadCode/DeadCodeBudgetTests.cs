#nullable enable

using System.Threading.Tasks;
using AiNetLinter.Configuration;
using AiNetLinter.Mcp.Tools.Verify;
using AiNetLinter.Mcp.Tools.Verify.DeadCode;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Tools.DeadCode;

[Trait("Category", "Component")]
public sealed class DeadCodeBudgetTests
{
    [Fact]
    public async Task BudgetAfterFirstDocumentRetainsOnlyExaminedCandidatesAndLastPageStaysPartial()
    {
        using var fixture = RoslynTestSolutionFactory.CreateSolution(@"C:\ainetlinter-virtual\LateBudget.slnx",
            new ProjectSpec("Host", [("First.cs", "public sealed class First { }"), ("Second.cs", "public sealed class Second { }")]));
        var result = await DeadCodeAdvisoryScanner.ScanAsync(fixture.Solution,
            new(Accessibility: DeadCodeAccessibilityFilter.All, Clock: new DocumentBoundaryClock()));
        Assert.Equal("First", Assert.Single(result.DeadSymbols).SymbolName);
        Assert.Equal(0, result.Summary.Undecidable);
        Assert.Equal(1, result.Summary.Coverage!.ProcessedDocuments);
        Assert.Equal(1, result.Summary.Coverage.OpenDocuments);
        Assert.True(result.Summary.Coverage.ReferencesComplete);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(GetVerifyAdvisoriesTool.Render(result).Content)).Text;
        Assert.Contains("scanCompleteness=partial", text);
        Assert.Contains("listCompleteness=complete", text);
        Assert.Contains("continuationToken=none", text);
    }

    private sealed class DocumentBoundaryClock : System.TimeProvider
    {
        private int timestamps;
        public override long TimestampFrequency => global::System.TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => ++timestamps <= 2 ? 0 : 11 * global::System.TimeSpan.TicksPerSecond;
    }

    [Fact]
    public async Task ExhaustedPreparationBudgetLeavesOpenWorkAndNoNegativeFindings()
    {
        using var fixture = RoslynTestSolutionFactory.CreateSolution(@"C:\ainetlinter-virtual\Budget.slnx",
            new ProjectSpec("Host", [("Code.cs", "public sealed class Orphan { public void Run() { } }")]));
        var result = await DeadCodeAdvisoryScanner.ScanAsync(fixture.Solution,
            new(Config: TestHelper.CreateDefaultConfig() with { DeadCode = new DeadCodeConfig { VerifyBudgetSeconds = 0 } }));
        Assert.Empty(result.DeadSymbols);
        Assert.Equal(0, result.Summary.Undecidable);
        Assert.Equal("partial", result.Summary.Status);
        Assert.Equal(1, result.Summary.Coverage!.OpenDocuments);
        Assert.Equal("budget", result.Summary.Coverage.StopReason);
        Assert.False(result.Summary.Coverage.ReferencesComplete);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(GetVerifyAdvisoriesTool.Render(result).Content)).Text;
        Assert.Contains("scanCompleteness=partial", text);
        Assert.Contains("listCompleteness=complete", text);
        Assert.Contains("continuationToken=none", text);
    }
}
