#nullable enable

using System;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Registration;
using AiNetLinter.Output;
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
        Assert.Contains("AiNetLinter MCP-Bootstrap", content.Text, StringComparison.Ordinal);
        Assert.Contains("ainetlinter-rules.json", content.Text, StringComparison.Ordinal);
        Assert.Contains("ainetlinter --docs mcp-rule", content.Text, StringComparison.Ordinal);
        Assert.Contains(".agents/rules", content.Text, StringComparison.Ordinal);
        Assert.Contains(".cursor/rules", content.Text, StringComparison.Ordinal);
        Assert.Contains("## Laufzeitpfad des MCP-Servers", content.Text, StringComparison.Ordinal);
        Assert.Contains("--mcp-server", content.Text, StringComparison.Ordinal);
        Assert.Contains("Dauerhafte Agentenregel", content.Text, StringComparison.Ordinal);
        Assert.Contains("alwaysApply: true", content.Text, StringComparison.Ordinal);
        Assert.Contains("MUSS zuerst das passende", content.Text, StringComparison.Ordinal);
        Assert.Contains("report_observability_feedback", content.Text, StringComparison.Ordinal);

        const string workflowMarker = "## Dauerhafte Agentenregel\n\n";
        var workflowStart = content.Text.IndexOf(workflowMarker, StringComparison.Ordinal);
        Assert.True(workflowStart >= 0);
        var workflow = content.Text[(workflowStart + workflowMarker.Length)..];
        Assert.DoesNotContain("## Ablauf", workflow, StringComparison.Ordinal);
        Assert.Contains("targetPath", workflow, StringComparison.Ordinal);
        Assert.Contains("get_server_health", workflow, StringComparison.Ordinal);
        Assert.Contains("report_observability_feedback", workflow, StringComparison.Ordinal);
        Assert.Contains(".dll", workflow, StringComparison.Ordinal);
        Assert.Contains("symbolIdentifier", workflow, StringComparison.Ordinal);
        Assert.Contains("get_feature_context", workflow, StringComparison.Ordinal);
        Assert.Contains("get_symbol_body", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("assemblyPath", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("`view_file`", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("`list_dir`", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("deutlich weniger Kontext", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("operationStatus", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("`capabilities`", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("Consumer-Kontext", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("optionalen Consumer-Typ", workflow, StringComparison.Ordinal);

        var embeddedWorkflow = EmbeddedResourceReader
            .ReadRequired(McpAgentGuideRegistration.WorkflowResourceName)
            .Trim();
        Assert.Equal(embeddedWorkflow, workflow);
    }
}
