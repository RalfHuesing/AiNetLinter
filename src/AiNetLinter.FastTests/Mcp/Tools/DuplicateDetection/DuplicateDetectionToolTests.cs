#nullable enable

using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Tools.DuplicateDetection;
using AiNetLinter.FastTests.Fixtures;
using AiNetLinter.TestKit;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Tools.DuplicateDetection;

/// <summary>
/// Tests fuer <see cref="DuplicateDetectionTool"/> — Argument-Validierung, Fehlerbehandlung,
/// <c>StructuredContent</c>-ist-Objekt-Regressionstest (siehe
/// <see cref="McpToolResults.Text{T}"/>-Doc-Kommentar) und End-zu-End-Wiring gegen eine
/// selbst gebaute In-Memory-Solution mit einem echten exact-Klon-Paar.
/// </summary>
[Trait("Category", "Component")]
public sealed class DuplicateDetectionToolTests
{
    private static string BuildMethod(string className, string methodName) =>
        TestHelper.BuildCalibratedMethod(className, methodName);

    private static McpInMemoryTestContext CreateContext(params (string FileName, string Content)[] files) =>
        new(RoslynTestSolutionFactory.CreateSolution(
            @"C:\ainetlinter-virtual\DuplicateDetectionToolTests.slnx",
            new ProjectSpec("TestProject", files, VirtualProjectDirectory: ".")));

    [Fact]
    public async Task ExecuteAsync_NoSolutionLoaded_ReturnsErrorWithSolutionNotLoadedCode()
    {
        var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(new McpCodeGraphServerOptionsFromParameters(null)));

        var result = await DuplicateDetectionTool.ExecuteAsync(
            state, new DuplicateDetectionInput(null, null, null, null, null), CancellationToken.None);

        Assert.True(result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("SOLUTION_NOT_LOADED", textContent.Text);
    }

    [Fact]
    public async Task ExecuteAsync_InvalidSimilarityThreshold_ReturnsRecoverableInvalidArgument()
    {
        using var context = CreateContext(("A.cs", BuildMethod("A", "One")));
        var state = context.CreateServer();

        var result = await DuplicateDetectionTool.ExecuteAsync(
            state, new DuplicateDetectionInput(null, "sideways", null, null, null), CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("INVALID_ARGUMENT", textContent.Text);
        Assert.Contains("exact", textContent.Text, StringComparison.Ordinal);
        Assert.Contains("near", textContent.Text, StringComparison.Ordinal);
        Assert.Contains("fuzzy", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_NegativeMinTokens_ReturnsRecoverableInvalidArgument()
    {
        using var context = CreateContext(("A.cs", BuildMethod("A", "One")));
        var state = context.CreateServer();

        var result = await DuplicateDetectionTool.ExecuteAsync(
            state, new DuplicateDetectionInput(MinTokens: 0, null, null, null, null), CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("INVALID_ARGUMENT", textContent.Text);
    }

    [Fact]
    public async Task ExecuteAsync_NegativeMaxResults_ReturnsRecoverableInvalidArgument()
    {
        using var context = CreateContext(("A.cs", BuildMethod("A", "One")));
        var state = context.CreateServer();

        var result = await DuplicateDetectionTool.ExecuteAsync(
            state, new DuplicateDetectionInput(null, null, null, null, MaxResults: 0), CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("INVALID_ARGUMENT", textContent.Text);
    }

    [Fact]
    public async Task ExecuteAsync_StructuredContent_IsJsonObjectNotArray()
    {
        using var context = CreateContext(("A.cs", BuildMethod("A", "One")), ("B.cs", BuildMethod("B", "Two")));
        var state = context.CreateServer();

        var result = await DuplicateDetectionTool.ExecuteAsync(
            state, new DuplicateDetectionInput(null, null, null, null, null), CancellationToken.None);

        Assert.NotNull(result.StructuredContent);
        Assert.Equal(JsonValueKind.Object, result.StructuredContent!.Value.ValueKind);
        Assert.Equal(JsonValueKind.Array, result.StructuredContent.Value.GetProperty("clusters").ValueKind);
        Assert.Equal(JsonValueKind.Object, result.StructuredContent.Value.GetProperty("summary").ValueKind);
    }

    [Fact]
    public async Task ExecuteAsync_ExactCloneFound_TextMentionsBucketAndBothMethods()
    {
        using var context = CreateContext(("A.cs", BuildMethod("A", "One")), ("B.cs", BuildMethod("B", "Two")));
        var state = context.CreateServer();

        var result = await DuplicateDetectionTool.ExecuteAsync(
            state, new DuplicateDetectionInput(null, null, null, null, null), CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("exact", textContent.Text, StringComparison.Ordinal);
        Assert.Contains("A.cs", textContent.Text, StringComparison.Ordinal);
        Assert.Contains("B.cs", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_EmptySolution_ReturnsNoDuplicatesMessageWithoutError()
    {
        using var context = CreateContext();
        var state = context.CreateServer();

        var result = await DuplicateDetectionTool.ExecuteAsync(
            state, new DuplicateDetectionInput(null, null, null, null, null), CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("Keine Duplikat-Cluster", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_NotTruncated_AppendsSufficiencyHint()
    {
        using var context = CreateContext(("A.cs", BuildMethod("A", "One")), ("B.cs", BuildMethod("B", "Two")));
        var state = context.CreateServer();

        var result = await DuplicateDetectionTool.ExecuteAsync(
            state, new DuplicateDetectionInput(null, null, null, null, null), CancellationToken.None);

        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("Diese Daten sind vollstaendig", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_MaxResultsExceeded_SetsTruncatedTrueAndOmitsSufficiencyHint()
    {
        // Zwei strukturell unabhaengige exakte Klon-Paare -> 2 Cluster, maxResults=1 kappt auf 1.
        const string secondFamilyA = """
            public static class C1
            {
                public static int G1(int yy)
                {
                    int uu = yy ^ 11; int vv = yy ^ 12; int ww = yy ^ 13; int xx2 = yy ^ 14; int zz = yy ^ 15;
                    int aa2 = uu | vv; int bb2 = ww | xx2; int cc2 = zz | aa2; int dd2 = bb2 | cc2; int ee2 = dd2 & uu;
                    return ee2;
                }
            }
            """;
        const string secondFamilyB = """
            public static class C2
            {
                public static int G2(int yy)
                {
                    int uu = yy ^ 11; int vv = yy ^ 12; int ww = yy ^ 13; int xx2 = yy ^ 14; int zz = yy ^ 15;
                    int aa2 = uu | vv; int bb2 = ww | xx2; int cc2 = zz | aa2; int dd2 = bb2 | cc2; int ee2 = dd2 & uu;
                    return ee2;
                }
            }
            """;
        using var context = CreateContext(
            ("A.cs", BuildMethod("A", "One")), ("B.cs", BuildMethod("B", "Two")),
            ("C1.cs", secondFamilyA), ("C2.cs", secondFamilyB));
        var state = context.CreateServer();

        var result = await DuplicateDetectionTool.ExecuteAsync(
            state, new DuplicateDetectionInput(null, null, null, null, MaxResults: 1), CancellationToken.None);

        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.DoesNotContain("Diese Daten sind vollstaendig", textContent.Text, StringComparison.Ordinal);
        Assert.Contains("maxResults erhoehen", textContent.Text, StringComparison.Ordinal);

        var summary = result.StructuredContent!.Value.GetProperty("summary");
        Assert.True(summary.GetProperty("truncated").GetBoolean());
        Assert.Equal(2, summary.GetProperty("totalClusters").GetInt32());
        Assert.Equal(1, summary.GetProperty("shownClusters").GetInt32());
    }

    [Fact]
    public async Task ExecuteAsync_ExactSimilarityThreshold_FiltersOutNonExactMatchesButKeepsExact()
    {
        using var context = CreateContext(("A.cs", BuildMethod("A", "One")), ("B.cs", BuildMethod("B", "Two")));
        var state = context.CreateServer();

        var result = await DuplicateDetectionTool.ExecuteAsync(
            state, new DuplicateDetectionInput(null, "exact", null, null, null), CancellationToken.None);

        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("exact", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_InvalidScopeType_ReturnsRecoverableInvalidArgument()
    {
        using var context = CreateContext(("A.cs", BuildMethod("A", "One")));
        var state = context.CreateServer();

        var result = await DuplicateDetectionTool.ExecuteAsync(
            state, new DuplicateDetectionInput(null, null, null, null, null, ScopeType: "invalid-scope"), CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("INVALID_ARGUMENT", textContent.Text);
        Assert.Contains("scopeType", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_ScopeTypeProduction_FiltersOutTestFiles()
    {
        using var context = CreateContext(
            ("A.cs", BuildMethod("A", "One")),
            ("BTests.cs", BuildMethod("B", "Two")));
        var state = context.CreateServer();

        var result = await DuplicateDetectionTool.ExecuteAsync(
            state, new DuplicateDetectionInput(null, null, null, null, null, ScopeType: "production"), CancellationToken.None);

        var summary = result.StructuredContent!.Value.GetProperty("summary");
        Assert.Equal(0, summary.GetProperty("totalClusters").GetInt32());
    }

    [Fact]
    public async Task ExecuteAsync_ScopeTypeTests_FiltersOutProductionFiles()
    {
        using var context = CreateContext(
            ("A.cs", BuildMethod("A", "One")),
            ("BTests.cs", BuildMethod("B", "Two")));
        var state = context.CreateServer();

        var result = await DuplicateDetectionTool.ExecuteAsync(
            state, new DuplicateDetectionInput(null, null, null, null, null, ScopeType: "tests"), CancellationToken.None);

        var summary = result.StructuredContent!.Value.GetProperty("summary");
        Assert.Equal(0, summary.GetProperty("totalClusters").GetInt32());
    }

    [Fact]
    public async Task RenderText_WhenMoreThanTwentyClusters_IncludesTopClusterSummary()
    {
        static string BuildUniqueMethod(string className, string methodName, int seed) => $$"""
            public static class {{className}}
            {
                public static int {{methodName}}(int x)
                {
                    int a{{seed}} = x + {{seed}}; int b{{seed}} = x + {{seed + 1}}; int c{{seed}} = x + {{seed + 2}}; int d{{seed}} = x + {{seed + 3}}; int e{{seed}} = x + {{seed + 4}};
                    int f{{seed}} = a{{seed}} + b{{seed}}; int g{{seed}} = c{{seed}} + d{{seed}}; int h{{seed}} = e{{seed}} + f{{seed}}; int i{{seed}} = g{{seed}} + h{{seed}}; int j{{seed}} = i{{seed}} - a{{seed}};
                    int k{{seed}} = j{{seed}} - b{{seed}}; int l{{seed}} = k{{seed}} - c{{seed}}; int m{{seed}} = l{{seed}} - d{{seed}}; int n{{seed}} = m{{seed}} - e{{seed}}; int o{{seed}} = n{{seed}} * 2;
                    int p{{seed}} = o{{seed}} / 2; int q{{seed}} = p{{seed}} + 1; int r{{seed}} = q{{seed}} + 2; int s{{seed}} = r{{seed}} + 3; int t{{seed}} = s{{seed}} + 4;
                    return t{{seed}};
                }
            }
            """;

        var files = new (string FileName, string Content)[42];
        for (int i = 0; i < 21; i++)
        {
            files[i * 2] = ($"A{i}.cs", BuildUniqueMethod($"ClassA{i}", $"Method{i}", i));
            files[i * 2 + 1] = ($"B{i}.cs", BuildUniqueMethod($"ClassB{i}", $"Method{i}", i));
        }
        using var context = CreateContext(files);
        var state = context.CreateServer();

        var result = await DuplicateDetectionTool.ExecuteAsync(
            state, new DuplicateDetectionInput(null, null, null, null, MaxResults: 50), CancellationToken.None);

        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("### Top-Cluster Uebersicht:", textContent.Text, StringComparison.Ordinal);
    }
}
