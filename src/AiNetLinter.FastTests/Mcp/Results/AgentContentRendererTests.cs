#nullable enable

using System;
using System.Collections.Generic;
using System.Text;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Wire;
using AiNetLinter.Output;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Results;

[Trait("Category", "Unit")]
public sealed class AgentContentRendererTests
{
    [Fact]
    public void Render_SelectsWholeEvidenceInDeclaredOrderAndUsesUtf8Budget()
    {
        var renderer = new AgentContentRenderer();
        var request = new AgentContentRenderRequest(
            IsError: false,
            Evidence:
            [
                new AgentContentEvidence("ID: M:Demo.Überblick", IsRequired: true),
                new AgentContentEvidence("Detail: vollständig", IsRequired: false),
                new AgentContentEvidence("Weitere Details", IsRequired: false),
            ],
            MaxResponseBytes: Encoding.UTF8.GetByteCount("ID: M:Demo.Überblick\nDetail: vollständig"));

        var result = renderer.Render(request);

        Assert.Equal("ID: M:Demo.Überblick\nDetail: vollständig", result.Text);
        Assert.True(result.IsTruncated);
        Assert.True(Encoding.UTF8.GetByteCount(result.Text) <= request.MaxResponseBytes);
        Assert.DoesNotContain("Weitere", result.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_ReportsExactMinimumForRequiredVisibleContent()
    {
        var renderer = new AgentContentRenderer();
        var required = "ID: M:Demo.Überblick";
        var request = new AgentContentRenderRequest(
            IsError: false,
            Evidence: [new AgentContentEvidence(required, IsRequired: true)],
            MaxResponseBytes: Encoding.UTF8.GetByteCount(required) - 1);

        var result = renderer.Render(request);

        Assert.True(result.IsBudgetTooSmall);
        Assert.Equal(Encoding.UTF8.GetByteCount(required), result.MinimumResponseBytes);
    }

    [Fact]
    public void ResponseBudgetTooSmall_ContainsExactMinimumAndExecutableRetry()
    {
        var result = McpToolResults.Recoverable(
            LinterErrorCodes.ResponseBudgetTooSmall,
            "Antwortbudget ist zu klein.",
            new McpErrorParameters(RequestedBytes: 128, MinimumResponseBytes: 256));

        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.Contains("minimumResponseBytes: 256", text, StringComparison.Ordinal);
        Assert.Contains("maxResponseBytes=256", text, StringComparison.Ordinal);
    }
}
