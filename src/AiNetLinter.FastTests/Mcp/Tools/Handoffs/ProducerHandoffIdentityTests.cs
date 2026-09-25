#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.FastTests.Fixtures;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Tools;
using AiNetLinter.Mcp.Tools.Analysis;
using AiNetLinter.Mcp.Tools.DuplicateDetection;
using AiNetLinter.Mcp.Tools.MetricsLookup;
using AiNetLinter.Mcp.Tools.SymbolGraph;
using AiNetLinter.TestKit;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Tools.Handoffs;

[Trait("Category", "Component")]
public sealed class ProducerHandoffIdentityTests
{
    [Fact]
    public async Task MetricsLookupHandoff_FromActualOutputResolvesItsProject()
    {
        using var scenario = CreateDuplicateProjectContext();
        var context = scenario.Context;
        var state = context.CreateServer();
        var sourceHandle = await FindHandleAsync(state, "Measure", "src/First/Service.cs");

        var metrics = await MetricsLookupTool.ExecuteAsync(state, [sourceHandle], CancellationToken.None);
        var metricsText = TextOf(metrics);
        var metricsHandle = ExtractMetricsHandoff(metricsText);

        var body = await GetSymbolBodyTool.ExecuteAsync(state, [metricsHandle], 80, CancellationToken.None);
        var references = await FindReferencesTool.ExecuteAsync(
            state, new FindReferencesRequest(metricsHandle, MaxResults: 50, Depth: 1), CancellationToken.None);

        AssertProjectResolution(body, references, "src/First/Service.cs", "src/Second/Service.cs", "FIRST_PROJECT_BODY", "SECOND_PROJECT_BODY");
    }

    [Fact]
    public async Task SearchPatternCSharpEnrichmentHandoff_FromActualOutputResolvesItsProject()
    {
        using var scenario = CreateDuplicateProjectContext();
        var context = scenario.Context;
        var state = context.CreateServer();

        var result = await SearchPatternTool.ExecuteAsync(
            state,
            new SearchPatternToolArguments("Measure(int", false, 50, 0, 0, 8_192, null, null, null, EnrichCSharp: true),
            CancellationToken.None);
        var text = TextOf(result);
        var row = text.Split('\n').SingleOrDefault(line =>
            line.Contains("src/First/Service.cs", StringComparison.Ordinal)
            && line.Contains("handoffId: `h:", StringComparison.Ordinal));
        Assert.True(row is not null, text);
        var handoff = ExtractHandoff(row!);

        var body = await GetSymbolBodyTool.ExecuteAsync(state, [handoff], 80, CancellationToken.None);
        var references = await FindReferencesTool.ExecuteAsync(
            state, new FindReferencesRequest(handoff, MaxResults: 50, Depth: 1), CancellationToken.None);

        AssertProjectResolution(body, references, "src/First/Service.cs", "src/Second/Service.cs", "FIRST_PROJECT_BODY", "SECOND_PROJECT_BODY");
    }

    [Fact]
    public async Task FindDuplicatesCloneHandoff_FromActualOutputResolvesItsProject()
    {
        using var scenario = CreateDuplicateProjectContext();
        var context = scenario.Context;
        var state = context.CreateServer();

        var result = await DuplicateDetectionTool.ExecuteAsync(
            state, new DuplicateDetectionInput(null, "exact", null, null, null), CancellationToken.None);
        var text = TextOf(result);
        var row = text.Split('\n').SingleOrDefault(line =>
            line.Contains("src/First/Service.cs", StringComparison.Ordinal)
            && line.Contains("handoffId: `h:", StringComparison.Ordinal));
        Assert.True(row is not null, text);
        var handoff = ExtractHandoff(row!);

        var body = await GetSymbolBodyTool.ExecuteAsync(state, [handoff], 80, CancellationToken.None);
        var references = await FindReferencesTool.ExecuteAsync(
            state, new FindReferencesRequest(handoff, MaxResults: 50, Depth: 1), CancellationToken.None);

        AssertProjectResolution(body, references, "src/First/Service.cs", "src/Second/Service.cs", "FIRST_PROJECT_BODY", "SECOND_PROJECT_BODY");
    }

    [Fact]
    public async Task FindDuplicatesRefactoringDriftHandoff_FromActualOutputResolvesItsProject()
    {
        using var scenario = CreateDuplicateProjectContext(includeDriftCandidate: true);
        var context = scenario.Context;
        var state = context.CreateServer();
        var helperHandle = await FindHandleAsync(state, "SharedHelper", "src/First/Service.cs");

        var result = await DuplicateDetectionTool.ExecuteAsync(
            state,
            new DuplicateDetectionInput(1, "fuzzy", null, null, null, "refactoring-drift", helperHandle),
            CancellationToken.None);
        var text = TextOf(result);
        var row = text.Split('\n').Single(line =>
            line.Contains("src/First/Service.cs", StringComparison.Ordinal)
            && line.Contains("handoffId: `h:", StringComparison.Ordinal));
        var handoff = ExtractHandoff(row);

        var body = await GetSymbolBodyTool.ExecuteAsync(state, [handoff], 80, CancellationToken.None);
        var references = await FindReferencesTool.ExecuteAsync(
            state, new FindReferencesRequest(handoff, MaxResults: 50, Depth: 1), CancellationToken.None);

        AssertProjectResolution(body, references, "src/First/Service.cs", "src/Second/Service.cs", "FIRST_DRIFT_BODY", "SECOND_DRIFT_BODY");
    }

    private static ProducerScenario CreateDuplicateProjectContext(bool includeDriftCandidate = false)
    {
        const string firstSource = """
            namespace Shared.Contracts;

            public sealed class SharedService
            {
                public int Measure(int value)
                {
                    // Project body marker: FIRST_PROJECT_BODY.
                    int first = value + 1;
                    int second = first * 3;
                    int third = second - 5;
                    int fourth = third ^ 7;
                    int fifth = fourth + value;
                    int sixth = fifth * 2;
                    int seventh = sixth - first;
                    return seventh;
                }
                public static int FirstCaller() => new SharedService().Measure(1);
            }
            """;
        const string secondSource = """
            namespace Shared.Contracts;

            public sealed class SharedService
            {
                public int Measure(int value)
                {
                    // Project body marker: SECOND_PROJECT_BODY.
                    int first = value + 1;
                    int second = first * 3;
                    int third = second - 5;
                    int fourth = third ^ 7;
                    int fifth = fourth + value;
                    int sixth = fifth * 2;
                    int seventh = sixth - first;
                    return seventh;
                }
                public static int SecondCaller() => new SharedService().Measure(2);
            }
            """;
        var first = includeDriftCandidate
            ? firstSource + "\n" + DriftMembers("First")
            : firstSource;
        var second = includeDriftCandidate
            ? secondSource + "\n" + DriftMembers("Second")
            : secondSource;

        var directory = TestTempDirectory.Create("producer-handoff-");
        foreach (var project in new[] { "First", "Second" })
        {
            Directory.CreateDirectory(Path.Combine(directory.DirectoryPath, "src", project));
        }
        File.WriteAllText(Path.Combine(directory.DirectoryPath, "src", "First", "Service.cs"), first);
        File.WriteAllText(Path.Combine(directory.DirectoryPath, "src", "Second", "Service.cs"), second);
        var solution = RoslynTestSolutionFactory.CreateSolution(
            Path.Combine(directory.DirectoryPath, "ProducerHandoffIdentity.slnx"),
            new ProjectSpec("First", [("Service.cs", first)], VirtualProjectDirectory: Path.Combine("src", "First")),
            new ProjectSpec("Second", [("Service.cs", second)], VirtualProjectDirectory: Path.Combine("src", "Second")));
        return new ProducerScenario(directory, new McpInMemoryTestContext(solution));
    }

    private static string DriftMembers(string project) => $$"""

        public static class DriftOperations
        {
            public static int SharedHelper(int value)
            {
                int first = value + 11;
                int second = first * 3;
                int third = second - 5;
                int fourth = third ^ 7;
                return fourth;
            }

            public static int RepeatedOperation(int value)
            {
                // Project body marker: {{project.ToUpperInvariant()}}_DRIFT_BODY.
                int first = value + 11;
                int second = first * 3;
                int third = second - 5;
                int fourth = third ^ 7;
                return fourth;
            }

            public static int {{project}}DriftCaller() => RepeatedOperation(3);
        }
        """;

    private static async Task<string> FindHandleAsync(McpCodeGraphServer state, string symbol, string expectedPath)
    {
        var result = await FindSymbolTool.ExecuteAsync(state, [symbol], "method", 50, CancellationToken.None);
        var text = TextOf(result);
        var row = text.Split('\n').Single(line =>
            line.Contains(expectedPath, StringComparison.Ordinal)
            && line.Contains("handoffId: `h:", StringComparison.Ordinal));
        return ExtractHandoff(row);
    }

    private static void AssertProjectResolution(
        CallToolResult body,
        CallToolResult references,
        string expectedPath,
        string otherPath,
        string expectedBodyMarker,
        string otherBodyMarker)
    {
        var bodyText = TextOf(body);
        var referenceText = TextOf(references);

        Assert.NotEqual(true, body.IsError);
        Assert.NotEqual(true, references.IsError);
        Assert.DoesNotContain("AMBIGUOUS_SYMBOL", bodyText, StringComparison.Ordinal);
        Assert.DoesNotContain("AMBIGUOUS_SYMBOL", referenceText, StringComparison.Ordinal);
        Assert.Contains("### Method:", bodyText, StringComparison.Ordinal);
        Assert.Contains(expectedBodyMarker, bodyText, StringComparison.Ordinal);
        Assert.DoesNotContain(otherBodyMarker, bodyText, StringComparison.Ordinal);
        Assert.Contains(ToPlatformPath(expectedPath), bodyText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(ToPlatformPath(otherPath), bodyText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(expectedPath, referenceText, StringComparison.Ordinal);
        Assert.DoesNotContain(otherPath, referenceText, StringComparison.Ordinal);
    }

    private static string ToPlatformPath(string path) =>
        path.Replace('/', Path.DirectorySeparatorChar);

    private static string ExtractHandoff(string text)
    {
        var match = Regex.Match(text, @"handoffId: `(?<id>h:[^`]+)`", RegexOptions.CultureInvariant);
        Assert.True(match.Success, text);
        return match.Groups["id"].Value;
    }

    private static string TextOf(CallToolResult result) =>
        Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;

    private static string ExtractMetricsHandoff(string text)
    {
        var handoff = Regex.Match(text, @"- \*\*Id:\*\* `(?<id>h:[^`]+)`", RegexOptions.CultureInvariant)
            .Groups["id"].Value;
        Assert.NotEmpty(handoff);
        return handoff;
    }

    private sealed class ProducerScenario(TestTempDirectory directory, McpInMemoryTestContext context) : IDisposable
    {
        internal McpInMemoryTestContext Context { get; } = context;

        public void Dispose()
        {
            Context.Dispose();
            directory.Dispose();
        }
    }
}
