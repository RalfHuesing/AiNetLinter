#nullable enable

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Assemblies.Analysis.References;
using AiNetLinter.Mcp.Scope;
using AiNetLinter.Mcp.Tools.Common;
using AiNetLinter.Mcp.Tools.SymbolGraph;
using AiNetLinter.Mcp.Tools.SymbolGraph.Navigation;
using AiNetLinter.Output;
using ModelContextProtocol.Protocol;

namespace AiNetLinter.Mcp.Tools.CallTree;

internal sealed record AssemblyGetCallTreeRequest(
    GetCallTreeInput Input,
    bool IncludeReferences);

internal static class AssemblyGetCallTreeTool
{
    internal static Task<CallToolResult> ExecuteAsync(
        AssemblyAnalysisLease lease,
        AssemblyGetCallTreeRequest request,
        CancellationToken cancellationToken) =>
        ExecuteWithReferencesAsync(lease, request.Input, request.IncludeReferences, cancellationToken);

    private static async Task<CallToolResult> ExecuteWithReferencesAsync(
        AssemblyAnalysisLease lease,
        GetCallTreeInput input,
        bool includeReferences,
        CancellationToken cancellationToken)
    {
        var validationError = ValidateInput(input);
        if (validationError is not null) return validationError;

        try
        {
            return await BuildResponseAsync(lease, input, includeReferences, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return McpToolResults.CompilationError(
                $"Unerwarteter Fehler in get_call_tree: {exception.Message}",
                context: $"{input.SymbolIdentifier}; includeReferences=true");
        }
    }

    private static CallToolResult? ValidateInput(GetCallTreeInput input)
    {
        if (string.IsNullOrEmpty(input.SymbolIdentifier))
        {
            return McpToolResults.Recoverable(
                LinterErrorCodes.InvalidArgument,
                "Pflichtparameter 'symbolIdentifier' fehlt oder ist leer.",
                hint: McpToolResults.SymbolIdentifierHint);
        }

        return GetCallTreeTool.TryParseDirection(input.Direction, out _)
            ? GetCallTreeTool.TryParseFormat(input.Format, out _)
                ? ValidateScopeAndBudget(input)
                : McpToolResults.InvalidArgument(
                    $"Ungueltiger Wert fuer 'format': '{input.Format}'.",
                    "format muss 'ascii' oder 'mermaid' sein (Default: 'ascii').",
                    "$.format")
            : McpToolResults.Recoverable(
                LinterErrorCodes.InvalidArgument,
                $"Ungueltiger Wert fuer 'direction': '{input.Direction}'.",
                hint: "direction muss 'incoming', 'outgoing' oder 'both' sein.");
    }

    private static CallToolResult? ValidateScopeAndBudget(GetCallTreeInput input)
    {
        var scope = FindSymbolTool.ValidateScopeType(input.ScopeType);
        if (scope.Error is not null) return scope.Error;
        if (input.MaxResponseBytes > McpResponseBudgetLimits.MaxBytes)
        {
            return McpToolResults.InvalidArgument(
                $"maxResponseBytes darf höchstens {McpResponseBudgetLimits.MaxBytes} sein.",
                "maxResponseBytes auf höchstens 65536 setzen.",
                "$.maxResponseBytes");
        }

        return input.MaxResponseBytes > 0
            && input.MaxResponseBytes < McpResponseBudgetLimits.MinimumStructuredBytes
            ? McpToolResults.InvalidArgument(
                $"maxResponseBytes muss mindestens {McpResponseBudgetLimits.MinimumStructuredBytes} Bytes betragen.",
                "maxResponseBytes weglassen oder mindestens 512 setzen.",
                "$.maxResponseBytes")
            : null;
    }

    private static async Task<CallToolResult> BuildResponseAsync(
        AssemblyAnalysisLease lease,
        GetCallTreeInput input,
        bool includeReferences,
        CancellationToken cancellationToken)
    {
        var plan = AssemblySearchPlan.Create(input.SymbolIdentifier, includeReferences);
        var (target, error, navigation) = await AssemblySymbolResolver.ResolveAsync(
            lease,
            input.SymbolIdentifier!,
            plan,
            cancellationToken).ConfigureAwait(false);
        if (error is not null) return error;

        var graphResult = await AssemblyReferenceNavigator.BuildCallGraphAsync(
            AssemblyNavigationSourceFactory.CreateSources(lease, target!, plan),
            target!.Symbol,
            input,
            cancellationToken).ConfigureAwait(false);
        var diagnostics = graphResult.Diagnostics
            .Concat(lease.Context.Diagnostics)
            .Concat(AssemblyNavigationSupport.CreateExpansionDiagnostics(
                AssemblyNavigationLeaseAccess.CreateView(lease)))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var topN = input.TopN < 1 ? 1 : input.TopN;
        var effectiveDepth = Math.Clamp(input.Depth, 1, CallGraphTreeBuilder.MaxCallTreeDepth);
        return TransitiveCallGraphFormatter.FormatAssemblyCallGraphResponse(
            new AssemblyCallGraphResponseRequest(
                graphResult.Graph,
                AssemblyNavigationSupport.ParseDirection(input.Direction),
                input.Format,
                navigation,
                diagnostics,
                graphResult.Truncated,
                input.Depth,
                effectiveDepth,
                input.Depth != effectiveDepth,
                input.TopN < 1 ? 1 : input.TopN,
                FindSymbolTool.ValidateScopeType(input.ScopeType).ScopeType,
                input.IncludeGenerated,
                input.MaxResponseBytes));
    }
}
