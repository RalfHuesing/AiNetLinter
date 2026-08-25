#nullable enable

using System;
using AiNetLinter.Output;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace AiNetLinter.Mcp;

/// <summary>
/// Registriert den statischen Erstkontakt-Leitfaden für Agenten, bevor ein Projekt-Key
/// geladen werden kann. Der Inhalt stammt aus der eingebetteten AiNetLinter-MCP-Regeldatei.
/// </summary>
internal static class McpAgentGuideRegistration
{
    internal const string Uri = "ainetlinter://agent-guide";
    internal const string EmbeddedResourceName = "AgentRules/AiNetLinter-McpWorkflow.mdc";

    private static readonly Lazy<string> GuideText = new(
        () => EmbeddedResourceReader.ReadRequired(EmbeddedResourceName));

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
}
