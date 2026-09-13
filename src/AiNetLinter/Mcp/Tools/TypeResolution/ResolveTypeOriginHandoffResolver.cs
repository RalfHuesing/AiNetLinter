#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp.Handoffs;
using AiNetLinter.Mcp.Tools.Common;
using AiNetLinter.Mcp.Tools.SymbolGraph;
using Microsoft.CodeAnalysis;
using ModelContextProtocol.Protocol;

namespace AiNetLinter.Mcp.Tools.TypeResolution;

/// <summary>
/// Löst ausschließlich kanonische Typ-Handoffs für <c>resolve_type_origin</c> auf.
/// </summary>
internal static class ResolveTypeOriginHandoffResolver
{
    internal static async Task<(INamedTypeSymbol? Type, CallToolResult? Error)> TryResolveTypeAsync(
        Solution solution,
        string typeName,
        AnalysisSymbolIdentity? expectedIdentity,
        CancellationToken ct)
    {
        var identifier = McpInputNormalizer.StripEnclosingQuotesAndBackticks(typeName);
        if (HandoffCounterAlphabet.IsValidHandle(identifier) || identifier.StartsWith("h:", StringComparison.OrdinalIgnoreCase))
        {
            var restored = HandoffHandleRegistry.Default.RestoreInternalHandoffForInput(identifier);
            if (!restored.IsSuccess)
            {
                return (null, McpToolResults.HandoffError(restored.Error, "$.typeName"));
            }
            identifier = restored.Value!;
        }

        if (!SymbolHandoffIdentifier.IsInternalIdentifier(identifier)) return (null, null);
        if (!SymbolHandoffIdentifier.TryParse(identifier, out var handoff)) return (null, InvalidHandoff());
        if (!handoff.DocumentationCommentId.StartsWith("T:", StringComparison.Ordinal)) return (null, InvalidTypeHandoff());

        var (symbol, error) = await FindReferencesTool.ResolveSymbolAsync(solution, identifier, ct, expectedIdentity).ConfigureAwait(false);
        return error is not null
            ? (null, error)
            : symbol is INamedTypeSymbol type
                ? (type, null)
                : (null, InvalidTypeHandoff());
    }

    private static CallToolResult InvalidHandoff() => McpToolResults.InvalidArgument(
        "typeName enthält keine kanonische Handoff-ID.",
        "Eine Typ-Handoff-ID direkt aus der aktuellen Tool-Antwort kopieren.",
        "$.typeName");

    private static CallToolResult InvalidTypeHandoff() => McpToolResults.InvalidArgument(
        "typeName akzeptiert nur Handoff-IDs für Typen.",
        "Eine Handoff-ID mit dem DocumentationCommentId-Präfix 'T:' oder einen Typnamen übergeben.",
        "$.typeName");
}
