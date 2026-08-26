#nullable enable

using System;
using AiNetLinter.Mcp.Projects;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace AiNetLinter.Mcp.Registration;

internal static class RulesResourceRegistration
{
    internal const string RulesUriTemplate = "ainetlinter://rules{?projectRoot}";

    internal static void Register(McpServerResourceCollection resources, ProjectRegistry registry)
    {
        resources.Add(McpServerResource.Create(
            (string projectRoot) => BuildTemplatedResult(registry, projectRoot),
            new McpServerResourceCreateOptions
            {
                UriTemplate = RulesUriTemplate,
                Name = "rules",
                Description = "Effektive Regelkonfiguration des adressierten Projekt-Keys als frisch " +
                    "generiertes Markdown. Pflicht: absoluter, URL-kodierter projectRoot.",
                MimeType = "text/markdown",
            }));
    }

    internal static ReadResourceResult BuildTemplatedResult(ProjectRegistry registry, string? projectRoot) =>
        ProjectResourceLease.Execute(registry, projectRoot, BuildResult);

    internal static string BuildRulesText(ProjectSnapshot snapshot) =>
        RulesResourceFormatter.BuildMarkdown(snapshot);

    private static string BuildCanonicalUri(string projectRoot) =>
        $"ainetlinter://rules?projectRoot={Uri.EscapeDataString(projectRoot)}";

    private static ReadResourceResult BuildResult(ProjectSnapshot snapshot)
    {
        return new ReadResourceResult
        {
            Contents =
            [
                new TextResourceContents
                {
                    Uri = BuildCanonicalUri(snapshot.RootPath),
                    MimeType = "text/markdown",
                    Text = BuildRulesText(snapshot),
                },
            ],
        };
    }
}
