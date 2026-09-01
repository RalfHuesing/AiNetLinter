#nullable enable

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp.Assemblies.Analysis;
using AiNetLinter.Mcp.Daemon;
using AiNetLinter.Mcp.Projects;
using AiNetLinter.Mcp.Tools.AssemblyAnalysis;
using AiNetLinter.Mcp.Tools.ServerMaintenance.Projection;
using AiNetLinter.Output;
using ModelContextProtocol.Protocol;

namespace AiNetLinter.Mcp.Tools.ServerMaintenance;

internal sealed record GetServerHealthOptions(
    string? ProjectRoot = null,
    DaemonRuntimeContext? RuntimeContext = null,
    string? AssemblyPath = null,
    bool IncludeDiagnostics = false,
    int MaxDiagnostics = AssemblyAnalysisResponseLimits.DefaultMaxDiagnostics,
    bool IncludeSessions = false,
    int MaxSessions = GetServerHealthTool.DefaultMaxSessions);

/// <summary>
/// MCP-Tool <c>get_server_health</c> fuer den Diagnose-Schnappschuss der residenten
/// Projekt- und Assembly-Sessions. Die Antwortsprojektion liegt in einer separaten
/// Builder-Verantwortung, damit Routing und Ausgabe getrennt bleiben.
/// </summary>
internal static class GetServerHealthTool
{
    internal const int DefaultMaxSessions = 20;
    internal const int MaxSessions = 50;

    internal static Task<CallToolResult> ExecuteAsync(
        ProjectRegistry registry,
        string? projectRoot = null,
        DaemonRuntimeContext? runtimeContext = null) =>
        ExecuteAsync(registry, new GetServerHealthOptions(projectRoot, runtimeContext));

    internal static Task<CallToolResult> ExecuteAsync(
        ProjectRegistry registry,
        GetServerHealthOptions options)
        => ExecuteAsync(registry, assemblyRegistry: null, options, CancellationToken.None);

    internal static async Task<CallToolResult> ExecuteAsync(
        ProjectRegistry registry,
        IAssemblyAnalysisRegistry? assemblyRegistry,
        GetServerHealthOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.AssemblyPath is not null)
        {
            if (assemblyRegistry is null)
            {
                return AssemblyAnalysisResponse.Unsupported(options.AssemblyPath);
            }

            var leaseResult = await assemblyRegistry.LeaseAsync(options.AssemblyPath, cancellationToken).ConfigureAwait(false);
            if (leaseResult.Error is not null)
            {
                return leaseResult.Error;
            }

            using var lease = leaseResult.Lease!;
            await lease.ExpandReferencesAsync(cancellationToken).ConfigureAwait(false);
            return GetServerHealthResponseBuilder.Build(
                Array.Empty<ProjectSnapshot>(),
                [AssemblyHealthProjection.FromLease(lease)],
                options);
        }

        if (options.ProjectRoot is not null)
        {
            var guard = ProjectToolCall.GuardRequiredAbsoluteRoot(options.ProjectRoot);
            if (guard is not null)
            {
                return McpToolResults.Error(guard.Code, guard.Message, hint: guard.Hint);
            }

            var snapshot = registry.FindSnapshot(options.ProjectRoot);
            if (snapshot is null) return ProjectNotInitialized(options.ProjectRoot);
            return GetServerHealthResponseBuilder.Build(
                [snapshot],
                Array.Empty<AssemblyHealthEntry>(),
                options);
        }

        var assemblySnapshots = assemblyRegistry is null
            ? Array.Empty<AssemblyAnalysisHealthSnapshot>()
            : await assemblyRegistry.SnapshotsAsync().ConfigureAwait(false);
        return GetServerHealthResponseBuilder.Build(
            registry.Snapshots(),
            assemblySnapshots.Select(AssemblyHealthProjection.FromSnapshot).ToList(),
            options);
    }

    private static CallToolResult ProjectNotInitialized(string projectRoot) =>
        McpToolResults.Error(
            ProjectErrorCodes.ProjectNotInitialized,
            $"Fuer '{projectRoot}' existiert kein residenter Projekt-Key.",
            context: projectRoot,
            hint: "Ersten Tool-Aufruf mit diesem targetPath senden; der Server legt den Key lazy " +
                  "ueber eine Definitionsdatei ainetlinter.project.json im Projektroot an.");
}
