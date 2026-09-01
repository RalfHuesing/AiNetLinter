#nullable enable

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Assemblies;
using AiNetLinter.Mcp.Assemblies.Analysis;
using ModelContextProtocol.Protocol;

namespace AiNetLinter.Mcp.Tools.AssemblyAnalysis;

internal static class AssemblyAnalysisSourceToolSupport
{
    internal static async Task<CallToolResult> ExecuteAsync(
        AssemblyToolExecutionParameters parameters,
        IAssemblySourceSelectionResolver orchestrator,
        Action<AssemblySourceSelectionScope>? observeScope = null)
    {
        ArgumentNullException.ThrowIfNull(orchestrator);

        if (!AssemblyAnalysisToolSupport.TryPrepareInput(
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
        observeScope?.Invoke(source);
        if (source.Status is AssemblySourceSelectionStatus.ConfigurationFailure)
        {
            return AssemblyAnalysisSourceConfigurationSupport.CreateConfigurationFailureResult(source, fullPath!);
        }

        var (context, error) = await AssemblyAnalysisService.CreateContextAsync(
            new AssemblyAnalysisContextRequest(
                fullPath!,
                parameters.State?.GetCurrentSolution(),
                parameters.ReceiverType,
                source.Selection,
                parameters.CancellationToken,
                source.Fallback)).ConfigureAwait(false);
        if (context is null)
        {
            var diagnosticText = AssemblyAnalysisDiagnostics.FormatExternalDiagnostics(source.Diagnostics);
            return McpToolResults.CompilationError(
                AssemblyAnalysisSourceConfigurationSupport.AppendDiagnostics(
                    error ?? "Assembly konnte nicht analysiert werden.", diagnosticText),
                fullPath);
        }

        var diagnostics = context.Diagnostics
            .Concat(AssemblyAnalysisDiagnostics.FormatExternalDiagnostics(source.Diagnostics))
            .Where(diagnostic => !string.IsNullOrWhiteSpace(diagnostic))
            .Distinct(StringComparer.Ordinal)
            .Take(100)
            .ToList();
        var enrichedContext = context with { Diagnostics = diagnostics };
        return parameters.BuildResult(fullPath!, enrichedContext, parameters.MaxResults);
    }
}
