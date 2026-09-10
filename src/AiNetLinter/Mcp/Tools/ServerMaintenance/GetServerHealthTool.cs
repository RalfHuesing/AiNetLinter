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
    string? TargetPath = null,
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
        string? targetPath = null,
        DaemonRuntimeContext? runtimeContext = null) =>
        ExecuteAsync(registry, new GetServerHealthOptions(targetPath, runtimeContext));

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
        var validation = ValidateOptions(options);
        if (validation is not null) return validation;

        var effectiveOptions = NormalizeOptions(options);
        if (effectiveOptions.AssemblyPath is not null)
        {
            return await ExecuteAssemblyAsync(
                assemblyRegistry,
                effectiveOptions,
                cancellationToken).ConfigureAwait(false);
        }

        if (effectiveOptions.TargetPath is not null)
        {
            return await ExecuteTargetAsync(
                registry,
                effectiveOptions).ConfigureAwait(false);
        }

        return await ExecuteGlobalAsync(registry, assemblyRegistry, effectiveOptions).ConfigureAwait(false);
    }

    private static GetServerHealthOptions NormalizeOptions(GetServerHealthOptions options) =>
        options.TargetPath is null && options.AssemblyPath is null
            ? options with { IncludeDiagnostics = false, IncludeSessions = false }
            : options;

    private static async Task<CallToolResult> ExecuteAssemblyAsync(
        IAssemblyAnalysisRegistry? assemblyRegistry,
        GetServerHealthOptions options,
        CancellationToken cancellationToken)
    {
        if (assemblyRegistry is null)
        {
            return AssemblyAnalysisResponse.Unsupported(options.AssemblyPath!);
        }

        var leaseResult = await assemblyRegistry.LeaseAsync(options.AssemblyPath!, cancellationToken).ConfigureAwait(false);
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

    private static async Task<CallToolResult> ExecuteTargetAsync(
        ProjectRegistry registry,
        GetServerHealthOptions options)
    {
        if (options.RuntimeContext is not null)
        {
            return await ExecuteDaemonProjectAsync(
                options.RuntimeContext,
                options.TargetPath!,
                options).ConfigureAwait(false);
        }

        var guard = ProjectToolCall.GuardRequiredAbsoluteRoot(options.TargetPath!);
        if (guard is not null)
        {
            return McpToolResults.Error(guard.Code, guard.Message, hint: guard.Hint);
        }

        var snapshot = registry.FindSnapshot(options.TargetPath!);
        if (snapshot is null)
        {
            var leaseResult = registry.Lease(options.TargetPath!);
            if (!leaseResult.Succeeded || leaseResult.Lease is null)
            {
                return McpToolResults.Recoverable(
                    leaseResult.ErrorCode!,
                    leaseResult.ErrorMessage!,
                    hint: ProjectToolCall.RecoverHint(leaseResult.ErrorCode!));
            }

            using var lease = leaseResult.Lease;
            snapshot = registry.SnapshotFor(lease);
        }

        return snapshot is null
            ? ProjectNotInitialized(options.TargetPath!)
            : GetServerHealthResponseBuilder.Build(
                [snapshot],
                Array.Empty<AssemblyHealthEntry>(),
                options);
    }

    private static async Task<CallToolResult> ExecuteGlobalAsync(
        ProjectRegistry registry,
        IAssemblyAnalysisRegistry? assemblyRegistry,
        GetServerHealthOptions options)
    {
        var assemblySnapshots = assemblyRegistry is null
            ? Array.Empty<AssemblyAnalysisHealthSnapshot>()
            : await assemblyRegistry.SnapshotsAsync().ConfigureAwait(false);
        return GetServerHealthResponseBuilder.Build(
            registry.Snapshots(),
            assemblySnapshots.Select(AssemblyHealthProjection.FromSnapshot).ToList(),
            options);
    }

    internal static CallToolResult? ValidateOptions(GetServerHealthOptions options)
    {
        if (options.MaxDiagnostics > 0) return null;

        return McpToolResults.InvalidArgument(
            "maxDiagnostics muss mindestens 1 sein.",
            hint: "Eine positive Anzahl von Diagnose-Samples angeben oder maxDiagnostics weglassen.",
            fieldPath: "$.maxDiagnostics");
    }

    internal static Task<CallToolResult> ExecuteDaemonProjectAsync(
        DaemonRuntimeContext runtimeContext,
        string targetPath,
        GetServerHealthOptions options)
    {
        ArgumentNullException.ThrowIfNull(runtimeContext);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetPath);
        ArgumentNullException.ThrowIfNull(options);

        var validation = ValidateOptions(options);
        if (validation is not null) return Task.FromResult(validation);

        var guard = ProjectToolCall.GuardRequiredAbsoluteRoot(targetPath);
        if (guard is not null)
        {
            return Task.FromResult(McpToolResults.Error(guard.Code, guard.Message, hint: guard.Hint));
        }

        var snapshot = runtimeContext.FindProjectSnapshot(targetPath);
        if (snapshot is null)
        {
            var leaseResult = runtimeContext.LeaseProject(targetPath);
            if (leaseResult is not null)
            {
                if (!leaseResult.Succeeded || leaseResult.Lease is null)
                {
                    return Task.FromResult(McpToolResults.Recoverable(
                        ProjectErrorCodes.ProjectLoadFailed,
                        leaseResult.ErrorMessage ?? $"Projekt '{targetPath}' konnte nicht geladen werden."));
                }

                using var lease = leaseResult.Lease;
                snapshot = runtimeContext.FindProjectSnapshot(targetPath);
            }
        }

        return Task.FromResult(snapshot is null
            ? ProjectNotInitialized(targetPath)
            : GetServerHealthResponseBuilder.Build(
                [snapshot],
                Array.Empty<AssemblyHealthEntry>(),
                options));
    }

    private static CallToolResult ProjectNotInitialized(string targetPath) =>
        McpToolResults.Error(
            ProjectErrorCodes.ProjectNotInitialized,
            $"Fuer '{targetPath}' existiert kein residenter Projekt-Key.",
            context: targetPath,
            hint: "Ersten zielgebundenen Tool-Aufruf mit diesem targetPath senden; die Solution wird " +
                  "direkt geladen und optionale ainetlinter-rules.json daneben ausgewertet.");
}
