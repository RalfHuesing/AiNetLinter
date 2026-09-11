#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Assemblies.Analysis.References;
using AiNetLinter.Mcp.Handoffs;
using AiNetLinter.Mcp.Scope;
using AiNetLinter.Output;
using ModelContextProtocol.Protocol;

namespace AiNetLinter.Mcp.Tools.SymbolGraph;

internal sealed record AssemblyFindReferencesRequest(
    string? SymbolIdentifier,
    int MaxResults,
    int Depth,
    bool IncludeReferences,
    McpScopeType ScopeType = McpScopeType.All,
    bool IncludeGenerated = false,
    McpScopeClassifier? ScopeClassifier = null);

internal static class AssemblyFindReferencesTool
{
    internal static async Task<CallToolResult> ExecuteAsync(
        AssemblyAnalysisLease lease,
        AssemblyFindReferencesRequest request,
        CancellationToken cancellationToken)
    {
        var requiresReferenceExpansion = IsAssemblyHandoff(request.SymbolIdentifier);
        if (request.IncludeReferences || requiresReferenceExpansion)
        {
            if (requiresReferenceExpansion && !request.IncludeReferences)
            {
                await lease.ExpandReferencesAsync(cancellationToken).ConfigureAwait(false);
            }

            return await ExecuteWithReferencesAsync(lease, request, cancellationToken).ConfigureAwait(false);
        }

        return await FindReferencesTool.ExecuteAsync(
            lease.Server,
            new FindReferencesRequest(
                request.SymbolIdentifier,
                request.MaxResults,
                request.Depth,
                request.ScopeType,
                request.IncludeGenerated,
                request.ScopeClassifier),
            cancellationToken).ConfigureAwait(false);
    }

    private static bool IsAssemblyHandoff(string? symbolIdentifier) =>
        symbolIdentifier is not null
        && SymbolHandoffIdentifier.TryParse(symbolIdentifier, out var handoff)
        && handoff.Origin == SymbolHandoffOrigin.Assembly;

    private static async Task<CallToolResult> ExecuteWithReferencesAsync(
        AssemblyAnalysisLease lease,
        AssemblyFindReferencesRequest request,
        CancellationToken cancellationToken)
    {
        var symbolIdentifier = request.SymbolIdentifier;
        if (string.IsNullOrEmpty(symbolIdentifier))
        {
            return McpToolResults.Recoverable(
                LinterErrorCodes.InvalidArgument,
                "Pflichtparameter 'symbolIdentifier' fehlt oder ist leer.",
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
                    navigation,
                    lease.CanonicalPath,
                    new(
                        request.ScopeType,
                        request.IncludeGenerated,
                        request.ScopeClassifier ?? new McpScopeClassifier())),
                cancellationToken).ConfigureAwait(false);
            var formatted = TransitiveCallGraphFormatter.FormatResponse(
                traversal,
                traversal.Completeness.TotalCallSiteCount == 0
                    ? $"Keine Aufrufstellen gefunden fuer '{request.SymbolIdentifier}'"
                    : null);
            return McpToolResults.Text(formatted.Text, formatted.StructuredPayload);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return McpToolResults.CompilationError(
                $"Unerwarteter Fehler in find_references: {exception.Message}",
                context: $"{request.SymbolIdentifier}; includeReferences=true");
        }
    }
}
