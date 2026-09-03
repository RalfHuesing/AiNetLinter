#nullable enable

using System.Threading.Tasks;
using AiNetLinter.Mcp.Assemblies.Analysis;
using AiNetLinter.Mcp.Assemblies.Analysis.References;
using AiNetLinter.Mcp.Projects;
using AiNetLinter.Output;
using ModelContextProtocol.Protocol;

namespace AiNetLinter.Mcp;

internal delegate Task<CallToolResult> AnalysisToolRoute(AnalysisToolCallRequest request);

internal static class ProjectAnalysisDispatcher
{
    internal static AnalysisToolRoute CreateRoute(ProjectRegistry registry) => request =>
        request.Dispatch.ProjectCall is null
            ? Task.FromResult(UnsupportedProjectTarget())
            : ExecuteAsync(registry, request.Target, request.Dispatch.ProjectCall);

    internal static Task<CallToolResult> ExecuteAsync(
        ProjectRegistry registry,
        AnalysisTargetRequest request,
        Func<ProjectLease, Task<CallToolResult>> projectCall) =>
        ExecuteProjectAsync(registry, request, projectCall);

    internal static Task<CallToolResult> ExecuteAsync(
        ProjectRegistry registry,
        string? targetType,
        string? targetPath,
        Func<ProjectLease, Task<CallToolResult>> projectCall) =>
        ExecuteProjectAsync(registry, new AnalysisTargetRequest(targetType, targetPath), projectCall);

    internal static Task<CallToolResult> ExecuteAssemblyAsync(
        ProjectRegistry registry,
        string? targetType,
        string? targetPath,
        Func<string, Task<CallToolResult>> assemblyCall) =>
        ExecuteAssemblyAsync(new AnalysisTargetRequest(targetType, targetPath), assemblyCall);

    private static async Task<CallToolResult> ExecuteProjectAsync(
        ProjectRegistry registry,
        AnalysisTargetRequest request,
        Func<ProjectLease, Task<CallToolResult>> projectCall)
    {
        var resolution = AnalysisTargetResolver.Resolve(request);
        if (resolution.Error is not null)
        {
            return resolution.Error;
        }

        var target = resolution.Target!;
        if (target.TargetType == AnalysisTargetType.Project)
        {
            return await ProjectToolCall.ExecuteAsync(registry, target.CanonicalPath, projectCall);
        }

        return UnsupportedAssemblyTarget(target.CanonicalPath);
    }

    private static async Task<CallToolResult> ExecuteAssemblyAsync(
        AnalysisTargetRequest request,
        Func<string, Task<CallToolResult>> assemblyCall)
    {
        var resolution = AnalysisTargetResolver.Resolve(request);
        if (resolution.Error is not null)
        {
            return resolution.Error;
        }

        return resolution.Target!.TargetType == AnalysisTargetType.Assembly
            ? await assemblyCall(resolution.Target.CanonicalPath)
            : UnsupportedProjectTarget();
    }

    internal static async Task<CallToolResult> ExecuteFilesystemAsync(
        ProjectRegistry registry,
        AnalysisTargetRequest request,
        Func<ProjectLease, Task<CallToolResult>> projectCall)
    {
        var resolution = AnalysisTargetResolver.Resolve(request);
        if (resolution.Error is not null)
        {
            return resolution.Error;
        }

        if (resolution.Target!.TargetType == AnalysisTargetType.Assembly)
        {
            return UnsupportedAssemblyTarget(resolution.Target.CanonicalPath);
        }

        return await ProjectToolCall.ExecuteFilesystemAsync(
            registry,
            resolution.Target.CanonicalPath,
            projectCall);
    }

    /// <summary>
    /// Resolves a physical project target without leasing a Roslyn project session. This is the
    /// direct route for read-only filesystem discovery, including materialized decompiler roots
    /// that deliberately have no <c>ainetlinter.project.json</c> registration of their own.
    /// </summary>
    internal static async Task<CallToolResult> ExecutePhysicalFilesystemAsync(
        AnalysisTargetRequest request,
        Func<string, Task<CallToolResult>> filesystemCall)
    {
        var resolution = AnalysisTargetResolver.Resolve(request);
        if (resolution.Error is not null)
        {
            return resolution.Error;
        }

        var target = resolution.Target!;
        return target.TargetType == AnalysisTargetType.Project
            ? await filesystemCall(target.CanonicalPath)
            : UnsupportedAssemblyTarget(target.CanonicalPath);
    }

    internal static CallToolResult UnsupportedAssemblyTarget(string? canonicalPath = null) =>
        canonicalPath is null
            ? McpToolResults.Recoverable(
                LinterErrorCodes.AssemblyTargetUnsupported,
                "Ein Assembly-Ziel wird für dieses Tool noch nicht unterstützt.",
                hint: "targetType='project' für dieses Tool verwenden.")
            : AssemblyAnalysisResponse.Unsupported(canonicalPath);

    internal static CallToolResult UnsupportedProjectTarget() =>
        McpToolResults.Recoverable(
            LinterErrorCodes.InvalidArgument,
            "Dieses Tool unterstützt kein Projekt-Ziel.",
            hint: "targetType='assembly' mit dem Pfad der zu untersuchenden DLL verwenden.");
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
                    request.Dispatch.Cursor));

    private static Task<CallToolResult> UnsupportedRouteAsync(AnalysisToolCallRequest request)
    {
        var resolution = AnalysisTargetResolver.Resolve(request.Target);
        if (resolution.Error is not null)
        {
            return Task.FromResult(resolution.Error);
        }

        var result = resolution.Target!.TargetType == AnalysisTargetType.Assembly
            ? UnsupportedAssemblyTarget(resolution.Target.CanonicalPath)
            : UnsupportedProjectTarget();
        return Task.FromResult(result);
    }

    internal static async Task<CallToolResult> ExecuteAsync(
        IAssemblyAnalysisRegistry? assemblyRegistry,
        AnalysisTargetRequest request,
        Func<AssemblyAnalysisLease, Task<CallToolResult>> assemblyCall,
        AssemblyAnalysisExecutionOptions options)
    {
        var resolution = AnalysisTargetResolver.Resolve(request);
        if (resolution.Error is not null)
        {
            return resolution.Error;
        }

        var target = resolution.Target!;
        if (target.TargetType == AnalysisTargetType.Project)
        {
            return UnsupportedProjectTarget();
        }

        if (assemblyRegistry is null)
        {
            return UnsupportedAssemblyTarget(target.CanonicalPath);
        }

        var leaseResult = await assemblyRegistry.LeaseAsync(target.CanonicalPath, options.CancellationToken).ConfigureAwait(false);
        if (leaseResult.Error is not null)
        {
            return leaseResult.Error;
        }

        var lease = leaseResult.Lease!;
        try
        {
            if (options.ExpandAssemblyReferences)
            {
                await lease.ExpandReferencesAsync(options.CancellationToken).ConfigureAwait(false);
            }

            var result = await assemblyCall(lease).ConfigureAwait(false);
            return AssemblyAnalysisResponse.Enrich(
                result,
                lease,
                new AssemblyAnalysisResponseRequest(
                    options.MaxResponseBytes,
                    options.DetailLevel,
                    options.Cursor));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return McpToolResults.CompilationError(
                $"Unerwarteter Fehler in der Assembly-Roslyn-Route: {exception.Message}",
                target.CanonicalPath);
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
                hint: "targetType='project' für dieses Tool verwenden.")
            : AssemblyAnalysisResponse.Unsupported(canonicalPath);

    private static CallToolResult UnsupportedProjectTarget() =>
        McpToolResults.Recoverable(
            LinterErrorCodes.InvalidArgument,
            "Dieses Tool unterstützt kein Projekt-Ziel.",
            hint: "targetType='assembly' mit dem Pfad der zu untersuchenden DLL verwenden.");
}

internal sealed record AnalysisToolCallRequest(
    AnalysisTargetRequest Target,
    AnalysisToolDispatch Dispatch,
    CancellationToken CancellationToken = default);

internal static class AnalysisToolCall
{
    internal static AnalysisToolRoute CreateTargetRoute(
        AnalysisToolRoute projectRoute,
        AnalysisToolRoute assemblyRoute) => request =>
            string.Equals(request.Target.TargetType, "assembly", StringComparison.OrdinalIgnoreCase)
                ? assemblyRoute(request)
                : projectRoute(request);

    internal static Task<CallToolResult> ExecuteRouted(
        AnalysisToolRoute route,
        AnalysisToolCallRequest request) =>
        route(request);
}
