#nullable enable

using System.IO;
using System.Threading;
using AiNetLinter.Mcp.Tools.AssemblyAnalysis;
using AiNetLinter.TestKit;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Tools.AssemblyAnalysis;

[Trait("Category", "Unit")]
public sealed class AssemblySearchDeclarationFilterTests
{
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
