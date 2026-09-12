#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp.Tools.Verify;
using AiNetLinter.Mcp.Tools.Verify.DeadCode;
using AiNetLinter.Mcp.Tools.Verify.MagicValues;
using AiNetLinter.TestKit;
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
            CancellationToken.None);

        Assert.Equal("complete", result.Completeness);
        Assert.Equal(4, result.TotalCount);
        Assert.Collection(
            result.Entries,
            entry => Assert.Equal("dead_code", entry.RuleOrCategory),
            entry => Assert.Equal("dead_code", entry.RuleOrCategory),
            entry => Assert.StartsWith("magic_value:", entry.RuleOrCategory, StringComparison.Ordinal),
            entry => Assert.StartsWith("magic_value:", entry.RuleOrCategory, StringComparison.Ordinal));
        Assert.Equal(
            result.Entries.Select(entry => entry.HandoffId).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
            result.Entries.Count);
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
            CancellationToken.None);
        var expectedTotal = await CollectPerFileTotalAsync(testSolution.Solution, scopeFiles);

        Assert.Equal(expectedTotal, combined.TotalCount);
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
                new DeadCodeAdvisoryOptions(MaxResults: int.MaxValue, ScopeFiles: fileScope),
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
}
