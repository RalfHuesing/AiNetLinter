#nullable enable

using System;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Projects;
using AiNetLinter.Output;
using ModelContextProtocol;

namespace AiNetLinter.Mcp.Registration;

internal static class ProjectResourceLease
{
    internal static TResult Execute<TResult>(
        ProjectRegistry registry,
        string? targetPath,
        Func<ProjectSnapshot, TResult> render)
    {
        var guard = ProjectToolCall.GuardRequiredAbsoluteRoot(targetPath);
        if (guard is not null)
        {
            throw new McpException(ProjectToolCall.FormatGuard(guard));
        }

        var definition = ProjectDefinitionLoader.LoadSolutionTarget(targetPath);
        if (!definition.Succeeded || definition.Definition is null)
        {
            throw new McpException(LinterErrorFormatter.Format(
                definition.ErrorCode!,
                definition.Message!,
                hint: ProjectToolCall.RecoverHint(definition.ErrorCode!)));
        }

        var leaseResult = registry.Lease(definition.Definition.SolutionPath);
        if (!leaseResult.Succeeded || leaseResult.Lease is null)
        {
            throw new McpException(LinterErrorFormatter.Format(
                leaseResult.ErrorCode!,
                leaseResult.ErrorMessage!,
                hint: ProjectToolCall.RecoverHint(leaseResult.ErrorCode!)));
        }

        using var lease = leaseResult.Lease;
        if (lease.Server.LoadState == ServerLoadState.LoadFailed)
        {
            var failure = ProjectToolCall.BuildLoadFailure(lease.Server, lease);
            throw new McpException(LinterErrorFormatter.Format(
                ProjectErrorCodes.ProjectLoadFailed,
                failure.Message,
                context: failure.Context,
                hint: failure.Hint));
        }

        return render(registry.SnapshotFor(lease));
    }
}
