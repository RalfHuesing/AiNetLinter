#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
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
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Tools.Verify;

[Trait("Category", "Component")]
public sealed class VerifyAdvisoryProjectorTests
{
    [Fact]
    public void GetVerifyAdvisories_ContinuationCoversSnapshotWithoutDuplicateCandidates()
    {
        var entries = Enumerable.Range(1, 2000).Select(index => new DeadCodeEntry(
            Id: $"candidate-{index}", Kind: "method", ContainerType: "Service",
            SymbolName: $"Unused{index}", File: "Service.cs", Line: index,
            Column: 1, Accessibility: "private",
            Reason: "static scan", LimitsApplies: [], ProjectName: "TestApp",
            InternalSymbolIdentifier: $"M:TestApp.Service.Unused{index}")).ToArray();
        var scan = new DeadCodeScanResult(entries,
            new DeadCodeSummary(1, 2000, 2000, new Dictionary<string, int>()),
            [], new DeadCodeRecommendedNextAction("countercheck", "prüfen"), false);
        var pages = new VerifyAdvisoryPageStore();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var handles = new HashSet<string>(StringComparer.Ordinal);
        var result = pages.Start(scan);
        var pageCount = 0;

        while (true)
        {
            Assert.False(result.IsError == true);
            var content = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
            Assert.True(Encoding.UTF8.GetByteCount(content) <= 65_536);
            foreach (var row in content.Split('\n').Where(line => Regex.IsMatch(line, @"^\d+ \|", RegexOptions.CultureInvariant)))
            {
                var fields = row.Split(" | ");
                Assert.True(seen.Add(fields[1]));
                Assert.StartsWith("h:", fields[2], StringComparison.Ordinal);
                Assert.True(handles.Add(fields[2]));
            }
            pageCount++;
            var token = content.Split('\n').Single(line => line.StartsWith("continuationToken=", StringComparison.Ordinal))["continuationToken=".Length..];
            if (token == "none")
            {
                Assert.Contains("listCompleteness=complete", content, StringComparison.Ordinal);
                break;
            }
            Assert.Contains("listCompleteness=partial", content, StringComparison.Ordinal);
            if (pageCount == 1) entries[1999] = entries[0];
            result = pages.Continue(token);
        }

        Assert.True(pageCount > 1);
        Assert.Equal(2000, seen.Count);
        Assert.Equal(2000, handles.Count);
    }

    [Fact]
    public void GetVerifyAdvisories_UnknownContinuationTokenReturnsExplicitError()
    {
        var result = new VerifyAdvisoryPageStore().Continue("bad-token");

        Assert.True(result.IsError);
        var content = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.Contains("INVALID_CONTINUATION_TOKEN", content, StringComparison.Ordinal);
    }

    [Fact]
    public void GetVerifyAdvisories_EmptyScanReturnsContentOnlyWithoutClaimingClean()
    {
        var scan = new DeadCodeScanResult(
            [],
            new DeadCodeSummary(0, 0, 0, new Dictionary<string, int>()),
            [],
            new DeadCodeRecommendedNextAction("countercheck", "statische Grenzen prüfen"),
            IsTruncated: false);

        var result = GetVerifyAdvisoriesTool.Render(scan);

        Assert.False(result.IsError == true);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.Contains("status=complete; scope=unknown; candidates=0", text, StringComparison.Ordinal);
        Assert.Contains("shown=0; truncatedBy=0", text, StringComparison.Ordinal);
        Assert.DoesNotContain("clean", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetVerifyAdvisories_OutputTruncationDoesNotChangeScanCompleteness()
    {
        var candidate = new DeadCodeEntry(
            Id: "unused",
            Kind: "method",
            ContainerType: "Service",
            SymbolName: "Unused",
            File: "Service.cs",
            Line: 3,
            Column: 1,
            Accessibility: "private",
            Reason: "static scan",
            LimitsApplies: [],
            ProjectName: "TestApp");
        var scan = new DeadCodeScanResult(
            [candidate],
            new DeadCodeSummary(1, 2, 2, new Dictionary<string, int> { ["method"] = 2 }),
            [],
            new DeadCodeRecommendedNextAction("continue", "weitere Kandidaten prüfen"),
            IsTruncated: true);

        var result = GetVerifyAdvisoriesTool.Render(scan);

        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.StartsWith("status=complete; scope=unknown; candidates=2", text, StringComparison.Ordinal);
        Assert.Contains("listCompleteness=partial", text, StringComparison.Ordinal);
        Assert.Contains("shown=1; truncatedBy=1", text, StringComparison.Ordinal);
    }

    [Fact]
    public void GetVerifyAdvisories_RenderUsesStableOrderWithoutConfidence()
    {
        static DeadCodeEntry Candidate(
            string project,
            string file,
            int line,
            string name,
            string confidence) => new(
                Id: name,
                Kind: "method",
                ContainerType: "Namespace.Type",
                SymbolName: name,
                File: file,
                Line: line,
                Column: 1,
                Accessibility: "private",
                Reason: "static scan",
                LimitsApplies: [],
                ProjectName: project);

        var entries = new[]
        {
            Candidate("A", "src/a.cs", 1, "LowA", "low"),
            Candidate("Z", "src/z.cs", 10, "HighZ", "high"),
            Candidate("A", "src/a.cs", 2, "HighA", "high"),
            Candidate("A", "src/b.cs", 3, "LowB", "low"),
        };
        var scan = new DeadCodeScanResult(
            entries,
            new DeadCodeSummary(4, 4, 0, new Dictionary<string, int> { ["method"] = 4 }),
            [],
            new DeadCodeRecommendedNextAction("countercheck", "statische Grenzen prüfen"),
            IsTruncated: false);

        var result = GetVerifyAdvisoriesTool.Render(scan);

        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        var rows = text.Split('\n').Where(line => Regex.IsMatch(line, @"^\d+ \|", RegexOptions.CultureInvariant)).ToArray();
        Assert.Equal(
            [
                "1 | Type.LowA |  | ",
                "2 | Type.HighA |  | ",
                "3 | Type.LowB |  | ",
                "10 | Type.HighZ |  | ",
            ],
            rows);
        Assert.Equal(1, text.Split('\n').Count(line => line == "columns: line | symbol | symbolIdentifier | countercheck"));
        Assert.Equal(1, text.Split('\n').Count(line => line == "project: A"));
    }

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
            settings: new(Identity: CreateHandoffIdentity(testSolution.Solution)));

        Assert.Equal("partial", result.Completeness);
        Assert.Equal("unavailable", result.DeadCode!.Coverage!.ChangesBasis);
        Assert.Equal(await CollectPerFileTotalAsync(testSolution.Solution, scopeFiles), result.TotalCount);
        Assert.DoesNotContain(result.Entries, entry => entry.RuleOrCategory == "dead_code");
        Assert.True(result.DeadCode.Candidates > 0);
        Assert.Contains(result.Entries, entry => entry.RuleOrCategory.StartsWith("magic_value:", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CollectAsync_MultipleScopeFiles_PreservesThePerFileAdvisoryPopulation()
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
            settings: new(Identity: CreateHandoffIdentity(testSolution.Solution)));
        var expectedTotal = await CollectPerFileTotalAsync(testSolution.Solution, scopeFiles);

        Assert.Equal(expectedTotal, combined.TotalCount);
    }

    [Fact]
    public async Task CollectAsync_UnavailableRazorEvidenceKeepsOnlyTheCompactDeadCodeSummary()
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
            settings: new(Identity: CreateHandoffIdentity(testSolution.Solution)));

        Assert.DoesNotContain(result.Entries, entry => entry.RuleOrCategory == "dead_code");
        Assert.True(result.DeadCode!.Candidates > 0);
        Assert.NotNull(result.DeadCode.Coverage);
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
            total += magicValues.Payload!.Summary.Total;
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

        var changes = await VerifyAdvisoryProjector.CollectAsync(testSolution.Solution, scopeFiles, CancellationToken.None, settings: new(Identity: identity));
        var solution = await VerifyAdvisoryProjector.CollectAsync(testSolution.Solution, null, CancellationToken.None, settings: new(Identity: identity));

        Assert.Equal(changes.DeadCode?.Candidates, solution.DeadCode?.Candidates);
        Assert.Equal("complete", solution.DeadCode?.Status);
        Assert.DoesNotContain(solution.Entries, entry => entry.RuleOrCategory == "dead_code");
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

        var scan = await DeadCodeAdvisoryScanner.ScanAsync(
            testSolution.Solution,
            new DeadCodeAdvisoryOptions(
                MaxResults: int.MaxValue,
                HandoffIdentity: identity),
            CancellationToken.None);
        var compact = GetVerifyAdvisoriesTool.Render(scan);
        var compactText = Assert.IsType<TextContentBlock>(Assert.Single(compact.Content)).Text;
        Assert.Contains("project: First", compactText, StringComparison.Ordinal);
        Assert.Contains("project: Second", compactText, StringComparison.Ordinal);
        var compactIds = Regex.Matches(compactText, @"\bh:[A-Za-z0-9_-]+\b", RegexOptions.CultureInvariant)
            .Cast<Match>()
            .Select(match => match.Value)
            .ToArray();
        Assert.Equal(2, compactIds.Length);
        Assert.Equal(compactIds.Length, compactIds.Distinct(StringComparer.Ordinal).Count());

        var partialScan = scan with { Summary = scan.Summary with { Status = "partial" } };
        var partial = GetVerifyAdvisoriesTool.Render(partialScan);
        var partialText = Assert.IsType<TextContentBlock>(Assert.Single(partial.Content)).Text;
        Assert.True(partialText.StartsWith("status=partial;", StringComparison.Ordinal));
        Assert.DoesNotContain("clean", partialText, StringComparison.OrdinalIgnoreCase);

        var result = await VerifyAdvisoryProjector.CollectAsync(
            testSolution.Solution,
            null,
            CancellationToken.None,
            settings: new(Identity: identity));
        Assert.Equal(2, result.DeadCode!.Candidates);
        Assert.DoesNotContain(result.Entries, entry => entry.RuleOrCategory == "dead_code");
    }

    private static AnalysisSymbolIdentity CreateHandoffIdentity(Microsoft.CodeAnalysis.Solution solution) =>
        AnalysisSymbolIdentity.ForSource(
            Path.GetFullPath(solution.FilePath!),
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("verify-advisory-test-snapshot"))));
}
