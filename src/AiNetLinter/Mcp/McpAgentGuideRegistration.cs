#nullable enable

using System;
using AiNetLinter.Output;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace AiNetLinter.Mcp;

/// <summary>
/// Registriert den statischen Erstkontakt-Leitfaden für Agenten, bevor ein Projekt-Key
/// geladen werden kann. Der Bootstrap stammt aus einer eigenen Dokumentationsressource; die
/// anschließende dauerhafte Agentenregel wird separat eingebettet angehängt.
/// </summary>
internal static class McpAgentGuideRegistration
{
    internal const string Uri = "ainetlinter://agent-guide";
    internal const string BootstrapResourceName = "Docs/mcp-bootstrap.md";
    internal const string WorkflowResourceName = "AgentRules/AiNetLinter-McpWorkflow.mdc";

    private static readonly Lazy<string> GuideText = new(
        BuildGuideText);

    internal static void Register(McpServerResourceCollection resources)
    {
        resources.Add(McpServerResource.Create(
            BuildResource,
            new McpServerResourceCreateOptions
            {
                UriTemplate = Uri,
                Name = "agent-guide",
                Description = "Onboarding für die AiNetLinter-MCP-Integration; ohne projectRoot lesbar.",
                MimeType = "text/markdown",
            }));
    }

    internal static ReadResourceResult BuildResource() => new()
    {
        Contents =
        [
            new TextResourceContents
            {
                Uri = Uri,
                MimeType = "text/markdown",
                Text = GuideText.Value,
            },
        ],
    };

    private static string BuildGuideText()
    {
        var bootstrap = EmbeddedResourceReader.ReadRequired(BootstrapResourceName).TrimEnd();
        var workflow = EmbeddedResourceReader.ReadRequired(WorkflowResourceName).Trim();
        return bootstrap + "\n\n---\n\n## Dauerhafte Agentenregel\n\n" + workflow;
    }
}
