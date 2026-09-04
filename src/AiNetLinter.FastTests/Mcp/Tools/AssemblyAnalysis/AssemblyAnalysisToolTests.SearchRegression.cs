#nullable enable

using System;
using System.Linq;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Assemblies.Analysis;
using AiNetLinter.Mcp.Tools.AssemblyAnalysis;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Tools.AssemblyAnalysis;

public sealed partial class AssemblyAnalysisToolTests
{
    [Fact]
    public void AssemblySearchPaging_AdvancesWithinVisibleFilesAndStopsAtScopeEnd()
    {
        var matches = new[]
        {
            CreateSearchMatch("src/A.cs", 1),
            CreateSearchMatch("src/A.cs", 2),
            CreateSearchMatch("src/B.cs", 1),
        };

        var firstPage = AssemblySearchTool.SelectMatches(
            matches,
            matchedFileCount: 2,
            new AssemblySearchArguments("needle", false, "text", 1, 2, 0, 0, null, null));
        var secondPage = AssemblySearchTool.SelectMatches(
            matches,
            matchedFileCount: 2,
            new AssemblySearchArguments("needle", false, "text", 1, 2, 0, 0, null, "1"));
        var terminalPage = AssemblySearchTool.SelectMatches(
            matches,
            matchedFileCount: 2,
            new AssemblySearchArguments("needle", false, "text", 1, 1, 0, 0, null, "2"));

        Assert.True(firstPage.HasMoreVisibleMatches);
        Assert.Equal(1, firstPage.NextOffset);
        Assert.True(secondPage.HasMoreVisibleMatches);
        Assert.Equal(2, secondPage.NextOffset);
        Assert.True(terminalPage.MaxFilesTruncated);
        Assert.False(terminalPage.HasMoreVisibleMatches);
        Assert.Equal(2, terminalPage.NextOffset);
    }

    [Fact]
    public void FinalWireTrim_AssemblySearchKeepsUsableEnvelopeAndPaging()
    {
        var result = McpToolResults.Text(
            "assembly search",
            new
            {
                assemblySearch = new
                {
                    searchKind = "text",
                    query = "needle",
                    root = ".",
                    scope = "assembly-source-root",
                    results = Enumerable.Range(0, 80)
                        .Select(index => new
                        {
                            id = $"asm-search:{index:D3}",
                            filePath = $"src/very-long-file-name-{index:D3}.cs",
                            line = index + 1,
                            matchRanges = new[] { new { column = 1, length = 6 } },
                            lineText = new string('x', 500),
                            contextBefore = new[] { new string('b', 300) },
                            contextAfter = new[] { new string('a', 300) },
                        })
                        .ToArray(),
                    totalCount = 80,
                    returnedCount = 80,
                    isTruncated = false,
                    completeness = "complete",
                    truncatedBy = Array.Empty<string>(),
                    continuationToken = (string?)null,
                    matchedFileCount = 80,
                    returnedFileCount = 80,
                    detailHint = (string?)null,
                },
            });

        var projected = AssemblyAnalysisResponse.ApplyWireBudget(
            result,
            AssemblyAnalysisResponseLimits.MinimumResponseBytes,
            0);

        Assert.NotEqual(true, projected.IsError);
        var search = projected.StructuredContent!.Value.GetProperty("assemblySearch");
        var returned = search.GetProperty("results").GetArrayLength();
        Assert.True(returned > 0);
        Assert.True(returned < 80);
        Assert.Equal(80, search.GetProperty("totalCount").GetInt32());
        Assert.Equal(returned, search.GetProperty("returnedCount").GetInt32());
        Assert.Equal(returned, search.GetProperty("returnedFileCount").GetInt32());
        Assert.True(search.GetProperty("isTruncated").GetBoolean());
        Assert.Contains("responseBudget", search.GetProperty("truncatedBy").EnumerateArray().Select(item => item.GetString()));
        Assert.Equal(returned.ToString(), search.GetProperty("continuationToken").GetString());
        Assert.True(search.GetProperty("completeness").GetString() is "truncated" or "partial");
        Assert.True(projected.StructuredContent!.Value.GetProperty("wireBudget").GetProperty("totalBytes").GetInt32()
            <= AssemblyAnalysisResponseLimits.MinimumResponseBytes);
    }

    private static AssemblySearchMatch CreateSearchMatch(string filePath, int line) =>
        new(
            $"asm-search:{filePath}:{line}",
            filePath,
            line,
            Array.Empty<AssemblySearchMatchRange>(),
            "needle",
            Array.Empty<string>(),
            Array.Empty<string>());
}
