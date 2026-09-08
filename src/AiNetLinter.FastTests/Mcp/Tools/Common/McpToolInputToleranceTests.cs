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
    public void NormalizeNamePatterns_WithCanonicalPattern_ReturnsNormalizedPattern()
    {
        var pattern = FindSymbolTool.NormalizeNamePatterns(new FindSymbolPatternOptions(Pattern: "Greeter"));
        Assert.Equal(["Greeter"], pattern);
    }

    [Fact]
    public void NormalizeNamePatterns_WithBackticksAndMethodParentheses_CleansPattern()
    {
        var cleaned = FindSymbolTool.NormalizeNamePatterns(new FindSymbolPatternOptions(Pattern: "`Greeter()`"));
        Assert.Equal(["Greeter"], cleaned);
    }

    [Fact]
    public async Task FindSymbol_ExecuteAsync_WithCanonicalPattern_FindsSymbol()
    {
        using var fixture = new McpInMemoryTestContext();
        var request = new FindSymbolRequest(
            fixture.CreateServer(),
            NamePatterns: null,
            Kind: "class",
            MaxResults: 50,
            CancellationToken: CancellationToken.None,
            Pattern: "Greeter");

        var result = await FindSymbolTool.ExecuteAsync(request);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("Greeter", textContent.Text);
    }

    [Fact]
    public async Task FindSymbol_ExecuteAsync_WithCanonicalPatternAndBackticks_FindsSymbol()
    {
        using var fixture = new McpInMemoryTestContext();
        var request = new FindSymbolRequest(
            fixture.CreateServer(),
            NamePatterns: null,
            Kind: "class",
            MaxResults: 50,
            CancellationToken: CancellationToken.None,
            Pattern: "`Greeter`");

        var result = await FindSymbolTool.ExecuteAsync(request);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("Greeter", textContent.Text);
    }

    [Fact]
    public void GetSymbolBodyRequest_UsesCanonicalSymbolIdentifiersArray()
    {
        var req1 = new GetSymbolBodyRequest(SymbolIdentifiers: ["`MyMethod()`"]);
        Assert.Equal(["`MyMethod()`"], req1.SymbolIdentifiers!);

        var req2 = new GetSymbolBodyRequest(SymbolIdentifiers: ["\"MyMethod\""]);
        Assert.Equal(["\"MyMethod\""], req2.SymbolIdentifiers!);
    }

    [Fact]
    public void ToolRegistrations_ExposeCanonicalParameterNamesOnly()
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

        Assert.Contains("\"pattern\"", toolsByName["find_symbol"]);
        Assert.Contains("\"symbolIdentifier\"", toolsByName["find_references"]);
        Assert.Contains("\"symbolIdentifier\"", toolsByName["get_call_tree"]);
        Assert.Contains("\"symbolIdentifier\"", toolsByName["get_class_structure"]);
        Assert.Contains("\"root\"", toolsByName["get_file_tree"]);
        Assert.Contains("\"fileFilter\"", toolsByName["get_file_tree"]);
        Assert.Contains("\"filePaths\"", toolsByName["get_file_skeleton"]);
        Assert.Contains("\"scopeFilter\"", toolsByName["get_violations"]);
        Assert.Contains("\"ruleId\"", toolsByName["get_violations"]);
        Assert.Contains("\"scopeFilter\"", toolsByName["safeguard"]);
        Assert.Contains("\"symbolIdentifiers\"", toolsByName["get_symbol_body"]);
        Assert.Contains("\"scopeDir\"", toolsByName["find_duplicates"]);
        Assert.Contains("\"helperSymbol\"", toolsByName["find_duplicates"]);

        Assert.DoesNotContain("\"query\"", toolsByName["find_symbol"]);
        Assert.DoesNotContain("\"identifier\"", toolsByName["find_references"]);
        Assert.DoesNotContain("\"file\"", toolsByName["get_file_skeleton"]);
    }
}
