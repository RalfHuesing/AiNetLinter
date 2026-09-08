#nullable enable

using System;
using System.IO;
using System.Linq;
using AiNetLinter.Mcp.Projects;
using AiNetLinter.Mcp;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace AiNetLinter.Mcp.Registration;

internal static class RulesResourceRegistration
{
    internal const string RulesUriTemplate = "ainetlinter://rules{?targetPath}";

    internal static void Register(McpServerResourceCollection resources, ProjectRegistry registry)
    {
        resources.Add(McpServerResource.Create(
            (string targetPath) => BuildTemplatedResult(registry, targetPath),
            new McpServerResourceCreateOptions
            {
                UriTemplate = RulesUriTemplate,
                Name = "rules",
                Description = "Effektive Regelkonfiguration des adressierten Projekt-Keys als frisch " +
                    "generiertes Markdown. Pflicht: absoluter, URL-kodierter targetPath der konkreten Solution.",
                MimeType = "text/markdown",
            }));
    }

    internal static ReadResourceResult BuildTemplatedResult(ProjectRegistry registry, string? targetPath)
    {
        var target = ResolveTarget(targetPath);
        return ProjectResourceLease.Execute(registry, target.CanonicalPath, BuildResult);
    }

    internal static string BuildRulesText(ProjectSnapshot snapshot) =>
        RulesResourceFormatter.BuildMarkdown(snapshot);

    private static string BuildCanonicalUri(string targetPath) =>
        $"ainetlinter://rules?targetPath={Uri.EscapeDataString(targetPath)}";

    private static ReadResourceResult BuildResult(ProjectSnapshot snapshot)
    {
        var configSnapshot = snapshot.Server.GetConfigSnapshot();
        var notConfigured = configSnapshot.Config is null;
        var target = McpNavigationProjection.WithSourceSnapshot(
            AnalysisTargetResolver.ResolveRequiredSourceTarget(snapshot.Definition.SolutionPath),
            snapshot.Server);
        var navigation = McpNavigationProjection.Create(
            target,
            notConfigured ? "not_configured" : "ok",
            notConfigured ? "not_configured" : "complete",
            notConfigured
                ? "ainetlinter-rules.json neben dem adressierten Target anlegen und die Resource erneut lesen."
                : null);
        return new ReadResourceResult
        {
            Contents =
            [
                new TextResourceContents
                {
                    Uri = BuildCanonicalUri(Path.GetFullPath(snapshot.Definition.SolutionPath)),
                    MimeType = "text/markdown",
            Text = BuildRulesText(snapshot) + Environment.NewLine + Environment.NewLine +
                McpNavigationText.Format(navigation),
                },
            ],
        };
    }

    private static AnalysisTarget ResolveTarget(string? targetPath)
        => AnalysisTargetResolver.ResolveRequiredSourceTarget(targetPath);
}
