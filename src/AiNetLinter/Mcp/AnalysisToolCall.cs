#nullable enable

using System.Threading.Tasks;
using AiNetLinter.Mcp.Assemblies.Analysis;
using AiNetLinter.Mcp.Assemblies.Analysis.References;
using AiNetLinter.Mcp.Projects;
using AiNetLinter.Mcp.Registration;
using AiNetLinter.Output;
using ModelContextProtocol.Protocol;

namespace AiNetLinter.Mcp;

internal delegate Task<CallToolResult> AnalysisToolRoute(AnalysisToolCallRequest request);

internal static class ProjectAnalysisDispatcher
{
    internal static AnalysisToolRoute CreateRoute(ProjectRegistry registry) => request =>
        request.Dispatch.ProjectCall is null
            ? Task.FromResult(UnsupportedProjectTarget())
            : ExecuteAsync(
                registry,
                request.Target,
                request.Dispatch.ProjectCall,
                request.Dispatch.MaxResponseBytes,
                request.Dispatch.PostNavigationResponseBudget);

    internal static Task<CallToolResult> ExecuteAsync(
        ProjectRegistry registry,
        AnalysisTargetRequest request,
        Func<ProjectLease, Task<CallToolResult>> projectCall,
        int maxResponseBytes = 0,
        Func<CallToolResult, int, CallToolResult>? postNavigationResponseBudget = null) =>
        ExecuteProjectAsync(registry, request, projectCall, maxResponseBytes, postNavigationResponseBudget);

    internal static Task<CallToolResult> ExecuteConfiguredAsync(
        ProjectRegistry registry,
        AnalysisTargetRequest request,
        Func<ProjectLease, Task<CallToolResult>> projectCall) =>
        ExecuteProjectAsync(
            registry,
            request,
            lease => lease.Server.GetConfigSnapshot() is var configSnapshot &&
                configSnapshot.Config is null
                ? Task.FromResult(McpToolResults.Recoverable(
                    LinterErrorCodes.NotConfigured,
                    "Diese Lint-Operation ist für die Solution nicht konfiguriert: neben der Solution wurde keine ainetlinter-rules.json gefunden.",
                    context: lease.Definition.SolutionPath,
                    hint: "ainetlinter-rules.json neben der adressierten .sln/.slnx anlegen und den Aufruf erneut starten."))
                : projectCall(lease));

    private static async Task<CallToolResult> ExecuteProjectAsync(
        ProjectRegistry registry,
        AnalysisTargetRequest request,
        Func<ProjectLease, Task<CallToolResult>> projectCall,
        int maxResponseBytes = 0,
        Func<CallToolResult, int, CallToolResult>? postNavigationResponseBudget = null)
    {
        var resolution = AnalysisTargetResolver.ResolveTargetPathOnly(request);
        if (resolution.Error is not null)
        {
            return resolution.Error;
        }

        var target = resolution.Target!;
        if (target.TargetType == AnalysisTargetType.Project)
        {
            AnalysisTarget? analysisTarget = null;
            var result = await ProjectToolCall.ExecuteAsync(
                registry,
                target.CanonicalPath,
                async lease =>
                {
                    var leasedResult = await projectCall(lease);
                    analysisTarget = McpNavigationProjection.WithSourceSnapshot(target, lease.Server);
                    return leasedResult;
                });
            return McpToolResults.WithNavigation(
                result,
                analysisTarget ?? target,
                maxResponseBytes,
                postNavigationResponseBudget);
        }

        return McpToolResults.WithNavigation(UnsupportedAssemblyTarget(target.CanonicalPath), target);
    }

    internal static async Task<CallToolResult> ExecuteFilesystemAsync(
        ProjectRegistry registry,
        AnalysisTargetRequest request,
        Func<ProjectLease, Task<CallToolResult>> projectCall)
    {
        var resolution = AnalysisTargetResolver.ResolveTargetPathOnly(request);
        if (resolution.Error is not null)
        {
            return resolution.Error;
        }

        if (resolution.Target!.TargetType == AnalysisTargetType.Assembly)
        {
            return McpToolResults.WithNavigation(
                UnsupportedAssemblyTarget(resolution.Target.CanonicalPath),
                resolution.Target);
        }

        AnalysisTarget? analysisTarget = null;
        var result = await ProjectToolCall.ExecuteFilesystemAsync(
            registry,
            resolution.Target.CanonicalPath,
            async lease =>
            {
                var leasedResult = await projectCall(lease);
                analysisTarget = McpNavigationProjection.WithSourceSnapshot(resolution.Target, lease.Server);
                return leasedResult;
            });
        return McpToolResults.WithNavigation(result, analysisTarget ?? resolution.Target);
    }

    /// <summary>
    /// Resolves a physical project target without leasing a Roslyn project session. This is the
    /// direct route for read-only filesystem discovery, including materialized decompiler roots
    /// that deliberately do not materialize a Roslyn project session.
    /// </summary>
    internal static async Task<CallToolResult> ExecutePhysicalFilesystemAsync(
        AnalysisTargetRequest request,
        Func<string, Task<CallToolResult>> filesystemCall)
    {
        var resolution = AnalysisTargetResolver.ResolveTargetPathOnly(request);
        if (resolution.Error is not null)
        {
            return resolution.Error;
        }

        var target = resolution.Target!;
        return target.TargetType == AnalysisTargetType.Project
            ? McpToolResults.WithNavigation(await filesystemCall(target.CanonicalPath), target)
            : McpToolResults.WithNavigation(UnsupportedAssemblyTarget(target.CanonicalPath), target);
    }

    internal static CallToolResult UnsupportedAssemblyTarget(string? canonicalPath = null) =>
        canonicalPath is null
            ? McpToolResults.Recoverable(
                LinterErrorCodes.AssemblyTargetUnsupported,
                "Ein Assembly-Ziel wird für dieses Tool noch nicht unterstützt.",
                hint: "Eine unterstützte Source-Operation mit targetPath auf eine .sln/.slnx-Datei verwenden.")
            : AssemblyAnalysisResponse.Unsupported(canonicalPath);

    internal static CallToolResult UnsupportedProjectTarget() =>
        McpToolResults.Recoverable(
            LinterErrorCodes.InvalidArgument,
            "Dieses Tool unterstützt kein Projekt-Ziel.",
            hint: "targetPath auf eine vorhandene .dll/.exe-Datei setzen und eine Assembly-Operation verwenden.");
}

internal static class AssemblyAnalysisDispatcher
{
    internal static AnalysisToolRoute CreateRoute(IAssemblyAnalysisRegistry? assemblyRegistry) => request =>
        request.Dispatch.AssemblySessionCall is null
            ? UnsupportedRouteAsync(request)
            : ExecuteAsync(
                assemblyRegistry,
                request.Target,
                request.Dispatch.AssemblySessionCall,
                new AssemblyAnalysisExecutionOptions(
                    request.Dispatch.ExpandAssemblyReferences,
                    request.CancellationToken,
                    request.Dispatch.MaxResponseBytes,
                    request.Dispatch.DetailLevel,
                    request.Dispatch.Cursor,
                    request.Dispatch.PostNavigationResponseBudget));

    private static Task<CallToolResult> UnsupportedRouteAsync(AnalysisToolCallRequest request)
    {
        var resolution = AnalysisTargetResolver.ResolveTargetPathOnly(request.Target);
        if (resolution.Error is not null)
        {
            return Task.FromResult(resolution.Error);
        }

        var result = resolution.Target!.TargetType == AnalysisTargetType.Assembly
            ? UnsupportedAssemblyTarget(resolution.Target.CanonicalPath)
            : UnsupportedProjectTarget();
        return Task.FromResult(McpToolResults.WithNavigation(result, resolution.Target));
    }

    // ainetlinter-disable MaxMethodLineCount — der gemeinsame Dispatch bündelt Target-Aufloesung, Lease, Enrichment und Navigation.
    internal static async Task<CallToolResult> ExecuteAsync(
        IAssemblyAnalysisRegistry? assemblyRegistry,
        AnalysisTargetRequest request,
        Func<AssemblyAnalysisLease, Task<CallToolResult>> assemblyCall,
        AssemblyAnalysisExecutionOptions options)
    {
        var resolution = AnalysisTargetResolver.ResolveTargetPathOnly(request);
        if (resolution.Error is not null)
        {
            return resolution.Error;
        }

        var target = resolution.Target!;
        if (target.TargetType == AnalysisTargetType.Project)
        {
            return McpToolResults.WithNavigation(UnsupportedProjectTarget(), target);
        }

        if (assemblyRegistry is null)
        {
            return McpToolResults.WithNavigation(UnsupportedAssemblyTarget(target.CanonicalPath), target);
        }

        var leaseResult = await assemblyRegistry.LeaseAsync(target.CanonicalPath, options.CancellationToken).ConfigureAwait(false);
        if (leaseResult.Error is not null)
        {
            return McpToolResults.WithNavigation(leaseResult.Error, target);
        }

        var lease = leaseResult.Lease!;
        try
        {
            if (options.ExpandAssemblyReferences)
            {
                await lease.ExpandReferencesAsync(options.CancellationToken).ConfigureAwait(false);
            }

            var result = await assemblyCall(lease).ConfigureAwait(false);
            var snapshotTarget = target with
            {
                AnalysisSnapshotFingerprint = lease.Context.Origin.ContentHash,
                AnalysisSnapshotKind = "assembly",
                AnalysisSnapshotFresh = true,
            };
            var navigated = McpToolResults.WithNavigation(result, snapshotTarget);
            var enriched = AssemblyAnalysisResponse.Enrich(
                navigated,
                lease,
                new AssemblyAnalysisResponseRequest(
                    options.MaxResponseBytes,
                    options.DetailLevel,
                    options.Cursor));
            return options.PostNavigationResponseBudget is null || options.MaxResponseBytes <= 0
                ? enriched
                : options.PostNavigationResponseBudget(enriched, options.MaxResponseBytes);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return McpToolResults.WithNavigation(
                McpToolResults.CompilationError(
                    $"Unerwarteter Fehler in der Assembly-Roslyn-Route: {exception.Message}",
                    target.CanonicalPath),
                target);
        }
        finally
        {
            lease.Dispose();
            if (assemblyRegistry is IAssemblyAnalysisTemporaryReferenceEvictor evictor)
            {
                await evictor.EvictTemporaryReferenceSessionsAsync().ConfigureAwait(false);
            }
        }
    }

    internal static CallToolResult UnsupportedAssemblyTarget(string? canonicalPath = null) =>
        canonicalPath is null
            ? McpToolResults.Recoverable(
                LinterErrorCodes.AssemblyTargetUnsupported,
                "Ein Assembly-Ziel wird für dieses Tool noch nicht unterstützt.",
                hint: "Eine unterstützte Source-Operation mit targetPath auf eine .sln/.slnx-Datei verwenden.")
            : AssemblyAnalysisResponse.Unsupported(canonicalPath);

    private static CallToolResult UnsupportedProjectTarget() =>
        McpToolResults.Recoverable(
            LinterErrorCodes.InvalidArgument,
            "Dieses Tool unterstützt kein Projekt-Ziel.",
            hint: "targetPath auf eine vorhandene .dll/.exe-Datei setzen und eine Assembly-Operation verwenden.");
}

internal sealed record AnalysisToolCallRequest(
    AnalysisTargetRequest Target,
    AnalysisToolDispatch Dispatch,
    CancellationToken CancellationToken = default);

internal static class AnalysisToolCall
{
    /// <summary>
    /// Erstellt die neue Herkunftsroute. Die Auswahl erfolgt ausschliesslich aus
    /// dem aufgeloesten targetPath, nicht aus einem zweiten Agentenparameter.
    /// </summary>
    internal static AnalysisToolRoute CreateTargetPathRoute(
        AnalysisToolRoute sourceRoute,
        AnalysisToolRoute assemblyRoute) => request =>
    {
        var resolution = AnalysisTargetResolver.ResolveTargetPathOnly(request.Target);
        if (resolution.Error is not null)
        {
            return Task.FromResult(resolution.Error);
        }

        var target = resolution.Target!;
        var canonicalRequest = request with
        {
            Target = new AnalysisTargetRequest(target.CanonicalPath),
        };
        return target.Origin == AnalysisTargetOrigin.Decompiled
            ? assemblyRoute(canonicalRequest)
            : sourceRoute(canonicalRequest);
    };

    /// <summary>
    /// Gemeinsamer Lint-Einstieg fuer den neuen Vertrag. Decompiled-Targets
    /// werden strukturell als unsupported gemeldet und nie an den Source-Lease
    /// weitergereicht.
    /// </summary>
    internal static async Task<CallToolResult> ExecuteLintAsync(
        AnalysisTargetRequest request,
        Func<AnalysisTarget, Task<CallToolResult>> sourceCall)
    {
        var resolution = AnalysisTargetResolver.ResolveTargetPathOnly(request);
        if (resolution.Error is not null)
        {
            return resolution.Error;
        }

        var target = resolution.Target!;
        return target.Origin == AnalysisTargetOrigin.Decompiled
            ? McpToolResults.WithNavigation(
                AssemblyAnalysisDispatcher.UnsupportedAssemblyTarget(target.CanonicalPath),
                target)
            : McpToolResults.WithNavigation(await sourceCall(target), target);
    }

    internal static AnalysisToolRoute CreateTargetRoute(
        AnalysisToolRoute projectRoute,
        AnalysisToolRoute assemblyRoute) =>
        CreateTargetPathRoute(projectRoute, assemblyRoute);

    internal static Task<CallToolResult> ExecuteRouted(
        AnalysisToolRoute route,
        AnalysisToolCallRequest request) =>
        route(request);

}
