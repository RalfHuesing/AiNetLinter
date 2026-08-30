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
            return UnsupportedAssemblyTarget();
        }

        return await ProjectToolCall.ExecuteFilesystemAsync(
            registry,
            resolution.Target.CanonicalPath,
            projectCall);
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
            ? Task.FromResult(UnsupportedAssemblyTarget())
            : ExecuteAsync(
                assemblyRegistry,
                request.Target,
                request.Dispatch.AssemblySessionCall,
                request.CancellationToken);

    internal static async Task<CallToolResult> ExecuteAsync(
        IAssemblyAnalysisRegistry? assemblyRegistry,
        AnalysisTargetRequest request,
        Func<AssemblyAnalysisLease, Task<CallToolResult>> assemblyCall,
        CancellationToken cancellationToken = default)
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

        var leaseResult = await assemblyRegistry.LeaseAsync(target.CanonicalPath, cancellationToken).ConfigureAwait(false);
        if (leaseResult.Error is not null)
        {
            return leaseResult.Error;
        }

        using var lease = leaseResult.Lease!;
        try
        {
            await lease.ExpandReferencesAsync(cancellationToken).ConfigureAwait(false);
            var result = await assemblyCall(lease).ConfigureAwait(false);
            return AssemblyAnalysisResponse.Enrich(result, lease);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return McpToolResults.CompilationError(
                $"Unerwarteter Fehler in der Assembly-Roslyn-Route: {exception.Message}",
                target.CanonicalPath);
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
