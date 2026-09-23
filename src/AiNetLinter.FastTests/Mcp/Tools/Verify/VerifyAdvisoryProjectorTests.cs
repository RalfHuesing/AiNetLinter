#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp.Tools.Verify;
using AiNetLinter.Mcp.Tools.Verify.DeadCode;
using AiNetLinter.Mcp.Tools.Verify.MagicValues;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Handoffs;
using AiNetLinter.Mcp.Tools.SymbolGraph;
using AiNetLinter.TestKit;
using Microsoft.CodeAnalysis;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Tools.Verify;

[Trait("Category", "Component")]
public sealed class VerifyAdvisoryProjectorTests
{
    [Fact]
    public async Task CollectAsync_MultipleScopeFiles_AggregatesBothAdvisoryKinds()
    {
        using var testSolution = RoslynTestSolutionFactory.CreateSolution(
            @"C:\ainetlinter-virtual\VerifyAdvisoryProjectorTests.slnx",
            new ProjectSpec("TestApp", [
                ("FirstProbe.cs", ProbeSource("FirstProbe", "first")),
                ("SecondProbe.cs", ProbeSource("SecondProbe", "second"))],
                VirtualProjectDirectory: "."));
        var scopeFiles = testSolution.Solution.Projects
            .SelectMany(project => project.Documents)
            .Select(document => Path.GetFullPath(document.FilePath!))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var result = await VerifyAdvisoryProjector.CollectAsync(
            testSolution.Solution,
            scopeFiles,
            CancellationToken.None,
            handoffIdentity: CreateHandoffIdentity(testSolution.Solution));

        Assert.Equal("complete", result.Completeness);
        Assert.Equal(await CollectPerFileTotalAsync(testSolution.Solution, scopeFiles), result.TotalCount);
        Assert.Contains(result.Entries, entry => entry.RuleOrCategory == "dead_code");
        Assert.Contains(result.Entries, entry => entry.RuleOrCategory.StartsWith("magic_value:", StringComparison.Ordinal));
        var deadCodeEntries = result.Entries.Where(entry => entry.RuleOrCategory == "dead_code").ToList();
        var orderedEntries = result.Entries.ToList();
        var lastDeadCodeIndex = orderedEntries.FindLastIndex(entry => entry.RuleOrCategory == "dead_code");
        var firstMagicValueIndex = orderedEntries.FindIndex(entry => entry.RuleOrCategory.StartsWith("magic_value:", StringComparison.Ordinal));
        Assert.True(lastDeadCodeIndex < firstMagicValueIndex);
        Assert.Equal(deadCodeEntries.Count, deadCodeEntries.Select(entry => entry.HandoffId).Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public async Task CollectAsync_MultipleScopeFiles_PreservesThePerFileCandidatePopulation()
    {
        using var testSolution = RoslynTestSolutionFactory.CreateSolution(
            @"C:\ainetlinter-virtual\VerifyAdvisoryProjectorSemanticsTests.slnx",
            new ProjectSpec("TestApp", [
                ("FirstProbe.cs", DuplicateConstProbeSource("FirstProbe")),
                ("SecondProbe.cs", DuplicateConstProbeSource("SecondProbe"))],
                VirtualProjectDirectory: "."));
        var scopeFiles = testSolution.Solution.Projects
            .SelectMany(project => project.Documents)
            .Select(document => Path.GetFullPath(document.FilePath!))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var combined = await VerifyAdvisoryProjector.CollectAsync(
            testSolution.Solution,
            scopeFiles,
            CancellationToken.None,
            handoffIdentity: CreateHandoffIdentity(testSolution.Solution));
        var expectedTotal = await CollectPerFileTotalAsync(testSolution.Solution, scopeFiles);

        Assert.Equal(expectedTotal, combined.TotalCount);
    }

    [Fact]
    public async Task CollectAsync_UnavailableRazorEvidence_ProjectsTheReasonAndLowConfidence()
    {
        using var tempDirectory = TestTempDirectory.Create("verify-razor-advisory-");
        tempDirectory.CreateFile("src/Foo.razor");
        using var testSolution = RoslynTestSolutionFactory.CreateSolution(
            Path.Combine(tempDirectory.DirectoryPath, "RazorAdvisory.slnx"),
            new ProjectSpec("RazorAdvisory", [
                ("src/Foo.razor", ""),
                ("src/Foo.razor.cs", "namespace RazorAdvisory; public sealed partial class Foo { private void Unused() { } }")],
                VirtualProjectDirectory: "."));
        var scopeFiles = testSolution.Solution.Projects
            .SelectMany(project => project.Documents)
            .Select(document => Path.GetFullPath(document.FilePath!))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var result = await VerifyAdvisoryProjector.CollectAsync(
            testSolution.Solution,
            scopeFiles,
            CancellationToken.None,
            handoffIdentity: CreateHandoffIdentity(testSolution.Solution));

        var razorEntries = result.Entries.Where(entry =>
            entry.RuleOrCategory == "dead_code" && entry.Reason.Contains("Razor-Referenzen nicht entscheidbar", StringComparison.Ordinal)).ToList();
        Assert.NotEmpty(razorEntries);
        Assert.All(razorEntries, entry =>
        {
            Assert.Equal("low", entry.Confidence);
            Assert.Contains("Razor-Generierung/Projektladung gegenprüfen", entry.Reason, StringComparison.Ordinal);
        });
    }

    private static string ProbeSource(string typeName, string route) => $$"""
        namespace TestApp;

        public sealed class {{typeName}}
        {
            private static void Unused() { }

            public string Primary => "https://example.invalid/{{route}}";

            public string Secondary => "https://example.invalid/{{route}}";
        }
        """;

    private static async Task<int> CollectPerFileTotalAsync(
        Microsoft.CodeAnalysis.Solution solution,
        IReadOnlySet<string> scopeFiles)
    {
        var total = 0;
        foreach (var scopeFile in scopeFiles)
        {
            var fileScope = new HashSet<string>([scopeFile], StringComparer.OrdinalIgnoreCase);
            var deadCode = await DeadCodeAdvisoryScanner.ScanAsync(
                solution,
                new DeadCodeAdvisoryOptions(
                    Accessibility: DeadCodeAccessibilityFilter.All,
                    Confidence: DeadCodeConfidenceFilter.Both,
                    Kind: DeadCodeKindFilter.All,
                    Mode: DeadCodeMode.Members,
                    MaxResults: int.MaxValue,
                    ScopeFiles: fileScope),
                CancellationToken.None);
            var magicValues = await MagicValueAdvisoryScanner.ScanAsync(new MagicValueAdvisoryScannerParameters(
                solution,
                null,
                null,
                null,
                MinOccurrences: 2,
                MaxResults: int.MaxValue,
                IgnoreNumbers: null,
                IncludeTests: false,
                IncludeSuppressed: false,
                ChangedOnly: false,
                CancellationToken.None,
                ScopeFiles: fileScope));
            total += deadCode.Summary.TotalDead + magicValues.Payload!.Summary.Total;
        }
        return total;
    }

    private static string DuplicateConstProbeSource(string typeName) => $$"""
        namespace TestApp;

        public sealed class {{typeName}}
        {
            public const string Route = "https://example.invalid/shared";
        }
        """;

    [Fact]
    public async Task CollectAsync_SolutionAndChangesScopesUseTheSameReferenceLiveness()
    {
        using var testSolution = RoslynTestSolutionFactory.CreateSolution(
            @"C:\ainetlinter-virtual\VerifyAdvisoryScopeTests.slnx",
            new ProjectSpec("ScopeApp", [("Probe.cs", "namespace ScopeApp; internal sealed class Probe { private static void Unused() { } }")], VirtualProjectDirectory: "."));
        var scopeFiles = testSolution.Solution.Projects
            .SelectMany(project => project.Documents)
            .Select(document => Path.GetFullPath(document.FilePath!))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var identity = CreateHandoffIdentity(testSolution.Solution);

        var changes = await VerifyAdvisoryProjector.CollectAsync(testSolution.Solution, scopeFiles, CancellationToken.None, handoffIdentity: identity);
        var solution = await VerifyAdvisoryProjector.CollectAsync(testSolution.Solution, null, CancellationToken.None, handoffIdentity: identity);

        Assert.Equal(changes.DeadCode?.Candidates, solution.DeadCode?.Candidates);
        Assert.Equal(changes.DeadCode?.TestOnly, solution.DeadCode?.TestOnly);
        Assert.Equal("complete", solution.DeadCode?.Status);
        var candidate = solution.Entries.First(entry => entry.RuleOrCategory == "dead_code");
        Assert.StartsWith("h:", candidate.HandoffId, StringComparison.Ordinal);
        var restored = HandoffHandleRegistry.Default.RestoreInternalHandoffForInput(candidate.HandoffId);
        Assert.True(restored.IsSuccess);
        Assert.StartsWith("i:0:", restored.Value, StringComparison.Ordinal);
        var resolved = await SymbolIdentifierResolver.TryResolveByStableIdAsync(
            testSolution.Solution,
            candidate.HandoffId,
            CancellationToken.None,
            identity);
        Assert.NotNull(resolved.Symbol);
        Assert.Null(resolved.Error);
        Assert.Equal("unreferenced", candidate.Usage);
        Assert.Equal(solution.Entries.Count(entry => entry.RuleOrCategory == "dead_code"),
            solution.Entries.Where(entry => entry.RuleOrCategory == "dead_code").Select(entry => entry.HandoffId).Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public async Task CollectAsync_LinkedFileCandidatesWithTheSameDocumentationIdResolveToTheirProject()
    {
        const string source = "namespace Linked; internal sealed class Probe { private void Unused() { } }";
        using var testSolution = RoslynTestSolutionFactory.CreateSolution(
            @"C:\ainetlinter-virtual\LinkedVerifyAdvisoryTests.slnx",
            new ProjectSpec("First", [("Shared.cs", source)], VirtualProjectDirectory: "shared"),
            new ProjectSpec("Second", [("Shared.cs", source)], VirtualProjectDirectory: "shared"));
        var identity = CreateHandoffIdentity(testSolution.Solution);
        var projectSymbols = new List<Microsoft.CodeAnalysis.ISymbol>();
        foreach (var project in testSolution.Solution.Projects)
        {
            var compilation = await project.GetCompilationAsync();
            var type = compilation!.GetTypeByMetadataName("Linked.Probe")!;
            projectSymbols.Add(type.GetMembers("Unused").Single());
        }
        Assert.Equal(
            DocumentationCommentId.CreateDeclarationId(projectSymbols[0]),
            DocumentationCommentId.CreateDeclarationId(projectSymbols[1]));

        var result = await VerifyAdvisoryProjector.CollectAsync(
            testSolution.Solution,
            null,
            CancellationToken.None,
            handoffIdentity: identity);
        var candidates = result.Entries.Where(entry => entry.RuleOrCategory == "dead_code").ToList();

        Assert.Equal(4, candidates.Count);
        Assert.Equal(4, candidates.Select(entry => entry.HandoffId).Distinct(StringComparer.Ordinal).Count());
        var resolvedAssemblies = new HashSet<string>(StringComparer.Ordinal);
        foreach (var candidate in candidates)
        {
            var resolved = await SymbolIdentifierResolver.TryResolveByStableIdAsync(
                testSolution.Solution,
                candidate.HandoffId,
                CancellationToken.None,
                identity);
            Assert.NotNull(resolved.Symbol);
            Assert.Null(resolved.Error);
            resolvedAssemblies.Add(resolved.Symbol!.ContainingAssembly.Name);
        }
        Assert.Equal(new[] { "First", "Second" }, resolvedAssemblies.OrderBy(name => name, StringComparer.Ordinal));
    }

    private static AnalysisSymbolIdentity CreateHandoffIdentity(Microsoft.CodeAnalysis.Solution solution) =>
        AnalysisSymbolIdentity.ForSource(
            Path.GetFullPath(solution.FilePath!),
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("verify-advisory-test-snapshot"))));
}
