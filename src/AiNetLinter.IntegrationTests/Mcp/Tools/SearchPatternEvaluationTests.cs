#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Tools;
using AiNetLinter.Mcp.Tools.Analysis;
using ModelContextProtocol.Protocol;

namespace AiNetLinter.IntegrationTests.Mcp.Tools;

[Trait("Category", "Integration")]
public sealed class SearchPatternEvaluationTests
{
    private readonly SymbolGraphCatalogFixture fixture;
    private readonly ITestOutputHelper output;

    public SearchPatternEvaluationTests(SymbolGraphCatalogFixture fixture, ITestOutputHelper output)
    {
        this.fixture = fixture;
        this.output = output;
    }

    [Fact]
    public async Task ExecuteAsync_MixedFixture_ReportsOracleBudgetsAndWireBytes()
    {
        using var state = fixture.CreateReadOnlyServer();
        var oracle = await ObserveAsync(state, Arguments(new("search-anchor")));
        Assert.Equal(2, oracle.MatchedFileCount);
        Assert.Equal(2, oracle.TotalMatchedLineCount);
        Assert.Equal(2, oracle.ShownMatchedLineCount);
        Assert.Equal(new[] { "src/SymbolGraphMini/search-fixture.md", "src/SymbolGraphMini/wwwroot/search-fixture.json" }, oracle.Paths);
        Assert.Equal(JsonValueKind.Object, JsonDocument.Parse(oracle.StructuredJson).RootElement.ValueKind);

        var regex = await ObserveAsync(state, Arguments(new("search-anchor") { IsRegex = true }));
        Assert.Equal(oracle.Paths, regex.Paths);
        var context = await ObserveAsync(state, Arguments(new("search-anchor") { ContextLines = 1 }));
        Assert.Equal(oracle.Paths, context.Paths);
        var maxResults = await ObserveAsync(state, Arguments(new("search-anchor") { MaxResults = 1 }));
        var maxFiles = await ObserveAsync(state, Arguments(new("search-anchor") { MaxFiles = 1 }));
        var maxBytes = await ObserveAsync(state, Arguments(new("search-anchor") { MaxResponseBytes = 200 }));
        AssertBudgetExplained(oracle, maxResults, "maxResults");
        AssertBudgetExplained(oracle, maxFiles, "maxFiles");
        AssertBudgetExplained(oracle, maxBytes, "maxResponseBytes");

        var enriched = await ObserveAsync(state, Arguments(new("Greeter") { EnrichCSharp = true }));
        Assert.Contains("\"resolution\":\"resolved\"", enriched.StructuredJson, StringComparison.Ordinal);
        Assert.Contains("Greeter.cs", enriched.LegacyText, StringComparison.Ordinal);
        var mixedTypes = await ObserveAsync(state, Arguments(new("userService")));
        Assert.Equal(3, mixedTypes.MatchedFileCount);
        Assert.Contains(mixedTypes.Paths, path => path.EndsWith(".js", StringComparison.Ordinal));
        Assert.Contains(mixedTypes.Paths, path => path.EndsWith(".razor", StringComparison.Ordinal));
        Assert.Contains(mixedTypes.Paths, path => path.EndsWith(".xaml", StringComparison.Ordinal));

        await WriteTimingAsync(state, "plain-oracle", Arguments(new("search-anchor")));
        await WriteTimingAsync(state, "regex-oracle", Arguments(new("search-anchor") { IsRegex = true }));
        await WriteTimingAsync(state, "max-response-bytes", Arguments(new("search-anchor") { MaxResponseBytes = 200 }));
        WriteBytes("plain-oracle", oracle);
        WriteBytes("regex-oracle", regex);
        WriteBytes("context", context);
        WriteBytes("max-results", maxResults);
        WriteBytes("max-files", maxFiles);
        WriteBytes("max-response", maxBytes);
        WriteBytes("enrich-csharp", enriched);
        WriteBytes("mixed-filetypes", mixedTypes);
    }

    [Fact]
    public async Task ExecuteAsync_BudgetedScope_UsesExactlyOneDefinedFollowUpCall()
    {
        using var state = fixture.CreateReadOnlyServer();
        var oracle = await ObserveAsync(state, Arguments(new("search-anchor")));
        var first = await ObserveAsync(state, Arguments(new("search-anchor") { MaxFiles = 1 }));
        var targetPath = "src/SymbolGraphMini/wwwroot/search-fixture.json";
        Assert.True(first.Truncated);
        Assert.Equal(0, first.FollowUpCalls);
        Assert.DoesNotContain(targetPath, first.Paths);

        var followUp = await ObserveAsync(state, Arguments(new("search-anchor")
        {
            Scope = "src/SymbolGraphMini/wwwroot",
            IncludePatterns = ["**/*.json"],
        }),
            followUpCalls: 1);
        Assert.Contains(targetPath, followUp.Paths);
        Assert.Equal(1, followUp.FollowUpCalls);
        Assert.Equal(0, oracle.FollowUpCalls);
        output.WriteLine($"follow-up proxy: oracle={oracle.FollowUpCalls} budgeted={first.FollowUpCalls} continuation={followUp.FollowUpCalls}");
    }

    [Fact]
    public async Task ExecuteAsync_OverlayProblemFiles_ReportsSkipsAndKeepsLegacyText()
    {
        using var isolated = new SymbolGraphMiniFixtureWorkspace();
        CreateFile(isolated.RootPath, "visible-evaluation.txt", "problem-anchor");
        CreateFile(isolated.RootPath, "obj/Debug/Generated.cs", "problem-anchor");
        CreateFile(isolated.RootPath, "Generated.g.cs", "problem-anchor");
        CreateFile(isolated.RootPath, "bundle.min.js", "problem-anchor");
        CreateBytes(isolated.RootPath, "binary-anchor.bin", [0, 1, 0, 2]);
        CreateBytes(isolated.RootPath, "invalid-encoding.dat", [0xC3]);
        var catalog = await LoadedFixture.LoadCatalogAsync(isolated.RootPath);
        using var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(
            new McpCodeGraphServerOptionsFromParameters(catalog)));

        var result = await ObserveAsync(state, Arguments(new("problem-anchor")));
        Assert.Equal(new[] { "visible-evaluation.txt" }, result.Paths.Select(path => path.Split('/').Last()));
        Assert.Equal(1, result.SkippedBinaryFileCount);
        Assert.Equal(1, result.SkippedUnreadableFileCount);
        Assert.DoesNotContain("Generated.g.cs", result.LegacyText, StringComparison.Ordinal);
        Assert.DoesNotContain("bundle.min.js", result.LegacyText, StringComparison.Ordinal);
        Assert.True(result.CombinedToolUtf8Bytes > result.StructuredPayloadUtf8Bytes);
    }

    private async Task WriteTimingAsync(McpCodeGraphServer state, string caseId, SearchPatternToolArguments arguments)
    {
        await ObserveAsync(state, arguments);
        var durations = new List<double>();
        for (var index = 0; index < 7; index++)
        {
            var stopwatch = Stopwatch.StartNew();
            var observation = await ObserveAsync(state, arguments);
            stopwatch.Stop();
            durations.Add(stopwatch.Elapsed.TotalMilliseconds);
            if (index > 0) Assert.Equal(observation.StructuredJson, (await ObserveAsync(state, arguments)).StructuredJson);
        }

        durations.Sort();
        output.WriteLine($"{caseId}: warmup=1 iterations=7 min={durations[0]:F3} median={durations[3]:F3} p95={durations[^1]:F3} ms");
    }

    private static void AssertBudgetExplained(ToolObservation oracle, ToolObservation budgeted, string reason)
    {
        Assert.Contains(reason, budgeted.TruncatedBy);
        Assert.True(budgeted.ShownMatchedLineCount <= oracle.TotalMatchedLineCount);
        Assert.True(budgeted.ShownMatchedFileCount <= oracle.MatchedFileCount);
        Assert.True(budgeted.Truncated);
    }

    private void WriteBytes(string caseId, ToolObservation observation) => output.WriteLine(
        $"{caseId} bytes: legacy={observation.LegacyUtf8Bytes} structured={observation.StructuredPayloadUtf8Bytes} combined={observation.CombinedToolUtf8Bytes}");

    private static void CreateFile(string rootPath, string relativePath, string content)
    {
        var path = System.IO.Path.Combine(rootPath, relativePath);
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
        System.IO.File.WriteAllText(path, content);
    }

    private static void CreateBytes(string rootPath, string relativePath, byte[] content)
    {
        var path = System.IO.Path.Combine(rootPath, relativePath);
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
        System.IO.File.WriteAllBytes(path, content);
    }

    private static async Task<ToolObservation> ObserveAsync(
        McpCodeGraphServer state,
        SearchPatternToolArguments arguments,
        int followUpCalls = 0)
    {
        var result = await SearchPatternTool.ExecuteAsync(state, arguments, CancellationToken.None);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.NotNull(result.StructuredContent);
        var structuredJson = result.StructuredContent!.Value.GetRawText();
        using var document = JsonDocument.Parse(structuredJson);
        var root = document.RootElement;
        var matches = root.GetProperty("matches");
        var completeness = root.GetProperty("completeness");
        return new ToolObservation(
            text,
            structuredJson,
            matches.EnumerateArray().Select(match => match.GetProperty("filePath").GetString()!).ToArray(),
            completeness.GetProperty("matchedFileCount").GetInt32(),
            completeness.GetProperty("shownMatchedFileCount").GetInt32(),
            completeness.GetProperty("totalMatchedLineCount").GetInt32(),
            completeness.GetProperty("shownMatchedLineCount").GetInt32(),
            completeness.GetProperty("truncated").GetBoolean(),
            completeness.GetProperty("truncatedBy").EnumerateArray().Select(item => item.GetString()!).ToArray(),
            completeness.GetProperty("skippedBinaryFileCount").GetInt32(),
            completeness.GetProperty("skippedUnreadableFileCount").GetInt32(),
            followUpCalls,
            Encoding.UTF8.GetByteCount(text),
            Encoding.UTF8.GetByteCount(structuredJson),
            Encoding.UTF8.GetByteCount(text) + Encoding.UTF8.GetByteCount(structuredJson));
    }

    private static SearchPatternToolArguments Arguments(SearchPatternArgumentOptions options) =>
        new(
            options.Pattern,
            options.IsRegex,
            options.MaxResults,
            options.MaxFiles,
            options.ContextLines,
            options.MaxResponseBytes,
            options.Scope,
            options.IncludePatterns,
            null,
            options.EnrichCSharp);

    private sealed record SearchPatternArgumentOptions(string Pattern)
    {
        internal bool IsRegex { get; init; }
        internal int MaxResults { get; init; } = 50;
        internal int MaxFiles { get; init; }
        internal int ContextLines { get; init; }
        internal int MaxResponseBytes { get; init; }
        internal string? Scope { get; init; }
        internal string[]? IncludePatterns { get; init; }
        internal bool EnrichCSharp { get; init; }
    }

    private sealed record ToolObservation(
        string LegacyText,
        string StructuredJson,
        IReadOnlyList<string> Paths,
        int MatchedFileCount,
        int ShownMatchedFileCount,
        int TotalMatchedLineCount,
        int ShownMatchedLineCount,
        bool Truncated,
        IReadOnlyList<string> TruncatedBy,
        int SkippedBinaryFileCount,
        int SkippedUnreadableFileCount,
        int FollowUpCalls,
        int LegacyUtf8Bytes,
        int StructuredPayloadUtf8Bytes,
        int CombinedToolUtf8Bytes);
}
