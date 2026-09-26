#nullable enable

using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp.Assemblies.Analysis;
using AiNetLinter.Mcp.Assemblies.Analysis.References;
using AiNetLinter.Mcp.Tools;
using AiNetLinter.Mcp.Tools.AssemblyAnalysis;
using AiNetLinter.Mcp.Tools.CallTree;
using AiNetLinter.TestKit;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Tools.AssemblyAnalysis;

[Trait("Category", "Unit")]
public sealed class AssemblySearchDeclarationFilterTests
{
    [Fact]
    public void Scan_NestedFile_UsesCompletePathRelativeToRoot()
    {
        using var temp = TestTempDirectory.Create("assembly-search-relative-path-");
        var nestedDirectory = Path.Combine(temp.DirectoryPath, "Api", "Contracts");
        Directory.CreateDirectory(nestedDirectory);
        File.WriteAllText(Path.Combine(nestedDirectory, "Order.cs"), "public sealed class OrderContract { }");

        var payload = AssemblySearchTool.Scan(
            temp.DirectoryPath,
            new AssemblySearchArguments("OrderContract", false, "text", 50, 10, 0, 0, null, null),
            CancellationToken.None);

        var match = Assert.Single(payload.Results);
        Assert.Equal("Api/Contracts/Order.cs", match.FilePath);
        Assert.True(File.Exists(Path.Combine(temp.DirectoryPath, match.FilePath)));
    }

    [Fact]
    public void DeclarationOnly_ExcludesCommentsDocStringsAndCalls()
    {
        using var temp = TestTempDirectory.Create("decl-filter-");
        var code = """
            namespace Sample;

            /// <summary>
            /// Calculates TotalAmount for the order.
            /// </summary>
            public class OrderService
            {
                // Note: TotalAmount is crucial here
                public decimal TotalAmount { get; set; }

                public void Print()
                {
                    var msg = "TotalAmount is pending";
                    var val = TotalAmount;
                }
            }
            """;
        File.WriteAllText(temp.GetPath("OrderService.cs"), code);

        var argsWithoutFilter = new AssemblySearchArguments(
            Pattern: "TotalAmount",
            IsRegex: false,
            SearchKind: "text",
            MaxResults: 50,
            MaxFiles: 10,
            ContextLines: 0,
            MaxResponseBytes: 0,
            FileFilter: null,
            Cursor: null,
            DeclarationOnly: false,
            Kind: null);

        var allPayload = AssemblySearchTool.Scan(temp.DirectoryPath, argsWithoutFilter, CancellationToken.None);
        Assert.True(allPayload.Results.Count >= 4, $"Expected at least 4 matches, got {allPayload.Results.Count}");

        var argsWithFilter = argsWithoutFilter with { DeclarationOnly = true };
        var declPayload = AssemblySearchTool.Scan(temp.DirectoryPath, argsWithFilter, CancellationToken.None);

        var match = Assert.Single(declPayload.Results);
        Assert.NotEmpty(match.MatchRanges);
        Assert.Contains("public decimal TotalAmount { get; set; }", match.LineText);
    }

    [Fact]
    public void KindFilter_DistinguishesTypeMethodAndProperty()
    {
        using var temp = TestTempDirectory.Create("kind-filter-");
        var code = """
            namespace Sample;

            public class WidgetHandler
            {
                public string WidgetHandlerValue { get; set; } = "";

                public void WidgetHandlerAction()
                {
                    var x = WidgetHandlerValue;
                }
            }
            """;
        File.WriteAllText(temp.GetPath("WidgetHandler.cs"), code);

        // 1. Kind: "type"
        var typeArgs = new AssemblySearchArguments(
            Pattern: "WidgetHandler",
            IsRegex: false,
            SearchKind: "text",
            MaxResults: 50,
            MaxFiles: 10,
            ContextLines: 0,
            MaxResponseBytes: 0,
            FileFilter: null,
            Cursor: null,
            DeclarationOnly: true,
            Kind: "type");
        var typePayload = AssemblySearchTool.Scan(temp.DirectoryPath, typeArgs, CancellationToken.None);
        var typeMatch = Assert.Single(typePayload.Results);
        Assert.Contains("public class WidgetHandler", typeMatch.LineText);

        // 2. Kind: "method"
        var methodArgs = typeArgs with { Pattern = "WidgetHandlerAction", Kind = "method" };
        var methodPayload = AssemblySearchTool.Scan(temp.DirectoryPath, methodArgs, CancellationToken.None);
        var methodMatch = Assert.Single(methodPayload.Results);
        Assert.Contains("public void WidgetHandlerAction()", methodMatch.LineText);

        // 3. Kind: "property"
        var propArgs = typeArgs with { Pattern = "WidgetHandlerValue", Kind = "property" };
        var propPayload = AssemblySearchTool.Scan(temp.DirectoryPath, propArgs, CancellationToken.None);
        var propMatch = Assert.Single(propPayload.Results);
        Assert.Contains("public string WidgetHandlerValue { get; set; }", propMatch.LineText);
    }

    [Theory]
    [InlineData("SearchProbe", "type", "SearchProbe")]
    [InlineData("Run", "method", "Run")]
    [InlineData("Title", "property", "Title")]
    public async Task ExecuteAsync_DeclarationMatch_PublishesReusableAssemblyHandoff(
        string pattern,
        string kind,
        string expectedBodyContent)
    {
        using var temp = TestTempDirectory.Create("assembly-search-handoff-");
        var assemblyPath = AssemblyTestHelper.EmitAssembly(temp, "SearchHandoffProbe", """
            namespace Probe;
            public sealed class SearchProbe
            {
                public string Title { get; set; } = "";
                public void Run() { }
            }
            """);
        await using var registry = new AssemblyAnalysisRegistry();
        var leaseResult = await registry.LeaseAsync(assemblyPath);
        using var lease = Assert.IsType<AssemblyAnalysisLease>(leaseResult.Lease);

        var result = await AssemblySearchTool.ExecuteAsync(
            lease,
            new AssemblySearchArguments(pattern, false, "text", 10, 0, 0, 0, null, null, null, true, kind),
            CancellationToken.None);
        var text = AssemblyAnalysisTestSupport.TextOf(result);
        var handoffId = Regex.Match(text, @"handoffId: `(?<id>h:[^`]+)`").Groups["id"].Value;

        Assert.NotEmpty(handoffId);
        var body = await GetSymbolBodyTool.ExecuteAsync(lease, [handoffId], 80, CancellationToken.None);
        Assert.NotEqual(true, body.IsError);
        Assert.Contains(expectedBodyContent, AssemblyAnalysisTestSupport.TextOf(body), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_AssemblySearchHandle_FeedsAssemblyCallTree()
    {
        using var temp = TestTempDirectory.Create("assembly-call-tree-handoff-");
        var assemblyPath = AssemblyTestHelper.EmitAssembly(temp, "CallTreeHandoffProbe", """
            namespace Probe;
            public sealed class SearchProbe { public void Run() { } }
            """);
        await using var registry = new AssemblyAnalysisRegistry();
        var leaseResult = await registry.LeaseAsync(assemblyPath);
        using var lease = Assert.IsType<AssemblyAnalysisLease>(leaseResult.Lease);

        var search = await AssemblySearchTool.ExecuteAsync(
            lease,
            new AssemblySearchArguments("Run", false, "text", 10, 0, 0, 0, null, null, null, true, "method"),
            CancellationToken.None);
        var handle = Regex.Match(AssemblyAnalysisTestSupport.TextOf(search), @"handoffId: `(?<id>h:[^`]+)`").Groups["id"].Value;
        Assert.NotEmpty(handle);

        var callTree = await AssemblyGetCallTreeTool.ExecuteAsync(
            lease,
            new AssemblyGetCallTreeRequest(
                new GetCallTreeInput(handle, 1, "ascii", 10, "incoming"),
                IncludeReferences: false),
            CancellationToken.None);

        Assert.NotEqual(true, callTree.IsError);
        Assert.Contains("SearchProbe.Run", AssemblyAnalysisTestSupport.TextOf(callTree), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_RejectsOpaquePatternAndResolvesQualifiedTypeFromInspection()
    {
        using var temp = TestTempDirectory.Create("assembly-search-qualified-type-");
        var assemblyPath = AssemblyTestHelper.EmitAssembly(temp, "SearchQualifiedTypeProbe", """
            namespace Probe { public sealed class SearchProbe { } }
            namespace Other { public sealed class SearchProbe { } }
            """);
        await using var registry = new AssemblyAnalysisRegistry();
        var leaseResult = await registry.LeaseAsync(assemblyPath);
        using var lease = Assert.IsType<AssemblyAnalysisLease>(leaseResult.Lease);

        var inspection = await InspectAssemblyTool.ExecuteAsync(
            lease,
            new InspectAssemblyArguments(assemblyPath, null, null, null, true, 10));
        var inspectionText = AssemblyAnalysisTestSupport.TextOf(inspection);
        var qualifiedTypeName = Regex.Match(inspectionText, @"- `(?<name>Probe\.SearchProbe)`").Groups["name"].Value;
        var handoffId = Regex.Match(inspectionText, @"handoffId: `(?<id>h:[^`]+)`").Groups["id"].Value;

        var unsupported = await AssemblySearchTool.ExecuteAsync(
            lease,
            new AssemblySearchArguments(handoffId, false, "text", 10, DeclarationOnly: true, Kind: "type"),
            CancellationToken.None);
        var search = await AssemblySearchTool.ExecuteAsync(
            lease,
            new AssemblySearchArguments(qualifiedTypeName, false, "text", 10, DeclarationOnly: true, Kind: "type"),
            CancellationToken.None);

        Assert.True(unsupported.IsError);
        Assert.Contains("UNSUPPORTED_IDENTIFIER", AssemblyAnalysisTestSupport.TextOf(unsupported), StringComparison.Ordinal);
        Assert.Contains("get_symbol_body", AssemblyAnalysisTestSupport.TextOf(unsupported), StringComparison.Ordinal);
        Assert.Contains("handoffId: `h:", AssemblyAnalysisTestSupport.TextOf(search), StringComparison.Ordinal);
        Assert.Contains("public sealed class SearchProbe", AssemblyAnalysisTestSupport.TextOf(search), StringComparison.Ordinal);
        Assert.DoesNotContain("Other", AssemblyAnalysisTestSupport.TextOf(search), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_TextMatch_DoesNotPromiseHandoff()
    {
        using var temp = TestTempDirectory.Create("assembly-search-raw-text-");
        var assemblyPath = AssemblyTestHelper.EmitAssembly(
            temp,
            "SearchRawTextProbe",
            "namespace Probe; public sealed class SearchProbe { public void Run() { var marker = \"needle\"; } }");
        await using var registry = new AssemblyAnalysisRegistry();
        var leaseResult = await registry.LeaseAsync(assemblyPath);
        using var lease = Assert.IsType<AssemblyAnalysisLease>(leaseResult.Lease);

        var result = await AssemblySearchTool.ExecuteAsync(
            lease,
            new AssemblySearchArguments("needle", false, "text", 10),
            CancellationToken.None);

        Assert.DoesNotContain("handoffId:", AssemblyAnalysisTestSupport.TextOf(result), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("method", true)]
    [InlineData("type", true)]
    [InlineData("property", true)]
    [InlineData("METHOD", true)]
    [InlineData("unknown", false)]
    [InlineData("variable", false)]
    public void ValidateArguments_ValidatesKindParameter(string kind, bool isValid)
    {
        var args = new AssemblySearchArguments(
            Pattern: "Test",
            IsRegex: false,
            SearchKind: "text",
            MaxResults: 50,
            MaxFiles: 10,
            ContextLines: 0,
            MaxResponseBytes: 0,
            FileFilter: null,
            Cursor: null,
            DeclarationOnly: true,
            Kind: kind);

        var validation = AssemblySearchTool.ValidateArguments(args);
        if (isValid)
        {
            Assert.Null(validation);
        }
        else
        {
            Assert.NotNull(validation);
            var textBlock = Assert.IsType<ModelContextProtocol.Protocol.TextContentBlock>(Assert.Single(validation!.Content));
            Assert.Contains("INVALID_ARGUMENT", textBlock.Text);
            Assert.Contains("Ungueltiger kind-Wert", textBlock.Text);
        }
    }
}
