#nullable enable

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Configuration;
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

        if (source.Selection is null
            && source.SourceMode is ExternalSourceSourceMode.SourceRequired)
        {
            return CreateSourceRequiredFailure(fullPath!, source.Diagnostics, sourceMode: source.SourceMode);
        }

        var (context, error) = await AssemblyAnalysisService.CreateContextAsync(
            new AssemblyAnalysisContextRequest(
                fullPath!,
                parameters.State?.GetCurrentSolution(),
                parameters.ReceiverType,
                source.Selection,
                parameters.CancellationToken,
                source.Fallback,
                source.SourceMode)).ConfigureAwait(false);
        if (context is null)
        {
            var diagnosticText = AssemblyAnalysisDiagnostics.FormatExternalDiagnostics(source.Diagnostics);
            if (source.SourceMode is ExternalSourceSourceMode.SourceRequired)
            {
                return CreateSourceRequiredFailure(fullPath!, source.Diagnostics, error, source.SourceMode);
            }

            return McpToolResults.CompilationError(
                AssemblyAnalysisSourceConfigurationSupport.AppendDiagnostics(
                    error ?? "Assembly konnte nicht analysiert werden.", diagnosticText),
                fullPath);
        }

        var enrichedContext = EnrichContext(context, source.Diagnostics, source.SourceMode);
        if (source.SourceMode is ExternalSourceSourceMode.SourceRequired
            && enrichedContext.Origin.IsDecompiled)
        {
            return CreateSourceRequiredFailure(fullPath!, source.Diagnostics, sourceMode: source.SourceMode);
        }

        return parameters.BuildResult(fullPath!, enrichedContext, parameters.MaxResults);
    }

    private static AssemblyContext EnrichContext(
        AssemblyContext context,
        System.Collections.Generic.IReadOnlyList<ExternalSourceConfigurationDiagnostic> diagnostics,
        ExternalSourceSourceMode sourceMode)
    {
        var mergedDiagnostics = context.Diagnostics
            .Concat(AssemblyAnalysisDiagnostics.FormatExternalDiagnostics(diagnostics))
            .Where(diagnostic => !string.IsNullOrWhiteSpace(diagnostic))
            .Distinct(StringComparer.Ordinal)
            .Take(100)
            .ToList();
        return context with
        {
            Diagnostics = mergedDiagnostics,
            Origin = context.Origin with
            {
                SourcePolicy = sourceMode.ToWireValue(),
            },
        };
    }

    private static CallToolResult CreateSourceRequiredFailure(
        string fullPath,
        System.Collections.Generic.IReadOnlyList<ExternalSourceConfigurationDiagnostic> diagnostics,
        string? additionalReason = null,
        ExternalSourceSourceMode sourceMode = ExternalSourceSourceMode.SourceRequired)
    {
        var details = AssemblyAnalysisDiagnostics.FormatExternalDiagnostics(diagnostics)
            .Concat(additionalReason is null ? Array.Empty<string>() : [additionalReason])
            .Where(detail => !string.IsNullOrWhiteSpace(detail))
            .Take(3);
        return McpToolResults.Recoverable(
            ExternalSourceConfigurationDiagnosticCodes.SourceRequiredUnavailable,
            $"Source-Policy {sourceMode.ToWireValue()} verweigert die Dekompilation für '{fullPath}': " +
            string.Join(" ", details),
            context: fullPath,
            hint: "Originalquelle, Revision und Mapping prüfen; source_preferred oder decompilation_allowed nur bewusst verwenden.");
    }
}
