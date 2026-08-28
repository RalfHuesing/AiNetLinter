#nullable enable

using System.Threading.Tasks;
using AiNetLinter.Mcp.Projects;
using AiNetLinter.Output;
using ModelContextProtocol.Protocol;

namespace AiNetLinter.Mcp;

internal static class AnalysisToolCall
{
    internal static Task<CallToolResult> ExecuteAsync(
        ProjectRegistry registry,
        string? targetType,
        string? targetPath,
        Func<ProjectLease, Task<CallToolResult>> projectCall) =>
        ExecuteAsync(
            registry,
            new AnalysisTargetRequest(targetType, targetPath),
            new AnalysisToolDispatch(ProjectCall: projectCall));

    internal static Task<CallToolResult> ExecuteAssemblyAsync(
        ProjectRegistry registry,
        string? targetType,
        string? targetPath,
        Func<string, Task<CallToolResult>> assemblyCall) =>
        ExecuteAsync(
            registry,
            new AnalysisTargetRequest(targetType, targetPath),
            new AnalysisToolDispatch(AssemblyCall: assemblyCall));

    internal static async Task<CallToolResult> ExecuteAsync(
        ProjectRegistry registry,
        AnalysisTargetRequest request,
        AnalysisToolDispatch dispatch)
    {
        var resolution = AnalysisTargetResolver.Resolve(request);
        if (resolution.Error is not null)
        {
            return resolution.Error;
        }

        var target = resolution.Target!;
        if (target.TargetType == AnalysisTargetType.Project)
        {
            if (dispatch.ProjectCall is null)
            {
                return UnsupportedProjectTarget();
            }

            return await ProjectToolCall.ExecuteAsync(registry, target.CanonicalPath, dispatch.ProjectCall);
        }

        if (dispatch.AssemblyCall is null)
        {
            return UnsupportedAssemblyTarget();
        }

        return await dispatch.AssemblyCall(target.CanonicalPath);
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

    internal static CallToolResult UnsupportedAssemblyTarget() =>
        McpToolResults.Recoverable(
            LinterErrorCodes.AssemblyTargetUnsupported,
            "Ein Assembly-Ziel wird für dieses Tool noch nicht unterstützt.",
            hint: "targetType='project' für dieses Tool verwenden; Assembly-Analyse folgt über die spezialisierten Assembly-Tools.");

    private static CallToolResult UnsupportedProjectTarget() =>
        McpToolResults.Recoverable(
            LinterErrorCodes.InvalidArgument,
            "Dieses Tool unterstützt kein Projekt-Ziel.",
            hint: "targetType='assembly' mit dem Pfad der zu untersuchenden DLL verwenden.");
}
