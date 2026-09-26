#nullable enable

using System.Linq;
using System.Threading.Tasks;
using AiNetLinter.Mcp.Tools.Verify.DeadCode;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Tools.DeadCode;

// @covers DeadCodeXamlUsage
// @covers DeadCodeRazorUsage
[Trait("Category", "Component")]
public sealed class DeadCodeMarkupUsageTests
{
    [Fact]
    public async Task XamlDataMembersAreNotCandidates()
    {
        using var fixture = Create("""
            namespace Ui;
            public sealed class ViewModel { public string Label { get; set; } = ""; public string Orphan { get; set; } = ""; }
            """);
        var solution = fixture.Solution.Projects.Single().AddAdditionalDocument("View.xaml", """
            <Window xmlns:ui="clr-namespace:Ui">
                <Window.DataContext><ui:ViewModel /></Window.DataContext>
                <TextBlock Text="{Binding Label}" />
            </Window>
            """).Project.Solution;
        var result = await DeadCodeAdvisoryScanner.ScanAsync(solution, new(Accessibility: DeadCodeAccessibilityFilter.All, Kind: DeadCodeKindFilter.Property));
        Assert.Empty(result.DeadSymbols);
    }

    [Fact]
    public async Task UnresolvedBindingDoesNotCreateDataMemberCandidates()
    {
        using var fixture = Create("""
            namespace Ui;
            public sealed class ViewModel { public string Label { get; set; } = ""; public string Orphan { get; set; } = ""; }
            """);
        var solution = fixture.Solution.Projects.Single().AddAdditionalDocument("View.xaml", """
            <Window><TextBlock Text="{Binding Label}" /></Window>
            """).Project.Solution;
        var result = await DeadCodeAdvisoryScanner.ScanAsync(solution, new(Accessibility: DeadCodeAccessibilityFilter.All, Kind: DeadCodeKindFilter.Property));
        Assert.Empty(result.DeadSymbols);
        Assert.Equal(0, result.Summary.Undecidable);
    }

    [Fact]
    public async Task RazorEventBindsCodeBehindAndKeepsPublicUnboundMethod()
    {
        using var fixture = RoslynTestSolutionFactory.CreateSolution(@"C:\ainetlinter-virtual\Markup.slnx",
            new ProjectSpec("Host", [("View.razor.cs", "namespace Ui; public sealed partial class View { public void Click() { } public void Orphan() { } }")]));
        var solution = fixture.Solution.Projects.Single().AddAdditionalDocument("View.razor", """
            <button @onclick="Click">Run</button>
            """).Project.Solution;
        var result = await DeadCodeAdvisoryScanner.ScanAsync(solution, new(Accessibility: DeadCodeAccessibilityFilter.All, Kind: DeadCodeKindFilter.Method));
        Assert.DoesNotContain(result.DeadSymbols, entry => entry.SymbolName == "Click");
        Assert.Contains(result.DeadSymbols, entry => entry.SymbolName == "Orphan");
    }

    private static RoslynTestSolution Create(string source) =>
        RoslynTestSolutionFactory.CreateSolution(@"C:\ainetlinter-virtual\Markup.slnx",
            new ProjectSpec("Host", [("Code.cs", source)]));
}
