#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp;
using ModelContextProtocol.Protocol;

namespace AiNetLinter.Mcp.Tools.AssemblyAnalysis;

internal static class AssemblyAnalysisToolSupport
{
    internal static async Task<CallToolResult> ExecuteAsync(
        AssemblyToolExecutionParameters parameters)
    {
        var preparation = await PrepareAsync(
            parameters.State,
            parameters.AssemblyPath,
            parameters.ReceiverType,
            parameters.CancellationToken);
        if (preparation.Error is not null)
        {
            return preparation.Error;
        }

        return parameters.BuildResult(preparation.FullPath!, preparation.Context!, parameters.MaxResults);
    }

    internal static async Task<AssemblyToolPreparation> PrepareAsync(
        McpCodeGraphServer? state,
        string? assemblyPath,
        string? receiverType,
        CancellationToken ct)
    {
        if (!AssemblyAnalysisService.TryValidatePath(assemblyPath, out var fullPath, out var pathError))
        {
            return new(null, null, McpToolResults.InvalidArgument(
                pathError,
                "assemblyPath muss ein existierender absoluter lokaler .dll-Pfad sein."));
        }

        if (state?.LoadState == ServerLoadState.Loading)
        {
            return new(fullPath, null, McpToolResults.Loading());
        }

        var (context, error) = await AssemblyAnalysisService.CreateContextAsync(
            fullPath,
            state?.GetCurrentSolution(),
            receiverType,
            ct);
        if (context is null)
        {
            return new(fullPath, null, McpToolResults.CompilationError(
                error ?? "Assembly konnte nicht analysiert werden.",
                fullPath));
        }

        return new(fullPath, context, null);
    }
}

internal sealed record AssemblyToolPreparation(
    string? FullPath,
    AssemblyContext? Context,
    CallToolResult? Error);

internal sealed record AssemblyToolExecutionParameters(
    McpCodeGraphServer? State,
    string? AssemblyPath,
    string? ReceiverType,
    int MaxResults,
    CancellationToken CancellationToken,
    Func<string, AssemblyContext, int, CallToolResult> BuildResult);
