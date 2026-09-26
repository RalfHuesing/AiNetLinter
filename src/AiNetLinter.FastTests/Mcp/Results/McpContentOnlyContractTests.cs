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

    [Fact]
    public void SymbolNotFound_IsProtocolErrorWithMatchingContentStatus()
    {
        var result = McpToolResults.WithNavigation(McpToolResults.SymbolNotFound("Missing.Symbol"));
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;

        Assert.True(result.IsError);
        Assert.Contains("[ERROR]: SYMBOL_NOT_FOUND", text, StringComparison.Ordinal);
        Assert.Contains("Status: operation=error", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Loading_HasExplicitRetryStatusWithoutSemanticResult()
    {
        var result = McpToolResults.WithNavigation(McpToolResults.Loading());
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;

        Assert.NotEqual(true, result.IsError);
        Assert.Contains("Status: operation=retry", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Status: operation=ok", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Navigation_AlignsProtocolFlagWithErrorContent()
    {
        var result = McpToolResults.WithNavigation(new CallToolResult
        {
            IsError = false,
            Content = [new TextContentBlock { Text = "[ERROR]: INVALID_ARGUMENT: Eingabe fehlt." }],
        });

        Assert.True(result.IsError);
        Assert.Contains("Status: operation=error", Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text);
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
