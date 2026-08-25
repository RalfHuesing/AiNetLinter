#nullable enable

using System;
using AiNetLinter.Mcp;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.FastTests.Mcp;

/// <summary>Vertragstests für den statischen MCP-Erstkontakt-Leitfaden.</summary>
[Trait("Category", "Unit")]
public sealed class McpAgentGuideRegistrationTests
{
    [Fact]
    public void BuildResource_IsReadableWithoutProjectAndContainsIntegrationContract()
    {
        var result = McpAgentGuideRegistration.BuildResource();
        var content = Assert.IsType<TextResourceContents>(Assert.Single(result.Contents));

        Assert.Equal(McpAgentGuideRegistration.Uri, content.Uri);
        Assert.Equal("text/markdown", content.MimeType);
        Assert.Contains("ainetlinter.project.json", content.Text, StringComparison.Ordinal);
        Assert.Contains("ainetlinter --docs rules-json", content.Text, StringComparison.Ordinal);
        Assert.Contains(".agents/rules", content.Text, StringComparison.Ordinal);
        Assert.Contains(".cursor/rules", content.Text, StringComparison.Ordinal);
        Assert.Contains("report_observability_feedback", content.Text, StringComparison.Ordinal);
    }
}
