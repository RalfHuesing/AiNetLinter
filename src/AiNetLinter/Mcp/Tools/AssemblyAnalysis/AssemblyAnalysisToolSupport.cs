#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Configuration;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Assemblies;
using AiNetLinter.Mcp.Assemblies.Analysis;
using AiNetLinter.Mcp.Assemblies.Analysis.References;
using AiNetLinter.Output;
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

    internal static Task<CallToolResult> ExecuteLeaseAsync<TArgs>(
        AssemblyAnalysisLease lease,
        TArgs arguments,
        int rawMaxResults,
        Func<string, AssemblyContext, TArgs, int, AssemblyAnalysisLease, CallToolResult> buildResult) =>
        Task.FromResult(buildResult(
            lease.CanonicalPath,
            lease.Context,
            arguments,
            AssemblyAnalysisService.NormalizeLimit(rawMaxResults, 1, AssemblyAnalysisService.MaxResults),
            lease));

    internal static async Task<AssemblyToolPreparation> PrepareAsync(
        McpCodeGraphServer? state,
        string? assemblyPath,
        string? receiverType,
        CancellationToken ct)
    {
        if (!TryPrepareInput(state, assemblyPath, out var fullPath, out var inputError))
        {
            return new(fullPath, null, inputError);
        }

        var (context, error) = await AssemblyAnalysisService.CreateContextAsync(
            fullPath!,
            state?.GetCurrentSolution(),
            receiverType,
            ct);
        if (context is null)
        {
            return new(fullPath, null, McpToolResults.Recoverable(
                LinterErrorCodes.WorkspaceDiagnostic,
                error ?? "Assembly konnte nicht analysiert werden.",
                context: fullPath,
                hint: "Einmal erneut versuchen; bleibt der Fehler bestehen, Datei pruefen — Compile-Fehler blockieren Symbolaufloesung."));
        }

        return new(fullPath, context, null);
    }

    internal static bool TryPrepareInput(
        McpCodeGraphServer? state,
        string? assemblyPath,
        out string? fullPath,
        out CallToolResult? error)
    {
        if (!AssemblyAnalysisService.TryValidatePath(assemblyPath, out var validatedPath, out var pathError))
        {
            fullPath = null;
            error = McpToolResults.InvalidArgument(
                pathError,
                "assemblyPath muss ein existierender absoluter lokaler .dll- oder .exe-Pfad sein.");
            return false;
        }

        fullPath = validatedPath;
        if (state?.LoadState == ServerLoadState.Loading)
        {
            error = McpToolResults.Loading();
            return false;
        }

        error = null;
        return true;
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
