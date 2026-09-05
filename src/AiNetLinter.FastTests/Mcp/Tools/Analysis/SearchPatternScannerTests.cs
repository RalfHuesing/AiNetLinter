#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Baseline;
using AiNetLinter.Mcp.Tools.Analysis;
using AiNetLinter.TestKit;
using Microsoft.CodeAnalysis;

namespace AiNetLinter.FastTests.Mcp.Tools.Analysis;

[Trait("Category", "Unit")]
public sealed class SearchPatternScannerTests
{
    [Fact]
    public void Scan_PlainText_EmitsAllMatchRangesAndStablePositions()
    {
        using var tempDir = TestTempDirectory.Create("search-pattern-");
        using var solution = CreateSolution(tempDir.DirectoryPath);
        var path = Path.Combine(tempDir.DirectoryPath, "src", "Project", "matches.txt");
        File.WriteAllText(path, "before\r\n  Anchor anchor  \r\nafter");

        var result = SearchPatternScanner.Scan(CreateParameters(solution.Solution, new("anchor") { ContextLines = 1 }));
        var match = Assert.Single(result.Payload.Matches);

        Assert.Equal("src/Project/matches.txt", match.FilePath);
        Assert.Equal(2, match.Line);
        Assert.Equal("  Anchor anchor  ", match.LineText);
        Assert.Equal(new[] { new SearchPatternMatchRange(3, 6), new SearchPatternMatchRange(10, 6) }, match.MatchRanges);
        Assert.Equal(new[] { "before" }, match.ContextBefore);
        Assert.Equal(new[] { "after" }, match.ContextAfter);
        Assert.Equal("Project", match.ProjectName);
        Assert.Null(match.Semantic);
    }

    [Fact]
    public async Task Scan_CSharpEnrichment_ResolvesDeclarationAndReference()
    {
        using var tempDir = TestTempDirectory.Create("search-pattern-roslyn-");
        using var solution = CreateSolution(tempDir.DirectoryPath, "namespace Project;\npublic sealed class Target\n{\n    public string Run() => \"anchor\";\n}\npublic sealed class Caller\n{\n    public Target Create() => new Target();\n    public string Call() => new Target().Run();\n}");
        var path = Path.Combine(tempDir.DirectoryPath, "src", "Project", "Project.cs");
        File.WriteAllText(path, "namespace Project;\npublic sealed class Target\n{\n    public string Run() => \"anchor\";\n}\npublic sealed class Caller\n{\n    public Target Create() => new Target();\n    public string Call() => new Target().Run();\n}");

        var declarationResult = await SearchPatternScannerEnrichment.ScanAsync(CreateParameters(
            solution.Solution,
            new("Target") { EnrichCSharp = true }));
        var declaration = Assert.Single(declarationResult.Payload.Matches, match => match.Line == 2);
        var reference = Assert.Single(declarationResult.Payload.Matches, match => match.Line == 8);

        Assert.Equal(new SearchPatternMatchRange(21, 6), declaration.MatchRanges.Single());
        Assert.Equal("declaration", declaration.Semantic!.Kind);
        Assert.Equal("resolved", declaration.Semantic.Resolution);
        Assert.Equal("T:Project.Target", declaration.Semantic.SymbolId);
        Assert.Equal("symbol_reference", reference.Semantic!.Kind);
        Assert.Equal("resolved", reference.Semantic.Resolution);
        Assert.Equal("T:Project.Target", reference.Semantic.SymbolId);
        Assert.Equal("Project", reference.ProjectName);

        var methodResult = await SearchPatternScannerEnrichment.ScanAsync(CreateParameters(
            solution.Solution,
            new("Run") { EnrichCSharp = true }));
        var methodDeclaration = Assert.Single(methodResult.Payload.Matches, match => match.Line == 4);
        var methodReference = Assert.Single(methodResult.Payload.Matches, match => match.Line == 9);

        Assert.Equal("declaration", methodDeclaration.Semantic!.Kind);
        Assert.StartsWith("M:Project.Target.Run", methodDeclaration.Semantic.SymbolId, StringComparison.Ordinal);
        Assert.Equal("symbol_reference", methodReference.Semantic!.Kind);
        Assert.Equal(methodDeclaration.Semantic.SymbolId, methodReference.Semantic.SymbolId);
    }

    [Fact]
    public async Task Scan_CSharpEnrichment_DoesNotResolveCommentsOrStrings()
    {
        using var tempDir = TestTempDirectory.Create("search-pattern-roslyn-");
        const string source = "namespace Project;\npublic sealed class Target\n{\n    // Target in comment\n    public string Value => \"Target\";\n}";
        using var solution = CreateSolution(tempDir.DirectoryPath, source);
        File.WriteAllText(Path.Combine(tempDir.DirectoryPath, "src", "Project", "Project.cs"), source);

        var result = await SearchPatternScannerEnrichment.ScanAsync(CreateParameters(
            solution.Solution,
            new("Target") { EnrichCSharp = true }));

        var comment = Assert.Single(result.Payload.Matches, match => match.Line == 4);
        var stringLiteral = Assert.Single(result.Payload.Matches, match => match.Line == 5);
        Assert.Equal("comment", comment.Semantic!.Kind);
        Assert.Equal("not_applicable", comment.Semantic.Resolution);
        Assert.Equal("string", stringLiteral.Semantic!.Kind);
        Assert.Equal("not_applicable", stringLiteral.Semantic.Resolution);
        Assert.DoesNotContain(result.Payload.Matches, match => match.Semantic?.Kind == "symbol_reference");
    }

    [Fact]
    public async Task Scan_CSharpEnrichmentCancellation_ReusesLexicalPayloadWithoutRescan()
    {
        using var tempDir = TestTempDirectory.Create("search-pattern-cancellation-");
        const string source = "namespace Project; public sealed class Target { }";
        using var solution = CreateSolution(tempDir.DirectoryPath, source);
        File.WriteAllText(Path.Combine(tempDir.DirectoryPath, "src", "Project", "Project.cs"), source);
        using var cancellation = new CancellationTokenSource();
        var parameters = CreateParameters(solution.Solution, new("Target")
        {
            EnrichCSharp = true,
            CancellationToken = cancellation.Token,
        });
        var scanCalls = 0;

        SearchPatternScanResult ScanOnce(SearchPatternScannerParameters scanParameters)
        {
            scanCalls++;
            return SearchPatternScanner.Scan(scanParameters);
        }

        Task<IReadOnlyList<SearchPatternMatch>> CancelDuringEnrichment(
            Solution solution,
            IReadOnlyList<SearchPatternMatch> matches,
            CancellationToken token)
        {
            _ = solution;
            _ = matches;
            cancellation.Cancel();
            return Task.FromCanceled<IReadOnlyList<SearchPatternMatch>>(token);
        }

        var result = await SearchPatternScannerEnrichment.ScanAsync(
            parameters,
            ScanOnce,
            CancelDuringEnrichment);

        var match = Assert.Single(result.Payload.Matches);
        Assert.Equal(1, scanCalls);
        Assert.Equal("src/Project/Project.cs", match.FilePath);
        Assert.Contains("Target", match.LineText, StringComparison.Ordinal);
        Assert.Null(match.Semantic);
        Assert.Equal(1, result.Payload.Completeness.MatchedFileCount);
        Assert.Equal(1, result.Payload.Completeness.TotalMatchedLineCount);
        Assert.False(result.Payload.Completeness.ScanCompleted);
        Assert.True(result.Payload.Completeness.CancellationRequested);
        Assert.Contains("cancellation", result.Payload.Completeness.TruncatedBy);
        Assert.Equal("resident-solution", result.Payload.Snapshot.Source);
        Assert.Equal(1, result.Payload.Snapshot.ProjectCount);
    }

    [Fact]
    public async Task Scan_CSharpEnrichment_MarksAmbiguousAndUnavailableCases()
    {
        using var tempDir = TestTempDirectory.Create("search-pattern-roslyn-");
        const string source = "using A;\nusing B;\nnamespace A { public sealed class Ambiguous { } }\nnamespace B { public sealed class Ambiguous { } }\nnamespace Project { public sealed class Use { public object Create() => new Ambiguous(); } }";
        using var solution = CreateSolution(tempDir.DirectoryPath, source);
        var projectDir = Path.Combine(tempDir.DirectoryPath, "src", "Project");
        File.WriteAllText(Path.Combine(projectDir, "Project.cs"), source);
        File.WriteAllText(Path.Combine(projectDir, "Orphan.cs"), "namespace Project; public sealed class Orphan { }");

        var result = await SearchPatternScannerEnrichment.ScanAsync(CreateParameters(
            solution.Solution,
            new("Ambiguous") { EnrichCSharp = true }));

        var ambiguous = Assert.Single(result.Payload.Matches, match => match.Line == 5);
        Assert.Equal("unknown", ambiguous.Semantic!.Kind);
        Assert.Equal("ambiguous", ambiguous.Semantic.Resolution);

        var unavailableResult = await SearchPatternScannerEnrichment.ScanAsync(CreateParameters(
            solution.Solution,
            new("Orphan") { EnrichCSharp = true }));
        var unavailable = Assert.Single(unavailableResult.Payload.Matches);
        Assert.Equal("unknown", unavailable.Semantic!.Kind);
        Assert.Equal("unavailable", unavailable.Semantic.Resolution);
    }

    [Fact]
    public async Task Scan_EnrichmentDisabled_LeavesSemanticFieldUnset()
    {
        using var tempDir = TestTempDirectory.Create("search-pattern-roslyn-");
        using var solution = CreateSolution(tempDir.DirectoryPath);
        File.WriteAllText(Path.Combine(tempDir.DirectoryPath, "src", "Project", "Project.cs"),
            "namespace Project; public sealed class Target { }");

        var result = await SearchPatternScannerEnrichment.ScanAsync(CreateParameters(
            solution.Solution,
            new("Target")));

        Assert.Null(Assert.Single(result.Payload.Matches).Semantic);
    }

    [Fact]
    public void Scan_RegexUsesSameRangeModelAndStableOrdering()
    {
        using var tempDir = TestTempDirectory.Create("search-pattern-");
        using var solution = CreateSolution(tempDir.DirectoryPath);
        var projectDir = Path.Combine(tempDir.DirectoryPath, "src", "Project");
        File.WriteAllText(Path.Combine(projectDir, "b.txt"), "anchor");
        File.WriteAllText(Path.Combine(projectDir, "a.txt"), "anchor");

        var result = SearchPatternScanner.Scan(CreateParameters(solution.Solution, new("^anchor$") { IsRegex = true }));

        Assert.Equal(new[] { "src/Project/a.txt", "src/Project/b.txt" }, result.Payload.Matches.Select(m => m.FilePath));
        Assert.Equal(new[] { new SearchPatternMatchRange(1, 6) }, result.Payload.Matches[0].MatchRanges);
    }

    [Fact]
    public void Scan_ContextLines_PreservesUnchangedLineText()
    {
        using var tempDir = TestTempDirectory.Create("search-pattern-");
        using var solution = CreateSolution(tempDir.DirectoryPath);
        File.WriteAllText(
            Path.Combine(tempDir.DirectoryPath, "src", "Project", "context.txt"),
            "one\n two  \nthree\nfour");

        var result = SearchPatternScanner.Scan(CreateParameters(solution.Solution, new("two") { ContextLines = 1 }));
        var match = Assert.Single(result.Payload.Matches);

        Assert.Equal(" two  ", match.LineText);
        Assert.Equal(new[] { "one" }, match.ContextBefore);
        Assert.Equal(new[] { "three" }, match.ContextAfter);
    }

    [Fact]
    public void Scan_MaxFilesAndMaxResponseBytes_ReportSeparateTruncationReasons()
    {
        using var tempDir = TestTempDirectory.Create("search-pattern-");
        using var solution = CreateSolution(tempDir.DirectoryPath);
        var projectDir = Path.Combine(tempDir.DirectoryPath, "src", "Project");
        File.WriteAllText(Path.Combine(projectDir, "a.txt"), "anchor");
        File.WriteAllText(Path.Combine(projectDir, "b.txt"), "anchor");

        var fileLimited = SearchPatternScanner.Scan(CreateParameters(solution.Solution, new("anchor") { MaxFiles = 1 }));
        var byteLimited = SearchPatternScanner.Scan(CreateParameters(solution.Solution, new("anchor") { MaxResponseBytes = 200 }));

        Assert.Contains("maxFiles", fileLimited.Payload.Completeness.TruncatedBy);
        Assert.DoesNotContain("maxResponseBytes", fileLimited.Payload.Completeness.TruncatedBy);
        Assert.Contains("maxResponseBytes", byteLimited.Payload.Completeness.TruncatedBy);
        Assert.Equal(2, byteLimited.Payload.Completeness.TotalMatchedLineCount);
    }

    [Fact]
    public void Scan_MaxResults_CountsOnlyVisibleMatchedFiles()
    {
        using var tempDir = TestTempDirectory.Create("search-pattern-");
        using var solution = CreateSolution(tempDir.DirectoryPath);
        var projectDir = Path.Combine(tempDir.DirectoryPath, "src", "Project");
        File.WriteAllText(Path.Combine(projectDir, "a.txt"), "anchor\nanchor");
        File.WriteAllText(Path.Combine(projectDir, "b.txt"), "anchor");

        var result = SearchPatternScanner.Scan(CreateParameters(
            solution.Solution,
            new("anchor") { MaxResults = 1 }));

        Assert.Single(result.Payload.Matches);
        Assert.Equal(1, result.Payload.Completeness.ShownMatchedFileCount);
        Assert.Equal(3, result.Payload.Completeness.TotalMatchedLineCount);
        Assert.Contains("maxResults", result.Payload.Completeness.TruncatedBy);
    }

    [Fact]
    public void Scan_ScopeFiltersAndDefaultExclusions_StayInsideSolutionRoot()
    {
        using var tempDir = TestTempDirectory.Create("search-pattern-");
        using var solution = CreateSolution(tempDir.DirectoryPath);
        File.WriteAllText(Path.Combine(tempDir.DirectoryPath, "root.txt"), "anchor");
        File.WriteAllText(Path.Combine(tempDir.DirectoryPath, "src", "Project", "scope.json"), "anchor");
        Directory.CreateDirectory(Path.Combine(tempDir.DirectoryPath, "obj"));
        File.WriteAllText(Path.Combine(tempDir.DirectoryPath, "obj", "generated.txt"), "anchor");

        var result = SearchPatternScanner.Scan(CreateParameters(
            solution.Solution,
            new("anchor") { Scope = "src", IncludePatterns = new[] { "**/*.json" } }));

        var match = Assert.Single(result.Payload.Matches);
        Assert.Equal("src/Project/scope.json", match.FilePath);
        Assert.DoesNotContain(result.Payload.Matches, m => m.FilePath.Contains("obj", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Scan_GeneratedFileNamesOutsideBuildDirectories_AreExcluded()
    {
        using var tempDir = TestTempDirectory.Create("search-pattern-");
        using var solution = CreateSolution(tempDir.DirectoryPath);
        var projectDir = Path.Combine(tempDir.DirectoryPath, "src", "Project");
        File.WriteAllText(Path.Combine(projectDir, "Generated.g.cs"), "anchor");
        File.WriteAllText(Path.Combine(projectDir, "Project.AssemblyAttributes.cs"), "anchor");
        File.WriteAllText(Path.Combine(projectDir, "regular.txt"), "anchor");

        var result = SearchPatternScanner.Scan(CreateParameters(solution.Solution, new("anchor")));

        var match = Assert.Single(result.Payload.Matches);
        Assert.Equal("src/Project/regular.txt", match.FilePath);
    }

    [Fact]
    public void SafeEnumeration_CancellationStopsBetweenEnumerationUnits()
    {
        using var tempDir = TestTempDirectory.Create("search-pattern-");
        for (var index = 0; index < 3; index++)
        {
            File.WriteAllText(Path.Combine(tempDir.DirectoryPath, $"file-{index}.txt"), "content");
        }

        using var cancellation = new CancellationTokenSource();
        var enumeration = FileSystemExclusionHelpers.SafeEnumerateFilesWithErrors(
            tempDir.DirectoryPath,
            cancellation.Token);
        using var files = enumeration.Files.GetEnumerator();

        Assert.True(files.MoveNext());
        cancellation.Cancel();

        Assert.False(files.MoveNext());
    }

    [Fact]
    public void LegacyScan_RegexTimeout_IsExposedAsStatus()
    {
        using var tempDir = TestTempDirectory.Create("search-pattern-");
        using var solution = CreateSolution(tempDir.DirectoryPath);
        var path = Path.Combine(tempDir.DirectoryPath, "src", "Project", "legacy-timeout.txt");
        File.WriteAllText(path, string.Concat(Enumerable.Repeat("a", 100_000)) + "!");

        var result = SearchPatternLegacyFileHitScanner.Scan(solution.Solution, "^(a+)+$", isRegex: true);

        Assert.True(result.RegexTimedOut);
        Assert.True(result.HasErrors);
    }

    [Fact]
    public void Scan_BinaryUnreadableAndCancelledFiles_AreReflectedInCompleteness()
    {
        using var tempDir = TestTempDirectory.Create("search-pattern-");
        using var solution = CreateSolution(tempDir.DirectoryPath);
        File.WriteAllBytes(Path.Combine(tempDir.DirectoryPath, "binary.dat"), new byte[] { 0, 1, 2 });
        File.WriteAllBytes(Path.Combine(tempDir.DirectoryPath, "invalid.txt"), new byte[] { 0xFF, 0xFF });

        var result = SearchPatternScanner.Scan(CreateParameters(solution.Solution, new("anchor")));
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        var cancelledResult = SearchPatternScanner.Scan(CreateParameters(
            solution.Solution,
            new("anchor") { CancellationToken = cancelled.Token }));

        Assert.Equal(1, result.Payload.Completeness.SkippedBinaryFileCount);
        Assert.Equal(1, result.Payload.Completeness.SkippedUnreadableFileCount);
        Assert.Contains("cancellation", cancelledResult.Payload.Completeness.TruncatedBy);
        Assert.False(cancelledResult.Payload.Completeness.ScanCompleted);
    }

    private static RoslynTestSolution CreateSolution(string root, string source = "namespace Project; public sealed class ProjectType { }")
    {
        Directory.CreateDirectory(Path.Combine(root, "src", "Project"));
        return RoslynTestSolutionFactory.CreateSolution(
            Path.Combine(root, "Fixture.slnx"),
            new ProjectSpec(
                "Project",
                [("Project.cs", source)],
                VirtualProjectDirectory: Path.Combine("src", "Project")));
    }

    private static SearchPatternScannerParameters CreateParameters(
        Microsoft.CodeAnalysis.Solution solution,
        SearchPatternTestOptions options) =>
        new(
            solution,
            options.Pattern,
            options.IsRegex,
            options.MaxResults,
            options.MaxFiles,
            options.ContextLines,
            options.MaxResponseBytes,
            options.Scope,
            options.IncludePatterns,
            null,
            options.CancellationToken,
            options.EnrichCSharp,
            options.ScopeType);

    [Fact]
    public void Scan_ScopeTypeProduction_ExcludesTestFiles()
    {
        using var tempDir = TestTempDirectory.Create("search-pattern-scope-prod-");
        using var solution = CreateSolution(tempDir.DirectoryPath);
        var prodPath = Path.Combine(tempDir.DirectoryPath, "src", "Project", "Service.cs");
        var testPath = Path.Combine(tempDir.DirectoryPath, "src", "Project", "ServiceTests.cs");
        File.WriteAllText(prodPath, "public class Service { void Do() {} }");
        File.WriteAllText(testPath, "public class ServiceTests { void TestDo() {} }");

        var result = SearchPatternScanner.Scan(CreateParameters(solution.Solution, new("Service") { ScopeType = "production" }));

        Assert.Contains(result.Payload.Matches, m => m.FilePath.Contains("Service.cs") && !m.FilePath.Contains("ServiceTests.cs"));
        Assert.DoesNotContain(result.Payload.Matches, m => m.FilePath.Contains("ServiceTests.cs"));
    }

    [Fact]
    public void Scan_ScopeTypeTests_IncludesOnlyTestFiles()
    {
        using var tempDir = TestTempDirectory.Create("search-pattern-scope-test-");
        using var solution = CreateSolution(tempDir.DirectoryPath);
        var prodPath = Path.Combine(tempDir.DirectoryPath, "src", "Project", "Service.cs");
        var testPath = Path.Combine(tempDir.DirectoryPath, "src", "Project", "ServiceTests.cs");
        File.WriteAllText(prodPath, "public class Service { void Do() {} }");
        File.WriteAllText(testPath, "public class ServiceTests { void TestDo() {} }");

        var result = SearchPatternScanner.Scan(CreateParameters(solution.Solution, new("Service") { ScopeType = "tests" }));

        Assert.Single(result.Payload.Matches);
        Assert.Contains("ServiceTests.cs", result.Payload.Matches[0].FilePath);
    }

    [Fact]
    public void Format_ZeroHitsWithWildcardAndPlainSearch_AppendsWildcardHint()
    {
        using var tempDir = TestTempDirectory.Create("search-pattern-hint-");
        using var solution = CreateSolution(tempDir.DirectoryPath);

        var result = SearchPatternScanner.Scan(CreateParameters(solution.Solution, new("*NonExistent*") { IsRegex = false }));
        var formatted = SearchPatternLegacyFormatter.Format(result);

        Assert.Contains("0 Treffer", formatted);
        Assert.Contains("Wildcard", formatted);
        Assert.Contains("isRegex: true", formatted);
    }

    [Fact]
    public void Scan_LikelyRegexPatternWithoutIsRegex_AutoDetectsAndFindsHits()
    {
        using var tempDir = TestTempDirectory.Create("search-pattern-autodetect-");
        using var solution = CreateSolution(tempDir.DirectoryPath);
        var path = Path.Combine(tempDir.DirectoryPath, "src", "Project", "Service.cs");
        File.WriteAllText(path, "public class AnchorService {}");

        // Sucht mit \s+ und \w+ ohne isRegex anzugeben
        var result = SearchPatternScanner.Scan(CreateParameters(solution.Solution, new(@"class\s+\w+Service")));

        Assert.Single(result.Payload.Matches);
        Assert.True(result.IsRegex);
    }

    [Fact]
    public void Scan_PlainPatternWithCSharpCode_FindsHitsWithoutRegexFalsePositives()
    {
        using var tempDir = TestTempDirectory.Create("search-pattern-plain-code-");
        using var solution = CreateSolution(tempDir.DirectoryPath);
        var path = Path.Combine(tempDir.DirectoryPath, "src", "Project", "Code.cs");
        File.WriteAllText(path, "int[] buffer = new int[10];");

        // int[] enthaelt eckige Klammern (in Regex eine Zeichenklasse), soll aber als C#-Code literal matchen
        var result = SearchPatternScanner.Scan(CreateParameters(solution.Solution, new("int[]")));

        var match = Assert.Single(result.Payload.Matches);
        Assert.Contains("int[]", match.LineText);
        Assert.False(result.IsRegexAutoPromoted);
    }

    [Fact]
    public void Scan_ZeroPlainHitsWithWildcard_AutoPromotesToRegexAndFindsHits()
    {
        using var tempDir = TestTempDirectory.Create("search-pattern-autopromote-");
        using var solution = CreateSolution(tempDir.DirectoryPath);
        var path = Path.Combine(tempDir.DirectoryPath, "src", "Project", "Customer.cs");
        File.WriteAllText(path, "public class CustomerService {}");

        // *Service enthaelt kein Literal-Sternchen im Quelltext, Plain-Suche liefert 0 Treffer
        // Auto-Promotion erkennt die Wildcard und findet CustomerService
        var result = SearchPatternScanner.Scan(CreateParameters(solution.Solution, new("*Service")));

        var match = Assert.Single(result.Payload.Matches);
        Assert.Contains("CustomerService", match.LineText);
        Assert.True(result.IsRegexAutoPromoted);
    }

    [Fact]
    public void Scan_ZeroPlainHitsWithMethodParentheses_AutoPromotesToMethodCallRegex()
    {
        using var tempDir = TestTempDirectory.Create("search-pattern-method-paren-");
        using var solution = CreateSolution(tempDir.DirectoryPath);
        var path = Path.Combine(tempDir.DirectoryPath, "src", "Project", "Worker.cs");
        File.WriteAllText(path, "public async Task<int> CalculateAsync(int x, int y) { return x + y; }");

        // LLM sucht nach "CalculateAsync()", im Code hat die Methode aber Parameter (int x, int y)
        var result = SearchPatternScanner.Scan(CreateParameters(solution.Solution, new("CalculateAsync()")));

        var match = Assert.Single(result.Payload.Matches);
        Assert.Contains("CalculateAsync(int x, int y)", match.LineText);
        Assert.True(result.IsRegexAutoPromoted);
    }

    [Fact]
    public void Scan_ZeroPlainHitsWithGenericTypeParameter_AutoPromotesToGenericRegex()
    {
        using var tempDir = TestTempDirectory.Create("search-pattern-generic-type-");
        using var solution = CreateSolution(tempDir.DirectoryPath);
        var path = Path.Combine(tempDir.DirectoryPath, "src", "Project", "Repo.cs");
        File.WriteAllText(path, "public sealed class ResultRepository : IRepository<Customer> { }");

        // LLM sucht nach "IRepository<T>", im Code steht aber IRepository<Customer>
        var result = SearchPatternScanner.Scan(CreateParameters(solution.Solution, new("IRepository<T>")));

        var match = Assert.Single(result.Payload.Matches);
        Assert.Contains("IRepository<Customer>", match.LineText);
        Assert.True(result.IsRegexAutoPromoted);
    }

    [Fact]
    public void Scan_ZeroPlainHitsWithQuotedOrBacktickedPattern_AutoPromotesAndFindsHits()
    {
        using var tempDir = TestTempDirectory.Create("search-pattern-quoted-");
        using var solution = CreateSolution(tempDir.DirectoryPath);
        var path = Path.Combine(tempDir.DirectoryPath, "src", "Project", "Service.cs");
        File.WriteAllText(path, "public sealed class OrderProcessor { }");

        // LLM uebergibt Backticks aus Markdown (`OrderProcessor`)
        var result = SearchPatternScanner.Scan(CreateParameters(solution.Solution, new("`OrderProcessor`")));

        var match = Assert.Single(result.Payload.Matches);
        Assert.Contains("OrderProcessor", match.LineText);
        Assert.True(result.IsRegexAutoPromoted);
    }

    [Fact]
    public void Scan_ScopeWithAbsolutePathInsideSolutionRoot_NormalizesAndMatches()
    {
        using var tempDir = TestTempDirectory.Create("search-pattern-absolute-scope-");
        using var solution = CreateSolution(tempDir.DirectoryPath);
        var path = Path.Combine(tempDir.DirectoryPath, "src", "Project", "Code.cs");
        File.WriteAllText(path, "string key = \"TargetValue\";");

        var absoluteScope = Path.Combine(tempDir.DirectoryPath, "src");
        var result = SearchPatternScanner.Scan(CreateParameters(solution.Solution, new("TargetValue") { Scope = absoluteScope }));

        var match = Assert.Single(result.Payload.Matches);
        Assert.Contains("TargetValue", match.LineText);
    }

    private sealed record SearchPatternTestOptions(string Pattern)
    {
        internal bool? IsRegex { get; init; }
        internal int MaxResults { get; init; } = 50;
        internal int MaxFiles { get; init; }
        internal int ContextLines { get; init; }
        internal int MaxResponseBytes { get; init; }
        internal string? Scope { get; init; }
        internal string[]? IncludePatterns { get; init; }
        internal CancellationToken CancellationToken { get; init; }
        internal bool EnrichCSharp { get; init; }
        internal string? ScopeType { get; init; }
    }
}
