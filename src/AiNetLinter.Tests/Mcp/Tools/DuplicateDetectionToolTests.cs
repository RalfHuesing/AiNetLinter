#nullable enable

using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Baseline;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Tools.DuplicateDetection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.Tests.Mcp.Tools;

/// <summary>
/// Tests fuer <see cref="DuplicateDetectionTool"/> — Argument-Validierung, Fehlerbehandlung,
/// <c>StructuredContent</c>-ist-Objekt-Regressionstest (siehe
/// <see cref="McpToolResults.Text{T}"/>-Doc-Kommentar) und End-zu-End-Wiring gegen eine
/// selbst gebaute In-Memory-Solution mit einem echten exact-Klon-Paar.
/// </summary>
[Trait("Category", "Unit")]
public sealed class DuplicateDetectionToolTests : IDisposable
{
    private readonly string _tempDir;

    public DuplicateDetectionToolTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "ainetlinter-duptool-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { /* best-effort cleanup */ }
    }

    private static readonly string[] BaseStatements =
    [
        "int a = x + 1;", "int b = x + 2;", "int c = x + 3;", "int d = x + 4;", "int e = x + 5;",
        "int f = a + b;", "int g = c + d;", "int h = e + f;", "int i = g + h;", "int j = i - a;",
        "int k = j - b;", "int l = k - c;", "int m = l - d;", "int n = m - e;", "int o = n * 2;",
        "int p = o / 2;", "int q = p + 1;", "int r = q + 2;", "int s = r + 3;", "int t = s + 4;",
    ];

    private static string BuildMethod(string className, string methodName) => $$"""
        public static class {{className}}
        {
            public static int {{methodName}}(int x)
            {
                {{string.Join("\n            ", BaseStatements)}}
                return t;
            }
        }
        """;

    private McpCodeGraphServer BuildServer(params (string FileName, string Content)[] files)
    {
        var workspace = new AdhocWorkspace();
        var projectId = ProjectId.CreateNewId();
        var mscorlib = MetadataReference.CreateFromFile(typeof(object).Assembly.Location);
        var runtimeAsm = MetadataReference.CreateFromFile(typeof(System.Runtime.GCLatencyMode).Assembly.Location);

        var projectInfo = ProjectInfo.Create(
            projectId, VersionStamp.Create(), "TestProject", "TestProject", LanguageNames.CSharp)
            .WithMetadataReferences(new[] { mscorlib, runtimeAsm })
            .WithCompilationOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

        var solutionInfo = SolutionInfo.Create(SolutionId.CreateNewId(), VersionStamp.Create(), filePath: Path.Combine(_tempDir, "Test.slnx"));
        var solution = workspace.AddSolution(solutionInfo).AddProject(projectInfo);
        foreach (var file in files)
        {
            var fullPath = Path.Combine(_tempDir, file.FileName);
            File.WriteAllText(fullPath, file.Content);
            var documentId = DocumentId.CreateNewId(projectId);
            solution = solution.AddDocument(documentId, file.FileName, file.Content, filePath: fullPath);
        }

        var catalog = new SourceFileCatalog(solution, hasLoadingErrors: false);
        return new McpCodeGraphServer(McpCodeGraphServerOptions.From(new McpCodeGraphServerOptionsFromParameters(catalog)));
    }

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
        var state = BuildServer(("A.cs", BuildMethod("A", "One")));

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
        var state = BuildServer(("A.cs", BuildMethod("A", "One")));

        var result = await DuplicateDetectionTool.ExecuteAsync(
            state, new DuplicateDetectionInput(MinTokens: 0, null, null, null, null), CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("INVALID_ARGUMENT", textContent.Text);
    }

    [Fact]
    public async Task ExecuteAsync_NegativeMaxResults_ReturnsRecoverableInvalidArgument()
    {
        var state = BuildServer(("A.cs", BuildMethod("A", "One")));

        var result = await DuplicateDetectionTool.ExecuteAsync(
            state, new DuplicateDetectionInput(null, null, null, null, MaxResults: 0), CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("INVALID_ARGUMENT", textContent.Text);
    }

    [Fact]
    public async Task ExecuteAsync_StructuredContent_IsJsonObjectNotArray()
    {
        var state = BuildServer(("A.cs", BuildMethod("A", "One")), ("B.cs", BuildMethod("B", "Two")));

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
        var state = BuildServer(("A.cs", BuildMethod("A", "One")), ("B.cs", BuildMethod("B", "Two")));

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
        var state = BuildServer();

        var result = await DuplicateDetectionTool.ExecuteAsync(
            state, new DuplicateDetectionInput(null, null, null, null, null), CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("Keine Duplikat-Cluster", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_NotTruncated_AppendsSufficiencyHint()
    {
        var state = BuildServer(("A.cs", BuildMethod("A", "One")), ("B.cs", BuildMethod("B", "Two")));

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
        var state = BuildServer(
            ("A.cs", BuildMethod("A", "One")), ("B.cs", BuildMethod("B", "Two")),
            ("C1.cs", secondFamilyA), ("C2.cs", secondFamilyB));

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
        var state = BuildServer(("A.cs", BuildMethod("A", "One")), ("B.cs", BuildMethod("B", "Two")));

        var result = await DuplicateDetectionTool.ExecuteAsync(
            state, new DuplicateDetectionInput(null, "exact", null, null, null), CancellationToken.None);

        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("exact", textContent.Text, StringComparison.Ordinal);
    }
}
