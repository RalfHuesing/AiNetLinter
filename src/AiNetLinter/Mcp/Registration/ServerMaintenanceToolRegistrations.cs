#nullable enable

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Assemblies.Analysis;
using AiNetLinter.Mcp.Projects;
using AiNetLinter.Mcp.Tools.AssemblyAnalysis;
using AiNetLinter.Mcp.Tools.ServerMaintenance;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace AiNetLinter.Mcp.Registration;

/// <summary>
/// Registriert die server-eigenen Wartungs-/Diagnose-Tools (<c>reload_config</c>,
/// <c>get_server_health</c>) an der von <see cref="McpServerOptionsFactory"/> aufgebauten
/// Tool-Collection. Eigene Registrar-Klasse statt Anhaengen an eine bestehende Gruppe, weil diese
/// Tools semantisch den Server-Prozess selbst betreffen (Config-Reload, Health-Snapshot) statt die
/// Solution/den Symbolgraph zu befragen — passt zu keiner der bestehenden Gruppen (Symbolgraph,
/// Dateistruktur, Analyse, Symbol-Body).
/// </summary>
internal static class ServerMaintenanceToolRegistrations
{
    /// <summary>
    /// Fuegt <paramref name="tools"/> die Wartungs-Tools hinzu. Tools erreichen die residente
    /// Instanz ihres Keys per Lease-Closure - kein DI-Container (siehe
    /// <c>AiNetLinterRichtlinien.mdc</c> §2). Einzige Pflicht-Ausnahme: <c>get_server_health</c>
    /// nimmt ein optionales, paarweise zu validierendes Target und aggregiert ohne Target ueber alle Keys.
    /// </summary>
    internal static void Register(
        McpServerPrimitiveCollection<McpServerTool> tools,
        ProjectRegistry registry,
        Daemon.DaemonRuntimeContext? runtimeContext = null,
        IAssemblyAnalysisRegistry? assemblyRegistry = null)
    {
        AddReloadConfig(tools, registry);
        AddGetServerHealth(tools, registry, runtimeContext, assemblyRegistry);
        AddReportObservabilityFeedback(tools);
    }

    private static void AddReloadConfig(
        McpServerPrimitiveCollection<McpServerTool> tools,
        ProjectRegistry registry)
    {
        tools.Add(McpServerTool.Create(
            async (RequestContext<CallToolRequestParams> context, string targetPath, CancellationToken ct = default) =>
            {
                var unknownError = TargetPathToolRegistrationOptions.RejectUnknownArguments(context);
                if (unknownError is not null) return unknownError;
                return await ProjectAnalysisDispatcher.ExecuteAsync(
                    registry,
                    new AnalysisTargetRequest(targetPath),
                    lease => ReloadConfigTool.ExecuteAsync(
                        lease.Server,
                        Path.Combine(Path.GetDirectoryName(targetPath)!, "ainetlinter-rules.json"),
                        ct));
            },
            TargetPathToolRegistrationOptions.ReloadConfigTool("reload_config", ReloadConfigDescription)));
    }

    private const string ReloadConfigDescription =
        "Wann nutzen: ainetlinter-rules.json wurde waehrend des Server-Laufs geaendert und get_violations " +
        "soll die neuen Regeln sofort respektieren, ohne den Server neu zu starten. Gelesen wird " +
        "ausschliesslich die optionale ainetlinter-rules.json neben der adressierten Solution. " +
        "Bei ungueltigem Pfad/JSON bleibt die bisherige Konfiguration aktiv.";

    private static void AddGetServerHealth(
        McpServerPrimitiveCollection<McpServerTool> tools,
        ProjectRegistry registry,
        Daemon.DaemonRuntimeContext? runtimeContext,
        IAssemblyAnalysisRegistry? assemblyRegistry)
    {
        tools.Add(McpServerTool.Create(
            async (
                RequestContext<CallToolRequestParams> context,
                string? targetPath = null,
                bool includeDiagnostics = false,
                int maxDiagnostics = AssemblyAnalysisResponseLimits.DefaultMaxDiagnostics,
                bool includeSessions = false,
                int maxSessions = GetServerHealthTool.DefaultMaxSessions,
                 CancellationToken ct = default) =>
            {
                var unknownError = TargetPathToolRegistrationOptions.RejectUnknownArguments(context);
                if (unknownError is not null) return unknownError;
                return await ExecuteGetServerHealthAsync(
                    registry,
                    runtimeContext,
                    assemblyRegistry,
                    new GetServerHealthRequest(
                        targetPath,
                        includeDiagnostics,
                        maxDiagnostics,
                        includeSessions,
                        maxSessions,
                        ct));
            },
            TargetPathToolRegistrationOptions.ServerHealthTool("get_server_health", GetServerHealthDescription)));
    }

    private static async Task<CallToolResult> ExecuteGetServerHealthAsync(
        ProjectRegistry registry,
        Daemon.DaemonRuntimeContext? runtimeContext,
        IAssemblyAnalysisRegistry? assemblyRegistry,
        GetServerHealthRequest request)
    {
        var resolution = AnalysisTargetResolver.ResolveOptional(
            new AnalysisTargetRequest(request.TargetPath));
        if (resolution.Error is not null) return resolution.Error;

        var options = CreateHealthOptions(resolution.Target, runtimeContext, request);
        if (resolution.Target?.TargetType == AnalysisTargetType.Project && runtimeContext is not null)
        {
            var result = await GetServerHealthTool.ExecuteDaemonProjectAsync(
                runtimeContext,
                resolution.Target.CanonicalPath,
                options);
            return McpToolResults.WithNavigation(result, resolution.Target);
        }

        if (resolution.Target is null)
        {
            return await GetServerHealthTool.ExecuteAsync(registry, assemblyRegistry, options);
        }

        var targetedResult = await GetServerHealthTool.ExecuteAsync(
            registry,
            assemblyRegistry,
            options,
            request.CancellationToken);
        return McpToolResults.WithNavigation(targetedResult, resolution.Target);
    }

    private static GetServerHealthOptions CreateHealthOptions(
        AnalysisTarget? target,
        Daemon.DaemonRuntimeContext? runtimeContext,
        GetServerHealthRequest request) =>
        new(
            TargetPath: target?.TargetType == AnalysisTargetType.Project ? target.CanonicalPath : null,
            RuntimeContext: runtimeContext,
            AssemblyPath: target?.TargetType == AnalysisTargetType.Assembly ? target.CanonicalPath : null,
            IncludeDiagnostics: request.IncludeDiagnostics,
            MaxDiagnostics: request.MaxDiagnostics,
            IncludeSessions: request.IncludeSessions,
            MaxSessions: request.MaxSessions);

    private sealed record GetServerHealthRequest(
        string? TargetPath,
        bool IncludeDiagnostics,
        int MaxDiagnostics,
        bool IncludeSessions,
        int MaxSessions,
        CancellationToken CancellationToken);

    private static readonly string GetServerHealthDescription =
        "Wann nutzen: pruefen, ob der Server laeuft und welche Projekt- und Assembly-Sessions " +
        "resident sind. Ohne targetPath: globaler Status fuer alle Projekt-Keys und Assembly-Sessions. " +
        "Mit targetPath als .sln/.slnx wird der Projekt-Key, mit .dll/.exe die Assembly-Session gezielt geprueft. " +
         "Standardmaessig werden global nur Aggregat, Status- und Diagnosezaehler geliefert; " +
         "includeSessions=true fordert begrenzte Sessiondetails an, maxSessions wird serverseitig " +
         $"auf {GetServerHealthTool.MaxSessions} gedeckelt. includeDiagnostics=true fordert begrenzte " +
         "Diagnose-Samples an, maxDiagnostics begrenzt deren Anzahl. Zielgebundene Antworten bleiben detailliert.";

    private static void AddReportObservabilityFeedback(McpServerPrimitiveCollection<McpServerTool> tools)
    {
        tools.Add(McpServerTool.Create(
             (RequestContext<CallToolRequestParams> context,
             string feedbackType,
             string title,
             string description,
             string? relatedTool = null,
             string? severity = "medium",
             string? expectedBehavior = null,
             string? actualBehavior = null,
             string? additionalContext = null,
             CancellationToken ct = default) =>
            {
                 var unknownError = TargetPathToolRegistrationOptions.RejectUnknownArguments(context);
                  if (unknownError is not null) return Task.FromResult(unknownError);
                  return ReportObservabilityFeedbackTool.ExecuteAsync(
                    new ReportObservabilityFeedbackParameters(
                        feedbackType,
                        title,
                        description,
                        relatedTool,
                        severity,
                        expectedBehavior,
                        actualBehavior,
                         additionalContext,
                          ProjectRoot: null));
            },
            TargetPathToolRegistrationOptions.FeedbackTool("report_observability_feedback", ReportObservabilityFeedbackDescription)));
    }

    private const string ReportObservabilityFeedbackDescription =
        "Wann nutzen: Ein MCP-Tool meldet einen unerwarteten internen Fehler, liefert verwirrende " +
        "Ausgaben, einen False Positive oder ein Feature fehlt. NICHT nutzen fuer normale " +
        "Leermengen (z. B. Symbol/Datei existiert im Code nicht). feedbackType: bug, false_positive, " +
        "confusing_output, feature_request, performance. title, description Pflicht. severity (Default 'medium'). " +
        "Protokolliert das Feedback direkt in das System-Log zur Auswertung. Nach dem Absenden mit dem besten Workaround fortfahren.";
}
