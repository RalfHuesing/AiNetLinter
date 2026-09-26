#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AiNetLinter.Mcp.Tools.Verify.DeadCode;
using Microsoft.CodeAnalysis;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Tools.DeadCode;

[Trait("Category", "Component")]
public sealed class DeadCodeMarkupContractTests
{
    [Fact]
    public async Task RealAttachedPropertyBindsAccessorsAndKeepsOrdinaryMethod()
    {
        using var fixture = Create("""
            namespace Ui;
            public static class Behavior
            {
                public static readonly System.Windows.DependencyProperty ActiveProperty = System.Windows.DependencyProperty.RegisterAttached(
                    "Active", typeof(bool), typeof(Behavior));
                public static bool GetActive(System.Windows.DependencyObject target) => (bool)target.GetValue(ActiveProperty);
                public static void SetActive(System.Windows.DependencyObject target, bool value) => target.SetValue(ActiveProperty, value);
                public static void Orphan() { }
            }
            """, MetadataReference.CreateFromFile(Path.Combine(AppContext.BaseDirectory, "ReferenceFixtures", "WindowsBase.dll")));
        var solution = fixture.Solution.Projects.Single().AddAdditionalDocument("View.xaml",
            "<Window xmlns:ui=\"clr-namespace:Ui\" ui:Behavior.Active=\"True\" />").Project.Solution;
        var result = await Scan(solution);
        Assert.DoesNotContain(result.DeadSymbols, entry => entry.SymbolName is "GetActive" or "SetActive");
        Assert.Contains(result.DeadSymbols, entry => entry.SymbolName == "Orphan");
    }

    [Fact]
    public async Task GeneratorReferencesCountAlthoughGeneratedDeclarationsAreExcluded()
    {
        using var fixture = Create("public sealed class Worker { public void Used() { } public void Orphan() { } }");
        var project = fixture.Solution.Projects.Single();
        var solution = project.AddDocument("Generated.g.cs", "public static class Generated { public static void Run(Worker value) => value.Used(); }",
            filePath: Path.Combine(Path.GetDirectoryName(project.FilePath)!, "Generated.g.cs")).Project.Solution;
        var result = await Scan(solution);
        Assert.DoesNotContain(result.DeadSymbols, entry => entry.SymbolName is "Used" or "Run");
        Assert.Contains(result.DeadSymbols, entry => entry.SymbolName == "Orphan");
    }

    [Fact]
    public async Task RazorParameterBindingKeepsMethodCandidatesAndOmitsProperties()
    {
        using var fixture = Create("""
            using Microsoft.AspNetCore.Components;
            public sealed class Widget : ComponentBase
            {
                [Parameter] public int Value { get; set; }
                [Parameter] public int Unbound { get; set; }
                protected override void OnInitialized() { }
                public void Orphan() { }
            }
            """, MetadataReference.CreateFromFile(typeof(Microsoft.AspNetCore.Components.ComponentBase).Assembly.Location));
        var solution = fixture.Solution.Projects.Single().AddAdditionalDocument("Page.razor", "<Widget Value=\"1\" />").Project.Solution;
        var result = await Scan(solution);
        Assert.DoesNotContain(result.DeadSymbols, entry => entry.SymbolName is "Value" or "Unbound" or "OnInitialized");
        Assert.Contains(result.DeadSymbols, entry => entry.SymbolName == "Orphan");
    }

    private static RoslynTestSolution Create(string source, params MetadataReference[] references) =>
        RoslynTestSolutionFactory.CreateSolution(@"C:\ainetlinter-virtual\MarkupContracts.slnx",
            new ProjectSpec("Host", [("Code.cs", source)], AdditionalReferences: references.Append(
                MetadataReference.CreateFromFile(System.Reflection.Assembly.Load("System.Runtime").Location)).ToArray()));

    private static async Task<DeadCodeScanResult> Scan(Solution solution)
    {
        var compilation = await solution.Projects.Single().GetCompilationAsync();
        Assert.DoesNotContain(compilation!.GetDiagnostics(), diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
        return await DeadCodeAdvisoryScanner.ScanAsync(solution,
            new(Accessibility: DeadCodeAccessibilityFilter.All, Kind: DeadCodeKindFilter.All));
    }
}
