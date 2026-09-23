#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp.Tools.Verify.DeadCode;
using AiNetLinter.TestKit;
using Microsoft.CodeAnalysis;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Tools.DeadCode;

[Trait("Category", "Component")]
public sealed class RazorGeneratedEvidenceIndexTests
{
    [Fact]
    public async Task ScanAsync_MissingGeneratedRazorDocuments_LowersEveryCodeBehindCandidateOnly()
    {
        using var tempDirectory = TestTempDirectory.Create("dead-code-razor-evidence-");
        using var testSolution = CreateUnavailableSolution(tempDirectory);

        var result = await DeadCodeAdvisoryScanner.ScanAsync(
            testSolution.Solution,
            new DeadCodeAdvisoryOptions(
                Accessibility: DeadCodeAccessibilityFilter.Private,
                Kind: DeadCodeKindFilter.Method),
            CancellationToken.None);

        Assert.True(
            result.Summary.DocumentsInScope > 0,
            $"Expected candidate documents. solution={testSolution.Solution.FilePath}; project={testSolution.Solution.Projects.Single().FilePath}; " +
            $"documents={string.Join(", ", testSolution.Solution.Projects.Single().Documents.Select(document => document.FilePath))}");
        Assert.Equal(2, result.Summary.DocumentsInScope);
        Assert.Equal(2, result.Summary.Low);
        Assert.Equal(1, result.Summary.High);
        Assert.Equal(3, result.Summary.TotalDead);

        var boundHandler = Assert.Single(result.DeadSymbols, entry => entry.SymbolName == "BoundHandler");
        var unusedMember = Assert.Single(result.DeadSymbols, entry => entry.SymbolName == "UnusedMember");
        var regularMember = Assert.Single(result.DeadSymbols, entry => entry.SymbolName == "UnusedRegularMember");

        Assert.Equal("low", boundHandler.Confidence);
        Assert.Equal("low", unusedMember.Confidence);
        Assert.Contains("Razor-Referenzen nicht entscheidbar", boundHandler.Reason, StringComparison.Ordinal);
        Assert.Contains("Razor-Referenzen nicht entscheidbar", unusedMember.Reason, StringComparison.Ordinal);
        Assert.Equal("high", regularMember.Confidence);
    }

    [Fact]
    public async Task CreateAsync_MatchingGeneratedPathAndPartialType_IsAvailableWithoutAHandlerReference()
    {
        using var tempDirectory = TestTempDirectory.Create("dead-code-razor-available-");
        var componentPath = tempDirectory.CreateFile("src/Foo.razor");
        using var testSolution = CreateSolutionWithGeneratedDocument(tempDirectory, componentPath);
        var project = Assert.Single(testSolution.Solution.Projects);
        var codeBehind = project.Documents.Single(document => document.Name == "src/Foo.razor.cs");
        var generatedDocument = project.Documents.Single(document => document.Name == "src/Generated/Foo_razor.g.cs");
        var compilation = await project.GetCompilationAsync();
        var componentType = Assert.IsAssignableFrom<INamedTypeSymbol>(
            compilation?.GetTypeByMetadataName("RazorEvidence.Foo"));

        var index = await RazorGeneratedEvidenceIndex.CreateAsync(project, [generatedDocument], CancellationToken.None);

        Assert.Equal(
            RazorGeneratedEvidenceStatus.Available,
            index.GetStatus(componentType.GetMembers("UnusedMember").Single(), codeBehind));
        var assessment = RazorGeneratedEvidenceIndex.Assess(
            RazorGeneratedEvidenceStatus.Available,
            "high",
            "Keine Referenzen gefunden.",
            []);
        Assert.Equal("high", assessment.Confidence);
        Assert.Contains("Generierte Razor-Referenzen wurden mitgeprüft", assessment.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreateAsync_SameGeneratedFileNameFromAnotherDirectory_IsUnavailable()
    {
        using var tempDirectory = TestTempDirectory.Create("dead-code-razor-path-");
        tempDirectory.CreateFile("src/One/Foo.razor");
        var otherComponentPath = tempDirectory.CreateFile("src/Two/Foo.razor");
        using var testSolution = CreateSolutionWithGeneratedDocument(
            tempDirectory,
            otherComponentPath,
            codeBehindPath: "src/One/Foo.razor.cs",
            generatedPath: "src/Two/Generated/Foo_razor.g.cs");
        var project = Assert.Single(testSolution.Solution.Projects);
        var codeBehind = project.Documents.Single(document => document.Name == "src/One/Foo.razor.cs");
        var generatedDocument = project.Documents.Single(document => document.Name == "src/Two/Generated/Foo_razor.g.cs");
        var compilation = await project.GetCompilationAsync();
        var componentType = Assert.IsAssignableFrom<INamedTypeSymbol>(
            compilation?.GetTypeByMetadataName("RazorEvidence.Foo"));

        var index = await RazorGeneratedEvidenceIndex.CreateAsync(project, [generatedDocument], CancellationToken.None);

        Assert.Equal(
            RazorGeneratedEvidenceStatus.Unavailable,
            index.GetStatus(componentType.GetMembers("UnusedMember").Single(), codeBehind));
    }

    [Theory]
    [InlineData("locals")]
    [InlineData("both")]
    public async Task ScanAsync_UnavailableRazorEvidenceLowersDiagnosticCandidatesInLocalsAndBoth(string mode)
    {
        using var tempDirectory = TestTempDirectory.Create("dead-code-razor-diagnostics-");
        using var testSolution = CreateUnavailableSolution(tempDirectory, diagnostics: true);

        var result = await DeadCodeAdvisoryScanner.ScanAsync(
            testSolution.Solution,
            new DeadCodeAdvisoryOptions(
                Accessibility: DeadCodeAccessibilityFilter.Private,
                Kind: DeadCodeKindFilter.Field,
                Mode: DeadCodeAdvisoryOptions.ParseMode(mode)),
            CancellationToken.None);

        Assert.Equal(2, result.Summary.DocumentsInScope);
        var codeBehindField = Assert.Single(result.DeadSymbols, entry => entry.SymbolName == "_unusedCodeBehind");
        var regularField = Assert.Single(result.DeadSymbols, entry => entry.SymbolName == "_unusedRegular");
        Assert.Equal("low", codeBehindField.Confidence);
        Assert.Contains("Razor-Referenzen nicht entscheidbar", codeBehindField.Reason, StringComparison.Ordinal);
        Assert.Equal("high", regularField.Confidence);
        Assert.Equal(1, result.Summary.Low);
        Assert.Equal(1, result.Summary.High);
        Assert.Equal(2, result.Summary.TotalDead);
    }

    [Fact]
    public async Task ScanAsync_HighConfidenceFilterRunsAfterUnavailableEvidenceAdjustment()
    {
        using var tempDirectory = TestTempDirectory.Create("dead-code-razor-high-filter-");
        using var testSolution = CreateUnavailableSolution(tempDirectory);

        var result = await DeadCodeAdvisoryScanner.ScanAsync(
            testSolution.Solution,
            new DeadCodeAdvisoryOptions(
                Accessibility: DeadCodeAccessibilityFilter.Private,
                Confidence: DeadCodeConfidenceFilter.High,
                Kind: DeadCodeKindFilter.Method),
            CancellationToken.None);

        var regularMember = Assert.Single(result.DeadSymbols);
        Assert.Equal("UnusedRegularMember", regularMember.SymbolName);
        Assert.Equal(1, result.Summary.High);
        Assert.Equal(0, result.Summary.Low);
        Assert.Equal(1, result.Summary.TotalDead);
    }

    private static RoslynTestSolution CreateUnavailableSolution(TestTempDirectory tempDirectory, bool diagnostics = false)
    {
        tempDirectory.CreateFile("src/Foo.razor");
        var componentSource = diagnostics
            ? """
                namespace RazorEvidence;

                public sealed partial class Foo
                {
                    private int _unusedCodeBehind;
                }
                """
            : """
                namespace RazorEvidence;

                public sealed partial class Foo
                {
                    private void BoundHandler() { }
                    private void UnusedMember() { }
                }
                """;
        var regularSource = diagnostics
            ? """
                namespace RazorEvidence;

                public sealed class RegularService
                {
                    private int _unusedRegular;
                }
                """
            : """
                namespace RazorEvidence;

                public sealed class RegularService
                {
                    private void UnusedRegularMember() { }
                }
                """;

        return RoslynTestSolutionFactory.CreateSolution(
            Path.Combine(tempDirectory.DirectoryPath, "RazorEvidence.slnx"),
            new ProjectSpec("RazorEvidence", [
                ("src/Foo.razor", ""),
                ("src/Foo.razor.cs", componentSource),
                ("src/RegularService.cs", regularSource)],
                VirtualProjectDirectory: "."));
    }

    private static RoslynTestSolution CreateSolutionWithGeneratedDocument(
        TestTempDirectory tempDirectory,
        string checksumPath,
        string codeBehindPath = "src/Foo.razor.cs",
        string generatedPath = "src/Generated/Foo_razor.g.cs")
    {
        var codeBehindDirectory = Path.GetDirectoryName(codeBehindPath)!.Replace('\\', '/');
        var codeBehindSourcePath = $"{codeBehindDirectory}/Foo.razor";
        tempDirectory.CreateFile(codeBehindSourcePath);
        var generatedSource = string.Join(
            Environment.NewLine,
            $"#pragma checksum \"{checksumPath}\" \"{{406EA660-64CF-4C82-B6F0-42D48172A799}}\" \"0000000000000000000000000000000000000000\"",
            "namespace RazorEvidence;",
            "public sealed partial class Foo { }");
        var sourceDocuments = new[]
        {
            (codeBehindSourcePath, ""),
            (codeBehindPath, "namespace RazorEvidence; public sealed partial class Foo { private void UnusedMember() { } }"),
            (generatedPath, generatedSource),
        };

        return RoslynTestSolutionFactory.CreateSolution(
            Path.Combine(tempDirectory.DirectoryPath, "RazorEvidence.slnx"),
            new ProjectSpec("RazorEvidence", sourceDocuments, VirtualProjectDirectory: "."));
    }
}
