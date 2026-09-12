#nullable enable

using System;
using System.Collections.Generic;
using AiNetLinter.Mcp;
using ModelContextProtocol.Protocol;

namespace AiNetLinter.FastTests.Mcp.Results;

[Trait("Category", "Unit")]
public sealed class McpContentOnlyContractTests
{
    [Fact]
    public void CommonToolResultPaths_UseExactlyOneNonEmptyTextBlock()
    {
        var results = new[]
        {
            new NamedToolResult("success", McpToolResults.Text("Symbol gefunden.")),
            new NamedToolResult("empty", McpToolResults.Text("EMPTY Keine Treffer.")),
            new NamedToolResult("loading", McpToolResults.Loading()),
            new NamedToolResult("error", McpToolResults.InvalidArgument("maxResults muss positiv sein.")),
            new NamedToolResult(
                "response-budget",
                McpToolResults.Recoverable(
                    "RESPONSE_BUDGET_TOO_SMALL",
                    "Die kleinste vollstaendige Evidenzeinheit passt nicht.")),
        };

        AssertContentOnly(results);
    }

    private static void AssertContentOnly(IEnumerable<NamedToolResult> results)
    {
        var violations = new List<string>();
        foreach (var namedResult in results)
        {
            var content = namedResult.Result.Content;
            if (content.Count != 1)
            {
                violations.Add($"{namedResult.Name}: expected exactly one content block, got {content.Count}.");
            }
            else if (content[0] is not TextContentBlock { Text: { } text } || string.IsNullOrWhiteSpace(text))
            {
                violations.Add($"{namedResult.Name}: expected one non-empty TextContentBlock.");
            }

        }

        Assert.True(
            violations.Count == 0,
            "Content-only contract violations:" + Environment.NewLine + string.Join(Environment.NewLine, violations));
    }

    private sealed record NamedToolResult(string Name, CallToolResult Result);
}
