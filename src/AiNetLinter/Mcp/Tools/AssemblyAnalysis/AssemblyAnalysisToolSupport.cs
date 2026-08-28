#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Configuration;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Assemblies;
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

    internal static async Task<CallToolResult> ExecuteAsync(
        AssemblyToolExecutionParameters parameters,
        AssemblySourceSelectionOrchestrator orchestrator)
    {
        ArgumentNullException.ThrowIfNull(orchestrator);

        if (!TryPrepareInput(
                parameters.State,
                parameters.AssemblyPath,
                out var fullPath,
                out var inputError))
        {
            return inputError!;
        }

        using var source = await orchestrator.ResolveAsync(
            fullPath!,
            parameters.CancellationToken).ConfigureAwait(false);
        var (context, error) = await AssemblyAnalysisService.CreateContextAsync(
            new AssemblyAnalysisContextRequest(
                fullPath!,
                parameters.State?.GetCurrentSolution(),
                parameters.ReceiverType,
                source.Selection,
                parameters.CancellationToken)).ConfigureAwait(false);
        if (context is null)
        {
            var diagnosticText = FormatExternalDiagnostics(source.Diagnostics);
            return McpToolResults.CompilationError(
                AppendDiagnostics(error ?? "Assembly konnte nicht analysiert werden.", diagnosticText),
                fullPath);
        }

        var diagnostics = context.Diagnostics
            .Concat(FormatExternalDiagnostics(source.Diagnostics))
            .Where(diagnostic => !string.IsNullOrWhiteSpace(diagnostic))
            .Distinct(StringComparer.Ordinal)
            .Take(100)
            .ToList();
        var enrichedContext = context with { Diagnostics = diagnostics };
        return parameters.BuildResult(fullPath!, enrichedContext, parameters.MaxResults);
    }

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
            return new(fullPath, null, McpToolResults.CompilationError(
                error ?? "Assembly konnte nicht analysiert werden.",
                fullPath));
        }

        return new(fullPath, context, null);
    }

    private static bool TryPrepareInput(
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
                "assemblyPath muss ein existierender absoluter lokaler .dll-Pfad sein.");
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

    private static IReadOnlyList<string> FormatExternalDiagnostics(
        IEnumerable<ExternalSourceConfigurationDiagnostic> diagnostics) =>
        diagnostics
            .Where(diagnostic => !string.IsNullOrWhiteSpace(diagnostic.Message))
            .Select(diagnostic =>
                $"External-Source-Diagnose [{diagnostic.Severity}] {diagnostic.Code}: {diagnostic.Message} ({diagnostic.Location})")
            .Distinct(StringComparer.Ordinal)
            .Take(100)
            .ToList();

    private static string AppendDiagnostics(string message, IReadOnlyList<string> diagnostics) =>
        diagnostics.Count == 0
            ? message
            : $"{message} {string.Join(" ", diagnostics)}";
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
