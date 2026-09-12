#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Projects;
using AiNetLinter.Output;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using ModelContextProtocol;

namespace AiNetLinter.Mcp.Registration;

/// <summary>
/// Registriert die MCP-Resource <c>ainetlinter://overview</c> als Resource-Template mit dem
/// Pflicht-Query-Parameter <c>targetPath</c>: ein kurzer, bei jedem <c>resources/read</c>
/// frisch generierter Markdown-Status fuer Agenten, die den Server bereits adressieren — mit
/// welcher Solution/Config-Quelle der adressierte Key tatsaechlich laeuft. MCP-Resources nehmen keine
/// Tool-Argumente, daher adressiert der URL-kodierte Projektroot den Registry-Key; Guards und
/// Fehlervertraege entsprechen denen der Tools (INVALID_ARGUMENT,
/// PROJECT_NOT_INITIALIZED). Der Erstkontakt ohne Projektdefinition erfolgt ueber die direkte
/// Resource <c>ainetlinter://agent-guide</c>; Tool-Schemas stehen in <c>tools/list</c>.
/// </summary>
internal static class OverviewResourceRegistration
{
    // RFC-6570-Form-Expansion: ainetlinter://overview{?targetPath} expandiert zu
    // ainetlinter://overview?targetPath=<url-encoded>.
    private const string OverviewUriTemplate = "ainetlinter://overview{?targetPath}";

    internal static void Register(McpServerResourceCollection resources, ProjectRegistry registry)
    {
        resources.Add(McpServerResource.Create(
            (string targetPath) => BuildTemplatedResult(registry, targetPath),
            new McpServerResourceCreateOptions
            {
                UriTemplate = OverviewUriTemplate,
                Name = "overview",
                Description = "Projektstatus fuer Agenten (geladene Solution und Regelquelle). " +
                    "Der Erstkontakt ohne Projektdefinition erfolgt ueber " +
                    "ainetlinter://agent-guide. Pflicht: Query-Parameter " +
                    "targetPath mit absolutem Pfad der konkreten .sln/.slnx-Datei (URL-kodiert). Bei jedem Read frisch generiert.",
                MimeType = "text/markdown",
            }));
    }

    internal static ReadResourceResult BuildTemplatedResult(ProjectRegistry registry, string? targetPath) =>
        BuildTemplatedResult(registry, targetPath, BuildResult);

    internal static ReadResourceResult BuildTemplatedResult(
        ProjectRegistry registry,
        string? targetPath,
        Func<ProjectSnapshot, ReadResourceResult> render)
    {
        var target = ResolveTarget(targetPath);
        return ProjectResourceLease.Execute(
            registry,
            target.CanonicalPath,
            snapshot => render(snapshot));
    }

    private static string BuildCanonicalUri(string targetPath) =>
        $"ainetlinter://overview?targetPath={Uri.EscapeDataString(targetPath)}";

    private static ReadResourceResult BuildResult(ProjectSnapshot snapshot)
    {
        var targetPath = Path.GetFullPath(snapshot.Definition.SolutionPath);
        var configSnapshot = snapshot.Server.GetConfigSnapshot();
        var notConfigured = configSnapshot.Config is null;
        var target = McpNavigationProjection.WithSourceSnapshot(
            AnalysisTargetResolver.ResolveRequiredSourceTarget(targetPath),
            snapshot.Server);
        var operationStatus = snapshot.Server.LoadState switch
        {
            ServerLoadState.Loading => "loading",
            ServerLoadState.LoadFailed => "target_mismatch",
            _ when notConfigured => "not_configured",
            _ => "ok",
        };
        var navigation = McpNavigationProjection.Create(
            new McpNavigationProjectionParameters(
                target,
                operationStatus,
                snapshot.Server.LoadState == ServerLoadState.LoadFailed ? "not_applicable" : "complete",
                Hint: snapshot.Server.LoadState == ServerLoadState.LoadFailed
                    ? "Solution-Loadfehler beheben und den Target-Call wiederholen."
                    : null));
        return new ReadResourceResult
        {
            Contents =
            [
                new TextResourceContents
                {
                    Uri = BuildCanonicalUri(targetPath),
                    MimeType = "text/markdown",
                    Text = BuildOverviewText(snapshot) + Environment.NewLine + Environment.NewLine +
                        McpNavigationText.Format(navigation),
                },
            ],
        };
    }

    private static AnalysisTarget ResolveTarget(string? targetPath)
        => AnalysisTargetResolver.ResolveRequiredSourceTarget(targetPath);

    /// <summary>Reine Text-Bau-Funktion, direkt unit-testbar ohne MCP-Protokoll-Umweg.</summary>
    internal static string BuildOverviewText(ProjectSnapshot snapshot)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# AiNetLinter MCP-Server — Projektstatus");
        sb.AppendLine();
        sb.AppendLine(
            "AiNetLinter laeuft als stdio-MCP-Server und analysiert die adressierte " +
            ".NET-Solution semantisch ueber Roslyn.");
        sb.AppendLine();
        sb.AppendLine($"## Server-Status (Projekt {snapshot.RootPath})");
        sb.AppendLine();
        sb.AppendLine($"- Solution: {DescribeSolution(snapshot.Server)}");
        sb.AppendLine($"- Regeln: {DescribeConfig(snapshot.Server)}");
        sb.AppendLine($"- Zuletzt genutzt (UTC): {snapshot.LastUsedUtc:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine();
        sb.AppendLine();
        sb.AppendLine("## Weiter");
        sb.AppendLine();
        sb.AppendLine("- Erstkontakt und Integration: `ainetlinter://agent-guide`");
        sb.AppendLine("- Vollständige Tool- und Parameterschemas: `tools/list`");
        sb.AppendLine("- Nach Änderungen: `get_impact` und `get_violations`");
        return sb.ToString().TrimEnd();
    }

    private static string DescribeSolution(McpCodeGraphServer mcpState)
    {
        return mcpState.LoadState switch
        {
            ServerLoadState.Loading => "wird noch geladen",
            ServerLoadState.LoadFailed => "Laden fehlgeschlagen — jeder Tool-Call liefert PROJECT_LOAD_FAILED bis zur Neuanlage des Keys",
            _ => mcpState.GetCurrentSolution()?.FilePath ?? "unbekannt",
        };
    }

    private static string DescribeConfig(McpCodeGraphServer mcpState)
    {
        // Atomarer Schnappschuss statt zweier getrennter Property-Zugriffe: sonst koennte ein
        // gleichzeitiger reload_config-Aufruf eine zerrissene Kombination liefern (siehe
        // McpCodeGraphServer.GetConfigSnapshot).
        var (config, resolvedConfigPath) = mcpState.GetConfigSnapshot();
        return resolvedConfigPath is null || config is null
            ? "not_configured — neben der adressierten Solution wurde keine ainetlinter-rules.json gefunden; Navigation bleibt verfügbar, Lint-Operationen sind nicht konfiguriert"
            : resolvedConfigPath ?? "unbekannt";
    }
}
