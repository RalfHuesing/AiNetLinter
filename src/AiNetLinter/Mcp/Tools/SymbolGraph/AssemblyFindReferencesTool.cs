#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Assemblies.Analysis.References;
using AiNetLinter.Output;
using ModelContextProtocol.Protocol;

namespace AiNetLinter.Mcp.Tools.SymbolGraph;

internal sealed record AssemblyFindReferencesRequest(
    string? SymbolIdentifier,
    int MaxResults,
    int Depth,
    bool IncludeReferences,
    string? Symbol = null)
{
    public string? EffectiveSymbolIdentifier =>
        !string.IsNullOrWhiteSpace(SymbolIdentifier) ? SymbolIdentifier : Symbol;
}

internal static class AssemblyFindReferencesTool
{
    internal static Task<CallToolResult> ExecuteAsync(
        AssemblyAnalysisLease lease,
        AssemblyFindReferencesRequest request,
        CancellationToken cancellationToken) =>
        request.IncludeReferences
            ? ExecuteWithReferencesAsync(lease, request, cancellationToken)
            : FindReferencesTool.ExecuteAsync(
                lease.Server,
                new FindReferencesRequest(
                    request.EffectiveSymbolIdentifier,
                    request.MaxResults,
                    request.Depth),
                cancellationToken);

    private static async Task<CallToolResult> ExecuteWithReferencesAsync(
        AssemblyAnalysisLease lease,
        AssemblyFindReferencesRequest request,
        CancellationToken cancellationToken)
    {
        var symbolIdentifier = request.EffectiveSymbolIdentifier;
        if (string.IsNullOrEmpty(symbolIdentifier))
        {
            return McpToolResults.Recoverable(
                LinterErrorCodes.InvalidArgument,
                "Pflichtparameter 'symbolIdentifier' (oder 'symbol') fehlt oder ist leer.",
                hint: McpToolResults.SymbolIdentifierHint);
        }

        try
        {
            var (target, error, navigation) = await AssemblySymbolResolver.ResolveAsync(
                lease,
                symbolIdentifier,
                cancellationToken).ConfigureAwait(false);
            if (error is not null) return error;

            var traversal = await AssemblyReferenceNavigator.FindReferencesAsync(
                new AssemblyReferenceTraversalRequest(
                    AssemblyNavigationSourceFactory.CreateSources(lease, target!),
                    request.MaxResults,
                    request.Depth,
                    navigation),
                cancellationToken).ConfigureAwait(false);
            var formatted = TransitiveCallGraphFormatter.FormatResponse(
                traversal,
                traversal.Completeness.TotalCallSiteCount == 0
                    ? $"Keine Aufrufstellen gefunden fuer '{request.SymbolIdentifier}'"
                    : null);
            var finalBody = TransitiveCallGraphFormatter.IsComplete(formatted.Traversal)
                ? McpSufficiencyHints.Append(formatted.Text)
                : formatted.Text;
            return McpToolResults.Text(finalBody, formatted.Traversal);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return McpToolResults.CompilationError(
                $"Unerwarteter Fehler in find_references: {exception.Message}",
                context: $"{request.SymbolIdentifier}; includeReferences=true");
        }
    }
}
