#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AiNetLinter.Mcp.Tools.SymbolGraph;
using AiNetLinter.Mcp.Validation;
using Xunit;

namespace AiNetLinter.IntegrationTests.Mcp.Tools;

public sealed partial class McpServerArgumentValidationE2ETests
{
    [Fact]
    public async Task FindSymbol_EmptyPatternAndInvalidBatchShapesReturnPreciseFieldPaths()
    {
        var emptyPattern = await _fixture.Client.CallToolAsync(
            "find_symbol", new Dictionary<string, object?> { ["pattern"] = " " });
        Assert.Equal("$.pattern", emptyPattern.StructuredContent!.Value.GetProperty("fieldPath").GetString());

        var emptyBatch = await _fixture.Client.CallToolAsync(
            "find_symbol", new Dictionary<string, object?> { ["namePatterns"] = Array.Empty<string>() });
        Assert.Equal("$.namePatterns", emptyBatch.StructuredContent!.Value.GetProperty("fieldPath").GetString());

        var oversizedBatch = await _fixture.Client.CallToolAsync(
            "find_symbol",
            new Dictionary<string, object?>
            {
                ["namePatterns"] = Enumerable.Range(0, 11).Select(index => $"Greeter{index}").ToArray(),
            });
        Assert.Equal("$.namePatterns", oversizedBatch.StructuredContent!.Value.GetProperty("fieldPath").GetString());
    }

    [Fact]
    public async Task FindSymbolKindContract_UsesCanonicalValuesForToolDescriptionAndValidator()
    {
        var tool = Assert.Single((await _fixture.Client.ListToolsAsync())
            .Where(candidate => candidate.ProtocolTool.Name == "find_symbol"));

        foreach (var kind in McpEnumValues.FindSymbolKinds)
        {
            Assert.Contains(kind, tool.ProtocolTool.Description, StringComparison.Ordinal);
            Assert.Null(FindSymbolTool.ValidateKind(kind));
        }

        Assert.NotNull(FindSymbolTool.ValidateKind("trait"));
    }
}
