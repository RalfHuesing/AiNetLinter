#nullable enable

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.FastTests.Fixtures;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Composition;
using AiNetLinter.Mcp.Tools;
using AiNetLinter.Mcp.Tools.Common;
using AiNetLinter.Mcp.Tools.DuplicateDetection;
using AiNetLinter.Mcp.Tools.FileStructure;
using AiNetLinter.Mcp.Tools.SymbolGraph;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Tools.Common;

[Trait("Category", "Unit")]
public sealed class McpToolInputToleranceTests
{
    [Fact]
    public void NormalizeNamePatterns_WithQueryOrNameAlias_ReturnsNormalizedPattern()
    {
        // Query-Alias
        var fromQuery = FindSymbolTool.NormalizeNamePatterns(new FindSymbolPatternOptions(Query: "Greeter"));
        Assert.Equal(["Greeter"], fromQuery);

        // Name-Alias
        var fromName = FindSymbolTool.NormalizeNamePatterns(new FindSymbolPatternOptions(Name: "Greeter"));
        Assert.Equal(["Greeter"], fromName);
    }

    [Fact]
    public void NormalizeNamePatterns_WithBackticksAndMethodParentheses_CleansPattern()
    {
        var cleaned = FindSymbolTool.NormalizeNamePatterns(new FindSymbolPatternOptions(Query: "`Greeter()`"));
        Assert.Equal(["Greeter"], cleaned);
    }

    [Fact]
    public async Task FindSymbol_ExecuteAsync_WithQueryAlias_FindsSymbol()
    {
        using var fixture = new McpInMemoryTestContext();
        var request = new FindSymbolRequest(
            fixture.CreateServer(),
            NamePatterns: null,
            Kind: "class",
            MaxResults: 50,
            CancellationToken: CancellationToken.None,
            Query: "Greeter");

        var result = await FindSymbolTool.ExecuteAsync(request);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("Greeter", textContent.Text);
    }

    [Fact]
    public async Task FindSymbol_ExecuteAsync_WithNameAliasAndBackticks_FindsSymbol()
    {
        using var fixture = new McpInMemoryTestContext();
        var request = new FindSymbolRequest(
            fixture.CreateServer(),
            NamePatterns: null,
            Kind: "class",
            MaxResults: 50,
            CancellationToken: CancellationToken.None,
            Name: "`Greeter`");

        var result = await FindSymbolTool.ExecuteAsync(request);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("Greeter", textContent.Text);
    }

    [Fact]
    public void GetSymbolBodyRequest_WithIdentifierOrNameAlias_ResolvesEffectiveSymbolIdentifier()
    {
        var req1 = new GetSymbolBodyRequest(Identifier: "`MyMethod()`");
        Assert.Equal("MyMethod", req1.EffectiveSymbolIdentifier);

        var req2 = new GetSymbolBodyRequest(Name: "\"MyMethod\"");
        Assert.Equal("MyMethod", req2.EffectiveSymbolIdentifier);
    }

    [Fact]
    public void ToolRegistrations_IncludeLlmParameterAliasesInInputSchema()
    {
        var registry = ProjectRegistryFixture.CreateInspectionRegistry();
        var options = McpServerOptionsFactory.Create(
            McpServerToolCollectionFactory.Build(
                registry,
                AnalysisToolCall.CreateTargetRoute(
                    ProjectAnalysisDispatcher.CreateRoute(registry),
                    AssemblyAnalysisDispatcher.CreateRoute(null))),
            McpServerResourceCollectionFactory.Build(registry));

        var toolsByName = options.ToolCollection!.ToDictionary(t => t.ProtocolTool.Name, t => t.ProtocolTool.InputSchema.ToString());

        // find_symbol
        Assert.Contains("\"query\"", toolsByName["find_symbol"]);
        Assert.Contains("\"name\"", toolsByName["find_symbol"]);

        // find_references & get_call_tree
        Assert.Contains("\"identifier\"", toolsByName["find_references"]);
        Assert.Contains("\"name\"", toolsByName["find_references"]);
        Assert.Contains("\"identifier\"", toolsByName["get_call_tree"]);
        Assert.Contains("\"name\"", toolsByName["get_call_tree"]);

        // get_class_structure
        Assert.Contains("\"className\"", toolsByName["get_class_structure"]);
        Assert.Contains("\"identifier\"", toolsByName["get_class_structure"]);

        // get_file_tree
        Assert.Contains("\"path\"", toolsByName["get_file_tree"]);
        Assert.Contains("\"directory\"", toolsByName["get_file_tree"]);
        Assert.Contains("\"filter\"", toolsByName["get_file_tree"]);
        Assert.Contains("\"pattern\"", toolsByName["get_file_tree"]);

        // get_file_skeleton
        Assert.Contains("\"path\"", toolsByName["get_file_skeleton"]);
        Assert.Contains("\"file\"", toolsByName["get_file_skeleton"]);

        // get_violations
        Assert.Contains("\"scope\"", toolsByName["get_violations"]);
        Assert.Contains("\"path\"", toolsByName["get_violations"]);
        Assert.Contains("\"rule\"", toolsByName["get_violations"]);

        // safeguard
        Assert.Contains("\"scope\"", toolsByName["safeguard"]);
        Assert.Contains("\"path\"", toolsByName["safeguard"]);

        // get_symbol_body
        Assert.Contains("\"identifier\"", toolsByName["get_symbol_body"]);
        Assert.Contains("\"name\"", toolsByName["get_symbol_body"]);

        // find_duplicates
        Assert.Contains("\"scope\"", toolsByName["find_duplicates"]);
        Assert.Contains("\"helper\"", toolsByName["find_duplicates"]);
    }
}
