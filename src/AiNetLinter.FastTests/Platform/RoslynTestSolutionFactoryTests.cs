#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AiNetLinter.Core;
using AiNetLinter.TestKit;
using Microsoft.CodeAnalysis;
using Xunit;

namespace AiNetLinter.FastTests.Platform;

/// <summary>
/// Vertragstests fuer <see cref="RoslynTestSolutionFactory"/> (konzept.md §2/§4 sinngemaess auf die
/// Plattform selbst angewendet): belegt mechanisch Mehrprojekt-Verdrahtung, Nullable-Context,
/// Preprocessor-Symbole, Referenz-Caching und den Fehlerpfad bei unbekannten Projektnamen, statt
/// diese Eigenschaften nur im XML-Doc zu behaupten.
/// </summary>
[Trait("Category", "Component")]
public sealed class RoslynTestSolutionFactoryTests
{
    [Fact]
    public async Task CreateSolution_MultiProjectWithProjectReference_ResolvesSymbolAcrossProjects()
    {
        const string providerSource = """
            namespace Widgets;
            public class Gadget {}
            """;
        const string consumerSource = """
            namespace Widgets.Consumers;
            public class Consumer
            {
                public Widgets.Gadget? Field;
            }
            """;

        using var testSolution = RoslynTestSolutionFactory.CreateSolution(
            new ProjectSpec("Provider", [("Gadget.cs", providerSource)]),
            new ProjectSpec("Consumer", [("Consumer.cs", consumerSource)], ProjectReferences: ["Provider"]));

        var compilation = await GetCompilationAsync(testSolution.Solution, "Consumer");

        Assert.NotNull(compilation.GetTypeByMetadataName("Widgets.Gadget"));
    }

    [Fact]
    public async Task CreateSolution_NullableContextOptions_AffectsNullableDiagnostics()
    {
        const string source = """
            namespace NullableProbe;
            public class Probe
            {
                public string Get()
                {
                    string? maybe = null;
                    string value = maybe;
                    return value;
                }
            }
            """;

        using var enabledSolution = RoslynTestSolutionFactory.CreateSolution(
            new ProjectSpec("EnabledProject", [("Probe.cs", source)], Nullable: NullableContextOptions.Enable));
        using var disabledSolution = RoslynTestSolutionFactory.CreateSolution(
            new ProjectSpec("DisabledProject", [("Probe.cs", source)], Nullable: NullableContextOptions.Disable));

        var enabledCompilation = await GetCompilationAsync(enabledSolution.Solution, "EnabledProject");
        var disabledCompilation = await GetCompilationAsync(disabledSolution.Solution, "DisabledProject");

        Assert.Contains(enabledCompilation.GetDiagnostics(), d => d.Id == "CS8600");
        Assert.DoesNotContain(disabledCompilation.GetDiagnostics(), d => d.Id == "CS8600");
    }

    [Fact]
    public async Task CreateSolution_PreprocessorSymbols_GateConditionalCompilation()
    {
        const string source = """
            namespace PreprocessorProbe;
            #if PROBE_SYMBOL
            public class ConditionalType {}
            #endif
            """;

        using var withSymbol = RoslynTestSolutionFactory.CreateSolution(
            new ProjectSpec("WithSymbol", [("Conditional.cs", source)], PreprocessorSymbols: ["PROBE_SYMBOL"]));
        using var withoutSymbol = RoslynTestSolutionFactory.CreateSolution(
            new ProjectSpec("WithoutSymbol", [("Conditional.cs", source)]));

        var withSymbolCompilation = await GetCompilationAsync(withSymbol.Solution, "WithSymbol");
        var withoutSymbolCompilation = await GetCompilationAsync(withoutSymbol.Solution, "WithoutSymbol");

        Assert.NotNull(withSymbolCompilation.GetTypeByMetadataName("PreprocessorProbe.ConditionalType"));
        Assert.Null(withoutSymbolCompilation.GetTypeByMetadataName("PreprocessorProbe.ConditionalType"));
    }

    [Fact]
    public void CreateSolution_CalledTwice_ReusesSameCoreReferenceInstances()
    {
        const string source = "namespace CacheProbe; public class Probe {}";

        using var first = RoslynTestSolutionFactory.CreateSolution(new ProjectSpec("First", [("Probe.cs", source)]));
        using var second = RoslynTestSolutionFactory.CreateSolution(new ProjectSpec("Second", [("Probe.cs", source)]));

        var corlibLocation = typeof(object).Assembly.Location;
        var firstCorlib = FindCorlibReference(first.Solution, "First", corlibLocation);
        var secondCorlib = FindCorlibReference(second.Solution, "Second", corlibLocation);

        Assert.Same(firstCorlib, secondCorlib);
    }

    [Fact]
    public void CreateSolution_UnknownProjectReferenceName_ThrowsWithMissingName()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            RoslynTestSolutionFactory.CreateSolution(
                new ProjectSpec("Consumer", [("Consumer.cs", "namespace X; public class C {}")], ProjectReferences: ["MissingProject"])));

        Assert.Contains("MissingProject", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CreateSolution_NormalProjectWithoutTestFrameworkReference_IsNotTestProject()
    {
        using var testSolution = RoslynTestSolutionFactory.CreateSolution(
            new ProjectSpec("ProductionProbe", [("Probe.cs", "namespace Probe; public class Service {}")]));

        var project = testSolution.Solution.Projects.Single();

        Assert.False(TestProjectDetector.IsTestProject(project));
    }

    [Fact]
    public void CreateSolution_WithVirtualPath_AssignsNormalizedPathsWithoutMaterializingFiles()
    {
        var virtualSolutionFilePath = Path.Combine(
            Path.GetTempPath(),
            $"ainetlinter-virtual-{Guid.NewGuid():N}",
            "Virtual.slnx");

        using var testSolution = RoslynTestSolutionFactory.CreateSolution(
            virtualSolutionFilePath,
            new ProjectSpec("VirtualProject", [("Nested/Probe.cs", "namespace Probe; public class Type {}")]));

        var expectedSolutionFilePath = Path.GetFullPath(virtualSolutionFilePath);
        var expectedDocumentFilePath = Path.Combine(
            Path.GetDirectoryName(expectedSolutionFilePath)!, "VirtualProject", "Nested", "Probe.cs");
        var document = Assert.Single(testSolution.Solution.Projects.Single().Documents);

        Assert.Equal(expectedSolutionFilePath, testSolution.Solution.FilePath);
        Assert.Equal(expectedDocumentFilePath, document.FilePath);
        Assert.False(File.Exists(expectedSolutionFilePath));
        Assert.False(Directory.Exists(Path.GetDirectoryName(expectedDocumentFilePath)!));
    }

    [Fact]
    public void CreateSolution_VirtualProjectDirectory_PreservesProjectNameAndBuildsExpectedDocumentPath()
    {
        using var testSolution = RoslynTestSolutionFactory.CreateSolution(
            @"C:\ainetlinter-virtual\SymbolGraphMini.slnx",
            new ProjectSpec(
                "SymbolGraphMini",
                [("Greeter.cs", "namespace SymbolGraphMini; public class Greeter {}")],
                VirtualProjectDirectory: "src/SymbolGraphMini"));

        var project = Assert.Single(testSolution.Solution.Projects);
        var document = Assert.Single(project.Documents);

        Assert.Equal("SymbolGraphMini", project.Name);
        Assert.Equal(@"C:\ainetlinter-virtual\src\SymbolGraphMini\Greeter.cs", document.FilePath);
    }

    private static async Task<Compilation> GetCompilationAsync(Solution solution, string projectName)
    {
        var project = solution.Projects.Single(p => p.Name == projectName);
        return (await project.GetCompilationAsync())!;
    }

    private static MetadataReference FindCorlibReference(Solution solution, string projectName, string corlibLocation)
    {
        var project = solution.Projects.Single(p => p.Name == projectName);
        return project.MetadataReferences
            .Cast<PortableExecutableReference>()
            .Single(r => r.FilePath == corlibLocation);
    }
}
